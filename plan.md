# Iliac Bay — a viability experiment

**The question:** in 2026, with AI assistance, can one person build a *decent* 3D game in
the mould of Skyrim — set in High Rock and Hammerfell — and is that actually viable or not?

Not "similar to Skyrim". Extremely close: the camera feel, the animation, the audio bed,
the discovery beat, the dialogue camera, the level-up moment. Close enough that someone
watching it recognises what it's imitating without being told.

**Deliverable:** a ~5 minute playable vertical slice plus a reel cut from it. Homage, not
for sale — see [Legal guardrails](#legal-guardrails).

**This document is the experiment log as much as the plan.** The point is to answer the
viability question honestly, including if the answer turns out to be "partly" or "no".

---

## What this repo already tells us

Two days of AI-assisted work produced: a procedural 6.8 km Iliac Bay, first-person
traversal, melee + magic combat, 2 enemy types, NPCs with dialogue, 3 quests + journal,
inventory, world map with fog-of-war discovery, fast travel, day/night + regional weather,
versioned save/load, a HUD, and a 140 MB Windows build.

Then one hardening pass added: a single source of truth for world data, physics layers
replacing name-matched gameplay logic, versioned saves with quest state, assembly
definitions, 9 edit-mode tests, headless build/test/rebuild tooling, and git + LFS — while
finding and fixing bugs that had made the "shipped" vertical slice not actually playable
as described (every NPC and enemy spawning in one pile; two POIs authored in open water;
roads flying through the sky).

**Early read: the systems half of a Skyrim-like is comfortably viable.** That is a real
result, and it's the part most people assume is hard.

*(Secondary observation, since it comes up: this suggests the multi-year timelines on games
of this type aren't explained by the systems. It doesn't say much about shipping a
200-hour RPG — that cost is content volume, localization, cert, mod tooling, and the bug
tail of an emergent systems game. Worth stating carefully if the reel gets an audience,
because it's the first objection anyone will raise.)*

---

## Where AI helps, and where it doesn't

This is the actual finding to test. Evidence so far from this project:

**Works well — genuinely multiplies output**
- Systems code: controllers, combat, quests, save/load, inventory, weather
- Reading a codebase and finding real bugs (the spawn-pile bug was found by tracing code,
  not by playing)
- Large mechanical refactors: extracting single sources of truth, replacing name-matched
  logic with layers, converting 639 per-object `Update`s into one system
- Tooling and automation: headless compile-check, batch build/test commands, importer passes
- Data authoring and validation: layout tests that catch "this POI is in open water"
- Documentation that stays honest about what's broken

**Doesn't work — still the bottleneck**
- **Art.** Nobody is generating a coherent, consistently-styled, correctly-scaled,
  LOD'd, collision-ready 3D town from a prompt. Asset packs plus taste, not AI.
- **Animation.** The single biggest quality tell in the whole project, and the least
  automatable. Retargeted Mixamo clips are the realistic answer.
- **Game feel.** Camera damping, attack timing, footstep pacing, how long a hit-stop
  lasts — these require playing the thing repeatedly. I can't play it.
- **Level layout craft.** Whether a street "reads" as inhabited, whether a dungeon paces
  well, whether a vista draws you forward.
- **Audio design.** Sourcing and mixing is judgement work; the clips exist, the taste doesn't.
- **Knowing when it's good enough.** Entirely yours.

**So the strategy is:** let AI carry the systems, tooling and refactor load completely, buy
or source all art and audio, and spend your own hours almost exclusively on **feel, layout
and dressing**. The plan below is arranged so that's how the time actually gets spent.

---

## What "extremely close to Skyrim" actually means

Feature lists don't produce the feeling; these specific things do — roughly in order of
how fast a viewer notices them missing.

| Tell | Why it reads as Skyrim | Status |
|---|---|---|
| **Animated character** — locomotion blend, draw/sheathe, power attack, hit reaction, ragdoll death | A sliding capsule reads "student project" in two seconds. #1 by a wide margin. | ❌ none |
| **Third-person orbit camera** (+ first-person toggle), that FOV and shoulder offset | Skyrim is recognisable from framing alone | ❌ none |
| **Audio bed** — surface-aware footsteps, impacts, wind/rain, explore↔combat music | Biggest perceived-quality gain per hour spent | ⚠️ 9 UI clips |
| **Post-processing** — ACES tonemap, bloom, SSAO, sun shafts, per-region grade | Turns "Unity default" into "a game" in an afternoon | ⚠️ volume exists, unconfigured |
| **The compass** — thin top bar, tick marks, location diamonds fading in | Instantly identifiable | ⚠️ text-only |
| **"Location discovered"** centre-screen fade | The core exploration dopamine beat | ⚠️ toast only |
| **Interiors behind load doors** — warm firelight against cold exterior | Skyrim's entire pacing rhythm | ❌ none |
| **Dialogue camera** — cut to NPC upper body, shallow DoF, subtitle + options | Every quest runs through this | ⚠️ text panel |
| **Loading screen** — rotating 3D object on black + lore text | Pure Bethesda, and cheap | ❌ none |
| **Level-up constellation** star field | The signature progression moment | ⚠️ toast only |
| **Killcam** — slow-mo Cinemachine cut on the finishing blow | The thing people clip and share | ❌ none |
| **A shout-equivalent** signature power | The money shot of any Skyrim trailer | ❌ none |
| **NPC idles** — sweeping, hammering, sitting, walking with purpose | Makes a town inhabited rather than staffed by mannequins | ❌ none |
| **Physics props** you can knock over | Bethesda's most-imitated texture of interactivity | ❌ none |

Almost all of that is presentation, not systems — i.e. the half AI doesn't do for you.
That asymmetry *is* the experiment.

---

## The one strategic call: shrink the world

The current world is 6.8 km across with three cities and mostly empty procedural terrain.
It is the biggest single obstacle to looking good. A vertical slice does not need a
walkable province — it needs **one dense square kilometre per region**.

- **High Rock** — a rain-soaked Breton valley: stone bridge, castle silhouette, wet
  cobbles, one town you can walk end to end in 90 seconds.
- **Hammerfell** — golden Alik'r dunes and a sandstone gate: domes, arches, heat haze,
  hard shadows.
- **One dungeon** — torchlit, three rooms, a trap, undead, a boss chamber.

The contrast between wet Breton stone and golden Yoku desert *is the pitch*, and it's the
real reason to set this in the Iliac Bay rather than anywhere else. Keep the 6.8 km
generator as distant backdrop and fast-travel map; stop trying to make all of it presentable.

---

## Milestones

One person, AI-assisted, asset packs, focused days. Each ends in something screenshottable,
and each has a **viability gate** — a specific reason you might stop or change course.

### M0 — Foundations (2–3 days)

*Invisible on camera. Everything else is gated on it.*

- Player / Enemy / NPC / GameSystems / HUD become **prefabs**; spawners load and configure
  them instead of building them with `AddComponent`
- `ScriptableObject` data for enemies, NPCs, dialogue, quests, loot
- One `.inputactions` asset replacing scattered `Keyboard.current` polling — gamepad
  support falls out free, which matters for capture
- `GameState` enum (Menu / Cutscene / Playing / Paused / Dialogue / Loading)
- Additive scenes: `Bootstrap` + `World_HighRock` + `World_Hammerfell` + interiors

**Why first:** you cannot attach an Animator to a procedurally-built capsule and iterate.
**Gate:** you can change enemy health in the Inspector without recompiling.

### M1 — Feel (3–4 days) ← *biggest visible jump in the plan*

- **Character + animation.** Start from Unity's **Starter Assets – ThirdPerson** (free,
  animated, Cinemachine included) rather than hand-rolling. Retarget **Mixamo** clips:
  idle / walk / run / sprint / strafe, jump, draw–sheathe, light + power attack, block,
  hit reaction, death
- **Cinemachine** third-person orbit + first-person toggle; head-look IK via **Animation Rigging**
- **Ragdoll** on death
- **Audio pass** — cheapest large win in the project: surface-aware footsteps, weapon
  whoosh/impact/block, ambient beds per region and weather, `AudioMixer` snapshots for
  explore ↔ combat music
- **Post-processing** per region: ACES tonemap, bloom, SSAO, vignette, grading (cold
  desaturated High Rock, warm high-contrast Hammerfell), sun shafts
- **Adaptive Probe Volumes** for GI (Unity 6) — big lighting win for one bake
- Wind on foliage; rain that visibly wets surfaces

**Gate — the real one for this whole experiment.** Record 20 seconds of walking through
rain toward the castle. If that clip doesn't look like a game trailer, the viability answer
is "no with these tools", and it's better to know at day 6 than day 24.

### M2 — The corridor (4–6 days, art-heavy)

- Sculpt **one km²** of High Rock valley on Unity Terrain (heightmap, replacing
  noise-on-cylinders), Poly Haven textures, grass detail meshes
- Dress **one Breton street**: gate, inn, blacksmith with sparks, stalls, clutter, signs,
  lanterns, laundry lines — density is what sells it
- Sculpt the **Hammerfell dune approach** + sandstone gate
- **One interior** (inn: fire, warm light, physics clutter) and **one dungeon** (3 rooms,
  trap, boss chamber) as additive scenes behind **load doors**
- Loading screen: rotating prop on black + lore text
- LODs, GPU instancing, occlusion culling — hold capture framerate

**Gate:** this is where solo art capacity gets tested. If dressing one street takes four
days rather than one, scope drops to a single region and the reel gets shorter.

### M3 — The loop (3–4 days)

- **Dialogue camera** (Cinemachine cut to NPC upper body, shallow DoF) + branching options
  from ScriptableObjects
- **NPC idles + schedules** — sweeping, hammering, sitting, a patrol. Three well-animated
  NPCs beat thirty static ones
- **Compass** rebuild: bar, ticks, location diamonds, quest marker
- **"Location discovered"** centre-screen fade
- **Inventory** with 3D item preview; equipping changes the character mesh
- **Level-up constellation** + a small perk tree
- HUD rebuilt as prefabs with **TextMeshPro** — the 780-line code-built legacy `Text` HUD
  is the last big blocker to it looking authored

**Gate:** the full beat works — talk, accept, track, complete, level up.

### M4 — The set-piece (3–4 days)

- One **signature regional threat**. Original creature, not a dragon reskin — an Alik'r
  sand-wyrm surfacing from a dune is spectacular and genuinely yours. *(Your call.)*
- A **shout-equivalent** power: screen shake, VFX, ragdoll impulse — the trailer shot
- **Killcam**: on finishing blow, `timeScale ≈ 0.3` + Cinemachine cut to close orbit
- Scripted in **Timeline** so every capture take is identical

**Gate:** a 30-second clip you'd put first in the trailer.

### M5 — The reel (2–3 days)

Build to this shot list; don't discover it in the edit.

```
0:00  loading screen, lore quote → fade up on rain, castle on the ridge
0:20  traversal: sprint, weather, vista
0:45  town gate, guard hails you, "location discovered"
1:00  street life: sweeping NPC, blacksmith sparks, music shift
1:30  inn interior, firelight, dialogue camera, quest accepted
2:00  journal + map, fast-travel transition
2:15  HAMMERFELL — golden dunes, sandstone gate, heat haze   ← the contrast beat
2:45  dungeon: torchlight, trap, undead, killcam
3:30  emerge into the Alik'r; the wyrm surfaces
4:00  combat, shout-equivalent, creature death
4:30  level-up constellation, loot
4:45  wide vista, title card
```

- Original orchestral music, graded capture, 60 fps
- Also cut a 45-second version — that's the one people actually watch

**Total ~17–24 focused days**, of which roughly 5 are code. If that ratio holds, the
experiment's answer is: *viable, and the constraint is art and feel, not engineering.*

---

## Answering the question honestly

Record these as you go — they're the actual output of the experiment:

1. **Days spent on code vs. art/audio/feel.** Predicted ~5 vs. ~15. If code is much
   higher, AI assistance is weaker than this plan assumes.
2. **What AI couldn't do** that you expected it to. Concrete list.
3. **Where asset packs broke the illusion** — style mismatch, scale, missing pieces.
4. **Which "tells" mattered most** in the table above, judged from viewer reactions.
5. **The verdict:** decent 3D Skyrim-like as a solo AI-assisted project — viable, viable
   with caveats, or not viable? Say which, with the evidence.

A negative result is still a result, and a well-documented "here's exactly where it broke
down" is more interesting than a reel that overclaims.

---

## Stack

| Need | Use | Note |
|---|---|---|
| Character + camera | Unity **Starter Assets – ThirdPerson**, **Cinemachine 3** | free; don't hand-roll |
| Animation clips | **Mixamo** | free humanoid retarget; check current terms |
| IK / head look | **Animation Rigging** package | |
| Terrain | Unity Terrain + heightmap | replaces noise-on-cylinders for hero areas |
| Materials / HDRIs | **Poly Haven** (in repo) | CC0 |
| Props / buildings | **Quaternius**, **Kenney** (in repo) | CC0 |
| Audio | **Sonniss GDC bundles**, Kenney, freesound | royalty-free; verify each licence |
| Music | commissioned / royalty-free orchestral / AI-generated | **never** Jeremy Soule's tracks |
| Set-piece | **Timeline** + Cinemachine | repeatable takes |
| Capture | OBS or Unity Recorder | 60 fps, high bitrate |

---

## Legal guardrails

A public showcase draws attention a private prototype never does. "Free" and "homage" are
not defences against trademark or asset claims — fan projects do get C&D'd.

- No Bethesda meshes, textures, audio, music, voice lines, UI art, or quest text
- No "Skyrim" / "Elder Scrolls" / "ES6" branding in the build, title or thumbnail — the
  repo folder is a working title only; rename before publishing anything
- Original creature and place names for new content; keep the writing yours
- Keep `Assets/ThirdParty/ATTRIBUTION.md` current — CC0 packs still deserve credit and
  it's your provenance record
- State plainly: unaffiliated fan work, not for sale
- Distribute the **video** freely; be far more cautious about distributing a **build**

---

## Open decisions (yours)

1. **The creature.** Sand-wyrm, djinn, Dwemer-style construct? Drives M4 entirely.
2. **First- or third-person primary?** Third-person captures better; first-person is more
   authentic. Recommend third-person with a toggle.
3. **Which region opens the reel?** Recommend High Rock rain → Hammerfell gold; the
   warm/cold cut at 2:15 is the strongest moment.
4. **Playable build, or video only?** Changes the legal posture and about a week of polish.

---

## Appendix — completed hardening pass (2026-07-26)

Kept for the record; all verified and pushed.

- **Repo**: git + LFS, `.gitattributes` for Unity YAML, three commits pushed
- **Critical bugs**: `SnapToWalkable` ignored its argument and returned the Daggerfall
  spawn pad, so every NPC and enemy spawned in one pile; bandit camp and coastal ruin were
  authored in open water; roads were 4.2 km stretched cubes flying through the sky; the
  coastline teleported the player home; fast travel always charged the minimum time; quest
  discovery fired twice
- **Structure**: `WorldLayout` single source of truth, `GameLayers` + `WorldTagger`
  replacing name-matched gameplay logic, layer-masked combat casts, `PlayerRef`
- **Performance**: 639 per-prop `Update`s → one time-sliced system; camera far plane matched
  to the world; texture budget took the build from 206 MB to 140 MB
- **Tooling**: `Tools/compile-check.py` (per-asmdef), `BuildPlayerCommand`, `TextureBudget`,
  9 edit-mode tests, headless rebuild/test/build commands documented in the README

Still-open items from that pass are folded into **M0**.
