# Full storyline viability and production plan

**Audit date:** 2026-07-28

**Story authority:** `storyline.md`

**Target:** the complete authored opening chapter: shipwreck, rescue, character creation,
King's audience, all four tutorial routes, the prince reveal, prison escape, cave-exit
title crawl, confrontation and regime change, player title, and the Daggerfall/Council of
Kings handoff.

**Expected first-route length:** about 45–70 minutes before polish/playtest adjustment,
with substantial replay-only content in the other three routes.

**Engine:** Unity 6000.5.3f1, URP 17.5

## Scope lock

`storyline.md` is authoritative. This plan does not replace, condense, or redirect it.
The following are required content, not stretch goals:

1. Merchant ship voyage, Tower sightline, Thalmor warships, arcane pulse, wreck, and water
   sequence.
2. Rescue ship, Wayrest arrival, survivor processing, and real character creation.
3. King's audience and assignment by the player's declared inclination.
4. Four materially distinct routes:
   - Warrior: combat training, hunt/patrol, secret prisoner transport.
   - Mage: spell training, soul-gem delivery, restricted prison accident.
   - Commerce/Thief: sailing, lockpicking, pickpocketing, sneaking, and secured tower.
   - None/Refuse: general prison, soul-harvesting reveal, and the deliberately fastest
     route.
5. Prince discovery, explanation, route convergence, evidence, and joint escape.
6. A shared sea-cave exit used as the walking-into-the-world title-crawl moment.
7. Confrontation with the King, his removal, the successor, the prisoner soul-trapping
   ban, and persistent world-state changes.
8. The player's official title and the mission to Daggerfall to seek recognition from the
   Council of Kings.
9. A final Tower reminder that hands the player into the wider main story.

Place, faction, and character renaming is explicitly deferred. Current story names remain
the display names. Technical IDs must be stable and separate from display strings so a
later rename does not require rewriting saves, quest logic, or scene references.

## Verdict

The exact story is viable in this project. The existing world and gameplay prototype are
a useful foundation, but the opening is now a **multi-scene RPG chapter**, not the former
15–20 minute vertical-slice proposal. It needs new narrative, interaction, traversal,
save, AI, interior, cinematic, animation, audio, and world-mutation work.

Estimated readiness for the complete authored opening: **20–25%**.

| Area | Readiness | Evidence |
|---|---:|---|
| Build and tooling | 80% | Windows build, asmdefs, compile checker, 15 EditMode tests |
| Exterior-world foundation | 60% | elevated land, walled cities, roads, docks, collision, map/travel |
| General gameplay prototype | 55% | movement, basic combat, inventory, NPCs, enemies, save/load |
| Story architecture | 10% | complete outline exists; no story graph, flags, choices, or staged scenes |
| Route-specific mechanics | 10–15% | basic combat/magic exist; sailing, stealth, locks, and pickpocketing do not |
| Interiors, cinematics, and actors | 5–10% | useful source assets exist; no authored interiors, performances, or companion flow |
| Audio, animation, and final feel | 10–15% | SFX library and fonts exist; actors are static and there is no narrative mix |
| Story QA and shipping | 10–15% | clean build and geometry tests exist; no end-to-end route or save-state coverage |

Planning ranges, assuming one focused developer using existing low-poly assets:

- **Engineering-complete greybox of every story beat:** 50–75 focused days.
- **Presentable low-poly release candidate with all four routes:** 85–130 focused days.
- **Fully voiced, bespoke cinematic quality:** a separate production tier measured in
  additional person-months.

These are planning ranges rather than promises. The milestone gates below keep the full
story intact while exposing technical or content problems early.

## What is actually in the project

Verified in the repository and Unity Editor:

- 29 runtime scripts, about 6.8k lines;
- one generated `Main` scene;
- a successful local Windows player (about 140 MB before this audit);
- 15/15 EditMode tests passing, focused on geography, coast, terrain, routes, and layout invariants;
- 5 NPCs, 5 hostile spawns, 3 quests, and 8 discovery/travel markers;
- one 6.8 km generated bay with ten continuous elevated landmasses, three walled cities,
  five regional roads/causeways, and three islands;
- CC0 environment packs, OFL fonts, UI/combat SFX, and a Blender-generated seven-piece
  desert architecture kit;
- no gameplay prefabs, ScriptableObject gameplay data, `.inputactions` asset, Animator
  controllers/clips, additive areas, interiors, PlayMode tests, profiler captures, ambient
  beds, music, or implemented authored story content.

