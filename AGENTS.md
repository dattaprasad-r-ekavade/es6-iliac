# Ratna Bay AI contributor guide

Ratna Bay is a first-person roguelike. Game rules live in engine-free C#; MonoGame only
renders, samples input, and loads content. Read this file before changing code.

## Project map

| Path | What belongs here |
| --- | --- |
| `src/RatnaBay.Domain/` | Engine-independent rules, state, saves, generation. **No MonoGame types.** |
| `src/RatnaBay.Game/` | WindowsDX client: devices, the frame, world draw, audio. |
| `src/RatnaBay.Game/Ui/` | Every 2D screen and HUD renderer, plus shared canvas and hit-test layout. |
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

## Design boundaries

- Rules and persistence in `RatnaBay.Domain`. Presentation and device APIs in the game project.
- Sample keyboard and mouse only through `InputRouter`. Screen handlers interpret a snapshot;
  they do not call `Keyboard.GetState` / `Mouse.GetState`.
- Draw 2D UI through `UiCanvas`. Do not open `SpriteBatch` from a screen renderer.
- Hit-test rectangles live in `UiLayout`. Input and drawing must share them. If a clickable
  row is not the row on screen, the numbers have drifted.
- Build presentation snapshots (`WorldHudState`, `OverlayState`, `MenuState`) in `Game1`.
  Renderers receive those snapshots, not the rest of the coordinator.
- UI coordinates are a 1280×720 logical canvas (`UiLayout.Width` / `Height`) and must account
  for letterboxing.
- Save/load changes need a round-trip test and backward-compatible defaults.
- Generated mines are deterministic from their seed and must not be written into installed
  content during play.
- Do not revive parked features or add a package without a focused spike and a passing
  release-shaped build.

## Where a screen lives

| Player-facing surface | Renderer | Layout |
| --- | --- | --- |
| World HUD (vitals, toasts, crosshair, spell bar) | `Ui/HudRenderer.cs` | snapshot in `WorldHudState` |
| Pause / help / settings | `Ui/OverlayRenderer.cs` | `UiLayout.PauseItem`, `SettingsRow` |
| Title menu | `Ui/MenuRenderer.cs` | `UiLayout.MenuItem` |
| Character / pack / stones | `Ui/CharacterRenderer.cs` | `UiLayout.InventoryTile`, `EquippedSlot` |
| Dialogue | `Ui/DialogueRenderer.cs` | `UiLayout.DialogueTopic` |
| Stall | `Ui/ShopRenderer.cs` | `UiLayout.ShopItem` |
| Journal | `Ui/JournalRenderer.cs` | local panel |
| Recording consent | `Ui/ConsentRenderer.cs` | `UiLayout.ConsentButton` |
| Shut door, camp trader, shaft, run summary | `Ui/DescentRenderer.cs` | `CampRow`, `DepthRow`, `SummaryButton` |

World-space draw (rooms, enemies, weapon, nameplates, door prompts) stays in `Game1` until a
later extraction. Nameplates need a projection callback; do not expose all of `Game1` to
obtain one.

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

### Change a content manifest

Edit the JSON under `src/RatnaBay.Game/Content/{World,Dialogue,Quests,Shops}/`. Run
`dotnet run --project tools/RatnaBay.Tools -- validate`. Invalid JSON must fail validation
rather than crash the scene; hot-reload already keeps the last valid room.

### Change an input binding

1. Sample stays in `InputRouter`.
2. Meaning of the key stays in the screen handler in `Game1` (or a future controller).
3. Update the help overlay in `OverlayRenderer.DrawHelpOverlay` in the same change.
4. Parked features: a binding that does nothing must not appear in help. See `ParkedFeatures`.

### Validate and commit

Make the smallest coherent change. Run `.\verify.ps1` (or the domain suite plus a WindowsDX
compile on Linux). When removing content or a dependency, update the doctor command,
attribution records, and this file in the same change.

## Canonical design docs

- [`Docs/design_pivot.md`](Docs/design_pivot.md) — what the game is.
- [`Docs/PRODUCTION_PLAN.md`](Docs/PRODUCTION_PLAN.md) — what gets built, and closed decisions.
- [`Docs/TRAILER.md`](Docs/TRAILER.md) — scope contract for the slice.
- [`Docs/AI_READINESS_ROADMAP.md`](Docs/AI_READINESS_ROADMAP.md) — remaining client decomposition.
