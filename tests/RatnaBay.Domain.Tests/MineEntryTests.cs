using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The price of a way down, and the yard it is bought in.
///
/// This is the half of the loop that did not exist. Five recorded playtests answered the camp
/// decision in about a second, and each time the response was to make the descent more
/// dangerous — none of which addressed the reason, which is that banking stones bought
/// nothing. These assertions exist to keep the reward side real.
/// </summary>
public class MineEntryTests
{
    private static Inventory WithStones(int count)
    {
        var inventory = new Inventory();
        if (count > 0)
            inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, count,
                SoulCrystals.ItemKind);

        return inventory;
    }

    [Test]
    public void TheShallowestMineIsAlwaysFree()
    {
        // A player with nothing must always be able to descend, or a bad run is the end of
        // the game rather than the end of a run.
        Assert.Multiple(() =>
        {
            Assert.That(MineEntry.CostOf(1), Is.Zero);
            Assert.That(MineEntry.CanAfford(WithStones(0), 1), Is.True);
            Assert.That(MineEntry.TryOpen(WithStones(0), 1), Is.True);
        });
    }

    [Test]
    [TestCase(2, 8)]
    [TestCase(3, 24)]
    [TestCase(4, 48)]
    [TestCase(5, 80)]
    public void EachStepDownCostsMoreThanTheLast(int tier, int cost)
    {
        Assert.That(MineEntry.CostOf(tier), Is.EqualTo(cost));
    }

    [Test]
    public void CostRisesFasterThanPayoutDoes()
    {
        // The gap that stops "the deepest mine I can afford" from being the automatic answer.
        // A tier pays N x T a room, which is linear; the door is quadratic.
        for (var tier = 2; tier < MineEntry.MaxTier; tier++)
        {
            var costRatio = MineEntry.CostOf(tier + 1) / (float)MineEntry.CostOf(tier);
            var payoutRatio = (tier + 1) / (float)tier;

            Assert.That(costRatio, Is.GreaterThan(payoutRatio),
                $"tier {tier + 1} is a strictly better deal than tier {tier}");
        }
    }

    [Test]
    [TestCase(1, 0)]
    [TestCase(2, 3)]
    [TestCase(3, 4)]
    [TestCase(4, 5)]
    [TestCase(5, 6)]
    public void ADeepMineTakesRealWorkBeforeItHasPaidForItself(int tier, int rooms)
    {
        // Buying a tier-three door and camping after two rooms must be a loss, or depth is
        // free money rather than a gamble. Runs have been ending around seven to nine rooms,
        // so a break-even of four means over half a descent is spent paying for the way in.
        Assert.That(MineEntry.RoomsToBreakEven(tier), Is.EqualTo(rooms));
    }

    [Test]
    public void OpeningAMineSpendsTheStones()
    {
        var purse = WithStones(30);

        Assert.Multiple(() =>
        {
            Assert.That(MineEntry.TryOpen(purse, 3), Is.True);
            Assert.That(purse.CountOf(SoulCrystals.LesserId), Is.EqualTo(6));
        });
    }

    [Test]
    public void ARefusedDescentSpendsNothing()
    {
        // A player who asks for a door they cannot afford must not be left poorer for asking.
        var purse = WithStones(10);

        Assert.Multiple(() =>
        {
            Assert.That(MineEntry.TryOpen(purse, 4), Is.False);
            Assert.That(purse.CountOf(SoulCrystals.LesserId), Is.EqualTo(10));
        });
    }

    [Test]
    public void ThePurseDecidesHowDeepTheOrderWillSell()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MineEntry.DeepestAffordable(WithStones(0)), Is.EqualTo(1));
            Assert.That(MineEntry.DeepestAffordable(WithStones(8)), Is.EqualTo(2));
            Assert.That(MineEntry.DeepestAffordable(WithStones(45)), Is.EqualTo(3));
            Assert.That(MineEntry.DeepestAffordable(WithStones(999)), Is.EqualTo(MineEntry.MaxTier));
        });
    }

    [Test]
    public void NoDepthIsSoldBeyondWhatTheOrderWillWriteDown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MineEntry.CostOf(99), Is.EqualTo(MineEntry.CostOf(MineEntry.MaxTier)));
            Assert.That(MineEntry.CostOf(-4), Is.Zero);
            Assert.That(MineEntry.DescriptionOf(99), Is.Not.Empty);
        });
    }

    [Test]
    public void ARunAtTierThreeIsWorthThreeTimesOneAtTierOne()
    {
        // The reason to buy a door at all. Ties the entry price back to the run ledger, so a
        // rebalance of one without the other fails here rather than in play.
        Assert.That(RunState.PayoutFor(6, 3), Is.EqualTo(RunState.PayoutFor(6, 1) * 3));
    }
}