`storyline.md` now supplies the narrative outline, but none of its scenes or branches are
implemented yet. The current executable remains a **prototype build**, not a playable
version of that opening.

## Completed hardening

### Earlier pass

- Added `WorldLayout` as the shared source for geography and travel sites.
- Added explicit gameplay layers and layer-masked interaction/combat.
- Fixed all NPCs and enemies spawning in one pile.
- Moved two POIs out of open water and corrected road placement.
- Replaced 639 per-prop foliage updates with one culling system.
- Added asmdefs, `Tools/compile-check.py`, headless build tooling, and 9 EditMode tests.
- Reduced the Windows build from roughly 206 MB to 140 MB with a texture budget.
- Added the deterministic Blender Yoku kit generator.

### Audit repair pass — 2026-07-26

- **Save integrity:** removed the unconditional startup autosave that overwrote the only
  save slot; added a title-screen Continue path.
- **World-state restore:** loading now removes enemies killed in the save and respawns
  enemies that were killed after that save.
- **Economy:** merchants now charge gold before granting a potion.
- **Weather:** regional weather updates against the destination/current region before a
  reroll.
- **Map:** the player marker is no longer reused and destroyed as the first location marker.
- **Spawn/travel:** player spawn, fast travel, and rescue place the CharacterController
  feet on the ground with a small clearance instead of dropping the player by about 1 m.
- **First-person camera:** the Player layer is excluded, preventing the camera from
  rendering the player body from inside the head.
- **World geometry:** fixed the Balfiera tower inheriting a huge island-top scale.
- **Prototype artifacts:** removed generated city, POI, enemy, and NPC world-space
  nameplates that appeared mirrored from behind.
- **Cutscene:** authored actor scale is preserved and actors are always cleaned up after
  completion or skip.
- **Foliage:** culling follows the actively rendering camera during the intro and gameplay.
- **Dialogue/input:** dialogue now blocks gameplay, pauses safely, closes explicitly, and
  uses realtime timeout; menus consistently own pause and cursor state.
- **Presentation:** rebuilt the HUD and title menu with translucent charcoal, silver,
  muted bronze, Cinzel, and EB Garamond. This is Skyrim-inspired hierarchy using original
  and licensed assets, not copied Bethesda UI art.
- **Rendering:** switched to Linear color, repaired the broken VolumeProfile sub-assets,
  enabled camera post-processing, set 4× MSAA, four shadow cascades, and a 220 m shadow
  distance, and removed double-applied terrain texture tiling.
- **Generator safety:** presentation rebuilds no longer delete and recreate copied
  Resources assets, so their GUIDs stay stable.
- **Compiler health:** removed current Unity API obsolescence warnings in touched paths.

### World-depth repair pass — 2026-07-27

- **Continuous geography:** replaced rectangular terrain patches with closed elliptical
  meshes and subdivided coast strips. Shorelines now follow the same geometry in the
  world, travel checks, and map art instead of exposing triangular gaps.
- **Elevation:** added deterministic biome-aware ridged terrain with dry interior
  guarantees, higher Wrothgar/Dragontail profiles, and flat city build zones blended
  into their surroundings.
- **City collision:** replaced hundreds of mis-selected microscopic roof modules with
  134 bounded procedural buildings. Every non-enterable building has exactly one
  enabled `BoxCollider`; decorative roofs and foundations have none.
- **Walls and gates:** built 160 overlapping wall segments across the three cities,
  all with simple box collision, measured cardinal gate openings, gate towers/lintels,
  and terrain-matched approach ramps.
- **Waterfronts:** moved piers and quays from elevated grass to the waterline and added
  collider-backed causeways from each city pad. Decorative ships are normalized,
  offshore, and collider-free.
- **Regional structure:** added Glenumbra–Wrothgar, Sentinel–Alik'r, and
  Alik'r–Dragontail routes, widened roads, added guarded causeway rails, and concentrated
  modest dressing along playable routes.
- **Asset curation:** building, tower, dock, tree, prop, camp, and ruin pools are now
  deterministic and role-filtered. The `roof-corner-inner`/`Contains("inn")` bug is
  removed.
- **Save compatibility:** old saves keep their X/Z location but reconcile Y to the new
  terrain when submerged or materially displaced.
- **Rendering:** generated/runtime materials opt into GPU instancing where supported;
  terrain is static, while mesh collision is reserved for the ten terrain surfaces.
- **Title audio:** the overlay-only title now keeps one audio listener active without
  enabling a rendering camera, eliminating the startup listener warning.

## Current validation

