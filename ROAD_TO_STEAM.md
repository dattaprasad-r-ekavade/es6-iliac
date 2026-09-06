# Road to Steam

*Where Ratna Bay actually is, what is left, and the order to do it in.*

Written from the repository and the recordings rather than from memory, 6 September 2026.
**Updated the same day**, after the September art pass was committed and the story's continuity,
its ending and the life paths were all settled.
Companion to [`COMPLETE_OUTLINE.md`](COMPLETE_OUTLINE.md), which says what the game *is*, and to
`Docs/PRODUCTION_PLAN.md`, which is the iteration board this does not replace.

**The one-sentence status:** the game is built and the loop closes; almost nothing about it has
been judged by a stranger, and none of the Steam-specific work has been started.

---

## 1. Where we stand

| | |
|---|---|
| Playable | Yes, end to end: yard → buy a depth → descend → clear, camp or die → return, spend, again |
| Shipping now | itch.io, `alpha-2026.08.31-b7ee12c`, public |
| Domain tests | **725**, ~1 s |
| Published-build self-test | ~140 checks, run by `publish.ps1` |
| Gate | `verify.ps1` — Release build, doctor, tests, content, simulation, **two scripted playthroughs of the real client** |
| Recorded sessions | 180 on the server |
| Sessions by somebody other than the author | **1**, and they never reached a door |
| Steam | Not started. No account, no app, no store page, no SteamPipe |

That last pair of rows is the honest headline. The engineering is in good order; the product
evidence is one person's.

---

## 2. Done

### The game
- The full loop: mine tiers bought with stones, a surface to return to, a shop worth buying
  from, and levels that no longer outgrow the mine.
- Generated mines, deterministic from a seed, extended segment by segment as you press on.
- Camp-or-press-on with the payout curve, entry costs, camp traders and body recovery.
- Combat, guarding, weapon classes, five spells, prana economy, enemy levels that rise with
  room and tier.
- Bosses: every fifth room, three behaviours, a stone drop into the pot.
- Five cave themes, each with one element it shrugs off and one it fears, shown before you pay.
- Succession, amulets, stone slots, the seven-rung rank ladder.
- The fort as a place you walk into, with ten rooms and ten occupants gated by rank.

### The craft
- Three-project split with the boundary asserted by the build (`no domain reference in the engine`).
- The engine exported as a standalone framework with a second game (Campaign) built on it.
- A developer console, scripts, asserts and exit codes — the game can be driven and tested
  with nobody at the keyboard.
- Telemetry: consent-gated, disk-as-queue, uploading to the author's endpoint.
- `publish.ps1` → `release.ps1` → itch.io, gated end to end.

### Presentation
- Borderless fullscreen on the active display, UI authored on a stable 1280×720 canvas.
- The September art pass: painted dialogue portraits, a quieter panel language, and mines that
  read as dug rock rather than masonry.
- Painted-sprite override path: `--sprites` dumps every generated sprite; a PNG dropped into
  `Content/Art/Sprites` replaces it.

---

## 3. Settled on 6 September

- **The art pass is committed**: painted dialogue portraits, the quieter panel language, mines
  that read as dug rock, and two UI fixes on top of it. Gate green.
- **The twelve continuity conflicts are resolved** in `SCRIPT.md`, which now carries a header
  note saying what was decided. Two of them turned out to be the review's own staleness rather
  than the script's: the naming was consistent all along, and all four "already reconciled"
  fragments really do match `Fort.cs` — while one bullet described a fragment that has never
  been written.
- **One ending, not three.** The Ledger, the Lamp and the Release survive as the argument in the
  final room; what happens is that you relieve her. `STORY.md` and `COMPLETE_OUTLINE.md` are
  reconciled to it, so all three documents finally agree.
- **The three life paths are parked.** They were never reachable — `SelectLifePath`'s only
  callers in the client are two lines in a self-test — and they were the scaffolding under the
  three endings.

### Still in flight

- **Iteration 21, slice lock**, is the last iteration on the board and cannot be finished by
  building: its done-when is three people playing several runs each.

---

## 4. What is left, by area

### A. The game
| | Size | Note |
|---|---|---|
| Three external playtests | — | Blocked on people, not code. The board's oldest item |
| Settings, bindings, death and recovery flow polish | S | Iteration 21's buildable half |
| Balance judged by somebody else | M | Eight of the last ten iterations are "built, not judged". One class instead of three cuts that surface by two thirds |
| Steam Deck / Proton behaviour | S | Unknown. WindowsDX under Proton usually works; nobody has looked |

### B. The writing — *the long pole*
| | Size | Note |
|---|---|---|
| ~~Settle the twelve continuity conflicts~~ | — | **Done, 6 September.** `SCRIPT.md` is canonical and the other two documents agree with it |
| Act I–III majors (~15 scenes) | M | The structure is decided; the words are not. **One ending now, not three** — the finale is a scene, and the argument in it is already written |
| **The reaction pool (150–250 short lines)** | **L** | The thing that decides whether the world feels alive. `STORY.md` calls it the real authoring project and it has not started |
| Successor arrival lines | S | One line each; converts dying into content |

