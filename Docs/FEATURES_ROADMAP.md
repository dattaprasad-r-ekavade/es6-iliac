# Ratna Bay — features roadmap

An open-world first-person RPG set on **Ratna Bay**, between temperate Uttara and arid Maru.
Magic draws prana from lawful dāna stones while black jiva-binding cages unwilling people;
the locked contract is [`JIVA_METAPHYSICS.md`](JIVA_METAPHYSICS.md). The look is Arena
Miniature: Arena-budget geometry through Rajput/Pahari flat pigment and contour.

Use this doc to track the active delivery goals. `plan.md` owns estimates and gates;
`Docs/CHAPTER01_BEATS.md` owns the 42-beat story contract.

---

## Active goal: complete Chapter 01

**Delivery update (2026-08-12):** VS2 remains complete. The grey thread traverses all **42/42** beat
waypoints through the extracted Ratnapur exterior, 11 regenerable Chapter 01 rooms, the
clickable Raja's audience assignment panel, all four branches, convergence, B640 title crawl,
aftermath and Sabhapur Sabha handoff. It records typed outcomes/evidence and autosaves route
checkpoints. The P1 runtime repair, adopted-setting migration, Indic content guard, one-page
jiva contract and Arena Miniature proof captures/automated checks are complete. The external
Ratna World Builder MVP, its one-button Unity preview and the baseline Shantipur city/road
expansion are complete too. Human walkthrough/performance gates remain explicitly open.

The active deliverable is a content-complete, quality-reduced vertical slice in which the
entire `storyline.md` opening is playable on all four routes through the Sabha
handoff. The story is not being shortened. Free-roam expansion remains parked while the
Map Editor and authored Chapter 01 environments are built.

| Priority | Milestone | Status | Goal / gate |
|---:|---|---|---|
| 1 | **VS0 — story package and regression baseline** | Complete except deferred screenplay | preserve the locked story contracts and baseline |
| 2 | **VS1 — persistent technical spine** | **Complete — W-01–W-09 gate passed** | preserve the technical-spine regression suite |
| 3 | **VS2 — rough complete story** | **Complete — 42/42 grey beats, four routes reach B830** | preserve the grey scene/route contract |
| 4 | **Stabilisation + Arena Miniature proof slice** | **Complete: code/content guard, captures and automated tests** | preserve P1 flows, the jiva contract and deterministic proof builds; manual walk/performance acceptance remains below |
| 5 | **Ratna World Builder MVP** | **Complete: standalone editor, one-button Unity preview, 14/14 Python tests** | preserve validation, backups, undo/redo, PNG/SVG output and headless production preview |
| 6 | **Shantipur baseline expansion** | **Complete: `city_north`, highland footprint and connecting road** | preserve stable id, dry arrival, collision and road contracts |
| 7 | **W-12 dense region rebuild** | Not started | one dense Ratnapur region proves the region architecture before wider propagation |
| 8 | **VS3 — real opening** | Not started | ship through the four audience assignments is real content |
| 9 | **VS4 — reusable route mechanics** | System layer complete; authored use pending | combat/magic hooks, sailing, stealth, locks, pickpocket and companion systems proven in content |
| 10 | **VS5 — four real routes** | Not started | four authored routes satisfy the shared convergence contract |
| 11 | **VS6 — prison and escape** | Not started | Arun reveal, evidence, escort and one-time cave title moment complete |
| 12 | **VS7 — ending and handoff** | Not started | both Vikram outcomes, coronation, Rajdoot title and Sabhapur handoff persist |
| 13 | **VS8 — polish and package** | Not started | eight route/outcome runs, packaged build and blind playtests pass |
| 14 | **Advanced map-editor/region polish** | Optional after the slice | free-form heightmap, runtime marker importer, direct 3D sculpt/prefab UX |

### VS0 remaining checklist

