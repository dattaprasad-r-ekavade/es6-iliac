# Vertical slice plan — `storyline.md`, fully playable

**Audit date:** 2026-08-01 · **Retargeted to the vertical slice:** 2026-07-29

**Story authority:** `storyline.md` for Chapter 01 · [`Docs/STORY_ARC.md`](Docs/STORY_ARC.md)
for the world premise and Chapters 02+

**Gameplay flow authority:** [`Docs/GAMEPLAY_DESIGN.md`](Docs/GAMEPLAY_DESIGN.md) — navigation,
dialogue, travel, combat scope and the Everspire-as-HUD decision

**Picking this up cold?** Start at [`Docs/AGENT_HANDOFF.md`](Docs/AGENT_HANDOFF.md) — verification
commands, invariants, known traps and the ordered work packets.

**Target:** a vertical slice in which the complete authored opening chapter is playable end
to end — shipwreck, rescue, character creation, King's audience, all four tutorial routes,
the prince reveal, prison escape, cave-exit title crawl, confrontation and regime change,
player title, and the Caldemar / Crown Council handoff. Content-complete, quality-reduced.
See “Vertical slice definition” for the cut-lines.

**Expected first-route length:** about 45–70 minutes before polish/playtest adjustment,
with substantial replay-only content in the other three routes.

**Studio:** DataTheCodie Studios · **Engine:** Unity 6000.5.3f1, URP 17.5 · **Platform:**
Windows player

**Current delivery goal:** build the Map Editor MVP now that VS2's complete Chapter 01 grey
thread is green. VS1's persistent technical spine and the four-route VS2 gate are complete;
the generic open-world P2 backlog remains parked until the editor can feed authored scenes.

### Plain-English stage names

`VS` only means **Vertical Slice**. It is a stage number, not a feature or secret codename.

| Short name | Plain-English meaning |
|---|---|
| VS0 | Finish planning the story and dialogue |
| VS1 | Build the underlying save, scene, quest and dialogue systems |
| VS2 | Make the entire story playable in rough grey boxes |
| **Map Editor MVP** | Build the easy external world-layout editor before detailed environments |
| VS3 | Replace the rough opening with the real shipwreck, rescue, creator and audience |
| VS4 | Build the reusable route mechanics: sailing, stealth, locks, pickpocketing and companion AI |
| VS5 | Build the four actual tutorial routes |
| VS6 | Build the prince reveal, prison escape and cave title moment |
| VS7 | Build the confrontation, new ruler and Caldemar handoff |
| VS8 | Polish, test and package the complete chapter |

Other repeated planning terms:

- **Technical spine** = invisible foundations shared by every story scene.
- **Grey thread** = the whole story works from beginning to end, but uses plain rooms,
  placeholder text and simple actors.
- **Convergence** = all four routes meet when the prince is found.
- **Gate** = the test that must pass before moving to the next stage.
- **Stable id** = an internal computer label; players and world authors never need to see it.

## Scope lock

`storyline.md` is authoritative. This plan does not replace, condense, or redirect it.
The following are required content, not stretch goals:

1. Merchant ship voyage, Tower sightline, Ivory Concord warships, arcane pulse, wreck, and water
   sequence.
2. Rescue ship, Estmere arrival, survivor processing, and real character creation.
3. King's audience and assignment by the player's declared inclination.
4. Four materially distinct routes:
   - Warrior: combat training, hunt/patrol, secret prisoner transport.
   - Mage: spell training, soul-crystal delivery, restricted prison accident.
   - Commerce/Thief: sailing, lockpicking, pickpocketing, sneaking, and secured tower.
   - None/Refuse: general prison, soul-harvesting reveal, and the deliberately fastest
     route.
5. Prince discovery, explanation, route convergence, evidence, and joint escape.
6. A shared sea-cave exit used as the walking-into-the-world title-crawl moment.
7. Confrontation with the King, his removal, the successor, the prisoner soul-binding
   ban, and persistent world-state changes.
8. The player's official title and the mission to Caldemar to seek recognition from the
   Crown Council.
9. A final Tower reminder that hands the player into the wider main story.

The setting rename was applied on 2026-07-29 (see “Naming policy” below). Story names in
this plan and in `storyline.md` are the current display names. Technical IDs are stable and
separate from display strings, so any further rename stays a display-only change and does
not require rewriting saves, quest logic, or scene references.

## Verdict

The exact story is viable in this project, and a playable slice of all of it is a
reasonable next deliverable. The existing world and gameplay prototype are a useful
foundation, but the opening is a **multi-scene RPG chapter**, not a 15–20 minute demo. It
needs new narrative, interaction, traversal, save, AI, interior, cinematic, animation,
audio, and world-mutation work.

What makes the slice achievable is dropping the *finish* bar, not the story: greybox art,
placeholder audio, no voice acting, minimum animation. What makes it risky is that four
distinct routes must all reach one convergence contract. The plan front-loads that risk
into VS2 (the grey thread) so it is discovered in week three rather than month three.

Estimated readiness for the complete authored opening: **20–25%**.

| Area | Readiness | Evidence |
|---|---:|---|
| Build and tooling | 80% | Windows build, asmdefs, compile checker, 20 EditMode tests |
| Exterior-world foundation | 60% | elevated land, walled cities, roads, docks, collision, map/travel |
| General gameplay prototype | 55% | movement, basic combat, inventory, NPCs, enemies, save/load |
| Story architecture | 25% | 42-beat contract plus Bootstrap/additive scene and stable-spawn runtime exist; quest/dialogue graph and authored story scenes remain |
| Route-specific mechanics | 10–15% | basic combat/magic exist; sailing, stealth, locks, and pickpocketing do not |
| Interiors, cinematics, and actors | 5–10% | useful source assets exist; no authored interiors, performances, or companion flow |
| Audio, animation, and final feel | 10–15% | SFX library and fonts exist; actors are static and there is no narrative mix |
| Story QA and shipping | 10–15% | clean build and geometry tests exist; no end-to-end route or save-state coverage |

Planning ranges, assuming one focused developer using existing low-poly assets:

- **This vertical slice — every story beat playable, all four routes, greybox finish:**
  73–111 focused days. Broken down in the milestone table below.
- **Presentable low-poly release candidate:** a further 25–40 days on top of the slice,
  mostly art, animation and audio.
- **Fully voiced, bespoke cinematic quality:** a separate production tier measured in
  additional person-months.

These are planning ranges rather than promises. The milestone gates below keep the full
story intact while exposing technical or content problems early.

## Current goals — 2026-08-01

| Order | Goal | Current state | Exit condition |
|---:|---|---|---|
| 1 | **Close VS0** | Complete except screenplay, deliberately deferred to the VS2→VS3 content window | narrative contracts, regression baseline and asset ledger stay authoritative |
| 2 | **Build VS1** | **Complete: W-01–W-09 and consequence gate pass** | preserve the green technical-spine regression suite |
| 3 | **Prove VS2** | **Complete: four routes reach B830** | preserve the grey-thread route gate and stable scene contract |
| 4 | **Build the Map Editor MVP** | **Next** | edit and preview world layout without Unity before detailed environments are authored |
| 5 | **Replace the grey thread with content** | Not started | VS3–VS7 gates pass in order |
| 6 | **Package the slice** | Not started | VS8 route/outcome matrix, performance floor, second-machine build and blind playtests pass |

Immediate work is therefore documentation and architecture, not more free-roam POIs,
crafting, settlements or visual sprawl. Existing exploration systems stay working, but new
work must serve a Chapter 01 beat or a VS1/VS2 dependency.

## What is actually in the project

Verified in the repository and Unity Editor:

- 50 runtime scripts plus 14 editor scripts;
- persistent `Bootstrap`, generated legacy `Main`, additive `Estmere_Exterior`, and 11
  regenerable Chapter 01 grey scenes, plus four small transition fixtures;
