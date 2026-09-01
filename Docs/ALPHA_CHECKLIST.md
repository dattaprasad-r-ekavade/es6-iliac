# The alpha checklist

**What this is for.** One sentence in `PRODUCTION_PLAN.md` has been outstanding since iteration
14 and nothing else on this list matters beside it:

> *"By iteration 14, a stranger should play three runs in a row without being asked to."*

**One stranger has now played it, and it did not go well.** On 2026-08-30 somebody downloaded the
alpha and spent 119 minutes in a tier-1 mine without entering a single room, because the first
door told them it was locked when it was not, and the corridor it stood in had no light. They
never reached a shut door, so the question this whole list is built around was not merely
unanswered — it was unaskable.

Both faults are fixed and asserted, and the build carrying the fix is live. The list below is
what stands between that and a second attempt.

Everything here is in service of the sentence above, and anything that does not serve it is in
the last section on purpose.

---

## A. Blocking — a stranger cannot usefully play without these

- [x] ~~**Push the current build.**~~ Live as `alpha-2026.08.31-b7ee12c`, confirmed processed on
      the `windows` channel. It carries the lit corridors and the unlocked mine doors, which is
      the difference between a game a stranger can leave the first room in and one they cannot.

- [x] ~~**Time one real run.**~~ **Measured, from 273 recordings on the server.** 21 runs where
      the player swung and something hit them back:

      | | |
      |---|---:|
      | median run | **1.8 min** |
      | range | 0.2 – 5.8 min |
      | 4–5 rooms | ~1 – 1.8 min |
      | 7 rooms | ~2 – 3.4 min |
      | 9 rooms | ~5.2 – 5.8 min |

      **The page's "five to ten minutes a run" is about three times the truth.** Nine rooms —
      the longest anybody has cleared — is 5.8 minutes. Honest copy is *"two to six minutes"*,
      and it should say so before anybody is recruited on the old number: a tester promised ten
      minutes who gets two reports that as the game being thin.

      One caveat, stated rather than buried: every one of these runs is the owner's, and the
      owner knows where the doors are. Treat 1.8 minutes as a floor.

- [ ] **Generate and upload a cover and screenshots.** `build\RatnaBay.exe --cover` and
      `--screenshot` produce them; attaching them is a manual step in the itch editor that
      butler cannot do. *Not verifiable from the build machine — itch.io is not always
      reachable from here, so this stays open until somebody looks at the page.*

- [x] ~~**Make the page reachable.**~~ The page loads anonymously, so the project is public
      rather than Restricted. Correct for public recruitment.

- [ ] **Run the published build on a machine that is not the dev box.** The only way to find out
      what SmartScreen, Defender or Smart App Control do to an unsigned binary. A tester who is
      told "Windows protected your PC" and gives up is a fifth of a five-person sample.

---

## B. Needed to get anything *back* from the playtest

- [x] ~~**A way for a tester to hand over a recording.**~~ **Solved by not needing one.** The
      recording now uploads when the game closes, so there is nothing for a tester to find,
      zip or attach. The two settings buttons this item asked for are no longer worth building.

      This mattered more than it looked. Sends only ever happened at launch and at the moment
      consent was granted — and the second fires when there is nothing yet to send — so anybody
      who played once and never opened the game again uploaded **nothing, ever**. That is the
      ordinary case for an alpha, and it is why the one outside player's two hours arrived at
      all only because they happened to launch a second time.

- [x] ~~**Build the feedback form and link it from the page.**~~ Live, twelve questions, one
      required, no account needed. Linked from the store page and the devlog — and now from
      inside the game, on the pause and help screens, which is where somebody about to give up
      actually is.

- [ ] **Record which route each tester took** (`ITCHIO_APP`), so "did not play" can be told apart
      from "played and the data never arrived."

- [x] ~~**Read the first recording by hand.**~~ Done, and it paid for the whole exercise. The one
      outside session was 119 minutes, 61 swings, no damage taken, and **not a single room
      entered**. Reading it found two faults that no test could see: every mine door was flagged
      locked, so the prompt read *"Locked | a key, or Security 0"* on a door that would have
      opened on the first press; and corridors had no lights, so the way on was a black
      rectangle in a brown wall. Both are fixed and both are now asserted.

      It also found a third thing, which is why this item said not to automate it: the player
      was *invisible in their own recording*. The evidence was an absence. `player.stuck` exists
      now so the next one announces itself.

---

## C. Worth fixing before strangers see it

Found by visual inspection on 2026-08-28. **All cleared.**

- [x] ~~**Every box drew its interior.**~~ The cube was wound inside-out, so exteriors were culled
      and interiors drawn. Slabs hid it; anything with depth did not. This is what the "floating
      assets" near the shaft were.

- [x] ~~**Spoil heaps are cuboids.**~~ Now three shrinking, offset courses in the earth material
      rather than one box in masonry. A heap only has to break its own silhouette to stop
      reading as a crate.

- [x] ~~**The windlass rope renders as a plank.**~~ Rope is now its own material — laid fibre with
      a twist, tiled every 24 cm instead of every 1.1 m. Plank grain stretched down a hand-thick
      rope is what drew a hanging board.