- [x] 42 beats with stable ids, scenes, dependencies, exit states and acceptance tests
- [x] Route, flag, evidence and cast-role registries
- [x] Four-route convergence contract
- [x] Vikram kill/imprison outcome matrix; Arun successor; Rajdoot title
- [x] Character-creator production ceiling: 3–4 ancestries, one shared body/rig
- [x] Name and characterize the principal cast
- [x] Explain why the rescue ship chooses Ratnapur and why survivors receive a royal audience
- [ ] Write the screenplay/dialogue pass against beat ids
- [x] Snapshot the current compile, tests, scene, saves and packaged player
- [x] Complete the asset/source/license ledger

### Work intentionally parked until VS2

- New free-roam settlements, dungeons, radiant jobs, crafting, housing and marriage
- Broad world-density passes outside the Chapter 01 critical path
- Horses or unrestricted global sailing
- Additional generic quests beyond regression maintenance
- Controller/localization/final accessibility work beyond the slice cut-line

### Ratna World Builder MVP — complete

VS1 moved the world definition into versioned `kessil.world.json`; W-11 now supplies a
standalone Python/Tk editor that requires neither Unity interaction nor a pip install. It edits
the current elliptical landmass vocabulary, elevation/relief parameters, biomes, roads, sites,
city gates and story-spawn metadata; validates before save; creates backups; supports undo/redo;
and exports PNG/SVG previews. Its **Unity Preview** button invokes the production generator
headlessly and captures top-down and approach views. All 14 Python tests pass.

Start with `Tools/WorldBuilder/Launch World Builder.cmd`; full instructions are in
[`Tools/WorldBuilder/README.md`](../Tools/WorldBuilder/README.md). The approved external preview
is [`Screenshots/world-builder-preview.png`](Screenshots/world-builder-preview.png).

The MVP is not a free-form terrain sculptor. Free-form coast/heightmap authoring and importing
gate/story-spawn metadata into runtime placement remain follow-on work. The next world milestone
is W-12: rebuild Ratnapur as one dense 2 km-scale region, measure it, then decide how far to
propagate the architecture.

## Long-term product north star

The genre targets this project is built against:

| Genre pillar | Our target |
|---|---|
| Huge walkable world | Ratna Bay at travel scale (cities kilometres apart) |
| Discoverable map + fast travel | Fog-of-war map, markers, carriage / discovered FT |
| Living weather & time | Day/night + regional weather (Uttara rain, Maru clear/dust) |
| Dense nature | Proper trees/foliage, not floaty props |
| Cities that feel inhabited | Districts, NPCs, services, names |
| Adventure loop | Quests, combat, loot, leveling, radiant jobs |

---

## Current baseline (as of 2026-08-12)

### Done
- [x] Unity 6 + URP project, MCP agent workflow
- [x] Ratna Bay setting migration applied to player-facing product, world, story, scenes and docs;
      retained `Kessil*`/file/internal ids are explicitly documented codenames
- [x] Locked Arena Miniature art direction with automated drift tests and two proof captures
- [x] Chapter 01 decomposed into 42 stable beats with route/convergence/outcome contracts
- [x] Compile check passes for 18 Editor, 74 Runtime and 11 test source files (2026-08-12)
- [x] Release EditMode run: 120/120 passed (2026-08-12)
- [x] Latest full PlayMode run: 130/130 passed, including all four VS2 routes through the
      Sabhapur handoff (2026-08-12)
- [x] Ratna World Builder Python suite: 14/14 passed (2026-08-12)
- [x] W-01: Bootstrap scene, additive transition service, stable spawns, fades, recovery,
      exterior snapshot and three-scene fixtures
- [x] W-02: one `GameStateService` owns pause, cursor and gameplay-input policy across
      menu, cinematic, gameplay, dialogue, loading and death
- [x] W-03: one keyboard/mouse Input System asset; every current consumer uses named actions
      through `GameInput`; direct device polling removed
- [x] W-04: Player, GameSystems, NPC and full HUD visual prefabs; matching component files;
      five site-relative NPC archetype ScriptableObjects; no root-construction fallbacks
