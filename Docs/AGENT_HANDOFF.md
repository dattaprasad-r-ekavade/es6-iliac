# Agent handoff — how to pick up this project cold

**Updated:** 2026-08-11

This project is developed by rotating AI agents with no shared memory between sessions.
**This document is the memory.** Read it before doing anything else.

If you change something that makes a section here wrong, update the section in the same
commit. A stale handoff doc is worse than none.

---

## 1. Sixty-second orientation

A first-person fantasy RPG in Unity 6000.5.3f1 / URP 17.5, Windows target, solo developer
working ~8–10 hours a week on a 1+ year horizon.

- **Setting:** original. Kessil Bay, the realms of Halbrand and Sarrakh. No third-party
  game's names or assets are in the deliverable.
- **Design north star:** Morrowind — for look *and* flow. Reading-driven quests, directions
  over markers, topic dialogue, in-fiction travel.
- **Current deliverable:** Chapter 01 as an **internal proof of concept**, not a product. It
  proves the pipeline and teaches the process. The eventual product is 8 chapters, paid,
  $5–10.
- **Story:** a world whose magic runs on soul crystals, a king who ran out of legitimate
  supply and started harvesting prisoners, and a tower everyone believes is the source but is
  actually the alarm.

---

## 2. Authority map — which document owns what

Do not resolve a question from the wrong file.

| Question | Authority |
|---|---|
| Chapter 01 plot, beat for beat | `storyline.md` |
| Chapter 01 implementation contract, beats, cast, flags, evidence | `Docs/CHAPTER01_BEATS.md` |
| World premise, the Everspire truth, factions, Chapters 02+ | `Docs/STORY_ARC.md` |
| Navigation, dialogue, travel, combat, skills, economy | `Docs/GAMEPLAY_DESIGN.md` |
| Milestones, gates, risks, world architecture, art direction | `plan.md` |
| Third-party asset licensing | `Docs/ASSET_LEDGER.md` |
| What to work on next, and how to verify it | **this file** |

---

## 3. Verification — commands that actually work

All four are verified working as of 2026-08-11. Unity must be **closed** for the headless
ones (they take the project lock).

```bash
# Compile-check every assembly without opening Unity. Fast, run this constantly.
python Tools/compile-check.py

# EditMode tests — currently 61/61
"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" \
  -runTests -testPlatform EditMode \
  -testResults "<scratch>/em.xml" -logFile "<scratch>/em.log"

# PlayMode tests — currently 101/101
"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" \
  -runTests -testPlatform PlayMode \
  -testResults "<scratch>/pm.xml" -logFile "<scratch>/pm.log"

# Windows build → Builds/Windows/Kessil.exe
"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "D:/Projects/Elder Scrolls 6" \
  -executeMethod BuildPlayerCommand.BuildWindows -logFile "<scratch>/build.log"
```

Exit code 0 means pass. Parse the results XML for the `total/passed/failed` attributes —
the console output is not reliable.

**Definition of done for any code change:** compile-check clean, EditMode green, PlayMode
green. Do not report success without running them.

---

## 4. Invariants — do not break these

Each is enforced by a test, a contract, or an explicit lock. Breaking one silently is the
most expensive kind of mistake here.

| Invariant | Why | Enforced by |
|---|---|---|
| Save-persisted ids never embed display names (`city_west`, not `Caldemar`) | renames must stay display-only | `WorldLayoutTests` |
| Palette colours stay in the muted range; `ArtDirection.Current` is Morrowind Clean | prevents drift back to the engine-project look | `ArtDirectionTests` |
| Code branches on ids, never on display names | same as above | convention |
| The title card fires exactly once, only at B640 | authored moment | `CinematicRunner`, `GreyThreadDirector`, PlayMode gate |
| Watching and skipping a cinematic produce identical flags | save integrity | beat sheet acceptance tests |
| All four routes enter B600 **unarmed**, gear stored not destroyed | lets B630 be authored once | convergence contract clause 6 |
| Chapter 01 must never hint that the Everspire is an alarm | it is the Chapter 06 reveal | `STORY_ARC.md` |
| Chapter 01 must establish organic soul sourcing as **normal and legal** | without it the whole arc collapses into an abolition story | `STORY_ARC.md` plant list |
| `player.channeled` is tracked, **never** mechanically punished | it changes dialogue, not damage | `GAMEPLAY_DESIGN.md` |
| The protagonist is silent, permanently | it is what makes topic dialogue affordable | slice definition |
| Spoke chapters condition on `evidence_count`, never on *which* spokes are done | keeps the open hub consistent | spoke contract |

