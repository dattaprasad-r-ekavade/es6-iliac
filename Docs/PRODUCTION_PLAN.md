# Ratna Bay Production Plan

**Written:** 2026-08-22
**Status:** Active. This is the single planning document for the project.

This document supersedes the roadmap sections of `SOLO_AGILE_DEVELOPMENT_PLAN.md`,
`MONOGAME_PIVOT_PLAN.md`, and the gate ladder in `DAGGERFALL_SCOPE_AND_BUILD_RESEARCH.md`.
Those remain as research and rationale; when they disagree with this file, this file wins.

---

## 1. The decisions that are closed

These are not revisited. Reopening any of them costs months, and the project's history shows
that is the failure mode most likely to kill it.

| Decision | Ruling |
|---|---|
| **Engine** | MonoGame + custom tooling. The Unity archive is a source of ported logic, not a fallback. |
| **Game rules live in `RatnaBay.Domain`** | Engine-free C#, tested headlessly. No MonoGame reference, ever. |
| **Characters are billboard sprites** | Sidesteps skinned animation, MonoGame's worst gap, and is period-correct. |
| **Physics is hand-rolled** | Swept AABB/capsule against a static BVH. BepuPhysics stays unintegrated until the slice is done. |
| **Navigation is waypoints** | DotRecast stays unintegrated. A town needs waypoints, not a navmesh. |
| **UI is immediate-mode on SpriteBatch** | Not Gum. A Daggerfall-like is mostly UI, and immediate mode is faster to iterate. |
| **Levels are JSON with hot reload** | No GUI editor. The editor is a text file plus a running game. |
| **Scope is one town, one dungeon, one questline** | 2–3 hours of play. Daggerfall breadth is a sequel. |

### Explicitly excluded from the first product

Procedural world generation · sailing · day/night and weather · cutscene system · fast travel
and fog of war · world map · NPC schedules · time-bound quests · radiant quests · public mod
support · multiplayer · voice acting.

Each of these was implemented or partly implemented in the Unity build. Each is parked, not
deleted. Parked means: not on the board, not in the backlog, not a consideration in any
architectural decision until the slice ships.

---

## 2. Where the project actually is

**Iterations 5 and 6 are complete.** The domain drives the running game, and there is a
fight in it.

| | Lines | State |
|---|---:|---|
| `RatnaBay.Domain` | 3,100 | Tested game rules, engine-free |
| `RatnaBay.Domain.Tests` | 3,300 | 273 tests, ~140 ms |
| `RatnaBay.Game` | ~1,900 | Menu, camera, HUD, session, saves, sprites, combat |
| `RatnaBay.Tools` | ~150 | `doctor`, `asset-info` |

`.\publish.ps1 -Run` produces a self-contained `build\RatnaBay.exe` that runs on a machine
with no .NET installed. The HUD reads the domain's own numbers, and a save round-trips
through a real file — verified by the published executable's own `--selftest`.

### What the domain already does

Character vitals and levelling · eight use-based skills with five anti-grind rules · three
weapon classes with blocking · five mechanically distinct spells · the jiva-stone prana
economy · enemies with burn/chill/stagger · sight-based stealth with crouch and watchers ·
deterministic lockpicking and pickpocketing · crime witnessing · quests with kill and
location objectives · written directions with generated compass bearings · Morrowind-style
keyword dialogue with story-conditioned answers · chapter/beat/route story state · versioned
saves with migration and corruption handling.

---

## 3. The iteration plan

**The rule: every iteration ends with something playable for five minutes.** An iteration
that ends with a passing build and nothing new to play did not happen.

Estimates assume 15–20 hours per week. Halve them for full-time.

---

### Iteration 5 — Wire the shell — **DONE**

**Risk retired:** does the domain actually integrate, or is it a beautiful island?

This is first because it is the cheapest possible test of the whole architecture. If
`PlayerCharacter` does not drop into `Game1.cs` cleanly, that must surface in week one.

