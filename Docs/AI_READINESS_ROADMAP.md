# AI Readiness Roadmap

This document describes how ready the Ratna Bay repository is for ongoing work with AI coding
tools, and the next changes that will make that work safer and more predictable.

Assessment date: 2026-08-27 (updated after the console pass and the marker extraction)

## Current assessment

The repository is approximately **85% complete** for AI-friendly architecture.

| Area | Done | Pending |
| --- | ---: | ---: |
| Asset and dependency cleanup | 100% | Maintenance only |
| Build and test reproducibility | 100% | — |
| Runtime verification of a client change | 85% | UI panels and saves unreachable from a script |
| Contributor guidance | 100% | Keep it current as boundaries move |
| Input boundary | 100% | Minor future refinements |
| HUD and rendering boundaries | 95% | 3D pass, weapon, door prompt, spike scenes |
| Styling consistency | 100% | Add to `UiTheme` rather than to a call site |
| Client-layer testability | 45% | Layout and theme are shared; still no headless Game tests |
| `Game1` decomposition | 60% | Lifecycle, world draw, input handlers remain |

Current repository hygiene: **9/10**
Current AI-readiness: **8.5/10**

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
  `Ui/UiTheme`.
- Every 2D screen has a named renderer under `Ui/`, including the developer console, which was
  the last one painting through the game loop's own helpers.
- `Ui/WorldProjector` is the seam for anything anchored in the world and drawn flat;
  `Ui/MarkerRenderer` uses it for nameplates, floating damage, threat arrows and yard signs.

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

### 1. Extract the remaining world presentation

The weapon overlay, the door prompt, the coach line and the pointer are still drawn from
`Game1`. The door prompt is the awkward one: it queries the world for a door, an actor and a
pickup before drawing, so it needs a snapshot built where those live.

### 2. Give the 3D pass a primitive seam

`DrawAuthoredWorld`, `DrawCube`, `DrawTexturedCube`, `DrawCarvedFace`, `DrawCrystal`, `DrawGlow`
and the cave-lighting setup are about 700 cohesive lines with no game rules in them. They are
the largest single extraction left, and they need a small interface for "draw this box with
this material" before the moodboard and stambha spike scenes can move out too.

### 3. Separate update logic from the game shell

Split input and simulation coordination into focused controllers that return explicit commands
to `Game1`. Screen handlers already interpret `InputRouter` snapshots, so they can move next.
`UpdateGameScreen` and `StartSession` should not be attempted in one pass — they touch too many
fields; split the dependencies first.

### 4. Add client-layer tests

`UiLayout` and `UiTheme` are the seam: both are pure data, but they live in the WindowsDX
project so `RatnaBay.Domain.Tests` cannot see them. A net9.0 layout/theme assembly would let a
test assert that a clickable row is the row on screen, and that every colour a renderer asks
for exists, without a graphics device.

### 5. Widen what a script can assert

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
