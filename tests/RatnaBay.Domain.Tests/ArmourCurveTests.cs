using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What a suit of armour and a raised shield are actually worth.
///
/// Written after the best kit in the game turned out to be an off switch. Armour was
/// subtracted after the block multiplier, so the two compounded: eight points absorbed eight
/// damage with the shield down and thirty-six with it up. A blocked blow needed 41 raw damage
/// to beat MinimumDamage, and only two of the eight enemy archetypes reach that at any level,
/// so hauberk and bronze shield meant one damage per hit from nearly everything — twenty-four
/// hits to die against the worst enemy at level ten, where bare hands take four.
///
/// Nothing failed when the order was corrected. Changing the formula every incoming blow in
/// the game goes through broke no test at all, which is the gap these close.
///
/// They are deliberately relations and bounds rather than damage figures. Pinning "a hauberk
/// takes exactly 5.1" would make every future rebalance look like a regression, which is the
/// coupling this suite has already been cleaned of once.
/// </summary>
public class ArmourCurveTests
{
    private const float BossBlow = 55f;      // the heaviest archetype, around level ten
    private const float BanditBlow = 11f;    // an ordinary body at the same depth

    private static float Blocked(string? armourId, string? shieldId, float amount)
    {
        var armour = (EquipmentCatalog.GetArmour(armourId)?.Armour ?? 0f)
                     + (EquipmentCatalog.GetShield(shieldId)?.Armour ?? 0f);
        var factor = EquipmentCatalog.GetShield(shieldId)?.BlockFactor ?? DamageMath.BlockReduction;

        return DamageMath.Resolve(amount, armour, factor);
    }

    private static float Open(string? armourId, string? shieldId, float amount)
    {
        var armour = (EquipmentCatalog.GetArmour(armourId)?.Armour ?? 0f)
                     + (EquipmentCatalog.GetShield(shieldId)?.Armour ?? 0f);

        return DamageMath.Resolve(amount, armour, 1f);
    }

    /// <summary>
    /// Gear has to matter, and the ladder has to point the right way. This is the half that
    /// stops a fix for the exploit from quietly making armour pointless.
    /// </summary>
    [Test]
    public void BetterGearAlwaysTakesLessThanWorseGear()
    {
        var none = Blocked(null, null, BossBlow);
        var first = Blocked("padded_jerkin", "wicker_shield", BossBlow);
        var best = Blocked("mail_hauberk", "bronze_shield", BossBlow);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.LessThan(none), "tier-one gear has to beat no gear");
            Assert.That(best, Is.LessThan(first), "and the best kit has to beat tier one");
        });
    }

    /// <summary>
    /// The bound that was missing. MinimumDamage is an absolute floor, and an absolute floor
    /// cannot limit a proportion: the old kit took 3% of a blow and called it armour.
    ///
    /// A fifth of a boss's blow is not a generous allowance — it is five times the mitigation
    /// bare hands get — but it is a number, which is what "never invulnerability" needs to be
    /// if it is going to mean anything.
    /// </summary>
    [Test]
    public void NoKitReducesABossBlowToAlmostNothing()
    {
        var best = Blocked("mail_hauberk", "bronze_shield", BossBlow);

        Assert.That(best, Is.GreaterThan(BossBlow * 0.1f),
            "the best armour in the game still has to let a tenth of a boss's blow through");
    }

    /// <summary>
    /// Survivability stated the way a player experiences it, and the way the fault showed up:
    /// how many blows the run lasts.
    ///
    /// Twenty-four was the number that made this worth changing. Bounded above so the kit
    /// cannot become an off switch again, and below so it is still plainly worth wearing.
    /// </summary>
    [Test]
    public void TheBestKitBuysAHandfulOfBlowsRatherThanAnAfternoon()
    {
        var bare = 100f / Blocked(null, null, BossBlow);
        var best = 100f / Blocked("mail_hauberk", "bronze_shield", BossBlow);

        Assert.Multiple(() =>
        {
            Assert.That(best, Is.GreaterThan(bare * 1.5f),
                "a full suit and a shield have to be worth wearing");
            Assert.That(best, Is.LessThan(bare * 4f),
                "but not four times the survivability of a man with his hands up");
        });
    }

    /// <summary>
    /// The specific bug, stated as the number that actually moved.
    ///
    /// In absolute terms armour subtracts its own value under either order, so "a hauberk
    /// cannot stop more than a hauberk" is true of the broken formula too — that framing does
    /// not distinguish them, and a test that cannot fail is not a test.
    ///
    /// What compounded was armour's worth *in raw damage*. Subtracted after a x0.22 multiplier,
    /// eight points removed eight from an already-quartered blow, which is thirty-six points of
    /// the blow that was actually thrown. So the raw damage needed to hurt at all through the
    /// best kit was 41 — past every ordinary enemy in the game at every level. That threshold
    /// is the fault, and it is what this pins.
    /// </summary>
    [Test]
    public void AnOrdinaryEnemyCanStillHurtThroughTheBestArmourInTheGame()
    {
        var threshold = 0f;
        for (var raw = 0f; raw <= 200f; raw += 0.1f)
        {
            if (Blocked("mail_hauberk", "bronze_shield", raw) <= DamageMath.MinimumDamage) continue;
            threshold = raw;
            break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(threshold, Is.GreaterThan(0f), "something has to get through eventually");
            Assert.That(threshold, Is.LessThan(BanditBlow * 2f),
                "a body swinging at depth has to be able to hurt a man in a hauberk");
        });
    }

    /// <summary>
    /// The property that made this change safe to make at all: with no guard up, the two
    /// orders are the same expression, so nothing about taking a hit on the chin moved.
    /// </summary>
    [Test]
    public void AnUnguardedBlowIsUnaffectedByAnyOfThis()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Open("mail_hauberk", "bronze_shield", BossBlow),
                Is.EqualTo(BossBlow - 8f).Within(0.001f), "hauberk five, shield three");
            Assert.That(Open("padded_jerkin", null, BanditBlow),
                Is.EqualTo(BanditBlow - 2f).Within(0.001f));
        });
    }

    /// <summary>
    /// And the floor still exists, because a fight nobody can lose and a fight nobody can win
    /// are the same broken fight from opposite ends.
    /// </summary>
    [Test]
    public void SomethingAlwaysGetsThrough()
    {
        Assert.That(Blocked("mail_hauberk", "bronze_shield", 1f),
            Is.EqualTo(DamageMath.MinimumDamage));
    }
}
