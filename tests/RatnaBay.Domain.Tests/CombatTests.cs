using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class CombatTests
{
    private PlayerCharacter _player = null!;

    private static readonly EnemyArchetype Bandit = new()
    {
        Id = "bandit", DisplayName = "Bandit", MaxHealth = 55f, AttackDamage = 4f
    };

    [SetUp]
    public void Setup() => _player = PlayerCharacter.NewGame();

    private static Enemy Spawn(float health = 55f) =>
        new(new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = health }, "bandit.01");

    [Test]
    public void AFreshCharacterIsArmedFromTheirOwnPack()
    {
        // Not inexplicably swinging bare hands past the sword in their pack.
        Assert.That(_player.Equipment.WeaponId, Is.EqualTo("iron_sword"));
    }

    [Test]
    public void SwingingAtAnEnemyHurtsIt()
    {
        var enemy = Spawn();
        var outcome = _player.Combat.TryAttack(enemy);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(AttackResult.Hit));
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth));
        });
    }

    [Test]
    public void SwingingAtAirTrainsNothing()
    {
        var outcome = _player.Combat.TryAttack(null);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(AttackResult.Missed));
            Assert.That(_player.Skills.LevelOf(Skills.Blade), Is.Zero,
                "gains come from effect, not action");
        });
    }

    [Test]
    public void ALandedHitTrainsTheWeaponSkill()
    {
        _player.Combat.TryAttack(Spawn(500f));
        Assert.That(_player.Skills.LevelOf(Skills.Blade), Is.GreaterThan(0f));
    }

    [Test]
    public void ATwoHanderTrainsHeavyRatherThanBlade()
    {
        _player.Inventory.Add("iron_greatsword", "Iron Greatsword", 1, "weapon");
        _player.Equipment.Equip("iron_greatsword");

        _player.Combat.TryAttack(Spawn(500f));

        Assert.Multiple(() =>
        {
            Assert.That(_player.Skills.LevelOf(Skills.Heavy), Is.GreaterThan(0f));
            Assert.That(_player.Skills.LevelOf(Skills.Blade), Is.Zero);
        });
    }

    [Test]
    public void YouCannotSwingAgainUntilTheCooldownElapses()
    {
        _player.Combat.TryAttack(Spawn());
        Assert.That(_player.Combat.TryAttack(Spawn()).Result, Is.EqualTo(AttackResult.OnCooldown));

        _player.Combat.Tick(5f);
        Assert.That(_player.Combat.TryAttack(Spawn()).Result, Is.EqualTo(AttackResult.Hit));
    }

    [Test]
    public void ExhaustionStopsTheSwingWithoutStartingACooldown()
    {
        _player.Vitals.SpendStamina(_player.Vitals.Stamina);

        var outcome = _player.Combat.TryAttack(Spawn());

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(AttackResult.Exhausted));
            Assert.That(_player.Combat.IsReady, Is.True);
        });
    }

    [Test]
    public void ATwoHandedWeaponCannotBlock()
    {
        _player.Inventory.Add("iron_greatsword", "Iron Greatsword", 1, "weapon");
        _player.Equipment.Equip("iron_greatsword");

        _player.Combat.SetBlocking(true);

        Assert.That(_player.Combat.IsBlocking, Is.False,
            "not blocking is the whole trade for two-handed damage");
    }

    [Test]
    public void AttackingDropsTheGuard()
    {
        _player.Combat.SetBlocking(true);
        Assume.That(_player.Combat.IsBlocking, Is.True);

        _player.Combat.TryAttack(Spawn());

        Assert.That(_player.Combat.IsBlocking, Is.False,
            "a block cannot be held through a swing");
    }

    [Test]
    public void BlockingReducesWhatTheEnemyLands()
    {
        var unguarded = PlayerCharacter.NewGame();
        unguarded.Combat.TakeHit(30f);

        _player.Combat.SetBlocking(true);
        _player.Combat.TakeHit(30f);

        Assert.That(_player.Vitals.Health, Is.GreaterThan(unguarded.Vitals.Health));
    }

    [Test]
    public void WornArmourReducesWhatTheEnemyLands()
    {
        var unarmoured = PlayerCharacter.NewGame();
        unarmoured.Combat.TakeHit(20f);

        _player.Inventory.Add("mail_hauberk", "Mail Hauberk", 1, "armour");
        _player.Equipment.Equip("mail_hauberk");
        _player.Combat.TakeHit(20f);

        Assert.That(_player.Vitals.Health, Is.GreaterThan(unarmoured.Vitals.Health));
    }

    [Test]
    public void BeingHitStartsAFight()
    {
        _player.Combat.TakeHit(5f);
        Assert.That(_player.Combat.InCombat, Is.True);
    }

    [Test]
    public void AFightGoesQuietAfterTheForgetTime()
    {
        _player.Combat.TryAttack(Spawn());
        Assume.That(_player.Combat.InCombat, Is.True);

        _player.Combat.Tick(PlayerCombat.CombatForgetTime + 0.1f);

        Assert.That(_player.Combat.InCombat, Is.False);
    }

    [Test]
    public void TheEndOfAFightLiftsTheSkillEncounterCap()
    {
        // Tick the whole character, not just combat, so stamina regenerates between swings.
        for (var i = 0; i < 200; i++)
        {
            _player.Tick(5f);
            _player.Combat.TryAttack(Spawn(500f));
        }

        var capped = _player.Skills.LevelOf(Skills.Blade);
        Assume.That(capped, Is.GreaterThan(0f));

        _player.Tick(PlayerCombat.CombatForgetTime + 0.1f);
        _player.Combat.TryAttack(Spawn(500f));

        Assert.That(_player.Skills.LevelOf(Skills.Blade), Is.GreaterThan(capped));
    }
}

