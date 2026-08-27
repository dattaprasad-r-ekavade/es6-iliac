# Ratna Bay — Where the Story and the Systems Meet

**Status:** proposed. [`STORY.md`](STORY.md) says what the story is and how a roguelite paces
one. This says where it should stop being told and start being *played*.

**Why it exists.** Iteration 19 put ten rooms of story into the game and, in doing so, made a
gap obvious: the story is delivered entirely *beside* the mechanics. NPCs say things. The player
does something else. Everything below is an attempt to make those the same activity —
**without adding a verb**, because a moral question that costs the player a button press is a
moral question they will learn to skip.

---

## 1. Three findings, from reading the code rather than the design

### The moral spine has no mechanic at all

[`SETTING.md`](SETTING.md) §3 says prana is gathered three ways and that **"the difference
between them is the moral spine of the story."** [`TRAILER.md`](TRAILER.md) goes further:
*"Prana is the mana bar, the town's economy, and a moral question at the same time."*

Mechanically, today, the player's entire relationship with prana is:

```
gold  →  buy a jiva stone  →  spend it on a spell
```

The theme of the game — the quest for power corrupts — is currently a thing other people say
while the player shops.

### The second tap is empty

`design_pivot.md` §7 promises two: **descending reveals what the mountain holds; rising in rank
reveals what the town is hiding.** Iteration 19 filled the second with twenty-four fragments.

The first has **nothing in it.** The Stambha exists, is carved, is transliterated into Brahmi,
and appears only behind the `--stambha` flag — it has never been placed in a mine. A player can
descend to room thirty and learn precisely nothing about the mountain.

### `Channeled` is a ledger nothing reads

`PlayerVitals.Channeled` counts every jiva stone drawn. It is incremented, saved, restored, and
exposed to dialogue as `player.channeled`. **Nothing consequential reads it.**

That is the whole feature, already built and inert.

---

## 2. The answer: consumption destabilises the mountain

**No new verb.** The player already spends stones — that is the game. The change is that
spending has a consequence, and the consequence arrives where the player feels safe.

> Prana is what holds the dead down. Every stone burned is a stone that is no longer holding
> anything. Burn enough and they stop staying in the mine.

This is better than making the player choose *how* to take prana, and it is worth being explicit
about why: a choice at the point of consumption is a decision the player makes a hundred times
and optimises within an hour. A consequence of consumption is a pressure they feel constantly
and can never fully escape, because the only way to avoid it is to stop using the thing the
whole game is built around.

### Instability, as a number

One value, derived from what is already counted:

```
instability  ←  stones burned, less what has decayed back
```

What it does, in ascending order of how alarming it should feel:

| Instability | What changes |
|---|---|
| Low | Nothing. The fort is a fort |
| Rising | The lamps gutter. Occupants mention it before you notice it |
| High | **Chhaya in the fort corridors.** The safe room is not |
| Critical | Rooms are *held*. The occupant is not there, and the story in that room is unreachable until it is cleared |

**The last row is the one that makes it a system rather than a hazard.** Losing a fort room
costs the player *story* and *services* — the smith, the assayer, the physician — so instability
is not a fight, it is an interruption to everything else they were doing. That is a far better
threat than damage.

### Why this is cheap

Every piece exists. `Channeled` is the counter. `FortRoster` is the ten rooms. `EnemyCatalog`
has three tiers of risen. The generator already places them. What is missing is one number and
the wiring between them.

---

## 3. The half-life, and where to put it

A jiva stone leaks. That is good lore and good pressure, and it belongs to the **fort**, not to
the run.

### Not inside a descent

`design_pivot.md` §3 is unambiguous: **"the run has to end because the player stopped, not
because the game did."** A decaying stone in the pot makes the clock answer *"one more room?"*
instead of nerve, and that question is the entire game.

