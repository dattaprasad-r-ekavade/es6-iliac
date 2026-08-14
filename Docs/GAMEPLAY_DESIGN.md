# Gameplay design — flow, systems and scope

**Locked:** 2026-08-01 · **Ratna Bay migration/metaphysics sync:** 2026-08-12 ·
**Companion docs:** [`STORY_ARC_INDIC.md`](STORY_ARC_INDIC.md) and
[`JIVA_METAPHYSICS.md`](JIVA_METAPHYSICS.md)

Third-party games are named here as design benchmarks. That is deliberate and permitted:
the naming policy in `plan.md` governs **shipped and distributed material**, not internal
planning documents.

## The premise of this document

The art direction lock chose Morrowind as its north star. This document extends that from
*look* to *flow* — because the two are not separable, and because the arc in
[`STORY_ARC_INDIC.md`](STORY_ARC_INDIC.md) turned out to already be Morrowind-shaped.

Morrowind's main quest is formal recognition: the player must be acknowledged as Nerevarine
by four Ashlander tribes and as Hortator by three Great Houses, each on its own terms, in
almost any order, before they can act on Red Mountain. Ratna Bay's arc is a Rajdoot
seeking recognition from the Sabha, touring three powers who each need convincing on
their own terms, in any order, before the finale.

The openings are nearly the same scene too. Morrowind: arrive by boat, a bureaucrat
processes you and completes your paperwork — which *is* character creation — and sends you
to an important man elsewhere. Ratna Bay: arrive by boat, a guard registers you as a
survivor—which *is* character creation—and you are summoned to Raja Vikram.

This was arrived at independently. Treat it as evidence the instinct is sound.


### Movement speed — revised by playtest 2026-08-14

| | m/s | Crosses the 1.6 km city in |
|---|---|---|
| Walk (hold **Shift**) | 3.5 | 7.6 min |
| **Run — the default** | 5.25 | 5.1 min |
| Enemy pursuit | 5.6 | — |

The 3.5 m/s figure and the 7–8 minute metric it derives from are unchanged; what changed is
that the player no longer *travels* at it. Sprint cost nothing and had no downside, so holding
Shift was strictly correct at every moment — the only thing the default achieved was requiring
the player to hold a key for four unbroken minutes. Morrowind, Oblivion and Skyrim all default
to running for the same reason.

Enemy speed went 4.2 → 5.6 in the same change. Left alone, making run the default would have
silently made every fight optional, because nothing in the world could close the distance any
more. Outrunning a fight should be a decision, not the resting state of the world.


## The Stambha is the HUD

**The single load-bearing design decision in this document.**

Red Mountain is visible across most of Vvardenfell. It storms, it glows, it sits at the
centre of both the map and the plot, and players navigate by it without ever thinking of it
as an interface element.

Ratna Bay already has this geography: the Stambha on Meru, at the centre of the bay,
with the cities around it.

**Make the Stambha's state readable from every playable space, and make it the game's only
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

It complements the Arun barometer rather than duplicating it: **Arun reports what people
believe; the tower reports what is true.** The gap between them is the drama.

### Sightline requirement

Every exterior playable space must have at least one authored viewpoint of the Stambha,
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
3. **It makes the barometer cheap.** Arun with topics—*your father*, *the trade*,
   *Marukot*, *what you would do*—filtered on the stable internal key `terrin.lean` costs far
   less than authoring
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

The Stambha is a permanent bay-wide orienting landmark, which solves the hardest half of
navigation before any work is done.

**The Map Editor MVP is promoted from convenience tooling to enabling work.** Landmark-legible
navigation requires hand-placed, distinctive, readable-at-distance geography, which a
procedurally generated 6.8 km bay cannot currently provide.

## Traversal and scale — locked 2026-08-01

The two numbers everything spatial derives from.

### Movement speed

| | Value |
|---|---|
| Walk | **3.5 m/s** |
| Sprint | ~6.5 m/s (×1.85) |
| Enemy pursuit (`EnemyBrain`) | 4.2 m/s |

Previously the walk was **8 m/s** — faster than a sprinting human, and faster than anything
that could chase the player. Two things were broken by it:

1. **Every world felt small.** A city crossed in forty seconds carries no weight, and the
   ferry network had nothing to be worth using for. Morrowind reads as enormous partly
   because you move slowly; distance is only meaningful if it costs time.
2. **Nothing could catch the player.** Enemies at 4.2 m/s were half the player's walking
   speed, so combat was always optional and disengaging was free.

At 3.5 m/s the pursuit speed now sits 20% above a walk and 35% below a sprint, which is the
correct chase dynamic: you cannot stroll away from a fight, but you can outrun it by spending
stamina.

### Region scale

**The metric is a 7–8 minute walk across a city, north–south.** At 3.5 m/s that is
approximately **1.2 km**.

