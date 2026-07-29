using UnityEngine;

/// <summary>
/// Pure deterministic height sampling for the authored landmasses.
///
/// The mesh generator can call this without owning a second coast/noise definition, and
/// EditMode tests can validate terrain invariants without creating scene objects.
/// </summary>
public static class TerrainHeightSampler
{
    /// <summary>Minimum clearance retained across the stable interior of a landmass.</summary>
    public const float DryInteriorClearance = 1.5f;

    /// <summary>Normalised ellipse radius where the low coastal shoulder begins.</summary>
    public const float ShoreBandStart = 0.82f;

    /// <summary>How far the outer coast/mesh lies below the ocean to avoid coplanar seams.</summary>
    public const float SubmergedCoastDepth = 0.35f;

    private const float CoastalClearance = 0.75f;
    private const float SubmergedCoastStart = 0.9995f;
    private const int DefaultTerrainSeed = 1729;

    private readonly struct BiomeProfile
    {
        public readonly float Amplitude;
        public readonly float MacroScale;
        public readonly float MidScale;
        public readonly float DetailScale;
        public readonly float RidgeScale;
        public readonly float MacroWeight;
        public readonly float MidWeight;
        public readonly float DetailWeight;
        public readonly float RidgeWeight;

        public BiomeProfile(
            float amplitude,
            float macroScale,
            float midScale,
            float detailScale,
            float ridgeScale,
            float macroWeight,
            float midWeight,
            float detailWeight,
            float ridgeWeight)
        {
            Amplitude = amplitude;
            MacroScale = macroScale;
            MidScale = midScale;
            DetailScale = detailScale;
            RidgeScale = ridgeScale;
            MacroWeight = macroWeight;
            MidWeight = midWeight;
            DetailWeight = detailWeight;
            RidgeWeight = ridgeWeight;
        }
    }

    /// <summary>
    /// Stable FNV-1a hash for fallback authoring/tooling seeds. Runtime terrain uses each
    /// landmass's explicit <see cref="WorldLayout.Landmass.TerrainSeed"/> when present.
    /// </summary>
    public static int GetStableSeed(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
            }

            int seed = (int)(hash & 0x7fffffffu);
            return seed == 0 ? DefaultTerrainSeed : seed;
        }
    }

    /// <summary>
    /// Samples deterministic, biome-aware layered terrain. Positions inside the stable
    /// interior remain dry, city centres retain their flattened build area, and the
    /// shared elliptical outer coast sits just below the ocean.
    /// </summary>
    public static float Sample(float worldX, float worldZ, WorldLayout.Landmass patch)
    {
        var worldPosition = new Vector3(worldX, 0f, worldZ);
        float coastDistance = WorldLayout.GetNormalizedCoastDistance(worldPosition, patch);

        // World-space addition/subtraction on small islands can reconstruct the exact
        // ellipse radius a few ULPs below 1. Keep a narrow tolerance so boundary
        // vertices still sink while a deliberate 0.999-radius sample remains dry.
        if (coastDistance >= SubmergedCoastStart)
        {
            float outside = Mathf.Clamp01((coastDistance - SubmergedCoastStart) / 0.2f);
            return WorldLayout.WaterLevel
                   - Mathf.Lerp(0.08f, SubmergedCoastDepth, outside);
        }

        var profile = GetProfile(patch);
        int seed = patch.TerrainSeed != 0
            ? patch.TerrainSeed
            : GetStableSeed(patch.Name);
        Vector2 offset = GetNoiseOffset(seed);

        float macro = SignedNoise(
            worldX * profile.MacroScale + offset.x,
            worldZ * profile.MacroScale + offset.y);
        float mid = SignedNoise(
            worldX * profile.MidScale - offset.y - 31.7f,
            worldZ * profile.MidScale + offset.x + 19.3f);
        float detail = SignedNoise(
            worldX * profile.DetailScale + offset.x * 0.37f + 73.1f,
            worldZ * profile.DetailScale - offset.y * 0.41f - 47.9f);

        float ridgeNoise = Mathf.PerlinNoise(
            worldX * profile.RidgeScale + offset.y + 211.7f,
            worldZ * profile.RidgeScale - offset.x + 89.2f);
        float ridge = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
        float signedRidge = ridge * 2f - 1f;

        float localX = worldX - patch.Center.x;
        float localZ = worldZ - patch.Center.z;
        float cityRelief = 1f;
        if (patch.HasCity)
        {
            float cityDistance = Mathf.Sqrt(localX * localX + localZ * localZ);
            float flatRadius = patch.CityName == "Daggerfall" ? 280f : 260f;
            cityRelief = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(flatRadius, flatRadius + 140f, cityDistance));
        }

        float relief = (
            macro * profile.MacroWeight
            + mid * profile.MidWeight
            + detail * profile.DetailWeight
            + signedRidge * profile.RidgeWeight)
            * profile.Amplitude
            * cityRelief;

        float baseHeight = Mathf.Max(4f, patch.Size.y);
        float dryHeight = Mathf.Max(
            baseHeight + relief,
            WorldLayout.WaterLevel + DryInteriorClearance);

        if (coastDistance <= ShoreBandStart)
            return dryHeight;

        float shoreBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(ShoreBandStart, 1f, coastDistance));
        return Mathf.Lerp(
            dryHeight,
            WorldLayout.WaterLevel + CoastalClearance,
            shoreBlend);
    }

    private static float SignedNoise(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 2f - 1f;
    }

    private static Vector2 GetNoiseOffset(int seed)
    {
        uint first = Mix(unchecked((uint)seed));
        uint second = Mix(first ^ 0x9e3779b9u);
        return new Vector2(
            (first & 0xffffu) / 32f,
            (second & 0xffffu) / 32f);
    }

    private static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }
    }

    private static BiomeProfile GetProfile(WorldLayout.Landmass landmass)
    {
        return landmass.Biome switch
        {
            WorldLayout.Biome.Hammerfell => new BiomeProfile(
                28f, 0.0009f, 0.0036f, 0.01f, 0.0016f,
                0.36f, 0.30f, 0.12f, 0.18f),
            WorldLayout.Biome.IslandGreen => new BiomeProfile(
                22f, 0.0016f, 0.005f, 0.012f, 0.0024f,
                0.38f, 0.28f, 0.12f, 0.22f),
            WorldLayout.Biome.IslandRock => new BiomeProfile(
                35f, 0.0013f, 0.0042f, 0.011f, 0.0021f,
                0.28f, 0.20f, 0.08f, 0.52f),
            _ => new BiomeProfile(
                landmass.Size.y > 40f ? 70f : 42f,
                0.00105f, 0.0033f, 0.0085f, 0.0018f,
                0.42f, 0.25f, 0.10f, 0.35f)
        };
    }
}
