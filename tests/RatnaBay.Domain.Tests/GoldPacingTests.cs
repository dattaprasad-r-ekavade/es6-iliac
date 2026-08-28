using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// How long the player has to work for the thing in the shop window.
///
/// The production plan carried this as a known gap in exactly these words: *"Gold pacing is a
/// guess. Roughly 250 a run against a 450 sword, never measured."* These assertions are the
/// measurement, and they exist to hold it rather than to prove it once.
///
/// **Gold has one repeatable source.** Quests pay a one-off forty and never again; every other
/// coin in the game comes from `Encounter` paying <c>Random.Shared.Next(5, 18)</c> on a kill —
/// five to seventeen, so **eleven on average**. That single fact makes the economy a function
/// of one number nobody has been watching: how many enemies a generated mine contains.
///
/// So the pacing is not tuned by a gold constant. It is tuned, invisibly, by the spawn table.
/// Anybody making mines busier is giving the player a raise, and this is the test that says so.
/// </summary>
[TestFixture]
public sealed class GoldPacingTests
{
    /// <summary>Mean of Random.Shared.Next(5, 18): inclusive of 5, exclusive of 18.</summary>
    private const float GoldPerKill = (5 + 17) / 2f;

    /// <summary>What a run is worth if every enemy in the mine is killed.</summary>
    private static float RunGold(int seed, int rooms, int depth) =>
        MineGenerator.Generate(seed, rooms, depth).Spawns.Count * GoldPerKill;

    private static float AverageRun(int rooms, int depth) =>
        Enumerable.Range(0, 40).Select(seed => RunGold(seed * 7 + 1, rooms, depth)).Average();

    [Test]
    public void AShortRunIsWorthAboutAHundredGold()
    {
        // The floor of the economy: the shallowest paying run. If this drifts, everything
        // priced in the stall drifts with it and nothing else in the game will say so.
        var gold = AverageRun(rooms: 4, depth: 1);

        Assert.That(gold, Is.InRange(60f, 160f),
            $"a four-room descent now pays about {gold:0} gold");
    }

    [Test]
    public void GoingDeeperPaysMore()
    {
        // Not a tuning choice — a consequence of deeper mines being busier. Asserted because
        // the spawn table is where it actually lives, and nothing there mentions gold.
        Assert.That(AverageRun(10, 3), Is.GreaterThan(AverageRun(4, 1)));
    }

    [Test]
    public void TheSwordIsAboutFourRunsAway()
    {
        // The number the plan was guessing at. The stall's mid-tier weapon is 450 and a decent
        // run is worth roughly a hundred, so it is bought in about four or five descents --
        // long enough to be a goal and short enough to still be one.
        //
        // Two runs and it is not a decision; a dozen and the stall is scenery.
        const int SwordPrice = 450;
        var runs = SwordPrice / AverageRun(rooms: 6, depth: 2);

        Assert.That(runs, Is.InRange(2.5f, 7f), $"the sword is now {runs:0.0} runs away");
    }

    [Test]
    public void NoSingleRunBuysTheBestThingInTheShop()
    {
        // The stall's top item is 900. A run that clears it outright removes the only reason
        // to come back up with anything, which is the loop this game is built on.
        const int DearestItem = 900;

        var best = Enumerable.Range(0, 60)
            .Select(seed => RunGold(seed * 13 + 5, rooms: 12, depth: 4))
            .Max();

        Assert.That(best, Is.LessThan(DearestItem),
            $"the luckiest deep run now pays {best:0}, which buys the shop out in one descent");
    }

    [Test]
    public void TwoPlayersAtTheSameDepthHaveTheSameEconomy()
    {
        // Measured rather than assumed, and the measurement was a surprise: the spawn count is
        // a **pure function of rooms and depth**, identical across every seed. The first
        // version of this test allowed a 2.6x spread between the luckiest and unluckiest seed
        // and passed trivially, because the real spread is zero.
        //
        // Worth pinning rather than deleting. It means gold is fully predictable from the
        // shaft screen before the player pays, and it means nobody is quietly handed a harder
        // or richer evening than anybody else. If seeded variety in spawn counts is ever
        // wanted, this is the test that should be made to fail on purpose first.
        var runs = Enumerable.Range(0, 40)
            .Select(seed => RunGold(seed * 7 + 1, rooms: 6, depth: 2))
            .Distinct()
            .ToList();

        Assert.That(runs, Has.Count.EqualTo(1),
            "the same depth now pays different amounts depending on the seed");
    }

    [Test]
    public void TheCurveIsMeasured()
    {
        // The numbers the production plan was guessing at, written down so a change to the
        // spawn table has to come past them. Its guess was "roughly 250 a run"; a mid run is
        // actually 132, and 250 is not reached until about depth three.
        Assert.Multiple(() =>
        {
            Assert.That(AverageRun(4, 1), Is.EqualTo(66f).Within(22f), "four rooms, depth one");
            Assert.That(AverageRun(6, 2), Is.EqualTo(132f).Within(33f), "six rooms, depth two");
            Assert.That(AverageRun(10, 3), Is.EqualTo(319f).Within(66f), "ten rooms, depth three");
            Assert.That(AverageRun(12, 4), Is.EqualTo(429f).Within(88f), "twelve rooms, depth four");
        });
    }
}