- a successful local Windows player (142.1 MB) booting through `Bootstrap`;
- 45/45 EditMode and 30/30 PlayMode tests passing as of 2026-08-01, including scene-contract
  checks and all four VS2 routes reaching the Caldemar handoff;
- 5 NPCs, 5 hostile spawns, 3 quests, and 8 discovery/travel markers;
- SaveGameV4 persists player stats, inventory, quests, discovery, scene/spawn, profile, route,
  evidence, companion, cinematics, equipment, skills, mutations and outcome state;
- one 6.8 km generated bay with ten continuous elevated landmasses, three walled cities,
  five regional roads/causeways, and three islands;
- CC0 environment packs, OFL fonts, UI/combat SFX, and a Blender-generated seven-piece
  desert architecture kit;
- regenerable Player, GameSystems, NPC and full HUD visual prefabs, plus five NPC archetype
  ScriptableObjects. Animator controllers/clips, authored story interiors, profiler captures,
  ambient beds, music, and implemented authored story content remain absent. One
  keyboard/mouse `.inputactions` asset owns every current binding.

`storyline.md` now supplies the narrative outline. VS2 has a playable grey implementation:
placeholder geometry, route input, beat milestones, evidence, companion and outcome state
all run through B830. Authored screenplay, actors, mechanics and final environments remain
VS3–VS7 work.

## Completed hardening

### Earlier pass

- Added `WorldLayout` as the shared source for geography and travel sites.
- Added explicit gameplay layers and layer-masked interaction/combat.
- Fixed all NPCs and enemies spawning in one pile.
- Moved two POIs out of open water and corrected road placement.
- Replaced 639 per-prop foliage updates with one culling system.
- Added asmdefs, `Tools/compile-check.py`, headless build tooling, and 9 EditMode tests.
- Reduced the Windows build from roughly 206 MB to 140 MB with a texture budget.
- Added the deterministic Blender Sarrakh kit generator.

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
- **World geometry:** fixed the Corrath tower inheriting a huge island-top scale.
- **Prototype artifacts:** removed generated city, POI, enemy, and NPC world-space
  nameplates that appeared mirrored from behind.
- **Cutscene:** authored actor scale is preserved and actors are always cleaned up after
  completion or skip.
- **Foliage:** culling follows the actively rendering camera during the intro and gameplay.
- **Dialogue/input:** dialogue now blocks gameplay, pauses safely, closes explicitly, and
  uses realtime timeout; menus consistently own pause and cursor state.
- **Presentation:** rebuilt the HUD and title menu with translucent charcoal, silver,
  muted bronze, Cinzel, and EB Garamond — restrained hierarchy built from original and
  licensed assets.
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
  guarantees, higher Karnoth/Kiln profiles, and flat city build zones blended
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
- **Regional structure:** added Kelrith–Karnoth, Qadris–Sarrakh Waste, and
  Sarrakh Waste–Kiln routes, widened roads, added guarded causeway rails, and concentrated
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

- `python Tools/compile-check.py` rerun 2026-08-01: Editor (14 files), Runtime (50 files),
  PlayMode (9 files), and EditMode Tests (5 files) assemblies pass.
- Unity console after regeneration and title startup: **0 project errors, 0 project
  warnings**.
- Unity EditMode rerun 2026-08-01: **45 passed, 0 failed**.
- Unity **PlayMode** 2026-08-01: **30 passed, 0 failed**, exit code 0. Scene tests prove
  additive travel and rollback; state tests prove pause/cursor/input policy, exact restoration
  after temporary loading, and protection against mismatched state releases.
- `McpBootstrap` no longer starts the MCP bridge in batch mode. It retried 60 times against a
  server that cannot exist headlessly, and the package's connection errors failed whichever
  test fixture was active — the suite was non-deterministic before this was found.
- Live title, Continue, first-person Game view, city entrance, and coastline inspected
  after scene regeneration.
- Scene audit: **2,042 GameObjects, 1,797 renderers, 905 colliders**; 894 are box
  colliders and the only 10 mesh colliders are the terrain surfaces.
- Geometry audit: **0 invalid terrain indices, 0 fake roof-corner buildings, 0 building
  collider violations, 0 wall collider violations, and 0 approach collider violations**.
- Physics probes: representative wall and building rays hit their box colliders; the
  south gate walking ray remains clear; every city has five collider-backed approaches.
- Current Windows x64 build **2026-08-01: `Builds/Windows/Kessil.exe`, 142.1 MB, 0 errors**,
  built headlessly in 24.7s via `BuildPlayerCommand.BuildWindows`, with `Bootstrap` as scene
  zero. This replaces the stale
  pre-rename `IliacBay.exe`, which still sits beside it in the (gitignored) build folder and
  can be deleted.
- The packaged player launches to the title screen with **0 exceptions or managed errors in
  `Player.log`**. Driving it *through* gameplay is still manual; the New Game, Continue and
  Return-to-Menu paths are now covered automatically in-editor instead.
- Volume inspection confirms Color Adjustments, Vignette, and Bloom are real referenced
  components rather than null profile entries.

**The VS0 regression baseline is complete as of 2026-08-01.** Save rollback, menu flow, boot,
fast travel, dialogue pause, death recovery and merchant economy are now covered
automatically, and a current packaged build exists.

What the tests still do **not** prove: combat depth beyond the merchant/economy path, story
state, route convergence, and packaged-player behaviour past the title screen. Those arrive
with VS1's systems and VS2's grey thread — there is nothing yet for them to test.

## Remaining issues and risks

### P0 — required before building story scenes

*Delivered by VS0 and VS1. Nothing in the slice plan starts authoring until these are done.*

- [x] Decompose `storyline.md` into 42 stable beats with owning scenes, system
  dependencies, exit states and acceptance tests in `Docs/CHAPTER01_BEATS.md`.
- [x] Lock the continuity contract shared by all routes:
  - the prince's location and condition at each stage;
  - which route-specific evidence enters the shared evidence inventory;
  - how each route reaches the same prison checkpoint without contradicting another;
  - how the prince and player reach the sea cave;
  - the cave exit as the title-crawl checkpoint;
  - how they safely return to confront the King after the title moment.
- [x] Lock the outcome scope: the player chooses kill or imprison, the prince succeeds,
  and the player becomes Crown Envoy.
- [x] Lock the character-creation production ceiling: 3–4 ancestries using one shared body
  and rig.
- [ ] Finish the Chapter 01 screenplay and dialogue against the beat ids. **Unblocked as of
  2026-08-01** — both narrative locks that gated it are closed.
- [x] Name and characterize the principal cast, and define the four ancestries and their
  origins. House Selwyn, six named recurring roles, four regional ancestries. Only ancestry
  *starting values* remain, and they are a VS3 item.
- [x] Resolve why the rescue ship chooses Estmere and why the survivors receive a royal
  audience. **The rescue ship is the King's own**, searching the prince's route; one premise
  answers both.
- [x] Define the Everspire pulse rules before VS3 authors the wreck and memory responses.
  **Cause deferred to later chapters; effect authored** — memory disruption for everyone who
  was on the water.
- [x] Specify the speed route as intentional content. Target time **15 minutes**; mandatory
  information is B510's split reveal, skips are permitted but the reveal is repeatable, and
  `ev.prisoner_testimony` is the required state flag. Failure and gear rules are now locked for
  all four routes in `Docs/CHAPTER01_BEATS.md`.
- [x] Separate stable internal ids from display strings in world and story documentation.
- [x] Add PlayMode smoke tests for New Game, Continue, save → kill → load rollback, merchant
  purchase, fast travel, dialogue pause, death/rescue, and return to menu. **Done 2026-08-01:**
  `Game.PlayModeTests`; the suite is now **29/29 green**. All eight original categories are
  covered, plus SaveGameV4 migration/atomicity, story/dialogue/cinematic contracts,
  additive A→B→C travel, transactional rollback and the W-09 consequence gate. New Game,
  Continue and Return-to-Menu still drive the real generated `Main` flow.
