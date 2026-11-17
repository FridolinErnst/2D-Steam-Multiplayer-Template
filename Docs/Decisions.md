# Decisions

One entry per change to the rules (AGENTS.md, Architecture.md, setup scripts, tests that enforce rules).
Newest first. Say **what** changed and **why** in plain words, so anyone can see later why a rule exists.

Template:

```
## YYYY-MM-DD — Short title
**What:** …
**Why:** …
**Instead of:** (the option that was rejected, if any)
```

---

## 2026-11-17 — 2D Steam multiplayer template
**What:** This template is the 2D Template (URP 2D, 2D packages) plus the game-agnostic multiplayer layer of the 3D Steam multiplayer game: NGO 2.14.0, Multiplayer Play Mode 2.0.2, Multiplayer Tools 2.2.12 and Steamworks.NET 2025.165.0, the Steam bootstrap, lobby and sockets transport, session control, network conditions, player count, the networking ADRs and patterns guide, and the `steamworks` and `steam-publish` skills. New on top: a main menu (Host Online Game, Join Friend, Play Local, Quit), an in-game menu (Resume, Invite Friends, Leave to Main Menu), local same-screen sessions (a host on loopback that accepts nobody else; the only kind of session that pauses), and a one-line message in the main menu when the host leaves or the connection drops. Setup step 10 (`SessionSetup`) builds it into `Scenes/Main.unity` and rebuilds only its own root objects; the Player prefab is an empty NetworkObject created once and then left to the game. `EditorAutoSession` is now off by default so Play shows the main menu. `ArchitectureTests` lets UI reference `TMPro` text components. The Steam transport now lingers when dropping a client so NGO's disconnect reason arrives. TextMeshPro Essential Resources are committed. Gameplay code, the voice ADR and the 3D art pipeline are not part of the template.
**Why:** A starting point for 2D Steam co-op games with no game-specific code, so each new game starts at "menu, host, join, play local" instead of copying from an old game.
**Instead of:** Converting the 3D game in place (lots of 3D content to strip, and its history would be public), or the single-player 2D Template plus networking added per game.

## 2026-11-17 — `verify.sh` fails fast when the Editor is busy
**What:** `Tools/verify.sh` checks the Editor before doing anything and exits with 3 if it is in Play Mode, compiling, running tests, or doesn't answer within 15 s. Test runs are limited by `TEST_TIMEOUT` (default 300 s). AGENTS.md section 7 documents exit code 3.
**Why:** Several agent sessions share one Editor. A test run started while another session used it hung for over 10 minutes, and recompiling during someone's Play session breaks the network session.
**Instead of:** Waiting for the Editor (blocks the session for minutes with no result), or one Editor per session in separate worktrees (heavy on a laptop; still an option for long parallel features).

## 2026-11-16 — Agents ask before deciding player-visible behaviour
**What:** AGENTS.md workflow rule 10: agents don't invent anything a player sees, hears or feels. Open questions are asked up front with options and a recommendation; implementation choices stay with the agent; unavoidable placeholders are listed as "Assumed: …" in the summary.
**Why:** Visible behaviour is a design decision. Agents filling gaps silently put features in the game that the designer never chose and may not notice.
**Instead of:** Agents picking sensible defaults and reporting them afterwards (easy to miss in a long summary), or keeping the rule only in Claude's private memory (Copilot, Cursor and subagents wouldn't see it).

## 2026-11-16 — Networking patterns guide
**What:** New `Docs/Design/Networking-Patterns.md`: which pattern a networked mechanic uses (owner decides, instant action with undo, shooter claims and host checks, host decides, Atom mirror), each with a short example, plus pitfalls and a checklist. AGENTS.md section 1 points to it.
**Why:** The ADRs explain the decisions but not how to write the code. New mechanics should copy a proven pattern instead of inventing one.
**Instead of:** Long code samples inside the ADRs (they would mix the decision with code that changes), or no guide (each new mechanic re-derives the pattern from existing files).