- `python Tools/compile-check.py`: Editor, Runtime, and Tests assemblies pass.
- Unity console after regeneration and title startup: **0 project errors, 0 project
  warnings**.
- Unity EditMode: **15 passed, 0 failed**.
- Live title, Continue, first-person Game view, city entrance, and coastline inspected
  after scene regeneration.
- Scene audit: **2,042 GameObjects, 1,797 renderers, 905 colliders**; 894 are box
  colliders and the only 10 mesh colliders are the terrain surfaces.
- Geometry audit: **0 invalid terrain indices, 0 fake roof-corner buildings, 0 building
  collider violations, 0 wall collider violations, and 0 approach collider violations**.
- Physics probes: representative wall and building rays hit their box colliders; the
  south gate walking ray remains clear; every city has five collider-backed approaches.
- Fresh Windows x64 release build: **138.51 MB, 0 build errors, 0 build warnings**.
- Packaged New Journey and Continue flows both reached gameplay; the fresh 138.51 MB
  build's Continue path skipped the intro and loaded the existing v2 slot onto the new
  terrain.
- Packaged `Player.log` reached `[GameSystems] P0/P1 systems online` with no managed
  exception or project error.
- Volume inspection confirms Color Adjustments, Vignette, and Bloom are real referenced
  components rather than null profile entries.

The current automated tests do **not** prove save rollback, menu flow, combat, fast travel,
or packaged-player behavior. Those remain required gates below.

## Remaining issues and risks

### P0 — required before building story scenes

- Convert `storyline.md` into a versioned chapter graph, scene list, dialogue screenplay,
  cast sheet, evidence list, and outcome matrix. This is implementation detail, not an
  opportunity to remove or replace authored beats.
- Lock the continuity contract shared by all routes:
  - the prince's location and condition at each stage;
  - which route-specific evidence enters the shared evidence inventory;
  - how each route reaches the same prison checkpoint without contradicting another;
  - how the prince and player reach the sea cave;
  - the cave exit as the title-crawl checkpoint;
  - how they safely return to confront the King after the title moment.
- Choose the exact authored result for variables left open in `storyline.md`: whether the
  King can be killed or imprisoned, whether the prince or another heir rules, and which
  official title is granted. The underlying systems should support alternatives even if
  the first playable version ships with one canon result.
- Define allowed character-creation races, appearance controls, backgrounds, pronouns,
  starting values, and how background/skill declaration maps to the four assignments.
- Specify the speed route as intentional content: target time, mandatory information,
  permitted skips, and state flags. It must be fastest without corrupting later quests.
- Separate stable internal IDs from localized display strings. All name changes remain
  deferred until the user supplies them.
- Add PlayMode smoke tests for New Game, Continue, save → kill → load rollback, merchant
  purchase, fast travel, dialogue pause, death/rescue, and return to menu.
- Add a packaged-player launch smoke check to CI or a repeatable local script.
- Create an asset ledger with source URL, version, license, proof/date, and whether source
  redistribution is allowed.

### P1 — architecture and gameplay debt

- `Main` is a destructive generated artifact. Split it into a persistent `Bootstrap` and
  additive authored scenes; make editor generation idempotent and non-destructive.
- Replace code-built actors, systems, and UI with prefabs and ScriptableObject data.
- Add one Input System actions asset and controller support.
- Replace scattered pause checks with one `GameState` service.
- Replace random line pools and auto-active quests with conditional dialogue, explicit
  quest stages, story flags, route gates, evidence requirements, and consequences.
- Add generic interaction, doors, locks, readable evidence, item use, stealth/detection,
  crouching, pickpocketing, sailing, follower/escort, scene transition, and world-mutation
  systems. Combat and magic need tutorial-state hooks rather than special-case scene code.
- Add character profiles and route assignment. Character appearance must persist across
  every scene and save.
- Upgrade the save format to include current scene/spawn, character profile, story chapter
  and stage, flags, route, evidence, dialogue choices, companion state, King outcome,
  ruler state, granted title, opened locks, looted objects, and skipped cinematics.
- Version the new save schema and migrate or safely reject current v2 prototype saves.
- Continue is enabled by file existence; an old-version/corrupt save is rejected safely
  only after entering gameplay. Add lightweight header validation on the menu.
- Add PlayMode coverage for generated city collision, gate clearance, harbor causeways,
  and save-height migration; current geometry checks are editor inspection/probes.

### P1 — world and performance debt

- The islands and cross-bay travel are still fast-travel only; add a deliberate ferry or
  controlled transition rather than encouraging a long swim.
