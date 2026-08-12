using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every world surface texture, drawn in code from <see cref="ArtDirection"/>'s palette.
///
/// This is the texture-layer counterpart to <c>ArtDirection.cs</c>, and it exists for the same
/// reason: for a developer who codes but does not paint, a texture that is a function returns
/// more per hour than a texture that is a file. These are deterministic, regenerable,
/// git-diffable as source, and they cannot drift off-palette because they have no colours of
/// their own — every pixel is derived from the active palette.
///
/// **Why 64 px.** The PS1 Crunch spike was rejected in July because point filtering over 1–2K
/// PBR source bought aliasing without buying the chunky-texel read (plan.md). That verdict was
/// about a filter laid over high-resolution art. Authoring at 64 px inverts it: the texels are
/// real, so point filtering is finally rendering what is actually there.
///
/// **The rules that make it read as miniature painting rather than as noise.**
/// <list type="number">
/// <item>No gradients. Every pixel is one of a few quantised shades of the base colour.</item>
/// <item>Variation is per lattice cell, never per pixel — drawn blocks, not static.</item>
/// <item>Adjacent fields are separated by a drawn contour, not by shading.</item>
/// </list>
/// Those are the three properties that separate a Rajput miniature from a photograph, and they
/// happen to be the three cheapest things to generate.
/// </summary>
public static class ProceduralSurface
{
    /// <summary>Authored resolution. See the class remarks — this number is load-bearing.</summary>
    public const int Size = 64;

    public enum Kind
    {
        /// <summary>Lime-white rendered wall. The default city surface.</summary>
        Plaster,
        /// <summary>Coursed masonry: walls, gates, the curtain wall.</summary>
        Stone,
        /// <summary>Tiled roof in vermillion earth.</summary>
        Roof,
        /// <summary>Vertical planking: doors, hoardings, decks.</summary>
        Timber,
        /// <summary>Open ground outside the walls.</summary>
        Ground,
        /// <summary>Beach and street dust.</summary>
        Sand,
        /// <summary>Sea and harbour water.</summary>
        Water,
        /// <summary>Massed foliage, read as clumps rather than leaves.</summary>
        Foliage
    }

    /// <summary>How one surface is drawn. All of it is lattice size plus shade count.</summary>
    private readonly struct Spec
    {
        public readonly int CellWidth, CellHeight;
        /// <summary>Alternate rows shift by half a cell, the way brick courses do.</summary>
        public readonly bool StaggerRows;
        /// <summary>Distinct quantised shades, including the base. 2–4 reads as painted.</summary>
        public readonly int Shades;
        /// <summary>How far the darkest shade sits from the base, 0–1.</summary>
        public readonly float Spread;
        /// <summary>Draw the lattice grid as a contour line.</summary>
        public readonly bool Contoured;

        public Spec(int cellWidth, int cellHeight, int shades, float spread,
            bool contoured = true, bool staggerRows = false)
        {
            CellWidth = Mathf.Max(1, cellWidth);
            CellHeight = Mathf.Max(1, cellHeight);
            Shades = Mathf.Max(2, shades);
            Spread = Mathf.Clamp01(spread);
            Contoured = contoured;
            StaggerRows = staggerRows;
        }
    }

    private static readonly Dictionary<Kind, Texture2D> Textures = new();
    private static readonly Dictionary<Kind, Material> Materials = new();
    private static ArtDirection.Look _builtFor;
    private static bool _built;

    /// <summary>
    /// Drop every cached texture and material. Call after changing the active look, or the old
    /// palette stays baked into surfaces that were already generated — the same failure mode
    /// that made <c>ArtDirectionTool.ApplyAndRebuild</c> the only sanctioned way to change look.
    /// </summary>
    public static void Invalidate()
    {
        Textures.Clear();
        Materials.Clear();
        _built = false;
    }

    public static Texture2D Get(Kind surface)
    {
        EnsureCurrent();
        if (Textures.TryGetValue(surface, out var cached) && cached != null) return cached;

        var texture = Build(surface);
        Textures[surface] = texture;
        return texture;
    }

    /// <summary>An unlit-friendly Lit material carrying the surface. Shared, so it batches.</summary>
    public static Material MaterialFor(Kind surface)
    {
        EnsureCurrent();
        if (Materials.TryGetValue(surface, out var cached) && cached != null) return cached;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { name = $"M_{surface}" };

        var texture = Get(surface);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;

        // White base colour: the palette is already in the texels. Tinting on top would let a
        // surface leave the palette without any test noticing.
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

        // Flat pigment has no specular response. Smoothness above zero puts a highlight on a
        // painted surface, which is the single fastest way to break the illusion.
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);

