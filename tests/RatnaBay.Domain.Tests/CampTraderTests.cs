using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The trader you whistle down a cleared shaft.
///
/// The rule these exist to defend is that the pot cannot be laundered. Everything a camp
/// trader sells is spent before the run ends; if at-risk stones could be turned into anything
/// that survives a death, pressing on would become strictly safer and the decision the whole
/// game rests on would quietly stop being one.
/// </summary>
public class CampTraderTests
{
    private static RunState AfterRooms(int count, int tier = 1)
    {
        var run = RunState.Begin(seed: 4211, tier, rooms: 12);
        for (var index = 0; index < count; index++)
        {
            run.EnterRoom();
            run.ClearRoom();
        }

        return run;
    }

    private static Inventory WithLoot(int count)
    {
        var inventory = new Inventory();
        inventory.Clear();
        if (count > 0) inventory.Add("bandit_loot", "Bandit Satchel", count, "loot");
        return inventory;
    }

    // ---------------------------------------------------------------- the price of a whistle

    [Test]
    [TestCase(1, 0, 5)]
    [TestCase(1, 1, 10)]
    [TestCase(1, 2, 15)]
    [TestCase(2, 0, 10)]
    [TestCase(3, 0, 15)]
    [TestCase(3, 2, 45)]
    public void EachTraderCostsMoreThanTheLastAndDeeperCostsMoreStill(int tier, int called, int cost)
    {
        Assert.That(CampTrader.CostToCall(tier, called), Is.EqualTo(cost));
    }

    [Test]
    public void TheSecondTraderIsAlwaysDearerThanTheFirst()
    {
        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        for (var called = 0; called < 5; called++)
        {
            Assert.That(CampTrader.CostToCall(tier, called + 1),
                Is.GreaterThan(CampTrader.CostToCall(tier, called)),
                $"tier {tier}, call {called + 2}");
        }
    }

    [Test]
    public void ADeepMinePaysMoreAndChargesMoreInTheSameProportion()
    {
        // The shape of the decision has to be the same at every depth, exactly as the entry
        // price and the risk ratio are. A player learns the trade-off once.
        Assert.That(CampTrader.CostToCall(3, 0) / (float)CampTrader.CostToCall(1, 0),
            Is.EqualTo(RunState.PayoutFor(4, 3) / (float)RunState.PayoutFor(4, 1)).Within(0.001f));
    }

    // ---------------------------------------------------------------- calling one

    [Test]
    public void ATraderCanOnlyBeWhistledForAtAClearedRoomsExit()
    {
        var run = AfterRooms(4);          // 10 in the pot, call costs 5
        Assert.That(run.CanCallTrader, Is.True);

        run.EnterRoom();
        Assert.That(run.CanCallTrader, Is.False, "not in the middle of a fight");
    }

    [Test]
    public void APotThatCannotCoverTheWhistleCannotCallOne()
    {
        var run = AfterRooms(2);          // 3 in the pot, call costs 5
        Assert.Multiple(() =>
        {
            Assert.That(run.TraderCallCost, Is.EqualTo(5));
            Assert.That(run.CanCallTrader, Is.False);
        });
    }

    [Test]
    public void CallingSpendsOutOfThePotAndRaisesThePriceOfTheNext()
    {
        var run = AfterRooms(5);          // 15 in the pot

        Assert.That(run.TrySpend(run.TraderCallCost), Is.True);
        run.NoteTraderCalled();

        Assert.Multiple(() =>
        {
            Assert.That(run.Pending, Is.EqualTo(10), "five stones are not being carried out");
            Assert.That(run.TradersCalled, Is.EqualTo(1));
            Assert.That(run.TraderCallCost, Is.EqualTo(10));
        });
    }

    [Test]
    public void SpendingMoreThanThePotHoldsSpendsNothing()
    {
        var run = AfterRooms(3);          // 6 in the pot

        Assert.Multiple(() =>
        {
            Assert.That(run.TrySpend(7), Is.False);
            Assert.That(run.Pending, Is.EqualTo(6));
        });
    }

    [Test]
    public void WhatIsSpentAtACampIsStillLostByDying()
    {
        // The rule the whole thing rests on. Calling a trader must not be a way to bank: a
        // player who spends five and dies has lost the five as surely as the rest.
        var run = AfterRooms(5);
        run.TrySpend(5);
        run.NoteTraderCalled();

        var died = run.Die();

        Assert.Multiple(() =>
        {
            Assert.That(died.StonesLost, Is.EqualTo(10), "only what was left in the pot");
            Assert.That(run.Pending, Is.Zero);
        });
    }

