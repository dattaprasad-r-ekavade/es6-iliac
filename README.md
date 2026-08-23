# Ratna Bay

Ratna Bay is now a code-first Windows game built with MonoGame and a custom toolchain.

The former Unity project is preserved, without deletion, at:

`D:\Projects\Elder Scrolls 6_Unity_Archive_2026-08-22`

The current workspace is intentionally small and rebuildable:

```text
src/RatnaBay.Domain          Engine-independent game rules and save contracts
src/RatnaBay.Game            MonoGame WindowsDX client
tools/RatnaBay.Tools         Validators, world compiler, and authoring commands
tests/RatnaBay.Domain.Tests  Headless domain tests
Docs/                        Pivot plan and toolchain strategy
```

## Build

```powershell
.\build.ps1
```

The script restores the project-local MonoGame content tools, restores the solution,
builds the game, and runs the domain tests.

## Run the shell

```powershell
dotnet run --project src\RatnaBay.Game -- --mode menu
```

The default shell opens with a menu. Choose **Start New Game** to enter the first
Northwatch scene.

Direct release builds can be launched in a specific state:

```powershell
& "src\RatnaBay.Game\bin\Release\net9.0-windows\RatnaBay.Game.exe" --mode menu
& "src\RatnaBay.Game\bin\Release\net9.0-windows\RatnaBay.Game.exe" --mode scene
```

Menu: click or hover to choose, or Up/Down and Enter. **Continue** appears once a save
exists.

In the scene, the mouse looks and WASD moves at 6 m/s (11 sprinting). Left click attacks,
right click guards (one-handed weapons only), E talks to a facing NPC, takes a world pickup,
or interacts with an authored door. B opens a merchant shop, P pickpockets, J opens the
journal, I or K opens character inventory/equipment/skills, Ctrl toggles crouch, and mouse clicks select dialogue/shop options. Q casts the readied
spell and 4-8 choose it. Space jumps. Click the world to take the pointer, Tab to give it back. Shift
sprints and spends stamina, F2 opens settings, F5 saves, F9 loads, **F1 lists every control**,
and M or Escape returns to the menu. Escape is intentionally harmless on the main menu; use the
visible Exit item to close the game.

The HUD reads live values from `RatnaBay.Domain` — health, prana and stamina are the
domain's own numbers, not painted ones. Saves are written to
`%APPDATA%\RatnaBay\ratnabay_save.json`.

Capture a screenshot of any screen without playing to it:

```powershell
& "build\RatnaBay.exe" --mode scene --screenshot shot.png
```

A headless check of the whole save round trip, with no window:

```powershell
& "build\RatnaBay.exe" --selftest
```

This also runs automatically as the last gate in `publish.ps1`.

World geometry, doors, dungeon rooms, patrols, and pickups are authored in
`src/RatnaBay.Game/Content/World/northwatch.json`; NPC placements and keyword responses are
authored in `src/RatnaBay.Game/Content/Dialogue/northwatch.json`. Quest and shop data live
in the corresponding `Content/Quests` and `Content/Shops` folders.
When running the scene from the project output, valid edits hot-reload; invalid edits leave
the last valid room active. Validate the source data with:

```powershell
dotnet run --project tools\RatnaBay.Tools -- validate
```

The Steam presentation target is borderless fullscreen by default. The game authors UI
against a 1280×720 logical canvas and fits it uniformly into the active display, preserving
readability at 720p, 1080p, 1440p, 4K, and wider aspect ratios. F11 toggles a 1280×720
windowed development view; `--windowed` can be passed to a launch command for the same mode.

UI typography is reproducible and bundled: Cinzel is used for fantasy headings, Noto Sans
for dense UI/body copy, and FontStashSharp rasterizes both at 2× resolution before the
logical-canvas scale. License and attribution files live beside the font assets.

## Direction

Read [`Docs/MONOGAME_PIVOT_PLAN.md`](Docs/MONOGAME_PIVOT_PLAN.md) before adding systems.
The guiding rule is to keep game rules and data independent from MonoGame, while making
rendering, tools, content, and builds explicit C# projects or command-line steps.

**The plan of record is [`Docs/PRODUCTION_PLAN.md`](Docs/PRODUCTION_PLAN.md).** Read it first;
where the older documents disagree with it, it wins.

Supporting research and process notes:

- [`Docs/DAGGERFALL_SCOPE_AND_BUILD_RESEARCH.md`](Docs/DAGGERFALL_SCOPE_AND_BUILD_RESEARCH.md)
- [`Docs/SOLO_AGILE_DEVELOPMENT_PLAN.md`](Docs/SOLO_AGILE_DEVELOPMENT_PLAN.md)
- [`Docs/ITERATION_BOARD.md`](Docs/ITERATION_BOARD.md)
- [`Docs/KANBAN.md`](Docs/KANBAN.md)
- [`Docs/STEAM_PRESENTATION_BASELINE.md`](Docs/STEAM_PRESENTATION_BASELINE.md)
- [`Docs/COMMUNITY_TOOLS_BASELINE.md`](Docs/COMMUNITY_TOOLS_BASELINE.md)
