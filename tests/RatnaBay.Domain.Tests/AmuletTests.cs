using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The ratchet.
///
/// Iteration 17 exists to retire one risk: *does a losing run still pull you back in?* Every
/// assertion here is ultimately about that. An amulet earned only by surviving would make a bad
/// run worth nothing, which is the failure the iteration was written to prevent — so the
/// central test is that dying two rooms deeper than ever before still pays.
/// </summary>
[TestFixture]
public sealed class AmuletTests
{
    private static PlayerCharacter NewPlayer() => PlayerCharacter.NewGame();

    private static RunResult Died(int rooms) =>
        new(RunOutcome.Died, rooms, 0, 12, 1);

    // ------------------------------------------------------------------ the central rule

    [Test]
    public void ALostRunStillEarnsIfItWentDeeperThanEverBefore()
    {
        var player = NewPlayer();

        var earned = player.Legacy.RecordDepth(3);

        Assert.That(earned, Is.Not.Empty,
            "a run that reached a new best paid nothing, which is the whole iteration");
        Assert.That(player.Legacy.Amulets, Is.Not.Empty);
    }

    [Test]
    public void DyingDoesNotTakeTheAmuletsWithIt()
    {
        var player = NewPlayer();
        player.Legacy.RecordDepth(6);
        var held = player.Legacy.Amulets.ToList();

        Succession.Promote(player, Died(6), mineSeed: 41, roomIndex: 6);

        Assert.That(player.Legacy.Amulets, Is.EquivalentTo(held));
    }

    [Test]
    public void DyingDoesNotLowerTheHighWaterMark()
    {
        // The order remembers how far it got even when the person who got there did not come
        // back. Without this, a death would let the same amulet be earned twice.
        var player = NewPlayer();
        player.Legacy.RecordDepth(6);

        Succession.Promote(player, Died(6), mineSeed: 41, roomIndex: 6);

        Assert.That(player.Legacy.DeepestEver, Is.EqualTo(6));
        Assert.That(player.Legacy.RecordDepth(6), Is.Empty);
    }

    // ------------------------------------------------------------------ earning

    [Test]
    public void ShallowRunsRepeatedForeverEarnNothing()
    {
        var player = NewPlayer();
        player.Legacy.RecordDepth(3);
        var held = player.Legacy.Amulets.Count;

        for (var run = 0; run < 20; run++) player.Legacy.RecordDepth(3);

        Assert.That(player.Legacy.Amulets, Has.Count.EqualTo(held),
            "amulets could be farmed by repeating a run that reached nothing new");
    }

    [Test]
    public void OneVeryGoodRunEarnsEveryMilestoneItPassed()
    {
        // Returning only the deepest would quietly swallow the rest, and a player who had an
        // exceptional run would be paid for part of it.
        var player = NewPlayer();

        var earned = player.Legacy.RecordDepth(10);

        Assert.That(earned, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(earned, Is.Unique);
    }

    [Test]
    public void EarningIsCumulativeAcrossRuns()
    {
        var player = NewPlayer();
        player.Legacy.RecordDepth(3);
        player.Legacy.RecordDepth(6);

        Assert.That(player.Legacy.Amulets, Has.Count.EqualTo(2));
    }

    [Test]
    public void NoAmuletIsEverHeldTwice()
    {
        var player = NewPlayer();
        player.Legacy.RecordDepth(3);
        player.Legacy.RestoreAmulets(
            player.Legacy.Amulets.Concat(player.Legacy.Amulets).ToList(), 3);

        Assert.That(player.Legacy.Amulets, Is.Unique);
    }

    [Test]
    public void TheNextMilestoneIsAlwaysAheadOfTheBest()
    {
        foreach (var best in new[] { 0, 3, 6, 10 })
        {
            var next = AmuletCatalog.NextAfter(best);
            Assert.That(next, Is.Not.Null, $"nothing to aim at from room {best}");
            Assert.That(next!.Value.Depth, Is.GreaterThan(best));
        }
    }

    [Test]
    public void TheLadderEventuallyEnds()
    {
        Assert.That(AmuletCatalog.NextAfter(AmuletCatalog.DeepestMilestone), Is.Null);
    }

    // ------------------------------------------------------------------ the effects

    [Test]
    public void TheLampAddsASocket()
    {
        var player = NewPlayer();
        var before = player.Stones.Capacity;

        player.Legacy.RestoreAmulets(new[] { AmuletCatalog.SocketId }, 3);

        Assert.That(player.Stones.Capacity, Is.EqualTo(before + 1));
    }

    [Test]
    public void SteadyHandMakesTheFirstBlowOfARoomAnOpening()
    {
        static float FirstBlow(bool withAmulet)
        {
            var player = NewPlayer();
            if (withAmulet)
                player.Legacy.RestoreAmulets(new[] { AmuletCatalog.FirstBloodId }, 6);

            player.Combat.EnterRoom();

            var enemy = new Enemy(
                new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = 500f },
                "bandit.01");

            player.Combat.TryAttack(enemy);
            return 500f - enemy.Health;
        }

        Assert.That(FirstBlow(withAmulet: true), Is.GreaterThan(FirstBlow(withAmulet: false)));
    }

