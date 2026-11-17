---
name: steamworks
description: >
  Steamworks SDK use in this project's game code: Steamworks.NET init and shutdown, lobbies and friend
  invites, the NGO 2.x transport over Steam networking sockets, Rich Presence, Steam Cloud, Steam
  voice behind the voice seam, app 480 (Spacewar) development and the "no Steam in the editor"
  fallback. Use for SteamAPI.Init failing, invites not joining, clients not connecting, voice
  silence, or any ISteam* call. Not for uploads or store pages (`steam-publish`).
---

# Steamworks (Steamworks.NET + NGO 2.x, this project's stack)

The stack is fixed by `Docs/adr/0001`, `0002`, `0004` and `Architecture.md` D12, D15, D16. Read them first if you haven't.
- **Netcode:** Netcode for GameObjects 2.x. The player who hosts is the server and decides everything shared.
- **Steam wrapper:** Steamworks.NET (a thin 1:1 wrapper, so Valve's partner docs apply directly).
- **Connection:** our own NGO `NetworkTransport` on `ISteamNetworkingSockets` (P2P through Valve's relay).
- **Voice (not in the template; if a game wants it):** Steam voice over NGO, behind a `Game.Core` interface so Dissonance could replace it. Record that as the game's own ADR.
- **Without Steam** (editor, Multiplayer Play Mode, tests): Unity Transport on localhost, and voice off.

Facts that drift between versions: check the installed source, never memory.
- **NGO transport API:** `Library/PackageCache/com.unity.netcode.gameobjects*/Runtime/Transports/NetworkTransport.cs`
- **Steamworks.NET API:** `Library/PackageCache/com.rlabrecque.steamworks.net*/` (or wherever `Packages/manifest.json` puts it)
- **Packages:** if a package isn't in `Packages/manifest.json` yet, ask before adding it (AGENTS.md §1).

## When to use

- Writing or changing anything that calls `SteamAPI`, `SteamMatchmaking`, `SteamFriends`, `SteamNetworkingSockets`, `SteamUser` (voice) or `SteamRemoteStorage`.
- The Steam transport, lobby → NGO session flow, invites, join-during-Flight rules.
- "SteamAPI.Init failed", "invite does nothing", "client connects in editor but not over Steam", "voice is silent".
- **Not** for builds, depots, steamcmd or store pages: use `steam-publish`.

## Architecture rules (this project)

1. **No `SteamManager` singleton.** The Steamworks.NET sample uses a static `Instance`, which is forbidden (D3, ArchitectureTests). Instead:
   - One `SteamBootstrap` component on the systems prefab initializes and shuts down Steam, and calls `SteamAPI.RunCallbacks()` every frame.
   - It writes `IsSteamRunning` (BoolVariable) and `LocalPlayerName` (StringVariable). It is the single writer of both.
2. **Steam types never leave `Game.Runtime/Steam/`** (namespace `Game.Steam`). Cores and other features see plain ids (`ulong`) and Atoms only.
3. **Guard all Steam code with `#if !DISABLESTEAMWORKS`.** Steamworks.NET defines it on unsupported platforms. Add the Steamworks.NET runtime asmdef to `Game.Runtime.asmdef` references (check its exact name in the package).
4. **Lobby and session state are networked state too.** Lobby events (invite accepted, member joined) are **local** occurrences. Raise Atom Events from one Steam binder, and let the session component react. Don't call NGO from inside Steam callbacks scattered across features.
5. **One config asset** (`NetworkConfig`, Game.Data) holds the app ID, the max players (6, it's a setting), and whether to use the Steam transport or Unity Transport. Never hard-code 480 or 6.

## Init and shutdown

```csharp
// SteamBootstrap, Awake (only one, on the systems prefab; DontDestroyOnLoad comes from the systems prefab)
if (SteamAPI.RestartAppIfNecessary(new AppId_t(_config.AppId))) { Application.Quit(); return; } // builds only, skip in editor
bool ok = SteamAPI.Init();   // newer Steamworks.NET also has InitEx(out string error); prefer it if present
_isSteamRunning.Value = ok;
if (!ok) { Debug.LogWarning("Steam not running: falling back to Unity Transport."); return; }
SteamNetworkingUtils.InitRelayNetworkAccess();   // warm up relay early, connections are faster later
```
- `Update`: call `if (_isSteamRunning.Value) SteamAPI.RunCallbacks();`. Without it, no callback ever fires (lobbies, connections, invites).
- `OnApplicationQuit` / `OnDestroy`: call `SteamAPI.Shutdown()` once.
- **`steam_appid.txt`** containing `480` goes in the project root, next to `Assets/`, for editor and dev builds. Never ship it in a release build (the build script deletes it). Without it, init fails unless the game is launched from Steam.
- **Init fails** when the Steam client isn't running, isn't logged in, or the account doesn't own the app (app 480 Spacewar is free to all accounts). Every failure path must fall back to Unity Transport, never crash.
- **Keep `Callback<T>` and `CallResult<T>` instances in fields.** If they are only local variables, they are garbage-collected and stop firing.

## Lobbies, invites and joining

**The Steam lobby is how friends find the host.** NGO carries the actual game traffic. Flow:

1. **Host:**
   - Call `SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _config.MaxPlayers)` and wait for `CallResult<LobbyCreated_t>`.
   - On `LobbyCreated_t`: call `SetLobbyData(lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString())`.
   - Then start NGO as host (`NetworkManager.StartHost()` with the Steam transport listening).
2. **Invite:** `SteamFriends.ActivateGameOverlayInviteDialog(lobbyId)`. The overlay only works in builds started through Steam, or with `steam_appid.txt`. In the editor it often does nothing.
3. **Friend accepts:**
   - **While the game is running:** `Callback<GameLobbyJoinRequested_t>` fires. Call `SteamMatchmaking.JoinLobby(evt.m_steamIDLobby)`.
   - **While the game is closed:** Steam launches it with `+connect_lobby <id>`. Parse `System.Environment.GetCommandLineArgs()` at startup and join that lobby.
4. **On `Callback<LobbyEnter_t>`** (as a client): read the host id with `GetLobbyOwner` or the `"host"` data, set it on the transport, and call `NetworkManager.StartClient()`.
5. **Leaving:** `SteamMatchmaking.LeaveLobby(id)` *and* `NetworkManager.Shutdown()`. If the host leaves, the session ends for everyone (ADR 0001); there is no host migration.
- **Join only during the Flight** (a design rule): the host uses NGO **connection approval**. Outside the Flight, a joining player is approved as a spectator who waits for the next Flight. Don't use `SetLobbyJoinable(false)` for this, because invites must still work.
- **Max players:** pass `_config.MaxPlayers` to `CreateLobby`, *and* reject extra connections in connection approval. The two limits must match.

## Rich Presence

- Join button: `SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobbyId)`. This makes "Join Game" appear on the friend list.
- Grouping in the friend list: set `"steam_player_group"` to the lobby id and `"steam_player_group_size"` to the member count.
- Status text ("Level 3 – 2 players") uses `"steam_display"` with a `#Token` from a localization file uploaded on the partner site. **This can't be customized on app 480**, so it only shows once the game has its own app ID.

## Steam Cloud

- Prefer **Steam Auto-Cloud**: write the save to `Application.persistentDataPath`, and configure the path and quota on the partner site. No `SteamRemoteStorage` code is needed.
- Auto-Cloud isn't available on app 480. Until the game has its own app ID, the save is simply local, which is fine.
- Decide per save what is per player (cloud) and what belongs to the host's world (saved on the host's machine only).

## The NGO transport (`SteamSocketsTransport : NetworkTransport`)

Start from the community `com.community.netcode.transport.steamnetworkingsockets` (Unity multiplayer-community-contributions). **Check every override against the installed NGO 2.x `NetworkTransport.cs`**, because signatures changed between NGO versions.

- **Server:**
  - `SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null)` and `CreatePollGroup()`.
  - On `SteamNetConnectionStatusChangedCallback_t` with state `Connecting`: call `AcceptConnection`, then `SetConnectionPollGroup`.
  - On `Connected`: queue `NetworkEvent.Connect`.
  - On `ClosedByPeer` or `ProblemDetectedLocally`: queue `Disconnect` and `CloseConnection`.
- **Client:** `ConnectP2P(ref identity, 0, 0, null)` with `identity.SetSteamID(hostId)`.
- **Ids:**
  - NGO wants `ulong` transport ids. Keep two dictionaries, `HSteamNetConnection ↔ ulong`, with ids from a counter.
  - `ServerClientId` is the host's id (0).
  - Never expose raw Steam ids as NGO client ids.
- **Delivery mapping:**
  - `Reliable`, `ReliableSequenced` and `ReliableFragmentedSequenced` → `k_nSteamNetworkingSend_Reliable` (which is also ordered).
  - `Unreliable` and `UnreliableSequenced` → `k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoNagle`.
  - Steam splits large unreliable messages into packets, and losing any one packet drops the whole message. Keep unreliable payloads small, and send big state reliably.
- **The implementation is `Assets/_Project/Scripts/Steam/SteamSocketsTransport.cs`**, with the id map in `Game.Core.Net.TransportIdMap` (unit-tested). Change those files; don't start a new transport.
- **Receive:**
  - `ReceiveMessagesOnPollGroup` (server) or `ReceiveMessagesOnConnection` (client), with a reused `IntPtr[]` buffer.
  - Copy each payload into a **pooled** byte array, then call `SteamNetworkingMessage_t.Release(ptr)` on every message. Leaking messages leaks native memory.
- **`PollEvent`:** returns queued connect and disconnect events first, then data, then `NetworkEvent.Nothing`. No allocations per call.
- **`GetCurrentRtt`:** read it from `GetConnectionRealTimeStatus` (`m_nPing`).
- **`Shutdown`:** close every connection, the listen socket and the poll group, and dispose the callbacks.
- **Choosing the transport:** a small selector component reads `NetworkConfig` + `IsSteamRunning` and assigns `NetworkManager.NetworkConfig.NetworkTransport`, choosing the Steam transport or `UnityTransport` (localhost). Multiplayer Play Mode always uses `UnityTransport`.

## Voice (if a game adds it)

- **The interface** lives in `Game.Core`: start/stop capture, get the encoded bytes for this frame, and push received bytes for a player. The implementation is `SteamVoice` in `Game.Steam`. Gameplay only sees Atoms (e.g. a per-player loudness Instancer).
- **Capture:**
  - Start and stop with `SteamUser.StartVoiceRecording()` / `StopVoiceRecording()`.
  - Each frame, call `GetAvailableVoice(out uint size)` and, if there is data, `GetVoice(true, buffer, size, out uint written)`.
  - Steam does its own voice activity detection, so silence produces no data.
  - Push-to-talk simply starts and stops recording.
- **Send:**
  - Use `NetworkManager.CustomMessagingManager.SendNamedMessage` with `NetworkDelivery.Unreliable`, to the server.
  - The server forwards to everyone else, tagging the speaker's client id (the topology is a star, not a mesh).
- **Receive and play:**
  - Call `DecompressVoice(...)` at `GetVoiceOptimalSampleRate()`. It gives 16-bit mono PCM.
  - Write it into a ring buffer, and play it from an `AudioSource` on that player's character (positional if the game wants proximity voice) through a streaming clip or `OnAudioFilterRead`.
  - Voice effects per player state are a mixer group or snapshot picked from a mirrored Atom.
- **Loudness that affects gameplay** (e.g. enemies hearing players) is computed on the **server** from each decoded packet, including the host's own capture. Clients never decide it.
- **In the editor without Steam:** a no-op implementation, so voice is off. Don't fake it with `Microphone`. That's a different code path and would hide bugs.

## Testing

1. **Solo and Scenarios:** the host with no clients is the same code path (`Assets/_Project/Data/Scenarios/`).
2. **Multiplayer Play Mode:**
   - Window > Multiplayer > Multiplayer Play Mode, up to 4 players, over `UnityTransport` on localhost.
   - Each virtual player is its own process, so global Atom Variables don't leak between players.
   - Steam is not involved.
3. **Real Steam:**
   - Two machines, two Steam accounts, app 480, dev builds with `steam_appid.txt`.
   - Test invites while the game is running *and* while it's closed (`+connect_lobby`), a host quitting mid-Round, and joining during an island visit (the player must become a spectator).
   - Also test with 8 players now and then, to catch hidden 6-player assumptions.
- Edit Mode tests never touch Steam. Test the transport's id mapping and event queue by splitting them into a plain class that doesn't call Steam.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `SteamAPI.Init` returns false in the editor | Steam client not running or not logged in, or `steam_appid.txt` missing from the project root |
| Callbacks never fire | `RunCallbacks` not called every frame, or the `Callback<T>` was a local variable and got garbage-collected |
| The invite overlay doesn't open | Editor or a non-Steam launch. Test in a build with `steam_appid.txt` |
| A friend clicks "Join" but nothing happens when the game is closed | `+connect_lobby` not parsed at startup |
| Client stuck connecting | Relay not initialized, the host's listen socket not created before the client connects, or the wrong host id |
| Large messages disappear | An unreliable message above the MTU. Send it reliably or split it |
| Native memory grows | Received messages not `Release`d |
| Voice silent | Not recording, `GetVoice` called without checking `GetAvailableVoice`, the wrong sample rate on decompress, or the AudioSource is 3D with the speaker out of range |
| Recompile right after entering Play, then "Server failed to bind" on port 7777 | Steamworks.NET adding its `STEAMWORKS_NET` define on each reload. Keep `canManageDefineSymbols: false` in `ProjectSettings/SteamworksNETSettings.json`, and restart the Editor to free the orphaned port |
| Rich Presence status is blank | App 480 can't have localization tokens. Expected until the game has its own app ID |

## What is already in place (template)

- **Packages:** NGO 2.14.0, Multiplayer Play Mode 2.0.2, Multiplayer Tools 2.2.12, Steamworks.NET 2025.165.0 (git tag). They are referenced from `Game.Runtime.asmdef` as `Unity.Netcode.Runtime`, `Unity.Networking.Transport` and `com.rlabrecque.steamworks.net`.
- **`steam_appid.txt`** (`480`) is in the project root. Strip it from release builds.
- **Code:** `Game.Steam` holds `SteamBootstrap`, `SteamSocketsTransport` and `SteamLobby`. `Game.Networking` holds `TransportSelector`, `SessionStarter`, `PlayerCountMirror`, `EditorAutoSession` and the network conditions picker. `Game.UI` holds `MainMenu` (Join Friend opens the Steam friends list) and `PauseMenu` (Invite Friends); `Game.Session` holds `SessionPause`. Settings live in `Game.Data.SessionSettings` (`Assets/_Project/Data/Session/SessionSettings.asset`).
- **Wiring:** all of it lives on the `Network` and `UI` objects in `Scenes/Main.unity`, which are built by `Game/Setup/10 Session`. To change wiring, edit that setup script and re-run it rather than editing the scene by hand.
- **Steam in the Editor** is off by default (`SessionSettings._useSteamInEditor`). Turn it on only for a single-Editor Steam test.