- [x] W-05: atomic SaveGameV4, backup, validation, injectable paths, v3 migration and full
      story/profile/scene/spawn/companion/equipment/skill state
- [x] W-06: topic-based shared dialogue knowledge with route, flag, faction, disposition,
      location, channeled and evidence-count conditions
- [x] W-07: StoryDirector, route/beat/flag authority, readable evidence documents,
      dialogue-choice records and authored quest definitions
- [x] W-08: deterministic CinematicRunner whose skipped and watched end states are identical
- [x] W-09: three-scene branch/evidence/save/quit/continue/companion/mutation/rollback gate
- [x] Versioned `kessil.world.json` is the runtime source for dimensions, anchors,
      landmasses, sites and roads; the existing map geometry remains regression-identical
- [x] Current packaged Windows build: 142.5 MB, Bootstrap scene zero, 0 errors
- [x] VS2 captures: `Screenshots/vs2-estmere-palace.png` and
      `Screenshots/vs2-caldemar-arrival.png` (legacy filenames, current Ratnapur/Sabhapur views)
- [x] Arena proof captures: `Screenshots/arena-miniature-ratnapur-street.png` and
      `Screenshots/arena-miniature-prison.png`
- [x] Ratna Bay landmass layout (Uttara north / Maru south / bay / islands)
- [x] Cities: **Sabhapur**, **Ratnapur**, **Marukot**, plus Shantipur's baseline highland
      district/road (district streets, walls, gates, docks and signs where applicable)
- [x] Cities ~**4 km** apart (not “one jump”)
- [x] Player WASD + mouse look + sprint (Shift) + jump
- [x] Menu → intro dialogue → scenic flyover → gameplay
- [x] **Skip dialogue** (Space / Enter / Esc / Tab / RMB / SKIP button)
- [x] Kenney Nature / Castle / Fantasy Town / Pirate kits (CC0)
- [x] Flat box land colliders (fixed capsule-slide void falls)
- [x] Blender 4.5 LTS + Blender MCP scaffolding (`Tools/BlenderMCP/`)

### Known gaps / pain points
- [x] Chapter 01 quest/dialogue/save/cinematic architecture passes the VS1 gate
- [x] VS2 grey thread: all 42/42 beat waypoints across 11 regenerable Chapter 01 rooms plus
      `Capital_Exterior`, audience assignment UI, B640 title crawl, additive transitions,
      evidence/companion/typed outcome state, route autosaves and B830 handoff
- [x] Persistent Bootstrap/additive foundation exists; generated `Main` uses linked runtime
      prefabs and remains a temporary legacy gameplay container until the story scene split
- [x] Trees ground-snapped + distance cull (biome pools) — further art polish still welcome
- [x] World map UI (M) + discovery fog list
- [x] Fast travel to discovered markers
- [x] Weather / day–night cycle (regional)
- [ ] Cities still read as kitbash rather than authored density (P2 art)
- [x] NPCs, dialogue (E), combat, inventory, saves (vertical slice)
- [x] Quaternius Medieval Village imported (`Assets/ThirdParty/Quaternius/MedievalVillage`)
- [x] Player, friendly NPC, systems and the complete HUD visual hierarchy are regenerable
      prefabs; NPC placement/dialogue/roles are inspectable ScriptableObject data
- [ ] Cities are only reachable via the causeway roads; the landmasses are separate
      islands with 180–1000 m water gaps between them
- [ ] HUD is now prefabbed but still uses legacy uGUI `Text` (no TextMeshPro or scrolling lists)

> **2026-07-26 hardening pass.** The P1 slice was not actually playable as described:
> `SnapToWalkable` ignored its argument and returned the Sabhapur spawn pad, so every
> NPC and enemy spawned in one pile on the start plaza. The bandit camp and coastal ruin
> were also authored in open water. Both are fixed — see [plan.md](../plan.md).

