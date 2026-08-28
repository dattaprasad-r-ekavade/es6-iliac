# The alpha checklist

**What this is for.** One sentence in `PRODUCTION_PLAN.md` has been outstanding since iteration
14 and nothing else on this list matters beside it:

> *"By iteration 14, a stranger should play three runs in a row without being asked to."*

`PLAYTEST_NOTES.md` records owner passes only. **No person who is not the author has ever played
this game.** Everything below is in service of changing that, and anything that does not serve it
is in the last section on purpose.

The build is ready. The *release* is not, and the gap is hours rather than weeks.

---

## A. Blocking — a stranger cannot usefully play without these

- [ ] **Push the current build.** The live build is `alpha-2026.08.25-1277591`, which predates
      procedural audio, maces, shields, bows, the entire stones system, amulets, cave themes, the
      fort, and every portrait. Anyone downloading today plays a materially worse game than the
      one in this repository, and their feedback would be about a version that no longer exists.
      One command: `.\release.ps1`. The gate runs first and refuses to upload if anything fails.

- [ ] **Replace the placeholder email.** `Docs/itch-description.html` line 19 currently reads
      `mailto:you@example.com`. A tester who wants to answer the one question the page asks has
      nowhere to send it. This is the single cheapest blocking item and the most embarrassing to
      ship.

- [ ] **Generate and upload a cover and screenshots.** `build\RatnaBay.exe --cover` and
      `--screenshot` produce them; attaching them to the page is a manual step in the itch
      editor that butler cannot do. A store page with no images converts close to nothing, and
      the cover is the only thing most people will ever see.

- [ ] **Make the page reachable.** `PLAYTEST_DISTRIBUTION.md` §5 recommends keeping the project
      **Restricted with a password in the invite link** — correct for five named testers, wrong
      for public recruitment. If a link posted to a forum asks for a password, most of the
      click-throughs are lost. Decide which mode this round is, and set it deliberately.

- [ ] **Run the published build on a machine that is not the dev box.** The only way to find out
      what SmartScreen, Defender or Smart App Control do to an unsigned binary. A tester who is
      told "Windows protected your PC" and gives up is a fifth of a five-person sample.

---

## B. Needed to get anything *back* from the playtest

Instrumentation exists and the last step of it does not, which means a tester can play perfectly
and you still learn nothing.

- [ ] **A way for a tester to hand over a recording.** `PlayRecorder` writes every session and
      the help overlay prints the folder as *text* — there is no button to open it and no way to
      copy a summary. `PLAYTEST_DISTRIBUTION.md` §5 asks for two settings buttons — reveal the
      recordings folder, and copy the review to the clipboard — and neither is built. **This is
      the highest-value unbuilt item on the whole list**, because without it the playtest
      produces opinions and no data.

- [ ] **Build the feedback form and link it from the page.** Template ready in
      [`PLAYTEST_FORM.md`](PLAYTEST_FORM.md) — twelve questions, one required, under three
      minutes. It is the only channel that works for a tester with no itch.io account and no
      wish to send an email.

- [ ] **Record which route each tester took** (`ITCHIO_APP`), so "did not play" can be told apart
      from "played and the data never arrived."

- [ ] **Read the first recording by hand.** Every one of the nine findings in the production
      plan came from reading a recording rather than from a summary, and four of them corrected a
      confident wrong conclusion. Do not automate this until it has been done manually once.

---

## C. Worth fixing before strangers see it

Found by visual inspection on 2026-08-28. None of these stop a run finishing.

- [x] ~~**Every box drew its interior.**~~ The cube was wound inside-out, so exteriors were culled
      and interiors drawn. Slabs hid it; anything with depth did not. Fixed, and it is what the
      "floating assets" near the shaft were.

- [ ] **Spoil heaps are cuboids.** `surface.spoil.a` and `.b` are named heaps and render as two
      neat brick blocks — now that they are solid, they read as crates. They want either a
      different material or a stepped silhouette.

- [ ] **The windlass rope renders as a plank.** `surface.shaft.rope` is drawn with
      `WorldMaterials.Timber`, so a rope gets plank grain tiled across it. There is no rope
      material; the cheapest fix is a material that does not tile.

- [ ] **The top-down view draws no props.** Placing the camera above the wall line renders ground
      and nothing else. Harmless in play — no player gets up there — but it is a symptom worth
      understanding rather than a thing to leave unexplained.

- [ ] **Portrait faults, listed rather than fixed.** Hair reads as glossy plastic; Ganaka's grey
      beard is invisible against grey hair; the side hair on Revati and Visakha reads as
      headphones; Visakha's skin is washed out. The mural technique is settled, so these are
      tuning rounds, and `--faces --face <room> --face-scale N` makes each one fast.

- [ ] **The sound effects.** Owner judgement is that they do not sound good. Sourcing and a
      recommended synth/sample split are in [`AUDIO_SOURCING.md`](AUDIO_SOURCING.md); the short
      version is that the Sonniss GDC bundle is free and professional, and Soundsnap is a
      $249/year subscription that can wait until after a playtest says it is the bottleneck.

- [ ] **Gold pacing is a guess.** Roughly 250 a run against a 450 sword, never measured. A tester
      who finds the economy broken spends their whole session on that and answers nothing else.

---

## D. Recruitment — where the strangers come from

`PLAYTEST_DISTRIBUTION.md` is nine hundred lines on how to distribute and instrument a test and
says nothing about where the testers come from. In order of expected yield:

- [ ] **r/IndianGaming.** The highest-yield free channel available and the most underused. A
      Mauryan-era roguelite from a solo Indian developer is novel there in a way it is not on a
      general gamedev board — the setting does the work that a marketing budget otherwise would.
- [ ] **r/playmygame** and **r/gamedev's Feedback Friday.** Reciprocal: real time spent playing
      other people's games in exchange for eyes on yours.
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
- **The fort as geometry.** It is a corridor of doors and that is a staging decision, not a gap.
- **Code signing.** `PLAYTEST_DISTRIBUTION.md` §10 puts this at iteration 21's slice lock, and
  that reasoning still holds: signing earns its cost when builds go out often, not for one round
  with five named people.

---

## The measure

Not "did they like it." Three things, and the first is the only one that is really the test:

1. **Did anybody play three runs in a row without being asked to?**
2. **At the shut door, did they hesitate?** Nine sessions of owner recordings say the decision is
   genuinely weighed — median 2.1s across 28 doors. That number is currently one person's.
3. **Where did they stop, and did they say why?**
