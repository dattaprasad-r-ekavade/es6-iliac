using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// The weapon in the player's hand, drawn in code.
///
/// Same reasoning as the character sprites: a held weapon is a palette and a few proportions
/// rather than a modelled asset. Every blade is drawn from one routine with different
/// measurements, so a new tier is a row of numbers.
/// </summary>
public static class WeaponSprites
{
    private const int Width = 96;
    private const int Height = 192;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>Metal, grip and trim for one weapon.</summary>
    private readonly record struct Metal(Color Edge, Color Body, Color Grip, Color Fitting);

    private static readonly Metal Iron = new(
        Edge: new Color(198, 204, 212),
        Body: new Color(142, 150, 160),
        Grip: new Color(74, 52, 40),
        Fitting: new Color(96, 82, 58));

    private static readonly Metal Steel = new(
        Edge: new Color(226, 232, 240),
        Body: new Color(168, 178, 190),
        Grip: new Color(58, 44, 38),
        Fitting: new Color(150, 126, 78));

    private static readonly Metal Wood = new(
        Edge: new Color(146, 110, 72),
        Body: new Color(112, 82, 54),
        Grip: new Color(70, 52, 38),
        Fitting: new Color(120, 118, 112));

    public static Texture2D Get(GraphicsDevice device, WeaponDefinition weapon)
    {
        if (Cache.TryGetValue(weapon.Id, out var cached)) return cached;

        var texture = Build(device, weapon);
        Cache[weapon.Id] = texture;
        return texture;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }

    private static Texture2D Build(GraphicsDevice device, WeaponDefinition weapon)
    {
        var pixels = new Color[Width * Height];
        var metal = weapon.Tier >= 2 ? Steel : Iron;

        switch (weapon.Class)
        {
            case WeaponClass.TwoHanded:
                DrawBlade(pixels, metal, bladeHalfWidth: 14, bladeTop: 6, guardY: 140, guardHalf: 30);
                break;

            case WeaponClass.Ranged:
                DrawBow(pixels);
                break;

            default:
                if (weapon.Id == EquipmentCatalog.UnarmedId) DrawFist(pixels);
                else DrawBlade(pixels, metal, bladeHalfWidth: 10, bladeTop: 26, guardY: 142, guardHalf: 22);
                break;
        }

        Outline(pixels);

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>A blade, tapering to a point, with a guard, grip and pommel below it.</summary>
    private static void DrawBlade(Color[] pixels, Metal metal, int bladeHalfWidth, int bladeTop,
        int guardY, int guardHalf)
    {
        const int centre = Width / 2;

        for (var y = bladeTop; y < guardY; y++)
        {
            // The first few rows narrow to a tip rather than ending square.
            var taper = MathF.Min(1f, (y - bladeTop) / 22f);
            var half = MathF.Max(1f, bladeHalfWidth * taper);

            FillRect(pixels, centre - (int)half, y, (int)half * 2, 1, metal.Body);

            // A bright edge down one side reads as a blade rather than a bar.
            FillRect(pixels, centre - (int)half, y, 2, 1, metal.Edge);
        }

        // Fuller: a dark groove down the middle.
        FillRect(pixels, centre - 1, bladeTop + 26, 2, guardY - bladeTop - 34,
            new Color(metal.Body.R - 40, metal.Body.G - 40, metal.Body.B - 40));

        FillRect(pixels, centre - guardHalf, guardY, guardHalf * 2, 7, metal.Fitting);
        FillRect(pixels, centre - 5, guardY + 7, 10, 34, metal.Grip);
        FillRect(pixels, centre - 8, guardY + 41, 16, 8, metal.Fitting);
    }

    private static void DrawBow(Color[] pixels)
    {
        const int centre = Width / 2;

        // A limb either side of the grip, bowed outward.
        for (var y = 16; y < Height - 24; y++)
        {
            var t = (y - 16) / (float)(Height - 40);
            var bulge = (int)(MathF.Sin(t * MathF.PI) * 24f);
            FillRect(pixels, centre + bulge - 3, y, 5, 1, Wood.Body);
            FillRect(pixels, centre + bulge - 3, y, 2, 1, Wood.Edge);
        }

        // String.
        for (var y = 16; y < Height - 24; y++)
            FillRect(pixels, centre - 4, y, 1, 1, new Color(206, 198, 176));

        FillRect(pixels, centre - 4, Height / 2 - 16, 8, 32, Wood.Grip);
    }

    private static void DrawFist(Color[] pixels)
    {
        var skin = new Color(198, 152, 112);
        var shade = new Color(168, 124, 90);

        FillRect(pixels, 30, 96, 36, 34, skin);
        FillRect(pixels, 30, 96, 36, 6, shade);

        // Knuckles.
        for (var i = 0; i < 4; i++) FillRect(pixels, 32 + i * 9, 100, 6, 10, shade);

        FillRect(pixels, 36, 130, 24, 30, shade);
    }

    private static void Outline(Color[] pixels)
    {
        var outlined = new Color[pixels.Length];
        Array.Copy(pixels, outlined, pixels.Length);
        var ink = new Color(20, 17, 19);

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
}