/// <summary>The yard above the mines.</summary>
public class SurfaceTests
{
    [Test]
    public void TheYardIsAWorldTheGameCanAlreadyLoad()
    {
        var json = WorldManifest.Serialize(Surface.Build());

        Assert.That(WorldManifest.TryParse(json, out var yard, out var error), Is.True, error);
        Assert.That(yard!.Id, Is.EqualTo(Surface.Id));
    }

    [Test]
    public void NothingIsWaitingInIt()
    {
        // The surface is where a run is not happening. Anything hostile here would make the
        // one safe place in the game unsafe, and the whole point is having somewhere to stand.
        var yard = Surface.Build();

        Assert.Multiple(() =>
        {
            Assert.That(yard.Spawns, Is.Empty);
            Assert.That(yard.Rooms, Is.Empty, "a yard is not a mine and must never pay like one");
            Assert.That(yard.Doors, Is.Empty);
        });
    }

    [Test]
    public void ThePlayerArrivesInsideTheWallsAndNotInThem()
    {
        var yard = Surface.Build();
        var spawn = yard.PlayerSpawn.Position;

        var buried = yard.Geometry.Any(solid =>
            solid.Max.Y > 0.2f && solid.Min.Y < 3f
            && spawn.X > solid.Min.X - 0.45f && spawn.X < solid.Max.X + 0.45f
            && spawn.Z > solid.Min.Z - 0.45f && spawn.Z < solid.Max.Z + 0.45f);

        Assert.That(buried, Is.False);
    }

    [Test]
    public void TheFixturesCanBeToldApartByStandingAtThem()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Surface.FixtureAt(Surface.Shaft), Is.EqualTo(SurfaceFixture.Shaft));
            Assert.That(Surface.FixtureAt(Surface.Trader), Is.EqualTo(SurfaceFixture.Trader));
            Assert.That(Surface.FixtureAt(Surface.Stambha), Is.EqualTo(SurfaceFixture.Stambha));
            Assert.That(Surface.FixtureAt(Surface.Spawn), Is.EqualTo(SurfaceFixture.None),
                "arriving must not immediately be standing at something");
        });
    }

    [Test]
    public void TheFixturesAreFarEnoughApartToBeSeparateThings()
    {
        var places = new[] { Surface.Shaft, Surface.Trader, Surface.Stambha };

        foreach (var pair in places.SelectMany((a, i) => places.Skip(i + 1).Select(b => (a, b))))
            Assert.That(pair.a.FlatDistanceTo(pair.b),
                Is.GreaterThan(Surface.InteractRange * 2f),
                "two fixtures within reach of one spot would be one ambiguous fixture");
    }

    [Test]
    public void TheYardIsWalledOnEverySide()
    {
        // Falling out of the world at the one place the player is meant to feel safe would be
        // a memorable first impression for the wrong reason.
        var index = new StaticCollisionIndex();
        index.Rebuild(Surface.Build().Geometry
            .Where(geometry => geometry.Solid)
            .Select(geometry => geometry.ToCollisionBox()));

        foreach (var heading in new[] { (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
        {
            var at = new WorldPoint(Surface.Spawn.X, 1.7f, Surface.Spawn.Z);
            for (var step = 0; step < 400; step++)
                at = index.Move(at, new WorldPoint(heading.Item1 * 0.2f, 0f, heading.Item2 * 0.2f), 0.45f);

            Assert.That(MathF.Abs(at.X), Is.LessThan(16f), $"walked out heading {heading}");
            Assert.That(MathF.Abs(at.Z), Is.LessThan(16f), $"walked out heading {heading}");
        }
    }
}
