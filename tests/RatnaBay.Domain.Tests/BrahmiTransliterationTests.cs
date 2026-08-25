using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The carved script, asserted rather than eyeballed.
///
/// A wrong codepoint here does not crash and does not fail a build. It puts a different letter
/// on a pillar in a language almost nobody reading it can check, and it stays there. So the
/// verses that actually get carved are spelled out below, syllable by syllable, against the
/// Unicode names.
/// </summary>
[TestFixture]
public sealed class BrahmiTransliterationTests
{
    private const string SurfaceVerse = "मा गृधः कस्य स्विद्धनम्";

    /// <summary>Codepoints of a string, so a surrogate pair counts as the one letter it is.</summary>
    private static int[] Codepoints(string text)
    {
        var result = new System.Collections.Generic.List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                result.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
                continue;
            }
            result.Add(text[i]);
        }
        return result.ToArray();
    }

    [Test]
    public void TheSurfaceVerseTransliteratesLetterForLetter()
    {
        var expected = new[]
        {
            0x1102B, 0x11038,                            // ma + aa
            ' ',
            0x11015, 0x1103E, 0x11025, 0x11002,          // ga + vocalic r, dha, visarga
            ' ',
            0x11013, 0x11032, 0x11046, 0x1102C,          // ka, sa + virama, ya
            ' ',
            0x11032, 0x11046, 0x1102F, 0x1103A,          // sa + virama, va + i
            0x11024, 0x11046, 0x11025, 0x11026,          // da + virama, dha, na
            0x1102B, 0x11046                             // ma + virama
        };

        Assert.That(Codepoints(BrahmiTransliteration.Transliterate(SurfaceVerse)),
            Is.EqualTo(expected));
    }

    [Test]
    public void EveryLetterLandsInTheBrahmiBlock()
    {
        var carved = Codepoints(BrahmiTransliteration.Transliterate(SurfaceVerse))
            .Where(c => c != ' ');

        Assert.That(carved, Is.All.InRange(0x11000, 0x1107F));
    }

    [Test]
    public void TheVerseSurvivesAsSurrogatePairs()
    {
        var carved = BrahmiTransliteration.Transliterate(SurfaceVerse);

        // Brahmi is above the Basic Multilingual Plane, so the string is longer in chars than
        // it is in letters. Anything that indexes it by char will be wrong, and this is the
        // assertion that says so out loud.
        Assert.That(carved.Length, Is.GreaterThan(Codepoints(carved).Length));
        Assert.That(char.IsHighSurrogate(carved[0]), Is.True);
    }

    [Test]
    public void SpacesAreKept()
    {
        Assert.That(BrahmiTransliteration.Transliterate(SurfaceVerse).Count(c => c == ' '),
            Is.EqualTo(3));
    }

    [Test]
    public void AVerseWithNoBrahmiEquivalentIsRefusedRatherThanCarvedWithHoles()
    {
        // Devanagari RRA has no Brahmi counterpart in the table. Half a verse on a pillar is
        // worse than the wrong script, so the answer is no rather than a gap.
        const string unmappable = "मा ऱ";

        Assert.That(BrahmiTransliteration.CanTransliterate(unmappable), Is.False);
        Assert.That(BrahmiTransliteration.Transliterate(unmappable), Is.EqualTo(unmappable));
    }

    [Test]
    public void TheSurfaceVerseIsCarvable()
    {
        Assert.That(BrahmiTransliteration.CanTransliterate(SurfaceVerse), Is.True);
    }

    [Test]
    public void NothingIsClaimedForAnEmptyVerse()
    {
        Assert.That(BrahmiTransliteration.CanTransliterate(""), Is.False);
        Assert.That(BrahmiTransliteration.CanTransliterate(null), Is.False);
        Assert.That(BrahmiTransliteration.Transliterate(null), Is.EqualTo(string.Empty));
    }
}
