using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The fort, its ranks, and the rule that decides whether the story is paced or drained.
///
/// Fragments fire on a conjunction of rank and depth, never on either alone. With an OR a
/// player finds whichever tap is cheaper and pulls the whole story through it, and the other
/// half of the game stops mattering — which is the entire reason there are two taps.
/// </summary>
[TestFixture]
public sealed class FortTests
{
    private static RunResult Banked(int rooms, int stones) =>
        new(RunOutcome.Camped, rooms, stones, 0, 1);

    private static RunResult Died(int rooms) => new(RunOutcome.Died, rooms, 0, 20, 1);

    // ------------------------------------------------------------------ rank

    [Test]
    public void EverybodyStartsAtTheBottom()
    {
        Assert.That(new ServiceRecord().Rank, Is.EqualTo(Rank.Yukta));
    }

    [Test]
    public void RankWantsBothDescentsAndStones()
    {
        // A single requirement is a single grind: descents alone rewards repeating the
        // shallowest mine forever, stones alone rewards one lucky run and then nothing.
        var manyRuns = new ServiceRecord();
        for (var run = 0; run < 30; run++) manyRuns.Record(Banked(1, 0));

        var oneBigRun = new ServiceRecord();
        oneBigRun.Record(Banked(30, 900));

        Assert.Multiple(() =>
        {
            Assert.That(manyRuns.Rank, Is.EqualTo(Rank.Yukta),
                "descents alone bought a promotion");
            Assert.That(oneBigRun.Rank, Is.EqualTo(Rank.Yukta),
                "stones alone bought a promotion");
        });
    }

    [Test]
    public void DoingBothEarnsThePromotion()
    {
        var record = new ServiceRecord();
        for (var run = 0; run < 3; run++) record.Record(Banked(4, 10));

        Assert.That(record.Rank, Is.EqualTo(Rank.Sthanika));
    }

    [Test]
    public void ALostRunAddsNothingToStanding()
    {
        // Amulets are the promise that a lost run still pays. Rank is deliberately not — the
        // order is not impressed by how deep somebody got if the stones stayed down there.
        var record = new ServiceRecord();
        for (var run = 0; run < 20; run++) record.Record(Died(12));

        Assert.Multiple(() =>
        {
            Assert.That(record.DescentsSurvived, Is.Zero);
            Assert.That(record.StonesBanked, Is.Zero);
            Assert.That(record.Rank, Is.EqualTo(Rank.Yukta));
        });
    }

    [Test]
    public void RankIsNeverLost()
    {
        var record = new ServiceRecord();
        for (var run = 0; run < 3; run++) record.Record(Banked(4, 10));
        var earned = record.Rank;

        for (var run = 0; run < 10; run++) record.Record(Died(20));

        Assert.That(record.Rank, Is.EqualTo(earned));
    }

    [Test]
    public void PromotionIsAnnouncedOncePerRungAndNeverForStandingStill()
    {
        var record = new ServiceRecord();
        var promotions = 0;
        record.Promoted += _ => promotions++;

        for (var run = 0; run < 12; run++) record.Record(Banked(6, 20));
        var afterClimbing = promotions;

        // More runs at a rank already held must announce nothing.
        for (var run = 0; run < 5; run++) record.Record(Banked(1, 0));

        Assert.That(promotions, Is.EqualTo(afterClimbing));
        Assert.That(promotions, Is.GreaterThan(0));
        Assert.That(promotions, Is.LessThan(Ranks.All.Count));
    }

    [Test]
    public void TheLadderOnlyEverGoesUp()
    {
        var previous = -1;
        foreach (var entry in Ranks.All)
        {
            Assert.That((int)entry.Rank, Is.GreaterThan(previous));
            previous = (int)entry.Rank;
        }

        Assert.That(Ranks.Next(Rank.Mahamatra), Is.Null);
    }

    // ------------------------------------------------------------------ the fort

    [Test]
    public void TheFortHasTenRooms()
    {
        // The design's cap, and what keeps this from becoming the open-city problem the pivot
        // exists to escape. Room eleven is the warning sign in the risk register.
        Assert.That(FortRoster.All, Has.Count.EqualTo(10));
    }

    [Test]
    public void EveryRoomHasSomebodyInIt()
    {
        foreach (var room in FortRoster.All)
        {
            Assert.That(room.Occupant, Is.Not.Empty, room.Id);
            Assert.That(room.Office, Is.Not.Empty, room.Id);
            Assert.That(room.Greeting, Is.Not.Empty, room.Id);
            Assert.That(room.Fragments, Is.Not.Empty, $"{room.Id} has no story in it");
        }
    }