    // ---------------------------------------------------------------- what they carry

    [Test]
    public void NothingOnOfferOutlivesTheDescent()
    {
        // The constraint that keeps the pot from being launderable. A camp trader carries
        // consumables; permanent gear stays at the surface stall.
        foreach (var good in CampTrader.Stock)
        {
            Assert.That(EquipmentCatalog.IsWeapon(good.ItemId), Is.False, good.Name);
            Assert.That(EquipmentCatalog.IsArmour(good.ItemId), Is.False, good.Name);
        }
    }

    [Test]
    public void EverythingOnOfferCostsSomething()
    {
        Assert.That(CampTrader.Stock, Is.Not.Empty);
        Assert.That(CampTrader.Stock,
            Has.All.Property(nameof(CampGood.Stones)).GreaterThan(0));
    }

    [Test]
    public void TheyDoNotSellStonesForStones()
    {
        // The pot is jiva stones, so a stone priced in stones is a circle.
        Assert.That(CampTrader.Stock.Any(good => good.ItemId == SoulCrystals.LesserId), Is.False);
    }

    // ---------------------------------------------------------------- what they buy

    [Test]
    public void LootFinallyHasSomewhereToGo()
    {
        // Every kill has dropped a satchel since the first bandit died and nothing in the game
        // has ever bought one.
        var pack = WithLoot(7);
        var run = AfterRooms(4);

        var paid = CampTrader.SellLoot(pack, run);

        Assert.Multiple(() =>
        {
            Assert.That(paid, Is.EqualTo(7));
            Assert.That(pack.CountOf("bandit_loot"), Is.Zero);
            Assert.That(run.Pending, Is.EqualTo(17), "ten from four rooms, seven from the pack");
        });
    }

    [Test]
    public void WhatIsSoldAtACampIsAtRiskLikeEverythingElse()
    {
        // Into the pot, not the pack. Selling at a camp must not be a way to walk something
        // out of a mine that dying would otherwise have taken.
        var pack = WithLoot(9);
        var run = AfterRooms(3);
        CampTrader.SellLoot(pack, run);

        Assert.That(run.Die().StonesLost, Is.EqualTo(15), "six from rooms, nine from loot");
    }

    [Test]
    public void APackOfLootCanPayForTheWhistleItself()
    {
        // What makes calling a judgement rather than a toll: enough satchels and the trader
        // pays their own fare.
        var pack = WithLoot(6);
        var run = AfterRooms(3);          // 6 in the pot, call costs 5

        var before = run.Pending;
        CampTrader.SellLoot(pack, run);

        Assert.That(run.Pending - before, Is.GreaterThan(run.TraderCallCost));
    }

    [Test]
    public void TheyTakeLootAndNothingElse()
    {
        var pack = new Inventory();
        pack.Clear();
        pack.Add("health_potion", "Health Potion", 3, "potion");
        pack.Add("key.northwatch.dungeon", "Watchpost Key", 1, "key");
        pack.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 4, SoulCrystals.ItemKind);

        var run = AfterRooms(4);
        var paid = CampTrader.SellLoot(pack, run);

        Assert.Multiple(() =>
        {
            Assert.That(paid, Is.Zero);
            Assert.That(pack.CountOf("health_potion"), Is.EqualTo(3));
            Assert.That(pack.CountOf(SoulCrystals.LesserId), Is.EqualTo(4), "the pack is not the pot");
            Assert.That(pack.Has("key.northwatch.dungeon"), Is.True);
        });
    }

    [Test]
    public void NothingIsSoldToAFinishedRun()
    {
        var pack = WithLoot(5);
        var run = AfterRooms(3);
        run.Camp();

        Assert.Multiple(() =>
        {
            Assert.That(CampTrader.SellLoot(pack, run), Is.Zero);
            Assert.That(pack.CountOf("bandit_loot"), Is.EqualTo(5), "and the pack is untouched");
        });
    }

    // ---------------------------------------------------------------- persistence

    [Test]
    public void HowManyHaveComeSurvivesSettingTheDescentAside()
    {
        var run = AfterRooms(6);
        run.TrySpend(run.TraderCallCost);
        run.NoteTraderCalled();
        run.TrySpend(run.TraderCallCost);
        run.NoteTraderCalled();

        var restored = RunState.Restore(run.Capture())!;

        Assert.Multiple(() =>
        {
            Assert.That(restored.TradersCalled, Is.EqualTo(2));
            Assert.That(restored.TraderCallCost, Is.EqualTo(15),
                "the third is still dearer after a reload");
            Assert.That(restored.Pending, Is.EqualTo(run.Pending));
        });
    }
}
