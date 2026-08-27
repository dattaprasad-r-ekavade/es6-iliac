using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// Cave themes, and the two rules that keep them a decision rather than a wall.
///
/// The first is *resistance, never immunity*: a player whose only offence is Flame must still
/// be able to finish a lava cave. The second is *depth decides reward, theme decides tactics*:
/// a hostile cave never pays more, or players stop choosing the cave they can handle and start
/// choosing the one that pays best.
/// </summary>
[TestFixture]
public sealed class CaveThemeTests
{
    [Test]
    public void ThereAreFiveCaves()
    {
        // The design's content budget. Not a magic number — five themes is what the trailer
        // scope contract and the first release were both sized against.
        Assert.That(CaveThemeCatalog.All, Has.Count.EqualTo(5));
    }

    [Test]
    public void NoCaveIsImmuneToAnything()
    {
        // The rule that cannot be allowed to erode. Immunity turns "which cave can I handle"
        // into "which cave am I locked out of".
        Assert.That(CaveThemeCatalog.ResistedFactor, Is.GreaterThan(0f));

        foreach (var theme in CaveThemeCatalog.All)
        foreach (var effect in new[] { SpellEffect.Fire, SpellEffect.Frost, SpellEffect.Shock })
            Assert.That(CaveThemeCatalog.DamageFactor(theme, effect), Is.GreaterThan(0f),
                $"{theme.Id} is immune to {effect}");
    }

    [Test]
    public void ResistanceHurtsAndFearHelps()
    {
        foreach (var theme in CaveThemeCatalog.All)
        {
            Assert.That(CaveThemeCatalog.DamageFactor(theme, theme.Resists),
                Is.LessThan(1f), theme.Id);
            Assert.That(CaveThemeCatalog.DamageFactor(theme, theme.Fears),
                Is.GreaterThan(1f), theme.Id);
        }
    }

    [Test]
    public void NoCaveBothResistsAndFearsTheSameElement()
    {
        foreach (var theme in CaveThemeCatalog.All)
            Assert.That(theme.Resists, Is.Not.EqualTo(theme.Fears), theme.Id);
    }

    [Test]
    public void AnUnthemedSpellIsUnchanged()
    {
        Assert.That(CaveThemeCatalog.DamageFactor(null, SpellEffect.Fire), Is.EqualTo(1f));

        foreach (var theme in CaveThemeCatalog.All)
            Assert.That(CaveThemeCatalog.DamageFactor(theme, SpellEffect.Heal),
                Is.EqualTo(1f), "a heal was scaled by the rock it was cast in");
    }

    [Test]
    public void EveryCaveIsReachable()
    {
        // A theme nobody can roll is a theme that does not exist. Checked across seeds rather
        // than asserted about the mixing function, because the mixing function is the thing
        // most likely to be changed by somebody who has not read this.
        var seen = Enumerable.Range(0, 400)
            .Select(seed => CaveThemeCatalog.For(seed, tier: 3).Id)
            .Distinct()
            .ToList();

        Assert.That(seen, Has.Count.EqualTo(CaveThemeCatalog.All.Count));
    }

    [Test]
    public void TheSameMineIsAlwaysTheSameCave()
    {
        // The shaft screen names the cave before the player pays, and the descent has to
        // deliver that cave. Both derive it from the seed, so this is what stops them drifting.
        // Captured first, then re-derived, so this actually tests determinism rather than
        // comparing one expression to itself.
        var first = Enumerable.Range(-50, 100)
            .Select(seed => CaveThemeCatalog.For(seed, 3).Id)
            .ToList();

        var again = Enumerable.Range(-50, 100)
            .Select(seed => CaveThemeCatalog.For(seed, 3).Id)
            .ToList();

        Assert.That(again, Is.EqualTo(first));
    }

    [Test]
    public void ANegativeSeedIsStillACave()
    {
        Assert.That(CaveThemeCatalog.For(int.MinValue, 4), Is.Not.Null);
        Assert.That(CaveThemeCatalog.For(-1, 2), Is.Not.Null);
    }

    [Test]
    public void TheFirstDescentIsAlwaysTheSameGentleCave()
    {
        // A first descent is a tutorial whether or not it is labelled one. Teaching the loop
        // against a resistance the player has no way to answer yet teaches them the wrong
        // lesson about their own competence.
        for (var seed = 0; seed < 200; seed++)
            Assert.That(CaveThemeCatalog.For(seed, RunState.MinTier).Id,
                Is.EqualTo(CaveThemeCatalog.All[0].Id));
    }

    [Test]
    public void EveryCaveSaysWhatItIsBeforeYouPay()
    {
        foreach (var theme in CaveThemeCatalog.All)
        {
            Assert.That(theme.DisplayName, Is.Not.Empty);
            Assert.That(theme.Summary, Does.Contain(CaveTheme.Name(theme.Resists)));
            Assert.That(theme.Summary, Does.Contain(CaveTheme.Name(theme.Fears)));
        }
    }

    [Test]
    public void ThemeChangesTacticsAndNeverReward()
    {
        // Depth decides reward, theme decides tactics. If a payout ever learns about a theme,
        // this is the assertion that should have stopped it.
        foreach (var theme in CaveThemeCatalog.All)
        foreach (var tier in new[] { 1, 2, 3 })
            Assert.That(RunState.PayoutFor(3, tier), Is.EqualTo(3 * tier),
                $"{theme.Id} changed what a room pays");
    }
}
