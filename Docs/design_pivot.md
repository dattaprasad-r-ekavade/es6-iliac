# Ratna Bay — Design Pivot

**Status:** Loop, run length, classes and progression are decided. A spell-damage pass and four
smaller questions remain — see §9.
**Supersedes:** the vertical-slice product definition in `PRODUCTION_PLAN.md`. The engineering
decisions in that document (engine, sprites, domain separation, publish gates) still stand.

---

## 1. The pitch

*A mining town digs up stones that raise the dead. You are hired to go down and clear them.*

Ratna Bay is a heavily taxed, heavily guarded mining town on a river. It mines **jiva stones**
from the mountains. Empty, they are only crystals. Filled, they hold **prana** — and prana is
what the whole town runs on.

Prana can be gathered several ways, and the difference between them is the moral spine of the
story:

- Released by the almost-dead, and captured as it goes.
- Drip-fed from the living, leaving them half dead.
- Taken from animals, which barely works.

Down in the mines, stones left in the rock give up their prana to the dead. **Preta** rise from
it — the past lives of dead miners, warriors, and whoever else the mountain has taken. Mining is
therefore lethal work, and the town hires people to go down and clear the caves before the
miners follow.

You are a stranger who joins the **Deepankar** — the Light-givers — the order that does that
clearing.

---

## 2. The core loop

```
        ┌──────────────────────────────────────────────┐
        │                                              │
   Town ──▶ pay jiva stones to open a mine ──▶ Descend ──▶ Clear caves of preta
        │                                              │        │
        │                                              │        ▼
        └───── rank, story, gear, trade ◀── Return ◀───┴── Set camp / die
```

1. **In town.** Trade, take work, spend what you brought back, and rise in the order.
2. **Open a mine.** Deeper mines must be cracked open with jiva stones, which cost money. You
   spend the thing you go down to collect.
3. **Descend.** Clear each cave of preta. Early mines are simple: clear the preta, set camp at
   the end, done. Deeper mines hold higher-tier preta and bosses.
4. **Return or die.** Either way, the order continues — see §5.
5. **Each cycle unravels part of the town's story.**

---

## 3. A run

**Five to eight minutes.** Roughly three to five caves. Short on purpose: this is a first
roguelike for a player who has not played one, a death costs half your gear, and short runs mean
the story arrives in frequent pieces rather than rare ones.

That length has one consequence worth stating, because it decides how progression is tuned:
**a build cannot form inside a single run.** There is not enough time to find and combine enough
stones. So the two layers do different jobs:

- **Amulets carry build identity.** They accumulate across many runs; this is where "my
  character does X" lives.
- **Stone slots are tactical variance.** What you socket answers *this cave*, not this character.

### Caves

Every cave is themed, and the theme is mechanical rather than decorative: different colour
shading, different preta sprites, and a different elemental table.

| Cave | Preta behaviour |
|---|---|
| Lava | Highly resistant to Flame |
| Water | Take double damage from Arc |
| *(others as authored)* | Each names one element it shrugs off and one it fears |

Two rules keep this a decision instead of a punishment:

1. **Resistance, never immunity.** A heavily resistant preta takes reduced damage, not none. A
   player whose only offence is Flame must still be able to finish a lava cave, badly.
2. **The element is shown before the player pays to open the mine.** Entry already costs jiva
   stones, so the information belongs at the point of payment. Choosing which cave to buy into,
   knowing what is down there, is the decision the whole loop rests on.

---

## 4. Life paths

The three life paths are the starting classes. They already exist in the codebase as
`route.warrior`, `route.mage` and `route.trade`.

| Path | Weapons | Spells | Trade prices | Shape of the curve |
|---|---:|---:|---|---|
| **Warrior** | 2× | 1.25× | x | Strongest first, hardest last |
| **Mage** | 1.25× | 2× | x | Fragile, compounding, better every run |
| **Trader** | 1× | 1× | **x⁰·⁷⁵** | Weakest fighter, strongest buyer |

The trader wields both weapons and spells and is simply given no inherited gift with either.
What it has instead is price, and price compounds:

| List price | Warrior / mage pay | Trader pays | |
|---:|---:|---:|---|
| 12 | 12 | 6 | 54% |
| 80 | 80 | 27 | 33% |
| 1,000 | 1,000 | 178 | 18% |
| 20,000 | 20,000 | 1,682 | 8% |

Negligible early, decisive late — which is the intended shape, and why the trader is the fastest
route through the story rather than the easiest fight.

Each path is a different difficulty curve rather than a different power level.

---

## 5. Death and succession

