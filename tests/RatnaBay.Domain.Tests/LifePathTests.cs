using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class LifePathTests
{
    private static LifePath Path(string routeId)
    {
        var path = new LifePath();
        path.Select(routeId);
        return path;
    }

    [Test]
    public void EachPathIsGiftedWithExactlyOneDiscipline()
    {
        var warrior = Path(StoryDirector.RouteWarrior);
        var mage = Path(StoryDirector.RouteMage);
        var trader = Path(StoryDirector.RouteTrade);

        Assert.Multiple(() =>
        {
            Assert.That(warrior.WeaponMultiplier, Is.EqualTo(LifePath.Gifted));
            Assert.That(warrior.SpellMultiplier, Is.EqualTo(LifePath.Secondary));

            Assert.That(mage.SpellMultiplier, Is.EqualTo(LifePath.Gifted));
            Assert.That(mage.WeaponMultiplier, Is.EqualTo(LifePath.Secondary));

            Assert.That(trader.WeaponMultiplier, Is.EqualTo(1f));
            Assert.That(trader.SpellMultiplier, Is.EqualTo(1f));
        });
    }

    [Test]
    public void OnlyTheTraderPaysADifferentPrice()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Path(StoryDirector.RouteWarrior).PriceOf(1000), Is.EqualTo(1000));
            Assert.That(Path(StoryDirector.RouteMage).PriceOf(1000), Is.EqualTo(1000));
            Assert.That(Path(StoryDirector.RouteTrade).PriceOf(1000), Is.LessThan(1000));
        });
    }

    [TestCase(12, 6)]
    [TestCase(80, 27)]
    [TestCase(1000, 178)]
    [TestCase(20000, 1682)]
    public void TheTraderDiscountCompoundsWithPrice(int listPrice, int expected)
    {
        Assert.That(Path(StoryDirector.RouteTrade).PriceOf(listPrice),
            Is.EqualTo(expected).Within(1));
    }

    [Test]
    public void TheDiscountIsNegligibleEarlyAndDecisiveLate()
    {
        var trader = Path(StoryDirector.RouteTrade);

        var cheapSaving = 1f - trader.PriceOf(12) / 12f;
        var dearSaving = 1f - trader.PriceOf(20000) / 20000f;

        Assert.Multiple(() =>
        {
            Assert.That(cheapSaving, Is.LessThanOrEqualTo(0.55f),
                "a trader should not dominate the first hour");
            Assert.That(dearSaving, Is.GreaterThan(0.85f), "and should dominate the last one");
        });
    }

    [Test]
    public void NothingIsEverFree()
    {
        var trader = Path(StoryDirector.RouteTrade);
        foreach (var price in new[] { 1, 2, 3, 5, 10 })
            Assert.That(trader.PriceOf(price), Is.GreaterThanOrEqualTo(1), $"price {price}");
    }

    [Test]
    public void RefusingTheRouteGrantsNoGift()
    {
        var refused = Path(StoryDirector.RouteRefuse);

        Assert.Multiple(() =>
        {
            Assert.That(refused.WeaponMultiplier, Is.EqualTo(1f));
            Assert.That(refused.SpellMultiplier, Is.EqualTo(1f));
            Assert.That(refused.PriceOf(1000), Is.EqualTo(1000));
        });
    }

    [Test]
    public void SelectingAPathGrantsItsSkillsAndItsGiftTogether()
    {
        var player = PlayerCharacter.NewGame();
        player.SelectLifePath(StoryDirector.RouteMage);

        Assert.Multiple(() =>
        {
            Assert.That(player.Skills.LevelOf(Skills.Destruction), Is.GreaterThan(0f));
            Assert.That(player.LifePath.SpellMultiplier, Is.EqualTo(LifePath.Gifted));
            Assert.That(player.Story.State.RouteId, Is.EqualTo(StoryDirector.RouteMage));
        });
    }

    [Test]
    public void ThePathSurvivesSaveAndReload()
    {
        var player = PlayerCharacter.NewGame();
        player.SelectLifePath(StoryDirector.RouteTrade);

        var reloaded = PlayerCharacter.NewGame();
        SaveGame.Restore(reloaded, SaveGame.Capture(player, default));

        Assert.That(reloaded.LifePath.PriceOf(1000),
            Is.EqualTo(player.LifePath.PriceOf(1000)));
    }
}

