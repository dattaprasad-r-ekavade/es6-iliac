using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards the locked art direction.
///
/// These exist because the world's surface colours were previously hardcoded in about
/// fifteen places inside the generator, which meant the palette could be set on the
/// materials and silently ignored by everything actually built. The palette is only a lock
/// if something fails when it drifts.
/// </summary>
public class ArtDirectionTests
{
    /// <summary>
    /// Saturation of the most-saturated authored colour is ~0.43 (Sarrakh sand). The old
    /// hardcoded ocean blue was 0.83 and the old grass green 0.50, so this threshold is
    /// what separates "in the palette" from the values that were there before.
    /// </summary>
    private const float MaxSaturation = 0.5f;

    /// <summary>Nothing in the palette should approach white; the look is muted throughout.</summary>
    private const float MaxBrightness = 0.75f;

    private static ArtDirection.Look[] AllLooks =>
        (ArtDirection.Look[])System.Enum.GetValues(typeof(ArtDirection.Look));

    [Test]
    public void DefaultLook_IsTheLockedOne()
    {
        Assert.AreEqual(ArtDirection.Look.MorrowindClean, ArtDirection.Current,
            "Morrowind Clean is the locked art direction; changing the default needs a plan.md update.");
    }

    [Test]
    public void EveryPaletteColour_StaysMutedAndDark([ValueSource(nameof(AllLooks))] ArtDirection.Look look)
    {
        var p = ArtDirection.Get(look).Palette;
        var entries = new (string Name, Color Color)[]
        {
            (nameof(p.Ocean), p.Ocean),
            (nameof(p.Halbrand), p.Halbrand),
            (nameof(p.Sarrakh), p.Sarrakh),
            (nameof(p.Sand), p.Sand),
            (nameof(p.CityStone), p.CityStone),
            (nameof(p.Mountain), p.Mountain),
            (nameof(p.Road), p.Road)
        };

        foreach (var (name, color) in entries)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);

            Assert.LessOrEqual(saturation, MaxSaturation,
                $"{look}.{name} is too saturated ({saturation:0.00}) for the locked palette.");
            Assert.LessOrEqual(value, MaxBrightness,
                $"{look}.{name} is too bright ({value:0.00}) for the locked palette.");
        }
    }

    /// <summary>
    /// Grading must pull a colour toward the palette, never push it away. A weather state
    /// is allowed its own mood, but not one that leaves the locked look.
    /// </summary>
    [Test]
    public void Grade_ReducesSaturationOfAnOffPaletteColour()
    {
        var offPalette = new Color(0.12f, 0.48f, 0.72f); // the ocean blue this replaced
        Color.RGBToHSV(offPalette, out _, out float before, out _);
        Color.RGBToHSV(ArtDirection.Grade(offPalette, ArtDirection.MorrowindClean), out _, out float after, out _);

        Assert.Less(after, before,
            "Grading pushed a colour further from the palette instead of toward it.");
    }
}
