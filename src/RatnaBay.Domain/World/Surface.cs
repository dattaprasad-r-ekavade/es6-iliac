using System;

namespace RatnaBay.Domain;

/// <summary>
/// The yard above the mines: where a Deepankar stands between descents.
///
/// Built in code for the same reasons the mines are — it is testable, it is a few hundred
/// lines rather than a few thousand of hand-written JSON, and it emits the manifest format
/// the game already loads, so it inherits collision, lighting and validation without asking
/// for anything new.
///
/// Deliberately small. It exists to answer one question — does having somewhere to come back
/// to, and something to spend on, make the decision at the door mean anything — and a larger
/// place would take weeks to build before that question got asked once. The fort grows out of
/// this later; it does not start here.
/// </summary>
public static class Surface
{
    public const string Id = "surface.ratnabay";

    /// <summary>Half the width of the yard, walls excluded.</summary>
    private const float Half = 15f;

    private const float WallThickness = 0.6f;
    private const float FloorBottom = -0.6f;
    private const float FloorTop = -0.2f;
    private const float WallTop = 7f;

    /// <summary>Where the player stands on arriving, facing the shaft.</summary>
    public static readonly WorldPoint Spawn = new(0f, 2.4f, 11f);

    /// <summary>The way down. Standing here is how a descent is bought.</summary>
    public static readonly WorldPoint Shaft = new(0f, 0f, -9f);

    /// <summary>The stall. Standing here is how stones become gear.</summary>
    public static readonly WorldPoint Trader = new(-9.5f, 0f, 1f);

    /// <summary>The carved pillar, which is set dressing and the game's thesis at once.</summary>
    public static readonly WorldPoint Stambha = new(9.5f, 0f, 1f);

    /// <summary>How near a fixture must be stood at before it can be used.</summary>
    public const float InteractRange = 3.4f;

    public static WorldManifest Build()
    {
        var manifest = new WorldManifest
        {
            Version = 1,
            Id = Id,
            PlayerSpawn = new WorldSpawn
            {
                Position = new WorldVector(Spawn.X, Spawn.Y, Spawn.Z),
                Yaw = 0f
            }
        };

        Ground(manifest);
        Walls(manifest);
        TheShaft(manifest);
        TheStall(manifest);
        ThePillar(manifest);
        Lanterns(manifest);

        return manifest;
    }

    private static void Ground(WorldManifest manifest)
    {
        const float outer = Half + WallThickness;

        // Packed earth, warmer than anything underground. The colour is doing the work the
        // fiction needs: coming up out of a mine should feel like arriving somewhere.
        Box(manifest, "surface.ground", -outer, FloorBottom, -outer, outer, FloorTop, outer,
            new WorldColor(96, 84, 66));

        // A worn path from where the player arrives to the shaft they will use.
        Box(manifest, "surface.path", -2.4f, FloorTop, -Half, 2.4f, FloorTop + 0.03f, Half,
            new WorldColor(112, 100, 80));
    }

    private static void Walls(WorldManifest manifest)
    {
        const float outer = Half + WallThickness;
        var stone = new WorldColor(104, 96, 84);

        Box(manifest, "surface.wall.north", -outer, FloorTop, -outer, outer, WallTop, -Half, stone);
        Box(manifest, "surface.wall.south", -outer, FloorTop, Half, outer, WallTop, outer, stone);
        Box(manifest, "surface.wall.west", -outer, FloorTop, -Half, -Half, WallTop, Half, stone);
        Box(manifest, "surface.wall.east", Half, FloorTop, -Half, outer, WallTop, Half, stone);
    }