- [x] Produce a current Kessil Bay Windows build and repeat the packaged launch smoke.
  **Updated 2026-08-01 after VS1:** `Builds/Windows/Kessil.exe`, 142.1 MB, 0 errors,
  built headlessly in 38.3s with Bootstrap as scene zero. The packaged player initializes
  with **0 exceptions in its smoke log**.
  Driving the packaged build *through* gameplay remains a manual step — but the flows it used
  to be the only evidence for are now covered automatically in-editor.
- [x] Create an asset ledger with source URL, version, license, proof/date, and whether source
  redistribution is allowed. [`Docs/ASSET_LEDGER.md`](Docs/ASSET_LEDGER.md) — everything is CC0
  except OFL fonts. Four missing licence files and unrecorded download dates are the only gaps.

### P1 — architecture and gameplay debt

- [x] Add persistent `Bootstrap`, additive loading, stable spawn ids, fade/rollback, and an
  exterior snapshot while keeping destructive `Main` regeneration safe. **W-01 complete
  2026-08-01.** `Main` remains the temporary legacy gameplay container until story scenes
  replace it during VS2/VS3.
- [x] Replace code-built player, friendly-NPC, systems and UI roots with regenerable prefabs;
  move friendly NPC definitions into ScriptableObject data. **W-04 complete 2026-08-01.**
- [x] Add one Input System actions asset for the slice's keyboard/mouse bindings. **W-03
  complete 2026-08-01:** all runtime consumers use `GameInput`; direct device polling is
  gone. Controller support remains explicitly outside slice scope.
- [x] Replace scattered pause, cursor and gameplay-input checks with one `GameStateService`.
  **W-02 complete 2026-08-01.** It owns menu, cinematic, gameplay, dialogue, loading and
  death modes, including nested loading rollback.
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
- Version the new save schema and migrate, preserve as legacy sandbox, or safely reject
  current v3 prototype saves.
- The current Kessil prototype is already schema v3. The story-aware format must therefore
  be **v4**, not v3; otherwise old stat/world saves could be accepted without route or
  chapter state.
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
- Estmere specifically needs a survivor-processing dock, functioning palace/throne room,
  guard yard, Arcanum, working harbor, secured tower, layered prison, soul-harvesting
  chamber, escape route, and sea cave. Caldemar needs at least a convincing arrival and
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
- Before any public distribution, perform the final IP/rights review. The setting rename
  is done; what remains is the repository directory, the `.sln`, and a review of art,
  audio and font provenance.
- Test a clean zip on a second machine.

## Story flow contract

