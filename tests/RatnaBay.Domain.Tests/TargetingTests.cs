using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class TargetingTests
{
    private static Enemy At(float x, float z, float health = 55f)
    {
        var enemy = new Enemy(new EnemyArchetype
        {
            Id = "bandit", DisplayName = "Bandit", MaxHealth = health
        }, $"bandit@{x}_{z}")
        {
            Position = new WorldPoint(x, 0f, z)
        };

        enemy.Home = enemy.Position;
        return enemy;
    }

    /// <summary>Yaw zero looks down -Z, a quarter turn clockwise looks down +X.</summary>
    [TestCase(0f, 0f, -1f)]
    [TestCase(MathF.PI / 2f, 1f, 0f)]
    [TestCase(MathF.PI, 0f, 1f)]
    [TestCase(-MathF.PI / 2f, -1f, 0f)]
    public void ForwardFollowsTheCameraConvention(float yaw, float x, float z)
    {
        var forward = Targeting.FlatForward(yaw);
        Assert.Multiple(() =>
        {
            Assert.That(forward.X, Is.EqualTo(x).Within(0.001f));
            Assert.That(forward.Z, Is.EqualTo(z).Within(0.001f));
        });
    }

    [Test]
    public void SomethingStraightAheadAndInReachIsHit()
    {
        var target = At(0f, -2f);
        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { target }), Is.SameAs(target));
    }

    [Test]
    public void SomethingBehindYouIsNotHit()
    {
        var behind = At(0f, 2f);
        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { behind }), Is.Null);
    }

    [Test]
    public void SomethingOutOfReachIsNotHit()
    {
        var far = At(0f, -30f);
        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { far }), Is.Null);
    }

    [Test]
    public void SomethingOffToTheSideIsOutsideTheCone()
    {
        // Two metres away, but ninety degrees off the facing.
        var beside = At(2f, 0f);
        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { beside }), Is.Null);
    }

    [Test]
    public void TurningToFaceItBringsItIntoTheCone()
    {
        var beside = At(2f, 0f);
        Assert.That(Targeting.Find(default, MathF.PI / 2f, 2.4f, new[] { beside }),
            Is.SameAs(beside));
    }

    [Test]
    public void ADeadTargetIsSkipped()
    {
        var corpse = At(0f, -2f);
        corpse.TakeDamage(9999f);

        var living = At(0f, -2.2f);

        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { corpse, living }), Is.SameAs(living));
    }

    [Test]
    public void TheNearestCandidateWins()
    {
        var far = At(0f, -2.3f);
        var near = At(0f, -1.2f);

        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { far, near }), Is.SameAs(near));
    }

    [Test]
    public void SomethingStandingOnTopOfYouCounts()
    {
        // A zero-length delta has no direction; it must not be decided to be behind you.
        var onTop = At(0f, 0f);
        Assert.That(Targeting.Find(default, 0f, 2.4f, new[] { onTop }), Is.SameAs(onTop));
    }

    [Test]
    public void SwingingAtAnEmptyWorldFindsNothing()
    {
        Assert.That(Targeting.Find(default, 0f, 2.4f, Array.Empty<Enemy>()), Is.Null);
    }

    [Test]
    public void ASpellConeIsTighterThanASwing()
    {
        // Far enough off-axis to be swung at but not cast at.
        var offAxis = At(3f, -8f);
        var candidates = new[] { offAxis };

        Assert.Multiple(() =>
        {
            Assert.That(Targeting.Find(default, 0f, 18f, candidates), Is.SameAs(offAxis));
            Assert.That(Targeting.Find(default, 0f, 18f, candidates, Targeting.SpellConeRadians),
                Is.Null);
        });
    }

    [Test]
    public void ArcJumpsToTheNearestOtherTarget()
    {
        var primary = At(0f, -2f);
        var near = At(1f, -2f);
        var far = At(5f, -2f);

        Assert.That(Targeting.FindNearestOther(primary, new[] { primary, near, far }, 6f),
            Is.SameAs(near));
    }

    [Test]
    public void ArcNeverJumpsBackToItsOwnTarget()
    {
        var primary = At(0f, -2f);
        Assert.That(Targeting.FindNearestOther(primary, new[] { primary }, 6f), Is.Null);
    }

    [Test]
    public void ArcWillNotJumpAcrossTheRoom()
    {
        var primary = At(0f, -2f);
        var distant = At(40f, -2f);
        Assert.That(Targeting.FindNearestOther(primary, new[] { primary, distant }, 6f), Is.Null);
    }
}

public class EnemyIntentTests
{
    private static Enemy Spawn(float x = 0f, float z = 0f) =>
        new(new EnemyArchetype
        {
            Id = "bandit", DisplayName = "Bandit",
            AggroRange = 14f, AttackRange = 2.1f, AttackCooldown = 1.4f
        }, "bandit.01")
        {
            Position = new WorldPoint(x, 0f, z),
            Home = new WorldPoint(x, 0f, z)
        };

    [Test]
    public void ADistantPlayerIsIgnored()
    {
        Assert.That(Spawn().Decide(new WorldPoint(0f, 0f, 40f)), Is.EqualTo(EnemyIntent.Idle));
    }

