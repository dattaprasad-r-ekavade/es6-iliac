using UnityEngine;

/// <summary>
/// Draws a standing figure in code, in the grammar of miniature painting: flat colour fields
/// separated by a drawn contour, never by shading.
///
/// This replaces the head/torso/legs blob that stood in while the humanoid question was open.
/// W-13 established that the mesh, not the rig, is the blocker — so the answer is that
/// characters are not meshes. A sprite needs no rig, no retargeting, no knee joints and no
/// animation system, and a *contoured* sprite is the difference between reading as a deliberate
/// reference and reading as an unfinished placeholder.
///
/// **Why the outline is the whole trick.** Flat fields with no separation are greybox. The same
/// fields with a hard dark contour are a drawing. Miniature painters outlined for exactly this
/// reason: it lets you keep the colour flat, which is what makes the pigment sing, and still
/// read form. It costs one neighbour check per pixel.
///
/// The figure is derived deterministically from the actor's name, so the same character is the
/// same person in every session without anything being stored.
/// </summary>
public static class CharacterSprite
{
    /// <summary>Half the height, matching the billboard quad's aspect so texels stay square.</summary>
    public const int Width = 32;
    public const int Height = 64;

    public enum Headwear { None, Turban, Cap, Hood }

    /// <summary>What distinguishes one person from another at 32 px: silhouette and two colours.</summary>
    public struct Figure
    {
        public Color Garment;
        public Color Skin;
        /// <summary>Sash, headwear and trim. The second read after the garment.</summary>
        public Color Accent;
        public Headwear Head;
        /// <summary>0 slight, 1 broad. Scales shoulders and torso only.</summary>
        public float Build;
        /// <summary>A long robe rather than separated legs.</summary>
        public bool Robed;
    }

    /// <summary>
    /// <see cref="Sleeve"/> paints the same colour as <see cref="Garment"/> and exists only so
    /// the contour pass finds a boundary there. That is how an outline drawing separates an arm
    /// from a torso in the same cloth — a drawn line, not a shading ramp.
    /// </summary>
    private enum Region { Empty, Skin, Garment, Accent, Sleeve }

    /// <summary>
    /// Derive a figure from a name and a tint. Deterministic, so a character's appearance is a
    /// property of who they are rather than of when they were spawned.
    /// </summary>
    public static Figure From(string key, Color tint)
    {
        int h = StableHash(key ?? string.Empty);
        var palette = ArtDirection.Active.Palette;

        // Skin sits in a narrow warm band. It is derived from the palette rather than authored
        // so it cannot drift off-look, and the band is deliberately tight — variation between
        // characters comes from garment and silhouette, which read at 32 px, not from skin,
        // which does not.
        float skinRoll = ((h >> 3) & 0xFF) / 255f;
        var skin = Color.Lerp(
            new Color(0.72f, 0.53f, 0.36f),
            new Color(0.45f, 0.30f, 0.20f),
            skinRoll);

        var garment = tint;
        garment.a = 1f;

        // Accent is the garment pushed toward a different pigment, so the two always relate.
        var accent = Color.Lerp(garment, ((h >> 11) & 1) == 0 ? palette.Arid : palette.Ocean, 0.60f);
        accent.a = 1f;

        return new Figure
        {
            Garment = garment,
            Skin = skin,
            Accent = accent,
            Head = (Headwear)(((h >> 17) & 0xFF) % 4),
            Build = ((h >> 23) & 0xFF) / 255f,
            Robed = ((h >> 5) & 1) == 0
        };
    }

