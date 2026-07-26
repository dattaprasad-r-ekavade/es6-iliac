# Iliac Bay — a viability experiment

**The question:** in 2026, with AI assistance, can one person build a *decent* 3D game in
the mould of Skyrim — and is that actually viable?

**The game:** an open-world RPG on a landmass deliberately similar to the Iliac Bay —
temperate, rainy, castle-strewn north and an arid south across a shared gulf. Same biome
logic, same city positions, same travel scale. **Everything is renamed.** It reads as a
homage, never as a claim to be ES6.

**Deliverable:** a playable vertical slice (~15–20 min of curated content) plus a ~5 minute
reel cut from it, carried by a real story rather than a feature tour.

This document is the experiment log as much as the plan. The point is to answer the
viability question honestly, including if the answer is "partly".

---

## 1. What this repo already tells us

Two days of AI-assisted work produced a procedural 6.8 km bay, traversal, melee + magic
combat, enemies, NPCs with dialogue, quests + journal, inventory, fog-of-war map, fast
travel, day/night + regional weather, versioned save/load, HUD, and a Windows build.

A hardening pass then added a single source of truth for world data, physics layers,
assembly definitions, edit-mode tests, headless build/test tooling and git + LFS — while
finding bugs that had made the "shipped" slice not actually playable (every NPC and enemy
spawning in one pile; two POIs authored in open water; roads flying through the sky).

**Read so far: the systems half is comfortably viable.** That's the part most people
assume is hard. What follows is designed around the half that isn't.

---

## 2. Where AI helps, and where it doesn't

**Multiplies output** — systems code; reading a codebase to find real bugs; large
mechanical refactors; tooling and automation; data authoring and validation;
**procedural/scripted asset generation** (see §4); documentation that stays honest.

**Still the bottleneck** — hero art and characters; animation quality; *game feel* (camera
damping, attack timing, hit-stop — these need someone playing it repeatedly); level layout
craft; audio mixing taste; knowing when it's good enough.

**Strategy:** AI carries systems, tooling and generated art wholesale. You spend your hours
almost exclusively on **feel, layout and dressing**. The milestones are ordered so that's
where the time actually goes.

---

## 3. Renaming — same geography, different names

Cheap to do, and it's what separates "homage" from "asking for a C&D". Because
`WorldLayout.cs` is now the single source of truth, this is a one-file change plus display
strings — nothing else in the codebase hardcodes a place name.

| Current | Proposed | Character |
|---|---|---|
| Iliac Bay | **the Sundered Gulf** | the shared sea between two peoples |
| High Rock | **Highmoor** | rain, granite, castles, feuding baronies |
| Hammerfell | **Karsand** | dune sea, sandstone cities, sword-saint culture |
| Daggerfall | **Thornhaven** | walled peninsula capital, player start |
| Wayrest | **Rivermeet** | river-mouth trade city |
| Sentinel | **Sunwatch** | desert port under a cliff fortress |
| Betony / Balfiera / Cybiades | **Barrow Isle / the Adamant Isle / Saltmere** | Adamant keeps the ancient-tower homage |

Races/cultures get the same treatment — Breton-analogue and Redguard-analogue peoples with
your own names. *Your call; easy to swap.*

---

## 4. Blender is a real asset pipeline here — proven, not assumed

Blender 4.5 LTS is installed and runs headless. **The MCP server in `.cursor/mcp.json` is
wired to Cursor, not to this session — but it isn't needed.** `blender -b -P script.py`
gives full scripted control, and scripts get committed, versioned and re-run
deterministically, which an interactive MCP session doesn't.

**Proof of concept, built and committed:** `Tools/Blender/make_kit_yoku.py` generates a
7-piece modular desert architecture kit — horseshoe arch, onion dome with finial,
crenellated parapet, pillar, stairs, wall, slit-window wall. 3,964 tris total, every piece
on a 4 m grid with base-centre pivots, uniform bevel, one shared material, exported
Unity-oriented. Contact sheet: `Assets/Screenshots/kit-yoku-preview.png`.

This matters because **the biggest threat to M2 is style incoherence** — free packs from
five artists at five scales with five bevel languages. A generated kit fixes the style in
one place, and regenerating with different parameters is free.

What the Blender pipeline should do:

1. **Asset conditioning (highest value).** Batch-normalise every downloaded asset: metres,
   Y-up, pivot at base centre, material naming, LOD0/1/2 via decimate, FBX to Unity. Hours
   of tedium, fully automatable, and it's what stops mixed free assets looking mismatched.
2. **Modular kits** — the Yoku kit above, plus a Highmoor set (half-timber, steep gables,
   granite bases, buttresses, conical tower roofs).
3. **Terrain heightmaps** — sculpt/erode in Blender, export 16-bit for Unity Terrain.
   Real silhouettes instead of Perlin noise on cylinders.
