using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Equipment and the three weapon classes.
///
/// Before this the inventory was cosmetic — the Iron Sword in the player's pack did nothing,
/// and melee damage was a hardcoded field on PlayerCombat. Loot, merchants and route rewards
/// were all hollow. These tests exist so that stays fixed.
/// </summary>
public class EquipmentSmokeTests : SmokeTestFixture
{
    private PlayerEquipment SpawnEquippedPlayer()
    {
        var player = SpawnPlayer();
        return player.AddComponent<PlayerEquipment>();
    }

    [Test]
    public void UnknownOrMissingWeapon_ResolvesToUnarmed_RatherThanNull()
    {
        var equipment = SpawnEquippedPlayer();

        Assert.IsNotNull(equipment.Weapon, "An unset weapon must resolve to unarmed, not null.");
        Assert.AreEqual(EquipmentCatalog.UnarmedId, EquipmentCatalog.GetWeapon("no_such_item").Id,
            "An unknown id must degrade to unarmed so a bad save cannot throw.");
    }

    [Test]
    public void Equipping_RequiresThePlayerToActuallyHoldTheItem()
    {
        var equipment = SpawnEquippedPlayer();

        Assert.IsFalse(
            equipment.Equip("steel_greatsword"),
            "Equipped a weapon the player does not carry — a UI bug could conjure gear.");

        PlayerInventory.Instance.Add("steel_greatsword", "Steel Greatsword", 1, "weapon");
        Assert.IsTrue(equipment.Equip("steel_greatsword"), "Equipping a held weapon failed.");
        Assert.AreEqual("steel_greatsword", equipment.WeaponId);
    }

    [Test]
    public void WeaponClasses_DifferInDamageReachAndStamina()
    {
        var oneHanded = EquipmentCatalog.GetWeapon("iron_sword");
        var twoHanded = EquipmentCatalog.GetWeapon("iron_greatsword");
        var ranged = EquipmentCatalog.GetWeapon("hunting_bow");

        Assert.Greater(twoHanded.Damage, oneHanded.Damage, "Two-handed should hit harder.");
        Assert.Greater(twoHanded.Cooldown, oneHanded.Cooldown, "Two-handed should be slower.");
        Assert.Greater(twoHanded.StaminaCost, oneHanded.StaminaCost, "Two-handed should cost more.");
        Assert.Greater(ranged.Range, twoHanded.Range, "Ranged should out-reach melee by a lot.");
        Assert.Less(ranged.Damage, oneHanded.Damage, "Ranged trades per-hit damage for reach.");
    }

    /// <summary>
    /// Only one-handed weapons block. That restriction is the entire trade for two-handed
    /// damage, so it is worth a test of its own.
    /// </summary>
    [Test]
    public void OnlyOneHandedWeaponsCanBlock()
    {
        Assert.IsTrue(EquipmentCatalog.GetWeapon("iron_sword").CanBlock);
        Assert.IsFalse(EquipmentCatalog.GetWeapon("iron_greatsword").CanBlock);
        Assert.IsFalse(EquipmentCatalog.GetWeapon("hunting_bow").CanBlock);
    }

    [Test]
    public void Blocking_IsRefusedWithATwoHandedWeapon()
    {
        var player = SpawnPlayer();
        var equipment = player.AddComponent<PlayerEquipment>();
        var combat = player.AddComponent<PlayerCombat>();
        PlayerInventory.Instance.Add("iron_greatsword", "Iron Greatsword", 1, "weapon");
        equipment.Equip("iron_greatsword");

        combat.SetBlocking(true);

        Assert.IsFalse(combat.IsBlocking, "A two-handed weapon should not be able to block.");
    }

    [Test]
    public void Armour_ReducesIncomingDamage_ButNeverToZero()
    {
        var player = SpawnPlayer();
        var equipment = player.AddComponent<PlayerEquipment>();
        var stats = PlayerStats.Instance;
        PlayerInventory.Instance.Add("mail_hauberk", "Mail Hauberk", 1, "armour");
        equipment.Equip("mail_hauberk");

        stats.Health = 100f;
        stats.Damage(10f);
        float withArmour = 100f - stats.Health;

        Assert.Less(withArmour, 10f, "Armour did not reduce incoming damage.");

        // A trivial hit against heavy armour must still land for something.
        stats.Health = 100f;
        stats.Damage(1f);
        Assert.Less(stats.Health, 100f, "Armour made the player immune to weak hits.");
    }

    [Test]
    public void Blocking_HalvesIncomingDamage()
    {
        var player = SpawnPlayer();
        var equipment = player.AddComponent<PlayerEquipment>();
        var combat = player.AddComponent<PlayerCombat>();
        var stats = PlayerStats.Instance;
        equipment.Equip("iron_sword");

        combat.SetBlocking(false);
        stats.Health = 100f;
        stats.Damage(20f);
        float unguarded = 100f - stats.Health;

        combat.SetBlocking(true);
        stats.Health = 100f;
        stats.Damage(20f);
        float guarded = 100f - stats.Health;

        Assert.Less(guarded, unguarded, "Blocking did not reduce the hit.");
    }

    [Test]
    public void AutoEquip_PicksTheHighestTierHeld()
    {
        var equipment = SpawnEquippedPlayer();
        var inventory = PlayerInventory.Instance;
        inventory.Add("iron_sword", "Iron Sword", 1, "weapon");
        inventory.Add("steel_sword", "Steel Sword", 1, "weapon");

        equipment.AutoEquipBest();

        Assert.AreEqual("steel_sword", equipment.WeaponId, "Auto-equip did not pick the better tier.");
    }

    [Test]
    public void EquippedSet_SurvivesASaveRoundTrip()
    {
        var player = SpawnPlayer();
        var equipment = player.AddComponent<PlayerEquipment>();
        var save = SpawnSaveService();
        var inventory = PlayerInventory.Instance;

        inventory.Add("steel_greatsword", "Steel Greatsword", 1, "weapon");
        inventory.Add("mail_hauberk", "Mail Hauberk", 1, "armour");
        equipment.Equip("steel_greatsword");
        equipment.Equip("mail_hauberk");

        save.Save();
        equipment.UnequipWeapon();
        equipment.UnequipArmour();
        save.Load();

        Assert.AreEqual("steel_greatsword", equipment.WeaponId, "Equipped weapon was lost on load.");
        Assert.AreEqual("mail_hauberk", equipment.ArmourId, "Worn armour was lost on load.");
    }

    /// <summary>A save naming gear that no longer exists must degrade, not throw.</summary>
    [Test]
    public void RestoringUnknownGear_FallsBackToUnarmed()
    {
        var equipment = SpawnEquippedPlayer();

        equipment.Restore("weapon_from_a_future_patch", "armour_from_a_future_patch");

        Assert.AreEqual(EquipmentCatalog.UnarmedId, equipment.WeaponId);
        Assert.AreEqual(string.Empty, equipment.ArmourId);
        Assert.AreEqual(0f, equipment.ArmourValue, 0.001f);
    }
}