```mermaid
flowchart TD
    A["Merchant ship and Tower pulse"] --> B["Rescue ship and Estmere docks"]
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
    R --> G["Travel to Caldemar / Crown Council"]
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
| `Estmere_Docks` | rescue arrival, processing, character creator | valid character profile |
| `Estmere_Palace` | first audience, questioning, assignment | one route locked |
| `Estmere_Exterior` | current exterior extracted from `Main` | safe regional traversal |
| `Tutorial_Warrior` | training and hunt/patrol | prince/evidence convergence payload |
| `Estmere_Arcanum` | spell training and delivery setup | access to restricted prison |
| `Estmere_Harbor` | sailing and thief instruction | tower objective complete |
| `Estmere_SecuredTower` | infiltration objective | evidence/prince route transition |
| `Estmere_Prison` | general cells, solitary, soul operation, convergence | prince follows player |
| `Estmere_SeaCave` | shared escape and title-crawl vista | prologue escape complete |
| `Estmere_Palace_Aftermath` | confrontation and outcome | ruler/title/world mutation saved |
| `Caldemar_Arrival` | Council mission handoff and Tower reminder | opening chapter complete |

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
| `DialogueGraph` | **topic-based, not tree-based** — keyword hypertext with conditional responses, choices, effects, subtitles, speaker/camera cues. See `Docs/GAMEPLAY_DESIGN.md`; this is the one delta with a VS1 deadline |
| `EvidenceRecord` | source route, inspected state, legal weight, confrontation availability, **full readable document body** — evidence is shown, never summarised into a journal line |
| `Interactable` | inspect, talk, loot, use, open, lockpick, pickpocket, activate, board |
| `DoorAndLock` | keys, difficulty, lock state, animation, crime/noise response |
| `DetectionSystem` | sight, hearing, suspicion, alert, concealment, route tutorial feedback |
| `PickpocketSystem` | target inventory, chance/skill rule, detection, consequence |
| `SailingController` | board, steer, throttle, disembark, reset, objective corridor |
| `CompanionController` | prince follow/wait/teleport recovery, combat policy, scene handoff. **Authored sequences only** — no open-world travel companions, so it never has to survive ferries, private sailing or arbitrary world save/load |
| `WorldMutation` | King/ruler swap, guards/dialogue, prison policy, banners, blocked/open areas |
| `GameState` | gameplay, dialogue, cinematic, menu, loading, death; input/cursor/time ownership |
| `SceneTransitionService` | additive load order, spawn placement, companion handoff, fade, failure recovery |
| `CinematicRunner` | deterministic cues plus an idempotent end-state applied when watched or skipped |
| `Equipment` | weapon and armour slots with stats on `InvItem`; `PlayerCombat` reads the equipped weapon instead of a hardcoded field |
| `SkillSystem` | eight use-based skills; `Level` derives from total skill progress; the five anti-grind rules in `Docs/GAMEPLAY_DESIGN.md` |
| `CrystalCharge` | Mana becomes non-regenerating charge drawn from soul crystals — the setting's scarcity made playable |
| `SaveGameV4` | atomic write/backup, profile plus all story/system state, scene/spawn, versioning, safe v3 handling, **plus skills and equipped set** |

Author content in ScriptableObjects or another inspectable data format; keep logic in
reusable runtime systems. Dialogue, quest stages, and cutscene cues must not be buried in
one-off scene scripts.

## Vertical slice definition

**The deliverable is one build in which `storyline.md` plays from the first frame to the
Caldemar handoff, on all four routes, with no missing beats and no developer
intervention.**

This is a *content-complete, quality-reduced* slice rather than the classic
narrow-but-final-quality kind. The story is not cut; the finish is. A beat that only works
if someone explains it, or that needs the editor to get past, is not in the slice.

### In the slice

- Every beat in `storyline.md`, in authored order, reachable and completable.
- All four routes playable and **mechanically distinct** — each teaches what the story says
  it teaches.
- Character creation that persists into every later scene and save.
- Save and load anywhere in the chapter, including mid-route and mid-escape.
- Watching or skipping any cinematic produces identical story state.
- Greybox-to-low-poly art built from the existing kits and the Blender generators.
- Library/placeholder audio with a real mix layout; subtitles on every line.
- Keyboard and mouse; one supported resolution family; Windows player.

### Deliberately not in the slice

Deferred to a later production tier, and listed here so they are not smuggled in:

- Voice acting. Silent protagonist, subtitles only. Generated cutscene voice is planned for
  the release-candidate tier; the slice builds the audio hooks and ships silent, because what
  the slice must prove is the cutscene state contract, not its performance. **The protagonist
  is silent permanently** — that is what keeps topic dialogue affordable.
- Conjuration, summons, and open-world travel companions. Cut outright, not deferred; see
  `Docs/GAMEPLAY_DESIGN.md`.
- Diving and underwater content. Swimming is surface-only and deliberately slow.
- Animation beyond a minimum set: locomotion, attack, block, hit, cast, death, and the
  handful of story-critical performances (imprisonment, the King's removal).
- Final art, lighting and material passes; interiors beyond what a beat requires; world
  density outside the critical path.
- Controller support, localization, and accessibility beyond subtitle size and volume.
- The existing free-roam sandbox (bandit camp, coastal ruin, radiant quests). It stays
  working but is **parked** — do not extend it during the slice.
- Performance work beyond holding the chosen floor at the five stress locations.

### The cut-line rule

When a beat runs long, reduce its *finish*, never its *presence*. A prison corridor may be
three grey rooms; it may not be a fade-to-black with a caption. The one exception is
crowd scale: named speaking roles are required, background populations are not.

## Vertical slice plan

Nine milestones. Each has a gate that must be green before the next starts, because every
later milestone depends on the state contract the previous one froze.

Planning range for the whole slice: **73–111 focused days** for one developer using the
existing kits. The grey thread (VS2) is the milestone that converts this plan from
speculation into a measurable burn-down, and it should land inside the first three weeks.

### VS0 — story production package and regression baseline (4–6 days)

- ✅ **Done 2026-07-29** — [`Docs/CHAPTER01_BEATS.md`](Docs/CHAPTER01_BEATS.md): 42 beats
  with stable ids, owning scenes, system dependencies, exit states and acceptance tests,
  plus the route/flag/evidence/cast registries, the convergence contract and the outcome
  matrix.
- Break `storyline.md` into numbered beats with ids, objectives, dialogue, choices and
  transition conditions. This is transcription and decomposition, not rewriting.
- Write the screenplay pass for every scene: ship, rescue, processing, both audiences,
  four tutorials, the prince reveal, prisoner exposition, escape, confrontation,
  succession, title grant, Caldemar handoff.
- Build the route/outcome matrix and the stable id registry (ids follow the naming policy:
  setting-neutral, never a display name).
- Record the shared convergence payload and the cave-exit title placement as hard
  contracts — these are what keep four routes from diverging into four games.
- Snapshot current build, test, scene and save behaviour as the regression baseline.
- Resolve the narrative locks that block authoring: King outcome, successor, player title,
  supported character options. See the locks list below.

**Gate:** every sentence in `storyline.md` maps to at least one beat id, owning scene,
system task and acceptance test. Existing prototype behaviour stays green.

### VS1 — persistent technical spine (8–12 days)

Build the systems the story sits on, before any story content exists.

- [x] `Bootstrap` scene, additive loading, spawn ids, transitions, fades, loading overlay,
  and a recovery path when a load fails. **W-01 complete 2026-08-01.** Three real fixture
  scenes pass in sequence and both missing-scene and missing-context rollback are tested.
- [x] `GameStateService` as the single owner of input permission, cursor, time scale and
  pause. **W-02 complete 2026-08-01.** Flow, HUD, transitions, movement, combat, interaction
  and death now consume the shared state.
- [x] One Input System actions asset, with typed `GameInput` access and no direct keyboard or
  mouse polling. **W-03 complete 2026-08-01.** Keyboard/mouse only per the scope lock.
- [x] Prefabbed player, NPC, systems and complete HUD visual hierarchy; no runtime
  `AddComponent` construction for those roots. **W-04 complete 2026-08-01:** secondary
  `MonoBehaviour` classes now live in matching files; generated Main preserves real Player
  and GameSystems prefab links; five rename-safe NPC archetype ScriptableObjects hold
  placement, appearance, dialogue and role data.
- [x] Data-driven quest stages, topic-based conditional dialogue, story flags, route gates,
  dialogue choices and readable full-document evidence records. **W-06/W-07 complete
  2026-08-01.**
- [x] `CharacterProfile` and `SaveGameV4`: scene/spawn, profile, chapter, stage, route,
  flags, evidence, companion, mutations, outcomes, cinematics, skills and equipment; atomic
  backup, menu validation, injectable test paths and safe v3 migration. **W-05 complete
  2026-08-01.**
- [x] `CinematicRunner` with deterministic cues and an idempotent end-state applied whether
  watched or skipped. **W-08 complete 2026-08-01.**
- [x] Extract the current exterior geography into regenerable `Estmere_Exterior` without
  destroying the working world.
- [x] Move global dimensions, anchors, landmasses, sites and roads into versioned
  `Assets/Resources/Data/World/kessil.world.json`; runtime generation and all prior geometry
  contracts consume it without changing the current map. **VS1 JSON plumbing complete
  2026-08-01.**

**Gate: passed 2026-08-01.** The W-09 consequence proof crosses three additive scenes,
branches, takes readable evidence, saves, simulates quit/continue, returns to the saved
scene/spawn, restores its companion and mutations, and rolls post-save changes back.

### VS2 — the grey thread (6–9 days) — **complete 2026-08-01**

**The de-risking milestone.** Make the entire chapter traversable before making any of it
good.

- Every scene in the scene table exists, even if it is a grey box with a placeholder sign.
- Every transition between beats works, in order, with the real spawn and autosave
  contract.
- All four routes are selectable at the audience and lead to the convergence checkpoint —
  stubbed, but genuinely separate paths, not one path with a flag.
- Every cinematic is a timed placeholder card that applies its real end-state.
- Dialogue is placeholder text driven by the real dialogue graph.

**Delivered:** 11 regenerable Chapter 01 grey scenes plus the extracted `Estmere_Exterior`
scene are enabled in build settings. Each grey scene has a stable `SceneContext`, named
spawns, stone walls, a gate, stepped elevation, lights and collision-backed geometry. The
`GreyThreadDirector` drives the prologue, Estmere assignment, four genuinely different route
branches, prison convergence, sea-cave title checkpoint, aftermath and Caldemar Council
handoff. F1/F2/F3/F4 select Warrior/Mage/Trade/Refuse after gameplay starts. A player is
preserved in persistent Bootstrap while old content scenes unload, fixing the first-scene
transition destruction bug.

**Evidence:** 45/45 EditMode, 30/30 PlayMode; all four routes end at B830 with evidence,
prince companion and outcome flags. Representative captures are in
[`Docs/Screenshots/vs2-estmere-palace.png`](Docs/Screenshots/vs2-estmere-palace.png) and
[`Docs/Screenshots/vs2-caldemar-arrival.png`](Docs/Screenshots/vs2-caldemar-arrival.png).

**Gate passed:** a developer can start a new game and reach the Caldemar handoff on **all four
routes** without touching the editor. Total playtime is short and it looks intentionally
grey; every later milestone now replaces placeholder content inside a proven structure.

### VS3 — opening: voyage, pulse, wreck, rescue, creator, audience (8–12 days)

- Traversable merchant-ship deck, passengers and crew, with a constrained sightline to the
  Everspire and the Ivory Concord warships.
- The pulse staged with lighting, VFX, audio, camera impulse, a damaged ship variant,
  controlled physics, water entry and blackout. Tightly directed is fine; every described
  visual beat must appear.
- Rescue ship, Estmere arrival, and the survivor-processing dock.
- Character creation: preview, validation, random/default options, profile persistence,
  keyboard navigation.
- The King's audience: the "every soul must contribute" edict, the questioning about the
  missing prince, the remembered/vague/no-memory responses, skill declaration, and four
  valid assignments. The edict foreshadows the prison operation without revealing it.

**Gate:** New Game reaches each of the four assignments with no broken camera, lost
profile, invalid save, sequence skip or contradictory quest state. Skipping the cinematics
lands on exactly the same flags as watching them.

### VS4 — route mechanics toolkit (7–11 days)

Build every route-specific mechanic **once**, as a shared system, before authoring the
routes that use them. Authoring four routes against four bespoke implementations is the
main way this slice fails.

- Tutorial prompt and objective framework, checkpoints, recoverable fail states.
- Combat and magic tutorial-state hooks on the existing systems.
- `DetectionSystem`: sight, hearing, suspicion, alert, concealment, crouch.
- `DoorAndLock` and lockpicking; `PickpocketSystem`; crime and alert response.
- `SailingController`: board, steer, disembark, reset, objective corridor.
- `CompanionController`: follow, wait, teleport recovery, combat policy, scene handoff.
- Telemetry on each, so route balancing later uses observed completion and failure data.

**Gate:** each mechanic is demonstrable in an isolated test scene, survives save/load, and
cannot strand the player. Sailing and stealth are proven here because they are the two
largest unknowns in the whole slice.

### VS5 — the four routes, authored to convergence (12–20 days)

The largest milestone. Each route replaces its VS2 stub with real content.

- **Warrior** — guard-yard instruction (movement, melee, block, hit feedback, safe spar),
  hunt/patrol with encounter pacing, the secret prisoner transport, and the wounded-prince
  discovery.
- **Mage** — spell instruction with cast/resource/target feedback, a nonlethal practice
  space, the soul-crystal delivery, restricted-access rules, and the staged accident that
  opens the sealed cell.
- **Commerce / Thief** — the bounded sailing lesson, sneaking and detection, pickpocketing,
  lockpicking, crime response with non-blocking retries, and the secured-tower
  infiltration connected spatially to the prison.
- **None / Refuse** — immediate arrest, general population, prisoner conversations that
  reveal the soul operation without one unskippable exposition dump, and a short
  intentional route to solitary. Measured against a target completion time.

Each route delivers its own evidence into the shared evidence set and enters convergence
with a valid prince state, companion state and story stage.

**Gate:** four clean-save end-to-end runs reach an identical convergence contract. The None
route is measurably fastest. No route relies on another having happened. Failure, death,
save/load and sequence breaks cannot strand progression.

### VS6 — prison, prince, escape, and the title moment (8–12 days)

- Prison layout: public cells, restricted wing, solitary, processing, evidence room, the
  soul-binding operation, guard routes and escape connections.
- The prince's explanation: his alternative, the interception, the imprisonment, his
  father's motive, and the Everspire/Ivory Concord seed.
- Route-specific discovery dialogue over one canonical reveal.
- Escort behaviour, guard alerts, alternate traversal, checkpoints, stuck recovery.
- The sea-cave exit composed as one tested sequence: exterior reveal, music swell, title
  card, subtitle timing, input hand-back, autosave.

**Gate:** every route escorts the prince from discovery to cave exit. Watching or skipping
produces identical required state. The title card appears exactly once, at the authored
cave checkpoint.

### VS7 — confrontation, succession, and the handoff (10–14 days)

- The return and confrontation path, including why the evidence can be presented rather
  than the player simply being rearrested.
- Evidence presentation, prince testimony, the King's defence, player response, and the
  authored outcome.
- World mutation: successor on the throne, throne-room population swap, guard and faction
  reactions, prisoner release, the operation outlawed, doors opened and closed, updated
  dialogue, journal and banners.
- The granted title, persisted and shown consistently in dialogue, journal and save
  metadata.
- Safe defaults for conflicting or missing flags, so a development save cannot produce two
  rulers or none.
- The Crown Council quest, gated on a valid aftermath state, and a Caldemar arrival that is
  a real space rather than a map marker. The chapter ends on the Everspire reminder and a
  clear next objective.

**Gate:** every route and supported outcome reaches one valid post-coup world. Save,
reload, death, fast travel and scene re-entry all preserve ruler, law, NPC set, title and
quest stage. A blind player can state who rules Estmere, what changed, why their title
matters, and why the Everspire matters.

### VS8 — POC assessment (3–5 days) · rescoped 2026-08-01

**Chapter 01 is an internal proof of concept**, built to prove the pipeline and teach the
process, not to be sold. The full hardening pass below is therefore **deferred to the real
release**, at the end of all eight chapters, and VS8 shrinks to what is needed to *judge* the
POC.

What VS8 keeps:

- The four-route and eight-outcome matrix, run for **structural validity** — no progression
  blockers, no save corruption, every beat present. This still matters; it is what the POC is
  proving.
- Enough readability on the critical path to answer the real question: **does this feel like
  the game described in the art direction lock?**
- A packaged build that launches and completes, so the pipeline itself is proven end to end.

What VS8 defers to the release tier: the narrative audio mix, the UI pass at multiple aspect
ratios, performance profiling against a frame-time floor, editor/debug payload removal,
notices and credits, second-machine validation, and blind playtesting.

Roughly ten days come out of the slice estimate. They are not deleted — they happen once, for
the whole game, rather than now for one chapter.

<details>
<summary>The full hardening scope, retained for the release tier</summary>

### Release hardening and packaged build (10–15 days, deferred)

- Run the QA matrix below on clean saves and on upgraded development saves.
- Bring the critical path to the readability bar: silhouettes, entrances, lighting,
  navigation, and the minimum animation set. Not a final art pass — the test is whether a
  stranger can navigate it without commentary.
- Lay in the narrative audio mix: bay, ship, storm, docks, city, palace, guild, prison,
  cave, confrontation, Caldemar. Footsteps, Foley, UI, impacts, music transitions.
- UI pass on creator, dialogue choices, tutorial prompts, evidence, journal, title card and
  save/load at common aspect ratios.
- Profile the five stress locations; hold the chosen frame-time and memory floor.
- Remove editor/debug payloads, complete notices and credits, build a clean zip, validate
  it on a second machine.
- Blind playtest every route, with the None path tested separately as a speed route.

**Gate:** no progression blockers or save corruption across the full matrix. Build and
console clean. Every beat in `storyline.md` present. The packaged game completes on a
machine with no editor and no developer present.

</details>

### Milestone summary

| Milestone | Focus | Days |
|---|---|---:|
| VS0 | Story package, ids, locks, baseline | 4–6 |
| VS1 | Technical spine | 8–12 |
| **VS2** | **Grey thread — whole chapter traversable** | **6–9** |
| VS3 | Opening through first audience | 8–12 |
| VS4 | Route mechanics toolkit | 7–11 |
| VS5 | Four routes to convergence | 12–20 |
| VS6 | Prison, escape, title moment | 8–12 |
| VS7 | Confrontation, succession, handoff | 10–14 |
| VS8 | **POC assessment** (rescoped 2026-08-01) | 3–5 |
| | **Total** | **63–101** |

Chapter 01 is an internal POC, so VS8 shrank from 10–15 days to 3–5. The deferred release
hardening happens once for the whole game rather than now for one chapter.

These are planning ranges, not commitments. The two ranges most likely to move are VS5
(four routes) and VS4 (sailing and stealth, the least-proven mechanics). If the slice has
to shrink, it shrinks by reducing route *depth* — never by removing a route, because
`storyline.md` requires all four.

## World architecture — regions, not one bay · decided 2026-08-01

**The reference is Witcher 3's regions, not Morrowind's continuous landmass.** A city plus a
dense, walkable hinterland, authored as one plane, with the ferry network connecting planes.

This supersedes the continuous 6.8 km bay. The bay was correct for a continuous world and is
wrong twice over for this one: too large to fill at the required density, and connected in the
wrong way.

| Region | Contains | First needed by |
|---|---|---|
| **Estmere** | city, docks, palace, prison, Arcanum, harbour, secured tower, sea cave, hinterland | **Chapter 01 — all of it** |
| Caldemar | Crown Council seat, city, hinterland | Chapter 02. Chapter 01 needs only an **arrival sliver**, not the plane |
| Qadris | arid city, the settlement that went dark, hinterland | Qadris spoke |
| Aldreth | highland city, hinterland | Aldreth spoke |
| Corrath | the Everspire — a small special location, not a full plane | Chapter 06 |

### What this does to Chapter 01's scope

**Chapter 01 needs one region.** `Prologue_Ship` at sea, the Estmere plane, and a Caldemar
arrival space. Caldemar and Qadris as authored regions are Chapter 02+ work.

That is a scope reduction, not only a rework.

### What it costs

- `WorldLayout` becomes per-region rather than one bay.
- `KessilWorldGenerator` (1,107 lines) needs rearchitecting around a region, not a world.
- **`WorldLayoutTests` — 16 of the 20 EditMode tests — are written against the current bay's
  geometry and will not survive.** They did their job; they need rewriting against the region
  model. Budget it rather than being surprised by it.
- Art-direction density rules now apply *per region* instead of globally. The "keep the bay
  thin and fog-limited" instruction was a consequence of the old architecture and does not
  carry over — regions are meant to be dense.
- The five performance stress locations need reselecting.

The ferry network already designed in `Docs/GAMEPLAY_DESIGN.md` is the right connective
tissue between planes, and needs no change.

## World-authoring goal — Kessil World Builder

Build this in two steps:

1. [x] **Completed during VS1:** global dimensions, anchors, landmasses, sites and roads now
   load from versioned `kessil.world.json`. The current map still generates exactly as
   before, enforced by the original geometry suite plus a JSON-source contract test.
2. **Immediately after VS2 and before VS3:** build the usable Tiled-backed Map Editor MVP.
   At that point the whole story structure has been proven, but detailed environments have
   not been authored, so coast, elevation, city, road and story-anchor edits do not force a
   large art/content rebuild.

The optional polished standalone 3D editor remains post-VS8. It is not needed to start
authoring the improved world.

This is viable and fits the project unusually well: `WorldLayout` already centralizes
landmasses, biomes, sites and road spines, and the generator already rebuilds the world
from those values. The problem is usability, not generation. Today those values are static
C# arrays and terrain shapes are mostly ellipses plus seeded noise, so changing the world
still means editing code and regenerating a destructive Unity scene.

### Recommended route

Start with **Tiled** as the easy external 2D authoring surface, then add a Kessil importer,
validator and one-click headless preview. Tiled is free/open source, supports large maps,
painted tile layers, polygons, polylines, custom properties, JSON and JavaScript extensions.
It can therefore prove the workflow before time is spent building a bespoke GUI.

Suggested authoring layers:

- `Elevation` — painted height bands or signed height values on a coarse cell grid;
- `Biome` — Halbrand, Sarrakh, coast, rock, forest and city-ground masks;
- `Land` / `Water` — editable coast polygons rather than fixed ellipses;
- `Roads`, `Rivers`, `Walls` — polylines with width/material/type properties;
- `Cities` — build-zone polygons with gates, districts, docks and flatten heights;
- `Sites` — cities, POIs, fast-travel anchors and discovery radii;
- `StoryAnchors` — stable beat/spawn ids for Chapter 01 and later chapters;
- `Exclusions` — no-foliage, no-building, encounter and navigation-control zones.

The source of truth becomes a versioned `kessil.world.json`, not a Unity scene and not
hand-edited C#. A converter reads Tiled JSON, validates stable ids and geometry, writes the
runtime format, invokes Unity headlessly, and produces a playable build plus top-down and
perspective preview images. The normal workflow requires no Unity Editor interaction.

### Tool fit

| Tool | Best use here | Limitation |
|---|---|---|
| [Tiled](https://www.mapeditor.org/) | recommended macro map, elevation/biome painting, roads, zones, POIs and metadata | 2D authoring; needs generated 3D previews |
| [Gaea](https://www.quadspinner.com/) | optional natural terrain/erosion heightmaps and masks | weak for cities, walls, quests and precise gameplay layout |
| [TrenchBroom](https://trenchbroom.github.io/) | optional fast brush-built interiors, caves and compact city blocks | not a regional terrain/world-map editor; requires a custom importer |
| [Crocotile 3D](https://www.crocotile3d.com/) | simple low-poly modular props and compact tile-built spaces | scene/asset modeller, not authoritative world data |

### Delivery range

- **8–14 focused days:** data refactor, Tiled project/template, importer, validation,
  undo-safe source files and one-click headless rebuild with preview screenshots.
- **A further 10–20 days:** dedicated standalone 3D preview, elevation sculpt brushes,
  prefab palette, live validation, autosave, undo/redo and polished non-technical UX.

The 8–14 day MVP is additional to the 73–111 day story-slice estimate, making the combined
story-plus-editor planning range **81–125 focused days**. The optional post-slice 3D polish
is not included in that total.

**Gate:** a non-Unity user can move a coastline, paint elevations/biomes, redraw a road,
place a city gate and story spawn, press one Build/Preview button, and receive a valid
Kessil world without editing C# or opening the Unity Editor. Existing stable ids and saves
survive geometry changes, and invalid water/road/spawn configurations are rejected with
plain-language errors.

## Art direction — locked 2026-07-29

**North star: Morrowind. Realistic execution bar: Dread Delusion.** Those are not in
tension — Dread Delusion is Morrowind's art direction reproduced by a team of about three,
which makes it the existence proof that this direction survives being made by almost
nobody.

Implemented in `Assets/Scripts/World/ArtDirection.cs`, applied via
**Kessil → Art Direction**. It is the render-layer counterpart to `WorldLayout`: geometry
in one file, look in the other.

### What is being targeted, and what is not

| Aspect of Morrowind | Target | Why |
|---|---|---|
| Poly budgets, texture res, lighting model | **Exceed comfortably** | Its characters ran 5–10k tris on 2002 hardware; this is free now |
| Distinctive regional silhouettes | **Match** | A design decision, not an art skill |
| Regional identity — Halbrand vs Sarrakh | **Match** | Palette and kit discipline; costs nothing |
| Hand-painted texture craft | **Approximate** | Via a small reused material library, not per-object art |
| Content density | **Explicitly not** | Morrowind hand-placed 316k objects across ~100 man-years |

The density line is the one that matters. Chapter spaces are dense; the 6.8 km bay stays
thin and fog-limited. Do not attempt Morrowind's object count.

### Budgets

| | Target |
|---|---|
| Characters | 2,000–5,000 tris |
| Architecture modules | 200–800 tris |
| Textures | 256² standard, 512² hero |
| Material library | ~25 tileable + 3–4 trim sheets, **total**, reused everywhere |
| Real-time lights | Few; bake or fake the rest |
| Draw distance | 150–300 m, with fog carrying the falloff |

### The rules that hold it together

1. **The palette is not negotiable.** 6–8 colours per region, authored in
   `ArtDirection.Palette` and written onto the world materials. Assets are held to the
   palette; the palette does not adapt to assets. This is what prevents the earlier
   three-visual-languages problem from recurring.
2. **Reuse materials, do not author per-object textures.** Morrowind reused a small texture
   set across its kits far more than people remember, and that is the affordable half of
   its look.
3. **Silhouette over surface detail.** A distinctive shape reads at any fidelity; a detailed
   texture on a generic shape does not.
4. **Fog is an aesthetic, not an apology.** It defines the palette and hides the draw
   distance at the same time.
5. **One humanoid base.** Vary characters by clothing and colour, not by mesh. Hoods,
   helmets and dim interiors mean faces never have to carry a scene.

### Pipeline, by asset type

Generative 3D and procedural generation fail at opposite things, so they are assigned to
opposite categories rather than used interchangeably.

| Asset type | Method | Why |
|---|---|---|
| Modular architecture — prison, palace, ship deck | **Procedural Blender scripts** (`Tools/Blender/`) | Needs exact module sizes that snap; deterministic and git-diffable. AI generation cannot hold dimensions |
| Cultural hero architecture — the Everspire, Sarrakh domes, Estmere's skyline | **AI generation** via blender-mcp (Rodin / Hunyuan3D) | One-off organic shapes with no tiling requirement — generative 3D's strongest category, and the same category Morrowind's most memorable assets fall into |
| Nature, rocks, foliage | Existing CC0 (Quaternius, Poly Haven) | Already solved |
| Characters | One rigged humanoid base + Mixamo/AccuRIG animation | The least-solved area; contain it rather than fight it |
| Materials and textures | Small tileable library, AI-assisted, palette-locked | Consistency beats individual quality |
| **The look layer** | `ArtDirection.cs` — fog, grading, palette, filtering, sky | **Highest ROI.** Code and settings, not art |

The last row is the thesis: for a developer who codes but does not model, the render layer
returns more visual improvement per day than asset work does, and it is the half that can
be iterated in seconds.

### Where the look is enforced

- `ArtDirection.Palette` is written onto the world materials by value, not blended, so
  applying a look repeatedly is idempotent. Post-grading alone could not tame an
  off-palette material: a saturated blue ocean under grey fog still reads as a saturated
  blue ocean. This was found by capturing the comparison, not by reasoning about it.
- `ArtDirection.Grade()` pulls every weather colour toward the palette, so the weather
  system cannot drift outside it.
- `TimeWeatherSystem` scales its authored fog density by the active preset instead of
  owning absolute values, so a look change survives entering Play mode.
- The default procedural skybox is disabled. A flat sky in the fog colour makes the horizon
  dissolve; the bright blue gradient behind a muted world was the single largest reason the
  prototype read as an engine project rather than a game.

### Spike result — 2026-07-29

Both looks were captured from four matched viewpoints
(`Assets/Screenshots/ArtDirection/`, regenerate with
`ArtDirectionTool.CaptureComparison`). **Morrowind Clean is adopted and baked in** —
confirmed 2026-07-29 after reviewing the comparison.

It is enforced three ways, so it cannot drift back:

- `ArtDirection.Current` defaults to it, asserted by `ArtDirectionTests`.
- `ArtDirectionTests` rejects any palette colour outside the muted range, in either preset.
- `ArtDirectionTool.ApplyAndRebuild` is the only sanctioned way to change look, because a
  preset that is applied without regenerating leaves the old palette baked into the terrain.

A comparison run necessarily ends in whichever preset it captured last. Restore the lock
afterwards with `ArtDirectionTool.LockMorrowindClean`.

PS1 Crunch was rejected for a concrete reason rather than taste: point filtering and a 0.55
render scale only pay off against genuinely low-resolution textures. The project's art is
1–2K PBR from Poly Haven and Quaternius, so the crunch bought aliasing without buying the
chunky-texel read, and softened the building silhouettes. Committing to PS1 would mean
re-authoring every texture at 64–128 px — more work, not less. The preset is kept in
`ArtDirection.cs` so the comparison can be re-run if the texture library ever changes.

Known off-palette surfaces still to fix, found by the spike:

- The Caldemar spawn pad reads bright yellow: `M_Sand` has a light sand `_BaseMap` that
  overpowers the palette tint. Needs a darker texture or no texture.
- Kenney NPCs remain saturated toybox characters against a muted world — the clearest
  argument for the one-humanoid-base rule above.
- Ground texture tiling is far too large (roughly 2 m cobbles). A UV-scale bug, not an art
  direction issue, but very visible.

### Slice implications

Art direction is a VS0 deliverable, not a VS8 one — it determines what every later asset is
built against. What belongs to the slice is the palette lock, the material library, the
look preset and a critical path that reads without commentary. Final lighting passes, hero
modelling and per-object texture art belong to the release-candidate tier.

## Content and asset checklist

Existing source assets reduce the modelling burden: the repository already contains
multiple ship/canoe models, cave pieces, a large modular castle/wall/door/tower/crypt set,
furniture, human variants, and a broad OGG sound library. They still require selection,
conditioning, materials, collision, LODs, prefabs, and license records.

**For the slice, read this table as a list of what must *exist and function*, not what must
look finished.** Every row must be present and playable; only the named speaking roles and
the critical-path silhouettes need to read clearly. LODs, hero-piece modelling and bespoke
props belong to the release-candidate tier, not here.

| Story location/beat | Required content |
|---|---|
| Merchant ship | intact and damaged variants, deck collision, rigging/cargo, passengers, crew, rescue ship |
| Bay event | distant Tower, Ivory Concord silhouettes, pulse VFX, shockwave, debris, water entry, underwater/blackout transition |
| Estmere docks | harbor approach, survivor triage, guards, civilians, processing stations, character-creator backdrop |
| First palace visit | gate-to-throne route, throne room, King, court, guards, missing-prince visual references |
| Warrior route | guard yard, weapons, targets, patrol/hunt terrain, hostile encounter, secret transport |
| Mage route | guild hall, training room, spell targets, soul-crystal props, service corridor, sealed-cell mechanism |
| Commerce/Thief route | steerable boat, dock lesson space, stealth route, pockets/loot, locks, secured tower |
| Prison | general population, solitary, restricted wing, guard posts, evidence, black soul crystals, harvesting machinery |
| Escape | alternate cover/doors where appropriate, guard response, prince navigation, sea cave, reveal vista |
| Confrontation | evidence presentation, throne variants, King removal, successor, post-coup guards/prisoners/banners |
| Caldemar handoff | arrival landmark, travel transition, Council representatives/setup, next-quest framing |

Named speaking roles needed at minimum are the King, prince, rescue captain or sailor,
processing guard, one route instructor per branch, two or more prisoners, confrontation
witnesses/allies, successor if not the prince, and a Caldemar/Council contact. Names can
remain temporary, but stable role IDs, casting requirements, dialogue ownership, and
animation needs cannot.

## Dependencies and critical path

1. Story graph, IDs, and outcome locks precede final dialogue and save schema.
2. Bootstrap, scene loading, `GameState`, interactions, quest stages, and Save V4 precede
   branch authoring.
3. Character profile precedes survivor processing and every later player spawn.
4. Detection/locks/pickpocket/sailing precede the Commerce/Thief route.
5. Dialogue conditions, evidence, doors, companion AI, and navigation precede convergence
   and escape.
6. World mutation and outcome persistence precede confrontation and Caldemar gating.
7. Final lighting, audio, animation, and environment dressing follow greybox route lock,
   but representative assets must be proven early in each location.

The Commerce/Thief route is the largest mechanic risk; VS4 exists to prove its sailing and
stealth loops before VS5 authors content against them. The companion and save-state tests
are the largest progression risk and should stay continuously green from their first
implementation onward.

The ordering constraint that matters most: **VS2's grey thread must complete before any
milestone that authors content.** Its purpose is to prove the chapter's shape holds before
effort is sunk into scenes that a structural problem would invalidate.

## Required QA matrix

| Test family | Required coverage |
|---|---|
| Four routes | Warrior, Mage, Commerce/Thief, None from New Game to Caldemar handoff |
| Assignment | every background/declared-skill mapping, refusal, invalid/default selection |
| Route recovery | failure, arrest, death, checkpoint, objective retry, accidental area exit |
| Convergence | correct prince state, route evidence, dialogue variant, inventory, quest stage |
| Cinematics | watch and skip shipwreck, audiences, reveal, title, confrontation, transition |
| Saves | before/after every scene boundary, mid-route, mid-escape, pre/post-coup, corrupt/old save |
| Companion | blocked path, combat, wait, teleport recovery, scene load, save/load, death prevention |
| Outcomes | every supported King result and successor result, including reload and re-entry |
| World mutation | ruler/NPC/banner/door/prison/dialogue/journal state cannot regress or duplicate |
| Input/UI | keyboard/mouse, common aspect ratios, pause/dialogue/loading ownership (controller is out of slice) |
| Geometry | doors, stairs, cells, ships, walls, cave, docks, navigation, no fall-through or softlocks |
| Performance | ship destruction, docks crowd, city, palace, prison, VFX, Caldemar arrival |
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

No third-party game's meshes, textures, audio, music, writing, logos, names, or UI art
belong in the deliverable, and no third-party game is named in **shipped or distributed
material** — the build, its UI, credits, store text, or marketing. The UI target is described
there by its own terms — restrained hierarchy, carved stone, aged parchment, muted bronze —
not by reference to another title.

**Internal planning documents may name other games as design benchmarks**, because their
design philosophies are the shorthand being used. `plan.md`'s art direction section and
[`Docs/GAMEPLAY_DESIGN.md`](Docs/GAMEPLAY_DESIGN.md) both do so deliberately.

## Naming policy — applied 2026-07-29

The setting was renamed off its original derived names. The project now uses an original
setting throughout: code, scene object names, editor menus, product name, docs, and
`storyline.md`.

| Concept | Name |
|---|---|
| Sea | Kessil Bay |
| North realm (temperate) | Halbrand |
| South realm (arid) | Sarrakh |
| Regions | Kelrith Coast, Karnoth Highlands, Sarrakh Waste, Kiln Hills |
| River | the Esk |
| Cities | Caldemar (`city_west`), Estmere (`city_east`), Qadris (`city_south`) |
| Planned city | Aldreth (`city_north`), Karnoth Highlands — Chapters 02+ only; **not in `WorldLayout` and not built** |
| Islands | Tolm (`isle_west`), Corrath (`isle_center`), Sarn (`isle_south`) |
| Landmark | the Everspire, on Corrath |
| Foreign order | the Ivory Concord |
| Magic institution | the Arcanum |
| Institution | the Crown Council |
| Player resource | Mana |
| Story items | soul crystal, black soul crystal; the practice is soul-binding |
| Generated art kit | `Assets/Art/Generated/SarrakhKit` |

Rules that keep this cheap to revisit:

- **Save-persisted identifiers are setting-neutral.** `WorldLayout.Site.Id` and
  `Landmass.CityId` use keys like `city_west`; display names live in `DisplayName` /
  `CityName`. Code branches on ids only. `WorldLayoutTests` enforces the link by id.
- Scene object names may stay themed — they are regenerated from `WorldLayout`, so they
  cost nothing to change.
- No third-party game is named anywhere in the project, including comments, doc titles,
  menu paths, and working titles. Describe targets by genre, not by competitor.
- Keep crests, logos, and commissioned name-specific art replaceable.
- Still outstanding: the repository directory and `Elder Scrolls 6.sln` (needs Unity
  closed), and a final rights review before any public distribution.

## Studio and release identity

| Field | Value |
|---|---|
| Studio | DataTheCodie Studios |
| Copyright line | `© 2026 DataTheCodie Studios` |
| Contact | hello@datathecodie.com |
| Site | datathecodie.com |
| Unity `companyName` | `DataTheCodie Studios` (set — determines the save folder path) |
| Unity `productName` | `Kessil Bay` |

Applies to the credits/about screen, the packaged build's file properties, and third-party
notices. Two things to know:

- **`companyName` is now load-bearing.** It is part of `Application.persistentDataPath`, so
  changing it again orphans saves. Treat it as frozen from here.
- **The studio's brand accent (`#00d9ff`) does not belong in the game UI.** It clashes with
  the charcoal / silver / muted-bronze palette. Keep studio branding to the splash and
  credits; the in-game HUD keeps its own language.