**Deliverables**
- `PlayerCharacter` instantiated and ticked from the game loop.
- Immediate-mode HUD layer on `SpriteBatch`: health, prana, stamina bars reading live values.
- Toast/message queue fed by domain events (`SkillRaised`, `LevelGained`, `CrystalDrawn`).
- F5 saves, F9 loads, through `SaveGame` to a real file on disk.
- Player position and yaw round-trip through the save.

**Playable:** walk the Northwatch scene, watch your own stamina drain and recover, save,
quit, relaunch, and arrive back where you were.

**Done when:** a save written before a restart restores position, vitals and skills exactly.

**Delivered.** Plus two things beyond the brief: `--selftest`, a headless save round-trip that
also gates the publish, and the objective now persists (its bearing regenerates rather than
being stored).

---

### Iteration 6 — First fight — **DONE**

**Risk retired:** is the combat loop legible and does it feel like anything?

**Deliverables**
- Billboard sprite renderer: camera-facing quad, palette-locked, texture generated in code.
- `Enemy` wired to a game-layer chase controller (no navmesh — move toward, stop at range).
- Sphere cast down camera forward to find `IAttackable`.
- Attack, block, cooldown and stamina bound to input.
- Enemy health bar, damage flash, hit feedback.
- Skill-up toasts on screen.

**Playable:** a bandit notices you, closes, and fights. You swing, block, kill it, and watch
Blade go up.

**Done when:** the fight is winnable and losable, and Blade rises only on landed hits.

**Delivered.** All three are asserted headlessly by `--selftest`, which plays a whole fight
with no window: the bandit closes from 12 m, both sides trade blows, a swing away from it
trains nothing, four landed hits kill it, and an unarmed player against three of them dies.
Targeting moved into the domain as a cone test rather than a physics sphere cast, so its
edges are asserted rather than felt out in the running game.

---

### Iteration 7 — First room (2 weeks)

**Risk retired:** collision and level authoring — the two things MonoGame gives you nothing for.

**Deliverables**
- Static level BVH and swept AABB/capsule collision (~500 lines, no Bepu).
- JSON world manifest: geometry, spawns, props, lights.
- Hot reload — edit the JSON, see it without restarting.
- `content validate` in `RatnaBay.Tools`.
- One authored interior with a door.
- `Lockable` wired to that door with Security.

**Playable:** walk into a building through a door you picked open.

**Done when:** a new room can be added by editing JSON only, with no C# changes.

---

### Iteration 8 — First conversation (2 weeks)

**Risk retired:** can dialogue carry narrative without a conversation-tree editor?

**Deliverables**
- Dialogue topics loaded from JSON.
- Interaction prompt and target query.
- Immediate-mode topic menu and response panel.
- `SpeakingActor` wired to billboard NPCs.
- Three NPCs, ~15 topics, at least three with story conditions.

**Playable:** learn a keyword from one person and get a different answer from another.

**Done when:** a topic can be added in JSON alone, and no offered keyword produces silence.

---

### Iteration 9 — First quest (2 weeks)

**Risk retired:** does the whole loop connect end to end?

**Deliverables**
- Quest definitions in JSON.
- Journal screen.
- Objective banner with live compass bearing.
- Quest acceptance through dialogue.
- Reward payout, and quest state in the save.

**Playable:** take a quest from an NPC, follow written directions, kill three bandits, return
and get paid.

**Done when:** someone else can play it without you in the room. **This is the first external
playtest.**

---

### Iteration 10 — First theft (2 weeks)

**Risk retired:** stealth legibility — the hardest thing to make readable.

**Deliverables**
- `IWatcher` implemented with view cone and sight-blocker raycast.
- Crouch, and a visible awareness indicator.
- Guard patrol waypoints.
- `Pickpocketing` wired to an interaction.
- Suspicion decay feedback.

