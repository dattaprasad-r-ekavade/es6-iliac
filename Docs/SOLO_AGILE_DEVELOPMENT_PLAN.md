# Solo Agile Development Plan

**Project:** Ratna Bay  
**Method:** Solo Scrumban — one-week iterations with a small Scrum-style planning/review loop and Kanban-style work-in-progress limits  
**Developer:** One person  
**Primary goal:** Keep producing playable builds while growing the systems without destabilizing the project.

## Why this process fits a solo developer

Full Scrum ceremonies are overhead for one person. Pure ad-hoc development is risky because architecture, tools, content, and experiments can compete for attention without producing a visible result.

The process should keep the useful parts of Agile:

- A prioritized backlog.
- Small, testable increments.
- Frequent integration.
- A working build as the main progress signal.
- Regular review and reprioritization.
- Retrospectives that improve the way the project is built.

The process should avoid:

- Pretending one person is a full Scrum team.
- Large up-front task breakdowns that become obsolete.
- Measuring progress by hours or lines of code.
- Accepting “almost done” work that cannot be played or verified.
- Starting multiple risky systems at once.

## Steam presentation rule

Every playable milestone must be checked in the release presentation mode: borderless
fullscreen on the active Windows display, with UI authored against the 1280×720 logical
canvas. A feature is not considered visually done if it only works in a fixed development
window or clips at 1080p and above. F11/`--windowed` may be used for development diagnosis,
but it is not the release acceptance path.

## Operating model

### Iteration length

Use one-week iterations by default:

- **Planning:** 30–45 minutes at the start.
- **Implementation:** the main work period.
- **Build check:** run continuously and before the end-of-week review.
- **Review/demo:** play the build and record what changed.
- **Retrospective:** 15–30 minutes; record one improvement to keep and one problem to remove.

If a feature cannot produce a meaningful result in one week, split it into smaller playable increments. Do not create a four-week task and call it four iterations.

### Work-in-progress limit

The default WIP limit is **one active work item**.

One supporting investigation or documentation item may be open only when it directly unblocks the active item. New ideas go to the backlog, not into the current iteration.

This is the strongest protection against a solo project feeling busy but not progressing.

### Board states

```text
Backlog → Ready → In Progress → Review/Playtest → Done
              ↘ Parked
```

- **Backlog:** idea or possible work; not yet committed.
- **Ready:** small enough, understood enough, and valuable enough to start.
- **In Progress:** the one item currently being built.
- **Review/Playtest:** implementation exists; verify it in the game and run the checks.
- **Done:** Definition of Done is satisfied and the change is integrated.
- **Parked:** intentionally deferred with a reason.

## The build is the progress signal

Every iteration must end with a build that demonstrates a player-visible or tool-visible improvement.

An iteration is successful when it produces all four:

1. A new capability, visible behavior, content fixture, or tool result.
2. A build that starts from the repository.
3. Automated checks that protect the new behavior.
4. A short iteration note describing what changed and what is next.

The work does not need to be large. A camera moving through a 3D room, one imported model, one interactable door, or one quest stage transition is valid progress if it is integrated and repeatable.

## Definition of Ready

An item can enter `Ready` only when it has:

- A clear outcome written in plain language.
- A reason it matters to the current milestone.
- A small acceptance test or demonstration.
- Known dependencies.
- A likely size of one to three working days.
- A decision about whether it is runtime, tools, content, test, or documentation work.

If an item is larger than three days, split it by capability or player-visible outcome.

### Work-item format

```text
Title:
Type: runtime | tools | content | test | design | documentation | research
Milestone:
Player/tool outcome:
Why now:
Acceptance checks:
Dependencies:
Out of scope:
```

Example:

```text
Title: Move a camera through one loaded 3D room
Type: runtime
Milestone: Renderer proof
Player/tool outcome: The test window displays a room and WASD/mouse movement works.
Why now: Proves the MonoGame renderer and input loop before adding game systems.
Acceptance checks: Release build starts; camera collides with room bounds; Esc exits.
Dependencies: Renderer shell, one test mesh, input mapping.
Out of scope: Streaming, shadows, inventory, combat, procedural generation.
```

## Definition of Done

An item is `Done` only when the applicable checks below are complete:

- The code or content is integrated into the working solution.
- The affected domain behavior has an automated test or a deterministic fixture.
- `build.ps1 -Configuration Release` passes, unless the item is explicitly documentation-only.
- The game or tool can demonstrate the acceptance outcome.
- No generated file was hand-edited as a substitute for changing its source.
- Save/schema changes are versioned or explicitly marked as non-persistent prototype work.
- The iteration board is updated.
- The change is small enough to understand and revert.
- Known limitations are recorded instead of hidden.

For an experimental item, “Done” can mean “experiment answered the question and the result was either integrated or removed.” A discarded experiment is still useful if its conclusion is recorded.

## Branch and integration policy

Keep the branch that represents the playable project green.