---

## 5. Known traps

Found the expensive way. Do not rediscover them.

**MCP in batch mode.** `McpBootstrap` used to force-start the MCP bridge on every editor
load and retry 60 times. Headless runs have no server, the package logs a `LogError` per
attempt, and Unity fails any test that sees an unexpected error log — so the PlayMode suite
failed whichever fixture happened to be active and was non-deterministic. It now returns
early when `Application.isBatchMode`. **Do not revert this.**

**`LogAssert.ignoreFailingMessages` does not work as a global fix.** It resets at fixture
boundaries. Fix log noise at the source instead.

**Never let tests use the default `SaveLoadService.SaveFilePath`.** SaveGameV4 makes the path
injectable; `SmokeTestFixture` assigns a unique file in `temporaryCachePath` and removes the
slot, temporary file and backup on teardown. New save tests must inherit that fixture or
configure/reset an isolated path themselves.

**`compile-check.py` only sees assemblies that already have a generated csproj.** A brand new
`.asmdef` reports nothing until Unity refreshes. A clean compile-check does **not** prove new
test files compile — run the actual test command.

**Unity fails tests on unexpected `Debug.LogError`.** If the code under test logs one on
purpose, declare it with `LogAssert.Expect(LogType.Error, new Regex(...))`.

**`PlayerStats.Update` regenerates stamina every frame** (4/sec in combat, 12/sec at rest).
Use `[Test]` rather than `[UnityTest]` when asserting on stamina, so no frames advance.
**Mana does not regenerate at all** — it is crystal charge. If you find yourself adding mana
regen to make a test pass, the test is wrong.

**`KessilWorldGenerator.SnapCharacterToGround` returns its input unchanged when no terrain is
present.** This is why save/load round-trips exactly in a bare test scene.

**`Assets/Scenes/Main.unity` is a destructively generated artifact.** Editor tools rebuild it
wholesale. Never hand-author anything into it that you cannot regenerate.

**Scene architecture must be regenerated with Main.** `SetupGamePresentation` and
`BuildKessilWorld` call `SceneArchitectureBuilder`, which restores Main's context, the
exterior snapshot and build settings. If another destructive generator is introduced, wire
the same calls into it. Do not hand-edit `Estmere_Exterior`; it is also generated.

**Kenney mini-characters cannot be Humanoid rigs.** They have no knee joint, so Unity's
avatar generation fails on `Required human bone 'LeftLowerLeg' not found`. Do not spend time
trying to retarget animation onto them — see W-13. Setting them to Human anyway leaves a
broken state that logs a rig error on every import; `CharacterRigTests` guards against it.

**A generated scene without a `SceneContext` cannot be entered.** `SceneTransitionService`
fails the transition and the player is stranded wherever they were — New Game reached
`Prologue_Ship` and stopped there permanently until this was found. Any new generated scene
must build a `SceneContext` and at least one `SceneSpawnPoint`.

**`Game.Tests.asmdef` is Editor-only** (`includePlatforms: ["Editor"]`). PlayMode tests live
in the separate `Game.PlayModeTests` assembly.

**Git diff stats on this repo are wildly misleading.** `.unity` files are enormous generated
YAML. The VS1+VS2 commit reads as 336,372 insertions; 324,507 of those are scene files, and
the actual authored change is ~3,700 lines of C#. When measuring work, filter by extension:

```bash
git show --numstat <sha> -- "*.cs" | awk '$1 ~ /^[0-9]+$/ {a+=$1; d+=$2; n++} END {printf "%d files, +%d / -%d\n", n, a, d}'
```

---

## 6. Current state

**VS2 is complete (2026-08-01).** The grey-thread route gate runs all four assignments through
additive Chapter 01 scenes, covering 42/42 beat ids to B830 / Caldemar Council. The next packet
is W-11, the external Map Editor MVP.

