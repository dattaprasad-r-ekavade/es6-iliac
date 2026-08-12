# Chapter 01 — beat sheet

**Source of truth:** `storyline.md`. This file decomposes it; it does not extend it. If a
beat here is not traceable to a sentence there, it is wrong.

**World facts:** [`Docs/STORY_ARC.md`](STORY_ARC.md) holds the crystal-economy premise, the
Everspire truth and Chapters 02+. Chapter 01 must not contradict it, and its “What Chapter 01
must plant” section is required reading before the screenplay pass.

**Status (2026-08-01):** the VS0 beat-graph deliverable and VS2 grey-thread gate are complete.
The playable grey implementation now visits **42/42** beat ids across the four route paths and
reaches B830 through the generated scene contract; real authored content remains 0/42 and is
the VS3-VS7 work. Beat structure,
registries, convergence and outcome contracts are locked; dialogue is summarised, not
written. The two screenplay-blocking narrative locks were closed on 2026-08-01 and are
recorded under “Premises” and “Resolved locks” below. The regression snapshot and asset
ledger are complete; the screenplay is deliberately deferred to the VS2→VS3 content window.

## How to use this

Every beat has a stable id, an owning scene, the systems it needs, an exit state, and an
acceptance test. The VS0 gate is that every sentence in `storyline.md` maps to at least one
row below, and that every row has a test.

The single number worth tracking from VS2 onward: **beats with real content, out of 42**.

## ID conventions

Per the naming policy, ids are setting-neutral and survive a rename. Display names live
elsewhere.

| Kind | Form | Example |
|---|---|---|
| Beat | `B###`, gaps of 10 for insertion | `B030` |
| Route | `route.<slug>` | `route.warrior` |
| Flag | `flag.<slug>` | `flag.prince_following` |
| Evidence | `ev.<slug>` | `ev.transport_order` |
| Cast role | `role.<slug>` | `role.king` |
| Scene | `PascalCase`, matches plan.md's scene table | `Prison` |

Never branch on a display name. `ArtDirection` and `WorldLayout` already follow this; story
code must too.

## Registries

### Routes

| Id | Name | Teaches | Assigned when |
|---|---|---|---|
| `route.warrior` | City guard | melee, block, patrol | player declares combat inclination |
| `route.mage` | The Arcanum | casting, resource cost, targeting | player declares arcane inclination |
| `route.trade` | Docks and merchant navy | sailing, stealth, locks, pickpocketing | player declares trade or larceny inclination |
| `route.refuse` | City prison | nothing mechanical; fastest path | player refuses or declares nothing |

### Flags

| Id | Type | Set at | Meaning |
|---|---|---|---|
| `flag.rescued` | bool | B050 | survived the wreck; prologue may unload |
| `flag.profile_valid` | bool | B080 | character profile complete and persisted |
| `flag.route` | enum | B130 | which of the four routes is active |
| `flag.prince_located` | bool | B600 | player has found the prince |
| `flag.prince_following` | bool | B630 | companion is active and escorted |
| `flag.title_crawl_shown` | bool | B640 | title card played; must never fire twice |
| `flag.king_outcome` | enum | B730 | `killed` or `imprisoned` |
| `flag.ruler` | enum | B740 | `prince` (only supported value in the slice) |
| `flag.ban_enacted` | bool | B750 | soul-binding outlawed; world mutation applied |
| `flag.title_granted` | bool | B760 | player is Crown Envoy |
| `flag.chapter_complete` | bool | B830 | opening chapter finished |

### Evidence

Every route contributes exactly one unique item. `ev.black_crystal` and
`ev.prince_testimony` are shared, so the confrontation always has three items minimum.

| Id | Route | Acquired at | Weight at confrontation |
|---|---|---|---|
| `ev.transport_order` | `route.warrior` | B220 | written order moving a prisoner in secret |
| `ev.crystal_manifest` | `route.mage` | B310 | delivery manifest with prisoner-sourced stock |
| `ev.tower_ledger` | `route.trade` | B420 | ledger tying the operation to the crown |
| `ev.prisoner_testimony` | `route.refuse` | B510 | a named prisoner willing to speak |
| `ev.black_crystal` | all | B620 | physical proof of a human-sourced crystal |
| `ev.prince_testimony` | all | B610 | the heir himself |

