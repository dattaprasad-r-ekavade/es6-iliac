# Engine vs game

Ratna Bay is one game on a small first-person engine. The engine is not a second product yet;
it is the set of types a different game could take without taking mines, stones, or Northwatch.
This file is the map for that split, so the next extraction has somewhere to land and a second
game has somewhere to start.

## Three layers

| Layer | What it is allowed to know | Today |
| --- | --- | --- |
| **Rules** (`RatnaBay.Domain`) | Combat, saves, generation, items. No MonoGame types. | Own project. Reuse as-is only if the next game wants these rules. |
| **Engine** (this document) | Devices, the frame, a first-person view, boxes, imported models, a 2D canvas, input sampling and list picking. No mines, no quests, no Ratna Bay. | Types inside `RatnaBay.Game`, listed below. Not yet their own project. |
| **Game** (`Game1`, `Ui/`, `World/`, `Session/`) | Screens, HUD, the yard, descents, dialogue, the console's reach into this game. | The WindowsDX executable. Subclasses `EngineHost`. |

A second game should reference the engine types (or, once they move, `RatnaBay.Engine`) and
write its own presenter, screens and domain. It subclasses `EngineHost`, not `Game1`.

## What is already engine

These types have no Ratna Bay rules in them. A different first-person game would take them
unchanged:

| Type | Role |
| --- | --- |
| `EngineHost` | Devices, variable timestep, fonts, canvas attach, letterbox, capture framing, `--perf`. |
| `SceneRenderer` | Boxes, crystal, carved quad, glow, the two shaders. Material is a string (`stone`, `timber`, `cloth`, `earth`, `rope`). |
| `ModelCache` | Load, measure, normalise, draw imported models. |
| `BillboardRenderer` | Camera-facing cutout quads. |
| `FirstPersonView` | Look, walk, jump, crouch. Collision is a callback. |
| `CaptureHost` | Screenshot warmup, cover-sized render target, PNG write. A script hold is a bool. |
| `InputRouter` | One keyboard/mouse sample per frame. |
| `ListPicker` | Wrap or clamp a selected row from that snapshot. Bounds are a callback, not `UiLayout`. Grid walk and 1–9 digits included. |
| `ConsoleInput` | Buffer, history, toggle. Completing and running a line stay with the game. |
| `UiCanvas` | 2D primitives. Logical size is constructor arguments, not a Ratna Bay constant. `Scrim` and `Row` take colours. Callers pass `UiTheme`. |
| `WorldProjector` | World point → logical canvas. Logical size is passed in. |
| `StoneTextures` | Palettes are three colours. Construct `StonePalette` yourself; cave themes stay in `WorldPresenter.PaletteOf`. |

## What stays this game

- `Game1` — Ratna Bay on `EngineHost`: constructs host and Ratna Bay objects, samples input,
  applies commands, builds snapshots, implements `IConsoleTarget`. `--show` / `--swing` /
  `--cast` still open this game's panels and pose the weapon.
- `Input/ScreenStack` — which world panels are open, and which one owns the frame.
- `Input/OverlayInput` — consent, title, pause and settings. Selection and confirm only;
  Game1 still owns starting a game, toggling display, saving a descent. Do not pass `Game1`
  into it.
- `Input/WorldPanelInput` — inventory, shop, dialogue, shaft, camp trader, fort, run-summary
  button. Same rule: selection and confirm only; returns commands.
- `Input/SessionInput` — F5/F9/P/B/E mapped to session commands.
- `Session/SessionDirector` + `PlayState` — start/end a run, enter a world, save/load, the
  mine manifest. Game1 still applies toasts, audio and camera pose after a load.
- `Combat/CombatDirector` + `CombatFeel` — the fight, hitstop, shake, stride, stone drops.
- `Session/ConsoleHost` — run a line, pump a script, watches. Typing stays on `ConsoleInput`.
- `LaunchOptions` — `--mine`, `--yard`, `--moodboard`, `--stambha`, `--show`. Capture window
  size and `--perf` stay on `EngineHost`.
