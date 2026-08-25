using FontStashSharp;
using RatnaBay.Domain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

/// <summary>
/// A verse cut into stone.
///
/// Generated rather than painted, for the same reason the character sprites are: the text is
/// then data. Changing the verse, the weathering or the stone colour costs no artist time, and
/// a deeper pillar carrying a different line is a different string rather than a new asset.
///
/// The geometry it sits on stays as low-poly as everything else. The carving is the one
/// high-resolution element in the frame, which is deliberate — in a flat-shaded world the one
/// sharp thing owns the eye, so the composition comes free.
/// </summary>
public static class StambhaCarving
{
    /// <summary>Isha Upanishad 1. "Covet not — for whose is wealth?"</summary>
    public const string SurfaceVerse = "मा गृधः कस्य स्विद्धनम्";

    /// <summary>Bhagavad Gita 2.63. "From the ruin of judgement, he perishes."</summary>
    public const string DeepVerse = "बुद्धिनाशात्प्रणश्यति";

    /// <summary>
    /// Devanagari conjuncts turn to mush below roughly this size. The carving has to read as
    /// writing even when the words cannot be read, so it is generated well above the
    /// resolution the rest of the world uses.
    /// </summary>
    private const int GlyphPixels = 150;

    private const int Width = 1024;
    private const int Height = 512;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);
    private static FontSystem? _devanagari;

    /// <summary>
    /// The script the pillar is actually cut in, when its font is present.
    ///
    /// Devanagari postdates a Mauryan mine by roughly a thousand years. Brahmi is what the
    /// period's pillar edicts were carved in, and it is the better picture as well as the truer
    /// one: angular, open, and free of the stacked conjuncts that turn Devanagari to mush at
    /// this size. The verses stay authored in Devanagari because that is what a maintainer can
    /// read; <see cref="BrahmiTransliteration"/> converts at rasterisation time.
    /// </summary>
    private static FontSystem? _brahmi;

    /// <summary>
    /// Weathered stone, in the flat pigment the art direction is locked to.
    ///
    /// These have to be the shaft's own colour, not merely a similar one. The carved band is
    /// drawn as a lit quad lying on the shaft's front face, so if its base tone differs at all
    /// the band reads as a plaque bolted to the pillar rather than a course of the pillar
    /// itself. <see cref="ShaftStone"/> is the single source both sides read.
    /// </summary>
    public static readonly Color ShaftStone = new(104, 97, 87);

    private static readonly Color Stone = ShaftStone;
    private static readonly Color StoneDark = new(88, 82, 73);

    /// <summary>Inside a cut groove, where the light does not reach.</summary>
    private static readonly Color Groove = new(46, 42, 38);

    /// <summary>The top lip of a groove, where a raking light catches the edge.</summary>
    private static readonly Color Lip = new(158, 148, 130);

    /// <summary>Mineral staining that has run into the cuts over a few centuries.</summary>
    private static readonly Color Stain = new(88, 92, 74);

    public static void Load(string fontDirectory)
    {
        _devanagari = TryLoad(Path.Combine(
            fontDirectory, "NotoSansDevanagari", "NotoSansDevanagari-wght.ttf"));

        _brahmi = TryLoad(Path.Combine(
            fontDirectory, "NotoSansBrahmi", "NotoSansBrahmi-Regular.ttf"));
    }

    private static FontSystem? TryLoad(string path)
    {
        if (!File.Exists(path)) return null;

        var system = new FontSystem();
        system.AddFont(File.ReadAllBytes(path));
        return system;
    }

    /// <summary>True when a pillar can be carved at all, in either script.</summary>
    public static bool IsAvailable => _devanagari is not null || _brahmi is not null;

    /// <summary>True when carvings are in the setting's own script rather than the fallback.</summary>
    public static bool IsPeriodScript => _brahmi is not null;

    /// <summary>
    /// Which font cuts a given verse, and the text to cut with it.
    ///
    /// Brahmi wins when its font is loaded and the verse maps completely. A verse that only
    /// half-transliterates falls back whole rather than being carved with holes in it: the
    /// wrong script is something a scholar notices, and a missing syllable is something
    /// everybody notices.
    /// </summary>
    private static (FontSystem? Font, string Text, string Script) Resolve(string verse)
    {
        if (_brahmi is not null && BrahmiTransliteration.CanTransliterate(verse))
            return (_brahmi, BrahmiTransliteration.Transliterate(verse), "brahmi");

        return (_devanagari, verse, "devanagari");
    }

    /// <summary>
    /// The carved face of a pillar. Null when the Devanagari font is not present, so a missing
    /// font degrades to a blank pillar rather than taking the scene down with it.
    /// </summary>
    public static Texture2D? Get(GraphicsDevice device, string verse)
    {
        var (font, text, script) = Resolve(verse);
        if (font is null) return null;

        // Keyed by script as well as verse: the same line cut in two scripts is two textures,
        // and a cache that forgot which one it holds would serve the wrong stone.
        var key = script + ":" + verse;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var texture = Build(device, font, text, verse);
        Cache[key] = texture;
        return texture;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
        _devanagari?.Dispose();
        _devanagari = null;
        _brahmi?.Dispose();
        _brahmi = null;
    }

    private static Texture2D Build(GraphicsDevice device, FontSystem font, string text, string verse)
    {
        // The glyphs are rasterised once into a scratch target, then read back and turned into
        // stone. Doing it as pixels rather than as a shader keeps the whole thing inspectable.
        var mask = RenderGlyphMask(device, font, text);
        var pixels = new Color[Width * Height];

        // Seeded from the authored verse, never from the transliterated one, so a pillar weathers
        // identically whichever script it ends up cut in.
        var random = new Random(verse.GetHashCode());

        Quarry(pixels, random);
        Cut(pixels, mask);
        Age(pixels, random);

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Rasterise the verse to an alpha mask via the existing font stack.</summary>
    private static bool[] RenderGlyphMask(GraphicsDevice device, FontSystem fontSystem, string verse)
    {
        var font = fontSystem.GetFont(GlyphPixels);
        var measured = font.MeasureString(verse);

        // Fit the line to the pillar face with a margin, never scaling it up past its raster.
        var scale = MathF.Min(1f, (Width - 120f) / MathF.Max(1f, measured.X));
        var position = new Vector2(
            (Width - measured.X * scale) * 0.5f,
            (Height - measured.Y * scale) * 0.5f);

        using var target = new RenderTarget2D(device, Width, Height);
        var previous = device.GetRenderTargets();

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        using (var batch = new SpriteBatch(device))
        {
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            batch.DrawString(font, verse, position, Color.White, 0f, Vector2.Zero,
                new Vector2(scale), 0f, 0f, 0f, TextStyle.None, FontSystemEffect.None, 0);
            batch.End();
        }

        device.SetRenderTargets(previous);

        var raw = new Color[Width * Height];
        target.GetData(raw);

        var mask = new bool[raw.Length];
        for (var i = 0; i < raw.Length; i++) mask[i] = raw[i].A > 96;
        return Thicken(mask, ChiselWidth);
    }

    /// <summary>
    /// How many pixels to grow every stroke by before it is cut.
    ///
    /// Noto Sans Brahmi is a text face, and text faces are drawn thin. A chisel is not thin:
    /// carved at the font's own weight the verse reads as scratched into the stone rather than
    /// cut out of it, and the groove is too narrow for its lit lip to show at all. Growing the
    /// stroke is what turns a typeface into a mason's letter.
    /// </summary>
    private const int ChiselWidth = 3;

    /// <summary>Grow a mask by <paramref name="radius"/> pixels in every direction.</summary>
    private static bool[] Thicken(bool[] mask, int radius)
    {
        if (radius <= 0) return mask;

        // Separable: a horizontal pass then a vertical one costs 2r per pixel instead of r
        // squared, and for a box-shaped dilation the result is identical.
        var horizontal = new bool[mask.Length];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            for (var offset = -radius; offset <= radius; offset++)
            {
                var sample = x + offset;
                if (sample < 0 || sample >= Width) continue;
                if (!mask[y * Width + sample]) continue;

                horizontal[y * Width + x] = true;
                break;
            }
        }

        var grown = new bool[mask.Length];
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            for (var offset = -radius; offset <= radius; offset++)
            {
                var sample = y + offset;
                if (sample < 0 || sample >= Height) continue;
                if (!horizontal[sample * Width + x]) continue;

                grown[y * Width + x] = true;
                break;
            }
        }

        return grown;
    }

    /// <summary>Flat stone with a little tonal drift, so the face is not a single dead colour.</summary>
    private static void Quarry(Color[] pixels, Random random)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            // Broad bands, as though the block were cut from bedded rock.
            var band = MathF.Sin(y * 0.011f) * 0.5f + MathF.Sin(x * 0.004f) * 0.5f;
            var t = MathHelper.Clamp(0.5f + band * 0.5f, 0f, 1f);
            var shade = Color.Lerp(StoneDark, Stone, t);

            var grain = random.Next(-6, 7);
            pixels[y * Width + x] = new Color(
                Math.Clamp(shade.R + grain, 0, 255),
                Math.Clamp(shade.G + grain, 0, 255),
                Math.Clamp(shade.B + grain, 0, 255));
        }
    }

    /// <summary>
    /// Cut the glyphs into the face: dark in the groove, a bright lip on the upper edge where a
    /// raking light would catch it. The lip is what makes it read as carved rather than printed.
    /// </summary>
    private static void Cut(Color[] pixels, bool[] mask)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var index = y * Width + x;
            if (!mask[index]) continue;

            // Light rakes from below-left, as the jiva stone does, so the catch sits on the
            // upper edge of every cut.
            var aboveIsStone = y > 0 && !mask[index - Width];
            pixels[index] = aboveIsStone ? Lip : Groove;
        }

        // One more pass for the lower lip, a shade between groove and stone.
        for (var y = 0; y < Height - 1; y++)
        for (var x = 0; x < Width; x++)
        {
            var index = y * Width + x;
            if (mask[index] || !mask[index + Width]) continue;
            pixels[index] = Color.Lerp(pixels[index], StoneDark, 0.7f);
        }
    }

    /// <summary>Chips, and staining that has run down out of the cuts.</summary>
    private static void Age(Color[] pixels, Random random)
    {
        for (var i = 0; i < 220; i++)
        {
            var cx = random.Next(Width);
            var cy = random.Next(Height);
            var radius = random.Next(2, 9);

            for (var y = cy - radius; y <= cy + radius; y++)
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;

                var dx = (x - cx) / (float)radius;
                var dy = (y - cy) / (float)radius;
                if (dx * dx + dy * dy > 1f) continue;

                var index = y * Width + x;
                pixels[index] = Color.Lerp(pixels[index], StoneDark, 0.45f);
            }
        }

        // Staining runs downward from the cuts, because water does.
        for (var x = 0; x < Width; x++)
        {
            var run = 0;
            for (var y = 0; y < Height; y++)
            {
                var index = y * Width + x;
                if (pixels[index] == Groove) { run = random.Next(10, 46); continue; }

                if (run <= 0) continue;
                run--;
                pixels[index] = Color.Lerp(pixels[index], Stain, 0.16f);
            }
        }
    }
}
