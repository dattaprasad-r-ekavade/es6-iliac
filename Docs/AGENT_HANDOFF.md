# Agent handoff — how to pick up this project cold

**Updated:** 2026-08-12

This project is developed by rotating AI agents with no shared memory between sessions.
**This document is the memory.** Read it before doing anything else.

If you change something that makes a section here wrong, update the section in the same
commit. A stale handoff doc is worse than none.

---

## 1. Sixty-second orientation

A first-person fantasy RPG in Unity 6000.5.3f1 / URP 17.5, Windows target, solo developer
working ~8–10 hours a week on a 1+ year horizon.

- **Setting:** original, and Indic since 2026-08-12. Ratna Bay; the capital is Ratnapur,
  the council seat Sabhapur. No third-party game's names or assets are in the deliverable.
  The repo, the `Kessil*` classes and the `Kessil/` menu root still carry the old codename —
  internal only, and a separate decision.
- **Design north star:** Morrowind for *flow* — reading-driven quests, directions over
  markers, topic dialogue, in-fiction travel. **Look is Arena, read through Rajput and
  Pahari miniature painting** (locked 2026-08-12; see plan.md).
- **Current deliverable:** Chapter 01 as an **internal proof of concept**, not a product. It
  proves the pipeline and teaches the process. The eventual product is 8 chapters, paid,
  $5–10.
- **Story:** a world whose magic runs on jiva stones, a raja who ran out of lawful supply
  and started taking them from prisoners, and a pillar everyone believes is the source but is
  actually the alarm. The line the whole arc rests on is **dāna against steya** — freely
  given against taken. The trade is not the crime.

---

## 2. Authority map — which document owns what

Do not resolve a question from the wrong file.

| Question | Authority |
|---|---|
| Chapter 01 plot, beat for beat | `storyline.md` |
| Chapter 01 implementation contract, beats, cast, flags, evidence | `Docs/CHAPTER01_BEATS.md` |
| World premise, the Stambha truth, factions, Chapters 02+ | `Docs/STORY_ARC_INDIC.md` — **authority since 2026-08-12** |
| The same arc in its original Western-fantasy naming | `Docs/STORY_ARC.md` — **superseded**; kept because it still owns structure, the spoke contract and the endings, which the variant does not restate |
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

# EditMode tests — currently 99/99
"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" \
  -runTests -testPlatform EditMode \
  -testResults "<scratch>/em.xml" -logFile "<scratch>/em.log"

# PlayMode tests — currently 124/124
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
the same calls into it. Do not hand-edit `Capital_Exterior`; it is also generated.

**Kenney mini-characters cannot be Humanoid rigs.** They have no knee joint, so Unity's
avatar generation fails on `Required human bone 'LeftLowerLeg' not found`. Do not spend time
trying to retarget animation onto them — see W-13. Setting them to Human anyway leaves a
broken state that logs a rig error on every import; `CharacterRigTests` guards against it.

**`SceneArchitectureBuilder.EnsureBuildSettings` replaces the build list wholesale.** Any
scene not named in it silently stops being loadable at runtime. `Capital_Region` was dropped
this way and the game became unable to reach its own exterior. Add new scenes there, not only
where they are generated.

**A MonoBehaviour must live in a file matching its class name** or Unity will not serialise it
into a generated scene. `PickpocketTarget` shared `PickpocketSystem.cs` and the prison ended
up with a holder object and no component on it, with no error anywhere.

**A generated scene without a `SceneContext` cannot be entered.** `SceneTransitionService`
fails the transition and the player is stranded wherever they were — New Game reached
`Prologue_Ship` and stopped there permanently until this was found. Any new generated scene
must build a `SceneContext` and at least one `SceneSpawnPoint`.

**`Game.Tests.asmdef` is Editor-only** (`includePlatforms: ["Editor"]`). PlayMode tests live
in the separate `Game.PlayModeTests` assembly.

**There are two scene lists, and only one of them ships.** `EditorBuildSettings` governs the
editor and PlayMode tests. `BuildPlayerCommand` decides what goes into a **player**. These were
maintained separately, and that is how `Capital_Region` came to be in build settings — so
present in the editor and in every passing test — while being **absent from every shipped
build**, stranding the player on New Game with no exterior to walk into. No editor test could
have seen it.