| | Status |
|---|---|
| VS0 — story package and regression baseline | **Complete**, except the screenplay (deliberately deferred to the VS2→VS3 window) |
| VS1 — technical spine | **Complete — W-01–W-09 gate passed** |
| VS2 — grey thread | **Complete — 42/42 grey beats; all four routes reach B830** |
| Tests | EditMode 61/61, PlayMode 101/101 |
| Build | `Builds/Windows/Kessil.exe`, 142.5 MB, 0 errors; Bootstrap is scene zero |
| Code | 51 runtime scripts, 14 editor scripts, plus Python tooling |
| Scenes | `Bootstrap`, generated `Main`, additive `Estmere_Exterior`, 11 Chapter 01 grey scenes; four test fixtures |
| Prefabs / ScriptableObjects / `.inputactions` | 4 runtime prefabs; NPC, dialogue, quest and cinematic data assets; one input-actions asset |

Every narrative and production lock for Chapter 01 is closed. The grey implementation covers
all 42 beat ids; “authored content 0/42” is intentionally still the next content burn-down.

### Velocity, for planning

| Phase | Conventional effort | Calendar |
|---|---|---|
| Prototype → VS0 complete | ~600–1,000 person-hours | 7 days |
| VS1 + VS2 | ~110–170 person-hours (planned at 14–21 days) | ~1 session |

**Do not extrapolate this rate to VS3–VS7.** VS1 and VS2 are the most leverageable milestones
in the plan — systems architecture, scene plumbing, save schemas, tests: well-specified,
self-contained, machine-verifiable. VS5's four authored routes are content, and *does this
feel like Morrowind* cannot be asserted in a test. Estimates assuming otherwise will be wrong
by a large factor.

Current planning range for the Chapter 01 POC at 8–10 h/week: **4–8 months**. All eight
chapters: **2–4 years**.

---

## 7. Work packets

Ordered by dependency. Each is sized to be picked up cold. **Read the listed sections before
starting** — they contain decisions that are expensive to contradict.

### Sequencing — read this before picking a packet

**The region rebuild and the Map Editor are both deferred until after VS2.** The architecture
decision is recorded in `plan.md`; executing it is not urgent, and an earlier draft of this
document wrongly said otherwise.

The reasoning:

| Milestone | What it needs from the world |
|---|---|
| VS1 | terrain that exists, spawn points, something to extract into scenes |
| VS2 | **grey boxes** — plan.md says "it will look like nothing, that is expected and correct" |
| VS3 | authored environments. **This is where the Map Editor pays off** |

Scene architecture is **geometry-agnostic**: building `Estmere_Exterior` as an additive scene
with a spawn contract and a fade is the same work whichever geometry sits inside it. Twelve of
VS2's thirteen scenes are interiors and are region-independent regardless.

Two traps this ordering avoids:

- **Rebuilding a world VS2 will only grey-box.** The current bay is wrong-shaped and ugly, but
  it is geometry with 20 passing tests, which is all VS1 and VS2 require.
- **Building an authoring tool before authoring anything.** Nobody has built a region in this
  project or this data model, so the requirements would be guesses. Tools built after one
  painful manual pass are far better than tools built before it.

Both cost 8–14 days during which VS2 — the milestone that converts this plan from speculation
into a measurable burn-down — would not move.

**Keep the scene plumbing geometry-agnostic** and the deferral costs nothing.

### W-01 · Bootstrap scene, additive loading, scene transitions — **complete 2026-08-01**

- Read: `plan.md` § *Scene and loading architecture*
- Persistent `Bootstrap` scene owning services, input, UI, audio, saves, loading.
- `SceneTransitionService`: additive load order, spawn ids, fade, companion handoff, and a
  recovery path when a load fails.
- Extract the exterior out of generated `Main` **without destroying the working world**.

**Done when:** three scenes load additively with spawn placement, and a failed load recovers
rather than hanging.

**Delivered:** `SceneTransitionService`, `SceneContext`, stable `SceneSpawnPoint` ids,
black fade overlay, transactional rollback, `BootstrapEntryPoint`, the regenerable
`Estmere_Exterior` snapshot, and A/B/C plus invalid-scene fixtures. `SceneArchitectureBuilder`
recreates the architecture after any destructive Main rebuild. `BuildPlayerCommand` packages
Bootstrap/Main/exterior only; fixtures remain in EditorBuildSettings for PlayMode tests.

