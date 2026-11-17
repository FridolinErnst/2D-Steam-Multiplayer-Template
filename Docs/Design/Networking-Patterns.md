# Networking patterns: how to build a networked mechanic

The ADRs say **why** multiplayer works the way it does ([0001](../adr/0001-ngo-listen-server-host-authority.md) host authority, [0002](../adr/0002-networked-state-and-atom-mirrors.md) Atom mirrors, [0004](../adr/0004-who-decides-what-in-multiplayer.md) authority tiers). This page says **how**: one pattern per case. The snippets are short sketches (2D, NGO 2.x); only `PlayerCountMirror` exists in the template. When a game adds the first real example of a pattern, link it here.

Netcode for GameObjects 2.x terms used below: **host** = the player running the session (server + client in one). **Owner** = the machine that owns a `NetworkObject` (each player owns their Player). `IsServer` is true on the host. A **local** (same-screen) game is a host with no remote clients, so every pattern works unchanged there.

---

## 0. Pick the pattern first

Answer in order and stop at the first "yes":

| Question | Pattern |
|---|---|
| Does only the owning player control it (own movement, own aim)? | [1. Owner decides](#1-owner-decides-tier-1) |
| Is it shared, but only one player uses it at a time, and waiting for the host would feel laggy (pick up an item, pull a lever)? | [2. Instant action with undo](#2-instant-action-with-undo-tier-2) |
| Is it a hit from a weapon or projectile? | [3. Shooter claims, host checks](#3-shooter-claims-host-checks-weapons) |
| Is it contested or game-critical (damage, health, loot, score, AI, shared physics)? | [4. Host decides](#4-host-decides-tier-3) |
| Does UI or another local system need to react to replicated state? | add [5. Mirror into an Atom](#5-mirror-replicated-state-into-an-atom) on top |

Also decide which **channel** each message uses (ADR 0002):

- **State** (late joiners must see it: "door is open") → `NetworkVariable`.
- **Request** (a player wants something: "pick this up") → `[Rpc(SendTo.Server)]`.
- **Occurrence** (a one-off thing happened: "a shot was fired") → `[Rpc(SendTo.NotOwner)]` or `SendTo.ClientsAndHost`. Late joiners don't get it, so it must not carry state.

---

## 1. Owner decides (tier 1)

The owner's machine computes the value and publishes it. Everyone else just displays it, slightly late.

**Movement:** give the Player prefab a `NetworkTransform` with owner authority (in the game's setup step, with `SetupUtil.UpdatePrefab` on `Prefabs/Session/Player.prefab`), and only read input on the owner.

```csharp
var nt = root.AddComponent<NetworkTransform>();
nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // owner moves, others follow
nt.SyncPositionZ = false; nt.SyncRotAngleX = false; nt.SyncRotAngleY = false;   // 2D: x, y and z-rotation only
```

**Any other owner value:** a `NetworkVariable` with owner write permission.

```csharp
private readonly NetworkVariable<Vector2> _aim = new NetworkVariable<Vector2>(
    default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

private void Update()
{
    if (!IsSpawned) return;
    if (IsOwner) _aim.Value = ReadAimInput();
    // every peer reads _aim.Value to draw the aim
}
```

Rules:
- Guard input and owner logic with `if (!IsSpawned || !IsOwner) return;`.
- Send positions **relative to the player** where they follow the player. A world position arrives late and lags behind the player's own interpolated body.
- Enable input actions only on the owner (`if (IsOwner) _move.action.Enable();` in `OnNetworkSpawn`).
- Local same-screen players are several Player objects on the host, all owned by the host. A game that has them reads each player's input from that player's own device, not from `IsOwner`.

---

## 2. Instant action with undo (tier 2)

The acting player sees the result **at once**. The host confirms it through normal state, or sends a denial that **names what it denied**. With no answer within a timeout, the preview is undone.

```csharp
// Owner: show it now, ask the host.
BeginPreview(item);                                // local only, e.g. hide the item's collider and draw it in hand
PickUpRpc(item.NetworkObject);

[Rpc(SendTo.Server)]
private void PickUpRpc(NetworkObjectReference target)
{
    if (TryPickUpOnServer(target)) return;          // success: replicated state confirms it
    PickUpDeniedRpc(target);                        // failure: tell the owner exactly what was denied
}

[Rpc(SendTo.Owner)]
private void PickUpDeniedRpc(NetworkObjectReference target)
{
    // Only undo the preview this answer is about; a later one may already be pending.
    if (_preview != null && target.TryGet(out NetworkObject no) && no == _preview.NetworkObject) EndPreview();
}

// Owner, every frame: give up if the host never answers.
if (_preview != null && Time.time - _previewSince > PreviewTimeout) EndPreview();
```

Rules:
- The preview is **local only**. Never write `NetworkVariable`s for it.
- When the confirmed state arrives, end the preview and let the real state take over.
- The host still validates everything (reach, that nobody else has it).
- Never use this for damage, deaths, loot or score. Those are tier 3.
- Things several players can push never use this tier. They stay simulated by the host.

---

## 3. Shooter claims, host checks (weapons)

The shooter's machine decides what it hit and sends a claim. The host sanity-checks the claim with lenient tolerances and applies the outcome. Others get the cosmetic occurrence.

```csharp
// Owner: instant cosmetic feedback, then the claim.
RaycastHit2D hit = Physics2D.Raycast(origin, direction, _range.Value, _hitMask);
ShowTracer(origin, hit ? hit.point : origin + direction * _range.Value);
FireRpc(origin, direction, hit ? hit.point : Vector2.zero, TargetOf(hit));

[Rpc(SendTo.Server)]
private void FireRpc(Vector2 origin, Vector2 direction, Vector2 point, NetworkObjectReference target)
{
    if (Time.time < _nextServerShot) return;                 // host enforces the cooldown too
    _nextServerShot = Time.time + _cooldown.Value * 0.8f;   // small slack for jitter
    if (!ClaimIsPlausible(origin, direction, point, target)) return;   // pure check in Game.Core, unit-tested
    ApplyHit(target, point);
    ShotRpc(origin, point);
}

[Rpc(SendTo.NotOwner)]                                       // NotOwner: the shooter already showed it
private void ShotRpc(Vector2 from, Vector2 to) => ShowTracer(from, to);
```

Rules:
- Put the claim check in `Game.Core` (pure, unit-tested) and reuse it for every weapon.
- Projectiles on a predictable arc use the same pattern (ADR 0004, "Projectiles"): the owner flies a local copy and sends the launch; the host checks and relays it; only the shooter's copy claims hits. Projectiles are local objects, never NetworkObjects.
- A projectile that must persist for late joiners (e.g. a pickup) is a host-simulated object (pattern 4).

---

## 4. Host decides (tier 3)

Logic runs only on the host. Clients see results through replicated state. The acting player may play **cosmetic** feedback at once (sound, animation), never a guessed outcome.

**Host-simulated object:** server-authority `NetworkTransform` (or `NetworkRigidbody2D` for physics), logic guarded by `IsServer`.

```csharp
private void Update()
{
    if (!IsSpawned || !IsServer) return;                    // AI only on the host
    ...
}
```

**Host decides, peers see occurrences:** an ability runs its timeline on the host and sends one-off RPCs for the visuals.

```csharp
ExplodedRpc(center);                                         // host decided; every peer plays the effect

[Rpc(SendTo.ClientsAndHost)]
private void ExplodedRpc(Vector2 center) { _effects.Play(center); _exploded.Raise(center); }   // local Atom event
```

**Host moves an owner-moved player:** players move only on their owner, so the host sends `[Rpc(SendTo.Owner)]` ("knock back by this much") and the owner applies it.

Rules:
- Server-only state is plain fields with a "server only" comment. Anything clients need goes into a `NetworkVariable` with default (server) write permission.
- Validate every request with a little slack for lag (reach + a margin, cooldown × 0.8).
- Release server-held things when the holder despawns (`OnNetworkDespawn`).

---

## 5. Mirror replicated state into an Atom

UI and other local systems never read `NetworkVariable`s directly. A **mirror binder** copies them into an Atom Variable (`Atoms/Variables/Net/` for gameplay state). It is that Atom's single writer, listed in Architecture.md section 3. From [`PlayerCountMirror.cs`](../../Assets/_Project/Scripts/Networking/PlayerCountMirror.cs):

```csharp
[Required, SerializeField] private IntVariable _connectedPlayers;
private readonly NetworkVariable<int> _count = new NetworkVariable<int>();

public override void OnNetworkSpawn()
{
    if (IsServer) { /* host computes the value */ _count.Value = NetworkManager.ConnectedClientsIds.Count; }
    _count.OnValueChanged += OnCountChanged;
    _connectedPlayers.Value = _count.Value;          // copy current value on spawn (events never replay, D7)
}

public override void OnNetworkDespawn()
{
    _count.OnValueChanged -= OnCountChanged;         // every += has a -=
    _connectedPlayers.Value = 0;                     // reset the mirror
}

private void OnCountChanged(int previous, int current) => _connectedPlayers.Value = current;
```

Rules:
- The host goes through the same mirror as clients. Never write the Atom directly from host logic.
- Per-player state: a Variable Instancer on the Player prefab, plus a `LocalPlayer*` mirror written by the owner's binder for the HUD.
- Requests in the other direction: a local Atom Event → a component raises a `[Rpc(SendTo.Server)]`.

---

## Pitfalls

- **Despawned objects:** `NetworkObjectReference.TryGet` fails and Unity objects compare equal to `null` after being destroyed. Check before use.
- **`OnValueChanged` is not called for the initial value.** Call your handler once in `OnNetworkSpawn`.
- **Double effects on the actor:** cosmetic occurrences go `SendTo.NotOwner` when the owner already showed them.
- **Updates before spawn:** `Update` runs before `OnNetworkSpawn`, so start with `if (!IsSpawned) return;`.
- **Pause is local only.** Only a local game pauses (`Time.timeScale` = 0, `IsPaused` true). Online games keep running with the in-game menu open, so never stop networked logic because a menu is open.
- **Testing without lag hides bugs.** Every Play starts on the "Typical" simulated connection (~80 ms round trip; F8 cycles profiles). Test with several players using the Multiplayer Play Mode scenarios `TwoPlayers` / `ThreePlayers` in `Assets/Settings/PlayMode/`. Tick `Auto Start` on the `EditorAutoSession` component (Network object) to skip the menu: the main Editor hosts and clones join.

## Checklist for a new networked mechanic

1. Pick the pattern (section 0) and name its tier in the class `<summary>`, citing ADR 0004.
2. List its state (`NetworkVariable`s, who writes each), requests (server RPCs) and occurrences (client RPCs).
3. Put the pure rules (checks, math) in `Game.Core` and unit-test them.
4. Host validates every request, with slack for lag.
5. If UI needs it: a mirror binder plus an Atom, and a row in Architecture.md section 3.
6. Unsubscribe everything in `OnNetworkDespawn`, and release anything the despawning player held.
7. Try it with two players on "Typical" and on "Bad Wi-Fi", and once in a local game.
