using UnityEngine;

/// <summary>
/// Single source of truth for the render-layer look: palette, fog, sky, filtering and
/// grading. Sits alongside <see cref="WorldLayout"/> (geometry) so the two halves of the
/// world description live in one place each.
///
/// The look is deliberately code, not art. Low-poly settings like Morrowind's read the way
/// they do because of fog, a tight palette and flat lighting — none of which require an
/// artist. Assets are held to the palette rather than the palette adapting to the assets.
/// </summary>
public static class ArtDirection
{
    public enum Look
    {
        /// <summary>Morrowind-descended: filtered textures, softer fog, painterly grade.</summary>
        MorrowindClean,
        /// <summary>PS1-descended: point-filtered, low internal resolution, harder grade.</summary>
        Ps1Crunch,
        /// <summary>
        /// Arena-descended geometry read through the grammar of Rajput and Pahari miniature
        /// painting: flat high-chroma colour fields, a hard dark contour, and depth carried by
        /// colour separation rather than atmosphere. The locked look since 2026-08-12.
        /// </summary>
        ArenaMiniature
    }

    public struct Preset
    {
        public string Name;

        // --- Atmosphere ---------------------------------------------------
        /// <summary>Multiplies every weather state's authored fog density.</summary>
        public float FogDensityScale;
        /// <summary>Fog and sky are pulled toward this hue by <see cref="PaletteBias"/>.</summary>
        public Color PaletteTint;
        /// <summary>0 = keep authored weather colour, 1 = fully replace with the tint.</summary>
        public float PaletteBias;
        /// <summary>Global saturation pull applied to fog/ambient before grading.</summary>
        public float Desaturation;

        // --- Lighting -----------------------------------------------------
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public float SunIntensityScale;

        // --- Render -------------------------------------------------------
        /// <summary>URP render scale. Below 1 gives the chunky low-resolution read.</summary>
        public float RenderScale;
        public FilterMode TextureFilter;

        // --- Grading ------------------------------------------------------
        public float Contrast;
        public float Saturation;
        public float PostExposure;
        public Color ColorFilter;
        public float BloomIntensity;
        public float VignetteIntensity;

        /// <summary>Authored base colours for the world surfaces. See <see cref="Palette"/>.</summary>
        public Palette Palette;
    }

    /// <summary>
    /// The locked per-region colour set. These are *authored* values that get written onto
    /// the world materials, not adjustments applied to whatever colour was there before —
    /// so applying a look repeatedly is idempotent instead of compounding.
    ///
    /// Grading alone cannot save an off-palette material: a saturated blue ocean under grey
    /// fog still reads as a saturated blue ocean. The palette has to be enforced at source.
    /// </summary>
    public struct Palette
    {
        public Color Ocean;
        public Color Temperate;
        public Color Arid;
        public Color Sand;
        public Color CityStone;
        public Color Mountain;
        public Color Road;

        /// <summary>
        /// The outline colour. Miniature painting separates adjacent flat fields with a drawn
        /// contour rather than with shading, which is what lets the palette stay flat and still
        /// read as form. Presets that do not draw contours leave this fully transparent, and
        /// <see cref="UsesContour"/> is how the generators ask.
        /// </summary>
        public Color Contour;
    }

    /// <summary>
    /// True when the look draws hard outlines instead of relying on lighting for separation.
    /// Texture and sprite generators branch on this rather than on the enum, so a future
    /// preset gets the behaviour by declaring a contour colour.
    /// </summary>
    public static bool UsesContour(in Preset preset) => preset.Palette.Contour.a > 0.01f;

    public static bool UsesContour() => UsesContour(Active);