    [Test]
    public void SomethingIsOpenFromTheVeryFirstRun()
    {
        Assert.That(FortRoster.OpenTo(Rank.Yukta), Is.Not.Empty,
            "a new player walked into a fort with every door shut");
    }

    [Test]
    public void TheWholeFortOpensEventually()
    {
        Assert.That(FortRoster.OpenTo(Rank.Mahamatra),
            Has.Count.EqualTo(FortRoster.All.Count));
    }

    [Test]
    public void RoomsOpenInOrderOfRank()
    {
        var previous = 0;
        foreach (var rank in new[]
                 { Rank.Yukta, Rank.Sthanika, Rank.Pradeshika, Rank.Adhyaksha, Rank.Mahamatra })
        {
            var open = FortRoster.OpenTo(rank).Count;
            Assert.That(open, Is.GreaterThanOrEqualTo(previous));
            previous = open;
        }
    }

    // ------------------------------------------------------------------ the conjunction

    [Test]
    public void NoFragmentFiresOnRankAlone()
    {
        // The rule most easily broken by accident, asserted against every fragment with a
        // depth requirement: holding the rank with no depth must not unlock it.
        foreach (var fragment in FortRoster.AllFragments.Where(f => f.RequiredDepth > 1))
            Assert.That(fragment.IsUnlocked(Rank.Mahamatra, deepestEver: 0), Is.False,
                $"{fragment.Id} unlocked on rank alone");
    }

    [Test]
    public void NoFragmentFiresOnDepthAlone()
    {
        foreach (var fragment in FortRoster.AllFragments.Where(f => f.RequiredRank > Rank.Yukta))
            Assert.That(fragment.IsUnlocked(Rank.Yukta, deepestEver: 999), Is.False,
                $"{fragment.Id} unlocked on depth alone");
    }

    [Test]
    public void BothTogetherUnlockIt()
    {
        foreach (var fragment in FortRoster.AllFragments)
            Assert.That(fragment.IsUnlocked(Rank.Mahamatra, 999), Is.True, fragment.Id);
    }

    [Test]
    public void EveryFragmentHasSomethingToSay()
    {
        foreach (var fragment in FortRoster.AllFragments)
            Assert.That(fragment.Text, Is.Not.Empty, fragment.Id);
    }

    [Test]
    public void FragmentIdsAreUnique()
    {
        // They key the already-heard set. A duplicate would silently mute one of them.
        Assert.That(FortRoster.AllFragments.Select(f => f.Id).ToList(), Is.Unique);
    }

    [Test]
    public void NobodyRepeatsThemselves()
    {
        var legacy = new Legacy();
        var fragment = FortRoster.AllFragments[0];

        Assert.That(legacy.Hear(fragment.Id), Is.True);
        Assert.That(legacy.Hear(fragment.Id), Is.False);
        Assert.That(legacy.HasHeard(fragment.Id), Is.True);
    }

    [Test]
    public void WhatWasHeardSurvivesDeathAndReload()
    {
        var player = PlayerCharacter.NewGame();
        var fragment = FortRoster.AllFragments[0];

        player.Legacy.Hear(fragment.Id);
        for (var run = 0; run < 4; run++) player.Legacy.Service.Record(Banked(5, 20));

        var saved = player.Legacy.Capture();
        Succession.Promote(player, Died(3), 9, 3);

        Assert.That(player.Legacy.HasHeard(fragment.Id), Is.True,
            "a death made the fort repeat itself");

        var loaded = new Legacy();
        loaded.Restore(saved);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.HasHeard(fragment.Id), Is.True);
            Assert.That(loaded.Service.Rank, Is.EqualTo(player.Legacy.Service.Rank));
        });
    }

    [Test]
    public void StandingSurvivesTheDeathOfThePersonWhoEarnedIt()
    {
        var player = PlayerCharacter.NewGame();
        for (var run = 0; run < 4; run++) player.Legacy.Service.Record(Banked(5, 20));
        var earned = player.Legacy.Service.Rank;

        Succession.Promote(player, Died(3), 9, 3);

        Assert.That(player.Legacy.Service.Rank, Is.EqualTo(earned),
            "a successor arrived demoted, which is a successor nobody wants to play");
    }
}
