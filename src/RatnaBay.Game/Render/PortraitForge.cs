using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Dialogue portraits: one face per occupant, six moods each, painted rather than pixelled.
///
/// **This is a second renderer on purpose.** The first attempt scaled the world-sprite
/// technique up and it was the wrong answer: <see cref="SpriteForge"/> writes thickness, so a
/// face can have bumps but never hollows, and it quantises into a five-step ramp, which at
/// thirty-two pixels is the style and at three hundred is just banding. A talking head needs
/// eye sockets that sit *behind* the cheeks beside them and shading that does not step.
///
/// So the geometry is a real height field — see <see cref="FaceField"/> — assembled from
/// half-ellipsoids fused with a smooth maximum, then *carved* for the sockets, the nostrils,
/// the fold beside the nose and the seam of the lips, then lit continuously with a key, a cool
/// fill, ambient occlusion read out of the field itself, a rim, and a warm scatter band across
/// the terminator so skin reads as flesh rather than plastic.
///
/// The world sprites are untouched. A fort occupant is the same person in two techniques: a
/// 32x48 billboard out in the world, and this at close range — which is the arrangement
/// Morrowind and the early Fallouts used, for the same reason.
/// </summary>
public static class PortraitForge
{
    public const int Width = 288;
    public const int Height = 384;

    private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

    /// <summary>One face, one mood. Generated on first use and kept.</summary>
    public static Texture2D Get(GraphicsDevice device, string roomId, Expression mood)
    {
        var key = roomId + "/" + mood;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var face = FaceCatalog.Find(roomId)
                   ?? throw new ArgumentException($"no face for {roomId}", nameof(roomId));

        var texture = new Texture2D(device, Width, Height);
        texture.SetData(Render(face, mood));
        Cache[key] = texture;
        return texture;
    }

    public static void Clear()
    {
        foreach (var texture in Cache.Values) texture.Dispose();
        Cache.Clear();
    }

    // ------------------------------------------------------------------ expression

    /// <summary>
    /// What a mood does to a face.
    ///
    /// Two of these carry almost all the weight and they are the two that survive translation:
    /// **inner brows up is sadness, inner brows down is anger**, in every population anybody
    /// has tested. The rest is reinforcement. Y grows downward, so grief takes a negative inner
    /// value. Units are output pixels, so every figure is roughly three times what the
    /// low-resolution version used.
    /// </summary>
    private readonly record struct Mood(
        float BrowInner, float BrowOuter, float BrowRaise,
        float EyeOpen, float MouthCurve, float MouthOpen, float Asymmetry, float Tension);

    private static Mood Of(Expression mood) => mood switch
    {
        // The eyes do the smiling. A mouth curve over wide flat eyes is a rictus, and that is
        // the commonest failure in a generated face.
        Expression.Warm => new Mood(1f, -3f, -2f, 0.58f, 9f, 0f, 0f, 0.15f),

        // Asymmetric on purpose: one brow up is the shorthand for somebody who has not decided
        // about you, where symmetry would read as flat disapproval.
        Expression.Wary => new Mood(4f, -5f, 0f, 0.82f, -2f, 0f, 7f, 0.30f),

        Expression.Grieved => new Mood(-9f, 6f, 0f, 0.68f, -8f, 0f, 0f, 0.25f),
        Expression.Angry => new Mood(10f, -5f, 3f, 0.84f, -6f, 4f, 0f, 0.75f),
        Expression.Afraid => new Mood(-8f, -6f, -9f, 1.42f, -4f, 13f, 0f, 0.45f),
        _ => new Mood(0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f)
    };

    // ------------------------------------------------------------------ helpers

    private static float Mix(float a, float b, float t) => a + (b - a) * t;

