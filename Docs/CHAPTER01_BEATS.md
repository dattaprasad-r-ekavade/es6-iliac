# Chapter 01 — beat sheet

**Source of truth:** `storyline.md`. This file decomposes it; it does not extend it. If a
beat here is not traceable to a sentence there, it is wrong.

**Status (2026-08-01):** the VS0 beat-graph deliverable is complete. Beat structure,
registries, convergence and outcome contracts are locked; dialogue is summarised, not
written. VS0 as a milestone is still **in progress** until the screenplay, two blocking
narrative locks, regression snapshot and asset ledger are complete.

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
| Scene | `PascalCase`, matches plan.md's scene table | `Estmere_Prison` |

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

Names are placeholders — narrative lock 1 is still open. Role ids are not.

| Id | Role | Scenes | Notes |
|---|---|---|---|
| `role.king` | King of Estmere | B090–B130, B700–B730 | the antagonist; outcome is a player choice |
| `role.prince` | His son and heir | B600–B760 | companion from B630, ruler from B740 |
| `role.rescue_captain` | Pulls the player from the water | B050–B060 | one scene, high impression |
| `role.processing_guard` | Survivor triage | B070–B090 | delivers the idle-persons law |
| `role.instructor_warrior` | Guard-yard trainer | B200–B210 | |
| `role.instructor_mage` | Arcanum tutor | B300–B310 | |
| `role.instructor_trade` | Dock crew lead | B400–B410 | |
| `role.prisoner_a` | Exposition, general population | B510 | becomes `ev.prisoner_testimony` |
| `role.prisoner_b` | Second voice, avoids one lecture | B510 | |
| `role.council_contact` | Crown Council representative | B820–B830 | sets up the next chapter |

## Beats

Systems column names the dependency that must exist first. Anything listed here that is not
in `plan.md`'s systems table is a gap in that table.

### Act 1 — sea and arrival · `Prologue_Ship`, `Estmere_Docks`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B010 | Voyage; player walks the merchant deck | `Prologue_Ship` | interaction, dialogue | deck traversable, Everspire visible | Everspire is in frame from the deck's constrained viewpoint |
| B020 | Ivory Concord warships sighted on the horizon | `Prologue_Ship` | dialogue | player has seen the warships | warship silhouettes render at authored distance under locked fog |
| B030 | Arcane pulse erupts from the Everspire | `Prologue_Ship` | cinematic, VFX, audio | pulse witnessed | watching and skipping set identical flags |
| B040 | Shockwave; ship breaks; water entry; blackout | `Prologue_Ship` | cinematic, damaged variant | player in water, screen black | no physics state can strand the player mid-sequence |
| B050 | Rescue ship pulls the player aboard | `Prologue_Ship` | scene transition | `flag.rescued` | prologue unloads without leaking actors |
| B060 | Arrival at the Estmere docks | `Estmere_Docks` | transition, spawn | player at dock spawn | spawn places feet on ground, not 1 m above |
| B070 | Survivors processed by guards | `Estmere_Docks` | dialogue, quest stage | triage complete | processing cannot be bypassed by walking away |
| B080 | **Character creation** | `Estmere_Docks` | `CharacterProfile`, creator UI | `flag.profile_valid` | profile persists through save, reload and every later scene |

### Act 2 — the King's audience · `Estmere_Palace`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B090 | Taken before the King under the idle-persons law | `Estmere_Palace` | transition, dialogue | player in throne room | arrival works from all triage outcomes |
| B100 | The edict: "every soul must contribute" | `Estmere_Palace` | dialogue | edict heard | line foreshadows the operation without revealing it |
| B110 | Questioned about the missing prince | `Estmere_Palace` | dialogue choices | response recorded | remembered / vague / no-memory all reach B120 |
| B120 | Player declares skill or inclination | `Estmere_Palace` | dialogue choices, profile | inclination recorded | every background maps to exactly one route |
| B130 | **Route assignment** | `Estmere_Palace` | `StoryDirector` | `flag.route` set | refusal and invalid/default selection both resolve to `route.refuse` |

### Act 3 — the four routes (parallel; each must stand alone)

