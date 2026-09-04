using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The summariser that reads a play recording back.
///
/// Tested harder than it looks like it deserves, because its output is going to be used to
/// decide what gets built next. A summariser that quietly miscounts is worse than no telemetry
/// at all: it produces confident conclusions from nothing.
/// </summary>
public class PlayReviewTests
{
    private sealed class Tape
    {
        private readonly PlayRecording _recording = new();
        private float _clock;

        public Tape Wait(float seconds)
        {
            _clock += seconds;
            return this;
        }

        public Tape At(string kind, string detail = "", float value = 0f, float extra = 0f,
            float health = 100f, string target = "", float distance = 0f)
        {
            _recording.Events.Add(new PlayEvent
            {
                At = _clock, Kind = kind, Detail = detail,
                Value = value, Extra = extra, Health = health,
                Target = target, Distance = distance
            });

            return this;
        }

        public PlayRecording Done() => _recording;
    }

    /// <summary>A descent that clears <paramref name="rooms"/> and then camps.</summary>
    private static PlayRecording Descent(int rooms, float hesitation, bool camp = true)
    {
        var tape = new Tape().At(PlayEventKind.RunStarted, "mine", 4211, 1);
        var pending = 0;

        for (var room = 1; room <= rooms; room++)
        {
            tape.At(PlayEventKind.RoomEntered, $"room {room}", room)
                .Wait(30f)
                .At(PlayEventKind.EnemyKilled, "Bandit")
                .At(PlayEventKind.RoomCleared, $"room {room}", room);

            pending += room;

            tape.At(PlayEventKind.DecisionOffered, "", pending, room + 1)
                .Wait(hesitation);

            var last = room == rooms;
            if (last && camp) tape.At(PlayEventKind.Camped, "", pending);
            else if (last) tape.At(PlayEventKind.Died, "", pending, health: 0f);
            else tape.At(PlayEventKind.PressedOn, "", pending, room + 1);
        }

        return tape.At(PlayEventKind.RunEnded, camp ? "camped" : "died", rooms).Done();
    }

    // ---------------------------------------------------------------- structure

