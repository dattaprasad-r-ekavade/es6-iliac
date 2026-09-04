using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The boss room, and the three ways a boss fights.
///
/// **Iteration 20's wording was "a boss ends a deeper mine rather than a camp", and that is not
/// what is built.** A mine with a bottom is the exact thing recorded play threw out — press-
/// your-luck cannot work in a level you can finish, and a run that ends because the level did
/// is four decisions and a wall. The boss is a milestone instead: every fifth room, with the
/// mine still endless underneath it. That turns it from a terminus into a reason to press on,
/// and makes the door after it the sharpest instance of the only question this game asks.
/// </summary>
[TestFixture]
public sealed class BossTests
{
    [Test]
    public void EveryFifthRoomHasABoss()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RunState.IsBossRoom(5), Is.True);
            Assert.That(RunState.IsBossRoom(10), Is.True);
            Assert.That(RunState.IsBossRoom(15), Is.True);

            Assert.That(RunState.IsBossRoom(1), Is.False);
            Assert.That(RunState.IsBossRoom(4), Is.False);
            Assert.That(RunState.IsBossRoom(6), Is.False);
        });
    }

    /// <summary>
    /// Room zero is the entrance, and the entrance is not a payable room. Treating it as a
    /// boss room would put one in front of a player who has not yet met an ordinary fight.
    /// </summary>
    [Test]
    public void TheEntranceIsNotABossRoom()
    {
        Assert.That(RunState.IsBossRoom(0), Is.False);
    }

    /// <summary>
    /// The one that caught the first attempt at this.
    ///
    /// A boss room paying three times the ordinary rate makes room six pay less than room five,
    /// which makes banking correct immediately after every boss — and
    /// <c>PressingOnAlwaysPaysMoreThanTheRoomBefore</c> exists because a prize that does not
    /// outgrow the pot turns "one more room?" into a question with a known answer.
    ///
    /// So a boss pays by dropping stones, not by bending the curve. This asserts the curve is
    /// still untouched at and around a boss room.
    /// </summary>
    [Test]
    public void ABossRoomDoesNotBendThePayoutCurve()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RunState.PayoutFor(4, 1), Is.EqualTo(4));
            Assert.That(RunState.PayoutFor(5, 1), Is.EqualTo(5), "the boss room pays its room rate");
            Assert.That(RunState.PayoutFor(6, 1), Is.EqualTo(6), "and the room after it pays more");
        });
    }

    /// <summary>The run knows what is behind the next door, so the panel can say so.</summary>
    [Test]
    public void ARunCanSeeABossComing()
    {
        var run = RunState.Begin(seed: 7, tier: 1, rooms: 12);

        for (var room = 1; room <= 4; room++)
        {
            Assert.That(run.NextRoomHasABoss, Is.EqualTo(room == 5),
                $"before clearing room {room}");
            run.EnterRoom();
            run.ClearRoom();
        }

        Assert.That(run.NextRoomHasABoss, Is.True, "the fifth door has a boss behind it");
    }

    /// <summary>
    /// A boss drops into the pot, which raises the stake against the next room rather than the
    /// prize for it. That is the whole reason the drop is additive.
    /// </summary>
    [Test]
    public void WhatABossDropsGoesIntoThePotAndRaisesTheStake()
    {
        var run = RunState.Begin(seed: 7, tier: 1, rooms: 12);
        run.EnterRoom();
        run.ClearRoom();

        var before = run.RiskRatio;
        run.Collect(RunState.BossStones);

        Assert.Multiple(() =>
        {
            Assert.That(run.Pending, Is.EqualTo(1 + RunState.BossStones));
            Assert.That(run.RiskRatio, Is.GreaterThan(before),
                "a fatter pot against the same prize is a tenser door");
        });
    }

    /// <summary>
    /// A levelled boss is still that boss.
    ///
    /// **This is the test that would have caught the worst bug in the iteration.** AtLevel
    /// copies EnemyArchetype field by field, and Behaviour was not on the list, so every boss
    /// -- all of them spawned through AtLevel -- came back as Behaviour.None. The Harrier never
    /// withdrew, the drop never fired, and all three fought identically. Nothing failed:
    /// every other test here calls EnemyCatalog.Find directly, where the level is one, and
    /// AtLevel returns the archetype untouched.
    ///
    /// Walked by reflection rather than by naming the properties, so the next field added to
    /// EnemyArchetype cannot be dropped in silence the way that one was. A test that lists the
    /// fields it checks is a test that ages exactly as badly as the code it is checking.
    /// </summary>
    [Test]
    public void AtLevelPreservesEverythingItDoesNotScale()
    {
        // The four the method exists to change, and the display name a title may replace.
        var scaled = new[] { "MaxHealth", "AttackDamage", "XpReward", "Level", "DisplayName" };

        foreach (var id in EnemyCatalog.Ids)
        {
            var one = EnemyCatalog.Find(id)!;
            var five = one.AtLevel(5);

            foreach (var property in typeof(EnemyArchetype).GetProperties())
            {
                if (!property.CanRead || scaled.Contains(property.Name)) continue;

                Assert.That(property.GetValue(five), Is.EqualTo(property.GetValue(one)),
                    $"{id}: AtLevel dropped '{property.Name}'");
            }
        }
    }

    /// <summary>The same fault stated in the terms that matter: a levelled boss is still a boss.</summary>
    [Test]
    public void ALevelledBossKeepsItsBehaviour()
    {
        foreach (var id in EnemyCatalog.BossIds)
        {
            var boss = EnemyCatalog.Find(id)!;

            Assert.Multiple(() =>
            {
                Assert.That(boss.AtLevel(4).Behaviour, Is.EqualTo(boss.Behaviour), id);
                Assert.That(boss.AtLevel(4).IsBoss, Is.True, id);
            });
        }
    }

    // ------------------------------------------------------------------ the three behaviours

    [Test]
    public void ThereAreThreeBossesAndThreeBehaviours()
    {
        var bosses = EnemyCatalog.BossIds
            .Select(id => EnemyCatalog.Find(id))
            .ToList();

        Assert.That(bosses, Has.All.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(bosses.Select(b => b!.Behaviour).Distinct().Count(), Is.EqualTo(3),
                "three bosses at three difficulties is one behaviour, not three");
            Assert.That(bosses, Has.All.Matches<EnemyArchetype>(b => b.IsBoss));
        });
    }

    /// <summary>
    /// Nothing that fills a room is a boss. If an ordinary enemy answered true here, every
    /// room would be a boss room and the milestone would mean nothing.
    /// </summary>
    [Test]
    public void OrdinaryEnemiesAreNotBosses()
    {
        foreach (var id in new[]
                 {
                     EnemyCatalog.BanditId, EnemyCatalog.ArcherId, EnemyCatalog.ChhayaId,
                     EnemyCatalog.VetalaId, EnemyCatalog.PishachaId
                 })
        {
            Assert.That(EnemyCatalog.Find(id)!.IsBoss, Is.False, id);
        }
    }

    /// <summary>
    /// A Harrier gives ground between blows; nothing else in the game does.
    ///
    /// This is the whole of what makes it a third behaviour rather than a fast Breaker, so it
    /// is worth asserting rather than trusting to the numbers.
    /// </summary>
    [Test]
    public void AHarrierWithdrawsBetweenBlowsAndABreakerDoesNot()
    {
        var player = new WorldPoint(0f, 0f, 0f);

        var harrier = new Enemy(EnemyCatalog.Find(EnemyCatalog.ChhalaId)!, "harrier")
            { Position = new WorldPoint(1.5f, 0f, 0f) };
        var breaker = new Enemy(EnemyCatalog.Find(EnemyCatalog.KhandaId)!, "breaker")
            { Position = new WorldPoint(1.5f, 0f, 0f) };

        // Both in reach, both having just swung.
        harrier.Attack();
        breaker.Attack();

        Assert.Multiple(() =>
        {
            Assert.That(harrier.Decide(player), Is.EqualTo(EnemyIntent.Withdraw),
                "a harrier is already leaving by the time the player answers");
            Assert.That(breaker.Decide(player), Is.EqualTo(EnemyIntent.Idle),
                "a breaker holds its ground and waits out its own cooldown");
        });
    }

    /// <summary>
    /// The withdrawal ends before the cooldown does, or the fight becomes a chase with no
    /// fight in it — the harrier would never be in reach when it was ready again.
    /// </summary>
    [Test]
    public void AHarrierComesBackBeforeItIsReadyToSwing()
    {
        var player = new WorldPoint(0f, 0f, 0f);
        var harrier = new Enemy(EnemyCatalog.Find(EnemyCatalog.ChhalaId)!, "harrier")
            { Position = new WorldPoint(1.5f, 0f, 0f) };

        harrier.Attack();
        harrier.Tick(harrier.Archetype.AttackCooldown * 0.75f);

        Assert.That(harrier.Decide(player), Is.EqualTo(EnemyIntent.Idle),
            "past the withdrawal it closes again rather than running the whole cooldown out");
    }

    /// <summary>
    /// A Warden will not come to the player: it keeps its distance and makes the approach the
    /// fight. That is the mirror of the Breaker, and it only works if it actually shoots.
    /// </summary>
    [Test]
    public void AWardenFightsAtRange()
    {
        var warden = EnemyCatalog.Find(EnemyCatalog.NetraId)!;

        Assert.Multiple(() =>
        {
            Assert.That(warden.IsRanged, Is.True);
            Assert.That(warden.StandOffRange, Is.GreaterThan(0f));
            Assert.That(warden.AttackRange, Is.GreaterThan(warden.StandOffRange),
                "it must be able to hit from further than it tries to stand");
        });
    }

    /// <summary>
    /// Which boss is behind a given door is a function of the mine's seed, like the cave theme,
    /// so the shaft screen, the generator and a replay of the same seed cannot disagree.
    /// </summary>
    [Test]
    public void WhichBossIsWaitingIsDecidedBySeed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EnemyCatalog.BossFor(4242, 5), Is.EqualTo(EnemyCatalog.BossFor(4242, 5)));
            Assert.That(EnemyCatalog.BossIds, Does.Contain(EnemyCatalog.BossFor(-1, 10)));
            Assert.That(EnemyCatalog.BossIds, Does.Contain(EnemyCatalog.BossFor(int.MaxValue, 5)));
        });
    }

    /// <summary>
    /// All three should appear across a spread of seeds. A mixing function that collapsed onto
    /// one boss would pass every test above and still ship one fight behaviour.
    /// </summary>
    [Test]
    public void AllThreeBossesActuallyOccur()
    {
        var seen = Enumerable.Range(0, 400)
            .Select(seed => EnemyCatalog.BossFor(seed * 7919, 5))
            .Distinct()
            .ToList();

        Assert.That(seen, Has.Count.EqualTo(3), "one of the three never appears");
    }
}