| | Size |
|---|---|
| City core | ~1.2 km across — 7–8 min on foot |
| Region (city + hinterland) | **2 km × 2 km square** |
| Region corner to corner | ~10 min on foot |

For calibration: Balmora is roughly 300 m across and Novigrad's walkable core is near a
kilometre. A 1.2 km city is genuinely large — build one and measure the real cost before
committing to four.

The same 7–8 minute metric at the old 8 m/s would have demanded a **3.4–3.8 km** city, larger
than the entire current bay's usable land. The metric was achievable; the speed was not.

### The square-and-sea architecture

Each region is a square bounded by open water. Ferries connect regions; the sea reads as
endless through fog and skybox, and the actual bound is a turn-back from the boat.

This solves the hardest problem in open-world design — ending the map without a wall — in
fiction rather than in geometry. Players accept "we cannot sail further" from a boat in a way
they never accept an invisible wall in a field.

It also makes regions **independently authorable and independently loadable**: no seams to
blend, no streaming across borders, no cross-region terrain continuity to maintain. For a solo
project that is worth a great deal.

## Travel — sailing is the infrastructure

Morrowind's travel is a system the player learns rather than a menu: public routes, guild
teleport, and Mark/Recall for the initiated. Geography stays meaningful; travel never becomes
tedious.

Ratna Bay is a bay. Boats are the obvious network, and `SailingController` serves the
Chapter 01 trade route.

| Layer | Availability |
|---|---|
| Public ferries | scheduled, cheap, **not instant**, on fixed routes between Ratnapur, Sabhapur, Marukot, Shantipur and Meru |
| Private sailing | for players who learned it in Chapter 01; reaches places the ferries do not |
| Siddha Order halls | instant travel between Order sites—**lost if the player alienates them** |

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

### Implemented state of the code — 2026-08-12

The earlier cosmetic-inventory and combat-stamina defects are closed.

| | Today |
|---|---|
| Melee | equipped weapon class/tier supplies damage; blocking and armour are live |
| Magic | five mechanically distinct spells spend non-regenerating prana |
| Level-up | derives from eight use-based skills; levelling does not refill prana |
| Prana regeneration | **none**; an empty reserve auto-draws lawful jiva-stone charge when available |
| Stamina regeneration | 4/sec in combat, 12/sec at rest |

### Resources — prana is jiva-stone charge

**Prana does not regenerate.** It is charge drawn from jiva stones, which are bought, looted
or issued. A lawful stone holds a released pranic imprint, not the continuing jiva; a black
jiva cages the person. The full rule is [`JIVA_METAPHYSICS.md`](JIVA_METAPHYSICS.md).

This is the setting made playable. The arc's central conflict is demand outstripping humane
supply; a player regenerating free magic for forty hours contradicts it at every second.

| Consequence | Why it matters |
|---|---|
| Gold gets a real sink | it currently has almost nowhere to go |
| `player.channeled` becomes literal | it is the player's receipts, not an abstract counter |
| Scarcity becomes playable | jiva-stone price and availability tighten as the story bites |
| The Chapter 08 vote becomes personal | voting abolition disarms the player |
| Mage and warrior feel structurally different | resource anxiety versus stamina rhythm |

#### The price curve — locked 2026-08-01

**Roughly 20× across the game, absorbed by skill.** Not the literal meal-to-used-car swing,
which is nearer 1000× and would end the mage build partway through Chapter 06.

| | Early (Ratnapur) | Late |
|---|---|---|
| Jiva-stone price | about the cost of a meal | about 20× that |
| Novice casts per stone | baseline | baseline |
| Expert casts per stone | — | **3–4× baseline** |

So a committed mage pays roughly 5× more per cast late-game than they did at the start, while
a dabbler pays the full 20×. **Scarcity prices out the tourist and squeezes the specialist**,
which is exactly the right shape: it rewards commitment to the route without collapsing it.

This makes Destruction and Restoration the most economically valuable skills in the game.
That asymmetry is intentional — no other skill pays a dividend in gold.

Pace it: lawful stones are cheap and common in Chapters 01–03, tightening later. That is the plot
arriving in the player's inventory.

#### Difficulty and the economy

Difficulty is a 1×–6× slider. That interacts with jiva-stone cost directly:
six times the enemy health means six times the casts means six times the spend, so **on the
hardest setting a mage goes broke and a warrior does not.**

The slider must therefore scale jiva-stone *prices* alongside enemy health, or hard mode
silently becomes a warrior-only mode. This is not optional balancing — it is a structural
consequence of making prana a purchased resource.

**This is a cost, not a punishment.** Casting spends stone charge—ordinary economy. Having
drawn five hundred stones' worth must change what Arun says, never how hard enemies hit. The
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
| Shock | burns enemy prana, chains | casters |
| Heal | restore, at jiva-stone cost | — |
| Light | utility: caves, prison, sea cave | — |

