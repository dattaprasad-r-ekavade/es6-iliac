# Ratna Bay Production Plan

**Written:** 2026-08-23
**Design of record:** [`design_pivot.md`](design_pivot.md) — what the game is.
**Scope contract:** [`TRAILER.md`](TRAILER.md) — what the slice must be able to show.
**This document:** what gets built, in what order, and how it is known to work.

---

## 1. Closed decisions

Not revisited. Reopening any of them costs months, and this project's history shows that is the
failure most likely to end it.

| Decision | Ruling |
|---|---|
| **Genre** | Roguelike. Runs of five to eight minutes, generated mines, a fort that opens a room at a time. |
| **Engine** | MonoGame + custom tooling. The Unity archive is a source of ported logic, not a fallback. |
| **Game rules live in `RatnaBay.Domain`** | Engine-free C#, tested headlessly. No MonoGame reference, ever. |
| **Characters and weapons are sprites drawn in code** | A palette and some proportions, not modelled assets. Sidesteps skinned animation entirely. |
| **Physics is hand-rolled** | Swept AABB against a static BVH, all three axes. BepuPhysics stays unintegrated. |
| **Navigation is direct pursuit** | Enemies close on the player. DotRecast stays unintegrated. |
| **UI is immediate-mode on SpriteBatch** | Not Gum. |
| **Levels are JSON manifests** | Generated mines emit the same format the game already loads and hot-reloads. |
| **The publish gate is the definition of done** | `.\publish.ps1` runs the domain tests, the sim, and the published build's own self-test. A build that fails is not handed to anyone. |

### Parked

Built, tested, and deliberately unreachable. The switches live in `ParkedFeatures`, with the
reason written beside each one.

| Feature | Why | When it might return |
|---|---|---|
| **Pickpocketing** | Built for a town full of people to move through. A descent has nothing with pockets in it and the yard has one trader you are meant to trade with. Testers never found it even when it was the only route to a key. | Iteration 19, the fort — ten rooms of occupants who will not talk to you yet is the situation it was written for. |

Parking is not deleting: the domain rules and their tests keep running, so a parked feature
cannot rot and switching it back on is one line.

### Excluded from the first release

Continent-scale world · authored open regions · sailing · day/night and weather · cutscene
system · fast travel · NPC schedules · radiant quests · public mod support · multiplayer · voice
acting.

---

## 2. Where the project is

| | Lines | State |
|---|---:|---|
| `RatnaBay.Domain` | ~4,600 | Tested game rules, engine-free |
| `RatnaBay.Domain.Tests` | ~5,600 | **455 tests, ~470 ms** |
| `RatnaBay.Game` | ~5,600 | Renderer, HUD, session, world runtime, combat |
| `RatnaBay.Tools` | ~370 | `doctor`, `validate`, `asset-info`, `sim`, `mine` |

**Built and carried into the pivot:** combat with guarding and weapon classes · enemy levels and
scaling · five spells with distinct effects, cast as travelling bolts · the prana economy · the
three life paths and their multipliers · eight use-based skills · collision, doors, locks ·
stealth, watchers, pickpocketing · dialogue, quests, shops · versioned saves · the world manifest
format, its validation and its hot reload · the publish pipeline and its gates.

**Not yet built:** stone slots · amulets · succession · five mechanical cave themes · the fort ·
bosses.

---

## 2b. What is actually missing — read this before planning anything

The design's loop has four stages. **One and a half of them exist.**

```
   Town ──▶ pay stones to open a mine ──▶ Descend ──▶ Clear / press / camp
    ✗                   ✗                    ✓                  ✓
    └──────── rank, story, gear, trade ◀── Return ────────────────┘
                        ✗                     ~
```

Five playtests said the camp decision is answered in about a second, and the response each
time was to make the descent more dangerous: attrition, deeper mines, escalating levels, a
shooter, doors that bar and rooms that wake. All of it was needed and none of it addressed
the actual reason.

**Banking stones does nothing.** There is nowhere to spend them, nothing to spend them on, and
no deeper mine to open with them. A player carrying forty-five stones out of a mine is carrying
out a number. The risk has been tuned five times; the reward has never been built at all.

No amount of danger makes a decision tense when one side of it is worthless. That is the
single most important sentence on this page.

**So the next milestone is not an iteration. It is closing the loop.** Nothing else — themes,
slots, bosses, the fort — is worth building until a run has a point.

### Milestone: the loop closes

| | Why it is the missing half |
|---|---|
| **Mine tiers bought with stones** | The design already says money gates depth. It is what banked stones are *for*, and it is the decision the whole surface exists to host. |
| **A surface to come back to** | Somewhere to stand between runs. It need not be the fort — one room and a trader is enough to test whether returning means anything. |
| **Something worth buying** | Gear that changes a descent. The shop and the equipment catalogue already exist; nothing in them is worth forty-five stones. |
| **Levels stop outgrowing the mine** | Max health compounds across successors with nothing to counterweight it. At 220 against enemies tuned for 100, tier one is already solved. |

