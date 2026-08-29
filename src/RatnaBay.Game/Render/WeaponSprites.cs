using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client.Render;

/// <summary>
/// The weapon in the player's hand, drawn in code.
///
/// Same reasoning as the character sprites: a held weapon is a palette and a few proportions
/// rather than a modelled asset. Every blade comes out of one routine with different
/// measurements, so a new tier is still a row of numbers.
///
/// Built on <see cref="SpriteForge"/>. The old version drew a blade as a stack of horizontal
/// runs with a lighter strip down one side, which is a convincing edge from one angle and a
/// painted-on stripe from any other. A blade here is a tapered plate with a raised spine down
/// its middle, and the light breaking over that spine is what makes it read as a blade. The
/// bright edge is a consequence of the form now rather than a decoration on top of it.
/// </summary>
public static class WeaponSprites
{
    private const int Width = 96;
    private const int Height = 192;

    /// <summary>The same lamp as the characters and the item icons.</summary>
    private static readonly Vector3 Key = new(-0.55f, -0.68f, 0.5f);

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    private static readonly SpriteMaterial Iron = new()
    {
        Ramp = new[]
        {
            new Color(34, 37, 48), new Color(58, 62, 76), new Color(88, 94, 108),
            new Color(126, 133, 146), new Color(166, 174, 185), new Color(212, 219, 228)
        },
        Outline = new Color(18, 19, 26),
        Gloss = 0.8f,
        Highlight = new Color(248, 252, 255)
    };

    private static readonly SpriteMaterial Steel = new()
    {
        Ramp = new[]
        {
            new Color(44, 50, 62), new Color(74, 82, 96), new Color(110, 120, 134),
            new Color(150, 160, 174), new Color(192, 202, 214), new Color(238, 244, 250)
        },
        Outline = new Color(22, 25, 33),
        Gloss = 1.0f,
        Highlight = Color.White
    };

    private static readonly SpriteMaterial Timber = SpriteMaterial.FromBase(new Color(112, 82, 54));
    private static readonly SpriteMaterial Grip = SpriteMaterial.FromBase(new Color(70, 52, 38));
    private static readonly SpriteMaterial Fitting = SpriteMaterial.FromBase(new Color(146, 118, 62), gloss: 0.7f);
    private static readonly SpriteMaterial Skin = SpriteMaterial.FromBase(new Color(198, 152, 112));

    private static readonly SpriteMaterial Cord = new()
    {
        Ramp = new[] { new Color(126, 118, 100), new Color(178, 170, 148), new Color(224, 218, 198) },
        Outline = new Color(64, 58, 48)
    };

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
        var forge = new SpriteForge(Width, Height);
        var metal = weapon.Tier >= 2 ? Steel : Iron;