Layout notes: `Assets/Scripts/World/KESSIL_LAYOUT.md`  
Assets: `Assets/ThirdParty/ATTRIBUTION.md`

---

## Legacy prototype backlog — parked during Chapter 01

The checklists below describe the pre-story sandbox. They remain useful history and
maintenance context, but they are **not the active execution order**. New work comes from
VS0–VS8 above unless it fixes a regression in an existing system.

Names such as Caldemar, Estmere, Qadris, Aldreth, Kessil Bay, Halbrand, Sarrakh, Arcanum and
Mana in this legacy block are contemporaneous historical labels. The active equivalents are
Sabhapur, Ratnapur, Marukot, Shantipur, Ratna Bay, Uttara, Maru, Siddha Order and prana.

### Former P0 — foundation polish

#### 1. Fix trees / foliage
- [x] Ground-snap trees with raycast onto land colliders
- [x] Biome rules (Halbrand trees / Sarrakh desert / islands rocks)
- [x] Keep clear of city radii
- [x] Distance culling (`FoliageDistanceCull`)
- [ ] Optional: grass / detail meshes near player only
- [ ] Optional Blender pass for better silhouettes

#### 2. World map
- [x] M opens map UI with named markers
- [x] Player-relative distances + fog (undiscovered hidden)
- [x] Select + Enter/F to fast travel when unlocked
- [ ] Hand-painted / RenderTexture silhouette map art
- [ ] Zoom + pan on a drawn map image

#### 3. Weather (match regions)
- [x] Time-of-day cycle (sun angle, fog, intensity)
- [x] Weather states: Clear, Cloudy, Rain, Storm, Fog, Dust
- [x] Regional defaults (Halbrand wetter, Sarrakh dust, Bay fog)
- [x] Rain / dust particles
- [ ] Wind affecting trees / flags
- [ ] Audio beds per weather

#### 4. Fast travel
- [x] Discover locations on proximity
- [x] Fast travel only to discovered markers
- [x] Time skip on travel + weather reroll
- [x] Block FT in combat
- [x] Fade transition + spawn pads
- [ ] Carriage / boat NPC flavour

---

### Former P1 — core loop prototype landed 2026-07-26

#### Exploration & world
- [x] Roads between Caldemar↔Estmere + bandit road
- [x] POIs: bandit camp + coastal ruin
- [ ] Heightmap / sculpted terrain
- [ ] More settlements / interiors
- [ ] Horses / boat travel

#### Character & progression
- [x] HP / Mana / Stamina + level/XP
- [x] Inventory + potions
- [x] Wait / sleep (T) restores vitals
- [ ] Full character creation / perks tree

#### Combat
- [x] Melee (LMB/1) + Flare magic (2)
- [x] Bandit / skeleton AI
- [x] Loot + XP on kill
- [ ] Bow / block / stagger polish

#### NPCs & dialogue
- [x] Named NPCs (merchant, guard, quest giver, city greeters)
- [x] E to talk + toast/dialogue panel
- [ ] Schedules / radiant job board UI

#### Quests & journal
- [x] Journal (J) with 3 starter quests
- [x] Main / bounty / ruin discovery hooks
- [ ] Longer quest spine + map quest icons

#### UI / UX
- [x] Compass bar, vitals bars, status line
- [x] Map / Journal / Inventory / Wait panels
- [x] Save (F5) / Load (F9)
- [ ] Tabbed inventory art pass / settings menu

#### Audio
- [ ] Footsteps / ambient / combat / music (P2)

---

### Former P2 — depth and fidelity (parked)

- [ ] Interiors (inns, keeps, shops) with load doors or seamless
- [ ] Lockpicking / pickpocket / crime & bounty
- [ ] Crafting: smithing, alchemy, enchanting (simplified)
- [ ] Survival-lite options (hunger/cold — optional toggle)
- [ ] Followers
- [ ] Marriage / house purchase
- [ ] A regional apex threat (original creature fantasy)
- [ ] Mod-friendly folder layout / ScriptableObjects for data
- [ ] Performance: streaming, occlusion, GPU instancing for forests
- [ ] Controllers / gamepad full support
- [ ] Build players (Windows first)