**Done when:** a player banks, spends, and descends deeper because they chose to — and the
recording shows a door answered in more than a second because both sides of it are real.

**Playtest after all four, not between them.** Each of these changes what the others mean;
measuring one at a time has produced five sessions of chasing symptoms.

---

## 3. The iteration plan

**Every iteration ends with something playable for five minutes.** An iteration that ends with a
passing build and nothing new to play did not happen.

Estimates assume 15–20 hours a week.

---

### Iteration 13 — One generated mine ✅ done

**Risk retired:** can generation produce the format the game already loads? **Yes.**

- Room-graph generator: a seed in, a `WorldManifest` out.
- Rooms connected by the existing door system; one entrance, one exit each.
- `RatnaBay.Tools mine --seed N` writes a manifest and validates it.
- Enemies placed per room by the existing spawn path.

**Playable:** `RatnaBay.exe --mine 4211 --rooms 5 --depth 2`. Descend into a generated mine,
fight through it, open each door, walk out the far end.

**Done when:** a new seed produces a different, valid, playable mine with no code change. ✅

**What it cost, and what it caught.** Two bugs that every other check waved through:

- The walkability tests fired a ray between room centres, and passed with the doorways sealed to
  two millimetres — an infinitely thin ray threads any gap. They now walk the route with the real
  swept mover at the real body radius, and fail when a doorway is narrower than a body.
- The random source was a bare xorshift, and the low bits it picked directions with were
  correlated enough to repeat the same step several times running: **every mine came out a
  straight corridor.** SplitMix32 fixed it, and `MinesActuallyTurnCorners` now guards it.

---

### Iteration 14 — The run ✅ built, not yet judged

**Risk retired:** *partly.* The loop exists and its numbers are the design's. Whether it is
**tense** is not a thing code can answer — that needs a stranger.

- `RunState`: seed, tier, rooms cleared, stones held. Engine-free and tested against the design
  table — 3 rooms banks 6, 5 banks 15, 8 banks 36.
- `RunRuntime`: the bridge from where the player is standing to what the ledger believes.
- Camp at a cleared room's exit — bank and end, or open the next door. Never mid-fight.
- Payout `N x T`; death forfeits the pot, and records what was lost so succession can fetch it.
- A run summary screen, and a running "at risk" total so the pot is never a surprise at the door.
- Camping checkpoints the banked stones at the surface; the same character, inventory, XP and
  skills enter the next mine. Generated worlds stay in memory rather than accumulating in the
  installed game directory.

**Playable:** a complete run. Clear rooms, decide each time whether to press on, walk out with
stones or lose them.

**Done when:** you catch yourself pushing one room too far. **Still open.** The code is
finished; the question it exists to answer is not. **This is the iteration that decides whether
the pivot works** — if the decision is not tense, no later system fixes it.

**Known gap:** a descent cannot be saved out of, by design — reloading the moment a fight turned
would remove the only thing being risked. Resuming an *interrupted* run is a different feature
and does not exist; it belongs with succession, which touches the same code.

---

### Iteration 15 — Succession ✅ done

**Risk retired:** does death read as continuity rather than punishment?

- Death promotes a named successor: levels kept, unspent progress cleared, half the pack lost.
- The fallen Deepankar's cache waits in the room they died in. Descending again returns to that
  same mine until it is fetched — a fresh random mine each time would put it somewhere
  unreachable by design.
- Life path and the order's training are inherited.

**Playable:** die, come back as somebody else, and go and fetch your own body.

**Two rules that keep a loss answerable.** Keys and the equipped weapon are never taken: a
successor who arrives unarmed cannot earn back what it costs to re-equip, and one locked out by
a lost key is stranded behind their own progress. Everything else in the pack is halved,
rounded up, so a single potion is a single potion lost.

**Not built:** amulets, which do not exist until iteration 17.

---

### Iteration 16 — Stones and slots (2 weeks)

**Risk retired:** is in-run variety readable inside five to eight minutes?

- Sockets on weapons and armour; stones found below, never carried down.
- A small set of stone effects that change how a weapon or spell behaves, not how large it is.
- Socketing UI in the existing inventory screen.

**Playable:** find a stone mid-run, socket it, and fight differently for the rest of the run.

**Done when:** two runs with different stones feel like different runs.

---

### Iteration 17 — The ratchet (2 weeks)

**Risk retired:** does a losing run still pull you back in?

- Amulets drop on clearing a mine; permanent, and they survive death.
- Character level moves onto the experience track; level-up grants skill points.
- A between-run screen showing what was gained.

**Playable:** lose a run and still be measurably stronger for the next one.

---

### Iteration 18 — Cave themes (2 weeks)

**Risk retired:** does theme change how you play, or only how it looks?

- Five themes: colour shading, preta sprite set, one resisted and one feared element.
- Resistance, never immunity.
- The theme is shown before the player pays to open the mine.

