using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client.World;

/// <summary>
/// A cast spell in flight.
///
/// The prana is spent the moment the player casts; the effect happens where the bolt lands.
/// That gap is the whole point — a spell you can watch travel is a spell an enemy can walk
/// out of, which is what turns casting into aiming rather than clicking.
/// </summary>
public sealed class SpellBolt
{
    public required SpellDefinition Spell { get; init; }
    public required Color Colour { get; init; }

    /// <summary>Where it is now, in world space.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Metres per second along its flight.</summary>
    public Vector3 Velocity { get; init; }

    /// <summary>What it was aimed at, if the crosshair found something.</summary>
    public Enemy? Target { get; init; }

    /// <summary>Seconds before it fizzles out having hit nothing.</summary>
    public float Remaining { get; set; }

    /// <summary>Spins the sprite so a bolt reads as energy rather than a floating disc.</summary>
    public float Spin { get; set; }
}

/// <summary>Glowing blobs, drawn in code, one per element colour.</summary>
public static class BoltSprites
{
    private const int Size = 32;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// A soft core with a harder rim.
    ///
    /// Alpha-tested rendering has no soft edges, so the falloff is drawn as concentric bands
    /// rather than a gradient — which also suits the flat-pigment art direction.
    /// </summary>
    public static Texture2D Get(GraphicsDevice device, Color colour)
    {
        var key = colour.PackedValue.ToString();
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var pixels = new Color[Size * Size];
        const float centre = (Size - 1) / 2f;

        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var dx = (x - centre) / centre;
            var dy = (y - centre) / centre;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > 1f) continue;

            // Three bands: white-hot core, the element's colour, then a darker rim.
            var shade = distance switch
            {
                < 0.34f => Color.Lerp(Color.White, colour, 0.35f),
                < 0.72f => colour,
                _ => new Color(colour.R / 2, colour.G / 2, colour.B / 2)
            };

            pixels[y * Size + x] = shade;
        }

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        Cache[key] = texture;
        return texture;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }
}
