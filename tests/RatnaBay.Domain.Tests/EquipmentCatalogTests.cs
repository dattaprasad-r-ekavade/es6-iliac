using System;
using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class EquipmentCatalogTests
{
    [TestCase("")]
    [TestCase(null)]
    [TestCase("sword_from_a_future_patch")]
    public void AnUnknownWeaponIdDegradesToUnarmedRatherThanThrowing(string? id)
    {
        // The contract that lets us rebalance weapons without invalidating old saves.
        Assert.That(EquipmentCatalog.GetWeapon(id), Is.SameAs(EquipmentCatalog.Unarmed));
    }

    [Test]
    public void AnUnknownArmourIdResolvesToNothingWorn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EquipmentCatalog.GetArmour("plate_from_a_future_patch"), Is.Null);
            Assert.That(EquipmentCatalog.GetArmour(null), Is.Null);
        });
    }

    [Test]
    public void UnarmedIsNotSoldAsAWeapon()
    {
        Assert.That(EquipmentCatalog.IsWeapon(EquipmentCatalog.UnarmedId), Is.False);
    }

    [Test]
    public void EveryIdMatchesTheKeyItIsStoredUnder()
    {
        Assert.Multiple(() =>
        {
            foreach (var weapon in EquipmentCatalog.AllWeapons)
                Assert.That(EquipmentCatalog.GetWeapon(weapon.Id), Is.SameAs(weapon));
            foreach (var armour in EquipmentCatalog.AllArmours)
                Assert.That(EquipmentCatalog.GetArmour(armour.Id), Is.SameAs(armour));
        });
    }

    [Test]
    public void WeaponAndArmourIdsDoNotCollide()
    {
        foreach (var weapon in EquipmentCatalog.AllWeapons)
            Assert.That(EquipmentCatalog.IsArmour(weapon.Id), Is.False);
    }

    [Test]
    public void OnlyAFreeHandCanBlock()
    {
        // The rule is about hands, not about class. Blunt joined one-handed when maces were
        // added: a mace leaves a hand free exactly as a blade does, and a greatsword or a bow
        // does not.
        foreach (var weapon in EquipmentCatalog.AllWeapons)
            Assert.That(weapon.CanBlock, Is.EqualTo(!weapon.IsTwoHanded),
                $"{weapon.Id} blocks against its class contract");
    }

    [Test]
    public void BareHandsCanBlock()
    {
        Assert.That(EquipmentCatalog.Unarmed.CanBlock, Is.True);
    }

    [Test]
    public void EachWeaponTrainsTheDisciplineItBelongsTo()
    {
        // Four classes, three disciplines, and that is deliberate. Blunt and two-handed both
        // train Heavy because a mace and a greatsword are the same discipline — weight, swung
        // — against Blade's finesse and Marksman's range. Giving blunt a ninth skill of its
        // own would have split that training in two and quietly devalued both halves, and the
        // design settled on eight skills for reasons that have not changed.
        foreach (var weapon in EquipmentCatalog.AllWeapons)
        {
            var expected = weapon.Class switch
            {
                WeaponClass.OneHanded => Skills.Blade,
                WeaponClass.TwoHanded or WeaponClass.Blunt => Skills.Heavy,
                _ => Skills.Marksman
            };
            Assert.That(weapon.SkillId, Is.EqualTo(expected), weapon.Id);
        }
    }

    [Test]
    public void EveryWeaponTrainsADeclaredSkill()
    {
        foreach (var weapon in EquipmentCatalog.AllWeapons)
            Assert.That(Skills.Exists(weapon.SkillId), Is.True);
    }

    [Test]
    public void EveryWeaponClassIsRepresented()
    {
        // Asserted against the enum rather than a literal, so adding a class without adding a
        // weapon for it fails here instead of shipping an option nobody can pick.
        Assert.That(EquipmentCatalog.AllWeapons.Select(w => w.Class).Distinct(),
            Is.EquivalentTo(Enum.GetValues<WeaponClass>()));
    }

    [Test]
    public void TierTwoBeatsTierOneWithinEveryWeaponClass()
    {
        foreach (var group in EquipmentCatalog.AllWeapons.GroupBy(w => w.Class))
        {
            var byTier = group.OrderBy(w => w.Tier).ToList();
            for (var i = 1; i < byTier.Count; i++)
                Assert.That(byTier[i].Damage, Is.GreaterThan(byTier[i - 1].Damage),
                    $"{byTier[i].Id} is a higher tier than {byTier[i - 1].Id} but hits no harder");
        }
    }

    [Test]
    public void TwoHandedTradesSpeedAndStaminaForDamage()
    {
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var greatsword = EquipmentCatalog.GetWeapon("iron_greatsword");

        Assert.Multiple(() =>
        {
            Assert.That(greatsword.Damage, Is.GreaterThan(sword.Damage));
            Assert.That(greatsword.Cooldown, Is.GreaterThan(sword.Cooldown));
            Assert.That(greatsword.StaminaCost, Is.GreaterThan(sword.StaminaCost));
            Assert.That(greatsword.CanBlock, Is.False);
        });
    }

    [Test]
    public void BowsBuyReachWithPerHitDamage()
    {
        var bow = EquipmentCatalog.GetWeapon("hunting_bow");
        var sword = EquipmentCatalog.GetWeapon("iron_sword");

        Assert.Multiple(() =>
        {
            Assert.That(bow.Range, Is.GreaterThan(sword.Range * 5f));
            Assert.That(bow.Damage, Is.LessThan(sword.Damage));
        });
    }

    [Test]
    public void EveryWeaponBeatsBareHands()
    {
        foreach (var weapon in EquipmentCatalog.AllWeapons)
            Assert.That(weapon.Damage, Is.GreaterThan(EquipmentCatalog.Unarmed.Damage),
                $"{weapon.Id} is no better than punching");
    }

    [Test]
    public void EveryWeaponHasUsableCombatStats()
    {
        foreach (var weapon in EquipmentCatalog.AllWeapons)
            Assert.Multiple(() =>
            {
                Assert.That(weapon.Range, Is.GreaterThan(0f), $"{weapon.Id} has no reach");
                Assert.That(weapon.Cooldown, Is.GreaterThan(0f), $"{weapon.Id} fires infinitely fast");
                Assert.That(weapon.StaminaCost, Is.GreaterThan(0f), $"{weapon.Id} is free to swing");
            });
    }

    [Test]
    public void TierTwoArmourProtectsMoreThanTierOne()
    {
        var jerkin = EquipmentCatalog.GetArmour("padded_jerkin")!;
        var hauberk = EquipmentCatalog.GetArmour("mail_hauberk")!;

        Assert.Multiple(() =>
        {
            Assert.That(hauberk.Tier, Is.GreaterThan(jerkin.Tier));
            Assert.That(hauberk.Armour, Is.GreaterThan(jerkin.Armour));
        });
    }

    [Test]
    public void ArmourNeverCancelsAWeaponEntirely()
    {
        var best = EquipmentCatalog.AllArmours.Max(a => a.Armour);
        Assert.That(EquipmentCatalog.AllWeapons.Select(w => w.Damage), Is.All.GreaterThan(best));
    }
}
