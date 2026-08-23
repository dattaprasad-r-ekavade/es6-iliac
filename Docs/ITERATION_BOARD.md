# Ratna Bay Iteration Board

This is the lightweight working board for the solo Agile process. Keep only the current iteration in the active columns. Move larger ideas to the backlog instead of expanding the current iteration.

## Current iteration

**Iteration:** 12 — Slice lock

**Primary outcome:** The player can start, talk, quest, fight, explore the dungeon, recover,
save, quit, and reload from a self-contained build.
**Status:** Implementation complete; external playtest sign-off pending
**Target build command:** `.\publish.ps1`
**Plan of record:** [`PRODUCTION_PLAN.md`](PRODUCTION_PLAN.md)

### In Progress

- [ ] Run three independent external playthroughs and record completion/blockers in
      [PLAYTEST_NOTES.md](PLAYTEST_NOTES.md).

### Review / Playtest

- [x] Published scene captured from build\RatnaBay.exe; see
      [artifacts/iteration12_packaged_scene.png](../artifacts/iteration12_packaged_scene.png).
- [ ] Physical keyboard pass: talk, accept the quest, enter the dungeon, take loot, buy stock,
      crouch/pickpocket, open the I/K character screen, die/recover, save/reload.
- [ ] On the main menu, press Escape and confirm the game stays open; use Exit to close it.

- [ ] Edit `src/RatnaBay.Game/Content/World/northwatch.json` while the scene is running;
      confirm a valid change reloads and an invalid change leaves the old room playable.
- [ ] Walk diagonally into the northwatch walls; confirm movement slides without tunnelling.
- [ ] Face the door, press E, and confirm the Security lock opens and the doorway becomes passable.
- [ ] Relaunch from the published build and confirm `Content/World/northwatch.json` is bundled.
- [ ] Walk toward the camp and let a bandit notice you.
- [ ] Kill one with the sword; confirm Blade rises only on landed hits.
- [ ] Hold right click and confirm incoming damage drops.
- [ ] Equip nothing and let three of them kill you; confirm recovery is not a dead end.
- [ ] Cast Rime (5 then Q) and confirm the target visibly slows and tints cold.
- [ ] Cast Arc (6 then Q) into two bandits and confirm the jump.
- [ ] Save mid-fight, reload, confirm the dead stay dead.
- [ ] Confirm the held weapon changes when you equip the greatsword or unequip entirely.
- [ ] Swing while standing still and while walking; the sway should only move when you do.
- [ ] Hold guard and confirm the blade lifts across the body.
- [ ] Watch for "striking" above an enemy and try to guard in time.

### Done

- [x] **Iteration 6:** Billboard sprite renderer with code-drawn, palette-locked characters.
- [x] **Iteration 6:** Cone targeting in the domain, replacing a physics sphere cast.
- [x] **Iteration 6:** Enemy chase, leashing, separation and attack cooldowns.
- [x] **Iteration 6:** Attack, guard, hit flash, damage vignette, enemy health bar.
- [x] **Iteration 6:** Spells bound to keys, with Arc's chain target resolved by targeting.
- [x] **Iteration 6:** A whole fight asserted headlessly in `--selftest`.
- [x] **Iteration 6:** First-person weapon sprite, drawn in code per weapon and tier.
- [x] **Iteration 6:** Swing arc, walking sway and guard pose on the held weapon.
- [x] **Iteration 6:** Enemy walk bob, attack lunge and hit recoil.
- [x] **Iteration 7:** Static BVH with swept horizontal collision, sliding and vertical overlap.
- [x] **Iteration 7:** JSON world manifest for geometry, props, lights, spawn and door data.
- [x] **Iteration 7:** Runtime manifest hot reload with invalid-edit fallback.
- [x] **Iteration 7:** `content validate` checks world manifests in `RatnaBay.Tools`.
- [x] **Iteration 7:** First authored northwatch room with a Security-wired door.
- [x] **Iteration 7:** Second room fixture added entirely in `northwatch.json`.
- [x] **Iteration 7:** Opened door state persists through save/load and rebuilds collision.
- [x] **Iteration 7:** Published executable launched, entered a new session, rendered the authored room, and exited cleanly.
- [x] **Iteration 8:** Dialogue manifest validates with two room-two traders and fourteen topics.
- [x] **Iteration 8:** Runtime NPC billboards, facing interaction, topic menu, responses,
      conditioned answers, quest-linked dialogue, and hot reload are wired.
- [x] **Iteration 9:** Quest definitions, journal, live objective bearing, dialogue acceptance,
      rewards, and quest save/reload are wired.
- [x] **Iteration 10:** Watcher view cones, sight blockers, crouch visibility, patrols, awareness
      decay, and pickpocket interaction are wired.
- [x] **Iteration 11:** Northwatch now has three enterable thresholds, two traders, a gold shop,
      persistent world pickups, and a generated ambient audio bed.
- [x] **Iteration 12:** Dungeon geometry, loot, death/recovery, settings, `RatnaBay.Tools sim`,
      self-contained publishing, and release self-test are complete.
- [x] **Iteration 12:** Save self-tests use an isolated slot; saves validate before replacement,
      retain a recovery backup, and failed Continue attempts remain safely on the menu.
- [x] **Iteration 12:** I/K character screen exposes inventory, equipment, vitals and all skills;
      completed trader quests receive completion-aware dialogue after reload.
- [x] **Iteration 5:** `PlayerCharacter` ticked from the game loop.
- [x] **Iteration 5:** Live health/prana/stamina HUD reading domain values.
- [x] **Iteration 5:** Domain events surfaced as on-screen toasts.
- [x] **Iteration 5:** F5/F9 save and load through a real file on disk.
- [x] **Iteration 5:** Continue on the main menu, shown only when a save exists.
- [x] **Iteration 5:** `--selftest` headless save round-trip harness.
- [x] **Iteration 5:** Objective persisted; its bearing regenerated rather than stored.
- [x] Port the Unity gameplay rules into `RatnaBay.Domain` (296 tests).
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
- [x] Pin the initial community package baseline.
- [x] Prepare the MonoGame.Extended content-pipeline reference path.
- [x] Add SharpGLTF-backed `asset-info` tooling.
- [x] Add the community tools baseline document.
- [x] Bundle Cinzel and Noto Sans under SIL OFL for reproducible runtime font loading.
- [x] Render the bundled fonts at 2× glyph resolution through FontStashSharp.

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
- [x] Versioned save/load.
- [x] One enemy and one combat loop.

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
