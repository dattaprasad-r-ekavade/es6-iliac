# Ratna Bay Iteration Board

This is the lightweight working board for the solo Agile process. Keep only the current iteration in the active columns. Move larger ideas to the backlog instead of expanding the current iteration.

## Current iteration

**Iteration:** 3 — Steam presentation baseline  
**Primary outcome:** Run the menu and first scene in borderless fullscreen with resolution-safe UI.  
**Status:** Review / playtest  
**Target build command:** `.\build.ps1 -Configuration Release`

### Ready

- [ ] Verify on a physical 1280×720 display.
- [ ] Verify on a physical 1920×1080 display.
- [ ] Verify on an ultrawide display and confirm letterboxing.
- [ ] Add a Settings screen for display and UI scale preferences.

### In Progress

- [ ] None.

### Review / Playtest

- [ ] Borderless fullscreen startup.
- [ ] 1280×720 logical canvas scaling.
- [ ] F11 and `--windowed` development path.
- [ ] Menu readability at the captured fullscreen size.

### Done

- [x] Establish the MonoGame solution and projects.
- [x] Install and restore MonoGame content tools.
- [x] Add the release build, doctor check, and domain test pipeline.
- [x] Move the original Unity project into the external archive without deleting it.
- [x] Document Daggerfall scope, exclusions, and quest mental model.
- [x] Add the default menu screen and keyboard navigation.
- [x] Add the first Northwatch development scene.
- [x] Normalize imported model bounds for predictable scene placement.
- [x] Add explicit `--mode` launch paths for repeatable development tests.
- [x] Set borderless fullscreen as the default WindowsDX presentation.
- [x] Add uniform UI scaling from a 1280×720 logical canvas.
- [x] Document the Steam presentation baseline and active Kanban.

### Parked

- [ ] Procedural dungeon generation — start only after the authored vertical slice.
- [ ] Public mod support — excluded from the product scope.
- [ ] Time-bound quests — excluded; use stage/event progression.
- [ ] Full NPC schedules — revisit only if the core loop requires them.
- [ ] General-purpose quest scripting — use the constrained data model first.

## Backlog

### Foundation

- [ ] Add a test fixture convention for runtime content.
- [ ] Add a documented clean-build smoke test.
- [ ] Add a developer debug overlay toggle.

### Renderer

- [ ] Camera movement.
- [ ] Mesh/material loading.
- [ ] Room collision.
- [ ] Directional light or simple baked light path.
- [ ] Debug draw for bounds and collision.

### Content pipeline

- [ ] Define source/generated/runtime content folders.
- [ ] Add content manifest schema.
- [ ] Add `content validate` command.
- [ ] Add `content build` command.
- [ ] Import one Blender-exported test asset.

### Gameplay

- [ ] Interaction query and prompt.
- [ ] Door or container state.
- [ ] Quest stages and objectives.
- [ ] Quest role binding.
- [ ] Journal UI.
- [ ] Versioned save/load.
- [ ] One enemy and one combat loop.

### Vertical slice

- [ ] One settlement.
- [ ] One authored dungeon.
- [ ] One NPC.
- [ ] One quest.
- [ ] One reward.
- [ ] Full save/reload loop.

## Iteration closeout template

Copy this section into the end of the board or into a dated iteration note:

```text
## Iteration N — YYYY-MM-DD to YYYY-MM-DD

Primary outcome:

Completed:
-

Build/test result:
- Command:
- Result:

Playable/tool demonstration:
-

Problems found:
-

Retrospective — keep:
-

Retrospective — change:
-

Next iteration outcome:
-
```
