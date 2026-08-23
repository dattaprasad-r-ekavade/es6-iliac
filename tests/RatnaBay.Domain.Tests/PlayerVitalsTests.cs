using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class PlayerVitalsTests
{
    private Inventory _inventory = null!;
    private PlayerVitals _vitals = null!;

    [SetUp]
    public void Setup()
    {
        _inventory = new Inventory();
        _vitals = new PlayerVitals(_inventory);
    }

    [Test]
    public void ANewCharacterStartsAliveAndWhole()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Level, Is.EqualTo(1));
            Assert.That(_vitals.IsAlive, Is.True);
            Assert.That(_vitals.Health, Is.EqualTo(_vitals.MaxHealth));
        });
    }

    [Test]
    public void StaminaRecoversFasterOutOfCombat()
    {
        _vitals.SpendStamina(50f);
        var drained = _vitals.Stamina;

        _vitals.Tick(1f, inCombat: true);
        var inCombat = _vitals.Stamina - drained;

        _vitals.Restore(new SavedVitals { Stamina = drained });
        _vitals.Tick(1f, inCombat: false);
        var resting = _vitals.Stamina - drained;

        Assert.That(resting, Is.GreaterThan(inCombat));
    }

    [Test]
    public void StaminaStillRecoversDuringAFight()
    {
        // Combat regen used to be zero, which gave twelve swings and then six seconds of
        // standing there unable to attack.
        _vitals.SpendStamina(50f);
        var drained = _vitals.Stamina;

        _vitals.Tick(1f, inCombat: true);

        Assert.That(_vitals.Stamina, Is.GreaterThan(drained));
    }

    [Test]
    public void PranaNeverRegenerates()
    {
        _vitals.SpendPrana(40f);
        var spent = _vitals.Prana;

        _vitals.Tick(60f, inCombat: false);

        Assert.That(_vitals.Prana, Is.EqualTo(spent),
            "the setting's scarcity is not real if the player's own bar refills for free");
    }

    [Test]
    public void StaminaNeverExceedsItsCeiling()
    {
        _vitals.Tick(1000f, inCombat: false);
        Assert.That(_vitals.Stamina, Is.EqualTo(_vitals.MaxStamina));
    }

    [Test]
    public void SpendingMoreStaminaThanHeldFails()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_vitals.SpendStamina(9999f), Is.False);
            Assert.That(_vitals.Stamina, Is.EqualTo(_vitals.MaxStamina));
        });
    }

    [Test]
    public void NonPositiveResourceSpendsAreRejected()
    {
        var stamina = _vitals.Stamina;
        var prana = _vitals.Prana;

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.SpendStamina(0f), Is.False);
            Assert.That(_vitals.SpendStamina(-10f), Is.False);
            Assert.That(_vitals.SpendPrana(0f), Is.False);
            Assert.That(_vitals.SpendPrana(-10f), Is.False);
            Assert.That(_vitals.Stamina, Is.EqualTo(stamina));
            Assert.That(_vitals.Prana, Is.EqualTo(prana));
        });
    }

    [Test]
    public void LevellingRaisesTheCeilingsAndRefillsHealth()
    {
        var maxHealth = _vitals.MaxHealth;
        var maxPrana = _vitals.MaxPrana;

        _vitals.TakeDamage(40f);
        _vitals.AddXp(_vitals.XpToLevel);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Level, Is.EqualTo(2));
            Assert.That(_vitals.MaxHealth, Is.GreaterThan(maxHealth));
            Assert.That(_vitals.MaxPrana, Is.GreaterThan(maxPrana));
            Assert.That(_vitals.Health, Is.EqualTo(_vitals.MaxHealth));
        });
    }

    [Test]
    public void LevellingIsNotASilentResupplyOfCrystals()
    {
        _vitals.SpendPrana(40f);
        var prana = _vitals.Prana;

        _vitals.AddXp(_vitals.XpToLevel);

        Assert.That(_vitals.Prana, Is.EqualTo(prana),
            "prana is charge, not a pool: levelling raises the ceiling but hands out no stones");
    }

    [Test]
    public void OneLargeXpGrantCanCarryMoreThanOneLevel()
    {
        var levels = 0;
        _vitals.LevelGained += _ => levels++;

        _vitals.AddXp(5000);

        Assert.That(levels, Is.GreaterThan(1));
    }

    [Test]
    public void ArmourReducesDamageButNeverToNothing()
    {
        var bare = new PlayerVitals(new Inventory());
        var armoured = new PlayerVitals(new Inventory());

        bare.TakeDamage(20f);
        armoured.TakeDamage(20f, armour: 5f);

        Assert.Multiple(() =>
        {
            Assert.That(armoured.Health, Is.GreaterThan(bare.Health));
            Assert.That(armoured.Health, Is.LessThan(armoured.MaxHealth));
        });
    }

    [Test]
    public void OverwhelmingArmourStillLetsAHitLand()
    {
        var dealt = _vitals.TakeDamage(2f, armour: 9999f);
        Assert.That(dealt, Is.EqualTo(DamageMath.MinimumDamage),
            "armour can never make the player invulnerable");
    }

    [Test]
    public void BlockingHalvesWhatGetsThrough()
    {
        var guarded = _vitals.TakeDamage(40f, armour: 0f, blocking: true);
        Assert.That(guarded, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void DyingFiresOnceAndStops()
    {
        var deaths = 0;
        _vitals.Died += () => deaths++;

        _vitals.TakeDamage(9999f);
        _vitals.TakeDamage(9999f);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.IsAlive, Is.False);
            Assert.That(_vitals.Health, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
        });
    }

    [Test]
    public void HealingNeverOverfills()
    {
        _vitals.TakeDamage(10f);
        _vitals.Heal(9999f);
        Assert.That(_vitals.Health, Is.EqualTo(_vitals.MaxHealth));
    }

    [Test]
    public void CastingDrawsAJivaStoneWhenTheReserveIsShort()
    {
        _inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1, SoulCrystals.ItemKind);
        _vitals.SpendPrana(_vitals.MaxPrana);
        Assume.That(_vitals.Prana, Is.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.SpendPrana(10f), Is.True);
            Assert.That(_inventory.CountOf(SoulCrystals.LesserId), Is.Zero, "the stone is consumed");
            Assert.That(_vitals.Channeled, Is.EqualTo(1));
        });
    }

    [Test]
    public void WithoutAStoneTheCastIsRefusedAndNothingIsCharged()
    {
        _vitals.SpendPrana(_vitals.MaxPrana);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.SpendPrana(10f), Is.False);
            Assert.That(_vitals.Prana, Is.Zero);
            Assert.That(_vitals.Channeled, Is.Zero);
        });
    }

    [Test]
    public void AnUnaffordablePranaSpendDoesNotBurnPartialStones()
    {
        _vitals.SpendPrana(_vitals.MaxPrana);
        _inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1, SoulCrystals.ItemKind);

        Assert.That(_vitals.SpendPrana(SoulCrystals.LesserCharge * 2f), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Prana, Is.Zero);
            Assert.That(_inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(1));
            Assert.That(_vitals.Channeled, Is.Zero);
        });
    }

    [Test]
    public void APranaSpendAboveTheReserveIsRefusedWithoutBurningStones()
    {
        _vitals.SpendPrana(_vitals.MaxPrana);
        _inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 5, SoulCrystals.ItemKind);

        Assert.That(_vitals.SpendPrana(_vitals.MaxPrana + 1f), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Prana, Is.Zero);
            Assert.That(_inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(5));
            Assert.That(_vitals.Channeled, Is.Zero);
        });
    }

    [Test]
    public void DrawingAStoneNeverOverfillsOrConsumesAtFullReserve()
    {
        _inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1, SoulCrystals.ItemKind);
        Assert.That(_vitals.TryDrawCrystal(), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Prana, Is.EqualTo(_vitals.MaxPrana));
            Assert.That(_inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(1));
        });
    }

    [Test]
    public void DeathRecoveryDoesNotRefillCrystalsYouDidNotPayFor()
    {
        _vitals.SpendPrana(40f);
        var prana = _vitals.Prana;

        _vitals.TakeDamage(9999f);
        _vitals.FullRestore();

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Health, Is.EqualTo(_vitals.MaxHealth));
            Assert.That(_vitals.Stamina, Is.EqualTo(_vitals.MaxStamina));
            Assert.That(_vitals.Prana, Is.EqualTo(prana),
                "dying is not a way to refill crystals you did not spend gold on");
        });
    }

    [Test]
    public void GoldNeverGoesNegative()
    {
        _vitals.AddGold(10);
        Assert.Multiple(() =>
        {
            Assert.That(_vitals.SpendGold(50), Is.False);
            Assert.That(_vitals.Gold, Is.EqualTo(10));
            Assert.That(_vitals.SpendGold(10), Is.True);
            Assert.That(_vitals.Gold, Is.Zero);
        });
    }

    [Test]
    public void SaveAndReloadPreservesEveryValue()
    {
        _vitals.AddXp(200);
        _vitals.AddGold(120);
        _vitals.TakeDamage(15f);
        _vitals.SpendStamina(20f);

        var restored = new PlayerVitals(new Inventory());
        restored.Restore(_vitals.Capture());

        Assert.Multiple(() =>
        {
            Assert.That(restored.Level, Is.EqualTo(_vitals.Level));
            Assert.That(restored.Xp, Is.EqualTo(_vitals.Xp));
            Assert.That(restored.Gold, Is.EqualTo(_vitals.Gold));
            Assert.That(restored.Health, Is.EqualTo(_vitals.Health).Within(0.001f));
            Assert.That(restored.MaxHealth, Is.EqualTo(_vitals.MaxHealth).Within(0.001f));
            Assert.That(restored.Stamina, Is.EqualTo(_vitals.Stamina).Within(0.001f));
        });
    }

    [Test]
    public void ACorruptSaveIsClampedRatherThanTrusted()
    {
        _vitals.Restore(new SavedVitals
        {
            Level = -5, Xp = -100, Gold = -50, Channeled = -1,
            Health = 9999f, MaxHealth = 100f, Stamina = -20f, MaxStamina = 100f
        });

        Assert.Multiple(() =>
        {
            Assert.That(_vitals.Level, Is.EqualTo(1));
            Assert.That(_vitals.Xp, Is.Zero);
            Assert.That(_vitals.Gold, Is.Zero);
            Assert.That(_vitals.Health, Is.EqualTo(100f), "health cannot exceed its own ceiling");
            Assert.That(_vitals.Stamina, Is.Zero);
        });
    }

    [Test]
    public void ADeadCharacterTakesNoFurtherDamage()
    {
        _vitals.TakeDamage(9999f);
        Assert.That(_vitals.TakeDamage(50f), Is.Zero);
    }
}