The existing portfolio is Android and bite-sized; this is a Windows open-world RPG chapter
of 73–111 focused days. That is a deliberate platform and scale change rather than an
extension of current work, and the slice is the cheapest honest test of whether it holds.

## Narrative and production locks still to resolve

These choices fill implementation gaps left open by the outline; they do not change its
scope.

**Resolved 2026-07-29** and recorded in [`Docs/CHAPTER01_BEATS.md`](Docs/CHAPTER01_BEATS.md):

- **King's fate — a player choice**, kill or imprison. Costs a second authored outcome and
  doubles the route matrix to eight end-to-end runs; VS7 and VS8 are re-estimated above to
  absorb it. The beat sheet caps what may differ between branches, which is what keeps the
  doubling affordable.
- **Successor — the prince is crowned.** No new character arrives late in the chapter; the
  cost is that the prince cannot accompany the player to Caldemar.
- **Player title — Crown Envoy.** Neutral about the ruler and still meaningful in the next
  chapter.
- **Character creation — moderate, 3–4 ancestries**, implemented as head/skin/hair variants
  on **one shared body and rig**. Distinct per-ancestry meshes are out of scope; that is
  what reconciles this with the one-humanoid-base rule in the art direction lock.
- **Evidence set — defined.** One unique item per route plus two shared, so the
  confrontation always has at least three.

