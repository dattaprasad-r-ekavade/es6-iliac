# Ratna Bay — Where the Story and the Systems Meet

**Status:** proposed. [`STORY.md`](STORY.md) says what the story is and how a roguelite paces
one. This says where it should stop being told and start being *played*.

**Why it exists.** Iteration 19 put ten rooms of story into the game and, in doing so, made a
gap obvious: the story is delivered entirely *beside* the mechanics. NPCs say things. The player
does something else. Everything below is an attempt to make those the same activity.

---

## 1. Three findings, from reading the code rather than the design

### The moral spine has no mechanic at all

[`SETTING.md`](SETTING.md) §3 says prana is gathered three ways — caught from the almost-dead,
drip-fed from the living, or taken from animals — and that **"the difference between them is the
moral spine of the story."** [`TRAILER.md`](TRAILER.md) goes further: *"Prana is the mana bar,
the town's economy, and a moral question at the same time."*

Mechanically, today, the player's entire relationship with prana is:

```
gold  →  buy a jiva stone  →  spend it on a spell
```

There is no choice in that, and therefore no moral question. The spine is something the fort's
occupants *tell you about*. The theme of the game — the quest for power corrupts — is currently
a thing other people say while the player shops.

### The second tap is empty

`design_pivot.md` §7 promises two: **descending reveals what the mountain holds; rising in rank
reveals what the town is hiding.** Iteration 19 filled the second one with twenty-four fragments.

The first has **nothing in it.** The Stambha exists, is carved, and appears only in the
`--stambha` preview — it has never been placed in a mine. A player can descend to room thirty
and learn precisely nothing about the mountain.

### The endings cannot currently be earned

[`STORY.md`](STORY.md) §10 leaves this open: *"whether the three endings are chosen or earned —
a menu at the end is weak; an ending you have been walking toward for ten hours without being
told is much stronger and much harder."*

It is only harder if nothing is tracking. Something already is — see below.

---

## 2. The missing verb: taking prana yourself

One mechanic closes the first two findings at once.

**A dying preta is releasing prana.** That is the fiction already: stones left in the rock give
up their prana to the dead, and the dead rise on it. When one falls, that prana goes somewhere.
The Deepankar are named light-*givers* and are, in fact, in the business of collecting it.

So give the player the three methods as an actual decision:

| Method | How it plays | What it costs |
|---|---|---|
| **Release** | Hold on a dying chhaya for a beat and take what leaves it | Time, in a room that may not be clear. The lawful way, and the slow one |
| **Draw** | Take it from someone living | Nothing you can see. Far more, far faster |
| **Animals** | The rats in the deep rooms | Almost nothing. There for the player who tries it once |

**Release is the interesting one mechanically**, because it fights the press-your-luck loop
directly: standing still over a body while three more things are getting up is a real risk, and
it is a risk taken for the *virtuous* option. That inversion — the honest way is the dangerous
way — is worth more than any amount of dialogue about ethics.

### Who is there to draw from

This is the part that makes it land, and the answer is already built.

**The camp trader.** They are whistled down for money, into a mine, alone, at the player's
convenience. Nobody above ground knows they came. The board already calls the camp *"the only
place a person is met every single run"* — which makes them simultaneously the natural home for
frequent story **and** the standing temptation.

The same NPC, two purposes, and they sharpen each other: the more the trader talks, the more
they are a person, and the worse it is to drain them.

---

## 3. `Channeled` is already the ledger

`PlayerVitals.Channeled` counts every jiva stone drawn. It is incremented, saved, restored, and
exposed to the dialogue system as `player.channeled`.

**Nothing reads it consequentially.** It is a moral ledger with no consequences attached — which
means the expensive half of this feature is already built and merely inert.

What it should feed:

- **The Stambha.** `SETTING.md` says the pillar *"measures what has been drawn."* Put one in
  every mine and carve the count into it. The verse — *covet not; for whose is wealth?* — stops
  being a decoration and becomes a number with the player's name on it. This is also the
  trailer's opening shot finally existing in the game.
- **The shrine.** The priest's fragments should read differently at 20 drawn than at 200.
- **What rises.** See §6.
- **Which endings are available.** See §5.

Splitting `Channeled` into *released* and *drawn* is the only new state this needs, and it is
one integer.

---

## 4. Giving the mountain a voice

The empty tap. Three sources, cheapest first:

**The Stambha, per mine.** One pillar, deep in a generated mine, carrying a verse and a count.
Costs nothing new — the carving generator, the Brahmi transliteration and the shot composition
all exist and are used by nothing.

**Released preta.** `SETTING.md` says the risen are *"the past lives the mountain has taken —
miners, soldiers, the convict labour the state sent down in earlier reigns."* **The preta are
the record.** A released chhaya can give one line of who it was:

> *"Forty of us went in on the governor's order. The register says twelve."*

