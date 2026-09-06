# Ratna Bay — Complete Outline

*The game, the world it is set in, and the rules the design and the code both follow.*

This is the one document to read first. It says what the game **is**, not how to build it: the
detail lives in `Docs/`, and the map to that is at the end. Where a thing is built, it says so.
Where it is designed and not yet built, it says that too — a document that quietly promises
finished work is worse than no document, because nobody can tell which half they are reading.

---

## 1. The pitch

> A mining town digs up stones that raise the dead. You are hired to go down and clear them.

**Ratna Bay** is a first-person roguelite: sprites in a 3D world, five-to-eight-minute descents,
and a town that changes between them. You join the **Bhagiratha** — the Light-givers — the order
paid to clear the mountain of what the mining wakes.

It is built by one person, and every decision below is shaped by that: procedural art, generated
worlds, no voice acting, and a story delivered in small pieces rather than cutscenes.

**Form:** first-person, real-time combat, mouse and keyboard, Windows.
**Length:** a run is 5–8 minutes. A campaign is aimed at roughly twenty hours, or ~150 runs.
**Audience:** deliberately somebody's *first* roguelike. That constraint decides the run length,
the death cost, and the fact that a losing run still advances the story.

---

## 2. The loop

```
        ┌──────────────────────────────────────────────────┐
        │                                                  │
   Yard ──▶ pay stones to open a mine ──▶ Descend ──▶ Clear a room of preta
        │                                                  │        │
        │                                                  │        ▼
        └──── rank, story, gear, trade ◀── Return ◀────────┴── Camp, press on, or die
```

**The one decision the whole game rests on** is the shut door at the end of a cleared room: bank
what is in the pot and end the run, or open it and risk the lot.

Every number below is chosen to keep that question open. All are implemented and asserted in the
domain tests.

| Rule | Value | Why |
|---|---|---|
| Room payout | `N × T` stones — the Nth room of a tier-T mine | A flat reward makes banking correct at every step; a rising one keeps "one more?" live |
| Mine entry | `4 × T × (T−1)` stones — free, 8, 24, 48, 80 | You spend the thing you go down to collect |
| Boss room | every 5th room; drops **8 stones into the pot** | Additive, so a boss fattens the stake without flattening the payout curve after it |
| Camp trader | `5 × tier × call number` — 5, 10, 15 at tier 1 | The second trader costs more than the first, and depth charges what it pays |
| Death | all banked stones, half your gear, unspent XP toward the next level | Levels already earned, and every amulet, are kept |
| Recovery | your successor finds the fallen one's cache, **once** | The loss stays real without becoming a wall |

Two rules protect the decision from being laundered away:

1. **Camp only at a cleared room's exit.** No banking mid-fight. Once the door opens you are in
   that room until it is clear or you are not.
2. **A camp trader deals only in what is spent before the run ends** — consumables, never gear —
   and everything at a camp is priced in the pot. If at-risk stones could be turned into
   something that survives death, pressing on would become strictly *safer*.

---

## 3. The world

### The period, and why it is this one

A mining province of an empire modelled on the **Maurya, c. 300 BCE** — not a fantasy kingdom
with Indian decoration. The century was chosen because it already contains the game's mechanics
as documented fact:

- **The state owned the mines.** The *Arthashastra* devotes chapters to mines and to the
  officials who ran them, and is explicit about why the crown cares: mines fill the treasury,
  the treasury pays the army, the army is the state.
- **The mining was real, deep and lethal.** Ancient workings in the goldfields of what is now
  Karnataka were driven by hand far below the surface.
- **The empire's signature object is a carved pillar** — polished sandstone, inscribed with
  edicts addressed to whoever walks past. A state that writes its conscience onto stone columns
  has already built this game's central prop.

**One honest caveat, which affects what may be claimed in public.** The *Arthashastra*'s date is
contested; current scholarship reads it as evidence for post-Mauryan practice. That is fine for a
fictional province in a Mauryan-*analogue* empire, and it is not fine in a store page beginning
"historically accurate". Say **inspired by**. The polity is fictionalised — a named province, a
named governor, and Ashoka is never named.