### Cast

Named 2026-08-01. Recurring roles carry personal names; two single-scene roles stay as
titles by decision, not by omission. Role ids remain the only thing code may branch on.

| Id | Name | Role | Scenes | Notes |
|---|---|---|---|---|
| `role.king` | **Osric Selwyn** | King of Estmere | B090–B130, B700–B730 | the antagonist; outcome is a player choice |
| `role.prince` | **Terrin Selwyn** | His son and heir | B600–B760 | companion from B630, ruler from B740 |
| `role.rescue_captain` | *(title only)* | Captain of the King's ship | B050–B060 | commands the vessel that recovers the player; the King is aboard but unseen |
| `role.processing_guard` | *(title only)* | Survivor triage | B070–B090 | registers survivors, delivers the idle-persons law and the summons |
| `role.instructor_warrior` | **Armsmaster Alaric Thorne** | Guard-yard trainer | B200–B210 | |
| `role.instructor_mage` | **Magister Seraphine Quill** | Arcanum tutor | B300–B310 | |
| `role.instructor_trade` | **Harbourmaster Corvin Ashgrove** | Dock crew lead | B400–B410 | |
| `role.prisoner_a` | **Bartholomew Reed** | Exposition, general population | B510 | becomes `ev.prisoner_testimony` |
| `role.prisoner_b` | **Iris Falk** | Second voice, avoids one lecture | B510 | |
| `role.council_contact` | **Councillor Lucien Ambrose** | Crown Council representative | B820–B830 | sets up the next chapter |

House Selwyn is a long-established line, which is what makes the soul-binding operation
read as a recent corruption of something once decent rather than a dynasty of villains.

### Ancestries

Four, one per region already on the map — appearance and origin only. Route assignment
runs through declared inclination at B120, never through ancestry. Implemented as
head/skin/hair variants on **one shared body and rig**, per the art direction lock.

| Id | Origin | Look |
|---|---|---|
| `anc.coastal` | Kelrith Coast | pale, weathered, dock-born |
| `anc.highland` | Karnoth Highlands | ruddy, heavy-featured |
| `anc.southern` | Sarrakh | dark, desert-adapted |
| `anc.isleborn` | Tolm or Sarn | mixed features; no standing in Estmere |

`anc.isleborn` is the outsider option, and the one that best motivates a player who
refuses the King at B130.

### Starting values — locked 2026-08-01

Mapped onto the stat model that **actually exists** in `PlayerRpg.cs`: three pools, a level,
and gold. There are no attributes, and none are being added for this.

Base is Health 100 / Mana 80 / Stamina 100. **Every ancestry totals 280**, so no ancestry is
mechanically superior and none can be the "correct" pick.

| Id | Health | Mana | Stamina | Total | Reading |
|---|---:|---:|---:|---:|---|
| `anc.coastal` | 95 | 75 | 110 | 280 | dock labour and small boats |
| `anc.highland` | 110 | 70 | 100 | 280 | hard country, hard people |
| `anc.southern` | 90 | 95 | 95 | 280 | the realm with the arcane tradition |
| `anc.isleborn` | 100 | 80 | 100 | 280 | exactly base — the outsider inherits nothing |

Swings are capped at ±15 on any pool, which is legible without being decisive. Starting gold
is **0 for every ancestry** — the player is a shipwreck survivor with nothing, and that is a
story fact before it is a balance one.

`anc.isleborn` sitting precisely on base is deliberate: mechanically it says the same thing
the fiction does.

**These values must not influence route assignment.** `flag.route` comes from declared
inclination at B120 and nothing else — see the ancestry rule above.

## Premises

Locked 2026-08-01. These are the load-bearing facts the screenplay must honour. They fill
gaps `storyline.md` left open; none of them contradicts it.

### The rescue is the King's own ship