    private static Color Toward(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)Mix(from.R, to.R, t), (int)Mix(from.G, to.G, t), (int)Mix(from.B, to.B, t));
    }

    private static Color Darken(Color colour, float amount) =>
        Toward(colour, new Color(38, 24, 22), amount);

    private const float Cx = Width / 2f;

    // ------------------------------------------------------------------ the face

    public static Color[] Render(FaceDescription face, Expression expression)
    {
        var mood = Of(expression);
        var field = new FaceField(Width, Height);

        var skin = face.Palette.Skin;

        var hairColour = face.Age <= 0.55f
            ? face.Palette.Hair
            : Toward(face.Palette.Hair, new Color(186, 182, 174), (face.Age - 0.55f) / 0.45f * 0.85f);

        var headRx = Mix(80f, 94f, face.Width);
        var jawRx = headRx * Mix(0.74f, 0.86f, face.Width);
        var shoulder = Mix(118f, 168f, face.Build);

        const float EyeY = 190f;
        const float MouthY = 274f;
        var eyeDx = headRx * 0.42f;
        var browY = 166f + mood.BrowRaise - (face.Age - 0.5f) * 5f;
        var lipHalf = Mix(21f, 27f, face.Width);

        // --- shoulders and neck ----------------------------------------------------
        field.Ellipsoid(Cx, 452f, shoulder, 100f, 62f, -104f, face.Palette.Garment, blend: 20f);
        field.Tube(Cx, 286f, Cx, 376f, Mix(42f, 50f, face.Build), Mix(50f, 60f, face.Build),
            42f, -56f, Darken(skin, 0.24f), blend: 14f);

        // --- the head --------------------------------------------------------------
        //
        // **One ellipsoid, and nothing else on the face is a solid.** Everything below is a
        // Bump or a Carve, which add nothing at their own boundary and therefore have no
        // boundary to see. The previous attempt built the brow, the cheeks and the lids out of
        // ellipsoids, and every one of them drew its own hard oval rim across the face.
        //
        // Proportion is the classical one, and it is worth being literal about because eyes in
        // the wrong place is the fault no amount of shading recovers from: a head is about
        // seven parts tall to five wide, and the eye line sits halfway down it.
        field.Ellipsoid(Cx, 190f, headRx, headRx * 1.40f, 62f, 0f, skin, blend: 10f);

        // Jaw and temples, taken away. The lower face pulls in toward the chin and the skull
        // narrows above the ears, which is what stops a head reading as an egg.
        field.Carve(Cx - headRx, 278f, 52f, 62f, depth: Mix(22f, 11f, face.Width), softness: 1.9f);
        field.Carve(Cx + headRx, 278f, 52f, 62f, depth: Mix(22f, 11f, face.Width), softness: 1.9f);
        field.Carve(Cx - headRx, 132f, 42f, 62f, depth: 26f, softness: 1.2f);
        field.Carve(Cx + headRx, 132f, 42f, 62f, depth: 26f, softness: 1.2f);
        field.Carve(Cx, 78f, headRx * 0.92f, 50f, depth: 16f, softness: 1.4f);

        // Cheekbone: wide, shallow, and shaded from underneath by the hollow below it. Nine
        // units of relief with a hollow under it is all a cheekbone has ever needed to be.
        field.Bump(Cx - 48f, 214f, 46f, 30f, amount: 9f);
        field.Bump(Cx + 48f, 214f, 46f, 30f, amount: 9f);
        field.Carve(Cx - 52f, 252f, 34f, 26f, depth: 8f);
        field.Carve(Cx + 52f, 252f, 34f, 26f, depth: 8f);

        field.Ellipsoid(Cx - headRx + 3f, 204f, 12f, 25f, 10f, -4f, skin, blend: 6f);
        field.Ellipsoid(Cx + headRx - 3f, 204f, 12f, 25f, 10f, -4f, skin, blend: 6f);

        // --- brow ridge, and the sockets under it ----------------------------------
        var browRidge = Mix(5f, 13f, face.BrowWeight);
        field.Bump(Cx - eyeDx, browY + 6f, 44f, 16f, amount: browRidge);
        field.Bump(Cx + eyeDx, browY + 6f, 44f, 16f, amount: browRidge);
        field.Bump(Cx, browY + 10f, 15f, 16f, amount: browRidge * 0.6f);

        // The whole reason this renderer exists. An eye sits in a hollow.
        field.Carve(Cx - eyeDx, EyeY - 2f, 34f, 25f, depth: 16f, softness: 1.3f);
        field.Carve(Cx + eyeDx, EyeY - 2f, 34f, 25f, depth: 16f, softness: 1.3f);

        // --- nose ------------------------------------------------------------------
        var noseBase = 240f + Mix(0f, 12f, face.NoseLength);

        field.BeginStroke();
        for (var i = 0; i <= 24; i++)
        {
            var t = i / 24f;
            var y = Mix(browY + 14f, noseBase - 4f, t);
            field.Bump(Cx, y, Mix(15f, 21f, t), 14f, amount: Mix(4f, 11f, t), softness: 2.2f);
        }

        field.Bump(Cx, noseBase, 21f, 15f, amount: 13f, softness: 2f);
        field.Bump(Cx - 18f, noseBase + 4f, 13f, 10f, amount: 7f, softness: 2f);
        field.Bump(Cx + 18f, noseBase + 4f, 13f, 10f, amount: 7f, softness: 2f);
        field.EndStroke();

        // In this tradition a nose is **drawn, not modelled**: one line down the shaded side,
        // the underside of the tip, and two marks for the nostrils. Trying to get it from
        // geometry alone gave either a blade down the middle of the face or nothing at all,
        // because a flat style has no gradient to describe a form with.
        field.BeginStroke();
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20f;
            field.Stain(Cx + Mix(9f, 17f, t * t), Mix(noseBase - 42f, noseBase + 1f, t),
                3.6f, 5f, Darken(skin, 0.42f), strength: Mix(0.10f, 0.62f, t * t),
                softness: 1.3f);
        }

        field.EndStroke();

        // Under the tip, and the two wings. These three marks are the nose; everything above
        // them is only the hint that gets the eye down to them.
        field.Stain(Cx, noseBase + 8f, 15f, 4.5f, Darken(skin, 0.5f), strength: 0.62f,
            softness: 1.4f);
        field.Stain(Cx - 12f, noseBase + 6f, 7f, 5f, Darken(skin, 0.68f), strength: 0.9f,
            softness: 1.1f);
        field.Stain(Cx + 12f, noseBase + 6f, 7f, 5f, Darken(skin, 0.68f), strength: 0.9f,
            softness: 1.1f);
        field.Carve(Cx, noseBase + 17f, 8f, 9f, depth: 4f);

        // The fold from the nostril to the corner of the mouth. Present on everybody, deeper
        // with age, and one of the strongest cues that a face is a face.
        field.BeginStroke();
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20f;
            var cut = 5f + MathF.Max(0f, face.Age - 0.3f) * 16f;
            var taper = cut * (1f - t * 0.75f);
            field.Carve(Cx - Mix(22f, 32f, t), Mix(noseBase + 6f, 278f, t), 8f, 10f, depth: taper);
            field.Carve(Cx + Mix(22f, 32f, t), Mix(noseBase + 6f, 278f, t), 8f, 10f, depth: taper);
        }

        field.EndStroke();

        // --- mouth -----------------------------------------------------------------
        var corner = MouthY - mood.MouthCurve;
        var middle = MouthY + mood.MouthCurve * 0.3f;
        var lip = Toward(skin, new Color(158, 82, 76), 0.5f);

        field.Bump(Cx, middle - 6f, lipHalf, 10f, amount: 10f);
        field.Bump(Cx, middle + 8f, lipHalf * 0.94f, 12f, amount: 13f);
        field.Stain(Cx, middle - 6f, lipHalf, 9f, lip, strength: 0.92f, softness: 0.7f, gloss: 0.2f);
        field.Stain(Cx, middle + 7f, lipHalf * 0.92f, 11f, lip, strength: 0.92f, softness: 0.7f,
            gloss: 0.3f);

        // The seam, as a curve of small carves, so the corners travel with the mood.
        field.BeginStroke();
        for (var i = -16; i <= 16; i++)
        {
            var t = i / 16f;
            field.Carve(Cx + t * lipHalf, Mix(middle, corner, t * t), 6f, 3.5f, depth: 12f);
        }

        field.EndStroke();

        // Chin, and the shadow under the lower lip that gives it a front plane.
        field.Carve(Cx, middle + 22f, 22f, 12f, depth: 8f);
        field.Bump(Cx, 300f, Mix(26f, 34f, face.Width), 24f, amount: 11f);

        if (mood.MouthOpen > 0f)
        {
            field.Carve(Cx, middle + 1f, lipHalf * 0.68f, mood.MouthOpen, depth: 30f, softness: 1.1f);
            field.Stain(Cx, middle + 1f, lipHalf * 0.62f, mood.MouthOpen * 0.9f,
                new Color(48, 24, 26), strength: 0.92f, softness: 0.7f);
        }

        // --- age -------------------------------------------------------------------
        if (face.Age > 0.45f)
        {
            var depth = (face.Age - 0.45f) / 0.55f;

            if (depth > 0.3f)
                for (var line = 0; line < 3; line++)
                    field.Carve(Cx, 132f + line * 13f, headRx * 0.56f, 3.5f, depth: 2f + depth * 5f);

            // Under-eye hollows, which is most of what makes an old face old.
            field.Carve(Cx - eyeDx, EyeY + 22f, 28f, 12f, depth: depth * 10f);
            field.Carve(Cx + eyeDx, EyeY + 22f, 28f, 12f, depth: depth * 10f);
            field.Carve(Cx - headRx * 0.74f, 288f, 24f, 22f, depth: depth * 9f);
            field.Carve(Cx + headRx * 0.74f, 288f, 24f, 22f, depth: depth * 9f);
        }

        // --- eyes ------------------------------------------------------------------
        Eye(field, face, Cx - eyeDx, EyeY, mood, skin, hairColour, browY, -1f);
        Eye(field, face, Cx + eyeDx, EyeY, mood, skin, hairColour, browY, 1f);

        // --- hair, beard, headwear, ornament ---------------------------------------
        Hair(field, face, hairColour, headRx);
        Facial(field, face, hairColour, skin, jawRx, MouthY, lipHalf);
        Cover(field, face, headRx);
        Trinket(field, face, headRx);

        return field.ResolveFresco(Wall);
    }

    /// <summary>
    /// The wall these are painted on.
    ///
    /// Six colours, taken from what a painter in this period actually had: red ochre for the
    /// drawn line, a burnt brown for the shaded planes, lime white for the lit ones, and a
    /// yellow-ochre plaster ground. No blue and no green — lapis existed and was for gods.
    /// </summary>
    private static readonly FaceField.Fresco Wall = new(
        Line: new Color(72, 32, 26),
        Shade: new Color(104, 52, 38),
        Highlight: new Color(242, 230, 206),
        Ground: new Color(146, 112, 76),
        GroundDark: new Color(104, 78, 54));

    /// <summary>
    /// One eye: colour laid into a hollow, not a ball with lids stacked over it.
    ///
    /// The lids are the expression, and the way to move them is to change the shape of the
    /// visible white rather than to draw a lid on top of it. Drawing lids as solids gave every
    /// character two hard ovals around each eye; painting the aperture instead lets a lid close
    /// to a slit without any edge appearing that was not already there.
    /// </summary>
    private static void Eye(FaceField field, FaceDescription face, float cx, float cy,
        Mood mood, Color skin, Color hairColour, float browY, float side)
    {
        var open = mood.EyeOpen;
        var aperture = 13f * open;

        // A slight convexity where the eyeball presses against the lids.
        field.Bump(cx, cy, 25f, 17f, amount: 12f, softness: 1.2f);

        // The almond, built as three overlapping stains rather than one ellipse: full at the
        // inner corner, tapering past the outer one. That taper is the single most recognisable
        // thing about a face in this tradition.
        field.Stain(cx, cy, 22f, aperture, new Color(206, 196, 180), strength: 1f,
            softness: 0.30f);
        field.Stain(cx - side * 8f, cy, 16f, aperture * 0.94f, new Color(206, 196, 180),
            strength: 1f, softness: 0.30f);
        field.Stain(cx + side * 14f, cy + 1.5f, 13f, aperture * 0.62f,
            new Color(206, 196, 180), strength: 1f, softness: 0.34f);

        // The iris rides high in a wide eye, which is what actually sells fear: a ring of white
        // showing above it.
        var irisY = cy + (open > 1f ? -1.5f : 1.5f);
        var irisR = MathF.Min(9.5f, aperture + 3f);
        field.Stain(cx, irisY, irisR, irisR, face.Palette.Eye, strength: 1f, softness: 0.3f,
            gloss: 0.85f);
        field.Stain(cx, irisY, irisR * 0.42f, irisR * 0.42f, new Color(14, 10, 10), strength: 1f,
            softness: 0.28f);
        field.Stain(cx - 3.2f, irisY - 3.2f, 2.6f, 2.6f, new Color(255, 255, 252),
            strength: 0.95f, softness: 0.45f);

        // The lid line: heavy, drawn, and carried out past the outer corner into a flick. It
        // is a painted line rather than eyelashes, and it is what makes the eye read as drawn
        // instead of rendered.
        for (var i = -8; i <= 9; i++)
        {
            var t = i / 8f;
            var x = cx + side * t * 24f;
            var lift = t < 0f ? t * t * 3f : t * t * 5f;
            field.Stain(x, cy - aperture * (1f - t * t * 0.25f) + 1f - lift,
                4.5f, 3.4f - MathF.Abs(t) * 1.1f, new Color(58, 30, 26),
                strength: 0.95f, softness: 0.7f);
        }

        field.Carve(cx, cy - aperture - 7f, 24f, 7f, depth: 7f);

        // --- brow ------------------------------------------------------------------
        var inner = mood.BrowInner - (side < 0f ? mood.Asymmetry : 0f);
        var outer = mood.BrowOuter - (side < 0f ? mood.Asymmetry : 0f);
        var thickness = Mix(3.6f, 6.2f, face.BrowWeight);

        field.BeginStroke();
        for (var i = 0; i <= 32; i++)
        {
            var t = i / 32f;
            var x = cx + side * Mix(-19f, 27f, t);

            // The arch: highest in the middle of its own length, which is what separates a brow
            // from a bar. Mood tilts the two ends and the curve carries between them.
            var arch = -MathF.Sin(t * MathF.PI) * 5f;
            var y = browY + Mix(inner, outer, t) + arch;
            var r = thickness * (1f - t * 0.55f);

            field.Bump(x, y, r * 2.1f, r, amount: 3f);
            field.Stain(x, y, r * 2.3f, r * 1.2f, hairColour, strength: 0.95f, softness: 0.85f);
        }

        field.EndStroke();

        // The corrugator bunch: the vertical pinch between the brows that comes with effort.
        if (mood.Tension > 0.2f)
            field.Carve(cx - side * 13f, browY + 12f, 6f, 17f, depth: mood.Tension * 13f);
    }

    // ------------------------------------------------------------------ parts

    private static void Hair(FaceField field, FaceDescription face, Color colour, float headRx)
    {
        if (face.Hair == HairStyle.Shaven)
        {
            // Not nothing: a shaven head still shows where the hair would be.
            field.Stain(Cx, 130f, headRx * 0.92f, 74f, Darken(colour, 0.1f), strength: 0.30f,
                softness: 2.2f);
            return;
        }

        var cloth = face.Hair == HairStyle.Cloth;
        var tint = cloth ? face.Palette.Trim : colour;

        // A receding hairline is one number and ages a face harder than any wrinkle.
        var recede = MathF.Max(0f, face.Age - 0.55f) * 46f;

        field.Ellipsoid(Cx, 128f + recede * 0.5f, headRx + (cloth ? 12f : 6f),
            80f - recede * 0.35f, cloth ? 34f : 30f, cloth ? 42f : 36f, tint,
            blend: cloth ? 22f : 16f, gloss: cloth ? 0.05f : 0.14f);

        switch (face.Hair)
        {
            case HairStyle.Cropped:
                break;

            case HairStyle.Bound:
                field.Ellipsoid(Cx, 56f, 30f, 28f, 30f, 20f, colour, blend: 12f, gloss: 0.16f);
                break;

            case HairStyle.Long:
                // Clumps rather than one mass, so it reads as hair and not as a hood.
                for (var i = 0; i < 4; i++)
                {
                    var t = i / 3f;
                    var x = headRx + 4f + t * 12f;
                    field.Tube(Cx - x, 120f + t * 18f, Cx - x - 8f, 300f - t * 30f,
                        20f - t * 4f, 15f - t * 3f, 26f, -14f, colour, blend: 12f, gloss: 0.15f);
                    field.Tube(Cx + x, 120f + t * 18f, Cx + x + 8f, 300f - t * 30f,
                        20f - t * 4f, 15f - t * 3f, 26f, -14f, colour, blend: 12f, gloss: 0.15f);
                }

                break;

            case HairStyle.Braid:
                for (var i = 0; i < 7; i++)
                {
                    var t = i / 6f;
                    field.Ellipsoid(Cx + headRx + 6f + t * 16f, 168f + t * 150f,
                        16f - t * 5f, 14f - t * 4f, 18f, -10f, colour, blend: 9f, gloss: 0.2f);
                }

                break;

            case HairStyle.Cloth:
                field.Tube(Cx - headRx - 10f, 120f, Cx - headRx - 16f, 250f, 22f, 26f, 30f, -20f,
                    face.Palette.Trim, blend: 18f);
                field.Tube(Cx + headRx + 10f, 120f, Cx + headRx + 16f, 250f, 22f, 26f, 30f, -20f,
                    face.Palette.Trim, blend: 18f);
                field.Ellipsoid(Cx, 250f, headRx + 30f, 34f, 26f, -30f, face.Palette.Trim,
                    blend: 20f);
                break;
        }
    }

    private static void Facial(FaceField field, FaceDescription face, Color colour, Color skin,
        float jawRx, float mouthY, float lipHalf)
    {
        switch (face.Beard)
        {
            case Beard.None:
                return;

            case Beard.Stubble:
                // A change of tone across a shape that is already there, not a shape.
                field.Stain(Cx, mouthY + 26f, jawRx * 0.9f, 52f, Darken(skin, 0.42f),
                    strength: 0.42f, softness: 1.8f);
                field.Stain(Cx, mouthY - 16f, lipHalf + 10f, 14f, Darken(skin, 0.42f),
                    strength: 0.34f, softness: 1.6f);
                return;

            case Beard.Moustache:
                field.Bump(Cx, mouthY - 19f, lipHalf + 16f, 10f, amount: 4f, softness: 2f);
                field.Stain(Cx, mouthY - 19f, lipHalf + 17f, 10f, colour, strength: 0.96f,
                    softness: 1.4f);
                return;

            case Beard.Short:
                field.Bump(Cx, mouthY + 30f, jawRx * 0.9f, 46f, amount: 16f, softness: 1.3f);
                field.Stain(Cx, mouthY + 30f, jawRx * 0.92f, 48f, colour, strength: 0.95f,
                    softness: 1.2f);
                field.Stain(Cx - jawRx * 0.78f, mouthY - 6f, 20f, 34f, colour, strength: 0.9f,
                    softness: 1.3f);
                field.Stain(Cx + jawRx * 0.78f, mouthY - 6f, 20f, 34f, colour, strength: 0.9f,
                    softness: 1.3f);
                field.Stain(Cx, mouthY - 20f, lipHalf + 9f, 11f, colour, strength: 0.9f,
                    softness: 1f);
                return;

            case Beard.Full:
                field.Ellipsoid(Cx, mouthY + 48f, jawRx, 62f, 44f, 4f, colour, blend: 22f);
                field.Tube(Cx - jawRx * 0.92f, mouthY - 28f, Cx - jawRx * 0.62f, mouthY + 52f,
                    20f, 28f, 32f, 0f, colour, blend: 16f);
                field.Tube(Cx + jawRx * 0.92f, mouthY - 28f, Cx + jawRx * 0.62f, mouthY + 52f,
                    20f, 28f, 32f, 0f, colour, blend: 16f);
                field.Ellipsoid(Cx, mouthY - 21f, lipHalf + 12f, 14f, 14f, 38f, colour, blend: 8f);
                return;
        }
    }

    private static void Cover(FaceField field, FaceDescription face, float headRx)
    {
        switch (face.Headwear)
        {
            case Headwear.None:
                return;

            case Headwear.Cap:
                field.Ellipsoid(Cx, 96f, headRx + 4f, 44f, 40f, 22f, face.Palette.Trim,
                    blend: 16f, gloss: 0.12f);
                return;

            case Headwear.Turban:
                field.Ellipsoid(Cx, 86f, headRx + 14f, 52f, 48f, 20f, face.Palette.Trim,
                    blend: 20f, gloss: 0.08f);
                for (var i = 0; i < 4; i++)
                    field.Carve(Cx - headRx + i * headRx * 0.66f, 84f + i * 6f, 40f, 7f, depth: 9f);
                return;

            case Headwear.Helmet:
                // Stops above the brow. A helmet over the brow costs the character every
                // expression they have, and he is the one who has to look furious in the finale.
                field.Ellipsoid(Cx, 116f, headRx + 12f, 64f, 58f, 16f, face.Palette.Trim,
                    blend: 14f, gloss: 0.55f);
                field.Ellipsoid(Cx, 152f, 13f, 46f, 20f, 62f, face.Palette.Trim,
                    blend: 8f, gloss: 0.6f);
                return;
        }
    }

    private static readonly Color BrassColour = new(196, 154, 70);

    private static void Trinket(FaceField field, FaceDescription face, float headRx)
    {
        switch (face.Ornament)
        {
            case Ornament.Earrings:
                field.Ellipsoid(Cx - headRx - 2f, 228f, 11f, 13f, 12f, 10f, BrassColour,
                    blend: 4f, gloss: 0.9f);
                field.Ellipsoid(Cx + headRx + 2f, 228f, 11f, 13f, 12f, 10f, BrassColour,
                    blend: 4f, gloss: 0.9f);
                return;

            case Ornament.Tilaka:
                field.Stain(Cx, 116f, 7f, 22f, new Color(168, 44, 40), strength: 0.95f,
                    softness: 0.6f);
                return;

            case Ornament.Necklace:
                for (var i = -4; i <= 4; i++)
                    field.Ellipsoid(Cx + i * 20f, 372f - MathF.Abs(i) * 4f, 10f, 10f, 10f, -46f,
                        BrassColour, blend: 4f, gloss: 0.9f);
                return;
        }
    }
}
