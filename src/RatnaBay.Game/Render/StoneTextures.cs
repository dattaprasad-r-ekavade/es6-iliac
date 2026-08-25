using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Tileable stone, generated rather than painted.
///
/// The same argument as <see cref="StambhaCarving"/> and the character sprites: a texture that is
/// a function of a palette and some numbers costs no artist time, varies per cave theme by
/// changing three colours, and cannot go missing from a build. A hand-drawn tileset is better
/// looking and is five times the work at five themes.
///
/// Everything here is authored to tile: the block courses wrap in X, and nothing samples outside
/// its own cell. A seam is the one defect that makes generated texture look generated.
/// </summary>
public static class StoneTextures
{
    /// <summary>
    /// Texture resolution. 256 at roughly two metres per tile puts a texel near a centimetre,
    /// which is about where the mortar lines stop looking like drawn lines and start looking
    /// like gaps.
    /// </summary>
    private const int Size = 256;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>One cave theme's stone, in three colours and nothing else.</summary>
    public readonly record struct StonePalette(string Id, Color Base, Color Mortar, Color Accent)
    {
        /// <summary>The default: cold grey blockwork, the mine as it is authored today.</summary>
        public static readonly StonePalette Granite =
            new("granite", new Color(86, 86, 92), new Color(34, 33, 36), new Color(120, 118, 122));

        /// <summary>
        /// Warm sandstone, for the yard above the mines.
        ///
        /// Coming up out of a mine has to look like arriving somewhere, and the same cold grey
        /// blockwork under daylight simply reads as another room with a sky over it. Warmth is
        /// doing the work the fiction needs at the cheapest possible price.
        /// </summary>
        public static readonly StonePalette Sandstone =
            new("sandstone", new Color(146, 122, 92), new Color(96, 78, 58), new Color(178, 152, 116));
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }

    /// <summary>Coursed blockwork for walls.</summary>
    public static Texture2D Wall(GraphicsDevice device, StonePalette palette)
        => Get(device, "wall:" + palette.Id, () => BuildWall(palette));

    /// <summary>Irregular flagstones for floors.</summary>
    public static Texture2D Floor(GraphicsDevice device, StonePalette palette)
        => Get(device, "floor:" + palette.Id, () => BuildFloor(palette));

    /// <summary>
    /// A soft radial falloff, drawn additively to fake the pool of light a torch throws.
    ///
    /// BasicEffect has no point lights, and the mine is generated so its lighting cannot be
    /// baked. Until there is a custom effect, a glow quad on the surface behind the flame is
    /// what puts light on a wall, and at this art scale it is very nearly indistinguishable.
    /// </summary>
    public static Texture2D Glow(GraphicsDevice device)
        => Get(device, "glow", BuildGlow);

    private static Texture2D Get(GraphicsDevice device, string key, Func<Color[]> build)
    {
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(build());
        Cache[key] = texture;
        return texture;
    }

    // ------------------------------------------------------------------ walls

    private const int CourseHeight = 32;
    private const int BlockWidth = 64;
    private const int Mortar = 3;

    private static Color[] BuildWall(StonePalette palette)
    {
        var pixels = new Color[Size * Size];
        var random = new Random(palette.Id.GetHashCode() ^ 0x5711);

        // Per-block tone, picked once so a block is one stone rather than a field of noise.
        var courses = Size / CourseHeight;
        var perCourse = Size / BlockWidth;
        var tone = new float[courses, perCourse + 1];
        for (var c = 0; c < courses; c++)
        for (var b = 0; b <= perCourse; b++)
            tone[c, b] = (float)random.NextDouble();

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var course = y / CourseHeight;
            var rowY = y % CourseHeight;

            // Every other course steps half a block, the way a wall is actually laid. The
            // offset wraps with the texture, so the tiling seam falls inside a block rather
            // than along its edge.
            var offset = (course % 2 == 0) ? 0 : BlockWidth / 2;
            var shifted = (x + offset) % Size;
            var block = shifted / BlockWidth;
            var colX = shifted % BlockWidth;

            var index = y * Size + x;

            if (rowY < Mortar || colX < Mortar)
            {
                pixels[index] = Jitter(palette.Mortar, random, 5);
                continue;
            }

            var shade = MathHelper.Lerp(-14f, 16f, tone[course, block]);

            // A cut face is lit from above: the top of a block catches, the bottom sits in the
            // shadow of the course over it. Two pixels of each is the whole bevel, and it is
            // what stops flat colour reading as wallpaper.
            if (rowY < Mortar + 2) shade += 22f;
            else if (rowY > CourseHeight - 3) shade -= 18f;
            if (colX < Mortar + 2) shade += 10f;
            else if (colX > BlockWidth - 3) shade -= 10f;

            var grain = random.Next(-7, 8);
            pixels[index] = Shift(palette.Base, shade + grain);
        }