- Prefer small, frequent commits.
- Use short-lived feature branches for risky renderer, schema, or tooling work.
- Integrate a branch when its acceptance checks pass; do not let branches become alternate projects.
- Do not mix a large refactor, content migration, and new feature in one change.
- If a change is not ready, keep it behind a small development flag or leave it out of the playable branch.
- Never allow generated runtime output to become the source of truth.

Suggested commit pattern:

```text
feat(renderer): draw one indexed mesh
test(domain): cover quest stage transition
tool(content): validate location references
docs(iteration): record renderer proof
refactor(world): isolate chunk manifest loading
```

The exact message style is less important than keeping each commit coherent and reversible.

## Backlog structure

Organize the backlog by product outcome rather than by technology alone.

### Epics

1. **Foundation** — solution, build, tests, diagnostics, input, settings.
2. **Renderer** — camera, meshes, materials, textures, lighting, sprites, debug views.
3. **World** — regions, locations, chunks, collision, portals, streaming.
4. **Content pipeline** — source schemas, importers, validators, compilers, manifests.
5. **Interaction** — doors, containers, pickups, dialogue, contextual actions.
6. **Character and combat** — attributes, equipment, attacks, damage, enemy behavior.
7. **Quest system** — stages, objectives, role bindings, conditions, events, journal.
8. **Persistence** — versioned save/load, migration, world state.
9. **Vertical slice** — one settlement, one authored dungeon, one quest, one enemy, one complete loop.
10. **Post-slice expansion** — procedural dungeon experiment, more regions, more factions, broader content.

### Prioritization rule

Prioritize work using this order:

1. It unblocks the next playable slice.
2. It reduces a high technical risk.
3. It protects existing behavior with tests or validation.
4. It improves the content creation loop.
5. It adds breadth or polish.

Breadth and polish should not outrank a missing core capability.

## Iteration planning

At the start of each iteration:

1. Read the current milestone exit criteria.
2. Choose one primary outcome.
3. Choose no more than two supporting items.
4. Write the acceptance checks before implementation.
5. Identify the smallest playable proof.
6. Move only those items to `Ready`.
7. Record what is deliberately not being attempted.

The primary outcome should be expressible as a sentence beginning with “At the end of this iteration, I can…”

Examples:

- “At the end of this iteration, I can walk through one imported 3D room.”
- “At the end of this iteration, I can interact with a door and see its state persist during the session.”
- “At the end of this iteration, I can accept a quest and advance it from stage 10 to stage 20 through a world event.”
- “At the end of this iteration, I can save at stage 20 and reload with the same objective visible.”

## Review and retrospective

The end-of-iteration review should be practical:

- Start from a clean build if possible.
- Play the new path rather than inspecting only code.
- Check the acceptance conditions.
- Note the build command and test result.
- Capture a screenshot or short recording for major milestones.
- Record any failure as a backlog item with evidence.

The retrospective should answer:

1. What became more playable or more reproducible?
2. What broke, confused, or took longer than expected?
3. Which rule, tool, test, or boundary should change next iteration?
4. What should be removed from the backlog because it no longer supports the product?

Do not use the retrospective to add scope. Use it to improve flow and reduce recurring failure.

## Build and test gates

### Every change

- Compile the affected project.
- Run the smallest relevant tests.
- Keep the domain layer independent of the graphics device where possible.

### Every iteration

Run:

```powershell
.\build.ps1 -Configuration Release
```

This currently restores tools, restores/builds the solution, runs the project doctor, and runs domain tests.

Then manually smoke-test the game window and the iteration’s acceptance path.

### Every milestone

- Build from a clean checkout or clean output folders.
- Validate all content fixtures.
- Test save/reload at every newly introduced persistent state.
- Test a fresh game and an intermediate save.
- Record performance observations for loading, memory, and frame rate.
- Tag the milestone build or otherwise record the exact commit.

## Protecting the project from large breakages

### Stable boundaries

Keep these boundaries explicit:

- `RatnaBay.Domain` contains rules and data, not MonoGame rendering types.
- `RatnaBay.Game` consumes domain and compiled content; it does not become the authoring database.
- `RatnaBay.Tools` validates and compiles source data; it does not silently mutate it.
- `RatnaBay.Domain.Tests` protects rules, state transitions, and serialization.
- Source content is editable; generated content is disposable.

### Contract-first changes

When changing a shared type or file format:

1. Write the intended contract change.
2. Add or update a fixture.
3. Add a test for the old and new behavior when compatibility matters.
4. Update the compiler and runtime together.
5. Run the full release build.
6. Record migration or incompatibility notes.

Do not change a shared schema casually inside a feature branch.

### Golden fixtures

Create tiny fixtures that are always cheap to load and verify:

- One room with collision.
- One region with one location.
- One NPC and one item.
- One linear quest with three stages.
- One save file at each quest stage.
- One dungeon module after procedural work begins.

These fixtures become the project’s safety net when the renderer, tools, or data model changes.

### Feature flags

Use development flags only for incomplete but useful work:

- Show collision.
- Show chunk bounds.
- Show role bindings.
- Trace quest events.
- Start directly at a quest stage.
- Use a fixed random seed.

Flags should help development and testing; they should not become a permanent replacement for finishing the feature.

