# Ratna Bay AI contributor guide

## Project map

- `src/RatnaBay.Domain/` contains engine-independent game rules, state, save contracts and
  deterministic generation. Keep MonoGame types out of this project.
- `src/RatnaBay.Game/` contains the MonoGame WindowsDX client, rendering, input, screen flow and
  content loading. `Game1` coordinates the lifecycle; new features should live in focused classes.
- `tools/RatnaBay.Tools/` contains validation, deterministic simulation and asset inspection
  commands.
- `tests/RatnaBay.Domain.Tests/` contains headless domain tests.
- `Docs/` contains design, production and feasibility records. Update documentation when a
  behavior or closed decision changes.

## Required checks

Use the .NET 9.0.302 SDK selected by `global.json`:

```powershell
.\build.ps1 -Configuration Release
```

This restores the local MonoGame tools, builds the solution, runs the doctor check and executes
the domain tests. For a release-shaped verification, also run:

```powershell
.\publish.ps1 -Clean -Configuration Release
```

The publish gate includes the deterministic simulation, content checks and the packaged
self-test. Do not commit `build/`, `bin/`, `obj/`, captures or generated world output.

## Design boundaries

- Put rules and persistence in `RatnaBay.Domain`; keep presentation and device APIs in the game
  project.
- Keep `Game1` as the MonoGame lifecycle coordinator, not the home for new independent systems.
- Route keyboard and mouse sampling through `InputRouter`; screen handlers may interpret input,
  but should not sample the device directly.
- Build world-HUD presentation data in `WorldHudState` and keep layout/drawing in
  `HudRenderer`; `Game1` should only collect state and coordinate draw order.
- UI coordinates use the 1280x720 logical canvas and must account for letterboxing.
- Save/load changes require a round-trip test and must preserve backward-compatible defaults.
- Generated mines are deterministic from their seed and should not be written into installed
  content during play.

## Change discipline

- Make the smallest coherent change and run the relevant test gate before committing.
- Do not revive parked features or add a package without a focused spike and a passing release
  build.
- When removing content or dependencies, update the doctor command, attribution records and
  release verification in the same change.
