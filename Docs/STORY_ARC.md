# Story arc — the crystal question

**Locked:** 2026-08-01 · **Covers:** the world premise and Chapters 02+

## Authority

| Document | Authority over |
|---|---|
| `storyline.md` | Chapter 01's plot, beat for beat |
| `Docs/CHAPTER01_BEATS.md` | Chapter 01's implementation contract |
| **This file** | the world premise, the Everspire truth, factions, and Chapters 02+ |

Where they touch, this file supplies the **world facts Chapter 01 must not contradict**;
`storyline.md` still decides what happens in Chapter 01. Nothing here rewrites it. What it
does do is tell the Chapter 01 screenplay what it is planting — see “What Chapter 01 must
plant”, which is the operative section for work happening now.

## The premise

1. **Magic runs on crystals. Crystals run on souls.** This is public, ordinary knowledge —
   not a secret and not a scandal.
2. **Organic sourcing is real and humane.** Natural death, willing donors, and creatures
   yield usable souls. At low volume the trade is defensible, and most people correctly
   believe they are participating in something decent.
3. **Demand is climbing and supply is not.** Wizards keep inventing spells that consume
   crystals, and each new working raises baseline consumption. The gap between humane
   supply and actual demand is the engine of the entire story.
4. **The gap is what produces atrocity.** Nobody set out to harvest prisoners. They ran out
   of dead people. Every villain in this story is standing in that gap.
5. **Estmere was the leading shipper**, and Chapter 01 ends its prisoner-sourced supply. The
   resulting shortage is the inciting condition for everything after.

Point 2 is load-bearing and easy to lose. If the audience concludes all soul use is
monstrous, the ending stops being a choice and the arc collapses into an abolition story.

## The Everspire — the truth

**Spoiler section. Nothing in Chapters 01–04 may state any of this.**

Everyone believes the Everspire is where crystals come from. It is not. It is an instrument.

- It **measures** total soul-channeling across the world.
- It **warns** when channeling passes a safe threshold. The Chapter 01 pulse was this, firing
  for the first time in living memory, because consumption had climbed past the line.
- It **regulates** — it actively suppresses the fissures that over-channeling opens. It has
  been holding that pressure for a very long time.

The third function is the one nobody suspects, and it inverts every plan in the setting:

| Faction plan | What they expect | What actually happens |
|---|---|---|
| Force it with heavy magic | breach the source, end the shortage | the suppression fails and releases centuries of held pressure |
| Break it with siege engines | same, by cruder means | same, faster |
| Ban crystals entirely | irrelevant to them — this faction isn't trying to open it | channeling falls, the Everspire stands down on its own |

So the abolitionists are the only ones whose method works — and they are pursuing it for the
wrong reason, having no idea the Everspire is responding to them. Two of the three great
powers are racing to detonate the thing keeping the world intact.

### Fissures

A fissure is a **breach that leaks soul-stuff**. Things come through, and the dead stop
staying dead in the surrounding area.

Tie what emerges to what was spent. The fissures do not vent arbitrary monsters — they vent
the accumulated souls the world has burned for magic, returning. That single constraint does
three things at once: it makes the bestiary thematically inevitable rather than a bolt-on, it
makes the abolitionist argument visceral instead of abstract, and it means every crystal lit
anywhere in the setting is a debt the endgame collects.

**Production note.** This choice pulls a diplomacy game toward combat in its final act, which
is a real cost — but it is largely a cost already paid. Chapter 01 builds melee, block, magic
and detection for its tutorial routes (VS4–VS5), and in a game that is otherwise about
argument those systems would go unused after the opening. The fissures are where they earn
their keep, and they give the arc an action climax rather than a vote.

Keep the fissures off-screen until Chapter 06 at the earliest. They are a better threat than
a monster right up until the moment one is standing in front of the player.

## Factions

Marked **derived** where the naming policy already establishes it, **proposed** where I've
invented to fill your sketch. Replace the proposals freely — the ids are what matter.

| Id | Faction | Seat | Position on crystals | Everspire plan |
|---|---|---|---|---|
| `faction.council` | The Crown Council — *derived* | Caldemar | regulation and status quo | none; it is the arena, not a player |
| `faction.estmere` | Estmere under Terrin — *derived* | Estmere | banned prisoner sourcing; still trades organically | none yet; the player's base |
| `faction.qadris` | Qadris and the Sarrakh south — *derived* | Qadris | **new sources at any cost** | siege — brute force, because it is what they can afford |
| `faction.arcanum` | The Arcanum — *derived* | spans realms | demand is progress; more spells, more consumption | **force it open with heavy magic** — the elegant answer, and the most dangerous |
| `faction.abolition` | **Aldreth** (`city_north`) — *name proposed* | Karnoth Highlands | **full ban**, on moral grounds | none — they are trying to end the trade, not open anything |
| `faction.concord` | The Ivory Concord — *derived* | foreign | indifferent; they care about the instrument, not the trade | **guardians** — keep it protected and the truth buried |

