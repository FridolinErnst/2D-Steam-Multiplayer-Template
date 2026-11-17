# 2D Steam Multiplayer Template

A Unity 6 starting point for 2D co-op games on Steam (URP 2D, sprites, tilemaps, 2D animation, Netcode for GameObjects).
Set up for building the game with [Claude Code](https://claude.com/claude-code). It contains no gameplay: you start at a
working main menu with online and local sessions, and build your game on top.

- **Architecture:** Scriptable Object architecture with [Unity Atoms](https://github.com/unity-atoms/unity-atoms).
  The rules every agent follows are in [AGENTS.md](AGENTS.md).
- **Multiplayer:** Netcode for GameObjects 2.x with a player host, Steam lobbies, invites and a Steam networking
  transport (Steamworks.NET), Unity Transport on localhost in the Editor, Multiplayer Play Mode and a simulated-lag
  picker (F8). How to build networked mechanics: [Docs/Design/Networking-Patterns.md](Docs/Design/Networking-Patterns.md).
- **Menus:** main menu (Host Online Game, Join Friend, Play Local, Quit), in-game menu on Esc (Resume, Invite Friends,
  Leave to Main Menu; only local games pause), and a message when the host leaves or the connection drops.
  See [Docs/Design/GameDesign.md](Docs/Design/GameDesign.md).
- **Built in:** missing-reference and required-field validation, a setup-step framework, scenarios and architecture tests.
- **Agent skills** (in `.claude/skills/`): planning interviews (`/grill-with-docs`), Steamworks, Steam publishing,
  Unity builds, animation, input and [graphify](https://github.com/Graphify-Labs/graphify) code search.

## Requirements

- Unity **6000.3.9f1** (install it from Unity Hub)
- [Git LFS](https://git-lfs.com)
- [Claude Code](https://claude.com/claude-code)
- [Steam](https://store.steampowered.com) running, to test invites (the project uses Spacewar, app 480, until your game has its own app id)

## Setup

1. On GitHub, click **Use this template** → create your own repository, then clone it.
2. From the project folder, run the setup script once:
   ```sh
   sh Tools/install-claude-plugins.sh
   ```
   It pulls the LFS files, installs `uv` and `graphify`, adds the Unity Claude plugin and downloads the optional Blender add-on.
3. **Restart your terminal and editor.** The hooks in `.claude/settings.json` need `graphify` on your PATH.
4. Open the project in Unity Hub. The first import takes a few minutes.
5. Set the product name (Edit → Project Settings → Player). It is the title of the main menu.
6. Open `Assets/_Project/Scenes/Main.unity` and press Play. To test with several players, open
   Window → Multiplayer → Multiplayer Play Mode, start a virtual player, host in one window and press Join Friend
   in the other. Tick **Auto Start** on the `EditorAutoSession` component (Network object) to skip the menu.
7. Before release: put your Steam app id in `Assets/_Project/Data/Session/SessionSettings.asset` and `steam_appid.txt`.

## Start your game

```sh
claude
```

```
/grill-with-docs <your game idea, in a few sentences>
```

Claude interviews you in rounds of numbered questions, each with a recommended answer ("yes" accepts it).
Decisions are written to `docs/adr/` and terms to `GLOSSARY.md` as you go. Nothing gets built until you confirm.
After that, ask Claude to build it feature by feature. Each change follows AGENTS.md: Claude does the Unity Editor work itself (assets, wiring, Play mode checks)
through the Unity CLI, runs only the tests for the code it changed, and ends with a summary of what it did.

## Folder layout

| Folder | What goes there |
|--------|-----------------|
| `Assets/_Project/` | All game content: Atoms, scripts, prefabs, scenes, data |
| `Docs/` | Design docs, decisions (`Docs/adr/`, `Docs/Decisions.md`) |
| `.claude/` | Agent skills, hooks and settings. Licenses for third-party skills are in `.claude/skill-licenses/` |

## Good to know

- `graphify-out/` is not committed. Rebuild it with `graphify update .`, which costs nothing (it uses no API).
- The `nosleep` skill and `Tools/claude-autocontinue.ps1` only work on Windows.
