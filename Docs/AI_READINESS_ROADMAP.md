# AI Readiness Roadmap

This document describes how ready the Ratna Bay repository is for ongoing work with AI coding
tools, and the next changes that will make that work safer and more predictable.

Assessment date: 2026-08-26

## Current assessment

The repository is approximately **60% complete** for AI-friendly architecture.

| Area | Done | Pending |
| --- | ---: | ---: |
| Asset and dependency cleanup | 100% | Maintenance only |
| Build and test reproducibility | 85% | CI and one unified verification command |
| Contributor guidance | 70% | More change recipes and architecture mapping |
| Input boundary | 100% | Minor future refinements |
| HUD and rendering boundaries | 45% | Remaining world and screen renderers |
| Client-layer testability | 35% | State and layout tests outside the domain |
| `Game1` decomposition | 30% | Still roughly 6,500 lines |

Current repository hygiene: **8/10**  
Current AI-readiness: **6/10**

These are engineering estimates based on boundary coverage, testability, and the amount of
unrelated work still concentrated in `Game1`; they are not product-quality ratings.

## Completed foundations

- Removed unused downloaded assets and obsolete package dependencies.
- Standardized the solution on the .NET 9 SDK selected by `global.json`.
- Added [`AGENTS.md`](../AGENTS.md) with project boundaries, invariants, and required checks.
- Centralized keyboard and mouse sampling in `InputRouter`.
- Added `WorldHudState` and `HudRenderer` for world-HUD presentation.
- Added `OverlayState` and `OverlayRenderer` for pause, help, and settings screens.
- Kept the repository clean after each change with focused commits.
- Maintained a warning-free Release build, 595 passing tests, tool validation, and a deterministic
  gameplay simulation.

## Next changes

### 1. Share the UI drawing boundary

Add a shared `UiCanvas` abstraction for `HudRenderer`, `OverlayRenderer`, and future screen
renderers. This removes repeated callback wiring and gives AI one consistent API for panels,
fills, borders, and text.

### 2. Extract the character and inventory screen

Create `CharacterRenderer` and `CharacterScreenState`. Keep item selection and input handling in
`Game1`, while moving layout and presentation into the renderer.

### 3. Extract the remaining screens

Move dialogue, journal, shop, camp trader, depth choice, and run-summary rendering into focused
renderer classes with explicit state snapshots.

### 4. Extract remaining world presentation

Move enemy nameplates, floating combat numbers, threat arrows, the spell bar, and content-error
display behind the rendering boundary. World projection should be supplied as a narrow callback,
not by exposing all of `Game1`.

### 5. Separate update logic from the game shell

Split input and simulation coordination into focused controllers that return explicit commands to
`Game1`. The game class should eventually coordinate lifecycle, device state, and draw order—not
contain every gameplay decision.

### 6. Add client-layer tests

Add headless tests for state snapshot creation, screen visibility rules, selection bounds, and
layout invariants. The current 595 tests primarily cover the engine-independent domain.

### 7. Add one verification entry point

Create `verify.ps1` to run the Release build, tool doctor, domain tests, deterministic simulation,
content validation, and packaging checks in one command.

### 8. Expand contributor recipes

Extend `AGENTS.md` with short recipes for:

- adding a domain rule;
- adding a screen or renderer;
- changing content manifests;
- changing input bindings;
- validating and committing a change.

## Definition of done

The repository should be considered AI-ready when:

- `Game1` is a small lifecycle coordinator rather than a multi-thousand-line feature container;
- each screen has an explicit state snapshot and renderer/controller boundary;
- client-layer behavior can be tested without opening a MonoGame window;
- one verification command proves build, tests, simulation, content, and packaging health;
- `AGENTS.md` tells an AI tool where each kind of change belongs and how to validate it.