**Playable:** crouch past a guard, lift a purse, get spotted, break line of sight, escape.

**Done when:** a playtester can explain *why* they were spotted, without being told.

---

### Iteration 11 — First town (3–4 weeks)

**Risk retired:** content authoring throughput — can one person fill a world?

This is the iteration that answers whether the project is finishable. It is the first time
the work is authoring rather than engineering.

**Deliverables**
- One hand-authored settlement: streets, buildings, two enterable interiors.
- Five NPCs with distinct topics.
- A shop that takes gold and moves items (replacing the placeholder).
- Loot placement and world pickups.
- Ambient sound and music.

**Playable:** a town worth walking around for fifteen minutes.

**Done when:** you can measure hours-of-authoring per hour-of-play. **Record this number** —
it is the single most useful figure for planning the full game.

---

### Iteration 12 — Slice lock (2 weeks)

**Risk retired:** does it survive contact with a real player and a real build?

**Deliverables**
- One authored dungeon.
- Death and recovery flow.
- Settings screen (display, UI scale, bindings).
- Packaged build that runs on a machine without .NET installed.
- Full playthrough regression script in `RatnaBay.Tools sim`.
- Three external playtests, with notes.

**Playable:** the complete loop — start, quest, dungeon, reward, save, quit, reload.

**Done when:** three people finish it without your help.

---

## 4. Timeline

```
Iteration  5  ██                          Wire the shell
Iteration  6  ███                         First fight
Iteration  7  ██                          First room
Iteration  8  ██                          First conversation
Iteration  9  ██                          First quest        ← first external playtest
Iteration 10  ██                          First theft
Iteration 11  ████                        First town         ← authoring throughput measured
Iteration 12  ██                          Slice lock
                                          ────────────────
                                          16–20 weeks
```

**Vertical slice: 16–20 weeks** at 15–20 hours/week. Roughly four to five months.

After the slice, and only after, the project decides between:
1. Expand to a low-mid sized game (est. 12–18 months further).
2. Ship the slice as a short paid game.
3. Stop, having learned what it costs.

That decision needs the authoring-throughput number from iteration 11. Do not make it earlier.

---

## 5. Working rules

**Definition of done.** Code integrated · `.\publish.ps1` passes end to end · the new thing is
playable by double-clicking `build\RatnaBay.exe` · the board records it.

**One command to a playable build.** `.\publish.ps1 -Run`. It gates on the domain tests and
on the published executable's own save round-trip, so a build that reaches `build\` is one
that can be handed to a playtester.

**Every new domain system arrives with its rules asserted as tests.** Not coverage for its own
sake — the design decisions, written as assertions, so a rebalance that breaks one is a
decision someone made on purpose.

**Work in progress limit: one.** One implementation item at a time.

**When something takes twice its estimate, cut scope rather than extend.** The estimate was
the hypothesis; the overrun is the result.

**No new package earns integration without a vertical spike and a passing release build.**
Eleven packages are pinned. Most should stay unintegrated through the slice.

---

## 6. Standing risks

| Risk | Watch for | Response |
|---|---|---|
| **Another engine pivot** | "MonoGame is fighting me on X" | The decision is closed. Solve X. |
| **Polish before playability** | An iteration spent on fonts, scaling, art direction | Ship the playable thing first. |
| **Content authoring is too slow** | Iteration 11's throughput number is bad | Cut world size, not systems. |
| **The domain outruns the game** | More tests, nothing new to play | Stop porting. Wire what exists. |
| **Art becomes the bottleneck** | Weeks spent on a single asset | Buy packs. Generate sprites. Never model by hand. |
| **Scope creep through the archive** | "While I'm in here, sailing was nice" | The parked list is parked. |

---

## 7. The measure

**By iteration 9, someone who is not you should be able to play the build and finish a quest
without help.**

Everything before that is preparation. Everything after that is a game getting better.