        switch (weapon.Class)
        {
            case WeaponClass.TwoHanded:
                Blade(forge, metal, halfWidth: 15f, tip: 6f, guardY: 138f, guardHalf: 31f);
                break;

            case WeaponClass.Ranged:
                Bow(forge);
                break;

            case WeaponClass.Blunt:
                Mace(forge, metal);
                break;

            default:
                if (weapon.Id == EquipmentCatalog.UnarmedId) Fist(forge);
                else Blade(forge, metal, halfWidth: 10f, tip: 26f, guardY: 142f, guardHalf: 22f);
                break;
        }

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(forge.Resolve(Key));
        return texture;
    }

    /// <summary>A blade tapering to a point, with a guard, grip and pommel below it.</summary>
    private static void Blade(SpriteForge forge, SpriteMaterial metal,
        float halfWidth, float tip, float guardY, float guardHalf)
    {
        const float Centre = Width / 2f;
        var shoulder = tip + 24f;

        // The plate: full width at the shoulder, drawn to a point at the tip.
        forge.Begin();
        forge.Polygon(
            new Vector2(Centre, tip),
            new Vector2(Centre + halfWidth, shoulder),
            new Vector2(Centre + halfWidth, guardY),
            new Vector2(Centre - halfWidth, guardY),
            new Vector2(Centre - halfWidth, shoulder));
        forge.Fill(metal, roundness: 0.5f, cap: 3.6f);

        // The spine. Everything the eye reads as "sharp" comes from this: the surface falls
        // away from the ridge toward both edges, so the lit side and the shadowed side are
        // produced by the form rather than painted on.
        forge.Begin();
        forge.Capsule(Centre, tip + 8f, Centre, guardY - 6f, halfWidth * 0.30f, halfWidth * 0.42f);
        forge.Fill(metal, roundness: 1.5f, cap: 6.2f, lift: 1.2f);

        forge.Begin();
        forge.Capsule(Centre - guardHalf, guardY + 3f, Centre + guardHalf, guardY + 3f, 3.4f, 3.4f);
        forge.Ellipse(Centre, guardY + 3f, 7f, 5f);
        forge.Fill(Fitting, roundness: 1.15f, cap: 4.0f, lift: 5.2f);

        forge.Begin();
        forge.Capsule(Centre, guardY + 10f, Centre, guardY + 40f, 5.0f, 4.6f);
        forge.Fill(Grip, roundness: 1.1f, cap: 4.6f, lift: 5.6f);

        forge.Begin();
        forge.Ellipse(Centre, guardY + 45f, 8f, 6f);
        forge.Fill(Fitting, roundness: 1.2f, cap: 6.0f, lift: 6.0f);
    }

    /// <summary>
    /// A flanged mace: a haft, a collar, and a head that is unmistakably not a blade.
    ///
    /// The silhouette is doing the work. A player has to know from the viewmodel alone that
    /// this swing staggers and the last one did not, and the only thing they see of their own
    /// weapon is its outline against the floor.
    /// </summary>
    private static void Mace(SpriteForge forge, SpriteMaterial metal)
    {
        const float Centre = Width / 2f;

        forge.Begin();
        forge.Capsule(Centre, 74f, Centre, 168f, 5.0f, 4.4f);
        forge.Fill(Grip, roundness: 1.05f, cap: 5.0f);

        // Collar where the head is seated, so the head does not appear to float on the haft.
        forge.Begin();
        forge.Capsule(Centre, 68f, Centre, 78f, 7.4f, 6.6f);
        forge.Fill(Fitting, roundness: 1.15f, cap: 5.2f, lift: 2.2f);

        // The head: a core with four flanges radiating from it. Flanges rather than a ball,
        // because a ball at this size reads as a lollipop.
        forge.Begin();
        forge.Ellipse(Centre, 46f, 13f, 15f);
        forge.Fill(metal, roundness: 0.8f, cap: 9f, lift: 3.4f);

        forge.Begin();
        for (var i = 0; i < 4; i++)
        {
            var angle = MathF.PI * (0.25f + i * 0.5f);
            forge.Capsule(
                Centre + MathF.Cos(angle) * 6f, 46f + MathF.Sin(angle) * 7f,
                Centre + MathF.Cos(angle) * 19f, 46f + MathF.Sin(angle) * 21f,
                5.5f, 2.2f);
        }
        forge.Fill(metal, roundness: 0.95f, cap: 7f, lift: 6.5f);

        forge.Begin();
        forge.Ellipse(Centre, 30f, 4.2f, 5.0f);
        forge.Fill(metal, roundness: 1.2f, cap: 8f, lift: 8.5f);

        forge.Begin();
        forge.Ellipse(Centre, 172f, 6.6f, 5.2f);
        forge.Fill(Fitting, roundness: 1.2f, cap: 6f, lift: 4f);
    }

    /// <summary>
    /// The shield, seen edge-on from behind as the off hand raises it.
    ///
    /// Drawn as its own sprite rather than as part of the weapon, because it is worn with any
    /// one-handed weapon and duplicating it into every blade would be four copies of the same
    /// picture that could drift apart.
    /// </summary>
    public static Texture2D Shield(GraphicsDevice device, ShieldDefinition shield)
    {
        if (Cache.TryGetValue("shield:" + shield.Id, out var cached)) return cached;

        var forge = new SpriteForge(Width, Height);
        var face = shield.Tier >= 2 ? Fitting : Timber;

        // The boards, as a tall oval. Period shields are hide over wood, not steel plate.
        forge.Begin();
        forge.Ellipse(Width / 2f, Height / 2f, 40f, 62f);
        forge.Fill(face, roundness: 0.34f, cap: 7f);

        // Rim, laid on top so it catches light along the edge and reads as a bound border.
        forge.Begin();
        forge.Ellipse(Width / 2f, Height / 2f, 40f, 62f);
        forge.Erase(Width / 2f, Height / 2f, 34f, 55f);
        forge.Fill(Iron, roundness: 1.2f, cap: 4.2f, lift: 4f);

        // The boss: the one thing that makes a shield read as a shield in silhouette.
        forge.Begin();
        forge.Ellipse(Width / 2f, Height / 2f, 13f, 13f);
        forge.Fill(Iron, roundness: 1.05f, cap: 9f, lift: 5.5f);

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(forge.Resolve(Key));
        Cache["shield:" + shield.Id] = texture;
        return texture;
    }

    /// <summary>
    /// An open hand, for the moment a spell leaves it.
    ///
    /// The player's own hand is the only part of them they ever see, and until now casting
    /// showed the sword instead — so the most distinctive thing a mage does looked exactly
    /// like the most ordinary thing a warrior does.
    /// </summary>
    public static Texture2D CastingHand(GraphicsDevice device)
    {
        if (Cache.TryGetValue("hand", out var cached)) return cached;

        var forge = new SpriteForge(Width, Height);

        // Palm, then four fingers spread and a thumb across. Seen from behind and below, as a
        // hand held up in front of you actually is.
        forge.Begin();
        forge.Ellipse(48f, 118f, 19f, 21f);
        forge.Fill(Skin, roundness: 0.7f, cap: 8f);

        forge.Begin();
        for (var i = 0; i < 4; i++)
        {
            var x = 33f + i * 10f;
            var lean = (i - 1.5f) * 3.4f;
            forge.Capsule(x, 106f, x + lean, 72f + MathF.Abs(i - 1.5f) * 7f, 5.0f, 3.8f);
        }
        forge.Fill(Skin, roundness: 1.0f, cap: 6.4f, lift: 2.6f);

        forge.Begin();
        forge.Capsule(32f, 124f, 16f, 106f, 5.4f, 4.2f);
        forge.Fill(Skin, roundness: 1.05f, cap: 6.6f, lift: 3.4f);

        forge.Begin();
        forge.Capsule(48f, 136f, 48f, 168f, 13f, 14f);
        forge.Fill(Skin, roundness: 0.8f, cap: 7f, lift: 0.4f);

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(forge.Resolve(Key));
        Cache["hand"] = texture;
        return texture;
    }

    private static void Bow(SpriteForge forge)
    {
        const float Centre = Width / 2f;

        // The limbs, as a chain of short capsules following a sine. A bow is one curve, and
        // approximating it in segments keeps the thickness taper honest along its length.
        forge.Begin();

        const int Segments = 22;
        for (var i = 0; i < Segments; i++)
        {
            var t0 = i / (float)Segments;
            var t1 = (i + 1) / (float)Segments;

            // Thickest at the grip, tapering to the nocks at both ends.
            forge.Capsule(
                LimbX(t0), LimbY(t0), LimbX(t1), LimbY(t1),
                2.0f + MathF.Sin(t0 * MathF.PI) * 1.8f,
                2.0f + MathF.Sin(t1 * MathF.PI) * 1.8f);
        }

        forge.Fill(Timber, roundness: 1.2f, cap: 3.4f);

        // The string, straight between the nocks.
        forge.Begin();
        forge.Capsule(Centre, 18f, Centre, Height - 26f, 0.9f, 0.9f);
        forge.Fill(Cord, roundness: 1.6f, cap: 1.6f, lift: 3.8f);

        forge.Begin();
        forge.Capsule(Centre + 24f, Height / 2f - 18f, Centre + 24f, Height / 2f + 18f, 4.4f, 4.2f);
        forge.Fill(Grip, roundness: 1.15f, cap: 4.4f, lift: 4.4f);

        static float LimbX(float t) => Width / 2f + MathF.Sin(t * MathF.PI) * 26f;
        static float LimbY(float t) => 18f + t * (Height - 44f);
    }

    private static void Fist(SpriteForge forge)
    {
        // Back of a closed hand: the mass of the fist, four knuckles proud of it, and the
        // wrist below. The knuckles are lifted so they catch light as separate rounds, which
        // is the only thing that stops a fist reading as a bag.
        forge.Begin();
        forge.Ellipse(48f, 116f, 20f, 17f);
        forge.Fill(Skin, roundness: 0.72f, cap: 8f);

        forge.Begin();
        for (var i = 0; i < 4; i++) forge.Ellipse(33f + i * 10f, 103f, 5.2f, 5.6f);
        forge.Fill(Skin, roundness: 1.25f, cap: 6.2f, lift: 5.4f);

        forge.Begin();
        forge.Capsule(48f, 132f, 48f, 164f, 12f, 13f);
        forge.Fill(Skin, roundness: 0.8f, cap: 7f, lift: 0.4f);
    }
}