### The province

A river province at the edge of the empire's reach: far enough that the governor's word is the
empire's word, close enough that the tax convoys leave on schedule.

Three things stacked on one another — a **revenue instrument** audited from a thousand miles
away, a **garrison** because a revenue instrument on a frontier needs one, and a **town of
people** living on a mountain that kills them.

**The tax is not villainy. It is arithmetic, and the officials collecting it mostly believe in
it.** That is the tone the whole game is written in.

---

## 4. Lore

### Jiva stones and prana

The mountain gives up **jiva stones**. Empty they are crystal, worth what crystal is worth.
Filled they hold **prana**, and prana is what the province actually exports — to the capital's
lamps, its foundries, its physicians, its army.

Prana can be gathered three ways, and the difference between them is the moral spine of the
story:

- released by the almost-dead, and caught as it goes;
- **drip-fed from the living, leaving them half dead**;
- taken from animals, which barely works.

### The preta, and why anyone goes down

Stones left in the rock give their prana to the dead. **Preta** rise from it — the past lives of
dead miners, soldiers, and whoever else the mountain has taken. Mining is therefore lethal work,
and the town hires the order to go down and clear the caves before the miners follow.

### The order

The **Bhagiratha**, the Light-givers. The state has exactly one word for one of them — *khanaka*,
digger, on the tally roll — so the order ranks its own the only way that means anything
underground: by which floor of the world below you have been to and come back from. The seven
rungs are the **patalas**, the nether-realms, in canonical descending order. *Tala* is a floor, a
storey — the ladder is named in the vocabulary of a mine.

| Rung | Rank | Descents | Stones banked |
|---:|---|---:|---:|
| 1 | Atala | 0 | 0 |
| 2 | Vitala | 2 | 20 |
| 3 | Sutala | 5 | 60 |
| 4 | Talatala | 9 | 140 |
| 5 | Mahatala | 14 | 260 |
| 6 | Rasatala | 20 | 440 |
| 7 | Patala | 28 | 700 |

Both requirements, never either: descents alone rewards repeating the shallowest mine forever,
and stones alone rewards one lucky run and then nothing.

### Succession

**You do not resurrect. You are replaced.** Another Bhagiratha takes up the lamp, inheriting all
amulets and half your gear. This is the meta-progression *and* the fiction, and it costs the
fiction something, which respawning never does.

Its sharpest consequence, which the design has and the writing has not yet used: the mountain
raises the dead, and the order's dead are in the mountain. **A fallen Bhagiratha whose cache is
never recovered eventually rises** — meeting a vetala wearing your predecessor's gear is a
mechanic, a plot point, and the theme in one object.

---

## 5. Story

**It cannot live inside a run.** A run is too short for a build to form inside, let alone a
scene. Nobody will tell a story about run 47; they will tell one about the character they
assembled across forty runs. So the story lives between runs, and the run is the clock.

**Two taps, and both must stay live:** descending reveals what the mountain holds; rising in rank
reveals what the town is hiding. Beats fire on **conjunctions** (`rank ≥ 3 AND depth ≥ 2`), never
on either alone — with `OR`, players drain the whole story through whichever tap is cheaper and
the other half of the game stops mattering.

### The arc

- **Act I — The job.** Clear caves, get paid, rise a rank. The economy seems fine. The Stambha
  verses are in the mines from the first descent and mean nothing yet; they have to be furniture
  before they can be an accusation. *Turn: the lawful supply does not add up.*
- **Act II — The complicity.** You are the supply chain now. Each rank opens a room and each room
  admits a little more. *Covet not — for whose is wealth?* is carved by the state, in a mine the
  state opened, to extract wealth. *Turn: the order was founded to stop this, and the state
  captured it — not by force, but by being the most reliable employer in the province.*
- **Act III — The choice.** Three endings, each costing somebody: **The Ledger** (expose it; the
  province is shut down and innocent people starve), **The Lamp** (take the trade over and run it
  better; you become the thing), **The Release** (break the stones; end the economy and the risen
  together). Same content, three doors — a player can reach any ending from any path, harder.

