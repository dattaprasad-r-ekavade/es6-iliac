using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What can be true of an enemy at the same time.
///
/// Reported from a run as "burning enemy is staggered, burn is gone". The burn was not gone —
/// Tick counts it down and applies it whatever else is happening — but the nameplate showed
/// one status out of a priority chain, so the word "burning" disappeared the moment a stagger
/// landed and the player drew the obvious conclusion.
///
/// These pin the state, so that if the effects ever really do start cancelling each other the
/// failure lands here rather than in somebody's run.
/// </summary>
[TestFixture]
public sealed class EffectStackingTests
{
    private static Enemy Bandit()
    {
        var archetype = EnemyCatalog.Find(EnemyCatalog.BanditId)!;
        return new Enemy(archetype, "test.bandit");
    }

    [Test]
    public void AStaggerDoesNotPutOutAFire()
    {
        var enemy = Bandit();
        enemy.ApplyBurn(4f, 3f);

        Assert.That(enemy.IsBurning, Is.True);

        enemy.ApplyStagger(1f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsStaggered, Is.True);
            Assert.That(enemy.IsBurning, Is.True, "the stagger must not cancel the burn");
        });
    }

    [Test]
    public void ABurningStaggeredChilledEnemyIsAllThreeAtOnce()
    {
        var enemy = Bandit();

        enemy.ApplyBurn(4f, 3f);
        enemy.ApplyChill(0.6f, 2f);
        enemy.ApplyStagger(1f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsBurning, Is.True);
            Assert.That(enemy.IsChilled, Is.True);
            Assert.That(enemy.IsStaggered, Is.True);
        });
    }

    /// <summary>The burn keeps eating health through a stagger, which is the point of it.</summary>
    [Test]
    public void TheFireKeepsBurningWhileTheEnemyIsStaggered()
    {
        var enemy = Bandit();
        enemy.ApplyBurn(10f, 3f);
        enemy.ApplyStagger(2f);

        var before = enemy.Health;
        enemy.Tick(1f);

        Assert.That(enemy.Health, Is.LessThan(before),
            "a staggered enemy should still be losing health to the fire on it");
        Assert.That(enemy.IsStaggered, Is.True, "and still be staggered a second in");
    }

    /// <summary>
    /// Each effect runs on its own clock, so a short stagger ending leaves a long burn alone.
    /// </summary>
    [Test]
    public void EffectsExpireIndependently()
    {
        var enemy = Bandit();
        enemy.ApplyBurn(2f, 5f);
        enemy.ApplyStagger(0.5f);

        enemy.Tick(1f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.IsStaggered, Is.False, "half a second of stagger is over");
            Assert.That(enemy.IsBurning, Is.True, "five seconds of burning is not");
        });
    }

    /// <summary>Re-applying takes the longer of the two rather than shortening what is there.</summary>
    [Test]
    public void ARefreshNeverCutsAnEffectShort()
    {
        var enemy = Bandit();

        enemy.ApplyStagger(2f);
        enemy.ApplyStagger(0.2f);
        enemy.Tick(0.5f);

        Assert.That(enemy.IsStaggered, Is.True,
            "a short stagger landing on a long one must not end it early");
    }
}
