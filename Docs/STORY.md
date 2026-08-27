# Ratna Bay — The Story, and How a Roguelite Tells One

**Status:** proposed. The shape is argued for; the wording is not written. Close it once, then
author against it.

**Depends on** [`SETTING.md`](SETTING.md) for the world and [`NAMES_AND_OFFICES.md`](NAMES_AND_OFFICES.md)
for what people are called. **Constrained by** [`design_pivot.md`](design_pivot.md) §7, which
already decided the delivery mechanism, and [`PRODUCTION_PLAN.md`](PRODUCTION_PLAN.md) §1, which
this does not reopen.

---

## 1. What kind of game is telling this story

**Ratna Bay is a roguelite, not a roguelike.** Amulets, levels, half your gear and your rank all
survive death. That is not a technicality; it decides everything below.

The consequence worth accepting deliberately: **individual runs will not be memorable.** A run is
five to eight minutes and, by §3 of the design, too short for a build to form inside. Nobody will
tell a story about run 47. They will tell stories about the character they assembled across forty
runs, and about the night they pushed to room nine and lost the lot.

So the story cannot live inside a run. It lives in the space between runs, and the run is the
clock that advances it.

---

## 2. The structural problem

A story wants a **sequence**. A roguelite delivers **repetition with variance**. Beats cannot be
assumed to arrive in order, at a fixed pace, or at all.

Three answers exist in the genre, and only one fits this game:

| Approach | Example | Fit |
|---|---|---|
| Story is optional lore in item text and murals | *Dead Cells* | Wastes the setting entirely |
| Fragments found in runs, assembled non-linearly | *Returnal* | Works, but nothing reacts to you |
| **A hub that reacts, gated by run state** | *Hades* | **This one** |

`design_pivot.md` §7 already chose the third and added a second axis to it: **descending reveals
what the mountain holds; rising in rank reveals what the town is hiding.** That is the right
instinct and this document builds on it rather than replacing it.

---

## 3. Why the player keeps going back — and the thing being wasted

The strongest roguelite stories make the repetition **diegetic**. Hades: you are a god and cannot
stay dead. Returnal: a time loop.

Ratna Bay's answer is **succession**, and it is better than either, because it costs the fiction
something. You do not respawn. Somebody else picks up the lamp.

Right now that is a rule in a design document. It should be a scene.

### Successors are people

Each new Dipadhara arrives with **one line about who they were before the order**. That is the
whole feature.

> *"I dug this mountain nine years. I know what is down there."*
> *"My brother went down with the last one. He did not come up."*
> *"The registrar found me a debt I could not pay. This is how it is paid."*

Cheap to author, and it converts dying from a pure cost into a piece of content. A player who is
losing gets *more* story, not less, which is exactly the right way round.

### Your predecessors come back

The mountain raises the dead. The order's dead are in the mountain. Therefore:

> **A fallen Dipadhara whose cache is never recovered eventually rises.**

The design already has body recovery — once, on the next descent. The natural extension is that
declining it, or dying before reaching it, has a consequence with a face on it. Meeting a
**vetala wearing your predecessor's gear** is a mechanic, a plot point, and the theme in one
object.

This is the single strongest idea available to this game and it costs one flag and one spawn rule.

---

## 4. The arc

Three acts. The turns are the load-bearing parts; everything else is dressing that can be
rewritten freely.

### Act I — The job

Clear caves, get paid, rise a rank. The town works. You are learning an economy, and the economy
seems fine.

The Stambha verses are in the mines from the first descent, and mean nothing yet. That is
deliberate: they have to be furniture before they can be an accusation.

> **Turn:** the lawful supply does not add up. The physician buys prana and will not say from
> whom. The tally in the registrar's room does not match what the order has actually brought up.

### Act II — The complicity

You are the supply chain now, and the town treats you as somebody worth talking to. Each rank
opens a room and each room admits a little more.

The verses start reading as accusation rather than decoration. *Covet not — for whose is wealth?*
is carved by the state, in a mine the state opened, to extract wealth.

> **Turn:** deep in the mountain, the first Dipadhara. The order was founded to **stop** this, and
> the state captured it — not by force, but by making it the most reliable employer in the
> province.

### Act III — The choice

The truth is now yours to do something with, and every option costs somebody.

| Ending | What you do | What it costs | Natural path |
|---|---|---|---|
| **The Ledger** | Expose the drip-feeding to the capital | The province is shut down. People who did nothing wrong starve | Trader |
| **The Lamp** | Take the trade over and run it better | You become the thing. It is genuinely better, for a while | Warrior |
| **The Release** | Break the stones. End the economy and the risen together | Everything. The town has nothing else | Mage |

The three map onto the existing life paths without being locked to them: the trader has the
leverage, the warrior has the force, the mage understands prana well enough to end it. **Same
content, three doors** — and a player who wants the ending their path does not favour can still
reach it, harder.

---

## 5. The arithmetic, which is the part that should worry you

Twenty hours, at six minutes a run plus roughly two in town, is about **150 cycles**.

