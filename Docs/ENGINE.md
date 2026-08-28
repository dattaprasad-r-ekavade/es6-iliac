# Engine vs game

Ratna Bay is one game on a small first-person engine. The engine is not a second product yet;
it is the set of types a different game could take without taking mines, stones, or Northwatch.
This file is the map for that split, so the next extraction has somewhere to land and a second
game has somewhere to start.

## Three layers

| Layer | What it is allowed to know | Today |
| --- | --- | --- |
| **Rules** (`RatnaBay.Domain`) | Combat, saves, generation, items. No MonoGame types. | Own project. Reuse as-is only if the next game wants these rules. |
| **Engine** (this document) | Devices, the frame, a first-person view, boxes, imported models, a 2D canvas, input sampling. No mines, no quests, no Ratna Bay. | Types inside `RatnaBay.Game`, listed below. Not yet their own project. |
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
| `InputRouter` | One keyboard/mouse sample per frame. |
| `UiCanvas` | 2D primitives. Logical size is constructor arguments, not a Ratna Bay constant. `Scrim` still uses this game's `UiTheme` colours — a second game should call `Panel` itself until that takes arguments. |
| `WorldProjector` | World point → logical canvas. Logical size is passed in. |

`StoneTextures` is *almost* engine: palettes are three colours. `FromTheme(CaveTheme)` is the
one Ratna Bay leak — a second game constructs `StonePalette` itself.

## What stays this game

- `Game1` — lifecycle, screen dispatch, the console, capture flags.
- `WorldPresenter` — walks a Ratna Bay manifest onto `SceneRenderer` / `ModelCache`. A
  different game writes a different presenter.
- Every renderer under `Ui/` except the canvas itself. `UiLayout` and `UiTheme` are this
  game's panels and palette.
- `WorldRuntime`, `Encounter`, `GameSession`, spike scenes (moodboard, stambha, asset case).

## How a second game would start

1. Copy or reference the engine types above. Do not copy `Game1`.
2. Subclass `Microsoft.Xna.Framework.Game` (or a future `EngineHost` once one exists).
3. Own a `FirstPersonView`, a `SceneRenderer`, a `ModelCache`, a `UiCanvas`.
4. Each frame: sample `InputRouter`, `view.Step(...)`, `view.RebuildView()`,
   `scene.Begin(...)`, draw your own world, `canvas.Begin()` / your HUD / `canvas.End()`.
5. Write a presenter that turns *your* world data into `DrawCube` / `DrawWorldBox` / `ModelCache.Draw`.
6. Keep your rules in an engine-free project, the way `RatnaBay.Domain` is.

Do not add a `RatnaBay.Engine` csproj until every type in the table above compiles without
`using RatnaBay.Domain`. That is the gate. `SceneRenderer` already does. `StoneTextures.FromTheme`
does not; move it (or stop calling it from the texture type) before cutting the project.

## What is still in Game1 that will move

In this order, because each cut has to be provably separable:

1. **Billboard pass** — actors, enemies, bolts already draw through `BillboardRenderer`; Game1
   still sorts and picks textures.
2. **Spike scenes** — moodboard, stambha, asset case. They are SceneRenderer with different
   lights. They can move once nothing in them reaches through to a session.
3. **Capture / screenshot host** — `--screenshot`, `--cover`, warmup. Engine concern, still
   tangled with Game1 fields.
4. **Screen input handlers** — menu, pause, inventory. They already read `InputRouter`
   snapshots. Move one screen at a time; do not take `UpdateGameScreen` in one pass.
5. **`EngineHost : Game`** — devices, timestep, fonts, the canvas attach. `Game1` then only
   contains Ratna Bay. That is the last cut, and it is the one that makes a second executable
   cheap.

## What not to do

- Do not put shop tiles, descent rows or Northwatch colours in the engine.
- Do not make the engine depend on `RatnaBay.Domain`. A string material name is the seam;
  `WorldVector` is not.
- Do not revive parked features as a way to "exercise" the engine.
- Do not add a package for one type. The csproj waits until the table above is Domain-free.