The vessel that recovers the player is King Osric's ship, out on the water searching the
route the prince's ship took. Estmere is both its home port and the nearest safe harbour,
so the destination needs no further justification.

This resolves the two questions that blocked the screenplay at once. The King wants to
speak to the player because his own crew pulled them from a wreck on his son's route —
they are a potential witness, not a random vagrant. The idle-persons law does not
disappear; it becomes the legal frame that lets him convert a witness into a conscript at
B130. That is tighter than the law alone, and B100's edict is still required.

**The King is never staged on-screen during the rescue.** B040's blackout covers the whole
recovery. The player wakes at the docks, registers at triage, and only then learns whose
ship it was. This keeps character creation (B080) before the King ever sees the player,
costs no new shipboard scene, and makes the audience read as a summons.

### The character creator is diegetically a survivor registration

B080 is the guards recording who came out of the water. This is why creation happens at
triage rather than anywhere else, and it is how the King's summons can name the player.

### The pulse: unexplained cause, observable effect

**What the Everspire pulse is remains unexplained in this chapter** and is deferred to
later chapters. `storyline.md` already frames it as a passing mention, so nothing here
requires a theory of the Tower.

The answer now exists — it is written down in [`Docs/STORY_ARC.md`](STORY_ARC.md) under
“The Everspire — the truth”. **Chapter 01 must not hint at it.** Knowing the answer is for
the writer's benefit only, so that nothing planted here has to be walked back later.

**What it did is authored:** the pulse disrupted the memory of everyone who was on the
water, scaled by proximity. The player is one of many survivors with gaps, and triage at
B070 can show others in the same state.

The chapter therefore seeds the Tower with evidence rather than assertion, and B110 works
honestly: the King is knowingly questioning an unreliable witness, which makes his
frustration real and makes *remembered*, *vague* and *no-memory* all truthful answers
rather than one truth and two lies.

This is the only pulse rule VS3 needs. Do not author scope beyond “everyone on the water”
— anything wider is a main-story commitment this chapter has not made.

### The King has a real case

Osric genuinely believes soul-binding is what feeds and defends Estmere, and that his
son's alternative would have starved the city. He is not sorry. B720 must give him that
argument rather than a confession — it is what makes the choice at B730 a decision instead
of an execution.

### The prince earns the crown

Terrin had a worked-out alternative to the crystal trade and was intercepted before he
could bring it home. He is competent, not naive, so B740 leaves behind a stable
settlement and the player reads as a decisive ally rather than the protagonist of his
story.

## Beats

Systems column names the dependency that must exist first. Anything listed here that is not
in `plan.md`'s systems table is a gap in that table.

### Act 1 — sea and arrival · `Prologue_Ship`, `Docks`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B010 | Voyage; player walks the merchant deck | `Prologue_Ship` | interaction, dialogue | deck traversable, Everspire visible | Everspire is in frame from the deck's constrained viewpoint |
| B020 | Ivory Concord warships sighted on the horizon | `Prologue_Ship` | dialogue | player has seen the warships | warship silhouettes render at authored distance under locked fog |
| B030 | Arcane pulse erupts from the Everspire | `Prologue_Ship` | cinematic, VFX, audio | pulse witnessed | watching and skipping set identical flags |
| B040 | Shockwave; ship breaks; water entry; blackout | `Prologue_Ship` | cinematic, damaged variant | player in water, screen black | no physics state can strand the player mid-sequence |
| B050 | The King's ship pulls the player aboard, under blackout | `Prologue_Ship` | scene transition | `flag.rescued` | the King is never on-screen here; prologue unloads without leaking actors |
| B060 | Arrival at the Estmere docks | `Docks` | transition, spawn | player at dock spawn | spawn places feet on ground, not 1 m above |
| B070 | Survivors processed by guards; others show the same memory gaps | `Docks` | dialogue, quest stage | triage complete | processing cannot be bypassed by walking away; at least one other survivor demonstrates the pulse effect |
| B080 | **Character creation**, staged as survivor registration | `Docks` | `CharacterProfile`, creator UI | `flag.profile_valid` | profile persists through save, reload and every later scene; the recorded name is what the summons later uses |