- The world is now elevated and connected locally, but the 6.8 km footprint remains
  sparsely dressed beyond cities, POIs, and regional routes.
- The current cities are readable, collidable exterior shells, not authored hero spaces:
  buildings have no doors/interiors, walls have no battlements or climb routes, and there
  is no navmesh.
- Wayrest specifically needs a survivor-processing dock, functioning palace/throne room,
  guard yard, Mages Guild, working harbor, secured tower, layered prison, soul-harvesting
  chamber, escape route, and sea cave. Daggerfall needs at least a convincing arrival and
  Council handoff space for this chapter.
- The story needs reliable crowd and companion navigation. Bake or build navigation per
  authored scene, add off-mesh links only where tested, and keep large decorative areas
  outside active simulation.
- About 2,042 GameObjects, 1,797 renderers, and 905 colliders are present. Mesh collision
  has been reduced to 10 terrain surfaces and instancing is enabled where supported, but
  there is still no measured LOD, occlusion, batching, frame-time, or memory budget.
- Enemies use simple direct pursuit rather than navigation.
- Weather is functional but not production quality: rain does not wet surfaces and there
  is no thunder/ambient mix.
- The current player/NPC models have no authored animation, hit reaction, block, stagger,
  or death presentation.

### Shipping and legal debt

- Remove MCP/editor runtime payloads and `BurstDebugInformation_DoNotShip` from release
  packaging.
- Add application icon, credits/about, accessibility settings, quality presets, and input
  rebinding.
- Add a project code license and complete third-party notices.
- Before any public distribution, perform the deferred naming/IP review. This does not
  block internal implementation of the current story.
- Test a clean zip on a second machine.

## Story flow contract

```mermaid
flowchart TD
    A["Merchant ship and Tower pulse"] --> B["Rescue ship and Wayrest docks"]
    B --> C["Character creation"]
    C --> D["King's audience and assignment"]
    D --> W["Warrior tutorial"]
    D --> M["Mage tutorial"]
    D --> T["Commerce / Thief tutorial"]
    D --> N["None / Refuse prison route"]
    W --> P["Prince found and evidence secured"]
    M --> P
    T --> P
    N --> P
    P --> E["Prison escape with prince"]
    E --> X["Sea-cave exit and title crawl"]
    X --> K["Confront the King"]
    K --> R["New ruler, ban, title, persistent consequences"]
    R --> G["Travel to Daggerfall / Council of Kings"]
    G --> H["Tower main-story reminder"]
```

The four branches may vary in length, mechanics, information order, and evidence, but
they must enter the convergence checkpoint with a valid prince, evidence set, companion
state, and story stage. No route may rely on another route having happened.

## Scene and loading architecture

Use one persistent scene and authored additive scenes. Do not keep expanding the generated
`Main` scene.

| Scene | Purpose | Required exit state |
|---|---|---|
| `Bootstrap` | persistent services, input, UI, audio, saves, loading | services initialized once |
| `Prologue_Ship` | voyage, Tower/warships, pulse, wreck, water | player marked rescued |
| `Wayrest_Docks` | rescue arrival, processing, character creator | valid character profile |
| `Wayrest_Palace` | first audience, questioning, assignment | one route locked |
| `Wayrest_Exterior` | current exterior extracted from `Main` | safe regional traversal |
| `Tutorial_Warrior` | training and hunt/patrol | prince/evidence convergence payload |
| `Wayrest_MagesGuild` | spell training and delivery setup | access to restricted prison |
| `Wayrest_Harbor` | sailing and thief instruction | tower objective complete |
| `Wayrest_SecuredTower` | infiltration objective | evidence/prince route transition |
| `Wayrest_Prison` | general cells, solitary, soul operation, convergence | prince follows player |
| `Wayrest_SeaCave` | shared escape and title-crawl vista | prologue escape complete |
| `Wayrest_Palace_Aftermath` | confrontation and outcome | ruler/title/world mutation saved |
| `Daggerfall_Arrival` | Council mission handoff and Tower reminder | opening chapter complete |

Reuse an authored scene for multiple states when safe; use world-state variants rather
than duplicating whole environments. Every transition needs a spawn ID, fade/load
contract, autosave policy, companion handoff, and recovery path if loading fails.

## Required data and systems