4. **Rock and cliff kits** — displacement + boolean + decimate. Very cheap, very effective.
5. **Texture atlasing / trim sheets** — bake many materials to one atlas; fewer draw calls
   *and* a unified look.
6. **Creature blockout** for the set-piece (rigging and animation still need a human).

Not: character animation quality, or taste.

---

## 5. Assets — free, no restrictions

Use anything free. Practical notes only:

- **Poly Haven** (CC0, in repo), **Quaternius** (CC0, in repo), **Kenney** (CC0, in repo),
  **Mixamo** animations, **Sonniss GDC** audio bundles, **AmbientCG**, Unity Asset Store
  free section, Fab free tier.
- The one hard line stays: **no Bethesda meshes, textures, audio, music, voice or UI art.**
  That's not a "free asset" question, it's a different category entirely.
- Music must be original / royalty-free / commissioned — never Jeremy Soule's tracks.
- Keep `Assets/ThirdParty/ATTRIBUTION.md` current: it costs nothing and it's your record
  of provenance.
- Don't ship "Skyrim" or "Elder Scrolls" in the build name, title, or thumbnail.

---

## 6. Story as a gameplay system

You have the storyline. What this needs is the **machinery to express it**, so the slice is
carried by narrative rather than being a systems demo with quest markers bolted on.

### The system to build (M3)

- **`StoryFlags`** — a saved blackboard of world facts (`met_the_smith`, `tower_opened`,
  `sided_with_karsand`). Everything else reads and writes this. Already have versioned
  saves to hang it on.
- **`QuestDefinition` (ScriptableObject)** — stages, objectives, completion conditions,
  rewards, map marker, journal text per stage.
- **`DialogueGraph` (ScriptableObject)** — nodes with speaker, line, and *conditions*
  (flag/level/item gates) plus *effects* (set flag, start quest, give item). Branching and
  consequence, not a line pool.
- **`Codex`** — readable letters, inscriptions and books. The cheapest possible worldbuilding
  and the most Bethesda-authentic; also how you deliver backstory without voice acting.
- **Environmental storytelling passes** — a ruined camp that tells you what happened, a
  body with a note. Zero systems cost, huge perceived depth.
- **Timeline-driven beats** for scripted moments, so takes are repeatable.

### Structure the slice around three acts

The reel needs a hook, a turn and a payoff — not a tour.

1. **Hook** (Highmoor, rain) — arrive, something is wrong, a character asks for help.
2. **Turn** (dungeon / discovery) — learn the real stakes; the mystery reframes.
3. **Payoff** (Karsand, gold) — the set-piece confrontation; one meaningful choice.

### What I need from you

Dump the storyline in roughly this shape and I'll build the data and wire it:

- **Logline** — one or two sentences.
- **The world's central conflict**, and where the slice sits inside the larger story
  (recommend: the prologue — a reel wants the hook, not act four).
- **3–5 characters** — name, role, what they want, one line of how they speak.
- **Factions** and what each believes.
- **The player's role** — chosen one, outsider, investigator, exile?
- **The mystery/reveal** the slice turns on.
- **One meaningful choice** and its two outcomes.
- **The set-piece** — what the player fights or confronts at the end.
- Any **names, places, terminology** you want preserved exactly.

Rough is fine — bullet points beat prose. I'll turn it into quests, dialogue graphs, codex
entries and a Timeline beat sheet, and flag where it needs tightening for a 15-minute slice.

---

## 7. The one strategic call: shrink the world

The 6.8 km world with three cities is the biggest single obstacle to looking good — it's
mostly empty. A slice needs **one dense square kilometre per region**, not a province.

- **Highmoor** — rain-soaked valley: stone bridge, castle silhouette, wet cobbles, a town
  you can cross in 90 seconds.
- **Karsand** — dune approach to a sandstone gate: domes, arches, heat haze, hard shadows.
- **One dungeon** — torchlit, three rooms, a trap, a boss chamber.

The wet-granite-to-gold-sand cut *is the pitch*, and it's the real reason to use this
landmass. Keep the 6.8 km generator as distant backdrop and the fast-travel map; stop
trying to make all of it presentable.

---

## 8. Milestones

One person, AI-assisted. Each ends in something screenshottable and has a **gate** — a
specific reason to stop or change course.

### M0 — Foundations (2–3 days)
*Invisible on camera. Everything else is gated on it.*
- Player / Enemy / NPC / Systems / HUD become **prefabs**; spawners load and configure them
- **ScriptableObject** data for enemies, NPCs, dialogue, quests, loot, story flags
- One `.inputactions` asset; gamepad support falls out free (matters for capture)
- `GameState` enum (Menu / Cutscene / Playing / Paused / Dialogue / Loading)
- Additive scenes: `Bootstrap` + `Highmoor` + `Karsand` + interiors
- Apply the renaming from §3

**Gate:** you can change enemy health in the Inspector without recompiling.

