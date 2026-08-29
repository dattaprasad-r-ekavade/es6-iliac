# AI Readiness Roadmap

This document describes how ready the Ratna Bay repository is for ongoing work with AI coding
tools, and the next changes that will make that work safer and more predictable.

Assessment date: 2026-08-28 (updated after engine project split)

## Current assessment

The repository is approximately **99% complete** for AI-friendly architecture.

| Area | Done | Pending |
| --- | ---: | ---: |
| Asset and dependency cleanup | 100% | Maintenance only |
| Build and test reproducibility | 100% | — |
| Runtime verification of a client change | 85% | UI panels and saves unreachable from a script |
| Contributor guidance | 100% | Keep it current as boundaries move |
| Input boundary | 100% | — |
| HUD and rendering boundaries | 100% | — |
| Styling consistency | 100% | Add to `UiTheme` rather than to a call site |
| Client-layer testability | 45% | Layout and theme are shared; still no headless Game tests |
| `Game1` decomposition | 100% | Overlay snapshot and `IConsoleTarget` stay |
| Engine / game split | 100% | `RatnaBay.Engine` is its own project |

Current repository hygiene: **9/10**
Current AI-readiness: **9/10**

These are engineering estimates based on boundary coverage, testability, and the amount of
unrelated work still concentrated in `Game1`; they are not product-quality ratings.

## Completed foundations

- Removed unused downloaded assets and obsolete package dependencies.
- Standardized the solution on the .NET 9 SDK selected by `global.json`.
- Added [`AGENTS.md`](../AGENTS.md) with boundaries, recipes, required checks, and how to drive
  the game from a script.
- Added `.cursor/rules/ratnabay.mdc` so Cursor agents load the same boundaries automatically.
- `verify.ps1` is the single Windows entry point, and `-Pack` now ends by driving the packaged
  build through `Docs/scripts/smoke.rbs`.
- Centralized keyboard and mouse sampling in `Input/InputRouter`.
- Shared drawing through `Ui/UiCanvas`, hit-testing through `Ui/UiLayout`, and colour through
  `Ui/UiTheme`. The canvas owns its primitives rather than pointing nine delegates back at
  `Game1`.
- Every 2D screen has a named renderer under `Ui/`, including the developer console, the
  weapon overlay, the door/talk/pickup prompt, the coach line and the pointer.
- `Ui/WorldProjector` is the seam for anything anchored in the world and drawn flat;
  `Ui/MarkerRenderer` uses it for nameplates, floating damage, threat arrows and yard signs.
- `Render/SceneRenderer` is the 3D primitive seam: boxes, the crystal, the carved quad, the
  glow, and the two shaders. Per-frame state is set once through `Begin`. Material is a
  string (`stone`, `timber`, `cloth`, `earth`, `rope`); the renderer does not import Domain.
- `Render/ModelCache` loads, measures, normalises and draws imported props. Lighting is
  applied at load, not per mesh per frame.
- `Engine/FirstPersonView` is look, walk, jump and crouch. Collision is a callback. Spawn
  and speeds are constructor/property values, not Ratna Bay types.
- `World/WorldPresenter` walks a Ratna Bay manifest onto `SceneRenderer` / `ModelCache`. A
  second game writes a different presenter against the same primitives.
- `World/FigurePresenter` draws speakers, watchers, enemies and bolts through
  `BillboardRenderer`. Texture choice stays this game.
- `World/SpikeScenes` owns the moodboard, stambha trailer shot and generated-asset case.
- `Engine/CaptureHost` is screenshot warmup, the cover render target and PNG write. A script
  hold is a bool, so the host does not know about the console. `Ui/CoverRenderer` is this
  game's store cover; `Render/FaceSheet` is `--faces`.
- `Input/ListPicker` wraps or clamps a selected row from an `InputRouter` snapshot. Bounds
  are a callback, not `UiLayout`. Grid walk and 1–9 digits live there too.
  `Input/OverlayInput` owns consent, title, pause and settings. `Input/WorldPanelInput`
  owns inventory, shop, dialogue, shaft, camp, fort and the run-summary button. Selection
  and confirm only. Game1 still owns starting a game, buying, asking a topic. Do not pass
  `Game1` into a controller. `Input/ConsoleInput` owns the buffer and history.
- `Engine/EngineHost` is devices, timestep, fonts, the canvas attach, letterboxing and
  capture framing. `Game1` subclasses it. `--perf` prints a wall-clock frame summary on
  exit. `StoneTextures` has no Domain import; cave colours become a palette in
  `WorldPresenter.PaletteOf`.
- [`Docs/ENGINE.md`](ENGINE.md) is the reuse map: three layers, the engine table, how a
  second game starts. `src/RatnaBay.Engine` is the project; doctor asserts it has no Domain
  reference.

## The scripted gate

Worth calling out separately, because it is the only way to assert on a running client and it
did not work until recently. The failure modes that were fixed are the ones worth remembering,
because each of them made a broken run look like a passing one:

- `--script` opened the title screen, so there was no world for a command to act on.
- A missing script path was ignored rather than reported, so a typo exited zero.
- An unknown command was found when its statement came up, after the asserts above it had
  already reported success.
- `assert where has yard` passed on the title screen, because "the yard" is what `where`
  answers when there is no run at all.

A scripted gate that can half-run is worse than no gate. Anything added to it should fail
loudly and early.

## Next changes

`RatnaBay.Engine` is its own project. It must stay Domain-free; `doctor` asserts that.
`WorldPresenter.PaletteOf` is this game's remaining theme → palette step. `LaunchOptions`
is this game (`--mine`, `--yard`).

Remaining:

### 1. Add client-layer tests

`UiLayout` and `UiTheme` are the seam: both are pure data, but they live in the WindowsDX
engine project so `RatnaBay.Domain.Tests` cannot see them. A net9.0 layout/theme assembly would let a
test assert that a clickable row is the row on screen, and that every colour a renderer asks
for exists, without a graphics device.

### 2. Widen what a script can assert

`IConsoleTarget` cannot reach the shop or dialogue panels, equipping, save/load round trips, or
story flags. Each addition should be deliberate — the value of the interface is that it is
narrow — but save/load and equipping are the two most worth having, because both are places a
regression is invisible until a player loses something.

## Definition of done

The repository should be considered AI-ready when:

- `Game1` is a small lifecycle coordinator rather than a multi-thousand-line feature container;
- each screen has an explicit state snapshot and renderer/controller boundary;
- client-layer behavior can be tested without opening a MonoGame window;
- one verification command proves build, tests, simulation, content, packaging, and a scripted
  playthrough;
- `AGENTS.md` tells an AI tool where each kind of change belongs and how to validate it.