| System/data | Minimum responsibility |
|---|---|
| `CharacterProfile` | name, race, appearance recipe, pronouns, background, declared inclination |
| `StoryState` | chapter, stage, selected route, flags, choices, outcome, title |
| `StoryDirector` | sole authority for beat transitions, idempotent actions, checkpoints, and route convergence |
| `QuestDefinition` / `QuestStage` | objectives, conditions, transitions, failure/recovery, map markers |
| `DialogueGraph` | conditional lines, choices, effects, subtitles, speaker/camera cues |
| `EvidenceRecord` | source route, inspected state, legal weight, confrontation availability |
| `Interactable` | inspect, talk, loot, use, open, lockpick, pickpocket, activate, board |
| `DoorAndLock` | keys, difficulty, lock state, animation, crime/noise response |
| `DetectionSystem` | sight, hearing, suspicion, alert, concealment, route tutorial feedback |
| `PickpocketSystem` | target inventory, chance/skill rule, detection, consequence |
| `SailingController` | board, steer, throttle, disembark, reset, objective corridor |
| `CompanionController` | prince follow/wait/teleport recovery, combat policy, scene handoff |
| `WorldMutation` | King/ruler swap, guards/dialogue, prison policy, banners, blocked/open areas |
| `GameState` | gameplay, dialogue, cinematic, menu, loading, death; input/cursor/time ownership |
| `SceneTransitionService` | additive load order, spawn placement, companion handoff, fade, failure recovery |
| `CinematicRunner` | deterministic cues plus an idempotent end-state applied when watched or skipped |
| `SaveGameV3` | atomic write/backup, profile plus all story/system state, scene/spawn, versioning, safe migration |

Author content in ScriptableObjects or another inspectable data format; keep logic in
reusable runtime systems. Dialogue, quest stages, and cutscene cues must not be buried in
one-off scene scripts.

## Full production plan

### Phase 0 — story production package and regression baseline (4–7 days)

- Break the exact outline into numbered beats, objectives, dialogues, choices, and
  transition conditions.
- Write a screenplay pass for the ship, rescue, survivor processing, both audiences,
  every tutorial, the prince reveal, prisoner exposition, escape, confrontation,
  succession, player title, and Daggerfall handoff.
- Build a route/outcome matrix and stable ID registry.
- Record the cave-exit title placement and the shared convergence payload as hard
  contracts.
- Snapshot current build, test, performance, scene, and save behavior.
- Add the existing P0 PlayMode and packaged-player smoke tests.

**Gate:** every sentence in `storyline.md` maps to at least one owned scene, system task,
content task, and acceptance test; current prototype behavior remains green.

### Phase 1 — persistent technical spine (7–12 days)

- Create `Bootstrap`, additive loading, spawn points, transitions, fades, loading UI, and
  failure recovery.
- Add `GameState`, InputActions, prefabbed actors/UI, data-driven quests, conditional
  dialogue, story flags, generic interactions, and evidence.
- Implement `CharacterProfile` and `SaveGameV3`, including v2 handling and menu header
  validation.
- Establish protagonist/NPC animation rigs, navigation, companion handoff, cinematic
  cameras, subtitles, and an AudioMixer before authoring dozens of scenes.
- Extract the current Wayrest/Daggerfall exterior geography from generated `Main` without
  destroying the existing working world.

**Gate:** a test quest can cross three additive scenes, branch, acquire evidence, save,
quit, continue, restore a companion, mutate the world, and roll back correctly.

### Phase 2 — shipwreck, rescue, creator, and first audience (8–14 days)

- Build a traversable merchant-ship deck with passengers and a constrained opening
  viewpoint toward the Tower and warships.
- Stage the pulse using lighting, VFX, audio, camera impulse, fractured/damaged ship
  variants, controlled physics, water entry, blackout, and rescue. It may be tightly
  directed, but every described visual beat must appear.
- Build the rescue-ship transition and Wayrest survivor-processing dock.
- Implement character creation with preview, validation, default/random options, profile
  persistence, and keyboard/controller navigation.
- Author the King's “every soul must contribute” edict, questioning about the missing
  prince, remembered/vague/no-memory responses, skill declaration, route assignment, and
  four valid exits. The edict should also foreshadow the prison soul operation without
  revealing it prematurely.

**Gate:** New Game reaches each assignment without a broken camera, lost profile, invalid
save, sequence skip, or contradictory quest state. Cutscene skipping lands on the same
canonical flags as watching.

### Phase 3 — shared tutorial mechanics and four complete routes (18–32 days)

Build reusable mechanics first, then author every route.

#### 3A — shared foundations

- Tutorial prompts, objective journal, checkpoints, recoverable fail states, route timing,
  crime/alert hooks, inventory/equipment handoff, and evidence collection.