**Playable:** choose which cave to buy into, knowing what is down there, and ready the right
spell for it.

---

### Iteration 19 — The fort (3–4 weeks)

**Risk retired:** content authoring throughput — the number that decides whether this is
finishable.

- Ten rooms, opened by wins and gold.
- Occupants stay silent until a rank or a sum is reached.
- Story fragments attached to rooms rather than to a questline.

**Playable:** a fort worth walking around between runs.

**Done when:** you can state **hours of authoring per hour of play**. Record it. This is the
single most useful figure for planning everything after release.

---

### Iteration 20 — Bosses (2–3 weeks)

**Risk retired:** is there a reason to reach the bottom?

- Three distinct fight behaviours, dressed per theme to make six or seven encounters.
- A boss ends a deeper mine rather than a camp.

**Playable:** a run that ends on a fight worth the descent.

---

### Iteration 21 — Slice lock (2 weeks)

- Settings, bindings, death and recovery flow.
- A full playthrough regression script in `RatnaBay.Tools sim`.
- Packaged build verified on a machine without .NET.
- **Three external playtests, with notes.**

**Done when:** three people play several runs each without your help.

---

## 4. Timeline

```
13  Generated mine       ██
14  The run              ███      ← the iteration that decides the pivot
15  Succession           ██
16  Stones and slots     ██
17  The ratchet          ██
18  Cave themes          ██
19  The fort             ████     ← authoring throughput measured
20  Bosses               ███
21  Slice lock           ██
                         ─────────
                         17–22 weeks
```

Roughly four to five months at 15–20 hours a week. The first external playtest is **iteration
14**, not 21 — the run loop is the thing that most needs a stranger's opinion, and it needs it
before eight more weeks are built on top of it.

---

## 5. The board

One board. Work in progress limit: **one**.

### In progress

- None.

### Next

- **Close the loop** — see §2b. Tiers bought with stones, a surface to return to, gear worth
  buying, and levels that stop outgrowing the mine. Build all four, then play.
- Trailer build list, from [`TRAILER.md`](TRAILER.md): the Stambha and its carved verse, the
  preta rise animation, one fort room with a conversation, the camp decision UI, succession,
  and two cave themes. Two themes and one room film the trailer; five and ten ship the game.

### Ready

- Iteration 18 — cave themes. Held deliberately: the sameness is mechanical, and a themed
  room is still a room with no reason to be entered until the loop pays.
- Iteration 16 — stones and slots.

### Playtest queue

- **The closed loop, in one sitting, after all four pieces land.** Not before: these changes
  redefine each other, and testing them one at a time is what produced five rounds of chasing
  symptoms of a missing reward.
- **Then a stranger.** Still the most valuable hour available to the project.

### Done

- Iterations 5–12: domain port, live HUD, saves, combat, sprites, authored world, collision,
  dialogue, quests, shops, stealth, the packaged build and its gates.
- Pivot groundwork: travelling spell bolts, enemy levels, life-path multipliers, the spell
  rebalance.
- Iteration 15: succession, the fallen cache, named Deepankars.
- Iteration 14: the run ledger, the camp decision, the payout curve, the summary screen.
- Play-driven fixes: enemies that respect walls, a room that pays once, levelling that heals
  nothing, mines deeper than a run, and an archer that makes the doorway the worst place to
  stand. All of it found by recording play and reading it back.
- Iteration 13: the mine generator, `WorldEnemySpawn` in the manifest, the enemy catalogue,
  `RatnaBay.Tools mine`, and `--mine N` in the game.
- The design, decided end to end.

---

## 6. Working rules

**Definition of done.** Code integrated · `.\publish.ps1` passes end to end · the new thing is
playable by double-clicking `build\RatnaBay.exe` · the board records it.

**Every new domain system arrives with its rules asserted as tests.** Not coverage for its own
sake — the design decisions, written as assertions, so a rebalance that breaks one is a decision
somebody made on purpose.

**When something takes twice its estimate, cut scope rather than extend.** The estimate was the
hypothesis; the overrun is the result.

**No new package earns integration without a vertical spike and a passing release build.**

---

## 7. Standing risks

| Risk | Watch for | Response |
|---|---|---|
| **The run loop is not tense** | Iteration 14 plays flat | Fix the numbers before building on it. Nothing later rescues a dull loop. |
| **Generated mines feel samey** | Every run reads the same after three | More room shapes before more systems |
| **The fort outgrows its cap** | Room eleven | Ten is the number. Story goes into the ten. |
| **Another engine or genre pivot** | "X is fighting me" | Both decisions are closed. Solve X. |
| **Polish before playability** | An iteration spent on shading | Ship the playable thing first |
| **Art becomes the bottleneck** | A week on one sprite | Sprites are generated. If one is taking a week, generate it. |

---

## 8. The measure

**By iteration 14, a stranger should play three runs in a row without being asked to.**

Everything before that is preparation. Everything after is a game getting better.