- [x] ~~**The top-down view draws no props.**~~ **Not a rendering fault.** The camera was falling
      during the capture's warm-up frames, so every elevated shot was taken from the floor.
      `noclip on` before `goto` holds it, and everything renders from above. Worth knowing for
      any future capture: **an elevated `--screenshot` needs noclip or it is a picture of the
      ground.**

- [x] ~~**Portrait faults.**~~ The lime accent is now gated on light albedo, so black hair and
      dyed cloth stopped looking lacquered. Beards are drawn a shade darker than hair, which
      gets Ganaka his jaw back at full grey. The side masses on Revati and Visakha start inside
      the skull silhouette and fall to the shoulder instead of ending at ear height, which is
      what made them read as headphones. Visakha's skin darkened.

- [x] ~~**Gold pacing is a guess.**~~ Measured, and written into `GoldPacingTests`. See below.

### What the economy actually is

Gold has **one repeatable source**: quests pay a one-off forty, and every other coin comes from
`Encounter` paying `Random.Shared.Next(5, 18)` on a kill — eleven on average. So the economy is
not tuned by a gold constant at all. **It is tuned, invisibly, by the spawn table.**

| Run | Gold |
|---|---|
| 4 rooms, depth 1 | **66** |
| 6 rooms, depth 2 | **132** |
| 10 rooms, depth 3 | **319** |
| 12 rooms, depth 4 | **429** |

The plan's guess of *"roughly 250 a run"* was high for shallow runs and low for deep ones: a mid
run pays 132, and 250 is not reached until about depth three. The 450 sword is **3.4 mid runs**
away, which is a good band — two and it is not a decision, a dozen and the stall is scenery.

**One surprise the measurement turned up:** spawn count has *zero* seed variance. It is a pure
function of rooms and depth, identical across every seed tested. That is pinned now, because the
first version of the variance test allowed a 2.6x spread and passed trivially. If seeded variety
in spawn counts is ever wanted, that test is the one to make fail on purpose first.

---

## D. Recruitment — where the strangers come from

`PLAYTEST_DISTRIBUTION.md` is nine hundred lines on how to distribute and instrument a test and
says nothing about where the testers come from. In order of expected yield:

- [ ] **r/IndianGaming.** The highest-yield free channel available and the most underused. A
      Mauryan-era roguelite from a solo Indian developer is novel there in a way it is not on a
      general gamedev board — the setting does the work that a marketing budget otherwise would.
- [x] ~~**r/playmygame.**~~ Posted 2026-08-28. It produced exactly one downloader, whose session
      is the one described at the top of this file. One is not a sample, but it was enough to
      find two blocking faults, which is a better return than the post looked like it got.
      **r/gamedev's Feedback Friday** is still unused.
- [ ] **r/roguelites.** Players rather than developers, which matters — developers find bugs,
      players find out whether it is fun, and the open question is the second kind. Read the
      self-promotion rules first; a ban costs the channel permanently.
- [ ] **r/DestroyMyGame**, when there is a trailer. Post it *before* paying for a Steam page.
- [ ] **Small roguelite streamers.** Low hit rate, zero cost, and anyone under a few thousand
      subscribers is often glad to be asked.

**Lead with the question, not with the game.** The page already asks the right one, and a
specific question gets answered where an open invitation gets scrolled past:

> *At the shut door — did you ever actually hesitate?*

---

## E. Explicitly not blocking this round

Named so they stop feeling like debt.

- **Bosses (iteration 20).** A real structural hole — runs currently stop rather than climax —
  and not a reason to delay three strangers by a fortnight.
- **The Steam page.** Costs **$100 USD** (recoupable after $1,000 of revenue), needs a W-8BEN and
  a PAN to avoid 30% withholding, and enforces a 30-day wait before release. Worth paying for a
  page that converts, which means after the art direction is committed and there are screenshots
  worth showing. Not this week.
- **Music.** Deferred deliberately and still the right call.
- **The sound effects.** Owner judgement is that they do not sound good, and the owner has
  parked the choice rather than made it. Sourcing, licences and a recommended synth/sample
  split are written up in [`AUDIO_SOURCING.md`](AUDIO_SOURCING.md) and will keep. Question 8 on
  the playtest form asks testers about it, so the next round produces a number to decide with
  instead of one person's ear.
- **The fort as geometry.** It is a corridor of doors and that is a staging decision, not a gap.
- **Code signing.** `PLAYTEST_DISTRIBUTION.md` §10 puts this at iteration 21's slice lock, and
  that reasoning still holds: signing earns its cost when builds go out often, not for one round
  with five named people.

---

## The measure

Not "did they like it." Three things, and the first is the only one that is really the test.

**Nothing below has been measured on a stranger yet.** The one who tried never reached a door,
so all three are still open, and the numbers in point 2 remain one person's:

1. **Did anybody play three runs in a row without being asked to?**
2. **At the shut door, did they hesitate?** Nine sessions of owner recordings say the decision is
   genuinely weighed — median 2.1s across 28 doors. That number is currently one person's.
3. **Where did they stop, and did they say why?**
