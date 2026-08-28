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
| **Game** (`Game1`, `Ui/`, `World/`, `Session/`) | Screens, HUD, the yard, descents, dialogue, the console's reach into this game. | The WindowsDX executable. |

A second game should reference the engine types (or, once they move, `RatnaBay.Engine`) and
write its own presenter, screens and domain. It should not subclass `Game1`.

## What is already engine

These types have no Ratna Bay rules in them. A different first-person game would take them
unchanged:

| Type | Role |
| --- | --- |
| `SceneRenderer` | Boxes, crystal, carved quad, glow, the two shaders. Material is a string (`stone`, `timber`, `cloth`, `earth`, `rope`). |
| `ModelCache` | Load, measure, normalise, draw imported models. |
| `BillboardRenderer` | Camera-facing cutout quads. |
| `FirstPersonView` | Look, walk, jump, crouch. Collision is a callback. |
| `CaptureHost` | Screenshot warmup, cover-sized render target, PNG write. A script hold is a bool. |
| `InputRouter` | One keyboard/mouse sample per frame. |
| `ListPicker` | Wrap or clamp a selected row from that snapshot. Bounds are a callback, not `UiLayout`. |
| `UiCanvas` | 2D primitives. Logical size is constructor arguments, not a Ratna Bay constant. `Scrim` still uses this game's `UiTheme` colours — a second game should call `Panel` itself until that takes arguments. |
| `WorldProjector` | World point → logical canvas. Logical size is passed in. |

`StoneTextures` is *almost* engine: palettes are three colours. `FromTheme(CaveTheme)` is the
one Ratna Bay leak — a second game constructs `StonePalette` itself.

## What stays this game

- `Game1` — lifecycle, screen dispatch, the console.
- `Input/OverlayInput` — consent, title, pause and settings. Selection and confirm only;
  Game1 still owns starting a game, toggling display, saving a descent. Do not pass `Game1`
  into it. Inventory, shop, dialogue, the shaft and `UpdateGameScreen` are not this type.
- `World/WorldPresenter` — walks a Ratna Bay manifest onto `SceneRenderer` / `ModelCache`. A
  different game writes a different presenter.
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
2. Subclass `Microsoft.Xna.Framework.Game` (or a future `EngineHost` once one exists).
3. Own a `FirstPersonView`, a `SceneRenderer`, a `ModelCache`, a `UiCanvas`, a `CaptureHost`.
4. Each frame: sample `InputRouter`, `view.Step(...)`, `view.RebuildView()`,
   `capture.BeginFrame(...)`, `scene.Begin(...)`, draw your own world, `canvas.Begin()` /
   your HUD / `canvas.End()`, `capture.EndFrame(...)`.
5. Write a presenter that turns *your* world data into `DrawCube` / `DrawWorldBox` / `ModelCache.Draw`.
6. Keep your rules in an engine-free project, the way `RatnaBay.Domain` is.

Do not add a `RatnaBay.Engine` csproj until every type in the table above compiles without
`using RatnaBay.Domain`. That is the gate. `SceneRenderer` already does. `StoneTextures.FromTheme`
does not; move it (or stop calling it from the texture type) before cutting the project.

## What is still in Game1 that will move

In this order, because each cut has to be provably separable:

1. **Remaining screen input handlers** — inventory, shop, dialogue, shaft, camp trader, fort,
   console. They already read `InputRouter` snapshots. Move one screen at a time; do not take
   `UpdateGameScreen` in one pass. Consent, title, pause and settings already live in
   `OverlayInput`.
2. **`EngineHost : Game`** — devices, timestep, fonts, the canvas attach. `Game1` then only
   contains Ratna Bay. That is the last cut, and it is the one that makes a second executable
   cheap.

## What not to do

- Do not put shop tiles, descent rows or Northwatch colours in the engine.
- Do not make the engine depend on `RatnaBay.Domain`. A string material name is the seam;
  `WorldVector` is not.
- Do not revive parked features as a way to "exercise" the engine.
- Do not add a package for one type. The csproj waits until the table above is Domain-free.
