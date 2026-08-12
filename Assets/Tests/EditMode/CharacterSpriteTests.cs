using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Locks the character sprite.
///
/// The figure that stood here before was three rectangles, defended by the art direction rule
/// that silhouette reads at any fidelity. That rule is true, and it was being used to excuse a
/// silhouette nobody had actually drawn. These tests hold the two things that make the
/// difference between a drawing and a placeholder: the outline exists, and the figure is a
/// property of who the character is rather than of when they spawned.
/// </summary>
public class CharacterSpriteTests
{
    private static readonly string[] SampleCast =
    {
        "Processing_Guard", "Armsmaster", "Magister", "Harbourmaster",
        "Prisoner_A", "Prisoner_B", "King", "Councillor"
    };

    [SetUp]
    public void UseTheLockedLook() => ArtDirection.Current = ArtDirection.Look.ArenaMiniature;

    /// <summary>
    /// <c>ArtDirection.Current</c> is a static and <c>UnderAMutedLook_NothingIsOutlined</c>
    /// deliberately changes it. Leaving it changed would make
    /// <c>ArtDirectionTests.DefaultLook_IsTheLockedOne</c> pass or fail depending on fixture order.
    /// </summary>
    [TearDown]
    public void RestoreTheLock() => ArtDirection.Current = ArtDirection.Look.ArenaMiniature;

    private static Texture2D Draw(string key) =>
        CharacterSprite.Build(CharacterSprite.From(key, new Color(0.5f, 0.4f, 0.6f)));

    [Test]
    public void TheFigureIsASilhouette_NotAFilledRectangle()
    {
        var pixels = Draw("Armsmaster").GetPixels();

        Assert.IsTrue(pixels.Any(p => p.a < 0.01f), "The sprite has no transparency at all.");
        Assert.IsTrue(pixels.Any(p => p.a > 0.99f), "The sprite is entirely transparent.");
    }

    /// <summary>
    /// The outline is the whole trick. Flat fields with no separation are greybox; the same
    /// fields with a hard dark edge are a drawing. If this stops holding, the direction silently
    /// degrades into the thing it was chosen to avoid.
    /// </summary>
    [Test]
    public void EverySilhouetteEdgePixel_IsContour()
    {
        var contour = ArtDirection.Active.Palette.Contour;
        var texture = Draw("Magister");
        var pixels = texture.GetPixels();

        int checkedEdges = 0;
        for (int y = 1; y < CharacterSprite.Height - 1; y++)
        {
            for (int x = 1; x < CharacterSprite.Width - 1; x++)
            {
                int i = y * CharacterSprite.Width + x;
                if (pixels[i].a < 0.99f) continue;

                bool touchesOutside =
                    pixels[i - 1].a < 0.01f || pixels[i + 1].a < 0.01f ||
                    pixels[i - CharacterSprite.Width].a < 0.01f ||
                    pixels[i + CharacterSprite.Width].a < 0.01f;
                if (!touchesOutside) continue;

                checkedEdges++;
                Assert.IsTrue(Close(pixels[i], contour),
                    $"Pixel ({x},{y}) is on the silhouette edge but is not contour. "
                    + "Unoutlined flat colour reads as an untextured greybox.");
            }
        }

        Assert.Greater(checkedEdges, 40, "Found almost no silhouette edge; is anything being drawn?");
    }

    [Test]
    public void TheFigureDoesNotTouchTheSideEdges_SoItIsNotClipped()
    {
        var pixels = Draw("King").GetPixels();

        for (int y = 0; y < CharacterSprite.Height; y++)
        {
            Assert.Less(pixels[y * CharacterSprite.Width].a, 0.01f,
                $"The figure runs off the left edge at row {y}.");
            Assert.Less(pixels[y * CharacterSprite.Width + CharacterSprite.Width - 1].a, 0.01f,
                $"The figure runs off the right edge at row {y}.");
        }
    }

    /// <summary>
    /// The same person looks the same in every session. Nothing about appearance is saved, so
    /// determinism here is the only thing making that true.
    /// </summary>
    [Test]
    public void TheSameNameAlwaysDrawsTheSamePerson()
    {
        CollectionAssert.AreEqual(
            Draw("Harbourmaster").GetPixels32(),
            Draw("Harbourmaster").GetPixels32(),
            "The same actor drew differently on a second call.");
    }

    [Test]
    public void DifferentNamesDrawDifferentPeople()
    {
        var signatures = new HashSet<string>();
        foreach (var name in SampleCast)
        {
            var pixels = Draw(name).GetPixels32();
            signatures.Add(string.Join(",", pixels.Where((_, i) => i % 37 == 0).Select(p => $"{p.r}{p.g}{p.b}{p.a}")));
        }

        // Not all eight need to differ — silhouette variation is coarse on purpose — but a
        // cast that all draws identically means the seed is not reaching the figure.
        Assert.GreaterOrEqual(signatures.Count, 4,
            $"{SampleCast.Length} cast members produced only {signatures.Count} distinct figures.");
    }

    /// <summary>
    /// A look that separates surfaces by lighting must not get outlines drawn on top of it.
    /// The generators branch on the declared contour rather than on the enum, and this is what
    /// holds that wiring together.
    /// </summary>
    [Test]
    public void UnderAMutedLook_NothingIsOutlined()
    {
        ArtDirection.Current = ArtDirection.Look.MorrowindClean;
        var contour = ArtDirection.Get(ArtDirection.Look.ArenaMiniature).Palette.Contour;

        var pixels = Draw("Magister").GetPixels();
        Assert.IsFalse(pixels.Any(p => p.a > 0.99f && Close(p, contour)),
            "A muted look drew miniature contours, so the two directions are bleeding together.");
    }

    private static bool Close(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
}