- `Ui/FramePresenter` — 2D draw order. Game1 still builds snapshots and draws the 3D world.
- `World/WorldPresenter` — walks a Ratna Bay manifest onto `SceneRenderer` / `ModelCache`.
  `PaletteOf(CaveTheme)` is the Domain → stone seam. A different game writes a different presenter.
- `World/FigurePresenter` — speakers, watchers, enemies, bolts onto `BillboardRenderer`.
- `World/SpikeScenes` — moodboard, stambha, asset case. Lighting studies, not engine.
- `Ui/CoverRenderer` — the store cover composition (MineEntry ladder). The target size is engine;
  the words are this game.
- `Render/FaceSheet` — `--faces` portrait contact sheet. Fort roster, not a frame capture.
- Every renderer under `Ui/` except the canvas itself. `UiLayout` and `UiTheme` are this
  game's panels and palette.
- `WorldRuntime`, `Encounter`, `GameSession`.

## How a second game would start

1. Copy or reference the engine types above. Do not copy `Game1`.
2. Subclass `EngineHost` (or `Microsoft.Xna.Framework.Game` if you want none of the host).
3. Own a `FirstPersonView`, a `SceneRenderer`, a `ModelCache`. The host already owns the
   canvas, capture, input router and fonts.
4. Each frame: `Input.Sample()`, `view.Step(...)`, `view.RebuildView()`,
   `BeginHostFrame()`, `scene.Begin(...)`, draw your own world, `canvas.Begin()` /
   your HUD / `canvas.End()`, `EndHostFrame(...)`.
5. Write a presenter that turns *your* world data into `DrawCube` / `DrawWorldBox` / `ModelCache.Draw`.
6. Keep your rules in an engine-free project, the way `RatnaBay.Domain` is.

Do not add a `RatnaBay.Engine` csproj until every type in the table above compiles without
`using RatnaBay.Domain`. That is the gate. `SceneRenderer` and `StoneTextures` already do.
`WorldPresenter.PaletteOf` is this game's remaining theme → palette step.

`--perf` prints a wall-clock frame summary on exit (`N frames, avg/min/max ms`). Use it after
a capture or a scripted run; it does not change the draw path.

## What is still in Game1

The ordered extraction is done. Game1 constructs the host and Ratna Bay objects, samples
input, applies commands, builds snapshots, and implements `IConsoleTarget`. Snapshot builders
(`BuildWorldHudState`, `BuildOverlayState`, weapon capture pose) stay here because they read
the live session. The 3D world pass, door prompt construction and `--show` panel open stay
until a change needs them out.

The device/font/canvas cut is done: that is `EngineHost`.

## Performance

The decomposition is extra method calls and readonly structs (`ListPick`), not extra
allocations per row. `ListPicker` returns a struct; `WorldPanelInput` is constructed once.

Measured 2026-08-28, Release, `--perf`, this machine:

| Run | Frames | Avg | Equivalent | Min | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| Yard capture, uncapped (`--warmup 180 --screenshot`) | 180 | 1.92 ms | 521 fps | 0.43 ms | 170.70 ms |
| Yard, windowed vsync, `wait 2; quit` | 53 | 48.15 ms | present-bound | 30.34 ms | 574.04 ms |

The uncapped capture is the cost of the draw: about two milliseconds, including the new
host and panel types. The 170–574 ms maxima are the first presented frame (shaders,
generated textures), not per-frame overhead from the split. A vsync window in this
environment is waiting on Present, not on `ListPicker`.

`--perf` prints that summary on exit. Use it after a capture or a scripted run; it does
not change the draw path. If a later cut shows up there as more than a millisecond of
average uncapped frame time against the same capture, that cut is the one to look at.

## What not to do

- Do not put shop tiles, descent rows or Northwatch colours in the engine.
- Do not make the engine depend on `RatnaBay.Domain`. A string material name is the seam;
  `WorldVector` is not. Cave colours become `StonePalette` in `WorldPresenter`, not in
  `StoneTextures`.
- Do not revive parked features as a way to "exercise" the engine.
- Do not add a package for one type. The csproj waits until the table above is Domain-free.
- Do not subclass `Game1`.
