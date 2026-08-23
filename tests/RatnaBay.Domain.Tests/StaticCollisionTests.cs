namespace RatnaBay.Domain.Tests;

public sealed class StaticCollisionTests
{
    [Test]
    public void LongMoveCannotTunnelThroughWall()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("wall", 0f, 0f, -2f, 0.25f, 3f, 2f)
        });

        var result = collision.Move(new WorldPoint(-2f, 2.4f, 0f),
            new WorldPoint(5f, 0f, 0f), radius: 0.4f);

        Assert.That(result.X, Is.LessThan(0f));
        Assert.That(result.X, Is.EqualTo(-0.4001f).Within(0.001f));
    }

    [Test]
    public void DiagonalMoveSlidesAlongWall()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("wall", 0f, 0f, -3f, 0.25f, 3f, 3f)
        });

        var result = collision.Move(new WorldPoint(-1f, 2.4f, 1f),
            new WorldPoint(2f, 0f, -2f), radius: 0.4f);

        Assert.That(result.X, Is.EqualTo(-0.4001f).Within(0.001f));
        Assert.That(result.Z, Is.EqualTo(-1f).Within(0.001f));
    }

    [Test]
    public void RebuildDropsInvalidSolidsAndKeepsValidOnes()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("bad", 1f, 0f, 1f, 1f, 2f, 2f),
            new CollisionBox("good", -1f, 0f, -1f, 1f, 2f, 1f)
        });

        Assert.That(collision.Boxes.Select(box => box.Id), Is.EqualTo(new[] { "good" }));
    }

    [Test]
    public void SolidOutsidePlayerHeightDoesNotBlock()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("lintel", -1f, 4f, -1f, 1f, 5f, 1f),
            new CollisionBox("floor", -5f, -1f, -5f, 5f, -0.2f, 5f)
        });

        var result = collision.Move(new WorldPoint(0f, 2.4f, 2f),
            new WorldPoint(0f, 0f, -8f), radius: 0.4f);

        Assert.That(result.Z, Is.EqualTo(-6f).Within(0.001f));
    }

    [Test]
    public void RaycastFindsAVisibleHeightWall()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("wall", -1f, 0f, -1f, 1f, 3f, 1f)
        });

        Assert.That(collision.RaycastBlocked(new WorldPoint(0f, 1.6f, 3f),
            new WorldPoint(0f, 1.6f, -3f), out var blocker), Is.True);
        Assert.That(blocker.Id, Is.EqualTo("wall"));
    }

    [Test]
    public void RaycastIgnoresSolidsAboveTheSightLine()
    {
        var collision = new StaticCollisionIndex();
        collision.Rebuild(new[]
        {
            new CollisionBox("lintel", -1f, 3f, -1f, 1f, 5f, 1f)
        });

        Assert.That(collision.RaycastBlocked(new WorldPoint(0f, 1.6f, 3f),
            new WorldPoint(0f, 1.6f, -3f), out _), Is.False);
    }
}

public class VerticalCollisionTests
{
    private static StaticCollisionIndex WithCeiling()
    {
        var index = new StaticCollisionIndex();
        index.Rebuild(new[]
        {
            new CollisionBox("floor", -10f, -1f, -10f, 10f, 0f, 10f),
            new CollisionBox("beam", -10f, 4f, -10f, 10f, 5f, 10f)
        });
        return index;
    }

    [Test]
    public void RisingStopsUnderACeiling()
    {
        var index = WithCeiling();
        var start = new WorldPoint(0f, 2f, 0f);

        var resolved = index.Move(start, new WorldPoint(0f, 10f, 0f), radius: 0.35f, height: 1.8f);

        Assert.That(resolved.Y, Is.LessThanOrEqualTo(4f),
            "a jump must not carry the player through the roof");
    }

    [Test]
    public void FallingStopsOnTheFloor()
    {
        var index = WithCeiling();
        var start = new WorldPoint(0f, 3.5f, 0f);

        var resolved = index.Move(start, new WorldPoint(0f, -10f, 0f), radius: 0.35f, height: 1.8f);

        Assert.That(resolved.Y, Is.EqualTo(1.8f).Within(0.01f),
            "the feet should land on the floor rather than pass through it");
    }

    [Test]
    public void AClearJumpIsUnobstructed()
    {
        var index = new StaticCollisionIndex();
        index.Rebuild(new[] { new CollisionBox("floor", -10f, -1f, -10f, 10f, 0f, 10f) });

        var resolved = index.Move(new WorldPoint(0f, 2f, 0f), new WorldPoint(0f, 1.5f, 0f),
            radius: 0.35f, height: 1.8f);

        Assert.That(resolved.Y, Is.EqualTo(3.5f).Within(0.001f));
    }

    [Test]
    public void SomethingBesideYouDoesNotBlockAJump()
    {
        var index = new StaticCollisionIndex();
        index.Rebuild(new[] { new CollisionBox("shelf", 20f, 4f, 20f, 24f, 5f, 24f) });

        var resolved = index.Move(new WorldPoint(0f, 2f, 0f), new WorldPoint(0f, 6f, 0f),
            radius: 0.35f, height: 1.8f);

        Assert.That(resolved.Y, Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void HorizontalMovementStillWorksWithVerticalSweepInPlace()
    {
        var index = new StaticCollisionIndex();
        index.Rebuild(new[] { new CollisionBox("wall", 2f, 0f, -10f, 3f, 4f, 10f) });

        var resolved = index.Move(new WorldPoint(0f, 2f, 0f), new WorldPoint(10f, 0f, 0f),
            radius: 0.35f, height: 1.8f);

        Assert.That(resolved.X, Is.LessThan(2f), "the wall should still stop a sideways run");
    }
}
