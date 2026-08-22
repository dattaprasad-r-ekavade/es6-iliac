using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class SpellCatalogTests
{
    [Test]
    public void AllFiveSpellsResolve()
    {
        string[] ids =
        {
            SpellCatalog.FireId, SpellCatalog.FrostId, SpellCatalog.ShockId,
            SpellCatalog.HealId, SpellCatalog.LightId
        };

        Assert.Multiple(() =>
        {
            Assert.That(SpellCatalog.All, Has.Count.EqualTo(5));
            foreach (var id in ids)
                Assert.That(SpellCatalog.Get(id), Is.Not.Null, $"{id} is missing");
        });
    }

    [Test]
    public void AnUnknownSpellReturnsNullRatherThanThrowing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellCatalog.Get("spell.nonexistent"), Is.Null);
            Assert.That(SpellCatalog.Get(null), Is.Null);
            Assert.That(SpellCatalog.Get(""), Is.Null);
            Assert.That(SpellCatalog.Exists("spell.nonexistent"), Is.False);
        });
    }

    [Test]
    public void EveryIdMatchesTheKeyItIsStoredUnder()
    {
        foreach (var spell in SpellCatalog.All)
            Assert.That(SpellCatalog.Get(spell.Id), Is.SameAs(spell));
    }

    [Test]
    public void EverySpellCostsSomething()
    {
        // Light is the cheap one, but a light in a crystal-lit world is never free.
        Assert.That(SpellCatalog.All.Select(s => s.BaseCost), Is.All.GreaterThan(0f));
    }

    [Test]
    public void EachSchoolTrainsItsOwnSkill()
    {
        foreach (var spell in SpellCatalog.All)
        {
            var expected = spell.School == SpellSchool.Destruction
                ? Skills.Destruction
                : Skills.Restoration;
            Assert.That(spell.SkillId, Is.EqualTo(expected));
        }
    }

    [Test]
    public void EverySpellTrainsADeclaredSkill()
    {
        foreach (var spell in SpellCatalog.All)
            Assert.That(Skills.Exists(spell.SkillId), Is.True);
    }

    [Test]
    public void TheThreeDestructionSpellsDoMechanicallyDifferentThings()
    {
        var effects = SpellCatalog.All
            .Where(s => s.School == SpellSchool.Destruction)
            .Select(s => s.Effect)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(effects, Has.Count.EqualTo(3));
            Assert.That(effects.Distinct().ToList(), Has.Count.EqualTo(3),
                "elements must differ by behaviour, not just by colour");
        });
    }

    [Test]
    public void OffensiveSpellsHaveReachAndDamage()
    {
        foreach (var spell in SpellCatalog.All.Where(s => s.School == SpellSchool.Destruction))
            Assert.Multiple(() =>
            {
                Assert.That(spell.Range, Is.GreaterThan(0f), $"{spell.Id} has no range");
                Assert.That(spell.Power, Is.GreaterThan(0f), $"{spell.Id} does no damage");
            });
    }

    [Test]
    public void SelfTargetedSpellsHaveNoRange()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellCatalog.Get(SpellCatalog.HealId)!.Range, Is.Zero);
            Assert.That(SpellCatalog.Get(SpellCatalog.LightId)!.Range, Is.Zero);
        });
    }

    [Test]
    public void ArcInterruptsBrieflyWhileBurnAndSlowLinger()
    {
        var shock = SpellCatalog.Get(SpellCatalog.ShockId)!;
        var fire = SpellCatalog.Get(SpellCatalog.FireId)!;
        var frost = SpellCatalog.Get(SpellCatalog.FrostId)!;

        Assert.Multiple(() =>
        {
            Assert.That(shock.Duration, Is.LessThan(fire.Duration));
            Assert.That(shock.Duration, Is.LessThan(frost.Duration));
        });
    }

    [Test]
    public void MendIsInstantAndEmberlightIsNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SpellCatalog.Get(SpellCatalog.HealId)!.Duration, Is.Zero);
            Assert.That(SpellCatalog.Get(SpellCatalog.LightId)!.Duration, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void ALesserStoneAffordsAtLeastOneCastOfEverySpell()
    {
        Assert.That(SpellCatalog.All.Select(s => s.BaseCost),
            Is.All.LessThanOrEqualTo(SoulCrystals.LesserCharge));
    }

    [Test]
    public void MasteryMultipliesCastsPerStoneByThreeToFour()
    {
        // The discount floor is the whole magic economy: at mastery a spell costs 30% of
        // base, so one stone must go 3-4x further than it does for a dabbler.
        var casts = 1f / SoulCrystals.MinCostMultiplier;
        Assert.That(casts, Is.InRange(3f, 4f));
    }

    [Test]
    public void TheDiscountFloorIsADiscountAndNotAPenalty()
    {
        Assert.That(SoulCrystals.MinCostMultiplier, Is.InRange(0f, 1f).And.GreaterThan(0f));
    }
}
