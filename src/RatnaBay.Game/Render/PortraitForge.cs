using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Faces, drawn in code, at a size worth looking at.
///
/// The world sprite in <see cref="CharacterSprites"/> is 32x48, which gives a face about eight
/// pixels tall — enough to read as a person and nowhere near enough to read as *someone*. A
/// portrait is 96x112 head-and-shoulders, so the same face gets roughly fifty-five pixels, and
/// that is the difference between a coloured blob and an expression.
///
/// Built on the same <see cref="SpriteForge"/> as everything else, which is the entire reason
/// this is affordable. Shapes write thickness rather than colour, the whole head is lit at the
/// end from the game's one lamp direction, and a brow drawn slightly proud of a forehead casts
/// its own shadow without anybody choosing a shade for it. That is what makes a two-pixel
/// change in a brow line legible as an emotion instead of a smudge.
///
/// **Expressions are re-forged, not overlaid.** Six per character means sixty textures, each a
/// fraction of a millisecond of work, all generated on first use and cached. Compositing an
/// eyebrow layer over a shared base would have been cheaper and would have lost the lighting,
/// which is the only thing making the geometry read.
/// </summary>
public static class PortraitForge
{
    /// <summary>Authoring size. Every coordinate below is in this space.</summary>
    public const int Width = 96;

    public const int Height = 112;

    /// <summary>
    /// How much bigger the stored texture is than the drawing.
    ///
    /// The UI sprite batch samples <c>LinearClamp</c>, which is right for everything else it
    /// draws and would turn a doubled portrait to mush. So the doubling happens here, by exact
    /// pixel replication, and the texture is drawn at one to one — the sampler never magnifies
    /// anything and every generated pixel stays a square. Cheaper than reopening the batch with
    /// a second sampler state, and it keeps the chunk that the rest of the game is drawn in.
    /// </summary>
    public const int Scale = 2;

    public const int TextureWidth = Width * Scale;
    public const int TextureHeight = Height * Scale;

    /// <summary>The same lamp as the item icons and the world sprites. Upper left.</summary>
    private static readonly Vector3 Key = new(-0.55f, -0.68f, 0.5f);

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>One face, one mood. Generated once and kept.</summary>
    public static Texture2D Get(GraphicsDevice device, string roomId, Expression mood)
    {
        var key = roomId + "/" + mood;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var face = FaceCatalog.Find(roomId)
                   ?? throw new ArgumentException($"no face for {roomId}", nameof(roomId));

        var texture = new Texture2D(device, TextureWidth, TextureHeight);
        texture.SetData(Double(Render(face, mood)));
        Cache[key] = texture;
        return texture;
    }

    /// <summary>Nearest-neighbour expansion. No filtering, by design.</summary>
    public static Color[] Double(Color[] source)
    {
        var expanded = new Color[TextureWidth * TextureHeight];

        for (var y = 0; y < TextureHeight; y++)
        {
            var row = y / Scale * Width;
            for (var x = 0; x < TextureWidth; x++)
                expanded[y * TextureWidth + x] = source[row + x / Scale];
        }

        return expanded;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }

    // ------------------------------------------------------------------ expression

    /// <summary>
    /// What a mood does to a face, as seven numbers.
    ///
    /// Two of them carry almost all the weight, and they are the two that survive translation:
    /// **inner brows up is sadness, inner brows down is anger**, in every population anybody
    /// has tested. Everything else here is reinforcement. Y grows downward, so grief takes a
    /// negative inner value.
    /// </summary>
    private readonly record struct Mood(
        float BrowInner, float BrowOuter, float BrowRaise,
        float EyeOpen, float MouthCurve, float MouthOpen, float Asymmetry);