        Materials[surface] = material;
        return material;
    }

    /// <summary>
    /// Roughly one texture tile every <see cref="MetresPerTile"/> metres, set per renderer via a
    /// property block so the shared materials still batch.
    ///
    /// Without this a 60 m building gets exactly one copy of a 64 px texture stretched across
    /// its whole face, which throws away the texel grain the entire direction is built on. A
    /// cube's faces do not share a UV axis, so this is an approximation: it picks the two axes
    /// the object is most likely to be seen from. That is the right call for flat-topped Arena
    /// geometry and would be the wrong one for anything organic.
    /// </summary>
    public const float MetresPerTile = 2.5f;

    public static void ApplyTiling(Renderer renderer, Vector3 scale)
    {
        if (renderer == null) return;

        bool isSlab = scale.y < scale.x * 0.2f && scale.y < scale.z * 0.2f;
        float u = Mathf.Max(1f, scale.x / MetresPerTile);
        float v = Mathf.Max(1f, (isSlab ? scale.z : scale.y) / MetresPerTile);

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", new Vector4(u, v, 0f, 0f));
        renderer.SetPropertyBlock(block);
    }

    private static void EnsureCurrent()
    {
        if (_built && _builtFor == ArtDirection.Current) return;
        if (_built) Invalidate();
        _built = true;
        _builtFor = ArtDirection.Current;
    }

    // --- drawing -------------------------------------------------------------

    private static Spec SpecFor(Kind surface) => surface switch
    {
        // Every cell size divides Size exactly. A lattice that does not divide leaves a partial
        // cell at the wrap, which shows up as a hard seam on every single tiled surface —
        // sixty-metre walls being the worst possible place to discover it.

        // Big calm fields with a faint course line: a rendered wall, not a brick one.
        Kind.Plaster => new Spec(cellWidth: 32, cellHeight: 16, shades: 3, spread: 0.10f),
        Kind.Stone   => new Spec(cellWidth: 16, cellHeight: 8, shades: 4, spread: 0.22f, staggerRows: true),
        Kind.Roof    => new Spec(cellWidth: 8, cellHeight: 8, shades: 3, spread: 0.26f, staggerRows: true),
        Kind.Timber  => new Spec(cellWidth: 8, cellHeight: 64, shades: 4, spread: 0.20f),

        // Landscape is not drawn with contours — outlining every patch of grass turns a field
        // into a tiled floor. Separation out here comes from the palette instead.
        Kind.Ground  => new Spec(cellWidth: 16, cellHeight: 16, shades: 3, spread: 0.14f, contoured: false),
        Kind.Sand    => new Spec(cellWidth: 16, cellHeight: 16, shades: 3, spread: 0.09f, contoured: false),
        Kind.Water   => new Spec(cellWidth: 64, cellHeight: 4, shades: 3, spread: 0.16f, contoured: false),
        Kind.Foliage => new Spec(cellWidth: 8, cellHeight: 8, shades: 4, spread: 0.24f, contoured: false),
        _ => new Spec(16, 16, 3, 0.12f)
    };

    private static Color BaseColour(Kind surface, in ArtDirection.Palette palette) => surface switch
    {
        Kind.Plaster => palette.CityStone,
        Kind.Stone => Color.Lerp(palette.CityStone, palette.Mountain, 0.35f),
        Kind.Roof => palette.Road,
        Kind.Timber => Color.Lerp(palette.Road, palette.Mountain, 0.30f),
        Kind.Ground => palette.Temperate,
        Kind.Sand => palette.Sand,
        Kind.Water => palette.Ocean,
        Kind.Foliage => Color.Lerp(palette.Temperate, palette.Mountain, 0.20f),
        _ => palette.CityStone
    };

    private static Texture2D Build(Kind surface)
    {
        var preset = ArtDirection.Active;
        var palette = preset.Palette;
        var spec = SpecFor(surface);
        var baseColour = BaseColour(surface, palette);

        bool contoured = spec.Contoured && ArtDirection.UsesContour(preset);

        // One ink, computed once. Blending the contour against whatever shade happened to be
        // underneath turned a single outline into four slightly different colours, which is
        // precisely the not-flat failure this direction cannot afford. Softened toward the base
        // so masonry lines read as drawn rather than as pure black grout.
        var contour = Color.Lerp(BaseColour(surface, palette), palette.Contour, 0.70f);
        contour.a = 1f;

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
        {
            name = $"T_{surface}",
            filterMode = preset.TextureFilter,
            wrapMode = TextureWrapMode.Repeat,
            anisoLevel = 0
        };

        var pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            int row = Mathf.FloorToInt((float)y / spec.CellHeight);

            // Staggering has to wrap: with an odd number of rows in the texture the seam would
            // show a doubled course every tile.
            int rowsInTexture = Mathf.Max(1, Size / spec.CellHeight);
            bool shift = spec.StaggerRows && (row % 2 == 1) && (rowsInTexture % 2 == 0);
            int xOffset = shift ? spec.CellWidth / 2 : 0;

            for (int x = 0; x < Size; x++)
            {
                int shifted = (x + xOffset) % Size;
                int column = Mathf.FloorToInt((float)shifted / spec.CellWidth);

                // Quantise to a small number of shades. Snapping to a step is what keeps this
                // painted rather than noisy — a continuous value here would read as dirt.
                float roll = Hash(column, row, (int)surface);
                int step = Mathf.Min(spec.Shades - 1, Mathf.FloorToInt(roll * spec.Shades));
                float t = step / (float)(spec.Shades - 1);

                Color c = Color.Lerp(baseColour, baseColour * (1f - spec.Spread), t);
                c.a = 1f;

                if (contoured)
                {
                    bool onVertical = (shifted % spec.CellWidth) == 0;
                    bool onHorizontal = (y % spec.CellHeight) == 0;
                    if (onVertical || onHorizontal) c = contour;
                }

                pixels[y * Size + x] = c;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return texture;
    }

    /// <summary>
    /// Integer hash. Deterministic across runs and platforms, and stateless — a shared
    /// <c>System.Random</c> would make one surface's appearance depend on which surfaces were
    /// generated before it, so the city would change depending on where the player walked first.
    /// </summary>
    private static float Hash(int x, int y, int salt)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + salt * 1274126177;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