**Evidence at the W-01 gate:** compile-check clean; EditMode 20/20; PlayMode 18/18; Windows
build 142.4 MB, 0 errors. W-02 subsequently landed; the current next packet is W-03.

### W-02 · `GameState` service — **complete 2026-08-01**

Single owner of input, cursor, time scale and pause. Replaces the scattered checks currently
spread across `GameHud`, `GameFlowController` and `PlayerCombat`.

States: gameplay, dialogue, cinematic, menu, loading, death.

**Migration result:** `GameHud.ShowDialogue` and `CloseDialogue` now push/pop dialogue state;
they no longer write `Time.timeScale` or cursor state directly.

**Delivered:** `GameStateService` owns menu, cinematic, gameplay, dialogue, loading and
death policy; it is the only runtime writer of time scale and cursor state. Movement,
combat and NPC interaction consume `GameplayInputAllowed`. Loading uses a guarded state
stack so success and every rollback restore the exact prior pause policy. Direct-Main play
anchors a compatibility owner to `GameFlowController`; packaged play owns it in Bootstrap.

**Evidence:** compile-check clean; EditMode 20/20; PlayMode 22/22, including real
Bootstrap→Main loading-state release; Windows build 142.4 MB, 0 errors. Next packet is W-03.

### W-03 · Input actions asset — **complete 2026-08-01**

One `.inputactions` asset. `com.unity.inputsystem@1.19.0` is already in the manifest.
Controller support is out of slice scope; keyboard and mouse only.

**Delivered:** `Resources/Input/KessilInputActions.inputactions` contains the single `Game`
map and every current movement, look, combat, interaction, save/load, HUD and intro action.
`GameInput` is the typed access point. All six former device-polling consumers now read
actions; source audit finds no runtime `Keyboard.current` or `Mouse.current` usage.

**Evidence:** compile-check clean; EditMode 31/31 (including action/binding/scope contract
tests); PlayMode 22/22; Windows build 142.4 MB, 0 errors. Next packet is W-04.

### W-04 · Prefabs and ScriptableObjects

Replace `AddComponent` construction with prefabbed player, NPC and UI. This is the change
that makes everything else testable — the current systems are hard to smoke-test precisely
because they are built by code at runtime.

**Complete 2026-08-01.** `RuntimePrefabBuilder` regenerates Player, GameSystems, NPC and the
complete HUD visual hierarchy. Generated Main contains linked Player and GameSystems prefab
instances and no missing scripts. Runtime startup now requires those authored components
instead of silently assembling roots with `AddComponent`.

The Unity source-layout debt is closed: `PlayerInventory`, `PlayerCombat`, `PlayerInteract`,
`NpcInteractable` and `QuestSystem` live in matching source files. Five `NpcArchetype`
ScriptableObjects carry stable ids, site-relative placement, models, tint, dialogue and role
flags; the GameSystems prefab references them. HUD button callbacks and visual references
are rebound after prefab instantiation, so the hierarchy is tunable without losing runtime
behavior. Do not hand-author prefab instances into generated `Main`.

**Evidence at W-04 close:** compile-check clean; EditMode 35/35; PlayMode 22/22; Windows
build 142.1 MB, 0 errors.

### W-05 · `SaveGameV4` — **complete 2026-08-01**

**Get the shape right the first time.** Migrating v4 → v5 mid-slice is the avoidable
disaster here.

Must carry: current scene and spawn id, `CharacterProfile`, chapter, stage, `flag.route`,
all story flags, evidence set with inspected state, dialogue choices, companion state, King
outcome, ruler state, granted title, opened locks, looted objects, skipped cinematics —
**plus skills and the equipped set** (see `GAMEPLAY_DESIGN.md` deltas 12–13).

Also: atomic write with backup, header validation on the menu so an invalid save is rejected
before entering gameplay, and safe handling of existing v3 files.

Make `SaveFilePath` injectable while you are here, then simplify `SmokeTestFixture`.

Implemented as schema v4 with atomic temporary-write/replace and `.bak`, menu validation,
injectable paths, safe v3 migration, current scene/spawn, profile, chapter/stage/beat, route,
flags, full evidence, choices, companion, outcomes, locks, loot, cinematics, skills,
equipment and `player.channeled`. Continue transitions to the saved additive scene/spawn
before applying state.

