# AI Readiness Roadmap

This document describes how ready the Ratna Bay repository is for ongoing work with AI coding
tools, and the next changes that will make that work safer and more predictable.

Assessment date: 2026-08-26 (updated after the Ui/ extraction)

## Current assessment

The repository is approximately **75% complete** for AI-friendly architecture.

| Area | Done | Pending |
| --- | ---: | ---: |
| Asset and dependency cleanup | 100% | Maintenance only |
| Build and test reproducibility | 95% | Packaged self-test still Windows-only |
| Contributor guidance | 95% | Keep recipes in `AGENTS.md` current |
| Input boundary | 100% | Minor future refinements |
| HUD and rendering boundaries | 80% | Nameplates, floating numbers, door prompts, weapon |
| Client-layer testability | 40% | Layout is shared; still no headless Game tests |
| `Game1` decomposition | 55% | Lifecycle, world draw, and input handlers remain |

Current repository hygiene: **8/10**
Current AI-readiness: **7.5/10**

These are engineering estimates based on boundary coverage, testability, and the amount of
unrelated work still concentrated in `Game1`; they are not product-quality ratings.

## Completed foundations

- Removed unused downloaded assets and obsolete package dependencies.
- Standardized the solution on the .NET 9 SDK selected by `global.json`.
- Added [`AGENTS.md`](../AGENTS.md) with project boundaries, recipes, and required checks.
- Added `.cursor/rules/ratnabay.mdc` so Cursor agents load the same boundaries automatically.
- Added `verify.ps1` as the single Windows verification entry point.
- Centralized keyboard and mouse sampling in `Input/InputRouter`.
- Shared drawing through `Ui/UiCanvas` and hit-test rectangles through `Ui/UiLayout`.
- World HUD, overlays, menu, character, dialogue, shop, journal, consent, and descent
  screens each have a named renderer under `Ui/`.
- `HudRenderer` no longer receives `GameSession`; it paints from `WorldHudState` only.

## Next changes

### 1. Extract remaining world presentation

Move enemy nameplates, floating combat numbers, threat arrows, door prompts, and content-error
display behind the rendering boundary. World projection should be supplied as a narrow
callback, not by exposing all of `Game1`.

### 2. Separate update logic from the game shell

Split input and simulation coordination into focused controllers that return explicit commands
to `Game1`. The game class should eventually coordinate lifecycle, device state, and draw
order — not contain every gameplay decision. Screen handlers already interpret `InputRouter`
snapshots; they can move out next.

### 3. Add client-layer tests

Add headless tests for snapshot creation, screen visibility rules, selection bounds, and
layout invariants. `UiLayout` is the seam: bounds are already shared, but they still live in
the WindowsDX project. A net9.0 layout helper would let `RatnaBay.Domain.Tests` (or a new
test project) assert that a clickable row is the row on screen without a graphics device.

### 4. Keep `Game1` shrinking

World draw, camera, combat feel (hitstop, shake, stride), and capture flags remain in
`Game1`. Extract those as named types when a change needs to touch them, not as a
speculative rewrite.

## Definition of done

The repository should be considered AI-ready when:

- `Game1` is a small lifecycle coordinator rather than a multi-thousand-line feature container;
- each screen has an explicit state snapshot and renderer/controller boundary;
- client-layer behavior can be tested without opening a MonoGame window;
- one verification command proves build, tests, simulation, content, and packaging health;
- `AGENTS.md` tells an AI tool where each kind of change belongs and how to validate it.
