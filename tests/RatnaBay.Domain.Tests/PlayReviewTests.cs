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
            float health = 100f)
        {
            _recording.Events.Add(new PlayEvent
            {
                At = _clock, Kind = kind, Detail = detail,
                Value = value, Extra = extra, Health = health
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
    public void NoDecisionsAtAllIsSaidPlainlyRatherThanGuessedAt()
    {
        Assert.That(PlayReview.Verdict(Array.Empty<DecisionReview>()),
            Does.Contain("Nobody got to a door"));
    }

    // ---------------------------------------------------------------- pace

    [Test]
    public void HowLongEachRoomTookIsRecovered()
    {
        var rooms = PlayReview.Runs(Descent(rooms: 3, hesitation: 1f))[0].RoomSeconds;

        Assert.That(rooms, Has.Count.EqualTo(3));
        Assert.That(rooms, Has.All.EqualTo(30f).Within(0.01f));
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
