# Gameplay design — flow, systems and scope

**Locked:** 2026-08-01 · **Companion doc to:** [`STORY_ARC.md`](STORY_ARC.md)

Third-party games are named here as design benchmarks. That is deliberate and permitted:
the naming policy in `plan.md` governs **shipped and distributed material**, not internal
planning documents.

## The premise of this document

The art direction lock chose Morrowind as its north star. This document extends that from
*look* to *flow* — because the two are not separable, and because the arc in
[`STORY_ARC.md`](STORY_ARC.md) turned out to already be Morrowind-shaped.

Morrowind's main quest is formal recognition: the player must be acknowledged as Nerevarine
by four Ashlander tribes and as Hortator by three Great Houses, each on its own terms, in
almost any order, before they can act on Red Mountain. Kessil Bay's arc is a Crown Envoy
seeking recognition from a Crown Council, touring three powers who each need convincing on
their own terms, in any order, before the finale.

The openings are nearly the same scene too. Morrowind: arrive by boat, a bureaucrat
processes you and completes your paperwork — which *is* character creation — and sends you
to an important man elsewhere. Kessil Bay: arrive by boat, a guard registers you as a
survivor — which *is* character creation — and you are summoned to the King.

This was arrived at independently. Treat it as evidence the instinct is sound.

## The Everspire is the HUD

**The single load-bearing design decision in this document.**

Red Mountain is visible across most of Vvardenfell. It storms, it glows, it sits at the
centre of both the map and the plot, and players navigate by it without ever thinking of it
as an interface element.

Kessil Bay already has this geography: the Everspire on Corrath, at the centre of the bay,
with the cities around it.

**Make the Everspire's state readable from every playable space, and make it the game's only
channeling meter.**

| Stage | What the tower does |
|---|---|
| Chapter 01 | pulses once, catastrophically; nobody knows why |
| Spokes | flickers, dims, brightens — the player notices without understanding |
| Chapter 06 | the reveal lands, and the player realises they have been reading a gauge for forty hours |
| Chapter 07 | whichever faction moves, the tower answers |
| Chapter 08 | the chosen ending is legible in the sky before anyone states it |

This costs a shader, a tracked value, and discipline about sightlines. In exchange it gives
a diegetic, world-scale, zero-UI meter for the story's central variable.

It complements the Terrin barometer rather than duplicating it: **Terrin reports what people
believe; the tower reports what is true.** The gap between them is the drama.

### Sightline requirement

Every exterior playable space must have at least one authored viewpoint of the Everspire,
and every hub must have one that is unavoidable on the critical path. This is a world-layout
constraint, not a polish item — it belongs in the Map Editor's validation rules.

## Dialogue — topic-based, not tree-based

**This is a VS1 decision with a deadline.** It is substantially harder to convert after
`DialogueGraph` is built.

Morrowind's dialogue is keyword hypertext rather than conversation trees. NPCs share a
common knowledge base, with responses filtered by faction, rank, disposition, location and
story flags, plus a small number of unique entries each.

Three reasons this is correct here:

1. **It is how one person writes eight chapters.** Trees cost combinatorially; topics cost
   linearly. Morrowind carries more words than most novels because none of it was recorded
   and no path had to be individually authored.
2. **It enforces the spoke contract for free.** The contract requires spokes to condition on
   `evidence_count` and never on *which* spokes are complete. In a tree system that is a
   discipline maintained by review. In a topic system it is simply how the data works —
   "player holds ≥ 2 evidence" is one condition among many. The architecture does the work
   that would otherwise be done by hand.
3. **It makes the barometer cheap.** Terrin with topics — *your father*, *the trade*,
   *Qadris*, *what you would do* — filtered on `terrin.lean` costs far less than authoring
   trees per lean state. It also produces the required lag-and-resist behaviour naturally,
   because he can hold different positions on different topics simultaneously. That is what
   someone mid-change actually sounds like.

The silent-protagonist lock is what makes this work. Topic dialogue with a voiced player
character is awkward; with a silent one it is native.