Light earns its slot thematically. In a world lit by jiva stones, carrying a light *is* consuming
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
5. **Magic is self-limiting** — casting costs prana, so grinding Destruction means buying
   levels with gold. Self-capping, and the world can notice.

#### The mage's bill

Worth stating plainly, because it is the strongest thing these systems do together: **a mage's
character growth adds measurable burden to the Stambha.** Getting better at Fire requires
casting Fire, which spends prana and depletes jiva-stone charge. Lawful dāna does not cage or
burn a person—the jiva has moved on—but the draw still counts, and black-jiva supply can hide
people inside the same economy. A warrior's growth does not add that channeling burden.

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

This is a required distinction, not a quibble. Chapter 01's B630 escorts Yuvraj Arun from his
cell through the prison to the sea cave, sets the stable internal flag `flag.prince_following`,
and requires the companion to be in a recoverable state at convergence.

The scope consequence is large and favourable. `CompanionController` no longer has to survive
the 6.8 km world, ferries, private sailing or arbitrary open-world save/load — it must handle
one prison, one cave and a handful of doors. The plan currently names companion and
save-state as the largest progression risk; this removes most of it.

Combined with cutting conjuration, **the game needs no persistent ally AI anywhere.**

The existing narrative lock already agrees: Arun is crowned Raja and cannot travel to
Sabhapur.

## Player channeling — the player is part of the problem

Spells spend prana. A lawful jiva stone carries only a released pranic imprint and its jiva
has moved on; a black jiva cages a continuing person. **Every draw, from either source,
burdens the Stambha.**

Therefore **every spell the player casts is on the same meter they have been watching in the
sky for forty hours.** A mage-route player arriving at the Chapter 08 vote—having personally
drawn prana for the entire game—occupies a materially different policy position from a warrior
who added no magical draw. That does not make lawful dāna murder; it makes demand personal.

This pays off the four Chapter 01 routes forty hours later without a single new system.

### The rule: track it, never punish it

No damage penalty, no resource starvation, no mechanical disadvantage of any kind. Punishing
a player for using a core mechanic is bad design.

What it changes is **what the world notices**: what Arun says, what the Dhruva Order remarks
on, and what the player can claim with a straight face before the Sabha. Track `player.channeled`
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
| 8 | Map Editor MVP promoted — it enables landmark navigation | ✅ **built after VS2**; advanced terrain remains |
| 9 | Stambha sightline validation added to world-layout rules | Map Editor expansion |
| 10 | `player.channeled` tracked and exposed as a dialogue condition | ✅ **built** — `PlayerStats.Channeled`, saved |
| 11 | Prana becomes non-regenerating jiva-stone charge | ✅ **built** — retained internal APIs `SoulCrystals`, `PlayerStats.SpendMana` |
| 12 | Equip system: weapon/armour slots with stats, read by `PlayerCombat` | ✅ **built** — `EquipmentCatalog`, `PlayerEquipment` |
| 13 | Eight use-based skills; `Level` derives from total skill progress | ✅ **built** — `Skills`, `SkillSystem` |
| 14 | Three weapon classes with tiers; five distinct spells | ✅ **built** — `EquipmentCatalog`, `SpellCatalog`, `SpellCaster` |
| 15 | Fix the in-combat stamina dead zone — reduced regen, not zero | ✅ **built** — 4/sec in combat, 12/sec at rest |
| 16 | Detection, locks, pickpocketing, crime response | ✅ **built** — `DetectionSystem`, `DoorAndLock`, `PickpocketSystem`, `CrimeWitness` |
| 17 | Sailing | ✅ **built** — `SailingController` |

**Items 10–17 were built on 2026-08-04** and are covered by 36 PlayMode tests. Decisions made
during implementation that are not otherwise recorded:

- **A jiva stone restores 40 charge and costs 12 gold early.** Deliberately close to a health
  potion, so the player reads it as an ordinary consumable before the arc makes it precious.
- **Casting auto-draws a jiva stone** when the reserve is short, announced by a toast. Fluid to
  play, and the player still feels every one.
- **Neither death nor levelling refills charge.** Both were silent resupply routes.
- **Locks and pickpocketing resolve deterministically against Security**, not by dice. The
  player is told the number they need. A hidden roll that fails is indistinguishable from a
  broken mechanic.
- **Being caught costs suspicion, never the goods.** Confiscation would make B410 unwinnable
  for a player seen once, and VS4's gate forbids stranding.
- **Detection is sight-only.** Hearing is the part of stealth players find unreadable; a cone
  plus a raycast is legible without a tutorial explaining it.
- **A boat with no way on cannot turn.** Steering authority scales with speed.
- **Every sailing failure path ends ashore.** No shore in reach returns boat and rider to the
  mooring rather than dropping the player in open water.

Item 1 is the only one with a real deadline. Items 12 and 13 add fields to `SaveGameV4`, so
the *data shape* has to be decided in VS1 even though the behaviour lands in VS4.