There is also no tuning that works. A run is five to eight minutes: leak fast enough to matter
and every run becomes a rush, leak slow enough not to and it is invisible. And it would dissolve
the `N x T` payout curve, because a pot that is worth less than it says is a promise the summary
screen is breaking.

### In the fort, where hoarding happens

**Banked stones leak between descents.** Slowly, visibly, and only while sitting still.

This aims the pressure at the behaviour that actually needs discouraging. The failure mode of a
press-your-luck game is the player who banks early every time and grinds safely; a stockpile
that quietly evaporates makes that strategy cost something without ever touching the decision at
the door.

It also closes the loop with §2 — **you cannot hoard safety.** Stones spent destabilise the
mountain; stones kept leak away. There is no position from which the player is simply fine, and
the only answer to either is to go back down.

### The one exception worth considering

**Socketed stones burning out mid-run** is the version that *can* live inside a descent, because
socketed stones are already temporary and already cleared on entry — there is no payout to
break. A stone leaking its prana into your weapon and going dark two rooms later is tension with
no cost to the loop. Worth trying after the fort version is in.

---

## 4. The order, and why it should be the one talking

Story delivered as **the Dipadhara managing a failing supply**, with the player as their agent.

The fort's twenty-four fragments are good and they are passive: the player reads them. Giving
the order a problem, and the player a part in it, turns a fort visit from reading into doing —
and it makes the order a character rather than a background fact.

The shape, roughly every one or two descents:

1. The order notices the shortfall. You are sent to the **trader** to argue the price down.
2. You have to **explain the shortage** — to the registrar, or to the physician, and what you
   say is a choice with a record.
3. The **armourer** wants supplies for the order that the province does not want to give.
4. **The fort is overtaken.** Rooms fall and you are sent to clear them. This is where §2 stops
   being atmosphere.
5. You begin **asking the royalty questions** they do not want asked.
6. You decide the fate.

**One rule that keeps this from becoming what the pivot escaped.** `design_pivot.md` §7 says the
story is not a questline, and what it is actually warning against is errands that consist of
walking somewhere. Every step above must be **a conversation with a decision in it** — negotiate,
choose what to admit, choose who to protect — and never a delivery. If a step could be completed
by a courier, it should be cut.

---

## 5. Giving the mountain a voice

The empty tap, and it stays cheap.

**The Stambha, in every mine.** One pillar deep in a generated mine, carrying a verse and the
count of what has been burned. `SETTING.md` already says the pillar *"measures what has been
drawn"* — so the carving becomes the instability read-out, in the world, with the player's own
number on it. The verse stops being decoration and becomes an accusation with an integer
attached. Every asset for this exists and is currently used by nothing.

**The risen are the record.** `SETTING.md` says they are the past lives the mountain took —
miners, soldiers, the convict labour the state sent down in earlier reigns. A killed chhaya can
leave one line of who it was:

> *"Forty of us went in on the governor's order. The register says twelve."*

No new verb; it arrives on a kill the player was making anyway. At the measured ~700 words an
hour, fifty of these is an afternoon.

---

## 6. Plot placement

The acts from `STORY.md` against the systems that exist. The conjunction rule still holds:
**rank and depth, never either alone.**

| Beat | Fires on | Written? |
|---|---|---|
| Act I opens | First descent | ✅ the coach |
| The tally does not balance | `Atala` + depth 3 | ✅ `gate.1` |
| **Turn I — the supply does not add up** | `Talatala` + depth 10 | ✅ `phys.1` |
| The order was founded to stop this | `Sutala` + depth 8 | ✅ `hall.2` |
| A stone taken is your life | `Rasatala` + depth 18 | ✅ `reg.2` |
| **Turn II — the first Dipadhara** | `Patala` + depth 26 | ✅ `gov.2` |
| Act III — the choice | `Patala` + depth 30 | ✅ `gov.3` |

**The acts are already placed.** What the turns lack is consequence — each is a line that changes
nothing about what the player can do next. Each should hand something over:

