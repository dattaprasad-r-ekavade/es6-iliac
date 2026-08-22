# Ratna Bay Kanban

This is the active Kanban view for the solo development project. Keep work-in-progress
small enough that every iteration produces a playable build.

## Work-in-progress limit

- One primary implementation item.
- One supporting documentation, research, or verification item only when it directly
  unblocks the primary item.

## Current board

### Backlog

- Settings screen: borderless/windowed mode, UI scale, display selection.
- Safe-area and ultrawide layout verification.
- Controller navigation for menus and Settings.
- Collision boundary for the Northwatch scene.
- First interaction target: lantern marker or campfire.
- Data-driven world manifest and content validation command.
- Runtime screenshot capture at release resolutions.

### Ready

- Add a Settings screen that owns presentation and input preferences.
- Define the first scene collision fixture.

### In Progress

- None. The current shell work is ready for playtest.

### Review / Playtest

- Borderless fullscreen on a physical 720p display.
- Borderless fullscreen on a physical 1080p display.
- Wide-display behavior and letterboxing.
- F11 windowed toggle and `--windowed` launch path.

### Done

- Main menu is the default startup screen.
- Start New Game enters the Northwatch scene.
- Renderer Lab and UI Stress Test remain available from the menu.
- Imported models are normalized and framed predictably.
- UI is authored against a 1280×720 logical canvas.
- Borderless fullscreen is the default WindowsDX presentation.
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