### Act 2 — the King's audience · `Palace`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B090 | Summoned before the King — it was his ship that recovered the player | `Palace` | transition, dialogue | player in throne room | the summons names the player from the B080 registration; arrival works from all triage outcomes |
| B100 | The edict: "every soul must contribute" | `Palace` | dialogue | edict heard | line foreshadows the operation without revealing it, and supplies the legal frame for B130 |
| B110 | Questioned about the missing prince | `Palace` | dialogue choices | response recorded | remembered / vague / no-memory are all honest under the pulse rule and all reach B120 |
| B120 | Player declares skill or inclination | `Palace` | dialogue choices, profile | inclination recorded | every background maps to exactly one route |
| B130 | **Route assignment** | `Palace` | `StoryDirector` | `flag.route` set | refusal and invalid/default selection both resolve to `route.refuse` |

### Act 3 — the four routes (parallel; each must stand alone)

No route may depend on another having happened.

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B200 | Guard-yard instruction: movement, melee, block, hit feedback | `Tutorial_Warrior` | combat tutorial hooks | basics demonstrated | safe spar cannot kill the player |
| B210 | Hunt/patrol with a real encounter | `Tutorial_Warrior` | navigation, encounter pacing | patrol resolved | death or failure returns to a checkpoint, never a softlock |
| B220 | A wounded prisoner is found in secret transport | `Tutorial_Warrior` | interaction, evidence | `ev.transport_order`, prince located | converges with valid payload |
| B300 | Spell instruction: cast, resource, target | `Order_Hall` | magic tutorial hooks | basics demonstrated | practice space is nonlethal; Quill plants climbing demand as an Arcanum *achievement* — new spells, more consumption — never as a warning |
| B310 | Soul-crystal delivery to the restricted wing | `Order_Hall` | interaction, access rules | `ev.crystal_manifest` | restricted access cannot be entered early |
| B320 | An accident opens a sealed cell | `Prison` | staged event | prince located | the accident fires exactly once |
| B400 | Sailing lesson, bounded and controllable | `Harbor` | `SailingController` | boat handled | boarding, disembarking and reset all recover cleanly |
| B410 | Sneaking, lockpicking, pickpocketing | `Harbor` | detection, locks, pickpocket | basics demonstrated | being caught is recoverable, never terminal |
| B420 | Secured-tower infiltration; retrieve the object | `Secured_Tower` | detection, locks | `ev.tower_ledger`, prince located | tower connects spatially to the prison |
| B500 | Arrest and transfer to general population | `Prison` | transition | player imprisoned | gear is stored and returned, never destroyed |
| B510 | Prisoners reveal the soul operation | `Prison` | conditional dialogue | `ev.prisoner_testimony` | exposition is split across Reed and Falk, skippable, repeatable, and delivered while the player moves rather than as a stationary scene; must establish that **organic sourcing is normal and legal** and that Estmere is the leading shipper (see `Docs/STORY_ARC.md`) |
| B520 | Route to solitary | `Prison` | interaction | prince located | measurably the fastest of the four routes; **target completion 15 minutes** from B500 |

### Act 4 — convergence, escape, and the title moment

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B600 | **Convergence** — the prince is found | `Prison` | `StoryDirector` | `flag.prince_located` | all four routes satisfy the convergence contract below |
| B610 | The prince explains: his alternative, the interception, his father's motive | `Prison` | dialogue | `ev.prince_testimony` | one canonical reveal; route flavour is additive only |
| B615 | The Everspire and the Ivory Concord seeded in passing | `Prison` | dialogue | seed planted | mentioned, never explained |
| B620 | Evidence secured from the operation | `Prison` | evidence, interaction | `ev.black_crystal` | evidence set is complete before escape is possible |
| B630 | Escape with the prince | `Prison` | companion, detection, doors | `flag.prince_following` | companion recovers from blocked paths and survives save/load |
| B640 | **Sea-cave exit — title card** | `Sea_Cave` | cinematic, audio | `flag.title_crawl_shown` | fires exactly once, only here, on every route, watched or skipped |