## Build ladder

The game should grow through a sequence of increasingly meaningful builds:

| Build level | Demonstration |
|---|---|
| 0 — Shell | Window opens, input works, clean build passes. |
| 1 — Renderer proof | Camera moves through one 3D room. |
| 2 — Interaction proof | Player opens or activates one world object. |
| 3 — Content proof | A source asset/data file is imported, validated, and loaded. |
| 4 — Gameplay proof | NPC, enemy, item, and one interaction loop work. |
| 5 — Quest proof | A quest advances through authored stages and objectives. |
| 6 — Persistence proof | Save/reload preserves player and quest state. |
| 7 — Vertical slice | Settlement → quest → travel → authored dungeon → combat → reward → save/reload. |
| 8 — Post-slice experiment | Procedural dungeon generator produces validated deterministic layouts. |

Never move to the next level only because the previous level exists. Move when its checks are reliable enough to support the next one.

## Initial iteration roadmap

Iteration 0 is already complete: the new MonoGame solution, tool restore, release build, project doctor, and initial test are working.

### Iteration 1 — Renderer proof

**Outcome:** The game displays one controlled 3D room and the player can move a camera through it.

**In scope:** camera, input, one mesh/material path, basic collision bounds, debug view.  
**Out of scope:** streaming, shadows, dungeon generation, inventory, quests.

**Exit checks:** release build passes; room loads from a controlled asset; camera movement is repeatable; no editor is required at runtime.

### Iteration 2 — Content proof

**Outcome:** One source asset and one source data record are validated, compiled, and loaded by the game.

**In scope:** content manifest, source/generated/runtime distinction, one Blender-exported asset, content validation command.  
**Out of scope:** asset browser, general editor, public mod format.

**Exit checks:** invalid references produce a useful error; valid content produces a runtime package; the game loads the package.

### Iteration 3 — Interaction proof

**Outcome:** The player can approach, target, and activate one authored object.

**In scope:** interaction ray/query, door or container state, prompt UI, one domain event.  
**Out of scope:** generalized interaction editor, complex physics, NPC schedules.

**Exit checks:** interaction works from a clean build; the object’s state is represented in domain data; a test covers the state transition.

### Iteration 4 — Quest proof

**Outcome:** A linear, no-deadline quest advances through three authored stages.

**In scope:** quest definition, stage, objective, role binding, event, condition, journal text.  
**Out of scope:** branches, radiant quests, timers, scenes, arbitrary scripts.

**Exit checks:** the quest can be started, advanced by an interaction, displayed in UI, and tested from a fresh state.

### Iteration 5 — Persistence proof

**Outcome:** The player can save and reload while preserving the quest stage, objective, interaction state, and inventory.

**In scope:** versioned save schema, one migration placeholder, golden saves.  
**Out of scope:** cloud saves, multiple profiles, mod compatibility.

**Exit checks:** save/reload tests pass at every quest stage; corrupt or incompatible save behavior is explicit.

### Iteration 6 — Authored dungeon proof

**Outcome:** The player can travel from a small settlement into one authored dungeon, complete the quest, and return.

**In scope:** one dungeon layout, entrance/exit, collision, one enemy, one reward.  
**Out of scope:** procedural generation, dungeon editor, large dungeon library.

**Exit checks:** complete vertical loop works from a clean runtime package; the dungeon is deterministic and debuggable.

### Iteration 7 — Vertical slice stabilization

**Outcome:** The complete loop is stable enough to serve as the foundation for all future work.

**In scope:** bug fixes, usability, test coverage, build documentation, performance baseline, removal of temporary hacks.  
**Out of scope:** new breadth features unless required to fix the loop.

**Exit checks:** a new build can be produced and played without manual project surgery; known issues are documented and prioritized.

### Iteration 8 — Procedural dungeon experiment

**Outcome:** A deterministic generator produces a small dungeon from a seed and passes structural validation.

**In scope:** module grammar, seed, connectivity checks, spawn/exit rules, preview report.  
**Out of scope:** replacing the authored dungeon, infinite variety, runtime generation without compiled validation.

**Exit checks:** the generator produces reproducible layouts; invalid layouts fail validation; the authored vertical slice remains unchanged.

## Progress tracking

Use the working board in [ITERATION_BOARD.md](ITERATION_BOARD.md). At the end of each iteration, record:

- Iteration number and dates.
- Primary outcome.
- Completed work.
- Build/test result.
- Demonstration or capture path.
- Problems found.
- One process improvement.
- The next iteration’s primary outcome.

Progress is measured by completed, playable capabilities and protected behavior—not by the number of tasks started.

## Director rules for changing the plan

The plan can change, but changes must be explicit:

- New feature: add it to the backlog and state what it displaces.
- New technical approach: run a bounded experiment with a success/failure criterion.
- Scope increase: update the exclusions and milestone impact.
- Scope reduction: record what is removed and why.
- Broken architecture: prefer a small migration with a fixture over a hidden rewrite.

The director’s job in a solo project is to protect the future developer—you—from unbounded commitments and invisible progress.