That is history, testimony, and evidence — delivered by the thing the player just killed, and
only to a player who took the slow honest route. **The mine's story is the reward for releasing
rather than draining**, which welds the theme to the verb.

**Depth as a gate on its own.** Deeper fragments name older things: the convict gangs, then the
first Deepankar, then whatever was down there before the province.

---

## 5. Plot placement

The acts from `STORY.md`, against the systems that now exist. The rule from §6 of that document
still holds — **conjunctions, never either tap alone.**

| Beat | Fires on | Already written? |
|---|---|---|
| **Act I opens** | First descent | The coach |
| The tally does not balance | `Yukta` + depth 3 | ✅ `gate.1` |
| **Turn I — the supply does not add up** | `Pradeshika` + depth 10 | ✅ `phys.1` |
| The order was founded to stop this | `Sthanika` + depth 8 | ✅ `hall.2` |
| The law: a stone is your life | `Adhyaksha` + depth 18 | ✅ `reg.2` |
| **Turn II — the first Deepankar** | `Mahamatra` + depth 26 | ✅ `gov.2` |
| **Act III — the choice** | `Mahamatra` + depth 30 | ✅ `gov.3` |

**The acts are already placed.** What they lack is consequence: every one of those turns is a
line of dialogue that changes nothing about what the player can do next.

### Make each turn hand over a mechanic

This is the change worth making, and it is what turns plot into play:

- **Turn I** — the physician admits the supply is drip-fed — **unlocks Draw.** Until that
  conversation the player *cannot* take prana from the living, because they do not know it is
  possible. The moment complicity becomes available is the moment the story says it exists. A
  player who never reaches that fragment plays an entirely honest game without ever being told
  they were being tested.
- **Turn II** — the governor names the first Deepankar — **unlocks the seam.** A specific
  descent, at a fixed seed, to the bottom. `STORY.md` §10 asks whether Act III is playable and
  whether a hand-authored level earns its cost; this is the one place it does, and only one is
  needed.

---

## 6. The endings, earned rather than chosen

`STORY.md` leaves this open. It can now be closed, because the counters exist.

| Ending | Earned by | Read from |
|---|---|---|
| **The Ledger** — expose it | Having the evidence, and the standing to be believed | Heard `clerk.1` and `clerk.2`; `Mahamatra` |
| **The Lamp** — take it over | Having already been doing it | High **drawn** count |
| **The Release** — break the stones | Having done it the hard way throughout | High **released**, low **drawn** |

The player is never told which one they are walking toward. At the bottom, the doors that open
are the ones their own hundred small decisions opened — and the one that does not open is the
one they argued themselves out of ten hours ago without noticing.

**A player who qualifies for none gets The Lamp**, which is the ending for someone who never
committed to anything. That is not a punishment; it is an answer, and it is the truest one.

---

## 7. Your own dead

Still the strongest single idea available, still unbuilt, and now cheaper than when
[`STORY.md`](STORY.md) proposed it: `Legacy.Fallen` exists, the vetala tier exists, and the
generator already places elites.

> A fallen Deepankar whose cache is never recovered rises, in the mine where they fell, wearing
> what they died in and named for who they were.

And it should read the ledger. **A predecessor who drew heavily comes back as something worse** —
a kravyada rather than a vetala. The order's own corruption is buried in the mountain and comes
back up at whoever inherited it. That is the theme, as a spawn rule.

---

## 8. What to build, and in what order

Cheapest and most load-bearing first. None of it is large; most of it is wiring things that
already exist to each other.

1. **Split `Channeled` into released and drawn.** One integer, and everything below reads it.
2. **Release, as a verb.** Hold on a dying chhaya. This alone changes how the loop is played.
3. **Preta testimony.** One line per release, from a pool. At ~700 words an hour, fifty of them
   is an afternoon.
4. **The Stambha in every mine**, carrying the drawn count. Assets already exist and are unused.
5. **Draw**, gated behind Turn I. The camp trader is the target.
6. **Endings read the ledger.**
7. **The seam** — one hand-authored descent, unlocked by Turn II.
8. **Your own dead rise**, tiered by what they drew.

Items 1 to 4 are the ones that matter. They give the mountain a voice, make the honest route the
dangerous one, and turn an inert counter into the spine the design has always claimed it had —
and they can be done without writing a single new subsystem.

---

## 9. Open

- Does drawing from the camp trader remove them for the rest of the save? It should cost
  something permanent, and losing the only person who visits you is the obvious candidate.
- Is Release automatic once learned, or always a held button? Automatic is kinder; held is a
  decision, and this design generally prefers decisions.
- Does the fort *notice* a heavy ledger before the ending — a garrison that watches you, a
  shrine that closes? Probably yes, and probably as one changed line rather than a system.
- What the first Deepankar actually found. It is named in `gov.2` and nothing behind it is
  written yet.
