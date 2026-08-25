using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The two things that make a swing a decision: when it lands, and what it lands on.
///
/// Both come out of the same observation — six recorded sessions in which melee looked
/// identical whatever the player did, whatever they were carrying, and wherever they stood.
/// </summary>
public class CombatFocusTests
{
    private static EnemyArchetype Bandit => new()
    {
        Id = "bandit", DisplayName = "Bandit", MaxHealth = 200f, AttackDamage = 4f
    };

    private static Enemy Standing(float x = 0f, float z = -2f) =>
        new(Bandit, $"b.{x}.{z}") { Position = new WorldPoint(x, 0f, z) };

    private static PlayerCharacter Armed(string weaponId)
    {
        var player = PlayerCharacter.NewGame();
        player.Inventory.Add(weaponId, weaponId, 1, "weapon");
        player.Equipment.Equip(weaponId);
        return player;
    }

    // ---------------------------------------------------------------- the opening strike

    [Test]
    public void SomethingStillRisingTakesTwiceTheDamage()
    {
        // The reward for being in a room rather than at its door, and the one idea worth
        // keeping out of the stealth pillar that was parked.
        var player = Armed("iron_sword");

        var upright = Standing();
        var rising = Standing();
        rising.Rouse(Enemy.RousingSeconds);

        var normal = player.Combat.TryAttack(upright);
        player.Combat.Tick(5f);
        var opening = player.Combat.TryAttack(rising);

        Assert.Multiple(() =>
        {
            Assert.That(opening.Damage,
                Is.EqualTo(normal.Damage * PlayerCombat.OpeningStrikeMultiplier).Within(0.01f));
            Assert.That(opening.WasOpening, Is.True);
            Assert.That(normal.WasOpening, Is.False);
        });
    }

    [Test]
    public void AStaggeredEnemyIsOpenTheSameWay()
    {
        // Shock already staggers what it hits. Making that a damage window rather than only a
        // pause is what turns Arc into a set-up rather than a third damage number.
        var enemy = Standing();
        enemy.ApplyStagger(1.5f);

        Assert.That(enemy.IsVulnerable, Is.True);
    }

    [Test]
    public void TheWindowCloses()
    {
        var player = Armed("iron_sword");
        var rising = Standing();
        rising.Rouse(0.4f);

        rising.Tick(0.6f);

        Assert.That(player.Combat.TryAttack(rising).WasOpening, Is.False,
            "it is on its feet now");
    }

    // ---------------------------------------------------------------- the sweep

    [Test]
    public void OneHandedAndTwoHandedDealTheSameDamageASecond()
    {
        // The reason a greatsword needed a reason. Eighteen every 0.45 seconds and thirty-four
        // every 0.85 are the same number, so with one target in front of you the choice
        // between them has never mattered.
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var great = EquipmentCatalog.GetWeapon("iron_greatsword");

        Assert.That(great.Damage / great.Cooldown,
            Is.EqualTo(sword.Damage / sword.Cooldown).Within(1f));
    }