## Cutscenes and audio

Dialogue is read. **Selected story beats are cutscenes**, and cutscenes eventually carry
generated voice.

### Slice boundary

The vertical slice definition excludes voice acting. That stands. **The slice ships silent**
with cutscene audio hooks in place; voice arrives at the release-candidate tier.

The slice's job is to prove the cutscene *state contract* — that watching and skipping
produce identical flags, which is already an acceptance test on B030, B640 and B730. Voice is
a layer applied over a solved problem, and the tooling improves faster than the schedule.

**The protagonist is silent permanently.** That is what keeps topic dialogue affordable.

### The tonal split, and its fix

A character who speaks in cutscenes and is silent in conversation reads as broken.

Morrowind's solution is cheap and works: **voiced greetings only.** One or two barks when
dialogue opens, then text. The player hears the voice once and reads the rest in it.

### Generated voice — two production rules

1. **Licensing goes in the asset ledger.** Commercial terms for generated-voice tools vary
   considerably. The asset ledger is already a VS0 deliverable; this belongs in it, recorded
   the same way as Mixamo and Sonniss.
2. **Render and commit final audio; never regenerate at build time.** Model and version drift
   will otherwise make a character sound like a different person between chapters.

## Navigation — directions first, markers derived

Markers are **player-toggleable, three-state**:

| Setting | Behaviour |
|---|---|
| Off | written directions only |
| **Area** | approximate region indicated; the default |
| Precise | exact objective marker |

Binary on/off makes *off* punishing and *on* trivialising. The middle setting is where most
players want to be, and it is the same target data at a different radius.

### The two disciplines that stop this rotting

When both systems exist, the marker path gets exercised and the direction path breaks
silently, because nobody plays with markers off.

1. **Author directions first; derive markers from the same target data.** They cannot drift
   apart if one is generated from the other.
2. **Run the QA matrix with markers off.** That is the mode that fails without anyone
   noticing.

Morrowind's genuine failure cases are quests unsolvable without a wiki because directions
were wrong. Every direction must be walked. Budget it as QA, not as writing.

### What makes this viable here

The Everspire is a permanent bay-wide orienting landmark, which solves the hardest half of
navigation before any work is done.

**The Map Editor MVP is promoted from convenience tooling to enabling work.** Landmark-legible
navigation requires hand-placed, distinctive, readable-at-distance geography, which a
procedurally generated 6.8 km bay cannot currently provide.

## Travel — sailing is the infrastructure

Morrowind's travel is a system the player learns rather than a menu: public routes, guild
teleport, and Mark/Recall for the initiated. Geography stays meaningful; travel never becomes
tedious.

Kessil Bay is a bay. Boats are the obvious network, and `SailingController` is already being
built for the Chapter 01 trade route.

| Layer | Availability |
|---|---|
| Public ferries | scheduled, cheap, **not instant**, on fixed routes between Estmere, Caldemar, Qadris, Aldreth and Corrath |
| Private sailing | for players who learned it in Chapter 01; reaches places the ferries do not |
| Arcanum halls | instant travel between Arcanum sites — **lost if the player alienates them** |

So the Chapter 01 tutorial with the most expensive mechanic becomes the one with the longest
payoff. `DiscoveryTravelSystem`'s menu fast-travel retires; the P1 list already wanted this.

## Traversal — swimming without diving

Surface-only. No oxygen system, no underwater combat, no submerged level design, no
underwater rendering work.

**Swimming is deliberately slow, tiring and unpleasant.** It is for fifty metres, never five
hundred. If sailing is the transport infrastructure, swimming must not compete with it.

Nothing currently locked requires diving. B040's water entry is a scripted blackout, and the
sea cave is enterable at surface level.

Needs a defined rule for what happens at depth — soft barrier or auto-surface. Pick one and
apply it everywhere.

## Combat and magic

**Direct-hit, Oblivion-style.** Morrowind's hidden-dice melee — where a visually connecting
swing misses because of an unseen roll — is its most disliked feature by a wide margin. The
project already has working direct combat. Do not touch it.

