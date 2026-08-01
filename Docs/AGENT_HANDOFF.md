# Agent handoff — how to pick up this project cold

**Updated:** 2026-08-01

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

All four are verified working as of 2026-08-01. Unity must be **closed** for the headless
ones (they take the project lock).

```bash
# Compile-check every assembly without opening Unity. Fast, run this constantly.
python Tools/compile-check.py

# EditMode tests — currently 20/20
"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" \
  -runTests -testPlatform EditMode \
  -testResults "<scratch>/em.xml" -logFile "<scratch>/em.log"

# PlayMode tests — currently 15/15
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
| The title card fires exactly once, only at B640 | authored moment | beat sheet acceptance test |
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

**`SaveLoadService.SaveFilePath` is a static pointing at `Application.persistentDataPath`.**
Any test that saves will overwrite the developer's real save. `SmokeTestFixture` backs it up
and restores it. Make the path injectable when `SaveGameV4` lands, then delete the
workaround.

**`compile-check.py` only sees assemblies that already have a generated csproj.** A brand new
`.asmdef` reports nothing until Unity refreshes. A clean compile-check does **not** prove new
test files compile — run the actual test command.

**Unity fails tests on unexpected `Debug.LogError`.** If the code under test logs one on
purpose, declare it with `LogAssert.Expect(LogType.Error, new Regex(...))`.

**`PlayerStats.Update` regenerates mana and stamina every frame.** Use `[Test]` rather than
`[UnityTest]` when asserting on those, so no frames advance.

**`KessilWorldGenerator.SnapCharacterToGround` returns its input unchanged when no terrain is
present.** This is why save/load round-trips exactly in a bare test scene.

**`Assets/Scenes/Main.unity` is a destructively generated artifact.** Editor tools rebuild it
wholesale. Never hand-author anything into it that you cannot regenerate.

**`Game.Tests.asmdef` is Editor-only** (`includePlatforms: ["Editor"]`). PlayMode tests live
in the separate `Game.PlayModeTests` assembly.

---

## 6. Current state

| | Status |
|---|---|
| VS0 — story package and regression baseline | **Complete**, except the screenplay (deliberately deferred to the VS2→VS3 window) |
| VS1 — technical spine | Not started |
| VS2 — grey thread | Not started |
| Tests | EditMode 20/20, PlayMode 15/15 |
| Build | `Builds/Windows/Kessil.exe`, 138.6 MB, 0 errors |
| Code | ~7.0k lines runtime, ~2.0k editor, ~3.5k Python tooling |
| Scenes | one — generated `Main` |
| Prefabs / ScriptableObjects / `.inputactions` | **none** |

Every narrative and production lock for Chapter 01 is closed.

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

### W-01 · Bootstrap scene, additive loading, scene transitions

- Read: `plan.md` § *Scene and loading architecture*
- Persistent `Bootstrap` scene owning services, input, UI, audio, saves, loading.
- `SceneTransitionService`: additive load order, spawn ids, fade, companion handoff, and a
  recovery path when a load fails.
- Extract the exterior out of generated `Main` **without destroying the working world**.

**Done when:** three scenes load additively with spawn placement, and a failed load recovers
rather than hanging.

### W-02 · `GameState` service

Single owner of input, cursor, time scale and pause. Replaces the scattered checks currently
spread across `GameHud`, `GameFlowController` and `PlayerCombat`.

States: gameplay, dialogue, cinematic, menu, loading, death.

**Watch out:** `GameHud.ShowDialogue` currently sets `Time.timeScale = 0` directly and
`CloseDialogue` restores it. Those become `GameState` transitions.

### W-03 · Input actions asset

One `.inputactions` asset. `com.unity.inputsystem@1.19.0` is already in the manifest but no
asset exists. Controller support is out of slice scope; keyboard and mouse only.

### W-04 · Prefabs and ScriptableObjects

Replace `AddComponent` construction with prefabbed player, NPC and UI. This is the change
that makes everything else testable — the current systems are hard to smoke-test precisely
because they are built by code at runtime.

### W-05 · `SaveGameV4`

**Get the shape right the first time.** Migrating v4 → v5 mid-slice is the avoidable
disaster here.

Must carry: current scene and spawn id, `CharacterProfile`, chapter, stage, `flag.route`,
all story flags, evidence set with inspected state, dialogue choices, companion state, King
outcome, ruler state, granted title, opened locks, looted objects, skipped cinematics —
**plus skills and the equipped set** (see `GAMEPLAY_DESIGN.md` deltas 12–13).

Also: atomic write with backup, header validation on the menu so an invalid save is rejected
before entering gameplay, and safe handling of existing v3 files.

Make `SaveFilePath` injectable while you are here, then simplify `SmokeTestFixture`.

### W-06 · Topic-based dialogue runtime

**The one delta with a real deadline** — converting a tree system to topics later means
rewriting the data *and* everything authored against it.

- Read: `GAMEPLAY_DESIGN.md` § *Dialogue — topic-based, not tree-based*
- Keyword hypertext, not conversation trees. Shared knowledge base, responses filtered by
  faction, disposition, location and story flags.
- Conditions must include `evidence_count` — this is what enforces the spoke contract in data
  rather than by review.

### W-07 · Story systems

`StoryDirector` as the sole authority for beat transitions and checkpoints. Data-driven quest
stages, story flags, route gates, `EvidenceRecord` with a **full readable document body**
(show the document; never summarise it into a journal line).

### W-08 · `CinematicRunner`

Deterministic cues plus an **idempotent end state applied whether watched or skipped**. Three
beat acceptance tests already depend on this.

### W-09 · VS1 gate

A throwaway test quest that crosses three additive scenes, branches, takes evidence, saves,
quits, continues, restores a companion, mutates the world, and rolls back correctly.

**This is the gate. Do not start VS2 until it passes.**

### W-10 · VS2 — the grey thread

**The de-risking milestone, and the most important packet in this document.**

Every scene in the scene table exists, even as a grey box with a placeholder sign. Every
transition works in order with the real spawn and autosave contract. All four routes are
selectable at the audience and reach the convergence checkpoint as genuinely separate paths,
not one path with a flag. Cinematics are timed placeholder cards that apply their real end
state. Dialogue is placeholder text driven by the real topic graph.

- Read: `plan.md` § *VS2*, and `CHAPTER01_BEATS.md` in full.
- Twelve of the thirteen scenes are interiors and are region-independent. Only
  `Estmere_Exterior` cares about world shape, and at this stage it is a grey box.

**Gate:** a developer starts a new game and reaches the Caldemar handoff on **all four
routes** without touching the editor. It will look like nothing. That is expected and correct.

From here the burn-down is measurable: beats with real content, out of 42.

### W-11 · Map Editor MVP · *after VS2*

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

**Chapter 01 needs only the Estmere region**, plus the shipboard prologue and a Caldemar
arrival sliver. Caldemar, Qadris and Aldreth as full regions are Chapter 02+ work.

- Rearchitect `KessilWorldGenerator` (1,107 lines) around a region rather than a world.
- **Rewrite `WorldLayoutTests`.** 16 of the 20 EditMode tests assert on the current bay's
  elliptical coasts, road spines and city-to-landmass links, and will not survive. Replace
  them rather than deleting them — the id-stability and dry-interior assertions still matter.
- Art direction's "keep the bay thin and fog-limited" was a consequence of the *old*
  architecture. **Regions are meant to be dense.** Do not carry the thinness rule over.

---

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