### C. Art and audio
| | Size | Note |
|---|---|---|
| World sprites tied to portrait identities | M | Painted portraits vs generic pixel figures is the visible seam. The override path is already built |
| Preta sets per theme (~27 sprites) | M | Budgeted in the design; currently reusing a few |
| Boss art per theme | S | Behaviour exists, dressing does not |
| Music | ? | **No decision.** There is synthesised ambience and effects, and no music at all |
| Sound pass | S | Effects are forged and thin in places |

### D. Steam platform work — *none of this exists yet*
| | Size | Note |
|---|---|---|
| Steamworks account, fee, bank and tax forms | S | Money and paperwork; the tax interview is the slow part for a non-US developer |
| App created, SteamPipe depot, build uploaded | M | A different pipeline from butler. `release.ps1` is the model to copy |
| Store page: capsules (several sizes), 5+ screenshots, trailer | M | Capsule art is a real design job, not a screenshot crop |
| Short and long description, tags, genres | S | Adapt `Docs/itch-description.html`, and fix the run-length claim |
| Content survey, incl. **AI-generated content disclosure** | S | The portraits are AI-generated; declare them. `Docs/Art/PROMPTS.md` is the provenance record |
| Age rating questionnaire | S | Violence, no sexual content |
| Privacy policy for telemetry | S | The game uploads play recordings. That needs a written policy and a link |
| Pricing and regional pricing | S | Undecided |
| Achievements | S | Optional; players expect a few |
| Cloud saves | S | Optional; cheap and appreciated |

> **Verify the specifics at signup rather than trusting this file.** Steam's fee per app, its
> mandatory wait between publishing a store page and releasing, and its review process all
> change, and this document was written from memory of them, not from the current page.

### E. Release engineering
| | Size | Note |
|---|---|---|
| Run the published build on a machine that is not the dev box | S | Still open from the alpha checklist. SmartScreen on an unsigned binary is a real loss of testers |
| Code signing certificate | S | Money. Removes the "Windows protected your PC" wall |
| Verify on real 720p / 1080p / ultrawide displays | S | Three unchecked boxes in `STEAM_PRESENTATION_BASELINE.md` |
| Crash reporting on a stranger's machine | S | Telemetry records play, not faults |

---

## 5. The order I would do it in

**Phase 1 — find out if it is good (weeks, mostly not coding).**
Commit the art pass. Fix the run-length copy: the page says five to ten minutes and 21 recorded
runs say **1.8 median**. Run the published build on somebody else's machine. Then get three
strangers to play and record it. *Done when: three people have played several runs each and you
know where they stopped.*

Everything below is worth doing only if Phase 1 says the loop holds.

**Phase 2 — make it a game with a story in it (the long pole).**
The continuity is settled; start writing. The successor lines and the first fifty reactions
— enough to measure hours of authoring per hour of play, which the board has wanted a number for
since iteration 19. Then Act I. *Done when: a player hears something new most runs.*

**Phase 3 — make it look like one thing.**
World sprites tied to the portraits, preta sets, boss dressing, and a decision about music.
*Done when: a stranger cannot tell which assets came from where.*

**Phase 4 — Steam.**
Account and paperwork first, because the tax and bank steps have waiting in them. Then the
SteamPipe pipeline as a sibling of `release.ps1`. Then store assets and the page. Publish the
page well before you intend to release — Steam enforces a wait, and the page needs to be
collecting wishlists during Phases 2 and 3 anyway. *Done when: a build you did not hand-copy
installs from Steam on a machine you do not own.*

**Phase 5 — release.**
Price, rating, disclosures, a launch build cut from the same gate every other build passes.

---

## 6. Risks, honestly

**The game is unjudged.** Eight iterations are marked "built, not judged". The single most
expensive possible outcome is finishing all of the above and then learning the loop is thin.
Phase 1 exists to buy that information as cheaply as possible.

**The story is the product and it is unwritten.** The setting is what differentiates this from
every other sprite-in-3D roguelite. It is also the largest remaining task and the one most
likely to be underestimated — 150–250 short lines is not a weekend.

**Two art systems.** Painted portraits, procedural everything else. Fallout got away with that
combination; it works when the pixel figure is clearly the same person as the portrait, and
right now it is not.

**AI disclosure is a launch-day fact, not a footnote.** The portraits are generated. Declare
them in the content survey, keep the prompt record, and expect a portion of the audience to
have an opinion. The defensible position is the one already true: the art was directed in
detail, and the record proves it.

**One person, Windows only, no music.** All three are survivable and all three are the kind of
thing that surprises a solo release late.

---

## 7. What I would cut

- **Achievements and cloud saves** until after launch, if they cost a week.
- **Localisation.** English only.
- **The generated-portrait track.** Tried and abandoned on 6 September; the AI portraits stay.
- **The three life paths.** Parked the same day. The trader's compounding price curve is the one
  piece worth reviving later, as an amulet or a rank perk rather than a class.
- **Branching endings.** One scene, and the positions argued inside it.
- **Anything on the parked list** — pickpocketing, sneaking. They wanted a town this game no
  longer has.

---

*Keep this file honest. If a row says "done" and a stranger cannot see it in the build, it is
not done — and the recordings, not this document, are the evidence.*