---

## Superseded prototype milestones

These M1–M7 milestones predate the Chapter 01 retarget. They are retained as historical
context; VS0–VS8 are now authoritative.

| Milestone | Theme | Exit criteria |
|---|---|---|
| **M1** | Trees + weather + time | Biome foliage looks grounded; day/night + 3 weather types |
| **M2** | Map + fast travel | M opens map; discover cities; FT between Caldemar / Estmere / Qadris |
| **M3** | Compass + HUD + save | Vitals/compass HUD; save/load works |
| **M4** | Combat vertical slice | Kill bandits outside Caldemar; loot; level once |
| **M5** | City life | Merchants + 1 inn interior + 3 dialogue NPCs |
| **M6** | Quest spine | Journal + 5 quests + map markers |
| **M7** | Bay expansion | Roads, 2 dungeons, boat FT, denser assets via Blender |

---

## Map system — design sketch

```
[M] → MapPanel
        ├── Background: stylized Kessil Bay (RenderTexture top-down or hand-painted)
        ├── FogMask: reveal by discovered RegionId
        ├── Markers: City | Landmark | Quest | Custom
        ├── PlayerIcon: world→map projection
        └── TravelButton: enabled if marker.Discovered && !InCombat
                 → Fade → teleport to SpawnPoint → advance TimeController
```

**Projection:** store each marker’s `Vector3 worldPos`; map UI uses normalized bounds of the generated world AABB (from land patches).

**Discovery:** `LocationDiscoverable` trigger on gate/plaza; writes to `PlayerDiscoveryState` (saved).

---

## Weather system — design sketch

```
TimeController (minutes per real second)
    ↓
WeatherController (region from player XZ biome)
    ↓
├── Visual: sky, fog density, light color/intensity
├── FX: rain/dust particles
├── Audio: loop beds
└── Gameplay hooks: bow accuracy in wind (later), NPC seek shelter (later)
```

Regions map to existing land patch biomes: `Halbrand`, `Sarrakh`, `IslandGreen`, `IslandRock`, plus `Ocean`.

---

## Fast travel — design sketch

Rules:
1. Must have discovered the destination.
2. Cannot FT from combat / mid-air / overcrowded interiors (later).
3. FT advances time (e.g. 1 hour per 500 m).
4. Chance to roll new weather on arrival.
5. Spawn on named `SpawnPad_<Location>` or gate interior pad.

---

## Asset pipeline (ongoing)

| Need | Approach |
|---|---|
| Better trees | Kenney upgrade in Blender; Poly Haven / Quaternius CC0 |
| Medieval cities | Quaternius Medieval Village MegaKit (manual itch download) → `Assets/ThirdParty/Quaternius/` |
| Bespoke architecture kits | Blender MCP edit passes; generated kits under `Assets/Art/Generated/` |
| Textures / trim sheets | Krita / Substance; URP Lit |

---

## Explicitly out of scope / legal

- No third-party game meshes, textures, voices, music, or quest text
- No third-party game branding anywhere in the project, including working titles,
  menu paths, folder names and comments
- Original setting, place names, factions and writing; see the naming policy in
  [plan.md](../plan.md)
- Keep attribution for CC0 packs (`Assets/ThirdParty/ATTRIBUTION.md`)

---

## Open questions

Resolved for the slice: additive authored scenes, first-person, single-player, Windows,
keyboard/mouse, silent protagonist, subtitles, and the locked Arena Miniature art direction.

Still open:

1. Full W-12 dense Ratnapur region rebuild and the decision to propagate it wider.
2. Free-form heightmap/coast authoring beyond the current ellipse/elevation parameters.
3. Runtime importer for World Builder gate and story-spawn metadata.
4. Real-controller dock-street/prison walkthrough and the minimum-machine 45+ FPS proof.
5. Subtitle standard and slice frame-time/memory floors at the stress locations.