### Act 5 — confrontation and consequence · `Palace_Aftermath`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B700 | Return to the palace without being rearrested | `Palace_Aftermath` | world state | player in throne room | the reason is dramatised, not asserted |
| B710 | Evidence presented; the prince testifies | `Palace_Aftermath` | evidence, dialogue | case made | all four evidence sets are sufficient |
| B720 | The King's defence | `Palace_Aftermath` | dialogue | defence heard | he is given a real argument, not a confession: legitimate supply ran dry, Estmere's defence and prosperity ran on it, and Terrin's alternative needed years the city did not have |
| B730 | **Outcome — player chooses: kill or imprison** | `Palace_Aftermath` | dialogue choice, `WorldMutation` | `flag.king_outcome` | both branches reach one valid post-coup world |
| B740 | The prince is crowned | `Palace_Aftermath` | `WorldMutation` | `flag.ruler = prince` | reload and re-entry cannot produce two rulers or none |
| B750 | **Prisoner** soul-binding outlawed; prisoners released | `Palace_Aftermath` | `WorldMutation` | `flag.ban_enacted` | the ban is on prisoner sourcing only, per `storyline.md:55` — organic sourcing remains legal; prison population, doors, banners and dialogue all update |
| B760 | Player named **Crown Envoy** | `Palace_Aftermath` | `StoryState` | `flag.title_granted` | title appears in dialogue, journal and save metadata |

### Act 6 — handoff · `Council_Arrival`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B800 | The new king explains he needs the Crown Council's recognition | `Palace_Aftermath` | dialogue, quest | quest opened | motivation is legible to a blind player |
| B810 | Departure, gated on a valid aftermath state | `Capital_Exterior` | transition gate | travel permitted | departure is impossible with incomplete aftermath flags |
| B820 | Arrival at Caldemar; the Council is set up | `Council_Arrival` | transition, dialogue | council contact met | arrival is an authored space, not a map marker |
| B830 | Final Everspire reminder; next objective given | `Council_Arrival` | dialogue/cinematic | `flag.chapter_complete` | a blind player can state what changed and where they are going |

## Failure and gear rules — locked 2026-08-01

### Failure

**No tutorial failure in Chapter 01 is terminal, and the player cannot die during one.**

Defeat resolves into a *state*, never a game over: knocked down in the spar, caught while
sneaking, arrested for a crime, or driven off during the patrol. Each routes to a recovery
that costs time and dignity rather than progress.

| Situation | Resolution |
|---|---|
| B200 safe spar | knocked down, stand up, instructor comments; cannot kill |
| B210 patrol encounter | defeat returns to the patrol checkpoint with the encounter reset |
| B300 practice space | nonlethal by construction; failed casts waste Mana only |
| B410 caught sneaking | detained, warned, released at the lesson start; repeatable |
| B420 caught in the tower | ejected to the approach, alarm resets after a cooldown |
| Crime response | fine or brief detention; never confiscation, never a closed route |

The one hard rule beneath all of these: **a failure state may never remove an evidence item,
and may never leave the player unable to reach B600.**

### Gear carryover

The player starts Chapter 01 with **nothing** — they came out of the water. Each route issues
its own appropriate kit, and B500 already promises that stored gear is returned and never
destroyed.

**All four routes enter B600 unarmed.** Warrior, mage and trade routes lose or surrender
their kit on the way into the prison; the refuse route never had any. Stored gear returns
after the escape, at the aftermath.

This is a production decision as much as a fiction one. It means **B630's escape is authored
once, for an unarmed player**, instead of four times for four loadouts. Route flavour lives
in dialogue and in the evidence carried, not in what the player is holding.

## The convergence contract

The load-bearing contract of the whole chapter. Four routes enter B600; one path leaves it.

**Entering B600, all routes guarantee:**

1. `flag.route` is set and `flag.profile_valid` is true.
2. Exactly one route-unique evidence item is held.
3. The prince is alive, in the prison, and has not yet spoken to the player.
4. The player is inside `Prison` with a known spawn id.
5. No route-specific tutorial state is still active.
6. **The player is unarmed, and route gear is in storage rather than destroyed.** Added
   2026-08-01 — the contract previously said nothing about equipment, which would have left
   B630 needing to work for both an armoured warrior and an empty-handed prisoner.