**Resolved 2026-08-01** and recorded in [`Docs/CHAPTER01_BEATS.md`](Docs/CHAPTER01_BEATS.md)
under “Premises”. These were the two locks blocking the screenplay, plus three that were
due before VS3:

- **Rescue and audience — the rescue ship is the King's own**, out searching the route his
  son's ship took. Estmere is its home port and the nearest safe harbour. He wants the
  player because his own crew pulled them from a wreck on that route. The idle-persons law
  is retained as the legal frame that converts a witness into a conscript at B130, so B100's
  edict is still required. The King is **not** staged on-screen during the rescue: B040's
  blackout covers it, so character creation stays at the docks and no shipboard scene is
  needed.
- **Cast — House Selwyn.** King Osric Selwyn and Prince Terrin Selwyn. Six recurring roles
  named; the rescue captain and processing guard stay as titles by decision.
- **The King is a true believer.** Soul-binding is what he thinks feeds and defends Estmere.
  B720 must give him a real argument, which is what makes B730 a choice rather than an
  execution.
- **The prince is a competent reformer**, intercepted before he could bring his alternative
  home. B740 therefore leaves a stable settlement.
- **Everspire pulse — cause unexplained and deferred to later chapters; effect authored.**
  It disrupted the memory of everyone who was on the water, scaled by proximity. This is the
  only rule VS3 needs; wider scope would be a main-story commitment this chapter has not
  made.
