using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// Death, and the person who takes the lamp afterwards.
///
/// The rule these assertions exist to defend is that a loss must always be recoverable. A
/// roguelike death that costs nothing is not a death; one that cannot be climbed back out of
/// is a quit button. Every number here sits between those two.
/// </summary>
public class SuccessionTests
{
    private static PlayerCharacter Dipadhara()
    {
        var player = PlayerCharacter.NewGame();
        player.SelectLifePath(StoryDirector.RouteMage);
        return player;
    }

    private static RunResult Died(int rooms, int stones, int tier = 1) =>
        new(RunOutcome.Died, rooms, 0, stones, tier);

    // ---------------------------------------------------------------- what carries over

    [Test]
    public void LevelsEarnedAreKeptAndProgressTowardTheNextIsNot()
    {
        var player = Dipadhara();
        player.Vitals.AddXp(player.Vitals.XpToLevel);   // level 2
        player.Vitals.AddXp(20);                        // and some of the way to 3

        var result = Succession.Promote(player, Died(4, 10), mineSeed: 1, roomIndex: 4);

        Assert.Multiple(() =>
        {
            Assert.That(player.Vitals.Level, Is.EqualTo(2), "a rank once held is never lost");
            Assert.That(player.Vitals.Xp, Is.Zero);
            Assert.That(result.UnspentXpCleared, Is.EqualTo(20));
        });
    }

    [Test]
    public void RepeatedDeathsStopAdvancementRatherThanReverseIt()
    {
        // The wall this design must not build: dying over and over should mean standing still,
        // never sliding backwards until the game is unplayable.
        var player = Dipadhara();
        player.Vitals.AddXp(player.Vitals.XpToLevel * 3);
        var level = player.Vitals.Level;

        for (var death = 0; death < 6; death++)
            Succession.Promote(player, Died(1, 1), mineSeed: death, roomIndex: 1);

        Assert.That(player.Vitals.Level, Is.EqualTo(level));
    }

    [Test]
    public void TheSuccessorInheritsTheLifePathAndItsTraining()
    {
        var player = Dipadhara();
        var destruction = player.Skills.LevelOf(Skills.Destruction);

        Succession.Promote(player, Died(3, 6), mineSeed: 1, roomIndex: 3);

        Assert.Multiple(() =>
        {
            Assert.That(player.LifePath.SpellMultiplier, Is.EqualTo(LifePath.Gifted));
            Assert.That(player.Skills.LevelOf(Skills.Destruction), Is.EqualTo(destruction),
                "the order's training is not buried with one body");
        });
    }

    [Test]
    public void TheSuccessorArrivesWhole()
    {
        var player = Dipadhara();
        player.Vitals.TakeDamage(70f);

        Succession.Promote(player, Died(2, 3), mineSeed: 1, roomIndex: 2);

        Assert.That(player.Vitals.Health, Is.EqualTo(player.Vitals.MaxHealth));
    }

    // ---------------------------------------------------------------- what is lost

    [Test]
    public void HalfThePackGoesIntoTheGround()
    {
        var player = Dipadhara();
        player.Inventory.Add("health_potion", "Health Potion", 4, "potion");

        var potions = player.Inventory.CountOf("health_potion");
        Succession.Promote(player, Died(3, 6), mineSeed: 1, roomIndex: 3);

        Assert.That(player.Inventory.CountOf("health_potion"),
            Is.EqualTo(potions - (int)MathF.Ceiling(potions * Succession.PackLost)));
    }

    [Test]
    public void ASinglePotionIsASinglePotionLost()
    {
        // Rounding the other way would make small packs immortal, and the first death of a
        // run is exactly when the pack is small.
        var player = PlayerCharacter.NewGame();
        player.Inventory.Clear();
        player.Inventory.Add("health_potion", "Health Potion", 1, "potion");

        Succession.Promote(player, Died(1, 1), mineSeed: 1, roomIndex: 1);

        Assert.That(player.Inventory.CountOf("health_potion"), Is.Zero);
    }

    [Test]
    public void KeysAreNotLoot()
    {
        // Losing the key to a door already opened would strand the player behind their own
        // progress, which is the unrecoverable state this whole design avoids.
        var player = Dipadhara();
        player.Inventory.Add("key.northwatch.dungeon", "Watchpost Key", 1, "key");

        Succession.Promote(player, Died(5, 15), mineSeed: 1, roomIndex: 5);

        Assert.That(player.Inventory.Has("key.northwatch.dungeon"), Is.True);
    }

    [Test]
    public void TheSuccessorIsNeverSentDownUnarmed()
    {
        // "Half your gear" cannot include the blade in your hand: a successor who arrives with
        // nothing cannot earn the stones needed to re-equip, and the loss becomes terminal.
        var player = Dipadhara();
        var weapon = player.Equipment.WeaponId;

        for (var death = 0; death < 4; death++)
            Succession.Promote(player, Died(2, 3), mineSeed: death, roomIndex: 2);

        Assert.Multiple(() =>
        {
            Assert.That(player.Equipment.WeaponId, Is.EqualTo(weapon));
            Assert.That(player.Combat.ActiveWeapon.Damage, Is.GreaterThan(0f));
        });
    }

