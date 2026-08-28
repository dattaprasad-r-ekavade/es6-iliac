# Ratna Bay AI contributor guide

Ratna Bay is a first-person roguelite. Game rules live in engine-free C#; MonoGame only
renders, samples input, and loads content. Read this file before changing code.

## Project map

| Path | What belongs here |
| --- | --- |
| `src/RatnaBay.Domain/` | Engine-independent rules, state, saves, generation. **No MonoGame types.** |
| `src/RatnaBay.Game/` | WindowsDX client: devices, the frame, world draw, audio. |
| `src/RatnaBay.Game/Engine/` | First-person view (look, walk, jump, crouch). No mines or Ratna Bay. |
| `src/RatnaBay.Game/Ui/` | Every 2D screen and HUD renderer, plus shared canvas and hit-test layout. |
| `src/RatnaBay.Game/Render/` | 3D primitives (`SceneRenderer`), imported models (`ModelCache`), generated sprites. |
| `src/RatnaBay.Game/World/` | Live world, encounters, `WorldPresenter`, `FigurePresenter`, spike scenes. |
| `src/RatnaBay.Game/Input/` | Device sampling (`InputRouter`). |
| `src/RatnaBay.Game/Content/` | JSON manifests (world, dialogue, quests, shops) and bundled fonts. |
| `tools/RatnaBay.Tools/` | `doctor`, `validate`, `sim`, `mine`, `review`. |
| `tests/RatnaBay.Domain.Tests/` | Headless domain tests. |
| `Docs/` | Design and production records. Update them when a closed decision or behaviour changes. |
| `ParkedFeatures.cs` | Built, tested, unreachable player-facing surfaces. Do not revive without a product decision. |

`Game1` coordinates lifecycle, device state, and draw order. New independent systems do not
go in `Game1`.

## Required checks

SDK version is pinned by `global.json` (currently 9.0.302).

On Windows, the one command that proves a change:

```powershell
.\verify.ps1
```

That is a Release build, tool doctor, domain tests, content validation, and the deterministic
simulation. Add `-Pack` to also run the publish gate (self-test of the packaged exe).

Faster loops:

```powershell
.\build.ps1                                  # restore, build, doctor, domain tests
dotnet test tests\RatnaBay.Domain.Tests
dotnet run --project tools\RatnaBay.Tools -- validate
dotnet run --project tools\RatnaBay.Tools -- sim
```

Linux agents cannot produce the WindowsDX client. They can still run the domain suite:

```bash
dotnet test tests/RatnaBay.Domain.Tests
dotnet build src/RatnaBay.Game/RatnaBay.Game.csproj -p:EnableWindowsTargeting=true -p:EnableMGCBItems=false
```

Do not commit `build/`, `bin/`, `obj/`, captures, or generated world output.

## Driving the game from a script

This is the most useful thing here for checking a client-side change, and the only way to
assert on a running build without a person at the keyboard. The console takes commands, a
script is a file of them, and a failed `assert` makes the process exit non-zero.

```powershell
build\RatnaBay.exe --script Docs\scripts\smoke.rbs
build\RatnaBay.exe --exec "goto shaft; look at shaft; hud off" --screenshot shot.png
```

What to know before writing one:

- **A script opens in the yard.** Commands act on a world and the menu has none. Pass `--mine`,
  `--moodboard` or `--stambha` to script a different scene.
- **One statement per frame.** `descend` then `enemies` sees a populated room because a frame
  ticked in between. This is why the queue is pumped rather than run at once.
- **`wait` is in simulated seconds, not frames.** Capture mode is uncapped, so a frame count
  buys an unpredictable and usually tiny amount of game time.
- **Unknown commands fail the run before the first statement.** A typo does not cost the whole
  script and then report success from the asserts above it.
- **`help` lists every command.** `GameConsole.Build` is the only place they are registered;
  there is no second table to keep in step.
- **Assert on something specific.** `assert where has yard` also passes on the title screen,
  because "the yard" is what `where` says when there is no run. Prefer named geometry.

What the console **cannot** reach, so do not try to assert it this way: shop and dialogue
panels, the depth-choice and camp-trader UI, equipping, save/load round trips, story flags,
and audio. Those want a domain test, or a person. `IConsoleTarget` is the full list of what a
script can touch — if something is not on it, add it there deliberately rather than widening
the interface to all of `Game1`.