**You do not resurrect. You are replaced.**

When you die in a mine, another Deepankar takes up the work. The new one inherits:

- **All amulets.** Everything permanent carries over.
- **Half your gear.** The rest stays in the mountain.

This is why the order exists in the fiction, and it is also the meta-progression: the player
keeps enough to feel a run mattered, and loses enough for a death to sting.

---

## 6. Progression

Three layers, deliberately separate.

### Within a run — stone slots

Armour and weapons have **stone slots**. Socketed jiva stones add buffs and powers. This is where
run-to-run variety lives: what you find decides what you can do this run, not how large your
numbers are.

### Between runs — amulets

Clearing a crawl yields **amulets**: permanent buffs that survive death and pass to your
successor. This is the ratchet that makes a losing run still worth something.

### Access — rank and jiva stones

- **Jiva stones** crack open deeper mines. Money gates depth.
- **Deepankar rank** gates the town. The town is not a capital — it is a **fort**. Each
  successful run and the gold it earns opens **one more room**, and every room adds to the
  story: the history, the past, and the truth about the jiva stones.

  A room is a bounded authoring unit, which is what keeps this from becoming the open-city
  problem the pivot exists to escape. The design has no ceiling; the *first release* should
  name one, so there is a shippable state.

---

## 7. Story delivery

The story is not a questline. It is released in pieces, by two taps:

- **Descending** reveals what the mountain holds.
- **Rising in rank** reveals what the town is hiding.

A player who only ever fights still reaches the end eventually. A player who trades and climbs
gets there faster. Neither is locked out.

---

## 8. What this reuses

The pivot changes the product, not the foundations. Already built and tested:

- Combat, guarding, weapon classes, enemy levels and scaling
- Five spells with distinct effects, prana economy, jiva stones as consumable charge
- The three life paths and the eight use-based skills
- Collision, doors, locks, stealth, pickpocketing
- Dialogue, quests, shops, saves, the world manifest format and its hot reload
- The publish pipeline and its gates

What is genuinely new: mine generation, run state, the slot and amulet systems, and the
succession-on-death rule.

---

## 9. Balance notes and open questions

### Decided

Run length, cave themes, the fort, and the class multipliers above are settled.

### Needs a numbers pass before it will play

**Spell power is too low for the mage to be a class.** The multipliers are not the problem; the
base values are, because they were set when spells were a side option rather than an identity.

| | base | warrior | mage |
|---|---:|---:|---:|
| Iron sword | 18 | **36** | 22 |
| Greatsword | 34 | **68** | 42 |
| Flame | 10 | 12 | **20** |
| Arc | 16 | 20 | **32** |

A warrior swinging an iron sword deals ~80 damage per second and spends only stamina, which
regenerates. A mage casting Flame deals ~15 per second including burn, and spends prana, which
is bought with gold. Range does not cover a five-fold gap. **Spell base power needs roughly a
2–3× lift of its own** before class multipliers are applied.

**`x⁰·⁷⁵` welds the trader discount to the current gold scale.** The exponent is applied to a
number with units, so rescaling prices — gold to copper, or an inflation pass — silently changes
every discount. The formula is fine; the gold scale is now load-bearing and must not be
rescaled casually.

### Still open

1. **What ends a run besides dying?** Setting camp is stated for early mines. Is that the exit
   everywhere, or do deeper mines end on a boss?
2. **What carries into a run, and what is found in it?** Are socketed stones taken down, or
   found below?
3. **How many rooms does the fort have at first release?**
4. **How many cave themes at first release?** Each is cheap, but each needs sprites and a table.

## 10. Reference points

Worth playing, each for one specific answer.

| Game | What to take from it |
|---|---|
| **Asura** | Indian indie roguelike that replaced a skill tree with a Janam Kundli. Precedent for making the progression system culturally specific rather than generic. |
| **Hades** | Story told across repeated runs, with a hub that reacts. The structure this design is reaching for. |
| **Soul Sacrifice** | Magic paid for with life force, and a Save/Sacrifice alignment. The closest existing answer to what prana harvesting should cost. |
| **Delver** | First-person sprite-in-3D roguelike. Nearly this game's exact form; play it to see where the loop gets thin. |
| **Devil Spire** | A tiny team shipped this and its sequel. Proof of the scale. |
| **Dead Cells** | Between-run unlocks that grant options rather than numbers. |

**Where this design is differentiated:** the sprite-in-3D roguelike niche is proven but its
games are almost all generic fantasy with the setting as wallpaper. A coherent world, a resource
economy that means something, and a story that unfolds across runs is the gap none of them fill.