    [Test]
    public void OnlyATwoHandedWeaponSweeps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Armed("iron_greatsword").Combat.WeaponSweeps, Is.True);
            Assert.That(Armed("iron_sword").Combat.WeaponSweeps, Is.False);
            Assert.That(Armed("hunting_bow").Combat.WeaponSweeps, Is.False);
        });
    }

    [Test]
    public void ASweepCarriesThroughEverythingElseInTheArc()
    {
        var player = Armed("iron_greatsword");
        var others = new[] { Standing(1f, -2f), Standing(-1f, -2.5f) };

        var struck = player.Combat.Sweep(others);

        Assert.Multiple(() =>
        {
            Assert.That(struck, Is.EqualTo(2));
            foreach (var enemy in others)
                Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth), enemy.SpawnId);
        });
    }

    [Test]
    public void WhatTheSweepCatchesTakesLessThanWhatWasAimedAt()
    {
        var player = Armed("iron_greatsword");
        var caught = Standing(1f, -2f);

        player.Combat.Sweep(new[] { caught });
        var secondary = caught.MaxHealth - caught.Health;

        Assert.That(secondary, Is.LessThan(player.Combat.WeaponDamage));
        Assert.That(secondary,
            Is.EqualTo(player.Combat.WeaponDamage * PlayerCombat.CleaveFactor).Within(0.01f));
    }

    [Test]
    public void ASweepAcrossARisingRoomIsWorthFarMoreThanAStab()
    {
        // The situation this exists for: five bodies coming up at once. A sword answers one of
        // them; a greatsword answers the room, and pays for it in stamina and speed.
        var swordsman = Armed("iron_sword");
        var reaper = Armed("iron_greatsword");

        var stabbed = Standing();
        var swept = new[] { Standing(), Standing(1f, -2f), Standing(-1f, -2f), Standing(2f, -2.5f) };
        foreach (var enemy in swept) enemy.Rouse(Enemy.RousingSeconds);
        stabbed.Rouse(Enemy.RousingSeconds);

        var stab = swordsman.Combat.TryAttack(stabbed).Damage;

        var first = reaper.Combat.TryAttack(swept[0]);
        reaper.Combat.Sweep(swept.Skip(1).ToArray());
        var sweepTotal = first.Damage + swept.Skip(1).Sum(e => e.MaxHealth - e.Health);

        Assert.That(sweepTotal, Is.GreaterThan(stab * 2f));
    }

    [Test]
    public void NothingAlreadyDeadIsSweptTwice()
    {
        var player = Armed("iron_greatsword");
        var dead = Standing();
        dead.TakeDamage(dead.MaxHealth * 2f);

        Assert.That(player.Combat.Sweep(new[] { dead }), Is.Zero);
    }

    [Test]
    public void ASwordSweepsNothingHoweverManyAreInFrontOfIt()
    {
        var player = Armed("iron_sword");
        var crowd = new[] { Standing(1f, -2f), Standing(-1f, -2f) };

        Assert.Multiple(() =>
        {
            Assert.That(player.Combat.Sweep(crowd), Is.Zero);
            foreach (var enemy in crowd)
                Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
        });
    }

    // ---------------------------------------------------------------- the arc itself

    [Test]
    public void TheArcFindsEverythingInFrontAndNothingBehind()
    {
        var here = new WorldPoint(0f, 0f, 0f);
        var ahead = Standing(0f, -2f);
        // Inside the cone, which is 0.6 radians either side of forward. At (1.4, -1.6) it
        // sits at 41 degrees and is correctly excluded — the first version of this test put
        // it there and blamed the code.
        var beside = Standing(0.6f, -2f);
        var behind = Standing(0f, 2f);
        var faraway = Standing(0f, -40f);

        var arc = Targeting.FindAll(here, 0f, 3f, new[] { ahead, beside, behind, faraway });

        Assert.Multiple(() =>
        {
            Assert.That(arc, Does.Contain(ahead));
            Assert.That(arc, Does.Contain(beside));
            Assert.That(arc, Does.Not.Contain(behind), "a sweep is not a spin");
            Assert.That(arc, Does.Not.Contain(faraway));
        });
    }

    [Test]
    public void TheArcIsOrderedNearestFirst()
    {
        var here = new WorldPoint(0f, 0f, 0f);
        var near = Standing(0f, -1f);
        var far = Standing(0f, -2.8f);

        Assert.That(Targeting.FindAll(here, 0f, 3f, new[] { far, near })[0], Is.EqualTo(near));
    }

    [Test]
    public void TheArcAgreesWithWhatTheCrosshairPicked()
    {
        // Two hit tests that disagree about what is in front of you would let a sweep miss the
        // thing it was aimed at, or catch something the crosshair said was not there.
        var here = new WorldPoint(0f, 0f, 0f);
        var crowd = new[] { Standing(0f, -2.6f), Standing(0.5f, -1.2f), Standing(-2f, -0.4f) };

        var focused = Targeting.Find(here, 0f, 3f, crowd);
        var arc = Targeting.FindAll(here, 0f, 3f, crowd);

        Assert.That(arc, Does.Contain(focused!));
        Assert.That(arc[0], Is.EqualTo(focused), "the crosshair takes the nearest, and so does the arc");
    }
}