    public static Texture2D Build(in Figure figure)
    {
        var preset = ArtDirection.Active;
        bool contoured = ArtDirection.UsesContour(preset);
        var contour = preset.Palette.Contour;

        var regions = new Region[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            float v = (y + 0.5f) / Height;
            for (int x = 0; x < Width; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / Width - 0.5f);
                regions[y * Width + x] = Sample(v, dx, figure);
            }
        }

        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false)
        {
            name = "T_Figure",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        var pixels = new Color32[Width * Height];
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                var region = regions[i];

                if (region == Region.Empty) { pixels[i] = clear; continue; }

                // The outline: any filled pixel touching a different region — including the
                // outside — becomes contour. One pass, and it draws the silhouette edge and the
                // internal seams (sleeve against torso, neck against collar) at the same time.
                bool edge = contoured && (
                    Neighbour(regions, x - 1, y) != region ||
                    Neighbour(regions, x + 1, y) != region ||
                    Neighbour(regions, x, y - 1) != region ||
                    Neighbour(regions, x, y + 1) != region);

                pixels[i] = edge ? contour : Colour(region, figure);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return texture;
    }

    private static Region Neighbour(Region[] regions, int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return Region.Empty;
        return regions[y * Width + x];
    }

    private static Color Colour(Region region, in Figure figure) => region switch
    {
        Region.Skin => figure.Skin,
        Region.Accent => figure.Accent,
        _ => figure.Garment   // Garment and Sleeve are the same cloth
    };

    /// <summary>
    /// The figure's profile, as a half-width per height band. Everything about the silhouette
    /// lives here — proportions are what read at this resolution, so this is the part worth
    /// getting right and the surface detail is the part that does not matter.
    /// </summary>
    private static Region Sample(float v, float dx, in Figure figure)
    {
        float broad = Mathf.Lerp(0.90f, 1.12f, Mathf.Clamp01(figure.Build));

        // --- headwear, above the head ---------------------------------------
        if (figure.Head == Headwear.Hood && v >= 0.78f)
        {
            float hood = Taper(v, 0.78f, 1.00f, 0.24f);
            return dx < hood ? Region.Accent : Region.Empty;
        }

        if (v >= 0.93f)
        {
            switch (figure.Head)
            {
                case Headwear.Turban:
                    return dx < Taper(v, 0.93f, 1.00f, 0.25f) ? Region.Accent : Region.Empty;
                case Headwear.Cap:
                    return v < 0.98f && dx < 0.19f ? Region.Accent : Region.Empty;
                default:
                    return dx < Taper(v, 0.93f, 0.99f, 0.17f) ? Region.Skin : Region.Empty;
            }
        }

        // --- head and neck ---------------------------------------------------
        if (v >= 0.82f) return dx < 0.17f ? Region.Skin : Region.Empty;
        if (v >= 0.78f) return dx < 0.09f ? Region.Skin : Region.Empty;

        // --- shoulders, torso and arms ---------------------------------------
        if (v >= 0.52f)
        {
            // Shoulders are the widest point and the main signal of build.
            float torso = Mathf.Lerp(0.27f, 0.34f, Mathf.InverseLerp(0.52f, 0.76f, v)) * broad;
            float arms = torso + 0.09f;

            // Arms hang beside the torso down to mid-chest. They are the same cloth, so they
            // carry the same colour and are separated by a drawn seam instead.
            if (v < 0.74f && dx < arms) return dx < torso ? Region.Garment : Region.Sleeve;
            return dx < torso ? Region.Garment : Region.Empty;
        }

        // --- sash at the waist -------------------------------------------------
        if (v >= 0.47f) return dx < 0.26f ? Region.Accent : Region.Empty;

        // --- lower body ---------------------------------------------------------
        if (figure.Robed)
        {
            // A robe flares toward the hem, which is the strongest silhouette difference
            // available between two characters at this size.
            float hem = Mathf.Lerp(0.40f, 0.25f, Mathf.InverseLerp(0.04f, 0.47f, v));
            return v >= 0.03f && dx < hem ? Region.Garment : Region.Empty;
        }

        if (v >= 0.04f)
        {
            // Two legs with a gap. The gap has to be at least two texels or the contour pass
            // closes it and the figure reads as a robe anyway.
            return dx < 0.27f && dx > 0.05f ? Region.Garment : Region.Empty;
        }

        return dx < 0.30f ? Region.Accent : Region.Empty;   // feet
    }

    /// <summary>Rounds the top of a shape so headwear does not read as a box.</summary>
    private static float Taper(float v, float from, float to, float halfWidth)
    {
        float t = Mathf.InverseLerp(from, to, v);
        return halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h == 0 ? 1 : h & 0x7fffffff;
        }
    }
}
