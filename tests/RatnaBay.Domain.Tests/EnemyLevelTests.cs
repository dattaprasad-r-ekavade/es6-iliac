using RatnaBay.Domain;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What level a body is, given where it is standing.
///
/// The old rule gave every enemy in a room one number, so a room was a squad of clones. These
/// check the three properties the new one exists for: a room is a group of individuals, depth
/// and tier both raise the band, and the bands between tiers overlap.
/// </summary>
[TestFixture]
public sealed class EnemyLevelTests
{
    private static List<int> RollMany(int tier, int room, bool elite = false, int count = 400)
    {
        var random = new Prng(20789);

        return Enumerable.Range(0, count)
            .Select(_ => EnemyLevels.Roll(tier, room, elite, random))
            .ToList();
    }

    [Test]
    public void TheFirstRoomOfTheCheapestCaveIsLevelOneish()
    {
        var (low, high) = EnemyLevels.Band(tier: 1, roomIndex: 0);

        Assert.That(low, Is.EqualTo(EnemyLevels.MinLevel));
        Assert.That(high, Is.EqualTo(2), "a shallow room should not be rolling level fours");
    }

    [Test]
    public void PressingOnRaisesTheBand()
    {
        var shallow = EnemyLevels.Centre(tier: 1, roomIndex: 0);
        var deep = EnemyLevels.Centre(tier: 1, roomIndex: 12);

        Assert.That(deep, Is.GreaterThan(shallow),
            "the room after the door has to be worse than the one behind it");
    }

    [Test]
    public void BuyingADeeperCaveRaisesTheBand()
    {
        var cheap = EnemyLevels.Centre(tier: 1, roomIndex: 0);
        var dear = EnemyLevels.Centre(tier: 5, roomIndex: 0);

        Assert.That(dear, Is.GreaterThan(cheap));
    }

    /// <summary>
    /// The property that keeps the shaft a decision rather than a shopping list.
    ///
    /// If tier five always outranked tier one, buying depth would be a strict upgrade and the
    /// only question at the shaft would be "can I afford it". Overlapping bands mean a player
    /// who presses on in a cheap cave reaches the same fights as one who paid to start there.
    /// </summary>
    [Test]
    public void ADeepCheapRoomReachesTheSameLevelsAsAShallowExpensiveOne()
    {
        var deepAndCheap = EnemyLevels.Centre(tier: 1, roomIndex: 15);
        var shallowAndDear = EnemyLevels.Centre(tier: 3, roomIndex: 0);

        Assert.That(deepAndCheap, Is.GreaterThanOrEqualTo(shallowAndDear),
            "pressing on has to be able to out-earn paying at the shaft");
    }

    [Test]
    public void ARoomIsAGroupOfIndividualsRatherThanClones()
    {
        var levels = RollMany(tier: 3, room: 9);

        Assert.That(levels.Distinct().Count(), Is.GreaterThan(1),
            "every enemy in a room used to share one number");
    }

    [Test]
    public void OrdinaryBodiesStayInsideTheirBandAlmostAlways()
    {
        var (low, high) = EnemyLevels.Band(tier: 2, roomIndex: 6);
        var levels = RollMany(tier: 2, room: 6);

        Assert.That(levels.Min(), Is.GreaterThanOrEqualTo(low));

        // The standout is the only thing allowed above the band, and it is rare by design.
        var above = levels.Count(level => level > high);
        Assert.That(above, Is.LessThan(levels.Count / 4),
            "a standout every second body is not a standout");
        Assert.That(above, Is.GreaterThan(0), "and it should still happen");
    }

    [Test]
    public void ALeaderOutranksTheRoomItLeads()
    {
        var rank = RollMany(tier: 3, room: 7).Average();
        var leader = RollMany(tier: 3, room: 7, elite: true).Average();

        Assert.That(leader, Is.GreaterThan(rank));
    }

    [Test]
    public void NothingIsEverBelowLevelOne()
    {
        Assert.That(RollMany(tier: 1, room: 0).Min(), Is.GreaterThanOrEqualTo(1));
        Assert.That(EnemyLevels.Centre(tier: 0, roomIndex: -5), Is.GreaterThanOrEqualTo(1),
            "nonsense in must not produce a level zero enemy");
    }

    /// <summary>
    /// The reason depth is the lever worth pulling: health climbs faster than damage.
    ///
    /// Recorded fights last about two seconds, which is not long enough for blocking, stagger
    /// or a chill to ever be seen. Levelling stretches a fight more than it sharpens it, so
    /// the tactical verbs get room to appear as the mine goes on.
    /// </summary>
    [Test]
    public void LevellingStretchesAFightMoreThanItSharpensIt()
    {
        Assert.That(EnemyArchetype.HealthPerLevel,
            Is.GreaterThan(EnemyArchetype.DamagePerLevel));

        var bandit = EnemyCatalog.Find(EnemyCatalog.BanditId)!;
        var deep = bandit.AtLevel(8);

        var healthRatio = deep.MaxHealth / bandit.MaxHealth;
        var damageRatio = deep.AttackDamage / bandit.AttackDamage;

        Assert.That(healthRatio, Is.GreaterThan(damageRatio),
            "a level-eight bandit should take longer to kill than it is deadlier");
    }

    /// <summary>
    /// The scaling curve's own promise, which nothing was checking.
    ///
    /// Enemy.cs argues at length that health must climb faster than damage, because what a
    /// fight costs is the product of the two: at DamagePerLevel 0.16 the curve was quadratic
    /// and a level-ten bandit took 75 health off a player who gains 6 a level. Depth is
    /// supposed to lengthen a fight, not end it.
    ///
    /// Doubling either constant broke no test before this. Doubling DamagePerLevel is exactly
    /// the regression the comment describes, and it went unnoticed.
    /// </summary>
    [Test]
    public void ADeeperEnemyGetsTougherFasterThanItGetsDeadlier()
    {
        Assert.That(EnemyArchetype.HealthPerLevel,
            Is.GreaterThan(EnemyArchetype.DamagePerLevel * 2f),
            "health has to outrun damage, or depth stops lengthening fights and starts ending them");
    }

    /// <summary>
    /// The same claim in the currency it is actually about: what ten levels cost a player.
    ///
    /// A level-ten enemy should take substantially longer to kill while hitting only somewhat
    /// harder. Bounded on both sides -- an enemy that never gets deadlier makes depth
    /// pointless, and one that gets much deadlier makes it unwinnable.
    /// </summary>
    [Test]
    public void TenLevelsLengthenAFightMoreThanTheyRaiseItsPrice()
    {
        var bandit = EnemyCatalog.Find("bandit")!;
        var deep = bandit.AtLevel(10);

        var tougher = deep.MaxHealth / bandit.MaxHealth;
        var deadlier = deep.AttackDamage / bandit.AttackDamage;

        Assert.Multiple(() =>
        {
            Assert.That(deadlier, Is.GreaterThan(1f), "depth has to mean something");
            Assert.That(tougher, Is.GreaterThan(deadlier),
                "ten levels must add more health than damage");
            Assert.That(deadlier, Is.LessThan(2f),
                "a level-ten bandit hitting twice as hard is the curve that had to be halved once already");
        });
    }
}
