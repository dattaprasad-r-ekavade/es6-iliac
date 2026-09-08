using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The run ledger, and the decision it exists to create.
///
/// These are the design's own numbers written as assertions. If a rebalance breaks one, that
/// should be somebody's deliberate decision rather than a discovery made three iterations later
/// when the loop has quietly stopped being tense.
/// </summary>
public class RunStateTests
{
    private static RunState Descend(int rooms = 10, int tier = 1) =>
        RunState.Begin(seed: 4211, tier, rooms);

    /// <summary>Clear <paramref name="count"/> rooms, door by door, as the player would.</summary>
    private static RunState After(int count, int rooms = 10, int tier = 1)
    {
        var run = Descend(rooms, tier);
        for (var index = 0; index < count; index++)
        {
            run.EnterRoom();
            run.ClearRoom();
        }

        return run;
    }

    // ---------------------------------------------------------------- the payout curve

    [Test]
    [TestCase(3, 6, 4)]
    [TestCase(5, 15, 6)]
    [TestCase(8, 36, 9)]
    public void TheBankingCurveMatchesTheDesign(int cleared, int banked, int nextPays)
    {
        // Straight out of the design table. These three rows are the whole economy.
        var run = After(cleared);

        Assert.Multiple(() =>
        {
            Assert.That(run.Pending, Is.EqualTo(banked));
            Assert.That(run.NextRoomPays, Is.EqualTo(nextPays));
        });
    }

    [Test]
    public void TheStakeClimbsAgainstThePrize()
    {
        // The design claim is the shape, not the values: every room banked makes the next door
        // a worse bet than the last, because the pot grows faster than the prize. The exact
        // ratios are a consequence of the curve, and the curve is pinned once, above.
        var run = Descend(rooms: 12);
        var previous = 0f;

        for (var room = 1; room <= 8; room++)
        {
            run.EnterRoom();
            run.ClearRoom();

            Assert.That(run.RiskRatio, Is.GreaterThan(previous),
                $"the door after room {room} is no tenser than the one before it");
            previous = run.RiskRatio;
        }
    }

    [Test]
    public void PressingOnAlwaysPaysMoreThanTheRoomBefore()
    {
        // The rule the entire mechanic rests on. A flat reward makes banking immediately
        // correct at every step, because the pot grows while the prize does not — and then
        // "one more room?" has a known answer and stops being a question.
        var run = Descend(rooms: 12);
        var previous = 0;

        for (var room = 0; room < 12; room++)
        {
            var pays = run.NextRoomPays;
            Assert.That(pays, Is.GreaterThan(previous), $"room {room + 1} pays no more than room {room}");
            previous = pays;

            run.EnterRoom();
            run.ClearRoom();
        }
    }

    [Test]
    public void ADeeperMinePaysProportionallyMore()
    {
        Assert.Multiple(() =>
        {
            Assert.That(After(5, tier: 3).Pending, Is.EqualTo(After(5, tier: 1).Pending * 3));
            Assert.That(RunState.PayoutFor(4, 2), Is.EqualTo(8));
        });
    }

    [Test]
    public void TheRiskRatioIsIndependentOfTier()
    {
        // Tier scales both sides, so the shape of the decision is the same at every depth and
        // only the size of the numbers changes. A player learns the curve once.
        Assert.That(After(5, tier: 4).RiskRatio, Is.EqualTo(After(5, tier: 1).RiskRatio).Within(0.001f));
    }

    // ---------------------------------------------------------------- camping

    [Test]
    public void NothingCanBeBankedUntilARoomIsCleared()
    {
        var run = Descend();

        Assert.Multiple(() =>
        {
            Assert.That(run.CanCamp, Is.False, "camping in the entrance is just leaving");
            Assert.That(run.Camp().Outcome, Is.EqualTo(RunOutcome.InProgress));
            Assert.That(run.IsActive, Is.True);
        });
    }

    [Test]
    public void ThereIsNoCampingInTheMiddleOfAFight()
    {
        // If the player could bank the instant a fight turned, the mechanic collapses into
        // "always bank when losing" and there is no risk left to press.
        var run = After(2);
        run.EnterRoom();
        var pot = run.Pending;

        Assert.Multiple(() =>
        {
            Assert.That(run.CanCamp, Is.False);
            Assert.That(run.Camp().Outcome, Is.EqualTo(RunOutcome.InProgress));
            Assert.That(run.Pending, Is.EqualTo(pot), "the pot is untouched by a refused camp");
        });
    }

    [Test]
    public void CampingCarriesThePotOutAndEndsTheRun()
    {
        var run = After(5);
        var pot = run.Pending;
        var result = run.Camp();

        Assert.Multiple(() =>
        {
            Assert.That(result.Survived, Is.True);
            Assert.That(result.StonesCarriedOut, Is.EqualTo(pot), "all of it, whatever the curve pays");
            Assert.That(result.StonesLost, Is.Zero);
            Assert.That(result.RoomsCleared, Is.EqualTo(5));
            Assert.That(run.IsActive, Is.False);
            Assert.That(run.Pending, Is.Zero);
        });
    }