    [Test]
    public void APlayerInsideAggroRangeIsChased()
    {
        Assert.That(Spawn().Decide(new WorldPoint(0f, 0f, 10f)), Is.EqualTo(EnemyIntent.Chase));
    }

    [Test]
    public void APlayerInReachIsAttacked()
    {
        Assert.That(Spawn().Decide(new WorldPoint(0f, 0f, 2f)), Is.EqualTo(EnemyIntent.Attack));
    }

    [Test]
    public void AttackingStartsACooldownDuringWhichItWaits()
    {
        var enemy = Spawn();
        var player = new WorldPoint(0f, 0f, 2f);

        Assume.That(enemy.Decide(player), Is.EqualTo(EnemyIntent.Attack));
        enemy.Attack();

        Assert.That(enemy.Decide(player), Is.EqualTo(EnemyIntent.Idle));

        enemy.Tick(2f);
        Assert.That(enemy.Decide(player), Is.EqualTo(EnemyIntent.Attack));
    }

    [Test]
    public void AWallStopsItAttackingThroughTheBuildingItGuards()
    {
        var enemy = Spawn();
        Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 2f), canSeePlayer: false),
            Is.EqualTo(EnemyIntent.Idle));
    }

    [Test]
    public void AStaggeredEnemyNeitherClosesNorSwings()
    {
        var enemy = Spawn();
        enemy.ApplyStagger(1.2f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 2f)), Is.EqualTo(EnemyIntent.Idle));
            Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 10f)), Is.EqualTo(EnemyIntent.Idle));
        });
    }

    [Test]
    public void ADeadEnemyWantsNothing()
    {
        var enemy = Spawn();
        enemy.TakeDamage(9999f);
        Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 2f)), Is.EqualTo(EnemyIntent.Idle));
    }

    [Test]
    public void AnEnemyLedTooFarFromHomeGivesUp()
    {
        var enemy = Spawn();
        enemy.Position = new WorldPoint(0f, 0f, 200f);

        Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 202f)), Is.EqualTo(EnemyIntent.Idle),
            "the player should not be able to drag a bandit across the world");
    }

    [Test]
    public void ChillingItDoesNotStopItWantingToChase()
    {
        var enemy = Spawn();
        enemy.ApplyChill(0.45f, 4f);

        Assert.Multiple(() =>
        {
            Assert.That(enemy.Decide(new WorldPoint(0f, 0f, 10f)), Is.EqualTo(EnemyIntent.Chase));
            Assert.That(enemy.CurrentMoveSpeed, Is.LessThan(enemy.Archetype.MoveSpeed),
                "frost buys distance rather than safety");
        });
    }
}

public class RelativeBearingTests
{
    private static WorldPoint At(float x, float z) => new(x, 0f, z);

    [Test]
    public void SomethingDeadAheadReadsAsZero()
    {
        Assert.That(Targeting.RelativeBearing(default, 0f, At(0f, -10f)),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SomethingToTheRightReadsPositive()
    {
        Assert.That(Targeting.RelativeBearing(default, 0f, At(10f, 0f)),
            Is.EqualTo(MathF.PI / 2f).Within(0.001f));
    }

    [Test]
    public void SomethingToTheLeftReadsNegative()
    {
        Assert.That(Targeting.RelativeBearing(default, 0f, At(-10f, 0f)),
            Is.EqualTo(-MathF.PI / 2f).Within(0.001f));
    }

    [Test]
    public void SomethingSlightlyLeftIsASmallNegativeAngleNotNearlyAFullTurn()
    {
        var bearing = Targeting.RelativeBearing(default, 0f, At(-1f, -10f));
        Assert.That(bearing, Is.LessThan(0f).And.GreaterThan(-0.5f));
    }

    [Test]
    public void TurningToFaceSomethingBringsItsBearingToZero()
    {
        var target = At(10f, -10f);
        var bearing = Targeting.RelativeBearing(default, 0f, target);

        Assert.That(Targeting.RelativeBearing(default, bearing, target),
            Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void SomethingDirectlyBehindIsAtTheWrapPoint()
    {
        var bearing = Targeting.RelativeBearing(default, 0f, At(0f, 10f));
        Assert.That(MathF.Abs(bearing), Is.EqualTo(MathF.PI).Within(0.001f));
    }

    [Test]
    public void StandingOnTheTargetIsNotADivideByZero()
    {
        Assert.That(Targeting.RelativeBearing(default, 0f, default), Is.Zero);
    }

    [Test]
    public void TheBearingAgreesWithTheDirectionForwardActuallyPoints()
    {
        // If the bearing says a target is dead ahead, walking along FlatForward must close on it.
        foreach (var yaw in new[] { 0f, 0.7f, -1.4f, 2.9f })
        {
            var forward = Targeting.FlatForward(yaw);
            var ahead = At(forward.X * 12f, forward.Z * 12f);
            Assert.That(Targeting.RelativeBearing(default, yaw, ahead),
                Is.EqualTo(0f).Within(0.001f), $"yaw {yaw}");
        }
    }
}