### M1 — Feel (3–4 days) ← *biggest visible jump in the plan*
- **Animation**: start from Unity **Starter Assets – ThirdPerson** (free, animated,
  Cinemachine included). Retarget **Mixamo**: idle/walk/run/sprint/strafe, jump,
  draw–sheathe, light + power attack, block, hit reaction, death
- **Cinemachine** third-person orbit + first-person toggle; head-look via **Animation Rigging**
- **Ragdoll** on death
- **Audio**: surface-aware footsteps, weapon impacts, ambient beds per region/weather,
  `AudioMixer` snapshots for explore ↔ combat
- **Post**: ACES tonemap, bloom, SSAO, per-region grading, sun shafts; **Adaptive Probe
  Volumes** for GI
- Wind on foliage; rain that visibly wets surfaces

**Gate — the real one.** Record 20 seconds walking through rain toward the castle. If that
doesn't look like a game trailer, the answer is "not viable with these tools" — and it's
much better to know at day 6 than day 25.

### M2 — The corridor (5–7 days, art-heavy)
- Blender: **asset conditioning pipeline** + **Highmoor kit** (the Yoku kit exists)
- Blender: **terrain heightmaps** for both hero areas → Unity Terrain, textured, grassed
- Dress the Highmoor street and the Karsand gate — density is what sells it
- **One interior** (inn) and **one dungeon**, additive scenes behind load doors
- Loading screen: rotating prop on black + lore text
- LODs, instancing, occlusion culling — hold capture framerate

**Gate:** solo art capacity gets tested here. If dressing one street takes four days rather
than one, drop to a single region and shorten the reel.

### M3 — Story and the loop (4–5 days)
- Build the narrative system from §6; author **your** story into it
- **Dialogue camera** (Cinemachine cut to NPC, shallow DoF) + branching options
- **NPC idles and schedules** — three well-animated NPCs beat thirty static ones
- **Compass** rebuild, **"location discovered"** fade, quest markers
- **Inventory** with 3D item preview; equipping changes the mesh
- **Level-up constellation** + small perk tree
- HUD rebuilt as prefabs with **TextMeshPro** (the 780-line code-built legacy `Text` HUD is
  the last blocker to it looking authored)

**Gate:** a player who knows nothing can follow the story start to finish without you
narrating.

### M4 — The set-piece (3–4 days)
- The confrontation your story builds to; original creature blocked out in Blender
- A signature power (shout-equivalent): screen shake, VFX, ragdoll impulse
- **Killcam**: `timeScale ≈ 0.3` + Cinemachine cut on the finishing blow
- Scripted in **Timeline** so every take is identical

**Gate:** a 30-second clip you'd open the trailer with.

### M5 — The reel (2–3 days)
- Capture at 60 fps, grade, original score
- Cut long (~5 min) and short (~45 s) — the short one is what people actually watch

**Total ~19–26 focused days**, of which roughly 5–6 are code. If that ratio holds, the
verdict is: *viable, and the constraint is art and feel, not engineering.*

---

## 9. What to record as you go

The actual output of the experiment:

1. **Days on code vs. art/audio/feel.** Predicted ~6 vs. ~18.
2. **What AI couldn't do** that you expected it to.
3. **How far generated kits got** versus hand-made or bought art.
4. **Which "tells"** mattered most to viewers (animation, audio, lighting, camera?).
5. **The verdict** — viable / viable with caveats / not viable, with the evidence.

A negative result is still a result. A documented "here's exactly where it broke down" is
more interesting than a reel that overclaims.

---

## 10. Open decisions (yours)

1. **The names** in §3 — approve or replace.
2. **The storyline** — the dump described in §6. This is the blocking one for M3.
3. **The creature** for the set-piece.
4. **Third-person or first-person primary?** Recommend third-person with a toggle;
   it captures far better.
5. **Playable build, or video only?** Changes the legal posture and about a week of polish.

---

## Appendix — completed hardening pass (2026-07-26)

- **Repo**: git + LFS, Unity YAML `.gitattributes`, pushed
- **Critical bugs**: `SnapToWalkable` ignored its argument and returned the spawn pad, so
  every NPC and enemy spawned in one pile; two POIs authored in open water; roads were
  4.2 km stretched cubes flying through the sky; the coastline teleported the player home;
  fast travel always charged the minimum time; quest discovery fired twice
- **Structure**: `WorldLayout` single source of truth, `GameLayers` + `WorldTagger`
  replacing name-matched gameplay logic, layer-masked combat casts, `PlayerRef`
- **Performance**: 639 per-prop `Update`s → one system; camera far plane matched to world;
  texture budget took the build from 206 MB to 140 MB
- **Tooling**: `Tools/compile-check.py` (per-asmdef), `BuildPlayerCommand`, `TextureBudget`,
  9 edit-mode tests, headless rebuild/test/build commands in the README
- **Blender**: `Tools/Blender/make_kit_yoku.py` + `preview_kit.py`, first generated kit

Open items from that pass are folded into **M0**.
