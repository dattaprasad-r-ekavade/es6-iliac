using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What makes each weapon class a different decision rather than a different number.
///
/// The count of weapons was never the problem — a longer list of things that all swing is one
/// weapon with several names. These assert the *verbs*: that blunt buys an opening, that both
/// hands on a weapon costs you the spell, and that a shield improves a guard without becoming
/// a precondition for having one.
/// </summary>
[TestFixture]
public sealed class WeaponIdentityTests
{
    private static PlayerCharacter NewPlayer() => PlayerCharacter.NewGame();

    private static Enemy Spawn() =>
        new(new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = 55f },
            "bandit.01");

    /// <summary>Put an item in the pack and hold it.</summary>
    private static void Wield(PlayerCharacter player, string id, string kind)
    {
        player.Inventory.Add(id, EquipmentCatalog.GetWeapon(id).DisplayName, 1, kind);
        player.Equipment.Equip(id);
    }

    private static WeaponDefinition Weapon(string id) => EquipmentCatalog.GetWeapon(id);

    // ------------------------------------------------------------------ blunt

    [Test]
    public void OnlyBluntStaggers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Weapon("iron_mace").StaggerSeconds, Is.GreaterThan(0f));
            Assert.That(Weapon("iron_sword").StaggerSeconds, Is.Zero);
            Assert.That(Weapon("iron_greatsword").StaggerSeconds, Is.Zero);
            Assert.That(Weapon("hunting_bow").StaggerSeconds, Is.Zero);
        });
    }

    [Test]
    public void ABluntBlowLeavesTheTargetOpen()
    {
        var player = NewPlayer();
        Wield(player, "iron_mace", "weapon");

        var enemy = Spawn();
        Assert.That(enemy.IsVulnerable, Is.False, "a bandit should not start staggered");

        player.Combat.TryAttack(enemy);

        // The stagger is the point of the weapon, and the domain already pays double on a
        // vulnerable target — so the mace's real damage is the second blow, not the first.
        Assert.That(enemy.IsStaggered, Is.True);
        Assert.That(enemy.IsVulnerable, Is.True);
    }

    [Test]
    public void ABladeLeavesNoOpening()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword", "weapon");

        var enemy = Spawn();
        player.Combat.TryAttack(enemy);

        Assert.That(enemy.IsStaggered, Is.False);
    }

    [Test]
    public void BluntDoesNotBleed()
    {
        // Burn is Flame's identity. A second damage-over-time on a mace would make the weapon
        // a worse version of a spell the player may already be carrying, which is the opposite
        // of giving it its own reason to exist.
        var player = NewPlayer();
        Wield(player, "iron_mace", "weapon");

        var enemy = Spawn();
        player.Combat.TryAttack(enemy);

        Assert.That(enemy.IsBurning, Is.False);
    }

    // ------------------------------------------------------------------ both hands

    [Test]
    public void BothHandsOnAWeaponCostsTheSpell()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Weapon("iron_greatsword").CastDelaySeconds, Is.GreaterThan(0f));
            Assert.That(Weapon("hunting_bow").CastDelaySeconds, Is.GreaterThan(0f));
            Assert.That(Weapon("iron_sword").CastDelaySeconds, Is.Zero);
        });
    }

    [Test]
    public void ABladeCostsLessThanAGreatswordAndABluntSitsBetween()
    {
        Assert.That(Weapon("iron_mace").CastDelaySeconds,
            Is.GreaterThan(Weapon("iron_sword").CastDelaySeconds));
        Assert.That(Weapon("iron_mace").CastDelaySeconds,
            Is.LessThan(Weapon("iron_greatsword").CastDelaySeconds));
    }

    [Test]
    public void AShoulderedWeaponRefusesTheCastWithoutChargingForIt()
    {
        var player = NewPlayer();
        var before = player.Vitals.Prana;

        player.Spells.Encumber(Weapon("iron_greatsword").CastDelaySeconds);
        var outcome = player.Spells.Cast(SpellCatalog.FireId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CastResult.Shouldering));
            Assert.That(outcome.WasCast, Is.False);

            // Refused before paying. A tax that also takes the prana is two punishments.
            Assert.That(player.Vitals.Prana, Is.EqualTo(before));
        });
    }

    [Test]
    public void TheWeaponComesDownAgain()
    {
        var player = NewPlayer();
        player.Spells.Encumber(0.9f);

        Assert.That(player.Spells.IsShouldering, Is.True);

        player.Spells.Tick(1.0f);

        Assert.That(player.Spells.IsShouldering, Is.False);
        Assert.That(player.Spells.Cast(SpellCatalog.FireId).Result,
            Is.Not.EqualTo(CastResult.Shouldering));
    }

    [Test]
    public void EncumberNeverShortensADelayAlreadyRunning()
    {
        var player = NewPlayer();

        player.Spells.Encumber(0.9f);
        player.Spells.Encumber(0.1f);

        Assert.That(player.Spells.ShoulderRemaining, Is.EqualTo(0.9f).Within(0.0001f));
    }

    // ------------------------------------------------------------------ the off hand

    [Test]
    public void AShieldImprovesTheGuardRatherThanGrantingIt()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword", "weapon");

        // A blade alone blocks exactly as well as it always did. Making the shield a
        // precondition would have quietly nerfed every save in existence.
        var bare = player.Equipment.BlockFactor;
        Assert.That(bare, Is.EqualTo(DamageMath.BlockReduction).Within(0.0001f));
        Assert.That(player.Equipment.CanBlock, Is.True);

        player.Inventory.Add("bronze_shield",
            EquipmentCatalog.GetShield("bronze_shield")!.DisplayName, 1, "shield");
        player.Equipment.Equip("bronze_shield");

        Assert.That(player.Equipment.BlockFactor, Is.LessThan(bare));
    }

    [Test]
    public void ABetterShieldBlocksBetter()
    {
        Assert.That(EquipmentCatalog.GetShield("bronze_shield")!.BlockFactor,
            Is.LessThan(EquipmentCatalog.GetShield("wicker_shield")!.BlockFactor));
    }

    [Test]
    public void BothHandsOnAWeaponMeansNoShield()
    {
        var player = NewPlayer();
        player.Inventory.Add("bronze_shield",
            EquipmentCatalog.GetShield("bronze_shield")!.DisplayName, 1, "shield");
        player.Equipment.Equip("bronze_shield");

        Wield(player, "iron_greatsword", "weapon");

        Assert.Multiple(() =>
        {
            Assert.That(player.Equipment.Shield, Is.Null);
            Assert.That(player.Equipment.BlockFactor,
                Is.EqualTo(DamageMath.BlockReduction).Within(0.0001f));

            // And the shield's armour goes with it, or a greatsword would carry a shield's
            // protection without the shield.
            Assert.That(player.Equipment.ArmourValue, Is.Zero);
        });
    }

    [Test]
    public void TheShieldIsNotLostWhenTheGreatswordIsPutDown()
    {
        var player = NewPlayer();
        player.Inventory.Add("bronze_shield",
            EquipmentCatalog.GetShield("bronze_shield")!.DisplayName, 1, "shield");
        player.Equipment.Equip("bronze_shield");
        Wield(player, "iron_greatsword", "weapon");
        Wield(player, "iron_sword", "weapon");

        Assert.That(player.Equipment.Shield, Is.Not.Null);
    }

    [Test]
    public void AShieldMakesABlockedBlowHurtLess()
    {
        static float Blocked(string? shieldId)
        {
            var player = NewPlayer();
            Wield(player, "iron_sword", "weapon");

            if (shieldId is not null)
            {
                player.Inventory.Add(shieldId,
                    EquipmentCatalog.GetShield(shieldId)!.DisplayName, 1, "shield");
                player.Equipment.Equip(shieldId);
            }

            player.Combat.SetBlocking(true);
            return player.Combat.TakeHit(40f);
        }

        Assert.That(Blocked("bronze_shield"), Is.LessThan(Blocked(null)));
    }

    // ------------------------------------------------------------------ ammunition

    [Test]
    public void OnlyBowsSpendArrows()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Weapon("hunting_bow").NeedsAmmunition, Is.True);
            Assert.That(Weapon("iron_sword").NeedsAmmunition, Is.False);
            Assert.That(Weapon("iron_mace").NeedsAmmunition, Is.False);
            Assert.That(Weapon("iron_greatsword").NeedsAmmunition, Is.False);
        });
    }

    [Test]
    public void LoosingAnArrowSpendsOne()
    {
        var player = NewPlayer();
        Wield(player, "hunting_bow", "weapon");

        var before = player.Inventory.CountOf(EquipmentCatalog.ArrowId);
        Assert.That(before, Is.GreaterThan(0), "a new character should start with some arrows");

        player.Combat.TryAttack(Spawn());

        Assert.That(player.Inventory.CountOf(EquipmentCatalog.ArrowId), Is.EqualTo(before - 1));
    }

    [Test]
    public void AMissedArrowIsStillGone()
    {
        // Spent on the swing rather than on the hit. It is the whole reason a bow asks the
        // player to aim, and refunding a miss would make the bow free again.
        var player = NewPlayer();
        Wield(player, "hunting_bow", "weapon");

        var before = player.Inventory.CountOf(EquipmentCatalog.ArrowId);
        player.Combat.TryAttack(null);

        Assert.That(player.Inventory.CountOf(EquipmentCatalog.ArrowId), Is.EqualTo(before - 1));
    }

    [Test]
    public void ASwordSpendsNoArrows()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword", "weapon");

        var before = player.Inventory.CountOf(EquipmentCatalog.ArrowId);
        player.Combat.TryAttack(Spawn());

        Assert.That(player.Inventory.CountOf(EquipmentCatalog.ArrowId), Is.EqualTo(before));
    }

    [Test]
    public void AnEmptyQuiverCostsNothingToDiscover()
    {
        var player = NewPlayer();
        Wield(player, "hunting_bow", "weapon");
        player.Inventory.Consume(EquipmentCatalog.ArrowId,
            player.Inventory.CountOf(EquipmentCatalog.ArrowId));

        var stamina = player.Vitals.Stamina;
        var outcome = player.Combat.TryAttack(Spawn());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(AttackResult.NoAmmunition));

            // Not the stamina and not the cooldown. Checked before either is spent, or the
            // player pays their breath for a shot that was never loosed.
            Assert.That(player.Vitals.Stamina, Is.EqualTo(stamina));
            Assert.That(player.Combat.IsReady, Is.True);
        });
    }

    // ------------------------------------------------------------------ the roster

    [Test]
    public void EveryClassIsRepresentedAtBothTiers()
    {
        foreach (var cls in new[]
                 {
                     WeaponClass.OneHanded, WeaponClass.TwoHanded,
                     WeaponClass.Ranged, WeaponClass.Blunt
                 })
        foreach (var tier in new[] { 1, 2 })
            Assert.That(
                EquipmentCatalog.AllWeapons.Any(w => w.Class == cls && w.Tier == tier),
                Is.True, $"no tier {tier} weapon of class {cls}");
    }

    [Test]
    public void OnlyTheHandsThatAreFreeCanBlock()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Weapon("iron_sword").CanBlock, Is.True);
            Assert.That(Weapon("iron_mace").CanBlock, Is.True);
            Assert.That(Weapon("iron_greatsword").CanBlock, Is.False);
            Assert.That(Weapon("hunting_bow").CanBlock, Is.False);
        });
    }
}