## Design boundaries

- Rules and persistence in `RatnaBay.Domain`. Presentation and device APIs in the game project.
- Sample keyboard and mouse only through `InputRouter`. Screen handlers interpret a snapshot;
  they do not call `Keyboard.GetState` / `Mouse.GetState`.
- Draw 2D UI through `UiCanvas`. Do not open `SpriteBatch` from a screen renderer. There is one
  deliberate exception, `DrawCoverArt`: the store cover is 1260×1000, so the UI transform that
  letterboxes a 16:9 canvas would put bars down its sides. Leave it alone.
- Hit-test rectangles live in `UiLayout`. Input and drawing must share them. If a clickable
  row is not the row on screen, the numbers have drifted.
- Colours come from `UiTheme`, by role. Do not write `new Color(...)` in a screen renderer for
  anything the theme already names.
- 3D boxes, the crystal and the carved quad go through `SceneRenderer`. Imported props go
  through `ModelCache`. Do not reopen a `BasicEffect` or a vertex buffer from a screen renderer.
- Look, walk, jump and crouch go through `FirstPersonView`. Collision is a callback; do not
  put mines in the view.
- Authored world boxes, lights and imported props go through `WorldPresenter`. A second game
  writes a different presenter; it should not subclass `Game1`. See `Docs/ENGINE.md`.
- Speakers, watchers, enemies and bolts go through `FigurePresenter`. Moodboard, stambha and
  the asset case go through `SpikeScenes`.
- Anything anchored to a point in the world but drawn flat goes through `WorldProjector` and
  `MarkerRenderer`. Do not hand a renderer the camera to get a screen position.
- Build presentation snapshots (`WorldHudState`, `OverlayState`, `MenuState`, `NameplateState`,
  `PromptState`) in `Game1`. Renderers receive those snapshots, not the rest of the coordinator.
- UI coordinates are a 1280×720 logical canvas (`UiLayout.Width` / `Height`) and must account
  for letterboxing.
- Save/load changes need a round-trip test and backward-compatible defaults.
- Generated mines are deterministic from their seed and must not be written into installed
  content during play.
- Do not revive parked features or add a package without a focused spike and a passing
  release-shaped build. Do not add `RatnaBay.Engine.csproj` until every engine type compiles
  without `using RatnaBay.Domain`.

## Where a screen lives

| Player-facing surface | Renderer | Layout |
| --- | --- | --- |
| World HUD (vitals, toasts, crosshair, spell bar, coach) | `Ui/HudRenderer.cs` | snapshot in `WorldHudState` |
| Pause / help / settings / pointer | `Ui/OverlayRenderer.cs` | `UiLayout.PauseItem`, `SettingsRow` |
| Title menu | `Ui/MenuRenderer.cs` | `UiLayout.MenuItem` |
| Character / pack / stones | `Ui/CharacterRenderer.cs` | `UiLayout.InventoryTile`, `EquippedSlot` |
| Dialogue | `Ui/DialogueRenderer.cs` | `UiLayout.DialogueTopic` |
| Stall | `Ui/ShopRenderer.cs` | `UiLayout.ShopItem` |
| Journal | `Ui/JournalRenderer.cs` | local panel |
| Recording consent | `Ui/ConsentRenderer.cs` | `UiLayout.ConsentButton` |
| Shut door, camp trader, shaft, run summary | `Ui/DescentRenderer.cs` | `CampRow`, `DepthRow`, `SummaryButton` |
| Door / talk / pickup prompt | `Ui/PromptRenderer.cs` | `UiLayout.TalkPrompt`, `SinglePrompt`; snapshot in `PromptState` |
| Held weapon sprite | `Ui/WeaponRenderer.cs` | `UiLayout.ShieldGrip`; pose from `WeaponView` |
| Nameplates, floating damage, threat arrows, yard signs, content errors | `Ui/MarkerRenderer.cs` | projected via `WorldProjector` |
| Developer console and watches | `Ui/ConsoleRenderer.cs` | local panels |
| Lit boxes, crystal, carved faces, glow | `Render/SceneRenderer.cs` | per-frame `Begin` |
| Imported props | `Render/ModelCache.cs` | loaded once, drawn by key |
| Speakers, watchers, enemies, bolts | `World/FigurePresenter.cs` | `BillboardRenderer` |
| Moodboard, stambha, asset case | `World/SpikeScenes.cs` | SceneRenderer + canvas; `--moodboard` / `--stambha` |

