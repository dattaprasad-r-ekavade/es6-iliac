using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class SpellCasterTests
{
    private PlayerCharacter _player = null!;

    [SetUp]
    public void Setup() => _player = PlayerCharacter.NewGame();

    private static Enemy Spawn(float health = 500f) =>
        new(new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = health }, "bandit.01");

    [Test]
    public void FireIsBoundByDefault()
    {
        Assert.That(_player.Spells.SelectedSpellId, Is.EqualTo(SpellCatalog.FireId));
    }

    [Test]
    public void SelectingAnUnknownSpellIsIgnored()
    {
        _player.Spells.SelectSpell("spell.nonexistent");
        Assert.That(_player.Spells.SelectedSpellId, Is.EqualTo(SpellCatalog.FireId));
    }

    [Test]
    public void CastingAtAnEnemyHurtsIt()
    {
        var enemy = Spawn();
        var outcome = _player.Spells.Cast(SpellCatalog.FireId, enemy);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CastResult.Landed));
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth));
        });
    }

    [Test]
    public void CastingCostsPrana()
    {
        var before = _player.Vitals.Prana;
        _player.Spells.Cast(SpellCatalog.FireId, Spawn());
        Assert.That(_player.Vitals.Prana, Is.LessThan(before));
    }

    [Test]
    public void WithNoChargeAndNoStoneTheSpellIsNeverPaidFor()
    {
        _player.Inventory.Consume(SoulCrystals.LesserId, _player.Inventory.CountOf(SoulCrystals.LesserId));
        _player.Vitals.SpendPrana(_player.Vitals.MaxPrana);

        var enemy = Spawn();
        var outcome = _player.Spells.Cast(SpellCatalog.FireId, enemy);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CastResult.NoCharge));
            Assert.That(outcome.WasCast, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth), "an unpaid spell applies nothing");
        });
    }

    [Test]
    public void CastingAtWallsIsNotPractice()
    {
        _player.Spells.Cast(SpellCatalog.FireId, target: null);
        Assert.That(_player.Skills.LevelOf(Skills.Destruction), Is.Zero);
    }

    [Test]
    public void ALandedDestructionSpellTrains()
    {
        _player.Spells.Cast(SpellCatalog.FireId, Spawn());
        Assert.That(_player.Skills.LevelOf(Skills.Destruction), Is.GreaterThan(0f));
    }

    [Test]
    public void AHealThatHealsIsAUse()
    {
        _player.Vitals.TakeDamage(40f);
        _player.Spells.Cast(SpellCatalog.HealId);
        Assert.That(_player.Skills.LevelOf(Skills.Restoration), Is.GreaterThan(0f));
    }

    [Test]
    public void MendRestoresHealth()
    {
        _player.Vitals.TakeDamage(50f);
        var hurt = _player.Vitals.Health;

        _player.Spells.Cast(SpellCatalog.HealId);

        Assert.That(_player.Vitals.Health, Is.GreaterThan(hurt));
    }

    [Test]
    public void EmberlightBurnsDownOverTime()
    {
        _player.Spells.Cast(SpellCatalog.LightId);
        Assert.That(_player.Spells.LightActive, Is.True);

        _player.Spells.Tick(61f);

        Assert.That(_player.Spells.LightActive, Is.False,
            "carrying a light in a crystal-lit world is consuming the resource");
    }

    [Test]
    public void FlameLeavesTheTargetBurning()
    {
        var enemy = Spawn();
        _player.Spells.Cast(SpellCatalog.FireId, enemy);
        Assert.That(enemy.IsBurning, Is.True);
    }

    [Test]
    public void RimeSlowsTheTarget()
    {
        var enemy = Spawn();
        var speed = enemy.CurrentMoveSpeed;

        _player.Spells.Cast(SpellCatalog.FrostId, enemy);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsChilled, Is.True);
            Assert.That(enemy.CurrentMoveSpeed, Is.LessThan(speed));
        });
    }

    [Test]
    public void ArcStaggersTheTarget()
    {
        var enemy = Spawn();
        _player.Spells.Cast(SpellCatalog.ShockId, enemy);
        Assert.That(enemy.IsStaggered, Is.True);
    }

    [Test]
    public void ArcJumpsToOneSecondTargetAtReducedPower()
    {
        var primary = Spawn();
        var secondary = Spawn();

        _player.Spells.Cast(SpellCatalog.ShockId, primary, secondary);

        var primaryDamage = primary.MaxHealth - primary.Health;
        var secondaryDamage = secondary.MaxHealth - secondary.Health;

        Assert.Multiple(() =>
        {
            Assert.That(secondaryDamage, Is.GreaterThan(0f));
            Assert.That(secondaryDamage, Is.LessThan(primaryDamage));
            Assert.That(secondary.IsStaggered, Is.True);
        });
    }

    [Test]
    public void OnlyArcChains()
    {
        var primary = Spawn();
        var secondary = Spawn();

        _player.Spells.Cast(SpellCatalog.FireId, primary, secondary);

        Assert.That(secondary.Health, Is.EqualTo(secondary.MaxHealth));
    }

    [Test]
    public void ArcNeverChainsBackToItsOwnTarget()
    {
        var enemy = Spawn();
        _player.Spells.Cast(SpellCatalog.ShockId, enemy, chainTarget: enemy);

        var spell = SpellCatalog.Get(SpellCatalog.ShockId)!;
        Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(spell.Power).Within(0.001f));
    }

    [Test]
    public void MasteryMakesTheSameSpellCheaper()
    {
        var spell = SpellCatalog.Get(SpellCatalog.FireId)!;
        var novice = _player.Spells.CostOf(spell);

        for (var i = 0; i < 400; i++)
        {
            _player.Skills.ReportUse(Skills.Destruction, 50f, 500f);
            _player.Skills.EndEncounter();
        }

        Assert.That(_player.Spells.CostOf(spell), Is.LessThan(novice));
    }

    [Test]
    public void CastingWithAnEmptyReserveDrawsAStone()
    {
        _player.Vitals.SpendPrana(_player.Vitals.MaxPrana);
        var stones = _player.Inventory.CountOf(SoulCrystals.LesserId);
        Assume.That(stones, Is.GreaterThan(0));

        var outcome = _player.Spells.Cast(SpellCatalog.FireId, Spawn());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.WasCast, Is.True);
            Assert.That(_player.Inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(stones - 1));
            Assert.That(_player.Story.State.PlayerChanneled, Is.EqualTo(1f),
                "the world has to hear about every stone drawn");
        });
    }

    [Test]
    public void AnUnknownSpellIdIsRefusedWithoutCharge()
    {
        var before = _player.Vitals.Prana;
        var outcome = _player.Spells.Cast("spell.nonexistent", Spawn());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(CastResult.UnknownSpell));
            Assert.That(_player.Vitals.Prana, Is.EqualTo(before));
        });
    }
}