    public static readonly Preset MorrowindClean = new Preset
    {
        Name = "Morrowind Clean",
        FogDensityScale = 2.4f,
        PaletteTint = new Color(0.60f, 0.62f, 0.58f),
        PaletteBias = 0.45f,
        Desaturation = 0.30f,
        AmbientSky = new Color(0.44f, 0.46f, 0.44f),
        AmbientEquator = new Color(0.36f, 0.35f, 0.31f),
        AmbientGround = new Color(0.20f, 0.18f, 0.15f),
        SunIntensityScale = 0.85f,
        RenderScale = 1.0f,
        TextureFilter = FilterMode.Bilinear,
        Contrast = 8f,
        Saturation = -14f,
        PostExposure = 0.05f,
        ColorFilter = new Color(1.00f, 0.98f, 0.93f),
        BloomIntensity = 0.10f,
        VignetteIntensity = 0.26f,
        Palette = new Palette
        {
            Ocean     = new Color(0.20f, 0.29f, 0.32f),
            Temperate = new Color(0.31f, 0.35f, 0.26f),
            Arid      = new Color(0.54f, 0.45f, 0.31f),
            Sand      = new Color(0.58f, 0.52f, 0.40f),
            CityStone = new Color(0.45f, 0.44f, 0.41f),
            Mountain  = new Color(0.35f, 0.34f, 0.33f),
            Road      = new Color(0.33f, 0.30f, 0.26f),
            Contour   = new Color(0f, 0f, 0f, 0f)   // lit, not drawn
        }
    };

    public static readonly Preset Ps1Crunch = new Preset
    {
        Name = "PS1 Crunch",
        FogDensityScale = 4.0f,
        PaletteTint = new Color(0.42f, 0.46f, 0.48f),
        PaletteBias = 0.60f,
        Desaturation = 0.42f,
        AmbientSky = new Color(0.34f, 0.36f, 0.38f),
        AmbientEquator = new Color(0.28f, 0.27f, 0.26f),
        AmbientGround = new Color(0.14f, 0.13f, 0.12f),
        SunIntensityScale = 0.75f,
        RenderScale = 0.55f,
        TextureFilter = FilterMode.Point,
        Contrast = 20f,
        Saturation = -26f,
        PostExposure = 0f,
        ColorFilter = new Color(0.96f, 0.97f, 1.00f),
        BloomIntensity = 0f,
        VignetteIntensity = 0.38f,
        Palette = new Palette
        {
            Ocean     = new Color(0.16f, 0.24f, 0.29f),
            Temperate = new Color(0.26f, 0.30f, 0.25f),
            Arid      = new Color(0.47f, 0.40f, 0.30f),
            Sand      = new Color(0.50f, 0.46f, 0.38f),
            CityStone = new Color(0.40f, 0.40f, 0.39f),
            Mountain  = new Color(0.30f, 0.30f, 0.30f),
            Road      = new Color(0.28f, 0.26f, 0.24f),
            Contour   = new Color(0f, 0f, 0f, 0f)   // lit, not drawn
        }
    };