Aldreth needs one new authored city in a region the naming policy already establishes and
which currently has no purpose. The name is a placeholder; `city_north` is the stable id.
Highland isolation is what makes principled abolition affordable for them and nobody else.

### The Concord reversal

The Concord is the load-bearing use of an existing Chapter 01 element. They were on the
horizon at B020 watching Corrath because they understand the instrument, and they have been
waiting for it to fire. That pays off a detail already in the shipped beat sheet at no cost.

They have kept the truth for generations on the reasoning that a world which knew the tower
regulates channeling would immediately try to game it — and, on the evidence of every other
faction in this document, they are right.

This makes them read as hostile foreign observers in Chapter 01, an obstruction through the
spokes, and the player's only informed ally from Chapter 06 onward. They are not a fourth
ending: they hold no position on crystal policy, so they complicate the race without
competing in it. Structurally they oppose whoever moves first in Chapter 07, whoever that
turns out to be.

Two kingdoms plus two institutions, rather than four kingdoms, keeps the world small enough
to author while still giving each philosophy a home.

## Chapter structure — hub and spokes

**Two hubs, with different jobs.** Caldemar dispatches; Estmere is where you come back.

| Hub | Role |
|---|---|
| **Caldemar** | the Council. Politics, spoke dispatch, the three positions argued in the open |
| **Estmere** | home. Terrin, the barometer, and the only place the player can read where their choices are heading |

The return trip is what makes the barometer work — it has to cost something to check, or it
becomes a menu. It also means Chapter 01's most heavily authored spaces (palace, throne room,
city) stay in service across all eight chapters instead of being abandoned after the opening.
That is the strongest production argument in this document.

| Chapter | Content | Order |
|---|---|---|
| **01** | Estmere. Prisoner sourcing exposed and banned; Terrin crowned; player made Crown Envoy | fixed — in production |
| **02** | Caldemar. The Council, the shortage, the three positions, both hubs open | fixed |
| **03–05** | The three spokes: Qadris, the Arcanum, Aldreth | **any order**, with Estmere returns between |
| **06** | The reveal. Corrath and the Everspire itself | gated on evidence, not chapter number |
| **07** | The crisis. The neglected faction moves; the player races | trigger is player-determined |
| **08** | The Council convenes. The player sets policy | fixed |

Chapter 02 is the hub tutorial and must stay linear — it teaches the structure that the rest
of the game relies on.

### Chapter 07 — the neglect trigger

**The faction with the lowest player disposition moves first.** Because the reveal gates at
2-of-3, a skipped spoke is neglected by definition; if all three were played, lowest
disposition decides. The crisis is therefore a direct consequence of how the player spent
their attention, and the hub choices carry into the finale instead of being forgotten at it.

This costs **two** Chapter 07 variants, not three. Only Qadris and the Arcanum use force. If
Aldreth is the neglected faction they escalate politically instead — forcing an early Council
vote — which is a dialogue-scale chapter rather than a set-piece one, and cheap enough to
treat as a third variant if you want it.

### The spoke contract

This is what makes an open hub survivable. It is Chapter 01's convergence contract one layer
up, and it is not optional.

**Every spoke chapter must:**

1. Open without assuming any other spoke has happened.
2. Close by depositing exactly **one** Everspire evidence item and **one** faction
   disposition value.
3. **Never state the truth about the Everspire.** Only Chapter 06 does. Spokes deliver
   fragments that are individually insufficient.
4. Condition its dialogue on `evidence_count`, never on *which* spokes are complete.
5. Be completable regardless of the player's standing with any other faction.

A spoke that reads differently depending on what came before it is broken, however well it
plays. That is the same standard B600 already applies to the four Chapter 01 routes.

### The reveal gate

Chapter 06 unlocks at **2 of 3** spokes, with the third still playable afterward.

Requiring all three is simpler to write but makes a long game longer and punishes the player
for the structure they were given. Two of three keeps pacing tight and gives the third spoke
genuine replay value. The cost is that Chapter 06 must read correctly with either two-spoke
combination, which is a real authoring constraint — budget for it.

## Terrin as the barometer

Terrin does not hold a fixed position. **The player's choices across the spokes mould his
viewpoint, and reading him is how the player sees which ending they are heading toward.**

This replaces what would otherwise be a UI meter with a character. The player walks back to
Estmere between spokes and hears their own accumulated drift argued back at them by the king
they crowned. And because he rules the realm that started all of this, his position at
Chapter 08 carries real weight at the Council — so reading the metric and moving it are the
same action. It is feedback and a vote at once, which is what stops it from being a readout.

### Starting position

**Genuinely uncertain.** Sure that what his father did was monstrous; not yet sure the trade
itself is wrong. That is the honest position for someone who has just lived through Chapter 01
and has not yet seen the wider world, and it leaves maximum travel available in both
directions — which is what makes him a sensitive instrument.

### The two rules that stop him becoming a mood ring

1. **He lags and resists.** He does not mirror the player's last choice. He moves under
   accumulated weight, argues back, and can be some distance behind where the player already
   is. A character who instantly agrees is not a character.
