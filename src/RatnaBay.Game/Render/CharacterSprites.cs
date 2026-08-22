using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>The colours one character is drawn from. Locked to a small palette per figure.</summary>
public readonly record struct CharacterPalette(
    Color Skin,
    Color Hair,
    Color Garment,
    Color Trim,
    Color Boots)
{
    public static readonly CharacterPalette Bandit = new(
        Skin: new Color(198, 152, 112),
        Hair: new Color(48, 36, 30),
        Garment: new Color(112, 54, 52),
        Trim: new Color(64, 58, 50),
        Boots: new Color(52, 40, 34));

    public static readonly CharacterPalette Wolf = new(
        Skin: new Color(122, 118, 110),
        Hair: new Color(78, 74, 68),
        Garment: new Color(96, 92, 86),
        Trim: new Color(58, 54, 50),
        Boots: new Color(40, 38, 36));
}

/// <summary>
/// Characters, drawn in code.
///
/// This is the decision that sidesteps MonoGame's worst gap. A skinned, rigged humanoid needs
/// a mesh, a rig, retargeting and an animation system; a camera-facing quad needs none of
/// them, and it is what Daggerfall itself did. Because the texture is generated rather than
/// authored, a character is data — a palette and a few proportions — rather than an asset
/// somebody has to model.
/// </summary>
public static class CharacterSprites
{
    private const int Width = 32;
    private const int Height = 48;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>Sprite for a palette, generated once and reused.</summary>
    public static Texture2D Get(GraphicsDevice device, string key, CharacterPalette palette)
    {
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var texture = Build(device, palette);
        Cache[key] = texture;
        return texture;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }

    private static Texture2D Build(GraphicsDevice device, CharacterPalette palette)
    {
        var pixels = new Color[Width * Height];

        // Head and hair.
        FillEllipse(pixels, 16, 8, 5, 6, palette.Skin);
        FillRect(pixels, 11, 2, 10, 4, palette.Hair);
        FillRect(pixels, 10, 5, 3, 4, palette.Hair);
        FillRect(pixels, 19, 5, 3, 4, palette.Hair);

        // Eyes, so the figure reads as facing the player even at a distance.
        FillRect(pixels, 13, 8, 2, 2, new Color(28, 24, 22));
        FillRect(pixels, 17, 8, 2, 2, new Color(28, 24, 22));

        // Torso, tapering to the waist.
        for (var y = 15; y < 30; y++)
        {
            var halfWidth = 7 - (y - 15) / 6;
            FillRect(pixels, 16 - halfWidth, y, halfWidth * 2, 1, palette.Garment);
        }

        // Belt.
        FillRect(pixels, 11, 28, 10, 2, palette.Trim);

        // Arms.
        FillRect(pixels, 7, 16, 3, 11, palette.Garment);
        FillRect(pixels, 22, 16, 3, 11, palette.Garment);
        FillRect(pixels, 7, 27, 3, 3, palette.Skin);
        FillRect(pixels, 22, 27, 3, 3, palette.Skin);

        // Legs and boots.
        FillRect(pixels, 12, 30, 3, 11, palette.Trim);
        FillRect(pixels, 17, 30, 3, 11, palette.Trim);
        FillRect(pixels, 11, 41, 5, 4, palette.Boots);
        FillRect(pixels, 16, 41, 5, 4, palette.Boots);

        Outline(pixels);

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>
    /// A dark edge around the silhouette.
    ///
    /// Flat pigment with a drawn contour is the locked art direction, and it is also what
    /// keeps a small sprite readable against scenery of a similar tone.
    /// </summary>
    private static void Outline(Color[] pixels)
    {
        var outlined = new Color[pixels.Length];
        Array.Copy(pixels, outlined, pixels.Length);
        var ink = new Color(22, 18, 20);

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (pixels[y * Width + x].A != 0) continue;
            if (!HasSolidNeighbour(pixels, x, y)) continue;
            outlined[y * Width + x] = ink;
        }

        Array.Copy(outlined, pixels, pixels.Length);
    }

    private static bool HasSolidNeighbour(Color[] pixels, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;

            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
            if (pixels[ny * Width + nx].A != 0) return true;
        }

        return false;
    }

    private static void FillRect(Color[] pixels, int x, int y, int width, int height, Color colour)
    {
        for (var py = y; py < y + height; py++)
        for (var px = x; px < x + width; px++)
        {
            if (px < 0 || py < 0 || px >= Width || py >= Height) continue;
            pixels[py * Width + px] = colour;
        }
    }

    private static void FillEllipse(Color[] pixels, int cx, int cy, int rx, int ry, Color colour)
    {
        for (var py = cy - ry; py <= cy + ry; py++)
        for (var px = cx - rx; px <= cx + rx; px++)
        {
            if (px < 0 || py < 0 || px >= Width || py >= Height) continue;

            var nx = (px - cx) / (float)rx;
            var ny = (py - cy) / (float)ry;
            if (nx * nx + ny * ny <= 1f) pixels[py * Width + px] = colour;
        }
    }
}