    /// <summary>
    /// The locked look. Arena's blocky flat-topped geometry, rendered in the visual grammar of
    /// Rajput and Pahari miniature painting.
    ///
    /// The two references agree more than they look like they should. Both are flat and frontal,
    /// both draw architecture as elevation rather than in perspective, both outline rather than
    /// shade, and both work in a small set of unmixed pigments. What Arena did because a 1994
    /// renderer could not do better, miniature painting did on purpose — so committing to it
    /// turns the engine's cheapest output into a deliberate reference rather than a limitation.
    ///
    /// The pigments are the historical ones: indigo, terre verte, Indian yellow, ochre,
    /// vermillion and lime white. That is the reason the palette is high-chroma and flat, and it
    /// is why <see cref="Ps1Crunch"/>'s objection does not apply here — see plan.md.
    /// </summary>
    public static readonly Preset ArenaMiniature = new Preset
    {
        Name = "Arena Miniature",

        // Miniature painting has no atmospheric perspective: distance is carried by colour
        // separation and by stacked registers, not by haze. Fog is kept only as a far-plane
        // cutoff so the region does not end in visible nothing.
        FogDensityScale = 0.55f,
        PaletteTint = new Color(0.86f, 0.78f, 0.60f),   // aged paper, not grey weather
        PaletteBias = 0.18f,
        Desaturation = 0f,                              // the whole point; do not raise this

        // Flat, even illumination. A miniature is lit from everywhere and nowhere — there is no
        // key light and no cast shadow, which is exactly what makes the colour fields read flat.
        AmbientSky = new Color(0.74f, 0.72f, 0.68f),
        AmbientEquator = new Color(0.70f, 0.67f, 0.62f),
        AmbientGround = new Color(0.60f, 0.56f, 0.50f),
        SunIntensityScale = 0.45f,

        // Point filtering finally pays for itself: the textures are authored at 64 px by
        // ProceduralTexture, so the texels are real rather than an upscaled blur.
        RenderScale = 0.72f,
        TextureFilter = FilterMode.Point,

        Contrast = 14f,
        Saturation = 16f,                               // positive, unlike every earlier preset
        PostExposure = 0.10f,
        ColorFilter = new Color(1.00f, 0.97f, 0.90f),
        BloomIntensity = 0f,                            // flat pigment does not bloom
        VignetteIntensity = 0.08f,                      // a border, not a darkened lens

        Palette = new Palette
        {
            Ocean     = new Color(0.16f, 0.30f, 0.55f),  // indigo
            Temperate = new Color(0.34f, 0.50f, 0.27f),  // terre verte
            Arid      = new Color(0.79f, 0.58f, 0.20f),  // Indian yellow
            Sand      = new Color(0.83f, 0.71f, 0.44f),  // pale ochre
            CityStone = new Color(0.82f, 0.76f, 0.64f),  // lime white
            Mountain  = new Color(0.43f, 0.40f, 0.52f),  // indigo-grey
            Road      = new Color(0.57f, 0.31f, 0.21f),  // vermillion earth
            Contour   = new Color(0.13f, 0.09f, 0.10f, 1f)
        }
    };

    /// <summary>The look the game currently renders with.</summary>
    public static Look Current = Look.ArenaMiniature;

    public static Preset Active => Get(Current);

    public static Preset Get(Look look) => look switch
    {
        Look.Ps1Crunch => Ps1Crunch,
        Look.ArenaMiniature => ArenaMiniature,
        _ => MorrowindClean
    };

    /// <summary>
    /// Pulls an authored weather colour toward the preset's palette and desaturates it.
    /// Weather still reads as distinct; it just cannot leave the palette.
    /// </summary>
    public static Color Grade(Color source, in Preset preset)
    {
        var tinted = Color.Lerp(source, preset.PaletteTint, preset.PaletteBias);
        float luma = tinted.r * 0.299f + tinted.g * 0.587f + tinted.b * 0.114f;
        return Color.Lerp(tinted, new Color(luma, luma, luma), preset.Desaturation);
    }

    public static Color Grade(Color source) => Grade(source, Active);

    /// <summary>
    /// Applies the parts of the look that live on <see cref="RenderSettings"/>. Fog density
    /// is owned by the weather system, which scales its own values by the active preset.
    /// </summary>
    public static void ApplyEnvironment(in Preset preset)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = preset.AmbientSky;
        RenderSettings.ambientEquatorColor = preset.AmbientEquator;
        RenderSettings.ambientGroundColor = preset.AmbientGround;

        // The default procedural sky is the single biggest reason the build reads as an
        // engine project: a bright blue gradient behind a muted world. A flat sky in the
        // fog colour makes the horizon dissolve instead, which is what sells the distance.
        RenderSettings.skybox = null;
        RenderSettings.fog = true;

        // A contoured look wants a hard horizon, not a dissolved one. Exponential fog reads as
        // atmospheric depth, which is the one thing miniature painting deliberately does not
        // have — so those presets get linear fog held off until the far plane, where it acts as
        // a cutoff rather than as distance.
        if (UsesContour(preset))
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 340f;
        }
        else
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        RenderSettings.fogColor = Grade(RenderSettings.fogColor, preset);
    }
}