    [Test]
    public void SteadyHandIsSpentAfterOneBlowAndComesBackWithTheNextRoom()
    {
        var player = NewPlayer();
        player.Legacy.RestoreAmulets(new[] { AmuletCatalog.FirstBloodId }, 6);

        var enemy = new Enemy(
            new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = 5000f },
            "bandit.01");

        player.Combat.EnterRoom();
        player.Combat.TryAttack(enemy);
        var first = 5000f - enemy.Health;

        player.Combat.Tick(5f);
        var before = enemy.Health;
        player.Combat.TryAttack(enemy);
        var second = before - enemy.Health;

        Assert.That(second, Is.LessThan(first), "the opening was not spent");

        player.Combat.EnterRoom();
        player.Combat.Tick(5f);
        var beforeThird = enemy.Health;
        player.Combat.TryAttack(enemy);

        Assert.That(beforeThird - enemy.Health, Is.EqualTo(first).Within(0.01f),
            "a new room did not re-arm the opening");
    }

    [Test]
    public void LongMemoryLeavesTheSuccessorMore()
    {
        static int Lost(bool withAmulet)
        {
            var player = NewPlayer();
            if (withAmulet)
                player.Legacy.RestoreAmulets(new[] { AmuletCatalog.LongMemoryId }, 10);

            player.Inventory.Add("health_potion", "Health Potion", 20, "potion");
            return Succession.Promote(player, Died(4), 7, 4).ItemsLost;
        }

        Assert.That(Lost(withAmulet: true), Is.LessThan(Lost(withAmulet: false)));
    }

    [Test]
    public void NoAmuletCanMakeDeathFree()
    {
        var player = NewPlayer();
        player.Legacy.RestoreAmulets(
            AmuletCatalog.All.Select(a => a.Id).ToList(), 30);

        player.Inventory.Add("health_potion", "Health Potion", 20, "potion");

        Assert.That(Succession.Promote(player, Died(4), 7, 4).ItemsLost,
            Is.GreaterThan(0), "death stopped costing anything");
    }

    [Test]
    public void SecondBreathOnlyHelpsOutOfCombat()
    {
        static float Regained(bool withAmulet, bool inCombat)
        {
            var player = NewPlayer();
            if (withAmulet)
                player.Legacy.RestoreAmulets(new[] { AmuletCatalog.SecondBreathId }, 21);

            player.Vitals.SpendStamina(60f);
            var before = player.Vitals.Stamina;
            player.Vitals.Tick(1f, inCombat);
            return player.Vitals.Stamina - before;
        }

        Assert.Multiple(() =>
        {
            Assert.That(Regained(true, inCombat: false),
                Is.GreaterThan(Regained(false, inCombat: false)));

            // Speeding recovery mid-fight would remove the reason stamina exists.
            Assert.That(Regained(true, inCombat: true),
                Is.EqualTo(Regained(false, inCombat: true)).Within(0.001f));
        });
    }

    // ------------------------------------------------------------------ saving

    [Test]
    public void AmuletsSurviveASaveAndLoad()
    {
        var player = NewPlayer();
        player.Legacy.RecordDepth(10);
        var saved = player.Legacy.Capture();

        var loaded = new Legacy();
        loaded.Restore(saved);

        Assert.That(loaded.Amulets, Is.EquivalentTo(player.Legacy.Amulets));
        Assert.That(loaded.DeepestEver, Is.EqualTo(10));
    }

    [Test]
    public void AnAmuletRemovedFromTheCatalogueDoesNotComeBackOutOfAnOldSave()
    {
        var loaded = new Legacy();
        loaded.RestoreAmulets(new[] { "amulet.deleted", AmuletCatalog.SocketId }, 5);

        Assert.That(loaded.Amulets, Is.EqualTo(new[] { AmuletCatalog.SocketId }));
    }

    [Test]
    public void EveryAmuletSaysWhatItDoes()
    {
        foreach (var amulet in AmuletCatalog.All)
        {
            Assert.That(amulet.DisplayName, Is.Not.Empty);
            Assert.That(amulet.Description, Is.Not.Empty, amulet.Id);
        }

        Assert.That(AmuletCatalog.All.Select(a => a.Effect),
            Is.EquivalentTo(System.Enum.GetValues<AmuletEffect>()));
    }
}