Still in `Game1` and not yet extracted: the capture/screenshot host (`--screenshot`, `--cover`,
warmup), and the screen input handlers. Extract those when a change needs to touch them, not
speculatively. Do not put look/walk, 3D primitive drawing, authored-world iteration, figures or
spike scenes back into `Game1` — those seams are `FirstPersonView`, `SceneRenderer`,
`WorldPresenter`, `FigurePresenter` and `SpikeScenes`. A second game should not subclass
`Game1`; see [`Docs/ENGINE.md`](Docs/ENGINE.md).

## Recipes

### Add a domain rule

1. Put the rule in the matching `RatnaBay.Domain` folder (`Combat/`, `Run/`, `Items/`, …).
2. Add or extend a test in `tests/RatnaBay.Domain.Tests`.
3. If the player must see it, add presentation in `Ui/` and any input in `Game1`'s existing
   screen handler — not a new branch of `Update` that samples the device itself.
4. If it is saved, update `SaveGame` with a default that keeps old files loading, and assert
   the round trip.

### Add a screen or change a panel

1. Put layout rectangles in `UiLayout` first, even if only drawing needs them today.
2. Add or extend a renderer under `Ui/` that takes `UiCanvas` plus a snapshot or a narrow
   domain object.
3. Construct it from `UiScreens` if it is a new class.
4. Keep selection, open/close, and side effects in `Game1`.
5. Hit-test with the same `UiLayout` method the renderer uses to draw the row.
6. Use `_ui.Row(bounds, selected)` for a list row and `UiTheme` for colour. If a colour is
   genuinely new, add it to `UiTheme` with a name saying what it is for.

### Add a landmark or a fixture to the yard

`Surface` owns both the geometry and the names for it. Add the fixture to `SurfaceFixture`,
build it in `Surface.Build`, give it a position, return that from `PositionOf`, and add the
names people would type to `Surface.Landmarks`. The console picks all of it up; there is no
second table. `SurfaceYardTests` will check every name reaches the thing it names.

### Change a content manifest

Edit the JSON under `src/RatnaBay.Game/Content/{World,Dialogue,Quests,Shops}/`. Run
`dotnet run --project tools/RatnaBay.Tools -- validate`. Invalid JSON must fail validation
rather than crash the scene; hot-reload already keeps the last valid room.

### Change an input binding

1. Sample stays in `InputRouter`.
2. Meaning of the key stays in the screen handler in `Game1` (or a future controller).
3. Update the help overlay in `OverlayRenderer.DrawHelpOverlay` in the same change.
4. Parked features: a binding that does nothing must not appear in help. See `ParkedFeatures`.

### Add a console command

Register it in `GameConsole.Build`. If it needs something from the running game that
`IConsoleTarget` does not expose, add that one member rather than widening the interface — the
value of it is that it is a short, readable list of everything a script can do. Then say so in
`help` text, because `help` is generated from the registry and is what an agent reads first.

### Validate and commit

Make the smallest coherent change. Run `.\verify.ps1` (or the domain suite plus a WindowsDX
compile on Linux). For a change a player would see, add a line to `Docs/scripts/smoke.rbs` and
run `.\verify.ps1 -Pack`, which drives the packaged build through it. When removing content or
a dependency, update the doctor command, attribution records, and this file in the same change.

## Canonical design docs

- [`Docs/design_pivot.md`](Docs/design_pivot.md) — what the game is.
- [`Docs/PRODUCTION_PLAN.md`](Docs/PRODUCTION_PLAN.md) — what gets built, and closed decisions.
- [`Docs/TRAILER.md`](Docs/TRAILER.md) — scope contract for the slice.
- [`Docs/ENGINE.md`](Docs/ENGINE.md) — which client types a second game can reuse, and the gate
  for cutting a `RatnaBay.Engine` project.
- [`Docs/AI_READINESS_ROADMAP.md`](Docs/AI_READINESS_ROADMAP.md) — remaining client decomposition.