### W-06 · Topic-based dialogue runtime — **complete 2026-08-01**

**The one delta with a real deadline** — converting a tree system to topics later means
rewriting the data *and* everything authored against it.

- Read: `GAMEPLAY_DESIGN.md` § *Dialogue — topic-based, not tree-based*
- Keyword hypertext, not conversation trees. Shared knowledge base, responses filtered by
  faction, disposition, location and story flags.
- Conditions must include `evidence_count` — this is what enforces the spoke contract in data
  rather than by review.

`TopicDialogueService` resolves a shared Resources knowledge base by keyword and chooses the
most specific valid response. Conditions cover route, flags, evidence count, faction,
disposition, location and channeling; choices are written back to `StoryDirector`.

### W-07 · Story systems — **complete 2026-08-01**

`StoryDirector` as the sole authority for beat transitions and checkpoints. Data-driven quest
stages, story flags, route gates, `EvidenceRecord` with a **full readable document body**
(show the document; never summarise it into a journal line).

`StoryDirector` is the single profile/beat/route/flag/consequence authority. Evidence retains
its full readable body and inspected state. Existing prototype quests now seed from three
`QuestDefinition` assets rather than C# literals.

### W-08 · `CinematicRunner` — **complete 2026-08-01**

Deterministic cues plus an **idempotent end state applied whether watched or skipped**. Three
beat acceptance tests already depend on this.

The runner sorts timed cues, applies every remaining state cue when skipped, commits the
same authored end-state contract, records skips and refuses to replay a completed sequence.

### W-09 · VS1 gate — **passed 2026-08-01**

A throwaway test quest that crosses three additive scenes, branches, takes evidence, saves,
quits, continues, restores a companion, mutates the world, and rolls back correctly.

**This is the gate. Do not start VS2 until it passes.**

`Vs1Gate_ThreeScenesBranchEvidenceSaveContinueCompanionAndRollback` crosses A→B→C, chooses
the mage branch, acquires readable evidence, mutates locks/loot/world state, saves, moves
away, destroys/recreates story services, continues back to saved C/spawn, restores the
companion and rejects every post-save mutation. Current evidence: compile clean, EditMode
38/38, PlayMode 29/29, Windows build 142.1 MB.

The non-packet VS1 world-authoring prerequisite is also complete:
`Assets/Resources/Data/World/kessil.world.json` is required at runtime and owns dimensions,
anchors, ten landmasses, eight sites and five road spines. `WorldLayout` exposes the stable
API but loads those values from JSON; the map-editor phase can therefore replace the file
without rewriting gameplay code.

### W-10 · VS2 — the grey thread — **complete 2026-08-01**

**The de-risking milestone, and the most important packet in this document.**

Every scene in the scene table exists, even as a grey box with a placeholder sign. Every
transition works in order with the real spawn and autosave contract. All four routes are
selectable at the audience and reach the convergence checkpoint as genuinely separate paths,
not one path with a flag. Cinematics are timed placeholder cards that apply their real end
state. Dialogue is placeholder text driven by the real topic graph.

- Read: `plan.md` § *VS2*, and `CHAPTER01_BEATS.md` in full.
- Twelve of the thirteen scenes are interiors and are region-independent. Only
  `Estmere_Exterior` cares about world shape, and at this stage it is a grey box.

**Delivered:** `GreyThreadSceneBuilder` regenerates 11 Chapter 01 rooms with stable contexts,
spawns, stepped elevations and collision-backed walls. `GreyThreadDirector` now visits all
**42/42** beat ids across the prologue, a real clickable King's audience assignment panel,
distinct Warrior/Mage/Trade/Refuse branches, prison/cave convergence, the invoked B640 title
crawl, aftermath and Caldemar handoff. It records profile/route choices, prince testimony,
typed King outcome/ruler/title state and valid V4 route checkpoints. The player-preservation
fix keeps the generated player alive when the first content scene unloads. Screenshots:
`Docs/Screenshots/vs2-estmere-palace.png`, `Docs/Screenshots/vs2-caldemar-arrival.png`.

**Gate passed:** a developer starts a new game, chooses a name and assignment in-game, and
reaches the Caldemar handoff on **all four routes** without touching the editor. It is
intentionally grey; the next packet replaces these placeholders with authored environments,
dialogue and mechanics.