    /// <summary>
    /// The mouth of the mine: a stone collar around a hole, with a frame over it.
    ///
    /// The hole itself is not walkable — descending is a decision made at the collar, not a
    /// step taken by accident. Solid geometry keeps that true without any special case.
    /// </summary>
    private static void TheShaft(WorldManifest manifest)
    {
        var collar = new WorldColor(84, 78, 70);
        var timber = new WorldColor(96, 68, 44);

        Box(manifest, "surface.shaft.collar.n", -3.2f, FloorTop, -12.2f, 3.2f, FloorTop + 0.9f, -10.8f, collar);
        Box(manifest, "surface.shaft.collar.s", -3.2f, FloorTop, -7.2f, 3.2f, FloorTop + 0.9f, -5.8f, collar);
        Box(manifest, "surface.shaft.collar.w", -3.2f, FloorTop, -10.8f, -1.8f, FloorTop + 0.9f, -7.2f, collar);
        Box(manifest, "surface.shaft.collar.e", 1.8f, FloorTop, -10.8f, 3.2f, FloorTop + 0.9f, -7.2f, collar);

        // The dark at the bottom of it, set below the collar so it reads as depth.
        Box(manifest, "surface.shaft.dark", -1.8f, FloorBottom - 3f, -10.8f, 1.8f, FloorTop - 0.05f,
            -7.2f, new WorldColor(14, 13, 16));

        // A winch frame, so the hole reads as worked rather than as a hole.
        Box(manifest, "surface.shaft.post.w", -3.4f, FloorTop, -9.4f, -2.8f, 4.4f, -8.6f, timber);
        Box(manifest, "surface.shaft.post.e", 2.8f, FloorTop, -9.4f, 3.4f, 4.4f, -8.6f, timber);
        Box(manifest, "surface.shaft.beam", -3.6f, 4.4f, -9.5f, 3.6f, 5f, -8.5f, timber);
    }

    private static void TheStall(WorldManifest manifest)
    {
        var timber = new WorldColor(104, 74, 48);
        var cloth = new WorldColor(126, 74, 62);

        Box(manifest, "surface.stall.counter", -12.4f, FloorTop, -0.6f, -10.4f, 1.1f, 2.6f, timber);
        Box(manifest, "surface.stall.post.n", -12.6f, FloorTop, -1f, -12f, 3.2f, -0.4f, timber);
        Box(manifest, "surface.stall.post.s", -12.6f, FloorTop, 2.4f, -12f, 3.2f, 3f, timber);
        Box(manifest, "surface.stall.awning", -13f, 3.2f, -1.2f, -8.6f, 3.6f, 3.2f, cloth);
    }

    /// <summary>The Stambha, carved with the verse. The trailer's opening shot, standing still.</summary>
    private static void ThePillar(WorldManifest manifest)
    {
        var plinth = new WorldColor(84, 78, 70);
        var shaft = new WorldColor(104, 97, 87);

        Box(manifest, "surface.stambha.plinth", 8.2f, FloorTop, -0.3f, 10.8f, 0.5f, 2.3f, plinth);
        Box(manifest, "surface.stambha.shaft", 8.5f, 0.5f, 0.2f, 10.5f, 4.6f, 1.8f, shaft);
        Box(manifest, "surface.stambha.capital", 8.2f, 4.6f, -0.1f, 10.8f, 5f, 2.1f, plinth);
    }

    private static void Lanterns(WorldManifest manifest)
    {
        Light(manifest, "surface.light.shaft", 0f, 4f, -9f, 26f);
        Light(manifest, "surface.light.stall", -10.5f, 3f, 1f, 18f);
        Light(manifest, "surface.light.gate", 0f, 4f, 11f, 22f);
    }

    private static void Light(WorldManifest manifest, string id, float x, float y, float z,
        float range)
    {
        manifest.Lights.Add(new WorldLight
        {
            Id = id,
            Position = new WorldVector(x, y, z),
            Color = new WorldColor(255, 206, 148),
            Intensity = 1f,
            Range = range
        });
    }

    private static void Box(WorldManifest manifest, string id,
        float minX, float minY, float minZ, float maxX, float maxY, float maxZ, WorldColor colour)
    {
        manifest.Geometry.Add(new WorldGeometry
        {
            Id = id,
            Min = new WorldVector(minX, minY, minZ),
            Max = new WorldVector(maxX, maxY, maxZ),
            Color = colour,
            Solid = true,
            Visible = true
        });
    }

    /// <summary>Which fixture the player is standing at, if any.</summary>
    public static SurfaceFixture FixtureAt(WorldPoint player)
    {
        if (player.FlatDistanceTo(Shaft) <= InteractRange + 2f) return SurfaceFixture.Shaft;
        if (player.FlatDistanceTo(Trader) <= InteractRange) return SurfaceFixture.Trader;
        if (player.FlatDistanceTo(Stambha) <= InteractRange) return SurfaceFixture.Stambha;
        return SurfaceFixture.None;
    }
}

public enum SurfaceFixture
{
    None,

    /// <summary>The way down, and the place a descent is bought.</summary>
    Shaft,

    /// <summary>The stall, where stones become gear.</summary>
    Trader,

    /// <summary>The carved pillar.</summary>
    Stambha
}