Both now derive from `SceneArchitectureBuilder.ShippingScenePaths()`. **Do not add a third
list.** `GreyThreadSceneTests` holds the region, Bootstrap-at-zero, on-disk existence, and that
test fixtures never ship.

**Unity does not serialise runtime-created textures or materials into a saved scene.** An
editor builder that does `new Texture2D(...)` and assigns it to a renderer produces something
that looks right until the editor reopens the scene, at which point the reference is null.
`BillboardActor` had this bug silently for a week. The rule now: **sprites regenerate on
`Awake`, world surfaces bake to assets** via `ProceduralSurfaceBaker`. Both come from the same
generator, so they cannot disagree.

**`SaveData.SceneId` is misnamed — it holds the Unity scene *name*, not a `scene.*` id.**
`SaveLoadService` assigns it `ActiveContentSceneName` because that is what
`CanStreamedLevelBeLoaded` and `TransitionTo` take. `SceneContext.SceneId` is the id and is
never persisted. So renaming a `scene.*` id is free; renaming a scene *asset* breaks saves and
needs an entry in `SaveLoadService.MigrateSceneName`.

**`ArtDirection.Current` is a static, and several fixtures change it.** Any test that switches
look must restore `ArenaMiniature` in `[TearDown]`, or whether
`ArtDirectionTests.DefaultLook_IsTheLockedOne` passes depends on the order NUnit happened to
run the fixtures in.

**Learned dialogue topics are not saved.** `TopicDialogueService.KnownTopics` is absent from
`StorySnapshot`, so a load loses every keyword the player has picked up. That made the Indic
keyword rename free, and it is a real gap for anyone implementing save/continue properly.

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
| Tests | EditMode 99/99, PlayMode 124/124 |
| Build | `Builds/Windows/Kessil.exe`, 143.8 MB, 0 errors; Bootstrap is scene zero; **15 scenes incl. `Capital_Region`** |
| Code | 53 runtime scripts, 15 editor scripts, plus Python tooling |
| Scenes | `Bootstrap`, generated `Main`, `Capital_Region`, additive `Capital_Exterior`, 11 Chapter 01 grey scenes; four test fixtures. **All named for the building, never the city** |
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

Scene architecture is **geometry-agnostic**: building `Capital_Exterior` as an additive scene
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
`Capital_Exterior` snapshot, and A/B/C plus invalid-scene fixtures. `SceneArchitectureBuilder`
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
  `Capital_Exterior` cares about world shape, and at this stage it is a grey box.

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
143.8 MB, all commands exit 0.

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
| 2.4 km region, walled 1.6 km city, sea bound | `CapitalRegion`, `CapitalRegionBuilder` |
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

### W-16 · Interiors as places — **complete 2026-08-11**

Closes the four gaps W-15 left open.

- **Multi-room interiors.** `GreyThreadSceneCatalog` declares rooms and contents per scene;
  the builder lays chambers out behind the entrance hall joined by real doorways. Prison is
  four rooms, palace and Arcanum three.
- **Exits.** `InteriorExit` returns the player to the door they entered by. The prologue, sea
  cave and aftermath deliberately have none — a door there would let the player walk out of
  the ending, and a test holds that.
- **Mechanics in the world.** Tower has a lock and a watcher, prison has a mark and a watcher,
  harbour has a boat, guard yard and Arcanum have training targets.
- **The audience is testable.** `GreyThreadAssignmentPanel.Submit` is what its buttons call;
  the director exposes `AssignmentPanel` and `AwaitingAssignment`.

**An end-to-end test that walked the chapter to reach the audience was written and removed.**
It depended on six chained scene transitions and could not be made reliable. If you try it
again, know that it has already failed once for that reason.

**Closed by W-17:** interiors now hold the named cast and topic conversation works.

### W-17 · The cast, and talking to them — **complete 2026-08-11**

The topic system was built, tested and wired to nothing in any scene. There was nobody to
talk to. Now there is.

