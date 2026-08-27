using RatnaBay.Domain;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The second name every key element carries.
///
/// The naming doctrine in NAMES_AND_OFFICES.md spends ease-of-pronunciation on the title and
/// the key elements and lets character names be as heavy as they like. These assertions hold
/// the *key elements* side of that bargain: the English name stays the one a player decides
/// with, and the Indic name rides alongside it so the province has its own word.
///
/// The failure they exist to catch is a new stone, spell or cave shipped with the English name
/// filled in and the other left blank — which nothing at runtime would ever complain about.
/// </summary>
[TestFixture]
public sealed class IndicNameTests
{
    private static IEnumerable<string> AllIndicNames() =>
        StoneCatalog.All.Select(s => s.Indic)
            .Concat(SpellCatalog.All.Select(s => s.IndicName))
            .Concat(CaveThemeCatalog.All.Select(c => c.Indic));

    [Test]
    public void EveryStoneCarriesBothNames()
    {
        foreach (var stone in StoneCatalog.All)
        {
            Assert.That(stone.Indic, Is.Not.Empty, stone.Id);
            Assert.That(stone.FullName, Does.Contain(stone.DisplayName).And.Contain(stone.Indic));
        }
    }

    [Test]
    public void EverySpellCarriesBothNames()
    {
        foreach (var spell in SpellCatalog.All)
            Assert.That(spell.IndicName, Is.Not.Empty, spell.Id);
    }

    [Test]
    public void EveryCaveCarriesBothNames()
    {
        foreach (var theme in CaveThemeCatalog.All)
            Assert.That(theme.Indic, Is.Not.Empty, theme.Id);
    }

    [Test]
    public void NoTwoKeyElementsShareASecondName()
    {
        // Two things called the same thing is worse than one of them having no second name at
        // all: the player overhears a word in the fort and goes looking for the wrong object.
        Assert.That(AllIndicNames().ToList(), Is.Unique);
    }

    [Test]
    public void TheSecondNameIsNeverJustTheFirstOne()
    {
        foreach (var stone in StoneCatalog.All)
            Assert.That(stone.Indic, Is.Not.EqualTo(stone.DisplayName), stone.Id);

        foreach (var spell in SpellCatalog.All)
            Assert.That(spell.IndicName, Is.Not.EqualTo(spell.DisplayName), spell.Id);
    }

    [Test]
    public void SecondNamesStayShortEnoughToSay()
    {
        // The whole point of the doctrine: these are key elements, so they are read aloud and
        // searched for. A name a player cannot repeat is one they cannot ask anybody about.
        foreach (var name in AllIndicNames())
            Assert.That(name.Length, Is.LessThanOrEqualTo(9), name);
    }
}