- **Turn I** — the supply is admitted to be short — starts the **errand chain** in §4. The story
  stops being something you read and becomes something you are sent to do.
- **Turn II** — the first Dipadhara is named — opens **the seam**: one fixed descent to the
  bottom. `STORY.md` §10 asks whether a hand-authored level ever earns its cost. This is the one
  place, and one is enough.

---

## 7. The finale: the fort falls

The endgame is **the fort itself, overrun**, cleared room by room against a clock.

It is the right climax for three reasons that have nothing to do with spectacle:

- **It is the theme, literally.** The province burned prana to keep its lamps lit, and the thing
  that comes for it is what the burning let up. The player did most of the burning.
- **It inverts the one safe place**, which is the strongest structural move a hub-and-run game
  has available and almost nobody uses.
- **It reuses ten rooms that already exist as data**, and it plays differently from every run
  before it — a clock and a defence rather than a descent and a decision. A finale that plays
  like the ninetieth run is not a finale.

**The honest cost:** the fort is currently a corridor of doors, not geometry. This is the piece
that needs it built, and it is the largest single item in this document. Nothing else here does.

---

## 8. The endings, earned rather than chosen

`STORY.md` §10 leaves this open. It can be closed now, because the counters exist.

| Ending | Earned by | Read from |
|---|---|---|
| **The Ledger** — expose it | Evidence, and standing enough to be believed | Heard `clerk.1` and `clerk.2`; `Patala` |
| **The Lamp** — take it over | Having burned through it without ever asking | High lifetime consumption |
| **The Release** — break the stones | Having kept the mountain steady at your own cost | Low instability at the end |

Nobody is told which one they are walking toward. At the bottom the doors that open are the ones
a hundred small decisions opened, and the one that does not is the one they argued themselves
out of ten hours ago without noticing.

**A player who qualifies for none gets The Lamp.** That is not a punishment; it is the honest
answer for somebody who never committed to anything.

---

## 9. Your own dead

Still unbuilt, still the strongest single idea available, and cheaper than when `STORY.md`
proposed it: `Legacy.Fallen` exists, the vetala tier exists, the generator places elites.

> A fallen Dipadhara whose cache is never recovered rises, in the mine where they fell, wearing
> what they died in and named for who they were.

And it should read the ledger: **a predecessor who burned heavily comes back as something
worse.** The order's own consumption, buried in the mountain, coming back up at whoever
inherited it. The theme, as a spawn rule.

---

## 10. What to build, and in what order

Cheapest and most load-bearing first. Nothing before item 6 needs a new subsystem.

1. **Instability, as one number** derived from consumption.
2. **The fort reacts to it** — lamps, then lines, then chhaya in the corridor.
3. **Rooms can be held**, and a held room costs its story and its services until cleared.
4. **The Stambha in every mine**, carrying the count. Assets exist and are unused.
5. **Banked stones leak** between descents.
6. **The errand chain**, starting at Turn I.
7. **Endings read the ledger.**
8. **The seam** — one hand-authored descent, from Turn II.
9. **The fort finale** — the expensive one, and the only item here that needs geometry.
10. **Your own dead rise**, tiered by what they burned.

Items 1 to 4 are the ones that matter. They make consumption cost something, give the mountain a
voice, and turn an inert counter into the spine the design has always claimed it had.

---

## 11. Open

- How fast should banked stones leak? Fast enough to notice across three runs, slow enough that
  a player who is simply saving for a sword is not robbed. Needs a number and a playtest.
- Does clearing a held fort room pay anything, or is getting the room back the whole reward?
  Probably the latter — paying for it would make instability farmable.
- Can instability ever be *reduced* deliberately, or only decay on its own? A way to spend
  stones on calming the mountain is elegant and risks becoming a chore tax.
- What the first Dipadhara actually found. Named in `gov.2`, and nothing behind it is written.
