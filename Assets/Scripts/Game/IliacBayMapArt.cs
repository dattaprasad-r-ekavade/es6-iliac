using UnityEngine;

/// <summary>
/// Procedural top-down map of the Iliac Bay homage world.
/// </summary>
public static class IliacBayMapArt
{
    // Bounds and landmass shapes come from WorldLayout so the drawn map can no longer
    // drift away from the world it represents — this file used to carry its own
    // hand-copied list of ellipses.
    public const float WorldMinX = WorldLayout.MapMinX;
    public const float WorldMaxX = WorldLayout.MapMaxX;
    public const float WorldMinZ = WorldLayout.MapMinZ;
    public const float WorldMaxZ = WorldLayout.MapMaxZ;

    private static Texture2D _cached;

    private struct Region
    {
        public float X, Z, Rx, Rz;
        public Color Color;
    }

    public static Texture2D GetMapTexture()
    {
        if (_cached != null) return _cached;

        const int w = 1024;
        const int h = 960;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var ocean = new Color(0.08f, 0.22f, 0.42f);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = ocean;

        var regions = BuildRegionsFromLayout();

        foreach (var r in regions)
            FillEllipse(pixels, w, h, r.X, r.Z, r.Rx, r.Rz, r.Color);

        // Coast foam ring
        foreach (var r in regions)
            StrokeEllipse(pixels, w, h, r.X, r.Z, r.Rx + 18f, r.Rz + 14f, new Color(0.72f, 0.68f, 0.5f, 0.55f), 3);

        // Compass rose hint
        DrawCompass(pixels, w, h);

        tex.SetPixels(pixels);
        tex.Apply();
        _cached = tex;
        return _cached;
    }

    public static Vector2 WorldToMapUV(Vector3 world) => WorldLayout.WorldToMapUV(world);

    /// <summary>Each landmass becomes one ellipse, coloured by its biome.</summary>
    private static Region[] BuildRegionsFromLayout()
    {
        var layout = WorldLayout.Landmasses;
        var regions = new Region[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            var land = layout[i];
            regions[i] = new Region
            {
                X = land.Center.x,
                Z = land.Center.z,
                Rx = land.Size.x * 0.5f,
                Rz = land.Size.z * 0.5f,
                Color = BiomeColor(land.Biome)
            };
        }
        return regions;
    }

    private static Color BiomeColor(WorldLayout.Biome biome) => biome switch
    {
        WorldLayout.Biome.Hammerfell => new Color(0.6f, 0.46f, 0.27f),
        WorldLayout.Biome.IslandRock => new Color(0.41f, 0.41f, 0.44f),
        WorldLayout.Biome.IslandGreen => new Color(0.3f, 0.5f, 0.28f),
        _ => new Color(0.24f, 0.44f, 0.22f)
    };

    private static void FillEllipse(Color[] pixels, int w, int h, float cx, float cz, float rx, float rz, Color color)
    {
        int minX = Mathf.Max(0, WorldXToPx(cx - rx, w) - 2);
        int maxX = Mathf.Min(w - 1, WorldXToPx(cx + rx, w) + 2);
        int minY = Mathf.Max(0, WorldZToPy(cz - rz, h) - 2);
        int maxY = Mathf.Min(h - 1, WorldZToPy(cz + rz, h) + 2);

        for (int py = minY; py <= maxY; py++)
        for (int px = minX; px <= maxX; px++)
        {
            float wx = PxToWorldX(px, w);
            float wz = PyToWorldZ(py, h);
            float dx = (wx - cx) / rx;
            float dz = (wz - cz) / rz;
            if (dx * dx + dz * dz > 1f) continue;
            float edge = 1f - Mathf.Clamp01(dx * dx + dz * dz);
            int idx = py * w + px;
            pixels[idx] = Color.Lerp(pixels[idx], color, 0.55f + edge * 0.45f);
        }
    }

    private static void StrokeEllipse(Color[] pixels, int w, int h, float cx, float cz, float rx, float rz, Color color, int thickness)
    {
        for (int a = 0; a < 360; a++)
        {
            float rad = a * Mathf.Deg2Rad;
            for (int t = 0; t < thickness; t++)
            {
                float wx = cx + Mathf.Cos(rad) * (rx + t);
                float wz = cz + Mathf.Sin(rad) * (rz + t);
                int px = WorldXToPx(wx, w);
                int py = WorldZToPy(wz, h);
                if (px < 0 || px >= w || py < 0 || py >= h) continue;
                pixels[py * w + px] = Color.Lerp(pixels[py * w + px], color, color.a);
            }
        }
    }

    private static void DrawCompass(Color[] pixels, int w, int h)
    {
        int cx = w - 56, cy = h - 56;
        for (int y = -20; y <= 20; y++)
        for (int x = -20; x <= 20; x++)
        {
            if (x * x + y * y > 400) continue;
            int px = cx + x, py = cy + y;
            if (px < 0 || px >= w || py < 0 || py >= h) continue;
            pixels[py * w + px] = new Color(0.9f, 0.85f, 0.65f, 0.35f);
        }
        // N arrow
        for (int i = -2; i <= 2; i++)
        {
            int px = cx + i, py = cy + 14;
            if (px >= 0 && px < w && py >= 0 && py < h)
                pixels[py * w + px] = new Color(0.95f, 0.3f, 0.25f);
        }
    }

    private static int WorldXToPx(float x, int w) => Mathf.RoundToInt(Mathf.InverseLerp(WorldMinX, WorldMaxX, x) * (w - 1));
    private static int WorldZToPy(float z, int h) => Mathf.RoundToInt(Mathf.InverseLerp(WorldMinZ, WorldMaxZ, z) * (h - 1));
    private static float PxToWorldX(int px, int w) => Mathf.Lerp(WorldMinX, WorldMaxX, px / (float)(w - 1));
    private static float PyToWorldZ(int py, int h) => Mathf.Lerp(WorldMinZ, WorldMaxZ, py / (float)(h - 1));
}
