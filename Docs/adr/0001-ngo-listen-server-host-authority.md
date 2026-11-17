# Netcode for GameObjects 2.x with a player-hosted, host-authoritative session

Games made from this template are small co-op games where one player hosts (a listen server) and friends connect peer-to-peer over Steam. We use Unity's Netcode for GameObjects (NGO) 2.x. The host decides all shared state: enemies, scores, pickups and every piece of shared physics. Each player moves only their own character, which is sent to the others. Things several players can push are simulated by the host's physics only, and clients send requests ("I push here"). No client ever simulates shared physics on its own, because games that do that desync. Solo and local (same-screen) play are the same code path: a host with no remote clients.

## Considered Options

- **Photon Fusion 2**: built-in tick-based prediction and lag compensation, and smoother shared physics for clients. Rejected because of monthly Photon cloud costs once players play, a closed-source core (agents can't read the source), and Fusion 1 vs 2 API confusion. For co-op, a slight delay on shared objects is acceptable.
- **Photon Quantum**: deterministic, never desyncs, but the whole game must be written in Quantum's own framework and physics. That cancels out the Unity Atoms architecture and Unity physics.
- **FishNet / Mirror**: viable, but they have smaller ecosystems and no official Unity tooling (Multiplayer Play Mode).

## Consequences

- If the host leaves, the session ends for everyone (clients return to the main menu with a message). There is no host migration.
- NGO has no built-in lag compensation. Timed inputs between players send the network time of the press, and the host checks it against what it recorded.
- A game decides when players may join. Joining at a quiet moment keeps the world snapshot a late joiner receives small.
