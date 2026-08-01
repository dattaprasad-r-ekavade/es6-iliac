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

### Current state of the code — 2026-08-01

Worth recording, because the gap between this section and `PlayerCombat.cs` is wide.

| | Today |
|---|---|
| Melee | 18 damage, 2.4 m, 0.45 s cooldown, 8 stamina |
| Magic | one spell (Flare), 26 damage, 16 mana, 18 m |
| Level-up | +12 HP, +8 MP, +8 SP, full restore |
| Mana regen | 4/sec, always |
| Stamina regen | 12/sec, **only out of combat** |

Two defects to fix before anything is balanced on top of them:

1. **The inventory is cosmetic.** `InvItem` is `{Id, Name, Count, Kind}` — no stats, no slots,
   no equip system. The Iron Sword in the player's pack does nothing; melee damage is a
   hardcoded serialized field.
2. **Stamina has a dead zone in combat.** Regen is disabled entirely while `InCombat`, combat
   persists 6 s past the last hit, and swings cost 8 from a 100 pool. That is 12 swings, then
   six seconds of standing there unable to attack. Give combat a reduced regen rate rather
   than none.

### Resources — mana is crystal charge

**Mana does not regenerate.** It is charge drawn from soul crystals, which are bought, looted
or issued.

This is the setting made playable. The arc's central conflict is demand outstripping humane
supply; a player regenerating free magic for forty hours contradicts it at every second.

| Consequence | Why it matters |
|---|---|
| Gold gets a real sink | it currently has almost nowhere to go |
| `player.channeled` becomes literal | it is the player's receipts, not an abstract counter |
| Scarcity becomes playable | crystal price and availability tighten as the story bites |
| The Chapter 08 vote becomes personal | voting abolition disarms the player |
| Mage and warrior feel structurally different | resource anxiety versus stamina rhythm |

Pace it: crystals cheap and everywhere in Chapters 01–03, tightening later. That is the plot
arriving in the player's inventory.

**This is a cost, not a punishment.** Casting spends a crystal — ordinary economy. Having spent
five hundred crystals must change what Terrin says, never how hard enemies hit. The
never-punish rule for `player.channeled` still stands.

### Weapons — three classes, tiers not variety

One humanoid rig and a minimum animation set means class count is the budget, not weapon count.

| Class | Identity | Animation cost |
|---|---|---|
| One-handed + shield | reliable; **can block** | one attack, one block pose |
| Two-handed | slow, high damage, **cannot block** | one attack |
| Ranged | the stealth payoff; weak in melee | one draw/release |

Variety comes from **tiers on the same mesh** — stat swaps, not new animations. Thirty weapons
that swing identically are worth less than three that feel different.

### Equipment

Weapon and armour slots carry stats. `InvItem` grows damage/armour values and `PlayerCombat`
reads the equipped weapon instead of a hardcoded field. Loot, merchants and route rewards all
become meaningful; the equipped set joins `SaveGameV4`.

Armour is flat protection with **no associated skill** — Block is the active defensive verb.

### Spells — five, each mechanically distinct

The trap is elements that are damage types with different particle colours. Each must *do*
something.

| Spell | Effect | Counters |
|---|---|---|
| Fire | burn over time | groups, unarmoured |
| Frost | slows, drains stamina | chargers |
| Shock | burns enemy mana, chains | casters |
| Heal | restore, at crystal cost | — |
| Light | utility: caves, prison, sea cave | — |

Light earns its slot thematically. In a world lit by crystals, carrying a light *is* consuming
the resource, so every dark corridor is a small decision.

### Progression — skill use, without Morrowind's flaw

**Skills grow by use.** Character `Level` derives from total skill progress and grants the pool
increases `PlayerStats` already implements, so the existing `+12/+8/+8` code survives and only
the XP source changes.

Eight skills. Each route grants two, which is what gives the routes lasting mechanical identity
rather than an hour of tutorial.

| Skill | Covers | Granted by |
|---|---|---|
| Blade | one-handed | Warrior |
| Block | shields, bash, parry | Warrior |
| Heavy | two-handed | — |
| Marksman | ranged | Trade |
| Destruction | fire, frost, shock | Mage |
| Restoration | heal, light | Mage |
| Stealth | sneak, detection | Trade |
| Security | locks, pickpocketing | Trade |

**The Refuse route grants nothing.** The fastest route gives the least — the correct price for
it, at zero design cost.

#### The five anti-grind rules

Morrowind's system aged badly for specific and fixable reasons. These are not optional; without
them, use-based progression becomes jumping in a corner for an hour.

1. **Gains come from effect, not action.** Damage dealt to something that can fight back. A
   swing that hits air gives nothing; a spell cast at a wall gives nothing.
2. **Gains scale with threat.** The fortieth identical bandit gives near zero.
3. **Diminishing returns per encounter**, so a single enemy cannot be farmed.
4. **No attribute multipliers on level-up.** Morrowind's worst tax was planning level-ups to
   avoid wasting them. Flat pools remove the planning game entirely.
5. **Magic is self-limiting** — casting costs crystals, so grinding Destruction means buying
   levels with gold. Self-capping, and the world can notice.

#### The mage's bill

Worth stating plainly, because it is the strongest thing these systems do together: **a mage's
character growth has a body count.** Getting better at Fire requires casting Fire, which spends
crystals, which spends souls. A warrior's growth is free.

No line of dialogue achieves that. It also means the Chapter 08 vote lands differently
depending on how the player played — a mage voting for abolition votes against their own
progression.

### Explicitly out of scope

- **Conjuration and summons.** Summons are companions with extra steps.
- **Travel companions.** See below.
- **Attributes.** There is no Strength/Intelligence layer and none is being added. The stat
  model is three pools, a level and gold — see `PlayerRpg.cs`. Skills grow by use; attributes
  do not exist to be multiplied.
- Encumbrance micromanagement.
- Perk trees. Considered and rejected in favour of use-based skills.

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
| 11 | Mana becomes non-regenerating crystal charge | VS4 |
| 12 | Equip system: weapon/armour slots with stats on `InvItem`, read by `PlayerCombat` | VS1 data, VS4 behaviour |
| 13 | Eight use-based skills; `Level` derives from total skill progress | VS4 |
| 14 | Three weapon classes with tiers; five distinct spells | VS4 |
| 15 | Fix the in-combat stamina dead zone — reduced regen, not zero | any time; it is a small bug |

Item 1 is the only one with a real deadline. Items 12 and 13 add fields to `SaveGameV4`, so
the *data shape* has to be decided in VS1 even though the behaviour lands in VS4.
