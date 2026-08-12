using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Locks the two properties that carry the Arena Miniature direction: surfaces are <em>flat</em>
/// and they are <em>drawn</em>.
///
/// Neither is guaranteed by anything else. A generator that produced smooth gradients would
/// still be palette-locked, still be procedural, still compile — and would read as mud. A
/// generator that stopped emitting contours would read as untextured greybox. Both failures are
/// invisible in code review and obvious on screen, which is exactly the kind of thing worth a
/// test rather than a comment.
/// </summary>
public class ProceduralSurfaceTests
{
    private static ProceduralSurface.Kind[] AllKinds =>
        (ProceduralSurface.Kind[])System.Enum.GetValues(typeof(ProceduralSurface.Kind));

    [SetUp]
    public void UseTheLockedLook()
    {
        ArtDirection.Current = ArtDirection.Look.ArenaMiniature;
        ProceduralSurface.Invalidate();
    }

    /// <summary>
    /// <c>ArtDirection.Current</c> is a static, and two tests here deliberately switch it. Without
    /// restoring it, whether <c>ArtDirectionTests.DefaultLook_IsTheLockedOne</c> passes would
    /// depend on the order NUnit happened to run the fixtures in.
    /// </summary>
    [TearDown]
    public void Reset()
    {
        ArtDirection.Current = ArtDirection.Look.ArenaMiniature;
        ProceduralSurface.Invalidate();
    }

    [Test]
    public void EveryKind_DrawsAnOpaqueTextureAtTheAuthoredSize(
        [ValueSource(nameof(AllKinds))] ProceduralSurface.Kind kind)
    {
        var texture = ProceduralSurface.Get(kind);

        Assert.AreEqual(ProceduralSurface.Size, texture.width, $"{kind} is not the authored width.");
        Assert.AreEqual(ProceduralSurface.Size, texture.height, $"{kind} is not the authored height.");
        Assert.IsTrue(texture.GetPixels().All(p => p.a > 0.99f),
            $"{kind} has transparent pixels; world surfaces are solid.");
    }

    /// <summary>
    /// The flatness lock. Miniature painting works in a handful of unmixed pigments — the whole
    /// reason the palette can be high-chroma and still read as coherent. A generator drifting
    /// toward per-pixel variation would blow this count out immediately.
    /// </summary>
    [Test]
    public void EveryKind_UsesAHandfulOfFlatShades(
        [ValueSource(nameof(AllKinds))] ProceduralSurface.Kind kind)
    {
        var distinct = new HashSet<Color32>(
            ProceduralSurface.Get(kind).GetPixels32(),
            new Color32Comparer());

        // Four quantised shades plus a contour is the widest any current spec goes.
        Assert.LessOrEqual(distinct.Count, 6,
            $"{kind} uses {distinct.Count} colours. Flat fields are the direction; a gradient "
            + "here reads as dirt and takes the palette down with it.");
        Assert.GreaterOrEqual(distinct.Count, 2,
            $"{kind} is a single flat colour, so it has no texel grain at all — the thing "
            + "point filtering exists to show.");
    }

    [Test]
    public void ArchitecturalSurfaces_AreContoured()
    {
        var contour = ArtDirection.Active.Palette.Contour;

        foreach (var kind in new[]
                 {
                     ProceduralSurface.Kind.Plaster, ProceduralSurface.Kind.Stone,
                     ProceduralSurface.Kind.Roof, ProceduralSurface.Kind.Timber
                 })
        {
            var pixels = ProceduralSurface.Get(kind).GetPixels();
            Assert.IsTrue(pixels.Any(p => Close(p, contour, 0.35f)),
                $"{kind} has no contour line, so adjacent masonry has nothing separating it.");
        }
    }

