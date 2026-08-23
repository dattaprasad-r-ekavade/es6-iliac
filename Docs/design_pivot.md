# Ratna Bay — Design Pivot

**Status:** The design is decided. Balance is implemented and asserted; the systems in §8 are
built and tested. What remains is a production plan and the building of mine generation, run
state, slots, amulets and succession.
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
3. **Descend.** Clear each room of preta. Deeper mines hold higher-tier preta and bosses.
4. **Camp or die.** Camp to bank what you have and end the run; push on for more. Either way
   the order continues — see §5.
5. **Each cycle unravels part of the town's story.**

---

## 3. A run

**Five to eight minutes.** Roughly three to five caves. Short on purpose: this is a first
roguelike for a player who has not played one, a death costs half your gear, and short runs mean
the story arrives in frequent pieces rather than rare ones.

That length has one consequence worth stating, because it decides how progression is tuned:
**a build cannot form inside a single run.** There is not enough time to find and combine enough
stones. So the two layers do different jobs:

- **Amulets and levels carry build identity.** They accumulate across many runs; this is where
  "my character does X" lives.
- **Stone slots are tactical variance.** What you socket answers *this cave*, not this character.

### Ending a run

A run ends when you **camp** or when you **die**.

Camp is available **at the exit of a cleared room, and nowhere else** — there is no camping
mid-fight. Camping banks what you have and ends the run there. Pushing on is worth more: **the
Nth room of a tier-T mine pays `N x T` stones**, not one.

| Rooms cleared | Banked if you camp | Next room pays | You are risking |
|---:|---:|---:|---|
| 3 | 6 | 4 | 1.5 : 1 |
| 5 | 15 | 6 | 2.5 : 1 |
| 8 | 36 | 9 | 4.0 : 1 |

The escalation is the point. A flat one-stone-per-room reward makes banking immediately the
correct play at every step, because the pot grows while the prize does not. Rising payouts keep
"one more room?" an open question all the way down.

**Dying costs** all banked stones, half your gear, and any unspent progress toward your next
level. Levels already earned are kept.

**But the body can be recovered.** Your successor's next descent into that mine finds the fallen
Deepankar's cache, once. This keeps the loss real without creating an unrecoverable state —
stones are also what opens mines, so a total wipe could otherwise leave a player unable to
descend at all. It is also simply what an order like this would do for its own.

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

Armour and weapons have **stone slots**. Socketed jiva stones add buffs and powers, and the
stones are **found below, not carried down**. You descend with your gear and its empty sockets;
what the mine gives you decides how this run plays.

### Between runs — character level

Levels come from experience earned on runs. Each level grants points to spend on skills.

This changes an existing rule and the change is deliberate: character level currently *derives
from* total skill progress, so making levels grant skill points would be circular. Level moves
onto the experience track, and the skills → level path is retired.

What is kept: skills still grow by use, and the five anti-grind rules still hold — gains come
from landed effect, scale with threat, diminish within an encounter, and magic stays
self-limiting because casting costs stones which cost gold.

### Between runs — amulets

Clearing a crawl yields **amulets**: permanent buffs that survive death and pass to your
successor. This is the ratchet that makes a losing run still worth something.

### Access — rank and jiva stones

- **Jiva stones** crack open deeper mines. Money gates depth.
- **Deepankar rank** gates the town. The town is not a capital — it is a **fort** of about
  **ten rooms**. Each successful run and the gold it earns opens **one more**, and every room
  adds to the story: the history, the past, and the truth about the jiva stones.

  Occupants will not speak to you until you have reached a certain rank or earned a certain
  sum, so access and story open together. A room is a bounded authoring unit, which is what
  keeps this from becoming the open-city problem the pivot exists to escape.

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

## 9. Balance and decisions

### Decided above

Run length, how a run ends, the banking curve, body recovery, cave themes, the fort, the class
multipliers, where stones are found, character levelling, and the content budget.

### Done: the spell-damage pass

Spell power was set when spells were a side option beside a sword, and it made the mage
pointless — a mage's own sword out-damaged their own magic. The multipliers were never the
problem; the base values were.

| Spell | was | now | shape |
|---|---:|---:|---|
| Flame | 10 | **22** | Lowest burst, highest total once it burns |
| Rime | 12 | **34** | Middle burst, and it buys distance |
| Arc | 16 | **38** | Highest burst, staggers, jumps once |
| Mend | 35 | **48** | Now beats the 40 a free potion restores |

Balanced by **whole resource bars**, because that is the unit a player spends — stamina refills
itself, prana is bought with gold:

| Path | One stamina bar | One prana reserve |
|---|---:|---:|
| Warrior | **581** | 412 |
| Mage | 363 | **660** |
| Trader | 290 | 330 |

Each path now does most with its own gift, a full reserve of prana is worth more than a full bar
of stamina, and the trader is worst at both. All of it is asserted rather than assumed.

### Content budget for first release

| | Target | Note |
|---|---|---|
| Cave themes | 5 | Colour shading, preta set, and one resisted / one feared element each |
| Preta sprites | 5–6 per theme (~27) | Generated from palette and proportions, so cheap: realistically 5–6 body shapes across 5 palettes |
| Boss encounters | 6–7 | **Budget 3 distinct behaviours**, dressed per theme. The art is cheap; the fight patterns are not, and a boss without its own pattern is only a large preta |
| Fort rooms | 10 | Opened by wins and gold. Occupants stay silent until a rank or a sum is reached |

### Settled

**Payout scales with depth, never with theme.** The Nth room of a tier-T mine pays `N x T`
stones.

| Rooms cleared | Tier 1 | Tier 2 | Tier 3 |
|---:|---:|---:|---:|
| 3 | 6 | 12 | 18 |
| 5 | 15 | 30 | 45 |
| 8 | 36 | 72 | 108 |

One variable per axis: **depth decides reward, theme decides tactics.** A cave whose element
you are poorly equipped for is already harder — it survives you for less time — so it does not
need a payout modifier on top, and giving it one would push players toward whichever theme paid
best rather than whichever they could handle.

This also sets what a mine can cost to open. A tier-3 mine can ask around 25 stones and still be
worth descending into.

**Death clears unspent progress toward the next level, not the level itself.** Levels already
earned and points already spent are kept.

The successor is a new person, trained to the standard the order has reached but not yet
promoted. It is the gentlest of the three options and the only one that cannot produce a wall: a
player who dies repeatedly stops advancing, but never goes backwards past a rank they have held.

**Camp only at a cleared room's exit.** There is no camping mid-fight.

If a player could bank the instant a fight turned, there would be no risk left to press — the
whole mechanic would collapse into "always bank when losing". Committing at the door is the
decision: once it opens you are in that room until it is clear or you are not.

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