        Pit(pixels, palette, random, count: 90);
        return pixels;
    }

    // ------------------------------------------------------------------ floor

    private static Color[] BuildFloor(StonePalette palette)
    {
        var pixels = new Color[Size * Size];
        var random = new Random(palette.Id.GetHashCode() ^ 0x21A7);

        const int flag = 42;
        var cells = Size / flag;
        var tone = new float[cells + 1, cells + 1];
        for (var a = 0; a <= cells; a++)
        for (var b = 0; b <= cells; b++)
            tone[a, b] = (float)random.NextDouble();

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var cx = x / flag;
            var cy = y / flag;
            var inX = x % flag;
            var inY = y % flag;
            var index = y * Size + x;

            if (inX < 2 || inY < 2)
            {
                pixels[index] = Jitter(palette.Mortar, random, 4);
                continue;
            }

            // Floors are walked on: less bevel than a wall, more wear toward the middle of
            // each flag.
            var shade = MathHelper.Lerp(-16f, 12f, tone[cy, cx]);
            if (inY < 4) shade += 10f;
            else if (inY > flag - 4) shade -= 10f;

            var grain = random.Next(-6, 7);
            pixels[index] = Shift(palette.Base, shade + grain - 10f);
        }

        Pit(pixels, palette, random, count: 140);
        return pixels;
    }

    // ------------------------------------------------------------------ shared

    /// <summary>Chips and pocks, so no two square metres of the tile read as identical.</summary>
    private static void Pit(Color[] pixels, StonePalette palette, Random random, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var cx = random.Next(Size);
            var cy = random.Next(Size);
            var radius = random.Next(1, 5);
            var darken = random.Next(10, 30);

            for (var y = cy - radius; y <= cy + radius; y++)
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                var dx = (x - cx) / (float)radius;
                var dy = (y - cy) / (float)radius;
                if (dx * dx + dy * dy > 1f) continue;

                // Wrap rather than clip, or every chip near an edge becomes a visible seam.
                var wx = ((x % Size) + Size) % Size;
                var wy = ((y % Size) + Size) % Size;
                var index = wy * Size + wx;

                pixels[index] = Shift(pixels[index], -darken);
            }
        }
    }

    private static Color[] BuildGlow()
    {
        var pixels = new Color[Size * Size];
        const float centre = Size / 2f;

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var dx = (x - centre) / centre;
            var dy = (y - centre) / centre;
            var distance = MathF.Sqrt(dx * dx + dy * dy);

            // Squared falloff, hard zero at the edge so the quad has no visible boundary.
            var strength = MathHelper.Clamp(1f - distance, 0f, 1f);
            strength *= strength;

            var value = (byte)(strength * 255f);
            pixels[y * Size + x] = new Color(value, value, value, value);
        }

        return pixels;
    }

    private static Color Shift(Color colour, float amount) => new(
        (byte)Math.Clamp(colour.R + amount, 0, 255),
        (byte)Math.Clamp(colour.G + amount, 0, 255),
        (byte)Math.Clamp(colour.B + amount, 0, 255));

    private static Color Jitter(Color colour, Random random, int spread) =>
        Shift(colour, random.Next(-spread, spread + 1));
}
