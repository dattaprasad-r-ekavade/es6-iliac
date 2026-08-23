# Ratna Bay Kanban

This is the active Kanban view for the solo development project. Keep work-in-progress
small enough that every iteration produces a playable build.

**Plan of record:** [`PRODUCTION_PLAN.md`](PRODUCTION_PLAN.md). The backlog below is
subordinate to it; anything on the plan's parked list does not belong on this board.

## Work-in-progress limit

- One primary implementation item.
- One supporting documentation, research, or verification item only when it directly
  unblocks the primary item.

## Current board

### Backlog

Ordered by the production plan. Nothing here is pulled until the item above it is playable.

- External playtest sign-off for the completed Iteration 12 slice.
### In Progress

- Three independent external playthroughs and notes.

### Review / Playtest

- Save, quit, relaunch, Continue — verify the character comes back whole.
- Sprint until stamina empties; confirm the bar is the domain's number.
- Attack, guard, cast, win and lose the camp fight; verify the held sword keeps its grip anchored.
- Borderless fullscreen on physical 720p, 1080p and ultrawide displays.

### Done

- Unity gameplay rules ported to `RatnaBay.Domain`: 2,560 C# lines, 295 tests, ~160 ms.
- The domain drives the running game; the HUD shows live vitals.
- Saves round-trip through a real file, verified headlessly by `--selftest`.
- Community packages are pinned and restore successfully.
- MonoGame.Extended content-pipeline references are prepared.
- SharpGLTF-backed `asset-info` tooling is available.
- Main menu is the default startup screen.
- Start New Game enters the Northwatch scene.
- Imported models are normalized and framed predictably.
- UI is authored against a 1280×720 logical canvas.
- Borderless fullscreen is the default WindowsDX presentation.
- Cinzel headings and Noto Sans body/UI text are bundled with licenses and loaded at runtime.
- Font glyphs render at 2× resolution before the logical-canvas scale.
- Release build, doctor check, and domain tests pass.
- Iteration 7 room content validates through `RatnaBay.Tools validate`.
- Iteration 7 second room and second door are authored in JSON and covered by `--selftest`.
- Iteration 7 collision, manifest loading, door opening, and entry are asserted by `--selftest`.
- Iteration 7 opened-door persistence is asserted by save tests and `--selftest`.
- Iteration 7 packaged smoke check launched a new session and exited cleanly.
- Iterations 8–12 are implemented in the current slice: dialogue, quest, stealth, settlement,
  shop, pickups, dungeon, settings, audio, simulation, and packaged release gates.
- Release verification is green: 296 tests, RatnaBay.Tools sim, and published selftest.
- Save validation, isolated self-tests, recovery backup loading, safe main-menu Escape, the I/K
  character screen, and completed-quest trader dialogue are integrated in the release build.
- Packaged scene capture is recorded in Docs/PLAYTEST_NOTES.md.

### Parked

- Procedural dungeons until the authored vertical slice is stable.
- Public mod support, which is outside the current product scope.
- Time-bound quests; progression remains stage/event based.
- General-purpose quest scripting.

## Pull rule

Move an item to `Done` only after the code is integrated, the release build passes, the
relevant screen or scene is playable, and the iteration board records the result. Move
visual work to `Review / Playtest` when it needs a physical display or packaged-build check.
