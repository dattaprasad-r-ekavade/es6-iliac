using System;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldBuilderPreviewTests
{
    private static WorldLayoutDocument CurrentDocument() => WorldLayoutData.LoadRequired();

    [Test]
    public void CurrentWorldPassesHeadlessPreviewPreflight()
    {
        WorldLayoutDocument document = CurrentDocument();
        Assert.DoesNotThrow(() => WorldBuilderPreviewValidation.ValidateOrThrow(document));
        Assert.DoesNotThrow(() =>
            WorldBuilderPreviewValidation.ValidateRuntimeProjectionOrThrow(document));
    }

    [Test]
    public void DuplicateSiteIdsAreRejectedWithTheOffendingId()
    {
        WorldLayoutDocument document = CurrentDocument();
        document.Sites[1].Id = document.Sites[0].Id;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => WorldBuilderPreviewValidation.ValidateOrThrow(document));
        StringAssert.Contains("duplicate site Id", error.Message);
        StringAssert.Contains(document.Sites[0].Id, error.Message);
    }

    [Test]
    public void AOnePointRoadIsRejectedBeforeMainIsDestroyed()
    {
        WorldLayoutDocument document = CurrentDocument();
        document.Roads[0].Points = new[] { Vector3.zero };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => WorldBuilderPreviewValidation.ValidateOrThrow(document));
        StringAssert.Contains("needs at least two points", error.Message);
    }

    [Test]
    public void UnknownBiomeIsRejectedBeforeGeneration()
    {
        WorldLayoutDocument document = CurrentDocument();
        document.Landmasses[0].Biome = "DefinitelyNotABiome";

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => WorldBuilderPreviewValidation.ValidateOrThrow(document));
        StringAssert.Contains("unknown Biome", error.Message);
    }

    [Test]
    public void TopDownFrameContainsTheWholeMapAtSixteenByNine()
    {
        WorldLayoutDocument document = CurrentDocument();
        const float aspect = 16f / 9f;
        WorldBuilderPreviewValidation.TopDownFrame frame =
            WorldBuilderPreviewValidation.CalculateTopDownFrame(document, aspect);

        float visibleHalfHeight = frame.OrthographicSize;
        float visibleHalfWidth = visibleHalfHeight * aspect;
        float requiredHalfWidth = (document.MapMaxX - document.MapMinX) * 0.5f;
        float requiredHalfHeight = (document.MapMaxZ - document.MapMinZ) * 0.5f;

        Assert.Greater(visibleHalfWidth, requiredHalfWidth);
        Assert.Greater(visibleHalfHeight, requiredHalfHeight);
        Assert.AreEqual((document.MapMinX + document.MapMaxX) * 0.5f,
            frame.CameraPosition.x, 0.001f);
        Assert.AreEqual((document.MapMinZ + document.MapMaxZ) * 0.5f,
            frame.CameraPosition.z, 0.001f);
    }
}