- `SpeakingActor` — a billboard with a role id. Topics offered are the intersection of what
  it can answer and what the player knows to ask, so the menu can never offer a keyword that
  produces silence.
- `GameHud.ShowTopicMenu` — pick a subject by number, not a line from a tree. The menu
  persists until the player leaves, and rebuilds after each answer because asking can teach
  new keywords.
- **Fourteen topics.** Four shared (anyone answers), the rest role-locked, so the same keyword
  answers differently depending on who is asked. That asymmetry is the reason for a shared
  knowledge base rather than a tree, and it has its own test.
- `PlayerInteract` prefers a `SpeakingActor` over an `NpcInteractable`, so the named cast open
  a conversation where street dressing only barks.

**The "soul crystals" topic carries a story requirement, not colour.** It states that crystals
are given at a natural death or bought from beast-drovers, and that this is lawful and has fed
Estmere for two centuries. `STORY_ARC.md` names establishing organic sourcing as normal and
legal the single most important thing Chapter 01 must plant — without it the audience
concludes all crystal use is monstrous and the eight-chapter argument collapses into an
abolition story. **Do not cut or soften that topic.**

Cast placement follows the beat sheet: processing guard at the docks, Thorne in the guard
yard, Quill in the Arcanum, Ashgrove at the harbour, Reed and Falk in the prison, Osric in the
palace, Ambrose at Caldemar. B510 requires the prison reveal split across two speakers; a test
holds that both are present.

### W-18 · Arena Miniature, and the setting goes Indic — **complete 2026-08-12**

Three linked decisions, taken together because each is cheapest before eight chapters exist.

**1. The look is locked to Arena Miniature.** Arena's flat-topped geometry read through the
visual grammar of Rajput and Pahari miniature painting: flat high-chroma pigment, hard drawn
contours, no atmospheric perspective, sprite characters. Full detail in plan.md.

The key point for anyone tempted to reopen it: this is **not** a reversal of the PS1 Crunch
rejection. That preset was rejected because point filtering over 1–2K PBR source bought
aliasing without the chunky-texel read. Authoring at 64 px inverts the argument, which is the
case the original spike said would change the answer.

- `ProceduralSurface` — all eight world textures, drawn in code from the palette.
- `CharacterSprite` — every figure, contoured, deterministic from the actor's name.
- `ProceduralSurfaceBaker` — persists surfaces as assets, because scenes cannot hold runtime
  textures (see traps).

**This removes the W-13 blocker rather than solving it.** Characters are not meshes, so
rigging, retargeting and animation are gone as a category. **W-13 is closed. Do not spend
time sourcing a humanoid base.**

**2. Scene identity is setting-neutral.** `scene.estmere_palace` → `scene.palace`,
`Caldemar_Arrival` → `Council_Arrival`, and so on — the building, not the city. This was the
one category the naming policy was not enforcing, and the one where a save actually breaks.
`GreyThreadSceneTests.SceneIdsAndNames_DoNotEmbedSettingPlaceNames` now holds it; add any new
proper noun to that list.

**3. The setting is Indic.** `Docs/STORY_ARC_INDIC.md` is the authority. Ratnapur, jiva
stones, prana, the Stambha, Raja Vikram. Role/route/flag/evidence/skill/anchor ids were
already neutral, so this was a display swap — the naming policy paying for itself.

**Do not soften the "jiva stones" topic.** It is the only place Chapter 01 establishes that
lawful sourcing is normal — **dāna**, freely given, against **steya**, taken. Without it the
audience concludes all jiva use is monstrous and the eight-chapter argument collapses into an
abolition story.

**Deliberately not done**, so this is not mistaken for finished:

- **Sprite rotations.** Figures are frontal only; Arena drew 5–8 angles. Largest known gap.
- **The `Kessil*` classes and the `Kessil/` menu root.** The project's own codename, referenced
  throughout the docs. Whether the project becomes Ratna Bay is a separate call.
- **`WorldLayout.Biome` and the landmass `Name` strings.** Internal, unpersisted, not
  player-visible.
- **The legacy CC0 kits.** Off the critical path but still in the build, costing download size.

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

### W-13 · Humanoid base spike — **CLOSED 2026-08-12 by W-18; the question no longer applies**

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