    [Test]
    public void ARunThatIsOverStaysOver()
    {
        var run = After(3);
        run.Camp();

        Assert.Multiple(() =>
        {
            Assert.That(run.Camp().StonesCarriedOut, Is.Zero, "a run cannot be banked twice");
            Assert.That(run.Die().StonesLost, Is.Zero, "and cannot be lost after it is won");
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Camped));
        });
    }

    // ---------------------------------------------------------------- dying

    [Test]
    public void DyingForfeitsEverythingInThePot()
    {
        var run = After(8);
        var pot = run.Pending;
        var result = run.Die();

        Assert.Multiple(() =>
        {
            Assert.That(result.Survived, Is.False);
            Assert.That(result.StonesLost, Is.EqualTo(pot), "all of it, whatever the curve pays");
            Assert.That(result.StonesCarriedOut, Is.Zero);
            Assert.That(run.Pending, Is.Zero);
        });
    }

    [Test]
    public void WhatWasLostIsRecordedSoASuccessorCanFetchIt()
    {
        // Succession recovers the cache once, so the size of it has to survive the run that
        // lost it. What matters is that the cache is the pot, not that the pot is any
        // particular number -- the curve is pinned once, in TheBankingCurveMatchesTheDesign.
        var run = After(6);
        var pot = run.Pending;

        Assert.Multiple(() =>
        {
            Assert.That(pot, Is.GreaterThan(0), "there is something to lose");
            Assert.That(run.Die().StonesLost, Is.EqualTo(pot));
        });
    }

    [Test]
    public void DyingInTheEntranceCostsNothingBecauseNothingWasEarned()
    {
        Assert.That(Descend().Die().StonesLost, Is.Zero);
    }

    // ---------------------------------------------------------------- the end of a mine

    [Test]
    public void AMineRunsOutOfRoomsToPressInto()
    {
        var run = After(4, rooms: 4);

        Assert.Multiple(() =>
        {
            Assert.That(run.IsExhausted, Is.True);
            Assert.That(run.CanPressOn, Is.False, "there is nothing left to press into");
            Assert.That(run.CanCamp, Is.True, "but the way out is still open");
            Assert.That(run.NextRoomPays, Is.Zero);
            Assert.That(run.RiskRatio, Is.Zero);
        });
    }

    [Test]
    public void ClearingTheSameRoomTwicePaysOnce()
    {
        var run = After(3);
        var pot = run.Pending;
        var again = run.ClearRoom();

        Assert.Multiple(() =>
        {
            Assert.That(again, Is.Zero);
            Assert.That(run.Pending, Is.EqualTo(pot), "and the pot does not move");
            Assert.That(run.RoomsCleared, Is.EqualTo(3));
        });
    }

    [Test]
    public void APayableRoomCannotBeSkipped()
    {
        // Entering twice without clearing must not advance the count, or a player who ran past
        // a fight would be paid for it.
        var run = Descend();
        run.EnterRoom();
        run.EnterRoom();

        Assert.That(run.RoomsCleared, Is.Zero);
    }

    // ---------------------------------------------------------------- events

    [Test]
    public void ClearingARoomAnnouncesWhatItPaid()
    {
        var paid = new List<int>();
        var run = Descend(tier: 2);
        run.RoomCleared += amount => paid.Add(amount);

        for (var index = 0; index < 3; index++)
        {
            run.EnterRoom();
            run.ClearRoom();
        }

        // What the event has to get right is that it fires once per cleared room and reports
        // the amount actually banked. Restating the curve here would mean a rebalance broke a
        // test about an event; the curve has an owner, and this is not it.
        Assert.Multiple(() =>
        {
            Assert.That(paid, Has.Count.EqualTo(3), "once per room, not once per clear attempt");
            Assert.That(paid.Sum(), Is.EqualTo(run.Pending), "and the announcements add up to the pot");
            Assert.That(paid, Is.Ordered.Ascending, "each room announces more than the one before");
        });
    }

    // ---------------------------------------------------------------- persistence

    [Test]
    public void AnUnfinishedRunSurvivesSaveAndReload()
    {
        var run = After(4, rooms: 9, tier: 3);
        var restored = RunState.Restore(run.Capture())!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.Seed, Is.EqualTo(run.Seed), "the same mine has to come back");
            Assert.That(restored.Tier, Is.EqualTo(3));
            Assert.That(restored.Rooms, Is.EqualTo(9));
            Assert.That(restored.RoomsCleared, Is.EqualTo(4));
            Assert.That(restored.Pending, Is.EqualTo(run.Pending));
            Assert.That(restored.CanCamp, Is.True);
            Assert.That(restored.NextRoomPays, Is.EqualTo(run.NextRoomPays));
        });
    }

    [Test]
    public void ASaveTakenMidFightComesBackMidFight()
    {
        var run = After(2);
        run.EnterRoom();

        Assert.That(RunState.Restore(run.Capture())!.CanCamp, Is.False,
            "reloading must not hand the player a free bank");
    }

    [Test]
    public void AFinishedRunComesBackFinished()
    {
        var run = After(3);
        run.Camp();

        Assert.That(RunState.Restore(run.Capture())!.Outcome, Is.EqualTo(RunOutcome.Camped));
    }

    [Test]
    public void ThereIsNoRunToRestoreWhenNobodyIsDownThere()
    {
        Assert.That(RunState.Restore(null), Is.Null);
    }

    [Test]
    public void ACorruptSaveDoesNotProduceANegativePot()
    {
        var restored = RunState.Restore(new SavedRun
        {
            Seed = 1, Tier = 1, Rooms = 5, RoomsCleared = 99, Pending = -40, Outcome = "nonsense"
        })!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.Pending, Is.Zero);
            Assert.That(restored.RoomsCleared, Is.EqualTo(5));
            Assert.That(restored.Outcome, Is.EqualTo(RunOutcome.InProgress));
        });
    }
}