### In scope

- Melee, block, hit reaction
- Elemental damage schools
- Healing
- Real targeting: self, touch, ranged
- Resource cost and feedback, sufficient for B300's *cast / resource / target* tutorial

### Explicitly out of scope

- **Conjuration and summons.** Summons are companions with extra steps.
- **Travel companions.** See below.
- Morrowind's level-up and attribute-multiplier systems — opaque and they reward degenerate
  play.
- Encumbrance micromanagement.

### Companions — the scope reading

**No recruitable companions in open-world travel. Scripted-sequence escorts remain.**

This is a required distinction, not a quibble. Chapter 01's B630 escorts the prince from his
cell through the prison to the sea cave, sets `flag.prince_following`, and the convergence
contract requires the companion to be in a recoverable state.

The scope consequence is large and favourable. `CompanionController` no longer has to survive
the 6.8 km world, ferries, private sailing or arbitrary open-world save/load — it must handle
one prison, one cave and a handful of doors. The plan currently names companion and
save-state as the largest progression risk; this removes most of it.

Combined with cutting conjuration, **the game needs no persistent ally AI anywhere.**

The existing narrative lock already agrees: the prince is crowned and cannot travel to
Caldemar.

## Player channeling — the player is part of the problem

Spells consume crystals. Crystals consume souls. The Everspire reads total channeling.

Therefore **every spell the player casts is on the same meter they have been watching in the
sky for forty hours.** A mage-route player arriving at the Chapter 08 vote — having
personally burned crystals for the entire game — occupies a materially different moral
position than a warrior who arrives with clean hands.

This pays off the four Chapter 01 routes forty hours later without a single new system.

### The rule: track it, never punish it

No damage penalty, no resource starvation, no mechanical disadvantage of any kind. Punishing
a player for using a core mechanic is bad design.

What it changes is **what the world notices**: what Terrin says, what the Concord remarks on,
and what the player can claim with a straight face at the Council. Track `player.channeled`
as a running total and expose it as a dialogue condition. That is the entire implementation.

## Evidence as documents

`EvidenceRecord` already carries an inspected state, which is most of the way there.

**Show the document; do not summarise it into a journal entry.** A manifest whose sourcing
column has been altered is worth more than a journal line asserting that the manifest proves
prisoner sourcing, because the player does the work and therefore owns the conclusion.

This is nearly free — text and a UI panel — and it is the most Morrowind thing achievable
with systems already planned.

## Deferred — considered and not adopted for the slice

- **Routes as persistent faction memberships** with rank, skill gates and inter-faction
  hostility. Genuinely attractive, and it would make Chapter 08 standing mechanically real.
  It is also substantial new content. Post-slice.
- **Morrowind's object density.** Roughly 316k hand-placed objects over about a hundred
  man-years. The art direction lock already rejects this. Note that landmark *legibility*
  and object *count* are different problems; conflating them turns navigation into an
  unbudgeted art task.

## Deltas to `plan.md`

| # | Change | Urgency |
|---|---|---|
| 1 | `DialogueGraph` becomes topic-based rather than tree-based | **VS1 — hardest to reverse later** |
| 2 | `DiscoveryTravelSystem` retires in favour of scheduled in-fiction transport | VS1–VS2 |
| 3 | `QuestDefinition` map markers become authored directions with derived three-state markers | VS1 |
| 4 | `EvidenceRecord` gains a full readable document body | VS1 |
| 5 | `CompanionController` scoped to authored sequences only | VS4 — reduces scope |
| 6 | Magic scope fixed: elements, healing, targeting; no conjuration | VS4 |
| 7 | Swimming surface-only, deliberately poor; no diving systems | VS4 |
| 8 | Map Editor MVP promoted — it enables landmark navigation | after VS2, as planned |
| 9 | Everspire sightline validation added to world-layout rules | Map Editor |
| 10 | `player.channeled` tracked and exposed as a dialogue condition | VS1 data, VS5+ payoff |

Item 1 is the only one with a real deadline.