public class EnemyTests
{
    private static Enemy Spawn(float health = 55f) => new(new EnemyArchetype
    {
        Id = "bandit", DisplayName = "Bandit", MaxHealth = health, AttackCooldown = 1.4f
    }, "bandit.01");

    [Test]
    public void AnEnemyStartsWhole()
    {
        var enemy = Spawn();
        Assert.Multiple(() =>
        {
            Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
            Assert.That(enemy.IsAlive, Is.True);
        });
    }

    [Test]
    public void KillingItFiresDiedExactlyOnce()
    {
        var enemy = Spawn();
        var deaths = 0;
        enemy.Died += _ => deaths++;

        enemy.TakeDamage(9999f);
        enemy.TakeDamage(9999f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(deaths, Is.EqualTo(1));
        });
    }

    [Test]
    public void BurnKeepsDealingDamageAfterTheHit()
    {
        var enemy = Spawn();
        enemy.ApplyBurn(damagePerSecond: 5f, duration: 4f);
        var before = enemy.Health;

        enemy.Tick(1f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsBurning, Is.True);
            Assert.That(enemy.Health, Is.LessThan(before));
        });
    }

    [Test]
    public void BurnStopsWhenItExpires()
    {
        var enemy = Spawn();
        enemy.ApplyBurn(5f, 2f);
        enemy.Tick(3f);
        var settled = enemy.Health;

        enemy.Tick(5f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsBurning, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(settled));
        });
    }

    [Test]
    public void ChillSlowsAndThenWearsOff()
    {
        var enemy = Spawn();
        var normal = enemy.CurrentMoveSpeed;

        enemy.ApplyChill(0.45f, 4f);
        Assert.That(enemy.CurrentMoveSpeed, Is.LessThan(normal));

        enemy.Tick(5f);
        Assert.That(enemy.CurrentMoveSpeed, Is.EqualTo(normal));
    }

    [Test]
    public void ChillNeverStopsATargetCompletely()
    {
        var enemy = Spawn();
        enemy.ApplyChill(0f, 4f);
        Assert.That(enemy.CurrentMoveSpeed, Is.GreaterThan(0f));
    }

    [Test]
    public void StaggerStopsItAttacking()
    {
        var enemy = Spawn();
        Assume.That(enemy.CanAttack, Is.True);

        enemy.ApplyStagger(1.2f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsStaggered, Is.True);
            Assert.That(enemy.CanAttack, Is.False, "shock is control, not a third damage number");
            Assert.That(enemy.Attack(), Is.Zero);
        });
    }

    [Test]
    public void StaggerWearsOff()
    {
        var enemy = Spawn();
        enemy.ApplyStagger(1.2f);
        enemy.Tick(2f);
        Assert.That(enemy.CanAttack, Is.True);
    }

    [Test]
    public void AttacksRespectTheirCooldown()
    {
        var enemy = Spawn();

        Assert.That(enemy.Attack(), Is.GreaterThan(0f));
        Assert.That(enemy.Attack(), Is.Zero, "the cooldown has not elapsed");

        enemy.Tick(2f);
        Assert.That(enemy.Attack(), Is.GreaterThan(0f));
    }

    [Test]
    public void StatusEffectsTakeTheStrongerOfTwoApplications()
    {
        var enemy = Spawn();
        enemy.ApplyChill(0.8f, 2f);
        var mild = enemy.CurrentMoveSpeed;

        enemy.ApplyChill(0.3f, 2f);

        Assert.That(enemy.CurrentMoveSpeed, Is.LessThan(mild));
    }

    [Test]
    public void ADeadEnemyStopsBurning()
    {
        var enemy = Spawn(health: 5f);
        enemy.ApplyBurn(50f, 10f);

        enemy.Tick(1f);
        Assume.That(enemy.IsAlive, Is.False);
        var health = enemy.Health;

        enemy.Tick(5f);
        Assert.That(enemy.Health, Is.EqualTo(health));
    }
}