    /// <summary>
    /// Landscape is deliberately not contoured. Outlining every patch of ground turns a field
    /// into a tiled floor, which is the failure mode of applying the rule everywhere.
    /// </summary>
    [Test]
    public void LandscapeSurfaces_AreNotContoured()
    {
        var contour = ArtDirection.Active.Palette.Contour;

        foreach (var kind in new[]
                 {
                     ProceduralSurface.Kind.Ground, ProceduralSurface.Kind.Sand,
                     ProceduralSurface.Kind.Water, ProceduralSurface.Kind.Foliage
                 })
        {
            var pixels = ProceduralSurface.Get(kind).GetPixels();
            Assert.IsFalse(pixels.Any(p => Close(p, contour, 0.20f)),
                $"{kind} is drawing contour lines, which turns open ground into tiling.");
        }
    }

    /// <summary>
    /// The lattice must divide the texture exactly, or the last cell before the wrap is a
    /// partial one and every tiled surface shows a hard seam. Sixty metres of city wall is the
    /// worst possible place to find that out, and it is invisible in a code review.
    ///
    /// Checked by measuring the contour spacing on a row that carries no horizontal line: the
    /// gaps have to be uniform and divide the width.
    /// </summary>
    [Test]
    public void ContourLatticesDivideTheTexture_SoSurfacesTileWithoutASeam()
    {
        var texture = ProceduralSurface.Get(ProceduralSurface.Kind.Stone);

        // Row 1 sits inside a course rather than on one, so the only contour it can carry is
        // vertical. The contour is by construction the darkest thing in the row.
        var row = Enumerable.Range(0, ProceduralSurface.Size)
            .Select(x => texture.GetPixel(x, 1)).ToArray();
        float darkest = row.Min(Luma);

        var columns = Enumerable.Range(0, ProceduralSurface.Size)
            .Where(x => Luma(row[x]) <= darkest + 0.001f)
            .ToArray();

        Assert.IsNotEmpty(columns, "No vertical contour found on a course row.");
        Assert.AreEqual(0, columns[0], "The first contour is not at the wrap, so tiles will double up.");

        int spacing = columns.Length > 1 ? columns[1] - columns[0] : ProceduralSurface.Size;
        Assert.AreEqual(0, ProceduralSurface.Size % spacing,
            $"Contours are spaced every {spacing}px, which does not divide {ProceduralSurface.Size}.");

        for (int i = 1; i < columns.Length; i++)
            Assert.AreEqual(spacing, columns[i] - columns[i - 1],
                $"Contour spacing is uneven at column {columns[i]}.");
    }

    private static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

    /// <summary>
    /// Regenerating must produce the identical texture. A city that repaints itself between
    /// sessions cannot be playtested, for the same reason the region generator is seeded.
    /// </summary>
    [Test]
    public void Generation_IsDeterministic()
    {
        var first = ProceduralSurface.Get(ProceduralSurface.Kind.Stone).GetPixels32();
        ProceduralSurface.Invalidate();
        var second = ProceduralSurface.Get(ProceduralSurface.Kind.Stone).GetPixels32();

        CollectionAssert.AreEqual(first, second,
            "The same surface generated twice produced different pixels.");
    }

    /// <summary>
    /// The palette lock reaches the texels. Without this the generator could be edited to mix in
    /// colours of its own and nothing in ArtDirectionTests would notice, because that suite only
    /// inspects the palette rather than what is drawn from it.
    /// </summary>
    [Test]
    public void ChangingTheLook_ChangesTheTexels()
    {
        var miniature = ProceduralSurface.Get(ProceduralSurface.Kind.Plaster).GetPixels32();

        ArtDirection.Current = ArtDirection.Look.MorrowindClean;
        ProceduralSurface.Invalidate();
        var muted = ProceduralSurface.Get(ProceduralSurface.Kind.Plaster).GetPixels32();

        CollectionAssert.AreNotEqual(miniature, muted,
            "Switching art direction left the surface unchanged, so the texels are authored "
            + "rather than derived from the palette.");
    }

    private static bool Close(Color a, Color b, float tolerance) =>
        Mathf.Abs(a.r - b.r) < tolerance
        && Mathf.Abs(a.g - b.g) < tolerance
        && Mathf.Abs(a.b - b.b) < tolerance;

    private sealed class Color32Comparer : IEqualityComparer<Color32>
    {
        public bool Equals(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        public int GetHashCode(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
    }
}
