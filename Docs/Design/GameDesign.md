# Game Design

What the game is, not how the code is built (that is `Architecture.md`).

## Pitch
*(one or two sentences)*

## Core loop
*(what the player does again and again)*

## Mechanics
*(one short section per mechanic: what it does, numbers to tune, how it ends)*

## Progression
*(levels, unlocks, difficulty)*

## Sessions and menus (from the template)

Already built; change it here first if the game wants something else. Words are defined in `GLOSSARY.md`.

- **Main Menu** (shown whenever no Session runs): title = the product name, then **Host Online Game**, **Join Friend**,
  **Play Local**, **Quit**. Plain placeholder look: dark background, a centred column of default buttons.
- **Host Online Game** starts an Online game. Friends join through a Steam invite or "Join Game" in their friends list.
- **Join Friend** opens the Steam friends list (in the Editor without Steam it joins the direct address instead).
- **Play Local** starts a Local game on this machine; nobody can join it. How extra same-screen players join is up to the game.
- **In-game Menu** (Esc / gamepad Start during a Session): **Resume**, **Invite Friends** (Online over Steam only),
  **Leave to Main Menu**. A Local game pauses while it is open; an Online game keeps running.
- **When the host leaves or the connection drops**, Clients return to the Main Menu with one line explaining why.
- **Max players:** 6 per Online game, host included (`SessionSettings`).

## Open questions