    private static Mood Of(Expression mood) => mood switch
    {
        // The eyes do the smiling. A mouth curve with wide flat eyes reads as a rictus, which
        // is the single most common failure in generated faces.
        Expression.Warm => new Mood(0.4f, -0.9f, -0.5f, 0.62f, 2.6f, 0f, 0f),

        // Deliberately asymmetric: one brow up is the universal shorthand for a person who has
        // not decided about you yet, and symmetry would read as mild disapproval instead.
        Expression.Wary => new Mood(1.1f, -1.5f, 0f, 0.80f, -0.6f, 0f, 2.2f),

        Expression.Grieved => new Mood(-2.7f, 1.9f, 0f, 0.70f, -2.4f, 0f, 0f),
        Expression.Angry => new Mood(2.9f, -1.4f, 0.7f, 0.86f, -1.7f, 1.2f, 0f),
        Expression.Afraid => new Mood(-2.2f, -1.7f, -2.5f, 1.40f, -1.0f, 3.6f, 0f),
        _ => new Mood(0f, 0f, 0f, 1f, 0f, 0f, 0f)
    };

    // ------------------------------------------------------------------ drawing

    private static float Mix(float a, float b, float t) => a + (b - a) * t;

    private static Color Toward(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)Mix(from.R, to.R, t), (int)Mix(from.G, to.G, t), (int)Mix(from.B, to.B, t));
    }

    private static Color Darken(Color colour, float amount) =>
        Toward(colour, new Color(28, 22, 20), amount);

    /// <summary>
    /// One colour, unlit, ignoring the shading model entirely.
    ///
    /// Needed exactly once and worth the exception. <see cref="SpriteMaterial.FromBase"/> bends
    /// its shadows cool on purpose, which is what makes stone and cloth look like stone and
    /// cloth — and which gave every occupant of the fort pale blue eyes, because an iris is
    /// dark enough to live at the bottom of its own ramp. A four-pixel iris has no volume to
    /// describe anyway.
    /// </summary>
    /// <summary>Ornaments are brass, whatever the wearer is dressed in.</summary>
    private static readonly SpriteMaterial Brass =
        SpriteMaterial.FromBase(new Color(186, 146, 66), gloss: 0.6f);

    private static SpriteMaterial Flat(Color colour) => new()
    {
        Ramp = new[] { colour, colour, colour },
        Outline = colour
    };

    public static Color[] Render(FaceDescription face, Expression expression)
    {
        var mood = Of(expression);
        var forge = new SpriteForge(Width, Height);

        var skinColour = face.Palette.Skin;
        var skin = SpriteMaterial.FromBase(skinColour);
        // Under the jaw, so it wants to be a shade rather than a different material. At 0.34
        // it read as a brown column with a head balanced on it.
        var shadow = SpriteMaterial.FromBase(Darken(skinColour, 0.15f));

        // Hair greys with age rather than being authored grey, so one number moves a character
        // through a life and the palette never has to be edited to match.
        var hairColour = face.Age <= 0.55f
            ? face.Palette.Hair
            : Toward(face.Palette.Hair, new Color(176, 172, 164), (face.Age - 0.55f) / 0.45f * 0.8f);

        var hair = SpriteMaterial.FromBase(hairColour);
        var garment = SpriteMaterial.FromBase(face.Palette.Garment);
        var trim = SpriteMaterial.FromBase(face.Palette.Trim, gloss: 0.45f);
        // Flat for the same reason the iris is, and it was the actual culprit: an eye white is
        // a four-pixel ellipse with steep sides, so almost all of it landed on the cold end of
        // its ramp and every face in the fort had blue eyes. Nothing that small has volume.
        var white = Flat(new Color(232, 228, 219));
        var iris = Flat(face.Palette.Eye);

        const float Cx = 48f;

        var headRx = Mix(19f, 25f, face.Width);
        var jawRx = headRx * Mix(0.84f, 0.95f, face.Width);
        var shoulder = Mix(27f, 41f, face.Build);
        var eyeDx = Mix(9.6f, 11.4f, face.Width);

        const float EyeY = 46f;
        const float BrowY = 37f;
        const float MouthY = 66f;

        // --- shoulders and garment -------------------------------------------------
        forge.Begin();
        forge.Ellipse(Cx, 108f, shoulder, 30f);
        forge.Fill(garment, roundness: 0.55f, cap: 9f);

        forge.Begin();
        forge.Ellipse(Cx, 86f, shoulder * 0.46f, 8f);
        forge.Fill(trim, roundness: 0.8f, cap: 4f, lift: 2f);

        // --- neck ------------------------------------------------------------------
        forge.Begin();
        forge.Capsule(Cx, 64f, Cx, 88f, Mix(11f, 14f, face.Build), Mix(13f, 16f, face.Build));
        forge.Fill(shadow, roundness: 0.7f, cap: 7f, lift: 3f);

        // --- hair behind the head --------------------------------------------------
        HairBehind(forge, face, hair, trim, headRx);

        // --- ears ------------------------------------------------------------------
        forge.Begin();
        forge.Ellipse(Cx - headRx + 0.5f, 50f, 3.2f, 5.6f);
        forge.Ellipse(Cx + headRx - 0.5f, 50f, 3.2f, 5.6f);
        forge.Fill(skin, roundness: 0.9f, cap: 4f, lift: 6f);

        // --- the head --------------------------------------------------------------
        //
        // Two ellipses rather than one: a cranium and a jaw, overlapping. A single ellipse
        // gives an egg, and an egg has no age and no character — the taper between these two
        // is most of what separates the smith from the clerk.
        forge.Begin();
        forge.Ellipse(Cx, 41f, headRx, 26f);
        forge.Ellipse(Cx, 54f, jawRx, Mix(19f, 21f, face.Width));
        forge.Fill(skin, roundness: 2.4f, cap: 11f, lift: 12f);

        // Everything from here up is a feature sitting on the face, so it has to clear the
        // head's ceiling of lift plus cap.
        const float Face = 24f;

        // --- age, as two creases ---------------------------------------------------
        if (face.Age > 0.5f)
        {
            var depth = (face.Age - 0.5f) / 0.5f;
            forge.Begin();
            forge.Capsule(Cx - 5.5f, 53f, Cx - jawRx * 0.62f, 66f, 1.0f, 1.3f);
            forge.Capsule(Cx + 5.5f, 53f, Cx + jawRx * 0.62f, 66f, 1.0f, 1.3f);
            if (depth > 0.5f)
            {
                forge.Capsule(Cx - 9f, 31f, Cx + 9f, 31f, 0.9f, 0.9f);
                forge.Capsule(Cx - 7f, 27f, Cx + 7f, 27f, 0.8f, 0.8f);
            }

            forge.Fill(SpriteMaterial.FromBase(Darken(skinColour, 0.22f + depth * 0.16f)),
                roundness: 1f, cap: 1.6f, lift: Face + 0.4f);
        }

        // --- eyes ------------------------------------------------------------------
        var lidRy = 3.4f * mood.EyeOpen;

        forge.Begin();
        forge.Ellipse(Cx - eyeDx, EyeY, 4.7f, lidRy);
        forge.Ellipse(Cx + eyeDx, EyeY, 4.7f, lidRy);
        forge.Fill(white, roundness: 0.9f, cap: 2.2f, lift: Face);

        // The pupil rides low in a narrowed eye and high in a wide one, which is what actually
        // sells fear: a visible ring of white above the iris.
        var pupilY = EyeY + (mood.EyeOpen > 1f ? -0.5f : 0.35f);
        forge.Begin();
        forge.Ellipse(Cx - eyeDx, pupilY, 2.0f, MathF.Min(2.0f, lidRy));
        forge.Ellipse(Cx + eyeDx, pupilY, 2.0f, MathF.Min(2.0f, lidRy));
        forge.Fill(iris, roundness: 1f, cap: 2f, lift: Face + 1.4f);

        // --- brows -----------------------------------------------------------------
        var browR = Mix(1.1f, 2.5f, face.BrowWeight);
        var browTop = BrowY + mood.BrowRaise - (face.Age - 0.5f) * 1.5f;

        forge.Begin();
        // Left brow carries the asymmetry, so Wary has one raised without a second table.
        forge.Capsule(
            Cx - eyeDx - 5f, browTop + mood.BrowOuter - mood.Asymmetry,
            Cx - eyeDx + 4.6f, browTop + mood.BrowInner - mood.Asymmetry,
            browR * 0.8f, browR);
        forge.Capsule(
            Cx + eyeDx + 5f, browTop + mood.BrowOuter,
            Cx + eyeDx - 4.6f, browTop + mood.BrowInner,
            browR * 0.8f, browR);
        forge.Fill(hair, roundness: 1f, cap: 2.6f, lift: Face + 1f);

        // --- nose ------------------------------------------------------------------
        // Barely proud of the face on purpose. The first version stood three units above a
        // head capped at eighteen, which made its flanks steep enough to fall off the bottom of
        // the ramp — so every character in the fort wore a dark strip down the middle. A nose
        // at this scale is a bump that catches the lamp, not a shape.
        var noseTip = 44f + Mix(7f, 11f, face.NoseLength);
        forge.Begin();
        forge.Capsule(Cx, 44f, Cx, noseTip, 1.2f, 2.2f);
        forge.Ellipse(Cx, noseTip, 3.1f, 1.6f);
        forge.Fill(skin, roundness: 0.7f, cap: 2.0f, lift: Face - 1.2f);

        // --- mouth -----------------------------------------------------------------
        // Shorter than it wants to be. A mouth drawn the full width of the philtrum is a
        // straight dark bar at neutral, and a straight dark bar reads as a grimace — which put
        // every occupant of the fort in a bad mood before they said anything.
        var lipHalf = Mix(4.4f, 6.2f, face.Width);
        var corner = MouthY - mood.MouthCurve;
        var middle = MouthY + mood.MouthCurve * 0.35f;

        forge.Begin();
        forge.Capsule(Cx - lipHalf, corner, Cx, middle, 0.9f, 1.35f);
        forge.Capsule(Cx + lipHalf, corner, Cx, middle, 0.9f, 1.35f);
        if (mood.MouthOpen > 0f)
            forge.Ellipse(Cx, middle + mood.MouthOpen * 0.45f, lipHalf * 0.55f, mood.MouthOpen * 0.5f);
        forge.Fill(Flat(Darken(skinColour, 0.55f)), roundness: 1f, cap: 1.6f, lift: Face + 0.8f);

        // --- beard -----------------------------------------------------------------
        Facial(forge, face, hair, skinColour, Cx, jawRx, MouthY, lipHalf, Face);

        // --- hair in front, headwear, ornament -------------------------------------
        HairFront(forge, face, hair, trim, headRx, Face);
        Cover(forge, face, trim, hair, headRx, Face);
        Trinket(forge, face, trim, headRx, Face);

        return forge.Resolve(Key);
    }

    // ------------------------------------------------------------------ parts

    private static void HairBehind(SpriteForge forge, FaceDescription face,
        SpriteMaterial hair, SpriteMaterial trim, float headRx)
    {
        const float Cx = 48f;

        switch (face.Hair)
        {
            case HairStyle.Shaven:
                return;

            case HairStyle.Cropped:
                forge.Begin();
                forge.Ellipse(Cx, 36f, headRx + 1.6f, 23f);
                break;

            case HairStyle.Bound:
                forge.Begin();
                forge.Ellipse(Cx, 36f, headRx + 1.6f, 23f);
                forge.Ellipse(Cx, 11f, 8.5f, 7.5f);
                break;

            case HairStyle.Long:
                forge.Begin();
                forge.Ellipse(Cx, 38f, headRx + 4f, 27f);
                forge.Capsule(Cx - headRx - 3f, 36f, Cx - headRx - 5f, 86f, 5f, 7f);
                forge.Capsule(Cx + headRx + 3f, 36f, Cx + headRx + 5f, 86f, 5f, 7f);
                break;

            case HairStyle.Braid:
                forge.Begin();
                forge.Ellipse(Cx, 35f, headRx + 2f, 23f);
                forge.Capsule(Cx + headRx - 1f, 40f, Cx + headRx + 7f, 100f, 4.6f, 3.2f);
                break;

            case HairStyle.Cloth:
                // Drawn in trim rather than hair: a head-cloth is cloth, and it is the fastest
                // way to give one occupant a silhouette nobody else in the fort can have.
                forge.Begin();
                forge.Ellipse(Cx, 31f, headRx + 3f, 20f);
                forge.Capsule(Cx - headRx - 3.5f, 32f, Cx - headRx - 5f, 70f, 6f, 7f);
                forge.Capsule(Cx + headRx + 3.5f, 32f, Cx + headRx + 5f, 70f, 6f, 7f);
                forge.Rect(Cx - headRx - 11f, 62f, Cx + headRx + 11f, 74f);
                forge.Fill(trim, roundness: 0.62f, cap: 8f, lift: 4f);
                return;
        }

        forge.Fill(hair, roundness: 0.62f, cap: 8f, lift: 4f);
    }

    private static void HairFront(SpriteForge forge, FaceDescription face,
        SpriteMaterial hair, SpriteMaterial trim, float headRx, float faceLift)
    {
        if (face.Hair == HairStyle.Shaven) return;

        const float Cx = 48f;
        var material = face.Hair == HairStyle.Cloth ? trim : hair;

        // A receding hairline is one number and it ages a face more than any wrinkle does.
        var brow = 22f + MathF.Max(0f, face.Age - 0.55f) * 12f;

        forge.Begin();
        forge.Ellipse(Cx, brow, headRx - MathF.Max(0f, face.Age - 0.55f) * 8f, 9f);
        forge.Fill(material, roundness: 0.8f, cap: 5f, lift: faceLift + 0.5f);
    }

    private static void Cover(SpriteForge forge, FaceDescription face,
        SpriteMaterial trim, SpriteMaterial hair, float headRx, float faceLift)
    {
        const float Cx = 48f;

        switch (face.Headwear)
        {
            case Headwear.None:
                return;

            case Headwear.Cap:
                forge.Begin();
                forge.Ellipse(Cx, 19f, headRx + 1f, 11f);
                forge.Fill(trim, roundness: 0.7f, cap: 6f, lift: faceLift + 1.5f);
                return;

            case Headwear.Turban:
                forge.Begin();
                forge.Ellipse(Cx, 17f, headRx + 4f, 14f);
                forge.Fill(trim, roundness: 0.55f, cap: 8f, lift: faceLift + 1.5f);

                forge.Begin();
                forge.Capsule(Cx - headRx - 3f, 24f, Cx + headRx + 3f, 22f, 2.6f, 2.6f);
                forge.Fill(hair, roundness: 1f, cap: 3f, lift: faceLift + 3f);
                return;

            case Headwear.Helmet:
                // Stops above the eyes on purpose. A helmet that covers the brow costs the
                // character every expression they have, and he is the one who has to look
                // furious in the finale.
                forge.Begin();
                forge.Ellipse(Cx, 24f, headRx + 3f, 19f);
                forge.Erase(Cx, 46f, headRx + 5f, 18f);
                forge.Capsule(Cx, 26f, Cx, 50f, 2.8f, 2.2f);
                forge.Fill(trim, roundness: 0.6f, cap: 8f, lift: faceLift + 1.5f);
                return;
        }
    }

    private static void Trinket(SpriteForge forge, FaceDescription face,
        SpriteMaterial trim, float headRx, float faceLift)
    {
        const float Cx = 48f;

        switch (face.Ornament)
        {
            case Ornament.Earrings:
                forge.Begin();
                forge.Ellipse(Cx - headRx - 0.5f, 58f, 2.4f, 2.8f);
                forge.Ellipse(Cx + headRx + 0.5f, 58f, 2.4f, 2.8f);
                forge.Fill(Brass, roundness: 1f, cap: 3f, lift: faceLift + 2f);
                return;

            case Ornament.Tilaka:
                forge.Begin();
                forge.Capsule(Cx, 25f, Cx, 32f, 1.5f, 1.1f);
                forge.Fill(trim, roundness: 1f, cap: 2f, lift: faceLift + 2f);
                return;

            case Ornament.Necklace:
                forge.Begin();
                for (var i = -3; i <= 3; i++)
                    forge.Ellipse(Cx + i * 4.6f, 88f + MathF.Abs(i) * -0.9f, 2.1f, 2.1f);
                forge.Fill(Brass, roundness: 1f, cap: 3f, lift: 8f);
                return;
        }
    }

    private static void Facial(SpriteForge forge, FaceDescription face, SpriteMaterial hair,
        Color skinColour, float cx, float jawRx, float mouthY, float lipHalf, float faceLift)
    {
        switch (face.Beard)
        {
            case Beard.None:
                return;

            case Beard.Stubble:
                // Drawn in shadowed skin rather than hair, because stubble is not a shape — it
                // is a change of tone across one that is already there.
                forge.Begin();
                forge.Ellipse(cx, mouthY + 6f, jawRx * 0.86f, 12f);
                forge.Erase(cx, mouthY - 1f, lipHalf + 1.5f, 3.5f);
                forge.Fill(SpriteMaterial.FromBase(Darken(skinColour, 0.26f)),
                    roundness: 0.9f, cap: 2.4f, lift: faceLift - 0.4f);
                return;

            case Beard.Moustache:
                forge.Begin();
                forge.Capsule(cx - lipHalf - 1.5f, mouthY - 7.5f, cx, mouthY - 6.5f, 1.2f, 1.9f);
                forge.Capsule(cx + lipHalf + 1.5f, mouthY - 7.5f, cx, mouthY - 6.5f, 1.2f, 1.9f);
                forge.Fill(hair, roundness: 0.95f, cap: 3.4f, lift: faceLift + 1.2f);
                return;

            case Beard.Short:
                forge.Begin();
                forge.Ellipse(cx, mouthY + 8f, jawRx * 0.82f, 11f);
                forge.Capsule(cx - jawRx * 0.8f, mouthY - 2f, cx - jawRx * 0.86f, 52f, 3f, 3f);
                forge.Capsule(cx + jawRx * 0.8f, mouthY - 2f, cx + jawRx * 0.86f, 52f, 3f, 3f);
                forge.Erase(cx, mouthY, lipHalf + 1f, 3f);
                forge.Fill(hair, roundness: 0.75f, cap: 5f, lift: faceLift + 0.8f);
                return;

            case Beard.Full:
                forge.Begin();
                forge.Ellipse(cx, mouthY + 13f, jawRx * 0.98f, 17f);
                forge.Capsule(cx - jawRx * 0.9f, mouthY - 4f, cx - jawRx * 0.95f, 48f, 3.6f, 3.6f);
                forge.Capsule(cx + jawRx * 0.9f, mouthY - 4f, cx + jawRx * 0.95f, 48f, 3.6f, 3.6f);
                forge.Capsule(cx - lipHalf - 2f, mouthY - 5f, cx + lipHalf + 2f, mouthY - 5f, 2.2f, 2.2f);
                forge.Erase(cx, mouthY + 0.5f, lipHalf, 2.6f);
                forge.Fill(hair, roundness: 0.7f, cap: 6.5f, lift: faceLift + 0.8f);
                return;
        }
    }
}
