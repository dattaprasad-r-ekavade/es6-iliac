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

- Iteration 6 — billboard sprite renderer, enemy chase, melee target query, hit feedback.
- Iteration 7 — static BVH and swept-AABB collision, JSON world manifest, hot reload.
- Iteration 8 — dialogue topics in JSON, interaction prompt, topic menu.
- Iteration 9 — quest definitions in JSON, journal screen, objective banner.
- Iteration 10 — view-cone watchers, crouch, guard patrols, awareness indicator.
- Iteration 11 — authored settlement, five NPCs, a real shop.
- Iteration 12 — authored dungeon, settings screen, packaged build, external playtests.

### Ready

- Iteration 6: billboard sprite renderer.

### In Progress

- None. Iteration 5 is ready for playtest.

### Review / Playtest

- Save, quit, relaunch, Continue — verify the character comes back whole.
- Sprint until stamina empties; confirm the bar is the domain's number.
- Borderless fullscreen on physical 720p, 1080p and ultrawide displays.

### Done

- Unity gameplay rules ported to `RatnaBay.Domain`: 2,835 lines, 247 tests, ~140 ms.
- The domain drives the running game; the HUD shows live vitals.
- Saves round-trip through a real file, verified headlessly by `--selftest`.
- Community packages are pinned and restore successfully.
- MonoGame.Extended content-pipeline references are prepared.
- SharpGLTF-backed `asset-info` tooling is available.
- Main menu is the default startup screen.
- Start New Game enters the Northwatch scene.
- Renderer Lab and UI Stress Test remain available from the menu.
- Imported models are normalized and framed predictably.
- UI is authored against a 1280×720 logical canvas.
- Borderless fullscreen is the default WindowsDX presentation.
- Cinzel headings and Noto Sans body/UI text are bundled with licenses and loaded at runtime.
- Font glyphs render at 2× resolution before the logical-canvas scale.
- Release build, doctor check, and domain tests pass.

### Parked

- Procedural dungeons until the authored vertical slice is stable.
- Public mod support, which is outside the current product scope.
- Time-bound quests; progression remains stage/event based.
- General-purpose quest scripting.

## Pull rule

Move an item to `Done` only after the code is integrated, the release build passes, the
relevant screen or scene is playable, and the iteration board records the result. Move
visual work to `Review / Playtest` when it needs a physical display or packaged-build check.
