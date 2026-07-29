# Kessil Bay — features roadmap

An open-world first-person RPG set on **Kessil Bay**, between the temperate north
(Halbrand) and the arid south (Sarrakh). Original setting, writing and layout; art is
CC0 / licensed packs plus generated kits, tracked in `Assets/ThirdParty/ATTRIBUTION.md`.

Use this doc to plan future work. Check items off as they land.

---

## North star

The genre targets this project is built against:

| Genre pillar | Our target |
|---|---|
| Huge walkable world | Kessil Bay at travel scale (cities kilometres apart) |
| Discoverable map + fast travel | Fog-of-war map, markers, carriage / discovered FT |
| Living weather & time | Day/night + regional weather (Halbrand rain, Sarrakh Waste clear/dust) |
| Dense nature | Proper trees/foliage, not floaty props |
| Cities that feel inhabited | Districts, NPCs, services, names |
| Adventure loop | Quests, combat, loot, leveling, radiant jobs |

---

## Current baseline (as of 2026-07)

### Done
- [x] Unity 6 + URP project, MCP agent workflow
- [x] Kessil Bay landmass layout (Halbrand N / Sarrakh S / bay / islands)
- [x] Cities: **Caldemar**, **Estmere**, **Qadris** (district streets, walls, gates, docks, name signs)
- [x] Cities ~**4 km** apart (not “one jump”)
- [x] Player WASD + mouse look + sprint (Shift) + jump
- [x] Menu → intro dialogue → scenic flyover → gameplay
- [x] **Skip dialogue** (Space / Enter / Esc / Tab / RMB / SKIP button)
- [x] Kenney Nature / Castle / Fantasy Town / Pirate kits (CC0)
- [x] Flat box land colliders (fixed capsule-slide void falls)
- [x] Blender 4.5 LTS + Blender MCP scaffolding (`Tools/BlenderMCP/`)

### Known gaps / pain points
- [x] Trees ground-snapped + distance cull (biome pools) — further art polish still welcome
- [x] World map UI (M) + discovery fog list
- [x] Fast travel to discovered markers
- [x] Weather / day–night cycle (regional)
- [ ] Cities still read as kitbash rather than authored density (P2 art)
- [x] NPCs, dialogue (E), combat, inventory, saves (vertical slice)
- [x] Quaternius Medieval Village imported (`Assets/ThirdParty/Quaternius/MedievalVillage`)
- [ ] **Nothing is a prefab** — player, NPCs, enemies, systems and the whole HUD are
      built by `AddComponent` at runtime, so no value can be tuned without editing C#
- [ ] Cities are only reachable via the causeway roads; the landmasses are separate
      islands with 180–1000 m water gaps between them
- [ ] HUD is code-built legacy uGUI `Text` (no TextMeshPro, no scrolling lists)

> **2026-07-26 hardening pass.** The P1 slice was not actually playable as described:
> `SnapToWalkable` ignored its argument and returned the Caldemar spawn pad, so every
> NPC and enemy spawned in one pile on the start plaza. The bandit camp and coastal ruin
> were also authored in open water. Both are fixed — see [plan.md](../plan.md).

Layout notes: `Assets/Scripts/World/KESSIL_LAYOUT.md`  
Assets: `Assets/ThirdParty/ATTRIBUTION.md`

---

## Priority backlog (plan in this order)

### P0 — Foundation polish (next sessions)

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

### P1 — core loop feature parity — vertical slice landed 2026-07-26

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

### P2 — Depth & fidelity

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

## Suggested milestones

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

1. Seamless open world vs cell streaming (cities as scenes)?
2. First-person only, or a third-person toggle?
3. Magic-heavy vs low-fantasy Kessil Bay tone?
4. Multiplayer? (default **no** — single-player)
5. Target platform beyond Windows editor/player?

---

## Quick commands (Unity)

| Action | Menu |
|---|---|
| Full rebuild menu + world | **Kessil → Presentation → Setup Menu + Cutscene + Smooth Map** |
| Map layout notes | `Assets/Scripts/World/KESSIL_LAYOUT.md` |
| Blender MCP notes | `Tools/BlenderMCP/README.md` |

---

## Session log (append as you go)

| Date | Done | Next |
|---|---|---|
| 2026-07-24 | Large cities, skip dialogue, Pirate kit, roadmap created | M1: trees + weather + time |
| 2026-07-26 | **P0+P1 vertical slice shipped** — trees ground-snap + cull; time/weather; map+FT; combat; NPCs; quests; HUD; save/load; roads/POIs | P2 interiors / denser assets |
| 2026-07-26 | **Prototype hardening** — git + LFS; `WorldLayout` single source of truth; physics layers replace name-matching; spawn-pile bug fixed; POIs moved onto land; causeway roads; foliage culling collapsed to one system; save versioning | Prefabs for player/NPC/systems; texture budget; tests |
| | | |