- **Ancestries — four, one per region** (Kelrith Coast, Karnoth Highlands, Sarrakh, isle-born
  from Tolm or Sarn), appearance and origin only.
- **`route.refuse` target — 15 minutes.** Aggressive; see the risk note in the beat sheet.

Also closed 2026-08-01: **tutorial failure rules and gear carryover** (no failure is terminal;
all four routes enter B600 unarmed, so B630 is authored once) and **ancestry starting values**
(mapped onto the three pools that exist in `PlayerRpg.cs`, every ancestry totalling 280).

Still open:

1. Subtitle standard and, after the slice, localization scope. *(Protagonist voicing is
   resolved: silent, subtitles only, permanently.)*
2. Frame-rate and memory floor at the five stress locations. *(Platform is resolved:
   Windows player, keyboard and mouse.)*

**Every Chapter 01 narrative and production lock is now closed.** Both remaining items are
VS8 measurements rather than decisions, and neither blocks any earlier milestone.

The cave-exit title position and all four route contents are already locked by this plan.
Setting names are locked by the naming policy above, and character names were locked on
2026-08-01. No naming work remains open.

## Progress ledger

Update this document at every milestone with:

1. the milestone reached (VS0–VS8) and whether its gate passed;
2. completed story beat IDs and scenes, against the VS0 beat list;
3. the four-route/outcome test result and blocker count;
4. save/schema version and migration coverage;
5. outstanding characters, environments, props, animation, VFX, audio, UI, and dialogue;
6. external playtest completion time and confusion/softlock reports;
7. frame time, draw calls, memory, and loading time at the five stress locations;
8. new assets, source/license proof, and Blender time saved versus manual cleanup.

The single number worth tracking after VS2: **beats replaced with real content, out of the
VS0 beat total.** Before VS2 that number is meaningless, because the structure holding the
beats is not yet proven.

Current conclusion: **the complete story is viable as a playable slice, and the grey thread
is now complete.** VS0's contracts, VS1's technical spine and VS2's four-route traversal are
green. Visible screenplay, actors, route mechanics and final environments remain VS3-VS7
work. The Map Editor MVP is next. The delivered VS2 director walks `storyline.md` to B830.