- Combat, magic, stealth, lock, pickpocket, and sailing telemetry so balancing is based on
  observed completion and failure data.

#### 3B — Warrior

- Guard-yard instruction with movement, melee, block, hit feedback, and a safe spar.
- Hunt/patrol with navigation, encounter pacing, return/failure handling, and a readable
  transition into the secret prisoner transport.
- Wounded-prince discovery, recognition, evidence, and prison-convergence transition.

#### 3C — Mage

- Spell instruction with cast/resource/target feedback and a nonlethal practice space.
- Soul-gem delivery objective, restricted-access rules, environmental foreshadowing, and
  a staged accident that opens the sealed cell.
- Prince reveal, route-specific evidence, and convergence transition.

#### 3D — Commerce / Thief

- A bounded but controllable sailing lesson with board/disembark/reset behavior.
- Sneaking/detection, pickpocketing, lockpicking, crime response, and non-blocking retry
  paths.
- Secured-tower infiltration connected spatially and narratively to the prison.
- Retrieve the assigned object, find the prince, secure evidence, and converge.

#### 3E — None / Refuse

- Immediate arrest and transfer to general population.
- Prisoner conversations that reveal the black-soul-gem operation without relying on one
  unskippable exposition dump.
- A short, intentional route to solitary and the prince, with only the mandatory state
  needed for the shared escape.

**Gate:** four clean-save end-to-end tests reach the same convergence contract. The None
route is measurably fastest; the other routes teach their promised mechanics; failure,
death, save/load, and sequence breaks cannot strand progression.

### Phase 4 — prison, prince, escape, and title moment (8–14 days)

- Complete the prison layout: public cells, restricted wing, solitary cell, processing
  areas, evidence room, soul-harvesting operation, guard routes, and escape connections.
- Author the prince's explanation of his alternative, interception, imprisonment, the
  King's motive, and the Tower/Thalmor seed.
- Give route-specific discoveries contextual dialogue while preserving one canonical
  reveal.
- Implement prince companion behavior, guard alerts, alternate traversal within the
  shared escape, checkpoints, and stuck/teleport recovery.
- End the escape through a sea cave. Compose the exterior reveal, music swell, title card,
  subtitle timing, input hand-back, and autosave as one tested sequence.

**Gate:** every route can escort the prince from discovery to cave exit; watching or
skipping dialogue/cinematics produces identical required state; the title appears once
and only at the authored cave checkpoint.

### Phase 5 — confrontation, succession, and persistent consequences (7–12 days)

- Build the return/confrontation path and explain why the evidence can be presented
  without the player simply being rearrested.
- Stage evidence presentation, prince testimony, King's defense, player response, and the
  selected kill/imprison outcome.
- Implement the successor state, throne-room population swap, guard/faction reactions,
  prisoner release, outlawed operation, closed/open doors, updated dialogue, journal, and
  banners or other readable visual consequences.
- Grant and persist the selected official title. Display it consistently in dialogue,
  journal, save metadata, and subtitles where relevant.
- Provide safe defaults for conflicting or missing flags so old development saves cannot
  create two kings or an absent ruler.

**Gate:** every route and supported King outcome reaches one valid post-coup world. Save,
reload, death, fast travel, and scene re-entry preserve the ruler, law, NPC set, title,
and quest stage.

### Phase 6 — Daggerfall and wider-game handoff (5–10 days)

- Add the quest to secure legitimacy from High Rock's other rulers.
- Gate departure until the aftermath state is valid, then support the intended travel
  method and arrival spawn.
- Author a convincing Daggerfall arrival/Council setup rather than leaving only a map
  marker.
- End the chapter with a visible or spoken Tower reminder and a clear next objective.

**Gate:** a blind player understands who rules Wayrest, what changed, why their title
matters, why they are going to Daggerfall, and why the Tower remains important.

### Phase 7 — art, animation, audio, UI, and world-density pass (15–25 days)

- Apply the restrained dark-metal, carved-stone, aged-parchment, cool-fog, muted-bronze
  visual language consistently without copying Bethesda assets or exact UI.
- Replace placeholder structures along the entire critical route with authored silhouettes,
  entrances, interiors, clutter clusters, decals, vertical sightlines, lighting, and
  navigation.
- Animate key performances and all tutorial feedback: locomotion, attacks, blocks, hits,
  spell casts, work loops, prisoner states, ship reactions, doors, locks, and the King's
  removal.
- Create a narrative audio mix: bay/ship, storm/pulse, docks, city, palace, guild, prison,
  cave, confrontation, and Daggerfall ambience; add footsteps, Foley, UI, impacts, and
  music transitions.