## 2026-11-16 — Tuning values as Constant assets, editor-tool and scenario rules, verify script
**What:** AGENTS.md 4.8: numbers tuned by feel are `FloatConstant`/`IntConstant` assets in `Atoms/Constants/<Feature>/`, created by setup steps only when missing. AGENTS.md 4.9 writes down the editor-code conventions. Section 7 adds `Tools/verify.sh`. Workflow rule 6 now asks for a scenario only when a feature takes over ~15 s to reach, with one scenario per feature area.
**Why:** Tuning a prefab literal needed a custom window and a "save" step. An asset can be edited during Play and keeps the value. The editor conventions were only discoverable by reading `ArchitectureTests`. Checking a change took several separate commands. A scenario per feature, with no limit, would pile up over time.
**Instead of:** A tuning window per value (the `HoldDistanceTuner` window, now removed), and `FloatVariable`s (code could write them, and they bring `Changed`/reset concerns that tuning values don't need).

## 2026-11-16 — Instant actions with undo (ADR 0004), Typical as default simulated network
**What:** ADR 0004 and D15 add the "instant action with undo" rule for tier 2: the acting machine shows the result at once, the host confirms or sends an explicit denial naming what it denied, and the preview is removed on denial or timeout. `NetworkConditionsPicker` starts every Play on Typical (~80 ms round trip) instead of remembering the last choice.
**Why:** With a typical connection, waiting a round trip after pressing E felt laggy. Starting every window on the same profile keeps both directions simulated without setting each window by hand.
**Instead of:** Feedback only (hand animation and sound, item arrives one round trip later), and handing item ownership to the grabber (a bigger change for the same result on light items).

## 2026-11-16 — Simulated network conditions for testing on one machine
**What:** Added the `com.unity.multiplayer.tools` package (2.2.12) and referenced its Network Simulator runtime from `Game.Runtime`. Setup step 10 now adds Unity's `NetworkSimulator` plus our `NetworkConditionsPicker` to the Network object. The picker shows a panel in every Game view (top right, F8 cycles) with Off / Good day / Typical / Bad Wi-Fi profiles. They can be switched during play, and the choice is remembered.
**Why:** Two Editors on one machine have no lag, so lag-dependent decisions (e.g. instant actions) can't be judged. The profiles model friends on the same continent connected through Steam.
**Instead of:** NGO's `SetDebugSimulatorParameters` (now has no effect) and macOS `dnctl`/`pfctl` on loopback (needs sudo, affects the whole machine).

## 2026-11-16 — Authority tiers and shooter-decided hits (D15, ADR 0004)
**What:** New D15 and `Docs/adr/0004`: every networked mechanic picks one of three authority tiers (owner decides / ownership while only I use it / host decides with instant local cosmetic feedback). Weapons let the shooter's machine decide the hit and send a hit claim; the host sanity-checks it and applies it. Projectiles on a predictable arc follow the same rule.
**Why:** With the host re-raycasting, a client hits a target on screen and the host, seeing it ~100 ms later, misses. A fixed rule per tier means new mechanics don't each need custom prediction.
**Instead of:** Host re-raycasting (unreliable under lag), host-side lag compensation (rewind; no NGO support, cheat resistance not needed among friends), and client prediction with reconciliation (per-mechanic rollback code).

## 2026-11-15 — Networking rules (D12)
**What:** `Architecture.md` gets D12: NGO 2.x, player-hosted and server-authoritative, with replicated state reaching Atoms only through mirror binders over three channels (state, request, occurrence). Details are in `Docs/adr/0001` and `0002`.
**Why:** Atom assets are local to one process and can't be the source of truth in multiplayer. One path for host and clients avoids duplicate effects.
**Instead of:** Photon Fusion or Quantum, syncing Atom assets directly, and a host-only shortcut to Atoms.

## 2026-11-15 — Agent skills and code graph for the Steam multiplayer template
**What:** Project skills in `.claude/skills/`: grilling (`/grill-me`, `/grill-with-docs`, `domain-modeling`),
`steam-publish`, `unity-build-pipeline`, `unity-animation`, `unity-input-system`,
and graphify (code knowledge graph, hooks in `.claude/settings.json`). Sources and licenses are in `.claude/skill-licenses/`.
**Why:** Plan with `/grill-with-docs` before building. Agents get correct Steam upload, build, animation and input
know-how without searching, and query the graph instead of grepping the whole project.
**Instead of:** Large third-party skill packs (e.g. 94 Blender skills): they cost context in every session and
depend on sibling skills. Duplicating skills from the official Unity plugin (`unity@unity-agent-plugin`)
was also rejected.
The `steamworks` skill covers Steamworks SDK use in game code.

## 2026-11-15 — Template extras on top of the Fluffle architecture
This project is the Fluffle architecture (see `Docs/Design/Architecture.md` D1–D9) plus the following.

### Gameplay code has its own assembly (`Game.Runtime`)
**What:** `Assets/_Project/Scripts/Game.Runtime.asmdef` holds all gameplay code; `Assembly-CSharp` stays empty.
Generated atoms live inside it too. Editor code and binder tests stay in `Assembly-CSharp-Editor`.
**Why:** Editing one script no longer recompiles everything, and PlayMode tests can reference gameplay code
(in Fluffle they couldn't see `Assembly-CSharp`).
**Instead of:** Keeping gameplay in `Assembly-CSharp` like Fluffle (simpler, but slower compiles).

### `[Required]` fields fail loudly
**What:** `Game.Atoms.RequiredAttribute` marks fields a component cannot work without. Empty required fields
block Play Mode (`PlayModeRequiredGuard`), are listed by `Game/Validate/Required Fields`, run in
`Game/Setup/Run All`, and fail `RequiredFieldTests` (project scan).
**Why:** A missing reference should be found before playing, all at once, with a message that names the field,
instead of a `NullReferenceException` or a UI that silently shows nothing. Optional fields stay optional.
**Instead of:** Throwing exceptions in `Awake` (stops at the first problem, only after pressing Play), or a
custom Inspector drawer (it would replace Unity Atoms' own Reference drawers).

### Scenarios start the game at a testable state
**What:** `Scenario` assets (Assets > Create > Game > Scenario, kept in `Assets/_Project/Data/Scenarios/`)
set Variables, spawn prefabs and raise events one frame after Play starts. Play them from
`Game/Scenarios/Scenario Picker`, the asset's "Play This Scenario" button, or `Game/Scenarios/Play Last Scenario`.
**Why:** Testing a new feature shouldn't mean playing for minutes to reach it. Because state lives in Atom
Variables, a starting state is mostly a list of Variable values.

### `.editorconfig` instead of a written style guide
**What:** Code style (naming, braces, `var`) lives in `.editorconfig`; the IDE shows hints. All rules are
suggestions, not errors.
**Why:** Machine-checked style beats a document nobody re-reads. AGENTS.md 4.7 remains the written summary.

### Test runs are small and bounded
**What:** AGENTS.md section 7 now says: run only the tests for what you changed, by filter, with a timeout.
**Why:** Long, unbounded test runs made chats hang.

### AGENTS.md folder layout matches the real test folders; test scenes stay out of player builds
**What:** AGENTS.md section 5 now shows `Core/`, `CoreTests/`, `Assets/Editor/Tests/<Feature>/` and
`Scenes/Tests/` instead of `Assets/_Project/Tests/EditMode|PlayMode` (which never existed; the empty PlayMode
asmdef in `Assets/Tests/` was removed). Scenes in `Assets/_Project/Scenes/Tests/` stay in Build Settings, because
NGO scene sync needs them there to play them with a client, but `TestScenesBuildFilter` drops them from player
builds.
**Why:** The documented layout sent agents to folders that don't exist, and a test scene would have shipped.
**Instead of:** Taking test scenes out of Build Settings, which breaks joining a host that started in one.