The implementation is structurally complete. The next burn-down is authored content: **0/42
beats replaced**, not missing traversal waypoints.

**Verified independently 2026-08-01:** compile-check clean; EditMode 45/45; PlayMode 30/30
including the 42-beat union, title/evidence/outcome/autosave assertions; Windows build
142.5 MB, all commands exit 0.

### W-14 · VS4 mechanics — **complete 2026-08-04**

Built ahead of W-11 because none of it needs art or a browser, and it turns the game from a
story skeleton into something with RPG mechanics. All of it is headlessly testable — grey
capsules swing swords perfectly well.

| System | Files |
|---|---|
| Crystal charge, channeling, stamina fix | `SoulCrystals`, `PlayerRpg` |
| Equipment, three weapon classes, armour, blocking | `EquipmentCatalog`, `PlayerEquipment` |
| Five distinct spells | `SpellCatalog`, `SpellCaster`, plus burn/chill/stagger on `EnemyBrain` |
| Eight use-based skills, five anti-grind rules | `Skills`, `SkillSystem` |
| Detection, locks, pickpocketing, crime | `DetectionSystem`, `DetectionWatcher`, `DoorAndLock`, `PickpocketSystem`, `CrimeWitness` |
| Sailing | `SailingController` |

**36 new PlayMode tests.** Design decisions made during the build are recorded in
`GAMEPLAY_DESIGN.md` § *Deltas*.

**Not yet wired into content.** These systems exist and are tested in isolation; no Chapter 01
scene uses them yet. VS5 is where the routes are authored against them.

**Still missing from VS4:** tutorial prompt and objective framework, checkpoints and
recoverable fail states, and `CompanionController` — which is scoped to authored sequences
only, so it is best built alongside the prison escape rather than in isolation.

### W-15 · Playable Chapter 01, Arena-style — **complete 2026-08-11**

The chapter is played rather than watched. Built procedurally so that playtesting can happen
before any art exists — the same reason Arena and Daggerfall were procedural.

| Piece | Files |
|---|---|
| 2.4 km region, walled 1.6 km city, sea bound | `EstmereRegion`, `EstmereRegionBuilder` |
| Doors into interiors, return-to-door | `RegionPortal`, `RegionReturn` |
| Arena billboard characters | `BillboardActor` |
| Written directions, live bearing | `ObjectiveService` |
| Player-driven beats | `GreyThreadDirector` player-driven mode |

**How it plays.** Boot → title → START → prologue plays automatically → the player stands in
the region with an objective. Walking to a door and pressing E enters that interior, its beats
fire, and the player is returned to the door they used with the next objective set.

**The automated VS2 gate still runs with player-driven mode off**, so it stays deterministic.
Do not "fix" that by making the gate interactive — it would then depend on UI input.

**Known gaps for the next session:**

- The King's audience blocks on `WaitForAssignment()`, a UI panel. Fine when a human plays;
  it is why the gate runs non-interactively.
- Interiors are still single grey rooms. The region is a place; the interiors are not yet.
- VS4 mechanics are wired to route beats but nothing in an interior *uses* detection, locks,
  pickpocketing or sailing yet. That is VS5.
- No exit doors inside interiors — the director returns the player, so a human who wanders
  cannot leave on their own.

### W-11 · Map Editor MVP · **next**

Now — not before — because you will have authored thirteen scenes and know what the pain
actually is. A tool built after one manual pass is far better than one built before it.

- Read: `plan.md` § *World-authoring goal — Kessil World Builder*
- Tiled-backed, with a Kessil importer, validator and one-click headless preview.
- Source of truth becomes a versioned `kessil.world.json`, not a Unity scene and not
  hand-edited C#.

**Gate:** a non-Unity user can move a coastline, paint elevation and biome, redraw a road,
place a city gate and a story spawn, press one button, and get a valid region back — with
plain-language errors for invalid configurations.

### W-12 · Region rebuild · *after W-11*

Execute the architecture already decided in `plan.md` § *World architecture*. The bay becomes
Witcher 3-style regions: a city plus a dense walkable hinterland per plane, connected by the
ferry network.