- Upgrade UI layouts for creator, dialogue choices, tutorial prompts, evidence, journal,
  inventory, title card, map, settings, subtitles, and save/load at common aspect ratios.
- Use Blender for modular interiors, collision proxies, LODs, damaged ship variants,
  prison/soul machinery, wall/gate hero pieces, and missing props when licensed assets do
  not cover them.

**Gate:** the complete critical path is visually coherent, readable, navigable, mixed,
and stable at the chosen performance floor without requiring developer commentary.

### Phase 8 — full-route QA and release (10–18 days)

- Run the route/outcome matrix below on clean saves and upgraded development saves.
- Profile the ship event, Wayrest exterior, crowded palace, prison, and Daggerfall arrival;
  set budgets for CPU/GPU frame time, memory, renderers, lights, audio voices, and loading.
- Add quality presets, rebinding, controller support, subtitle sizing, volume controls,
  motion reduction, camera sensitivity/FOV, contrast aids, and readable failure messages.
- Run external blind playtests for every route, with the None path tested separately as a
  speed route.
- Remove debug/editor payloads, complete notices/credits, build a clean zip, and validate
  it on a second machine.

**Gate:** no progression blockers or save corruption across the full matrix; build and
console are clean; all story beats are present; the packaged game completes on the target
machine without editor support.

## Content and asset checklist

Existing source assets reduce the modelling burden: the repository already contains
multiple ship/canoe models, cave pieces, a large modular castle/wall/door/tower/crypt set,
furniture, human variants, and a broad OGG sound library. They still require selection,
conditioning, materials, collision, LODs, prefabs, and license records.

| Story location/beat | Required content |
|---|---|
| Merchant ship | intact and damaged variants, deck collision, rigging/cargo, passengers, crew, rescue ship |
| Bay event | distant Tower, Thalmor silhouettes, pulse VFX, shockwave, debris, water entry, underwater/blackout transition |
| Wayrest docks | harbor approach, survivor triage, guards, civilians, processing stations, character-creator backdrop |
| First palace visit | gate-to-throne route, throne room, King, court, guards, missing-prince visual references |
| Warrior route | guard yard, weapons, targets, patrol/hunt terrain, hostile encounter, secret transport |
| Mage route | guild hall, training room, spell targets, soul-gem props, service corridor, sealed-cell mechanism |
| Commerce/Thief route | steerable boat, dock lesson space, stealth route, pockets/loot, locks, secured tower |
| Prison | general population, solitary, restricted wing, guard posts, evidence, black soul gems, harvesting machinery |
| Escape | alternate cover/doors where appropriate, guard response, prince navigation, sea cave, reveal vista |
| Confrontation | evidence presentation, throne variants, King removal, successor, post-coup guards/prisoners/banners |
| Daggerfall handoff | arrival landmark, travel transition, Council representatives/setup, next-quest framing |

Named speaking roles needed at minimum are the King, prince, rescue captain or sailor,
processing guard, one route instructor per branch, two or more prisoners, confrontation
witnesses/allies, successor if not the prince, and a Daggerfall/Council contact. Names can
remain temporary, but stable role IDs, casting requirements, dialogue ownership, and
animation needs cannot.

## Dependencies and critical path

1. Story graph, IDs, and outcome locks precede final dialogue and save schema.
2. Bootstrap, scene loading, `GameState`, interactions, quest stages, and Save V3 precede
   branch authoring.
3. Character profile precedes survivor processing and every later player spawn.
4. Detection/locks/pickpocket/sailing precede the Commerce/Thief route.
5. Dialogue conditions, evidence, doors, companion AI, and navigation precede convergence
   and escape.
6. World mutation and outcome persistence precede confrontation and Daggerfall gating.
7. Final lighting, audio, animation, and environment dressing follow greybox route lock,
   but representative assets must be proven early in each location.

The Commerce/Thief route is the largest mechanic risk. Prototype its sailing and stealth
loops during Phase 1 even though final content is scheduled in Phase 3. The companion and
save-state tests are the largest progression risk and should stay continuously green from
their first implementation onward.

## Required QA matrix