**Leaving B630, all routes guarantee:**

1. `flag.prince_following` is true and the companion is in a recoverable state.
2. Evidence set holds ≥ 3 items: one route-unique, `ev.black_crystal`, `ev.prince_testimony`.
3. `flag.title_crawl_shown` is false — the title fires at B640, never earlier.
4. An autosave exists at the prison exit.

A route that cannot meet these is broken, regardless of how well it plays.

## Outcome matrix

`flag.king_outcome` is a player choice, so every row below is authored twice.

| Route | King killed | King imprisoned |
|---|---|---|
| `route.warrior` | must reach B830 | must reach B830 |
| `route.mage` | must reach B830 | must reach B830 |
| `route.trade` | must reach B830 | must reach B830 |
| `route.refuse` | must reach B830 | must reach B830 |

Eight end-to-end runs. This is the QA matrix's largest single cost, and it is the direct
consequence of choosing player agency over a fixed outcome at B730.

Differences between the two branches are confined to: the B730 cinematic, the throne-room
dressing at B740, two guard dialogue variants, and whether the former king appears in a
cell during B750. Nothing else may diverge — that ceiling is what keeps the doubling
affordable.

## Resolved locks

| Lock | Decision | Consequence |
|---|---|---|
| King's fate | **Player choice** — kill or imprison | Two authored outcomes; eight end-to-end runs |
| Successor | **The prince is crowned** | No new character late in the chapter; the prince cannot travel to Caldemar |
| Player title | **Crown Envoy** | Neutral about the ruler; travels to the next chapter unchanged |
| Character creation | **Moderate — 3–4 ancestries** | Head, skin and hair variants on **one shared body and rig**, per the art direction lock. Distinct ancestry meshes are out of scope |
| Protagonist voicing | Silent, subtitles only | Set by the slice definition |

Closed 2026-08-01 — these are the two locks that were blocking the screenplay, plus three
that were due before VS3:

| Lock | Decision | Consequence |
|---|---|---|
| Why Estmere, why an audience | **The rescue ship is the King's own**, searching the prince's route | One premise answers both questions; the idle-persons law survives as the frame for B130 rather than the reason for B090 |
| Rescue staging | **Established by dialogue after the fact** | No shipboard scene; B080 stays at the docks and the King never meets a faceless player |
| Cast names | House **Selwyn**; six recurring roles named, two single-scene roles stay titles | Screenplay pass is unblocked; role ids unchanged |
| Everspire pulse | **Cause unexplained and deferred; effect authored** — memory disruption for everyone who was on the water | B110's three answers are all honest; VS3 authors the effect only, never the cause |
| `route.refuse` target | **15 minutes** from B500 | Aggressively fast. B510 must deliver its reveal in motion, not as a stationary scene — see the risk note below |
| Ancestries | **Four, one per region**, appearance and origin only | Route assignment stays with declared inclination at B120 |

### Risk note — the 15-minute refuse route

B510 must still split the soul-operation reveal across Reed and Falk, remain skippable and
remain repeatable. At 15 minutes that is tight, and the failure mode is the exposition dump
this beat sheet forbids. The way it works is to play the reveal **while the player is
moving toward solitary** — overheard fragments plus two short directed exchanges — rather
than as a scene the player stands still for. Author it that way from the start; retrofitting
a stationary version into 15 minutes will not fit.

## Still open

**Nothing.** Every narrative and production lock for Chapter 01 is closed as of 2026-08-01.

Tutorial failure rules, gear carryover and ancestry starting values were the last three, and
they are recorded above. What remains before VS0's gate is execution, not decisions: the
screenplay pass, which is deliberately deferred. The regression snapshot and asset ledger
([`ASSET_LEDGER.md`](ASSET_LEDGER.md)) are done.

Remaining questions in the project are all Chapters 02+ and live in
[`STORY_ARC.md`](STORY_ARC.md).