---

## Quick commands (Unity)

| Action | Menu |
|---|---|
| Full rebuild menu + world | **Kessil → Presentation → Setup Menu + Cutscene + Smooth Map** |
| External world editor | `Tools/WorldBuilder/Launch World Builder.cmd` |
| External validation/tests | `python Tools/WorldBuilder/world_builder.py --validate`; `python -m unittest discover -s Tools/WorldBuilder/tests -v` |
| Map layout notes | `Assets/Scripts/World/KESSIL_LAYOUT.md` |
| Blender MCP notes | `Tools/BlenderMCP/README.md` |

---

## Session log (append as you go)

| Date | Done | Next |
|---|---|---|
| 2026-07-24 | Large cities, skip dialogue, Pirate kit, roadmap created | M1: trees + weather + time |
| 2026-07-26 | **P0+P1 vertical slice shipped** — trees ground-snap + cull; time/weather; map+FT; combat; NPCs; quests; HUD; save/load; roads/POIs | P2 interiors / denser assets |
| 2026-07-26 | **Prototype hardening** — git + LFS; `WorldLayout` single source of truth; physics layers replace name-matching; spawn-pile bug fixed; POIs moved onto land; causeway roads; foliage culling collapsed to one system; save versioning | Prefabs for player/NPC/systems; texture budget; tests |
| 2026-08-01 | Scope synchronized around the complete 42-beat Chapter 01 slice; VS0 beat graph complete; compile check green; legacy free-roam backlog parked | Finish VS0 screenplay, blocking locks, regression snapshot and asset ledger |
| 2026-08-01 | **VS1 W-01 complete** — Bootstrap, additive A→B→C transitions, stable spawns, rollback, exterior snapshot, 18/18 PlayMode, 142.4 MB build | W-02: central `GameState` service |
| 2026-08-01 | **VS1 W-02 complete** — centralized state/time/cursor/input ownership, guarded loading restoration, Bootstrap boot proof, 22/22 PlayMode, clean build | W-03: Input System actions asset |
| 2026-08-01 | **VS1 W-03 complete** — one action asset, six consumers migrated, no direct polling, 31/31 EditMode and 22/22 PlayMode | W-04: prefabbed runtime and gameplay data assets |
| 2026-08-01 | **VS1 W-04 started** — regenerable Player/GameSystems prefabs linked into Main; prefab contract tests; 33/33 EditMode, 22/22 PlayMode, 142.0 MB build | Split shared-file components, then HUD/NPC/data prefabs |
| 2026-08-01 | **VS1 W-04 complete** — four runtime prefabs, five NPC ScriptableObjects, matching component files, no root fallbacks; 35/35 EditMode, 22/22 PlayMode, 142.1 MB build | W-05: SaveGameV4 |
| 2026-08-01 | **VS1 complete (W-05–W-09 + world JSON)** — SaveGameV4, topic dialogue, StoryDirector, authored quests/evidence, CinematicRunner, consequence proof and versioned world source; 38/38 EditMode, 29/29 PlayMode, 142.1 MB build | VS2: all 42 beats as a four-route grey thread |
| 2026-08-01 | **VS2 complete** — 42/42 beat waypoints, clickable audience assignment UI, B640 title crawl, four routes to B830, typed outcomes/evidence, route autosaves, player-preserving additive unload, 45/45 EditMode, 30/30 PlayMode, screenshot captures | W-11: external Map Editor MVP |
| 2026-08-12 | **Stabilisation and world-authoring gate complete** — P1 flow defects fixed; Indic content guard and jiva contract locked; Arena street/prison captures and automated checks complete; Ratna World Builder MVP with one-button Unity preview and 14/14 Python tests; Shantipur baseline city/road added | W-12 dense Ratnapur region; free-form terrain and marker importer; manual controller/45+ FPS acceptance |
| | | |