/// <summary>
/// The numbers that decide whether a class is worth choosing.
///
/// These compare whole resource bars rather than single hits, because that is the unit a
/// player actually spends: stamina refills itself, prana is bought with gold.
/// </summary>
public class ClassBalanceTests
{
    /// <summary>Damage from one full stamina bar, allowing for in-combat regeneration.</summary>
    private static float MeleeBarDamage(WeaponDefinition weapon, float multiplier)
    {
        const float stamina = 100f;
        var drainPerSecond = weapon.StaminaCost / weapon.Cooldown - PlayerVitals.CombatStaminaRegen;
        var seconds = stamina / drainPerSecond;
        var swings = seconds / weapon.Cooldown;
        return swings * weapon.Damage * multiplier;
    }

    /// <summary>Damage from one full prana reserve, including any burn.</summary>
    private static float SpellBarDamage(SpellDefinition spell, float multiplier)
    {
        var casts = 80f / spell.BaseCost;
        var power = spell.Power * multiplier;
        var burn = spell.Effect == SpellEffect.Fire ? power * 0.5f * spell.Duration : 0f;
        return casts * (power + burn);
    }

    [Test]
    public void AFullReserveOfPranaBeatsAFullBarOfStamina()
    {
        // It has to: stamina refills itself and prana is bought with gold.
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var flame = SpellCatalog.Get(SpellCatalog.FireId)!;

        Assert.That(SpellBarDamage(flame, 1f), Is.GreaterThan(MeleeBarDamage(sword, 1f)));
    }

    [Test]
    public void AMageDoesMoreWithSpellsThanWithASword()
    {
        // The failure this test exists to prevent: before the rebalance a mage's own sword
        // out-damaged their own magic, which made the class pointless.
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var flame = SpellCatalog.Get(SpellCatalog.FireId)!;

        var withSpells = SpellBarDamage(flame, LifePath.Gifted);
        var withSteel = MeleeBarDamage(sword, LifePath.Secondary);

        Assert.That(withSpells, Is.GreaterThan(withSteel * 1.3f));
    }

    [Test]
    public void AWarriorDoesMoreWithASwordThanWithSpells()
    {
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var flame = SpellCatalog.Get(SpellCatalog.FireId)!;

        Assert.That(MeleeBarDamage(sword, LifePath.Gifted),
            Is.GreaterThan(SpellBarDamage(flame, LifePath.Secondary)));
    }

    [Test]
    public void TheTraderIsWorstAtBothAndBestAtNeither()
    {
        var sword = EquipmentCatalog.GetWeapon("iron_sword");
        var flame = SpellCatalog.Get(SpellCatalog.FireId)!;

        Assert.Multiple(() =>
        {
            Assert.That(MeleeBarDamage(sword, 1f), Is.LessThan(MeleeBarDamage(sword, LifePath.Gifted)));
            Assert.That(SpellBarDamage(flame, 1f), Is.LessThan(SpellBarDamage(flame, LifePath.Gifted)));
        });
    }

    [Test]
    public void FireHasTheLowestBurstAndTheHighestTotal()
    {
        var flame = SpellCatalog.Get(SpellCatalog.FireId)!;
        var rime = SpellCatalog.Get(SpellCatalog.FrostId)!;
        var arc = SpellCatalog.Get(SpellCatalog.ShockId)!;

        Assert.Multiple(() =>
        {
            Assert.That(flame.Power, Is.LessThan(rime.Power), "burst: fire under frost");
            Assert.That(rime.Power, Is.LessThan(arc.Power), "burst: frost under shock");
            Assert.That(flame.Power * 1.5f * flame.Duration, Is.GreaterThan(arc.Power),
                "total: fire above shock once it has burned");
        });
    }

    [Test]
    public void MendIsWorthCastingRatherThanDrinking()
    {
        // A potion restores 40 and costs nothing to carry, so a heal bought with prana that
        // healed less would never be cast.
        Assert.That(SpellCatalog.Get(SpellCatalog.HealId)!.Power,
            Is.GreaterThan(ItemUse.PotionHeal));
    }

    [Test]
    public void EveryDestructionSpellIsWorthMoreThanASwordSwing()
    {
        var swing = EquipmentCatalog.GetWeapon("iron_sword").Damage;

        foreach (var spell in SpellCatalog.All.Where(s => s.School == SpellSchool.Destruction))
            Assert.That(spell.Power, Is.GreaterThan(swing),
                $"{spell.DisplayName} costs gold; a sword swing does not");
    }
}