2. **He is never a numeric tell.** He reads through *what he is worried about* and *what he
   has already ordered done in Estmere* — a new law, a halted shipment, a quiet pardon.
   The player infers the trend from governance, never from a stated position or a meter.

### Implementation

Track `terrin.lean` as a continuous value across the three policy positions, moved by spoke
outcomes and by what the player argues in his presence. Every Estmere return reads it and
selects a dialogue set. It is the same conditional-dialogue machinery the spoke contract
already requires, pointed at one character.

Expect players to learn to steer him. That is the intended loop, not an exploit.

## The endings

The player sets the world's crystal policy. The three faction philosophies become the three
endings, so every hour of the tour is an argument the player is being given. Terrin's lean at
Chapter 08 is both the preview of that choice and a thumb on its scale.

| Ending | What it does | The cost |
|---|---|---|
| **Abolition** | channeling stops; the Everspire stands down permanently; the world is safe | a magic-dependent civilization regresses, everywhere, all at once — and it was never necessary, because humane sourcing was fine |
| **Regulated quota** | channeling held below the threshold; organic sourcing only | the pie is now fixed, so **somebody has to be told no** — and the player decides who |
| **Unrestricted** | demand wins; consumption keeps climbing | the Everspire holds until it doesn't; a bet that another answer arrives first |

### Keeping the middle option from being the obvious one

Quota is physically correct, which is a problem: an ending that is simply right is not a
choice. What saves it is that quota converts an abundance question into an allocation
question. Total capacity becomes fixed, and Qadris — arid, poor, and the most
crystal-dependent realm in the setting — cannot survive its fair share.

So the quota ending carries a sub-choice: **who eats the shortfall.** That is precisely the
decision a Crown Envoy exists to make, it uses every relationship built across the spokes,
and it means the "correct" ending still costs the player something they will feel. Author
the sub-choice as part of the ending, not as an epilogue.

## What Chapter 01 must plant

The operative section. The screenplay is being written now, and these are cheap to include
and expensive to retrofit.

| Requirement | Where | Why |
|---|---|---|
| **Organic sourcing is normal, legal and humane** | B510 prisoners; Arcanum B300–B310 | Without this the audience concludes all crystals are evil and the entire arc collapses. This is the single most important item on this list |
| **Osric's crime was a shortcut, not an invention** | B720 | He didn't invent soul-binding; he ran out of legitimate supply and reached for prisoners |
| **Estmere is the leading shipper** | B510, B710 | The Chapter 02 shortage needs this established, not asserted later |
| **Demand is climbing because of new spells** | Magister Quill, B300–B310 | One line from a proud tutor about how much the Arcanum has achieved lately. Plants the engine of the whole arc as an achievement, not a warning |
| **The Ivory Concord was watching Corrath** | B020 | Already in the beat sheet. Change nothing; just don't cut it |
| **The pulse stays unexplained** | B615 | Already locked. Chapter 01 must not hint that it was an alarm |

### The Osric dividend

"Humane at low volume" upgrades B720 considerably. Osric is no longer a man defending
atrocity in the abstract — he is a man who watched legitimate supply run dry, understood
that Estmere's defense and prosperity ran on that supply, and made a decision he can state
out loud without flinching. His son's alternative would have taken years the city did not
have.

That is the real argument B720's acceptance test demands, and it makes B730's kill-or-imprison
choice genuinely hard. It also makes Chapter 01 the world's argument in miniature: the same
gap between demand and humane supply that Osric stood in is the gap every later faction
stands in. Nothing about it needs to be foreshadowed — it just needs to be true.

### One correction to the beat sheet

B750 currently reads "soul-binding outlawed", which — under this premise — would mean Terrin
bans the entire crystal trade in his first act. `storyline.md:55` is precise: *"The
soul-binding **of prisoners** is immediately outlawed."* The beat sheet should match.

## Open questions

The five questions this document opened on 2026-08-01 are all closed. What replaces them is a
tier down — none of it blocks the Chapter 01 screenplay, and none of it needs answering until
Chapter 02 is actually being written.

1. **Aldreth** is a placeholder name. Confirm or replace it; `city_north` is the stable id.
2. What returned souls look like. The rule is set — fissures vent what the world has spent —
   but the forms are undesigned, and they are the entire late-game bestiary.
3. Whether Chapter 07 gets the third Aldreth variant (a forced early Council vote) or only the
   two forceful ones.
4. Cast for Chapters 02+: Qadris's ruler, the Arcanum's head, the Aldreth leadership. Role ids
   can be assigned long before names are.
5. Whether the Ivory Concord gets a named representative, and the earliest chapter in which
   one speaks to the player directly rather than being observed at a distance.

## Scope

This is a story bible, not a schedule. Chapter 01 alone is planned at 73–111 focused days;
eight chapters at that rate is a multi-year solo project. The value of writing this now is
that the Chapter 01 screenplay knows what it is planting, and the vertical slice stops being
able to contradict its own sequel. Treat Chapters 02+ as designed, not committed.
