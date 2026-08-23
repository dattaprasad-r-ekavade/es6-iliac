using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class EnemyLevelTests
{
    private static readonly EnemyArchetype Bandit = new()
    {
        Id = "bandit", DisplayName = "Bandit",
        MaxHealth = 55f, AttackDamage = 7f, XpReward = 20
    };

    [Test]
    public void LevelOneIsTheArchetypeItself()
    {
        Assert.That(Bandit.AtLevel(1), Is.SameAs(Bandit));
    }

    [Test]
    public void ADeeperBanditIsToughen()
    {
        var deep = Bandit.AtLevel(5);

        Assert.Multiple(() =>
        {
            Assert.That(deep.MaxHealth, Is.GreaterThan(Bandit.MaxHealth));
            Assert.That(deep.AttackDamage, Is.GreaterThan(Bandit.AttackDamage));
            Assert.That(deep.XpReward, Is.GreaterThan(Bandit.XpReward));
            Assert.That(deep.Level, Is.EqualTo(5));
        });
    }

    [Test]
    public void HealthGrowsFasterThanDamage()
    {
        var deep = Bandit.AtLevel(10);
        var healthRatio = deep.MaxHealth / Bandit.MaxHealth;
        var damageRatio = deep.AttackDamage / Bandit.AttackDamage;

        Assert.That(healthRatio, Is.GreaterThan(damageRatio),
            "a run should get longer before it gets lethal");
    }

    [Test]
    public void SpeedAndReachDoNotScale()
    {
        var deep = Bandit.AtLevel(12);

        Assert.Multiple(() =>
        {
            Assert.That(deep.MoveSpeed, Is.EqualTo(Bandit.MoveSpeed));
            Assert.That(deep.AttackRange, Is.EqualTo(Bandit.AttackRange));
            Assert.That(deep.AttackCooldown, Is.EqualTo(Bandit.AttackCooldown),
                "a deeper enemy should hit harder, not become a different animal");
        });
    }

    [Test]
    public void ScalingIsMonotonic()
    {
        var previous = Bandit;
        for (var level = 2; level <= 20; level++)
        {
            var next = Bandit.AtLevel(level);
            Assert.That(next.MaxHealth, Is.GreaterThan(previous.MaxHealth), $"level {level}");
            Assert.That(next.XpReward, Is.GreaterThanOrEqualTo(previous.XpReward), $"level {level}");
            previous = next;
        }
    }

    [Test]
    public void ALevelledEnemyCanBeTitled()
    {
        Assert.That(Bandit.AtLevel(4, "Bandit Reaver").DisplayName, Is.EqualTo("Bandit Reaver"));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void JunkLevelsClampToTheFirst(int level)
    {
        var scaled = Bandit.AtLevel(level);
        Assert.That(scaled.MaxHealth, Is.EqualTo(Bandit.MaxHealth));
    }

    [Test]
    public void ALevelledEnemySpawnsWithItsScaledHealth()
    {
        var deep = Bandit.AtLevel(6);
        var enemy = new Enemy(deep, "bandit.deep.01");

        Assert.That(enemy.Health, Is.EqualTo(deep.MaxHealth).Within(0.01f));
    }
}

public class SpellDeliveryTests
{
    private PlayerCharacter _player = null!;

    [SetUp]
    public void Setup() => _player = PlayerCharacter.NewGame();

    private static Enemy Spawn() =>
        new(new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = 500f }, "b.01");

    [Test]
    public void PayingChargesButAppliesNothing()
    {
        var enemy = Spawn();
        var before = _player.Vitals.Prana;

        var paid = _player.Spells.Pay(SpellCatalog.FireId);

        Assert.Multiple(() =>
        {
            Assert.That(paid.WasCast, Is.True);
            Assert.That(_player.Vitals.Prana, Is.LessThan(before), "the cast is paid for at once");
            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth), "but nothing has landed yet");
        });
    }

    [Test]
    public void DeliveringAppliesTheEffect()
    {
        var enemy = Spawn();
        var paid = _player.Spells.Pay(SpellCatalog.FireId);

        Assert.Multiple(() =>
        {
            Assert.That(_player.Spells.Deliver(paid.Spell!, enemy), Is.True);
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth));
            Assert.That(enemy.IsBurning, Is.True);
        });
    }

    [Test]
    public void PayingWithoutChargeDeliversNothing()
    {
        _player.Inventory.Consume(SoulCrystals.LesserId, _player.Inventory.CountOf(SoulCrystals.LesserId));
        _player.Vitals.SpendPrana(_player.Vitals.MaxPrana);

        var paid = _player.Spells.Pay(SpellCatalog.FireId);

        Assert.Multiple(() =>
        {
            Assert.That(paid.Result, Is.EqualTo(CastResult.NoCharge));
            Assert.That(paid.WasCast, Is.False);
        });
    }

    [Test]
    public void DeliveringToNothingTrainsNothing()
    {
        var paid = _player.Spells.Pay(SpellCatalog.FireId);
        _player.Spells.Deliver(paid.Spell!, target: null);

        Assert.That(_player.Skills.LevelOf(Skills.Destruction), Is.Zero);
    }

    [Test]
    public void OnlyDestructionSpellsTravel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellCaster.IsProjectile(SpellCatalog.Get(SpellCatalog.FireId)!), Is.True);
            Assert.That(SpellCaster.IsProjectile(SpellCatalog.Get(SpellCatalog.FrostId)!), Is.True);
            Assert.That(SpellCaster.IsProjectile(SpellCatalog.Get(SpellCatalog.ShockId)!), Is.True);
            Assert.That(SpellCaster.IsProjectile(SpellCatalog.Get(SpellCatalog.HealId)!), Is.False);
            Assert.That(SpellCaster.IsProjectile(SpellCatalog.Get(SpellCatalog.LightId)!), Is.False);
        });
    }

    [Test]
    public void TheOneShotCastStillWorksForCallersThatWantIt()
    {
        var enemy = Spawn();
        Assert.That(_player.Spells.Cast(SpellCatalog.FireId, enemy).Result,
            Is.EqualTo(CastResult.Landed));
    }
}