| Test family | Required coverage |
|---|---|
| Four routes | Warrior, Mage, Commerce/Thief, None from New Game to Daggerfall handoff |
| Assignment | every background/declared-skill mapping, refusal, invalid/default selection |
| Route recovery | failure, arrest, death, checkpoint, objective retry, accidental area exit |
| Convergence | correct prince state, route evidence, dialogue variant, inventory, quest stage |
| Cinematics | watch and skip shipwreck, audiences, reveal, title, confrontation, transition |
| Saves | before/after every scene boundary, mid-route, mid-escape, pre/post-coup, corrupt/old save |
| Companion | blocked path, combat, wait, teleport recovery, scene load, save/load, death prevention |
| Outcomes | every supported King result and successor result, including reload and re-entry |
| World mutation | ruler/NPC/banner/door/prison/dialogue/journal state cannot regress or duplicate |
| Input/UI | keyboard/mouse and controller, common aspect ratios, pause/dialogue/loading ownership |
| Geometry | doors, stairs, cells, ships, walls, cave, docks, navigation, no fall-through or softlocks |
| Performance | ship destruction, docks crowd, city, palace, prison, VFX, Daggerfall arrival |
| Speed route | intended fastest completion, mandatory exposition retained, no invalid skips |

Automate state-machine, save, route-convergence, world-mutation, and scene-load invariants
in EditMode/PlayMode tests. Use packaged-player smoke runs and human playtests for timing,
cinematics, controls, readability, navigation, and emotional continuity.

## Asset and licensing policy

“Free” does not mean “no restrictions.”

- CC0 assets such as the current Poly Haven, Quaternius, and Kenney inputs are the easiest
  to keep in a public source repository.
- OFL fonts can be bundled with a game, but preserve the copyright/license material and
  add the full notices to the distribution.
- Mixamo permits royalty-free use in games, but record the Adobe account/source and do not
  treat downloaded source animation files as public-domain assets.
- Unity Asset Store assets generally belong in an embedded licensed product; do not assume
  their raw source can be published in this repository.
- Sonniss GDC sounds may be used and modified in games, but may not be sold as-is and the
  current license explicitly prohibits AI training use.

References: [Adobe Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html),
[Unity Asset Store EULA FAQ](https://assetstore.unity.com/browse/eula-faq),
[SIL OFL](https://software.sil.org/oflt/), and
[Sonniss GDC bundle license](https://sonniss.com/gdc-bundle-license/).

No Bethesda meshes, textures, audio, music, writing, logos, names, or UI art belong in the
deliverable. “Skyrim-inspired” here means hierarchy, restraint, and atmosphere only.

## Deferred naming policy

Place and faction renaming is intentionally postponed. Do not apply the earlier working
rename table, bulk-rename assets, rewrite `storyline.md`, record final voice-over, or bake
names into save keys. Until the user starts the rename pass:

- keep the names in `storyline.md` in all visible story material;
- use neutral stable IDs and localized/display-name fields internally;
- keep crests, logos, and commissioned name-specific art replaceable;
- treat public-release naming and rights review as a later release gate, not a blocker for
  implementing the story now.

## Narrative and production locks still to resolve

These choices fill implementation gaps left open by the outline; they do not change its
scope.

1. King and prince identities, appearances, factions, and final dialogue voices.
2. Tower pulse rules, its causal link to the shipwreck/soul operation, and why the player
   remembers only part of the event.
3. Why the rescue ship selects Wayrest and why a castaway is brought before the King.
4. The exact evidence set available on all routes and why the court accepts it.
5. Supported character races, bodies, appearances, backgrounds, pronouns, and starter
   equipment.
6. Tutorial failure rules, gear carryover, and the measured target for the None route.
7. Whether the King outcome is a player choice or one fixed result, with consequences.
8. Whether the prince or another legitimate heir succeeds; both are permitted by the
   story, but content production needs one initial authored answer.
9. Final official player title and how it appears in dialogue/journal/save metadata.
10. Silent or voiced protagonist, performance target, subtitle standard, and localization
    scope.
11. Target hardware, frame-rate/memory floor, supported inputs, and release format.

The cave-exit title position and all four route contents are already locked by this plan.
Naming is not part of this decision list.

## Progress ledger

Update this document at every milestone with:

1. completed story beat IDs and scenes;
2. the four-route/outcome test result and blocker count;
3. save/schema version and migration coverage;
4. outstanding characters, environments, props, animation, VFX, audio, UI, and dialogue;
5. external playtest completion time and confusion/softlock reports;
6. frame time, draw calls, memory, and loading time at the five stress locations;
7. new assets, source/license proof, and Blender time saved versus manual cleanup.

Current conclusion: **the complete story is viable, but most of its visible content and
branching infrastructure are still pending**. The next decisive deliverable is Phase 0's
validated story graph followed by Phase 1's three-scene save/load/consequence proof.