No route may depend on another having happened.

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B200 | Guard-yard instruction: movement, melee, block, hit feedback | `Tutorial_Warrior` | combat tutorial hooks | basics demonstrated | safe spar cannot kill the player |
| B210 | Hunt/patrol with a real encounter | `Tutorial_Warrior` | navigation, encounter pacing | patrol resolved | death or failure returns to a checkpoint, never a softlock |
| B220 | A wounded prisoner is found in secret transport | `Tutorial_Warrior` | interaction, evidence | `ev.transport_order`, prince located | converges with valid payload |
| B300 | Spell instruction: cast, resource, target | `Estmere_Arcanum` | magic tutorial hooks | basics demonstrated | practice space is nonlethal |
| B310 | Soul-crystal delivery to the restricted wing | `Estmere_Arcanum` | interaction, access rules | `ev.crystal_manifest` | restricted access cannot be entered early |
| B320 | An accident opens a sealed cell | `Estmere_Prison` | staged event | prince located | the accident fires exactly once |
| B400 | Sailing lesson, bounded and controllable | `Estmere_Harbor` | `SailingController` | boat handled | boarding, disembarking and reset all recover cleanly |
| B410 | Sneaking, lockpicking, pickpocketing | `Estmere_Harbor` | detection, locks, pickpocket | basics demonstrated | being caught is recoverable, never terminal |
| B420 | Secured-tower infiltration; retrieve the object | `Estmere_SecuredTower` | detection, locks | `ev.tower_ledger`, prince located | tower connects spatially to the prison |
| B500 | Arrest and transfer to general population | `Estmere_Prison` | transition | player imprisoned | gear is stored and returned, never destroyed |
| B510 | Prisoners reveal the soul operation | `Estmere_Prison` | conditional dialogue | `ev.prisoner_testimony` | exposition is split across two speakers, skippable, and repeatable |
| B520 | Route to solitary | `Estmere_Prison` | interaction | prince located | measurably the fastest of the four routes |

### Act 4 — convergence, escape, and the title moment

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B600 | **Convergence** — the prince is found | `Estmere_Prison` | `StoryDirector` | `flag.prince_located` | all four routes satisfy the convergence contract below |
| B610 | The prince explains: his alternative, the interception, his father's motive | `Estmere_Prison` | dialogue | `ev.prince_testimony` | one canonical reveal; route flavour is additive only |
| B615 | The Everspire and the Ivory Concord seeded in passing | `Estmere_Prison` | dialogue | seed planted | mentioned, never explained |
| B620 | Evidence secured from the operation | `Estmere_Prison` | evidence, interaction | `ev.black_crystal` | evidence set is complete before escape is possible |
| B630 | Escape with the prince | `Estmere_Prison` | companion, detection, doors | `flag.prince_following` | companion recovers from blocked paths and survives save/load |
| B640 | **Sea-cave exit — title card** | `Estmere_SeaCave` | cinematic, audio | `flag.title_crawl_shown` | fires exactly once, only here, on every route, watched or skipped |

### Act 5 — confrontation and consequence · `Estmere_Palace_Aftermath`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B700 | Return to the palace without being rearrested | `Estmere_Palace_Aftermath` | world state | player in throne room | the reason is dramatised, not asserted |
| B710 | Evidence presented; the prince testifies | `Estmere_Palace_Aftermath` | evidence, dialogue | case made | all four evidence sets are sufficient |
| B720 | The King's defence | `Estmere_Palace_Aftermath` | dialogue | defence heard | he is given a real argument, not a confession |
| B730 | **Outcome — player chooses: kill or imprison** | `Estmere_Palace_Aftermath` | dialogue choice, `WorldMutation` | `flag.king_outcome` | both branches reach one valid post-coup world |
| B740 | The prince is crowned | `Estmere_Palace_Aftermath` | `WorldMutation` | `flag.ruler = prince` | reload and re-entry cannot produce two rulers or none |
| B750 | Soul-binding outlawed; prisoners released | `Estmere_Palace_Aftermath` | `WorldMutation` | `flag.ban_enacted` | prison population, doors, banners and dialogue all update |
| B760 | Player named **Crown Envoy** | `Estmere_Palace_Aftermath` | `StoryState` | `flag.title_granted` | title appears in dialogue, journal and save metadata |

### Act 6 — handoff · `Caldemar_Arrival`

| Id | Beat | Scene | Systems | Exit state | Acceptance test |
|---|---|---|---|---|---|
| B800 | The new king explains he needs the Crown Council's recognition | `Estmere_Palace_Aftermath` | dialogue, quest | quest opened | motivation is legible to a blind player |
| B810 | Departure, gated on a valid aftermath state | `Estmere_Exterior` | transition gate | travel permitted | departure is impossible with incomplete aftermath flags |
| B820 | Arrival at Caldemar; the Council is set up | `Caldemar_Arrival` | transition, dialogue | council contact met | arrival is an authored space, not a map marker |
| B830 | Final Everspire reminder; next objective given | `Caldemar_Arrival` | dialogue/cinematic | `flag.chapter_complete` | a blind player can state what changed and where they are going |

## The convergence contract

The load-bearing contract of the whole chapter. Four routes enter B600; one path leaves it.

**Entering B600, all routes guarantee:**

1. `flag.route` is set and `flag.profile_valid` is true.
2. Exactly one route-unique evidence item is held.
3. The prince is alive, in the prison, and has not yet spoken to the player.
4. The player is inside `Estmere_Prison` with a known spawn id.
5. No route-specific tutorial state is still active.

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

## Still open

1. Character names for every role above (role ids are stable regardless).
2. Everspire pulse rules, and why the player remembers only part of the event.
3. Why the rescue ship chooses Estmere, and why a castaway is brought before a king.
4. Tutorial failure rules, gear carryover, and the measured target time for `route.refuse`.
5. The four ancestries: names, regional origin, and starting values.

Items 1 and 3 block the screenplay pass. Items 2, 4 and 5 can be settled during VS3.
