# Networked state reaches Atoms only through mirror binders

Unity Atoms are local to one process. ScriptableObject assets are not networked and are shared by everything in that process, so they cannot be the source of truth in a multiplayer game. The server (the host, in a player-hosted session) runs the authoritative gameplay Cores. Replicated state reaches Atoms only through mirror binders. The host goes through the same mirror path as every client, so it behaves the same and never sees an effect twice.

## The rule

1. The **server** runs the authoritative gameplay Cores. In a player-hosted session the host is both server and client.
2. There are three channels:
   - **State**: Core → `NetworkVariable` → mirror Atom. Example: "player 3 is Frozen". Late joiners receive it.
   - **Request**: local Atom Event → RPC to the server → Core. Example: "I want to fire".
   - **Occurrence**: Core → RPC to everyone → local Atom Event. Example: "a projectile hit this wall". Late joiners do not receive it.
3. **Only replicated gameplay state is mirrored.** Mirror Variables live in `Atoms/Variables/Net/`, and their single writer is the mirror binder, listed in the owner table. Settings, menus, camera and prediction Atoms keep their own local writers.
4. **Per-player state uses Variable Instancers on the player prefab.** The owning player's mirror binder also writes global `LocalPlayer*` mirrors for the HUD.
5. **Mirror binders copy the current value when spawned, then subscribe to changes.** When despawned they unsubscribe and reset the mirror. UI reads `.Value` when enabled, because Atom events never replay (D7).
6. **Cores never write Atoms.** On the server, setting a `NetworkVariable` and sending an RPC to "clients and host" also run locally, so the host needs no shortcut.
7. **Resetting means resetting the Core**, which the server then publishes. Clearing mirrors when leaving a session is a separate local cleanup step.

## Considered Options

- **Networking Atom assets directly** (syncing Variables or Events over the network). Rejected: assets are shared within a process, cannot be per-player, and would make every Atom a network concern.
- **The host writing its Atoms straight from the Core while clients use mirrors.** Rejected: the host would behave differently from clients and could receive effects twice.

## Consequences

- A dedicated server stays possible in principle, because mirror binders skip Atom writes when `!IsClient`. It is not built or tested (see ADR 0001).
- A Core can be tested in `Game.Core` with no network and no Atoms.