### The delivery problem, stated plainly

Fifteen major beats across ~150 runs is one beat every ten runs — an hour of nothing being said.
That is how a story-driven roguelite dies: not from a bad story, but from a correctly written one
delivered too slowly to feel present. The answer is two tiers — ~15 **majors** (a scene each) and
**150–250 reactions** of one or two lines on cheap conditions. *The reaction pool is the actual
authoring project.*

---

## 6. Systems, as built

| System | State | Notes |
|---|---|---|
| Generated mines | **built** | Deterministic from a seed; segment-based, extended as you press on |
| Run, pot, camp-or-press-on | **built** | The numbers in §2 |
| Combat, guarding, weapon classes | **built** | Real-time, first-person, stamina-costed |
| Spells | **built** | Flame 22, Rime 34, Arc 38, Mend 48; balanced by whole resource bars, not per-cast |
| Enemy levels | **built** | Level rises with room and tier: `+2` a tier, `+1` every 3 rooms, a ±1 band, 1-in-12 standouts at +1 |
| Bosses | **built** | Every 5th room; 3 distinct behaviours dressed per theme — a boss without its own pattern is only a large preta |
| Cave themes | **built** | Each names one element it shrugs off and one it fears. **Resistance, never immunity**, and it is shown *before* you pay to open the mine |
| The fort | **built** | Ten rooms, ten occupants, gated by rank. A place you walk into through the gate in the west wall, not a menu |
| Succession, amulets, stone slots | **built** | Amulets are permanent and quiet; stones are loud and answer one cave |
| Skills | **built** | Eight, grown by use: Blade, Block, Heavy, Marksman, Destruction, Restoration, Stealth, Security |
| Life paths | **built** | Warrior (weapons ×2), Mage (spells ×2), Trader (prices `x⁰·⁷⁵` — negligible early, decisive late) |
| Saves, dialogue, quests, shops | **built** | JSON manifests, hot-reloadable, validated in the build gate |
| Developer console + scripting | **built** | Commands, scripts, asserts, exit codes; a script is a build gate |
| Telemetry | **built** | Consent asked before a single byte leaves the machine, and never during a capture or a script |
| The reaction pool | **not written** | The largest remaining authoring task, and the one that decides whether the world feels alive |
| Acts II–III content | **not written** | The structure is decided; the words are not |

### The fort

| Room | Occupant | Opens at |
|---|---|---|
| The Gate | Ganaka, tally-keeper | Atala |
| The Order's Hall | Revati, lamp-keeper | Atala |
| The Assay | Nagadatta, assayer | Vitala |
| The Forge | Lohasena, smith | Sutala |
| The Physician | Visakha | Talatala |
| The Registry | Suvarnapala, *Akaradhyaksha* | Talatala |
| The Shrine | Isidata, priest | Mahatala |
| The Barracks | Bhadrasena, garrison captain | Rasatala |
| The Clerk's Room | Chandrashri, governor's clerk | Rasatala |
| The Governor | Vasumitra | Patala |

A room is a **bounded authoring unit**, which is what keeps the hub from becoming the open-city
problem the design pivot exists to escape.

### Parked, deliberately

**Pickpocketing** and **sneaking** are built, tested, and unreachable. The pivot to a run loop
took away the town full of people they were built for. Parking is not deleting: the rules and
their tests keep running, so a parked feature cannot rot, and the reason sits next to the switch
so it can be argued with rather than rediscovered.

---

## 7. Design principles

These are the rules the design actually follows. Most were bought with a mistake.

**One variable per axis.** Depth decides reward; theme decides tactics. A cave whose element you
are unequipped for is already harder — giving it a payout bonus too would push players toward
whichever theme paid best rather than whichever they could handle.

**Never make the safe play the correct play.** Every rule about camping, camp traders and the pot
exists to stop "always bank when losing" from being the answer.