**Dimensions are locked:** city core ~1.2 km, region 2 km × 2 km square bounded by open sea,
~10 minutes corner to corner at the 3.5 m/s walk. Do not re-derive these — see
`GAMEPLAY_DESIGN.md` § *Traversal and scale*.

**Chapter 01 needs only the Estmere region**, plus the shipboard prologue and a Caldemar
arrival sliver. Caldemar, Qadris and Aldreth as full regions are Chapter 02+ work.
**Build Estmere and measure what it actually costs before committing to four.**

- Rearchitect `KessilWorldGenerator` (1,107 lines) around a region rather than a world.
- **Rewrite `WorldLayoutTests`.** 16 of the 20 EditMode tests assert on the current bay's
  elliptical coasts, road spines and city-to-landmass links, and will not survive. Replace
  them rather than deleting them — the id-stability and dry-interior assertions still matter.
- Art direction's "keep the bay thin and fog-limited" was a consequence of the *old*
  architecture. **Regions are meant to be dense.** Do not carry the thinness rule over.

---

### W-13 · Humanoid base spike — **result 2026-08-01: the current characters cannot be rigged**

**Spike answered. The blocker was never the animation source — it is the mesh.**

Unity's Humanoid rig requires 15 bones. Every Kenney mini-character in
`Resources/Characters` fails avatar generation with:

```
Rig Error: Invalid Avatar Rig Configuration. Missing or invalid transform:
    Required human bone 'LeftLowerLeg' not found
```

They have **no knee joint** — single-segment legs. All 12 human models fail. This is a
skeleton limitation, not an import setting, so **no amount of Mixamo work would have made
them animate.** They were already rejected on art grounds ("saturated toybox characters
against a muted world"); they are now independently disqualified on technical grounds.

`Kessil → Characters → Convert To Humanoid Rig` attempts the conversion and **reverts
automatically if no valid avatar results**, because Human-without-an-avatar is strictly worse
than Generic — it logs a rig error on every import and still plays nothing.
`Kessil → Characters → Revert To Generic Rig` forces cleanup. `CharacterRigTests` guards
against anything being left in the broken middle state.

The models are currently Generic with no avatar, which is correct for static use.

#### What the replacement mesh must have

Use the tool to evaluate any candidate — drop it in `Resources/Characters`, run Convert, and
the test tells you whether it took. Required at minimum:

- Hips, spine, head
- Upper **and lower** arms, plus hands
- Upper **and lower** legs, plus feet

Anything stylised enough to drop the knee or elbow will fail the same way.

#### Remaining manual step

Sourcing the mesh and animations needs a browser and an Adobe account, so it is not
agent-work. Fetch: one humanoid mesh, Mixamo auto-rig, then idle / walk / run / attack /
block / hit / death. Record the account and source in `ASSET_LEDGER.md` — the licence is
royalty-free for games but is not public domain.

**Deliberately not built yet:** the `AnimationDriver` and Animator controller. With no valid
rig and no clips, that code could not be tested, and untested speculative code is against the
standard this project holds. Build it against the real rig, where it can be verified.

Budget when the mesh lands: half a session. Do not build a character system off the back of
it — the entire hostile roster is humans and humans who came back wrong.

## 8. Decisions still needed

None of these block current work. Raise them when their packet comes up.

0. ~~Map Editor sequencing~~ — **resolved 2026-08-01.** Both the editor (W-11) and the region
   rebuild (W-12) come after VS2. See *Sequencing* above.
1. **The weaponmaster.** The director wants one NPC introducing all weapons, but Chapter 01
   already has `role.instructor_warrior` (Alaric Thorne) in the guard yard, which three of the
   four routes never visit. Either the weaponmaster *is* Thorne and only warriors meet him, or
   he sits somewhere every route passes.

3. Aldreth is a placeholder name (`city_north` is the stable id).
4. What returned souls look like beyond "humans with purple fume" — the late-game bestiary.
5. Subtitle standard; frame-time and memory floor. Both VS8-tier.

## 9. Standing preferences

- Commit messages: descriptive body explaining *why*, not just what.
- Docs get updated in the same commit as the change they describe.
- Report test results as run, with numbers. Never claim green without the command output.
- The codebase is AI-generated and AI-audited by design. Human comprehension is not the
  maintenance model; the tests and this document are.
