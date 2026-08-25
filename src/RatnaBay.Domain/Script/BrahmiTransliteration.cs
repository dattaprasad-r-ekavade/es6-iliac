using System;
using System.Collections.Generic;
using System.Text;

namespace RatnaBay.Domain;

/// <summary>
/// Devanagari in, Brahmi out.
///
/// The verses are authored in Devanagari because that is the script a maintainer can read, and
/// carved in Brahmi because that is the script the setting actually had. Devanagari postdates a
/// Mauryan mine by roughly a thousand years; Brahmi is what the pillar edicts were cut in, and
/// Devanagari is its eventual descendant, which is why a table this small is enough.
///
/// It is a codepoint map and nothing cleverer. Brahmi is an abugida with the same inventory and
/// very nearly the same order as Devanagari, so consonants, independent vowels, vowel signs,
/// the virama and the dandas all correspond one to one. What it deliberately does not do is
/// reorder anything: the pre-base I-sign that Devanagari stores after its consonant stays where
/// it is, and where it ends up on screen is the font's business, not this table's.
///
/// Engine-free on purpose. It is a lookup over strings, so it is exactly the kind of thing that
/// should be asserted headlessly rather than eyeballed on a pillar.
/// </summary>
public static class BrahmiTransliteration
{
    /// <summary>
    /// Devanagari codepoint to Brahmi codepoint.
    ///
    /// The consonant runs look like they could be a fixed offset and cannot: Devanagari carries
    /// NNNA, RRA and LLLA inside its run at 0929, 0931 and 0934, and Brahmi does not have them
    /// in the corresponding places. An explicit table is a few more lines and cannot drift.
    /// </summary>
    private static readonly Dictionary<int, int> Map = new()
    {
        // Signs
        [0x0901] = 0x11000, // candrabindu
        [0x0902] = 0x11001, // anusvara
        [0x0903] = 0x11002, // visarga

        // Independent vowels
        [0x0905] = 0x11005, // a
        [0x0906] = 0x11006, // aa
        [0x0907] = 0x11007, // i
        [0x0908] = 0x11008, // ii
        [0x0909] = 0x11009, // u
        [0x090A] = 0x1100A, // uu
        [0x090B] = 0x1100B, // vocalic r
        [0x0960] = 0x1100C, // vocalic rr
        [0x090C] = 0x1100D, // vocalic l
        [0x0961] = 0x1100E, // vocalic ll
        [0x090F] = 0x1100F, // e
        [0x0910] = 0x11010, // ai
        [0x0913] = 0x11011, // o
        [0x0914] = 0x11012, // au

        // Consonants
        [0x0915] = 0x11013, // ka
        [0x0916] = 0x11014, // kha
        [0x0917] = 0x11015, // ga
        [0x0918] = 0x11016, // gha
        [0x0919] = 0x11017, // nga
        [0x091A] = 0x11018, // ca
        [0x091B] = 0x11019, // cha
        [0x091C] = 0x1101A, // ja
        [0x091D] = 0x1101B, // jha
        [0x091E] = 0x1101C, // nya
        [0x091F] = 0x1101D, // tta
        [0x0920] = 0x1101E, // ttha
        [0x0921] = 0x1101F, // dda
        [0x0922] = 0x11020, // ddha
        [0x0923] = 0x11021, // nna
        [0x0924] = 0x11022, // ta
        [0x0925] = 0x11023, // tha
        [0x0926] = 0x11024, // da
        [0x0927] = 0x11025, // dha
        [0x0928] = 0x11026, // na
        [0x092A] = 0x11027, // pa
        [0x092B] = 0x11028, // pha
        [0x092C] = 0x11029, // ba
        [0x092D] = 0x1102A, // bha
        [0x092E] = 0x1102B, // ma
        [0x092F] = 0x1102C, // ya
        [0x0930] = 0x1102D, // ra
        [0x0932] = 0x1102E, // la
        [0x0935] = 0x1102F, // va
        [0x0936] = 0x11030, // sha
        [0x0937] = 0x11031, // ssa
        [0x0938] = 0x11032, // sa
        [0x0939] = 0x11033, // ha
        [0x0933] = 0x11034, // lla

        // Dependent vowel signs
        [0x093E] = 0x11038, // aa
        [0x093F] = 0x1103A, // i
        [0x0940] = 0x1103B, // ii
        [0x0941] = 0x1103C, // u
        [0x0942] = 0x1103D, // uu
        [0x0943] = 0x1103E, // vocalic r
        [0x0944] = 0x1103F, // vocalic rr
        [0x0962] = 0x11040, // vocalic l
        [0x0963] = 0x11041, // vocalic ll
        [0x0947] = 0x11042, // e
        [0x0948] = 0x11043, // ai
        [0x094B] = 0x11044, // o
        [0x094C] = 0x11045, // au

        [0x094D] = 0x11046, // virama

        // Punctuation
        [0x0964] = 0x11047, // danda
        [0x0965] = 0x11048  // double danda
    };

    /// <summary>Characters that are carried through untouched rather than mapped.</summary>
    private static bool IsPassThrough(int codepoint) =>
        codepoint == ' ' || codepoint == '\n' || codepoint == '\t' || codepoint == ' ';

    /// <summary>
    /// True when every character of <paramref name="devanagari"/> has a Brahmi equivalent.
    ///
    /// The carving asks this before it commits to a script. A verse that is only partly
    /// transliterable must be cut in Devanagari rather than carved with holes in it: a wrong
    /// script is a thing only a scholar will notice, and a missing syllable is a thing
    /// everybody notices.
    /// </summary>
    public static bool CanTransliterate(string? devanagari)
    {
        if (string.IsNullOrEmpty(devanagari)) return false;

        foreach (var codepoint in Codepoints(devanagari))
        {
            if (IsPassThrough(codepoint)) continue;
            if (!Map.ContainsKey(codepoint)) return false;
        }

        return true;
    }

    /// <summary>
    /// The Brahmi form, or the input unchanged when it cannot be fully transliterated.
    ///
    /// Returning the input rather than throwing keeps a bad verse from taking the scene down;
    /// callers that need to know which script they got should ask
    /// <see cref="CanTransliterate"/> first.
    /// </summary>
    public static string Transliterate(string? devanagari)
    {
        if (string.IsNullOrEmpty(devanagari)) return string.Empty;
        if (!CanTransliterate(devanagari)) return devanagari;

        var builder = new StringBuilder(devanagari.Length * 2);

        foreach (var codepoint in Codepoints(devanagari))
        {
            builder.Append(IsPassThrough(codepoint)
                ? char.ConvertFromUtf32(codepoint)

                // Brahmi lives above the Basic Multilingual Plane, so every letter comes back
                // as a surrogate pair. Nothing downstream may index the result by char.
                : char.ConvertFromUtf32(Map[codepoint]));
        }

        return builder.ToString();
    }

    /// <summary>Walk a string as codepoints rather than as chars, so surrogates stay whole.</summary>
    private static IEnumerable<int> Codepoints(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1]))
            {
                yield return char.ConvertToUtf32(text[index], text[index + 1]);
                index++;
                continue;
            }

            yield return text[index];
        }
    }
}