**A losing run must still advance something.** Succession makes this natural. If a player can
stall the story by being bad at the game, they quit — and the players most likely to stall are
exactly the ones this game is for.

**Information belongs at the point of payment.** The cave's element is shown at the shaft, before
stones change hands, because that is where the decision is.

**A refusal must name what it wants.** "Shut" is a wall; "*Sutala, 3rd of 7*" is a goal. The alpha's
one outside player spent 110 minutes stuck behind refusals that gave them nothing to act on, and
that single fact has changed more of this design than any other.

**Resistance, never immunity.** A player whose only offence is Flame must still be able to finish
a lava cave — badly.

**Two layers, doing different jobs.** Amulets and levels carry build identity across runs; stone
slots are tactical variance inside one. A stone can afford to be loud because it is gone when you
leave the cave; an amulet cannot, because it will still be true on the hundredth run.

**The fiction pays for the mechanic.** Succession is not a respawn rule with a story bolted on —
it is why the order exists. When a mechanic and its fiction agree, neither has to be explained.

---

## 8. Craft principles

How the thing is built, and why it holds together with one person on it.

**Rules are engine-free.** `RatnaBay.Domain` has no MonoGame types; `RatnaBay.Engine` has no
domain; `RatnaBay.Game` is this game on top of both. The boundary is asserted by the build
(`[OK] no domain reference in the engine`), not by discipline. The engine has been exported as a
standalone framework with a second game built on it, which is the proof that the seam is real.

**If it is not proven, it is not done.** One command — `verify.ps1` — builds, runs the tool
doctor, 725 domain tests, content validation, a deterministic simulation, and two scripted
playthroughs of the real client. `publish.ps1` adds a self-test of the packaged executable.

**The game can be driven without a person at the keyboard.** The console takes commands, a script
is a file of them, `assert` fails the process with exit 1. That is how a door, a fight, or a
walkable route gets tested at all.

**Measure; do not reason.** Two screenshots and a pixel count settle in a minute what an hour of
staring at code will get wrong. The shaft was "a shader problem" in a comment for weeks; it was a
line in the renderer throwing the colour away.

**A log with no word for something reports its absence as a fact.** `where` had no word for the
fort, so standing in the fort reported "the yard". This is the single most repeated bug in this
project's history.

**Never trust a switch that reports success.** Three separate features were a flag that was set,
logged as set, and read by nothing. If you add a toggle, prove it changes pixels.

**Art is generated, and overridable.** Sprites, textures and sound effects are forged in code —
one shading model, so a bandit and the sword he holds are lit by the same lamp. Any of it can be
replaced one file at a time by dropping a painted PNG in `Content/Art/Sprites`. The interface is
set in an 8×8 pixel face cut three ways, so no two letters on a line are quite identical.

**Comments carry the reason, not the mechanism.** The code says what it does; the comment says
what went wrong the last time somebody assumed otherwise. A wrong diagnosis left in a comment
outlives the bug it describes.

**Park rather than delete.** A feature nobody can reach costs nothing to keep and a great deal to
rewrite.

---

## 9. Where to read more

| Document | What it settles |
|---|---|
| `Docs/design_pivot.md` | What the player does. The loop, the numbers, the balance passes |
| `Docs/SETTING.md` | What the world is — period, province, economy, what a room contains |
| `Docs/STORY.md` | How a roguelite tells a story: the arc, the two taps, the trigger rules |
| `Docs/NAMES_AND_OFFICES.md` | What people are called, what their jobs are, what the law does |
| `Docs/STORY_AND_SYSTEMS.md` | Where the fiction and the systems have to agree |
| `Docs/PRODUCTION_PLAN.md` | The board: what is being built, in what order |
| `Docs/ENGINE.md` | The engine/game split, and what a second game would reuse |
| `AGENTS.md` | How to work in this repository, and the checks a change must pass |
| `PLAN.md` | The modularisation record |

---

*Status of this document: current as of the pixel-face and painted-sprite work. If a number here
disagrees with the code, the code is right and this is stale — fix it here.*