    [Test]
    public void ADescentIsReadBackAsOneRun()
    {
        var runs = PlayReview.Runs(Descent(rooms: 3, hesitation: 1f));

        Assert.That(runs, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(runs[0].Seed, Is.EqualTo(4211));
            Assert.That(runs[0].Tier, Is.EqualTo(1));
            Assert.That(runs[0].RoomsCleared, Is.EqualTo(3));
            Assert.That(runs[0].EnemiesKilled, Is.EqualTo(3));
            Assert.That(runs[0].Survived, Is.True);
            Assert.That(runs[0].StonesBanked, Is.EqualTo(6), "1 + 2 + 3");
        });
    }

    [Test]
    public void SeveralSittingsInOneFileStaySeparate()
    {
        var combined = new PlayRecording();
        combined.Events.AddRange(Descent(2, 1f).Events);

        // The second run's clock continues; the reader must split on the marker, not the time.
        foreach (var item in Descent(4, 1f).Events)
        {
            item.At += 500f;
            combined.Events.Add(item);
        }

        var runs = PlayReview.Runs(combined);

        Assert.Multiple(() =>
        {
            Assert.That(runs, Has.Count.EqualTo(2));
            Assert.That(runs[0].RoomsCleared, Is.EqualTo(2));
            Assert.That(runs[1].RoomsCleared, Is.EqualTo(4));
        });
    }

    [Test]
    public void ARecordingThatStopsMidDescentIsStillReadable()
    {
        // The game was closed with the player halfway down. Losing the whole file over a
        // missing end marker would throw away the most interesting sessions there are.
        var tape = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 7, 2)
            .At(PlayEventKind.RoomEntered, "room 1", 1)
            .Wait(20f)
            .At(PlayEventKind.RoomCleared, "room 1", 1);

        var runs = PlayReview.Runs(tape.Done());

        Assert.Multiple(() =>
        {
            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].RoomsCleared, Is.EqualTo(1));
            Assert.That(runs[0].Survived, Is.False);
            Assert.That(runs[0].StonesLost, Is.Zero, "quitting is not dying");
        });
    }

    [Test]
    public void AnEmptyRecordingYieldsNothingRatherThanThrowing()
    {
        Assert.That(PlayReview.Runs(new PlayRecording()), Is.Empty);
    }

    [Test]
    public void EventsOutOfOrderAreSortedBeforeReading()
    {
        var recording = Descent(3, 1f);
        recording.Events.Reverse();

        Assert.That(PlayReview.Runs(recording)[0].RoomsCleared, Is.EqualTo(3));
    }

    // ---------------------------------------------------------------- hesitation

    [Test]
    public void TheClockRunsFromThePanelAppearingToTheAnswer()
    {
        // The number this whole feature exists to produce.
        var decisions = PlayReview.AllDecisions(Descent(rooms: 3, hesitation: 4.5f));

        Assert.That(decisions, Has.Count.EqualTo(3));
        Assert.That(decisions.Select(decision => decision.Hesitation),
            Has.All.EqualTo(4.5f).Within(0.01f));
    }

    [Test]
    public void WalkingAwayFromADoorAndBackIsStillOneDecision()
    {
        // Re-arming the clock on the second offer would report a four-second deliberation as
        // a snap answer, which is exactly backwards.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.RoomEntered, "room 1", 1)
            .At(PlayEventKind.RoomCleared, "room 1", 1)
            .At(PlayEventKind.DecisionOffered, "", 1, 2)
            .Wait(3f)
            .At(PlayEventKind.DecisionOffered, "", 1, 2)
            .Wait(1f)
            .At(PlayEventKind.Camped, "", 1)
            .Done();

        Assert.That(PlayReview.AllDecisions(recording)[0].Hesitation,
            Is.EqualTo(4f).Within(0.01f));
    }

    [Test]
    public void TimeSpentReadingYourPackIsNotDeliberation()
    {
        // Found in a real run: a twenty-three second pause at a door was reported as the
        // longest deliberation ever measured, and the player had been in the inventory the
        // whole time — re-equipping and drinking stones to top up prana before pressing on.
        // Preparing at a threshold is good play; calling it agonising flatters every number
        // this file exists to produce.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.RoomCleared, "", 1)
            .At(PlayEventKind.DecisionOffered, "", 21, 7)
            .Wait(1.5f)
            .At(PlayEventKind.Panel, "open", 0f, 1f)
            .Wait(20f)
            .At(PlayEventKind.Panel, "closed", 0f, 0f)
            .Wait(2f)
            .At(PlayEventKind.PressedOn, "", 21, 7)
            .Done();

        Assert.That(PlayReview.AllDecisions(recording)[0].Hesitation,
            Is.EqualTo(3.5f).Within(0.05f),
            "twenty of those twenty-three seconds were spent in a menu");
    }

    [Test]
    public void APanelOpenedBeforeTheDoorOnlyCountsFromTheOffer()
    {
        // Opening the pack mid-fight and still having it open when the room clears must not
        // subtract time that was never part of the decision in the first place.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.Panel, "open", 0f, 1f)
            .Wait(10f)
            .At(PlayEventKind.RoomCleared, "", 1)
            .At(PlayEventKind.DecisionOffered, "", 1, 2)
            .Wait(4f)
            .At(PlayEventKind.Panel, "closed", 0f, 0f)
            .Wait(1f)
            .At(PlayEventKind.Camped, "", 1)
            .Done();

        Assert.That(PlayReview.AllDecisions(recording)[0].Hesitation,
            Is.EqualTo(1f).Within(0.05f));
    }

    [Test]
    public void WhatWasOnTheTableIsRememberedWithTheAnswer()
    {
        var third = PlayReview.AllDecisions(Descent(rooms: 3, hesitation: 1f))[2];

        Assert.Multiple(() =>
        {
            Assert.That(third.Pending, Is.EqualTo(6), "1 + 2 + 3 was at stake");
            Assert.That(third.NextPays, Is.EqualTo(4), "and room four would have paid 4");
            Assert.That(third.PressedOn, Is.False, "this run camped");
        });
    }

    [Test]
    public void ThePanelLingeringAfterTheAnswerIsNotASecondDecision()
    {
        // Found in the first real recording. The camp panel stayed up for a frame after the
        // player pressed on, which logged a second offer with the old numbers — and the reader
        // then timed the *next* decision from that stale offer, reporting a one-second answer
        // as an eleven-second deliberation. The rule is that the same room count cannot be
        // asked about twice.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.RoomCleared, "room 1", 1)
            .At(PlayEventKind.DecisionOffered, "after 1 rooms", 1, 2)
            .Wait(2.5f)
            .At(PlayEventKind.PressedOn, "into room 2", 1, 2)
            .Wait(0.01f)
            .At(PlayEventKind.DecisionOffered, "after 1 rooms", 1, 2)
            .Wait(10.9f)
            .At(PlayEventKind.RoomCleared, "room 2", 2)
            .Wait(2.2f)
            .At(PlayEventKind.DecisionOffered, "after 2 rooms", 3, 3)
            .Wait(0.9f)
            .At(PlayEventKind.Camped, "after 2 rooms", 3)
            .Done();

        var decisions = PlayReview.AllDecisions(recording);

        Assert.That(decisions, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(decisions[0].Hesitation, Is.EqualTo(2.5f).Within(0.05f));
            Assert.That(decisions[1].Hesitation, Is.EqualTo(0.9f).Within(0.05f),
                "the second decision must be timed from its own offer, not from the stale one");
            Assert.That(decisions[1].Pending, Is.EqualTo(3));
        });
    }

    [Test]
    public void ADecisionNeverAnsweredIsNotCounted()
    {
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.RoomCleared, "room 1", 1)
            .At(PlayEventKind.DecisionOffered, "", 1, 2)
            .Done();

        Assert.That(PlayReview.AllDecisions(recording), Is.Empty);
    }

    // ---------------------------------------------------------------- the verdict

    [Test]
    public void AnsweringInstantlyMeansThePanelWasNotRead()
    {
        Assert.That(PlayReview.Verdict(PlayReview.AllDecisions(Descent(4, hesitation: 0.2f))),
            Does.Contain("reflex").IgnoreCase);
    }

    [Test]
    public void AlwaysPressingOnMeansThePayoutIsTooGenerous()
    {
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine", 1, 1);
        for (var room = 1; room <= 5; room++)
        {
            recording.At(PlayEventKind.RoomCleared, "", room)
                .At(PlayEventKind.DecisionOffered, "", room, room + 1)
                .Wait(3f)
                .At(PlayEventKind.PressedOn, "", room, room + 1);
        }

        Assert.That(PlayReview.Verdict(PlayReview.AllDecisions(recording.Done())),
            Does.Contain("too generous"));
    }

    [Test]
    public void NeverPressingOnMeansThePotIsTooPrecious()
    {
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine", 1, 1);
        for (var run = 0; run < 3; run++)
        {
            recording.At(PlayEventKind.RoomCleared, "", 1)
                .At(PlayEventKind.DecisionOffered, "", 1, 2)
                .Wait(3f)
                .At(PlayEventKind.Camped, "", 1);
        }

        Assert.That(PlayReview.Verdict(PlayReview.AllDecisions(recording.Done())),
            Does.Contain("Never pressed on"));
    }

    [Test]
    public void AMixOfSlowAnswersIsTheOutcomeWorthBuildingOn()
    {
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine", 1, 1);
        for (var room = 1; room <= 4; room++)
        {
            recording.At(PlayEventKind.RoomCleared, "", room)
                .At(PlayEventKind.DecisionOffered, "", room, room + 1)
                .Wait(4f);

            if (room < 4) recording.At(PlayEventKind.PressedOn, "", room, room + 1);
            else recording.At(PlayEventKind.Camped, "", room);
        }

        Assert.That(PlayReview.Verdict(PlayReview.AllDecisions(recording.Done())),
            Does.Contain("Genuinely weighed"));
    }

    [Test]
    public void CampingBecauseTheMineRanOutIsNotAChoice()
    {
        // Found in the first real recording: a player pressed on at all four real doors and
        // camped at the fifth only because there was nothing deeper. Counting that camp as a
        // decision to bank made "always pressed on" look like a balanced 4-to-1 split, and
        // hid the only finding the session actually produced.
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine", 1, 1);
        for (var room = 1; room <= 4; room++)
        {
            recording.At(PlayEventKind.RoomCleared, "", room)
                .At(PlayEventKind.DecisionOffered, "", room, room + 1)
                .Wait(1f)
                .At(PlayEventKind.PressedOn, "", room, room + 1);
        }

        recording.At(PlayEventKind.RoomCleared, "", 5)
            .At(PlayEventKind.DecisionOffered, "", 15, 0)
            .Wait(3f)
            .At(PlayEventKind.Camped, "", 15);

        var decisions = PlayReview.AllDecisions(recording.Done());

        Assert.Multiple(() =>
        {
            Assert.That(decisions, Has.Count.EqualTo(5), "the forced camp is still reported");
            Assert.That(decisions[^1].Forced, Is.True);
            Assert.That(decisions.Take(4).Any(decision => decision.Forced), Is.False);
            Assert.That(PlayReview.Verdict(decisions), Does.Contain("too generous"));
        });
    }

    [Test]
    public void AMineThatOnlyEverOffersItsLastDoorIsSaidToBeTooShort()
    {
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.RoomCleared, "", 1)
            .At(PlayEventKind.DecisionOffered, "", 1, 0)
            .Wait(1f)
            .At(PlayEventKind.Camped, "", 1)
            .Done();

        Assert.That(PlayReview.Verdict(PlayReview.AllDecisions(recording)),
            Does.Contain("too short"));
    }

    [Test]
    public void NoDecisionsAtAllIsSaidPlainlyRatherThanGuessedAt()
    {
        Assert.That(PlayReview.Verdict(Array.Empty<DecisionReview>()),
            Does.Contain("Nobody got to a door"));
    }

    // ---------------------------------------------------------------- pace

    [Test]
    public void ARoomIsTimedFromTheMomentItWasCommittedTo()
    {
        // Not from when the player walked in. A room fought from the previous doorway is
        // already empty on entry, and timing it from entry reports zero seconds for a fight
        // that took half a minute.
        var rooms = PlayReview.Runs(Descent(rooms: 3, hesitation: 1f))[0].RoomSeconds;

        Assert.That(rooms, Has.Count.EqualTo(3));
        Assert.That(rooms[0], Is.EqualTo(30f).Within(0.01f));
    }

    [Test]
    public void RoomsClearedBeforeTheyWereEnteredAreCounted()
    {
        // Reported by a real run: nine rooms, every one of them cleared in the same instant it
        // was entered. The player was fighting each room from the doorway of the one before,
        // which makes the shape of a room irrelevant — worth knowing before anyone spends a
        // week shaping rooms.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .Wait(20f)
            .At(PlayEventKind.RoomEntered, "room 1", 1)
            .Wait(0.05f)
            .At(PlayEventKind.RoomCleared, "room 1", 1)
            .At(PlayEventKind.DecisionOffered, "", 1, 2)
            .At(PlayEventKind.PressedOn, "", 1, 2)
            .At(PlayEventKind.RoomEntered, "room 2", 2)
            .Wait(25f)
            .At(PlayEventKind.RoomCleared, "room 2", 2)
            .Done();

        var run = PlayReview.Runs(recording)[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.RoomsTakenFromTheDoorway, Is.EqualTo(1));
            Assert.That(run.RoomSeconds[0], Is.EqualTo(20.05f).Within(0.1f),
                "the first room still cost twenty seconds of the run");
            Assert.That(run.RoomSeconds[1], Is.EqualTo(25f).Within(0.1f));
        });
    }

    [Test]
    public void HowTheFightWasFoughtIsCounted()
    {
        // The gap this closes: a session fought entirely with the sword was read back as one
        // where no melee happened, because nothing recorded melee and silence was mistaken
        // for absence. A miss counts as a swing; only a landing counts as landed.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.MeleeSwing, "Iron Sword", 9f, 1f)
            .At(PlayEventKind.MeleeSwing, "Iron Sword", 0f, 0f)
            .At(PlayEventKind.MeleeSwing, "Iron Sword", 9f, 1f)
            .At(PlayEventKind.SpellCast, "Flame", 22f)
            .At(PlayEventKind.CastFailed, "Flame", 22f)
            .At(PlayEventKind.CastFailed, "Flame", 22f)
            .Done();

        var run = PlayReview.Runs(recording)[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.MeleeSwings, Is.EqualTo(3));
            Assert.That(run.MeleeLanded, Is.EqualTo(2), "a miss is still a swing");
            Assert.That(run.SpellsCast, Is.EqualTo(1));
            Assert.That(run.CastsRefused, Is.EqualTo(2),
                "running dry is why a mage picks the sword back up");
        });
    }

    [Test]
    public void WhatKilledWhatIsRecoveredFromTheLog()
    {
        // The tactic this exists to make visible, reported by the player and invisible to the
        // recorder: burn an archer down from range, and save the sword for things that walk
        // into it. A log that only knows something died cannot tell the two apart.
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.EnemyKilled, "Flame (burning)", 3, target: "Bandit Archer", distance: 11f)
            .At(PlayEventKind.EnemyKilled, "Flame (burning)", 3, target: "Bandit Archer", distance: 9f)
            .At(PlayEventKind.EnemyKilled, "Iron Sword", 2, target: "Bandit", distance: 1.8f)
            .At(PlayEventKind.EnemyKilled, "Iron Sword", 2, target: "Bandit", distance: 2.1f)
            .At(PlayEventKind.EnemyKilled, "Iron Sword", 2, target: "Preta", distance: 1.9f)
            .Done();

        var run = PlayReview.Runs(recording)[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.KillsByWeapon["Bandit Archer"]["Flame (burning)"], Is.EqualTo(2));
            Assert.That(run.KillsByWeapon["Bandit"]["Iron Sword"], Is.EqualTo(2));
            Assert.That(run.KillsByWeapon.ContainsKey("Bandit Archer"), Is.True);
            Assert.That(run.KillsByWeapon["Bandit Archer"].ContainsKey("Iron Sword"), Is.False,
                "no archer was reached with steel");
        });
    }

    [Test]
    public void TheRangeAFightIsTakenAtIsRecovered()
    {
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.MeleeSwing, "Iron Sword", 9f, 1f, target: "Bandit", distance: 2f)
            .At(PlayEventKind.MeleeSwing, "Iron Sword", 9f, 1f, target: "Bandit", distance: 2.4f)
            .At(PlayEventKind.SpellCast, "Flame", 22f, target: "Bandit Archer", distance: 12f)
            .At(PlayEventKind.SpellCast, "Flame", 22f, target: "Bandit Archer", distance: 10f)
            .Done();

        var run = PlayReview.Runs(recording)[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.MedianMeleeRange, Is.EqualTo(2.2f).Within(0.05f));
            Assert.That(run.MedianSpellRange, Is.EqualTo(11f).Within(0.05f),
                "spells are being used at five times the range of steel");
        });
    }

    /// <summary>
    /// The session this whole signal exists for, in miniature.
    ///
    /// The alpha's one outside player swung sixty times, took no damage, and never entered a
    /// room in 119 minutes. Read as a list of actions it looks like somebody fighting. The
    /// only honest reading is that nothing was achieved, and that has to be a number.
    /// </summary>
    [Test]
    public void ARunThatNeverGetsAnywhereIsReportedAsStuck()
    {
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine.01", 1, 1);

        // Busy, and going nowhere: swinging at a door that will not open.
        for (var swing = 0; swing < 60; swing++)
            recording.Wait(1f).At(PlayEventKind.MeleeSwing, "Iron Sword");

        recording.Wait(1f).At(PlayEventKind.Stuck, "corridor", 92f);

        var run = PlayReview.Runs(recording.Done())[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.WasStuck, Is.True, "sixty swings and no room entered is a stuck run");
            Assert.That(run.LongestStuckMinutes, Is.EqualTo(92f).Within(0.01f));
            Assert.That(run.MeleeSwings, Is.EqualTo(60), "activity is still counted, just not as progress");
            Assert.That(run.RoomsCleared, Is.Zero);
        });
    }

    /// <summary>
    /// The worst stretch, not the sum. Two short breaks are a person having a life; one long
    /// silence is a person who cannot find the way on, and adding them conflates the two.
    /// </summary>
    [Test]
    public void TheLongestIdleStretchIsReportedRatherThanTheTotal()
    {
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine.01", 1, 1)
            .Wait(1f).At(PlayEventKind.Stuck, "room 0", 5f)
            .Wait(1f).At(PlayEventKind.Stuck, "room 0", 6f)
            .Wait(1f).At(PlayEventKind.RoomEntered, "room 1")
            .Wait(1f).At(PlayEventKind.Stuck, "corridor", 7f);

        var run = PlayReview.Runs(recording.Done())[0];

        Assert.That(run.LongestStuckMinutes, Is.EqualTo(7f).Within(0.01f),
            "18 would be the sum of three separate stretches, which is a different claim");
        Assert.That(run.WasStuck, Is.False, "seven minutes is a slow player, not a wall");
    }

    [Test]
    public void TimeSpentStandingInDoorwaysIsMeasured()
    {
        // The habit that decides whether shaping rooms is worth any effort. It cannot be
        // inferred from actions, only from sampling the spaces between them.
        var recording = new Tape().At(PlayEventKind.RunStarted, "mine", 1, 1);
        for (var second = 0; second < 10; second++)
            recording.Wait(1f).At(PlayEventKind.Stance, "room 3", 6f, second < 7 ? 1f : 0f);

        Assert.That(PlayReview.Runs(recording.Done())[0].ShareOfTimeInDoorways,
            Is.EqualTo(0.7f).Within(0.01f));
    }

    [Test]
    public void ARunWithNoSamplesReportsNoHabitRatherThanZero()
    {
        // Older recordings carry no stance samples at all. Reporting them as "nought percent
        // of the time in doorways" would be a confident claim about data that does not exist.
        var run = PlayReview.Runs(Descent(3, 1f))[0];

        Assert.Multiple(() =>
        {
            Assert.That(run.ShareOfTimeInDoorways, Is.Zero);
            Assert.That(run.MedianMeleeRange, Is.Zero);
        });
    }

    [Test]
    public void DamageTakenAccumulatesAcrossTheDescent()
    {
        var recording = new Tape()
            .At(PlayEventKind.RunStarted, "mine", 1, 1)
            .At(PlayEventKind.PlayerHurt, "clean", 12f)
            .At(PlayEventKind.PlayerHurt, "guarded", 3f)
            .At(PlayEventKind.PlayerHurt, "clean", 9f)
            .Done();

        Assert.That(PlayReview.Runs(recording)[0].DamageTaken, Is.EqualTo(24f).Within(0.01f));
    }

    [Test]
    public void ARecordingRoundTripsThroughItsOwnFormat()
    {
        var json = PlayRecording.Serialize(Descent(3, 2f));
        var path = Path.Combine(Path.GetTempPath(), $"ratnabay_tape_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, json);
            Assert.That(PlayRecording.TryLoad(path, out var loaded, out var error), Is.True, error);
            Assert.That(PlayReview.Runs(loaded!)[0].RoomsCleared, Is.EqualTo(3));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AMissingRecordingIsReportedRatherThanThrown()
    {
        Assert.That(PlayRecording.TryLoad("nowhere.json", out _, out var error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }
}