Fifteen major beats across 150 runs is **one beat every ten runs**. A player would go an hour with
nothing new said to them. That is how a story-driven roguelite dies: not by having a bad story,
but by having a correctly-written one delivered too slowly to feel present.

Hades answers this with hundreds of short lines — something small nearly every run, majors
occasionally. So this needs **two tiers of writing**, and they have very different costs.

| Tier | Count | Length | Trigger | Cost |
|---|---:|---|---|---|
| **Majors** | ~15 | A scene | Conjunctions (below) | Expensive, authored once |
| **Reactions** | 150–250 | One or two lines | Cheap conditions | The real workload |

Reactions are things like: a shopkeeper noticing you went deeper than usual, a guard mentioning
the tax convoy is late, the assayer commenting on a run where you banked nothing, a successor's
line about who they were.

**The reaction pool is the actual authoring project.** The majors are a fortnight. The reactions
are the thing that decides whether this game feels alive, and they land squarely on iteration 19 —
which `PRODUCTION_PLAN.md` says exists to measure *hours of authoring per hour of play*. That
number is now the most important unknown in the schedule, and this is why.

---

## 6. Trigger rules

Four rules, and the first is the one that gets broken by accident.

**1. Fire on conjunctions, never on a single condition.**

```
rank >= 3  AND  deepest_depth >= 2      →  beat fires
rank >= 3  OR   deepest_depth >= 2      →  wrong
```

With `OR`, players find whichever tap is cheaper and drain the whole story through it, and the
other half of the game stops mattering. Both taps have to stay live, which is the entire point of
having two.

**2. A losing run must still advance something.** Succession makes this natural. If a player can
stall the story by being bad at the game, they quit — and the players most likely to stall are
exactly the ones this game is meant for, since §3 says it is a first roguelike for somebody who
has not played one.

**3. Majors fire in town, never mid-descent.** A run cannot be saved out of by design. A scene
that arrives mid-fight is either skipped or resented.

**4. One major per return, at most.** Two beats in one visit reads as a cutscene dump and burns
two triggers for one memory.

---

## 7. What this needs from the code

Less than expected. `StoryDirector` already exists in the domain, is engine-free, and is already
written to the save.

**Reusable as-is:** the flag store (`SetFlag` / `HasFlag` / `FlagValue`), `RecordChoice`, the
route selection the life paths already read, and `KnownTopics`, which the dialogue system uses.

**Needs adding:** a beat table with conjunction conditions, and the counters those conditions read
— runs completed, deepest depth reached, successors buried, stones banked lifetime. Most of those
already exist somewhere in `RunState` and need persisting rather than inventing.

**Dead weight from the pre-pivot game**, to be deleted rather than carried: `CompanionState`,
`KingOutcome`, `RulerId`, `GrantedTitle`, `SkippedCinematics`. They describe a game that no longer
exists, and every one of them is a field somebody will one day try to make sense of.

Per the working rules, the beat table arrives with its conditions asserted as tests — in
particular that no beat can fire on a single tap, and that a player who never wins still reaches
Act II.

---

## 8. Does it end?

**Yes, and this is the strongest commercial argument in the document.**

The successful roguelites nearly all have endings: Hades, Dead Cells, Returnal, Risk of Rain 2,
Slay the Spire. The shape they share is **an ending for the story, and a ladder for the people who
want a hundred hours.**

Four reasons it matters more for this project than for most:

- **Reviews.** "I finished it" produces a positive review. Endless games accumulate *"gets
  repetitive"*, and for a solo first title, review sentiment is most of your discovery.
- **Scope control.** An ending is a definition of done. Endless is a treadmill that has to be fed
  forever, by one person.
- **Differentiation.** In a genre where most games have no story, a story that *resolves* is the
  thing coverage is written about.
- **The promise is already made.** `design_pivot.md` §7 says a player who only fights "still
  reaches the end eventually." Not delivering that is worse than never having said it.

**Target: 15–25 hours to an ending.** Then the ladder — tier 4, 5, 6, the mountain does not stop —
which is nearly free, because the content is generated. New Game Plus carries amulets and starts
the fort closed.

---

## 9. Decided here

- Roguelite, and the meta is where identity lives.
- Story lives between runs; the run is the clock.
- Succession is the diegetic reason for repetition, and successors get a line.
- Unrecovered dead rise, wearing what they died in.
- Three acts, two turns, three endings mapped to the three life paths.
- Beats fire on conjunctions of rank and depth, in town, one per return.
- Two tiers of writing, and the reaction pool is the real project.
- The game ends, in 15–25 hours, with a depth ladder after it.

## 10. Open

- Names for the province, the governor, and the first Dipadhara.
- Whether the three endings are chosen or earned — a menu at the end is weak; an ending you have
  been walking toward for ten hours without being told is much stronger and much harder.
- Whether Act III is playable or a resolution. A final descent is expensive and might be the only
  place a hand-authored level earns its cost.
- Whether the reaction pool can be authored fast enough to matter. **Measure it in iteration 19
  before committing to the count in §5.**