    // ---------------------------------------------------------------- the body

    [Test]
    public void TheFallenLeaveTheirStonesWhereTheyFell()
    {
        var player = Dipadhara();
        Succession.Promote(player, Died(6, 21), mineSeed: 4211, roomIndex: 6);

        var cache = player.Legacy.Fallen;

        Assert.That(cache, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(cache!.Stones, Is.EqualTo(21));
            Assert.That(cache.MineSeed, Is.EqualTo(4211));
            Assert.That(cache.RoomIndex, Is.EqualTo(6));
            Assert.That(player.Legacy.CanRecoverIn(4211), Is.True);
            Assert.That(player.Legacy.CanRecoverIn(4212), Is.False,
                "another mine is another place entirely");
        });
    }

    [Test]
    public void DyingWithNothingLeavesNothingToFetch()
    {
        var player = Dipadhara();
        Succession.Promote(player, Died(0, 0), mineSeed: 7, roomIndex: 1);

        Assert.Multiple(() =>
        {
            Assert.That(player.Legacy.Fallen, Is.Null);
            Assert.That(player.Legacy.Generation, Is.EqualTo(1), "somebody still died");
        });
    }

    [Test]
    public void ASecondDeathReplacesTheFirstBodyRatherThanQueueingIt()
    {
        // Keeping a queue would turn a losing streak into a stockpile to be collected in one
        // trip, which is the opposite of a cost.
        var player = Dipadhara();
        Succession.Promote(player, Died(6, 21), mineSeed: 100, roomIndex: 6);
        Succession.Promote(player, Died(2, 3), mineSeed: 200, roomIndex: 2);

        Assert.Multiple(() =>
        {
            Assert.That(player.Legacy.Fallen!.MineSeed, Is.EqualTo(200));
            Assert.That(player.Legacy.Fallen.Stones, Is.EqualTo(3));
            Assert.That(player.Legacy.CanRecoverIn(100), Is.False);
        });
    }

    [Test]
    public void ARecoveredCacheIsGoneForGood()
    {
        var player = Dipadhara();
        Succession.Promote(player, Died(6, 21), mineSeed: 4211, roomIndex: 6);

        player.Legacy.Recover();

        Assert.Multiple(() =>
        {
            Assert.That(player.Legacy.Fallen, Is.Null);
            Assert.That(player.Legacy.CanRecoverIn(4211), Is.False);
        });
    }

    [Test]
    public void EachSuccessorHasATheirOwnName()
    {
        var player = Dipadhara();
        var first = player.Legacy.CurrentName;

        Succession.Promote(player, Died(3, 6), mineSeed: 1, roomIndex: 3);

        Assert.Multiple(() =>
        {
            Assert.That(player.Legacy.CurrentName, Is.Not.EqualTo(first));
            Assert.That(player.Legacy.Fallen!.Name, Is.EqualTo(first),
                "the body is the person who was carrying it");
        });
    }

    [Test]
    public void NamesRunOutGracefullyRatherThanCrashing()
    {
        Assert.That(Succession.NameFor(9999), Is.Not.Empty);
    }

    // ---------------------------------------------------------------- persistence

    [Test]
    public void TheBodyAndTheBloodlineSurviveSaveAndReload()
    {
        var player = Dipadhara();
        player.Vitals.AddXp(player.Vitals.XpToLevel);
        Succession.Promote(player, Died(6, 21), mineSeed: 4211, roomIndex: 6);

        var reloaded = PlayerCharacter.NewGame();
        SaveGame.Restore(reloaded, SaveGame.Capture(player, default));

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Legacy.Generation, Is.EqualTo(1));
            Assert.That(reloaded.Legacy.Fallen!.Stones, Is.EqualTo(21));
            Assert.That(reloaded.Legacy.Fallen.MineSeed, Is.EqualTo(4211));
            Assert.That(reloaded.Legacy.Fallen.RoomIndex, Is.EqualTo(6));
            Assert.That(reloaded.Legacy.CurrentName, Is.EqualTo(player.Legacy.CurrentName));
        });
    }

    [Test]
    public void ASaveFromBeforeSuccessionExistedLoadsWithNobodyDead()
    {
        // Older saves carry no legacy block at all; they must not arrive haunted.
        var player = PlayerCharacter.NewGame();
        var data = SaveGame.Capture(player, default);
        data.Legacy = null;

        var reloaded = PlayerCharacter.NewGame();
        SaveGame.Restore(reloaded, data);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Legacy.Generation, Is.Zero);
            Assert.That(reloaded.Legacy.Fallen, Is.Null);
        });
    }
}
