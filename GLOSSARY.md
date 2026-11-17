# Glossary

Words for the multiplayer session layer this template ships with. A game made from it adds its own terms below
(the `domain-modeling` skill and `/grill-with-docs` keep this file up to date) and replaces this intro with its pitch.

## Language

### Sessions

**Session**:
One running game between pressing Host, Join or Play Local and returning to the Main Menu.
_Avoid_: Match, room, lobby

**Online game**:
A Session other players can join over Steam (or the direct address in the Editor). It never pauses.
_Avoid_: Multiplayer mode, network game

**Local game**:
A Session on one machine and one screen. Nobody can join over the network; any extra players play on the same screen.
_Avoid_: Offline mode, couch mode, split screen

**Host**:
The player whose machine runs the Session and decides all shared state. In a Local game the one machine is the Host.
_Avoid_: Server (except when talking about code), owner

**Client**:
A player who joined someone else's Online game.
_Avoid_: Guest, peer

**Player**:
One person in a Session, and the networked object that represents them (`Prefabs/Session/Player`).
_Avoid_: Character, avatar (until a game defines what the player controls)

### Menus

**Main Menu**:
The screen shown whenever no Session is running: Host Online Game, Join Friend, Play Local, Quit, and the
Disconnect Message if the last Session ended by itself.
_Avoid_: Title screen, lobby

**In-game Menu**:
The menu opened with Esc or gamepad Start during a Session: Resume, Invite Friends (Online game over Steam only),
Leave to Main Menu. It is titled "Paused" in a Local game and "Menu" in an Online game.
_Avoid_: Pause menu (an Online game never pauses), options menu

**Pause**:
Game time stopped while the In-game Menu is open. Only a Local game pauses.
_Avoid_: Freeze

**Disconnect Message**:
The one line in the Main Menu that says why the last Session ended when the player didn't leave on purpose:
"Host left the game.", "The game is full.", "Connection lost." or "Couldn't connect to the host."
_Avoid_: Error, kick message
