using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class SkillsTests
{
    [Test]
    public void ThereAreExactlyEightSkills()
    {
        Assert.That(Skills.All, Has.Count.EqualTo(8));
    }

    [Test]
    public void SkillIdsAreUnique()
    {
        Assert.That(Skills.All.Distinct().ToList(), Has.Count.EqualTo(Skills.All.Count));
    }

    [Test]
    public void EverySkillIdIsNamespacedForSaveStability()
    {
        Assert.That(Skills.All, Is.All.StartWith("skill."));
    }

    [Test]
    public void EverySkillHasADisplayLabel()
    {
        foreach (var id in Skills.All)
            Assert.That(Skills.Label(id), Is.Not.EqualTo(id), $"{id} has no label");
    }

    [Test]
    public void AnUnknownSkillDegradesToItsIdRatherThanThrowing()
    {
        Assert.That(Skills.Label("skill.nonexistent"), Is.EqualTo("skill.nonexistent"));
    }

    [TestCase("route.warrior", Skills.Blade, Skills.Block)]
    [TestCase("route.mage", Skills.Destruction, Skills.Restoration)]
    [TestCase("route.trade", Skills.Stealth, Skills.Security)]
    public void EachRouteGrantsItsTwoSkills(string routeId, string first, string second)
    {
        Assert.That(Skills.GrantedBy(routeId), Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void RefusingTheRouteGrantsNothing()
    {
        Assert.That(Skills.GrantedBy("route.refuse"), Is.Empty);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("route.unknown")]
    public void AnUnrecognisedRouteGrantsNothing(string? routeId)
    {
        Assert.That(Skills.GrantedBy(routeId), Is.Empty);
    }

    [Test]
    public void NoSkillIsGrantedByTwoRoutes()
    {
        string[] routes = { "route.warrior", "route.mage", "route.trade" };
        var granted = routes.SelectMany(Skills.GrantedBy).ToList();
        Assert.That(granted.Distinct().ToList(), Has.Count.EqualTo(granted.Count));
    }

    [Test]
    public void EveryGrantedSkillIsARealSkill()
    {
        string[] routes = { "route.warrior", "route.mage", "route.trade" };
        foreach (var id in routes.SelectMany(Skills.GrantedBy))
            Assert.That(Skills.Exists(id), Is.True, $"{id} is granted but not a declared skill");
    }
}
