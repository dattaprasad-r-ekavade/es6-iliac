using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards the locked art direction.
///
/// These exist because the world's surface colours were previously hardcoded in about
/// fifteen places inside the generator, which meant the palette could be set on the
/// materials and silently ignored by everything actually built. The palette is only a lock
/// if something fails when it drifts.
///
/// Until 2026-08-12 there was a single assertion that every palette in every preset stayed
/// muted and dark. That was correct while both presets descended from Morrowind, and wrong the
/// moment <see cref="ArtDirection.Look.ArenaMiniature"/> was adopted — a flat pigment palette
/// is supposed to be saturated. The lock is now per-look: each preset is held to the range that
/// preset is trying to be, and a separate test holds that the two ranges stay far enough apart
/// to be different looks rather than the same one drifting.
/// </summary>
public class ArtDirectionTests
{
    /// <summary>
    /// Saturation of the most-saturated authored colour in the muted presets is ~0.43 (Sarrakh
    /// sand). The old hardcoded ocean blue was 0.83 and the old grass green 0.50, so this
    /// threshold is what separates "in the palette" from the values that were there before.
    /// </summary>
    private const float MutedMaxSaturation = 0.5f;

    /// <summary>Nothing in a muted palette should approach white.</summary>
    private const float MutedMaxBrightness = 0.75f;

    /// <summary>
    /// Historical pigments are strong but not pure. Anything above this is a screen primary
    /// rather than a colour that ever came out of a grinding stone.
    /// </summary>
    private const float PigmentMaxSaturation = 0.85f;

    /// <summary>
    /// The mean saturation the miniature palette must clear. Its authored mean is ~0.50; the
    /// muted presets sit at ~0.25. A palette that drifts below this has quietly turned back
    /// into Morrowind Clean, which is the specific regression worth catching.
    /// </summary>
    private const float PigmentMinMeanSaturation = 0.35f;

    private static readonly ArtDirection.Look[] MutedLooks =
    {
        ArtDirection.Look.MorrowindClean,
        ArtDirection.Look.Ps1Crunch
    };

    private static (string Name, Color Color)[] Entries(ArtDirection.Look look)
    {
        var p = ArtDirection.Get(look).Palette;
        return new[]
        {
            (nameof(p.Ocean), p.Ocean),
            (nameof(p.Temperate), p.Temperate),
            (nameof(p.Arid), p.Arid),
            (nameof(p.Sand), p.Sand),
            (nameof(p.CityStone), p.CityStone),
            (nameof(p.Mountain), p.Mountain),
            (nameof(p.Road), p.Road)
        };
    }

    private static float Saturation(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out _);
        return s;
    }

    private static float MeanSaturation(ArtDirection.Look look) =>
        Entries(look).Average(e => Saturation(e.Color));

    [Test]
    public void DefaultLook_IsTheLockedOne()
    {
        Assert.AreEqual(ArtDirection.Look.ArenaMiniature, ArtDirection.Current,
            "Arena Miniature is the locked art direction; changing the default needs a plan.md update.");
    }

    [Test]
    public void MutedPresets_StayMutedAndDark([ValueSource(nameof(MutedLooks))] ArtDirection.Look look)
    {
        foreach (var (name, color) in Entries(look))
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);

            Assert.LessOrEqual(saturation, MutedMaxSaturation,
                $"{look}.{name} is too saturated ({saturation:0.00}) for a muted palette.");
            Assert.LessOrEqual(value, MutedMaxBrightness,
                $"{look}.{name} is too bright ({value:0.00}) for a muted palette.");
        }
    }

    /// <summary>
    /// The miniature palette is held to pigment, not to mud and not to neon. The ceiling is what
    /// stops it becoming screen primaries; the mean floor is what stops it sliding back toward
    /// the muted presets one "slightly calmer" edit at a time.
    /// </summary>
    [Test]
    public void ArenaMiniature_StaysInPigmentRange()
    {
        foreach (var (name, color) in Entries(ArtDirection.Look.ArenaMiniature))
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);

            Assert.LessOrEqual(saturation, PigmentMaxSaturation,
                $"ArenaMiniature.{name} is a screen primary ({saturation:0.00}), not a pigment.");
            Assert.GreaterOrEqual(value, 0.25f,
                $"ArenaMiniature.{name} is too dark ({value:0.00}); flat fields need to carry their own light.");
            Assert.LessOrEqual(value, 0.92f,
                $"ArenaMiniature.{name} is approaching white ({value:0.00}).");
        }

        Assert.GreaterOrEqual(MeanSaturation(ArtDirection.Look.ArenaMiniature), PigmentMinMeanSaturation,
            "The miniature palette has drifted back toward muted. Flat colour separation is the "
            + "only depth cue this look has — desaturating it removes the depth cue and the identity "
            + "in one edit.");
    }

    /// <summary>
    /// The two directions must stay recognisably apart. Without this, both palettes could be
    /// edited toward each other until the look enum described a difference that no longer existed.
    /// </summary>
    [Test]
    public void TheMiniatureAndMutedPalettes_AreActuallyDifferentLooks()
    {
        float miniature = MeanSaturation(ArtDirection.Look.ArenaMiniature);
        float muted = MeanSaturation(ArtDirection.Look.MorrowindClean);

        Assert.Greater(miniature, muted * 1.5f,
            $"ArenaMiniature ({miniature:0.00}) is no longer meaningfully more saturated than "
            + $"MorrowindClean ({muted:0.00}). These are supposed to be two directions, not one.");
    }

    /// <summary>
    /// Contours are the miniature look's substitute for shading. If the colour goes transparent
    /// the generators silently stop drawing outlines, and flat fields with no separation read as
    /// untextured greybox — the exact failure this direction was chosen to avoid.
    /// </summary>
    [Test]
    public void ArenaMiniature_DrawsADarkContour()
    {
        var preset = ArtDirection.Get(ArtDirection.Look.ArenaMiniature);

        Assert.IsTrue(ArtDirection.UsesContour(preset),
            "The miniature look stopped declaring a contour, so nothing will outline anything.");

        Color.RGBToHSV(preset.Palette.Contour, out _, out _, out float value);
        Assert.LessOrEqual(value, 0.30f,
            $"The contour is too light ({value:0.00}) to separate adjacent flat fields.");
    }

    [Test]
    public void MutedPresets_DrawNoContour([ValueSource(nameof(MutedLooks))] ArtDirection.Look look)
    {
        Assert.IsFalse(ArtDirection.UsesContour(ArtDirection.Get(look)),
            $"{look} declares a contour, but it separates surfaces by lighting them.");
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

    /// <summary>
    /// The counterpart for the locked look. Grading still holds weather inside the paper-warm
    /// range, but it must not flatten chroma on the way — the muted presets desaturate on
    /// purpose and this one must not inherit that.
    /// </summary>
    [Test]
    public void Grade_UnderTheMiniatureLook_DoesNotFlattenChroma()
    {
        var preset = ArtDirection.Get(ArtDirection.Look.ArenaMiniature);
        Assert.AreEqual(0f, preset.Desaturation, 0.0001f,
            "The miniature look started desaturating, which removes the only thing carrying depth.");

        var indigo = preset.Palette.Ocean;
        float before = Saturation(indigo);
        float after = Saturation(ArtDirection.Grade(indigo, preset));

        Assert.Greater(after, before * 0.6f,
            $"Grading crushed an in-palette pigment from {before:0.00} to {after:0.00}.");
    }
}
