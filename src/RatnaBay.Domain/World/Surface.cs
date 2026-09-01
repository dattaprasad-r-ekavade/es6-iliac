using System;
using System.Collections.Generic;

namespace RatnaBay.Domain;

/// <summary>
/// The yard above the mines: where a Bhagiratha stands between descents.
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

    /// <summary>
    /// The fort gate, set into the west wall.
    ///
    /// Away from the shaft on purpose. The two are the only places in the yard that take the
    /// player somewhere else, and a player who means to go down should never be a step away
    /// from a door that takes them indoors instead.
    /// </summary>
    public static readonly WorldPoint FortGate = new(-Half + 0.9f, 0f, -6f);

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
        TheFortGate(manifest);
        Dressing(manifest);
        Lanterns(manifest);

        return manifest;
    }

    private static void Ground(WorldManifest manifest)
    {
        const float outer = Half + WallThickness;

        // Packed earth, warmer than anything underground. The colour is doing the work the
        // fiction needs: coming up out of a mine should feel like arriving somewhere.
        // Four slabs with a gap where the shaft is, rather than one slab across the whole
        // yard. The mouth of the mine was drawn over by the ground it was supposed to be a
        // hole in, so the one thing the camp exists to point at was a dark tile at best and,
        // once the path was laid over it too, nothing at all.
        const float holeMinX = -1.8f;
        const float holeMaxX = 1.8f;
        const float holeMinZ = -10.8f;
        const float holeMaxZ = -7.2f;

        var earth = new WorldColor(96, 84, 66);

        Box(manifest, "surface.ground.north", -outer, FloorBottom, -outer, outer, FloorTop, holeMinZ, earth, WorldMaterials.Earth);
        Box(manifest, "surface.ground.south", -outer, FloorBottom, holeMaxZ, outer, FloorTop, outer, earth, WorldMaterials.Earth);
        Box(manifest, "surface.ground.west", -outer, FloorBottom, holeMinZ, holeMinX, FloorTop, holeMaxZ, earth, WorldMaterials.Earth);
        Box(manifest, "surface.ground.east", holeMaxX, FloorBottom, holeMinZ, outer, FloorTop, holeMaxZ, earth, WorldMaterials.Earth);

        // You may look down it; you may not walk into it. Descending is a decision made at
        // the collar, and an open hole in the floor of the one safe place would make losing a
        // run something you could do by leaning.
        manifest.Geometry.Add(new WorldGeometry
        {
            Id = "surface.shaft.lid",
            Min = new WorldVector(holeMinX, FloorTop - 0.02f, holeMinZ),
            Max = new WorldVector(holeMaxX, FloorTop, holeMaxZ),
            Color = new WorldColor(0, 0, 0),
            Solid = true,
            Visible = false
        });

        // A worn path from where the player arrives, stopping at the kerb. It used to run the
        // length of the yard and straight over the shaft.
        Box(manifest, "surface.path", -2.4f, FloorTop, -5.8f, 2.4f, FloorTop + 0.03f, Half,
            new WorldColor(150, 138, 116));
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

        var rope = new WorldColor(150, 136, 104);

        // A low kerb rather than a waist-high box: the old collar was tall enough to hide the
        // hole, so the one thing the whole yard exists to point at read as a brick crate.
        Box(manifest, "surface.shaft.collar.n", -3.2f, FloorTop, -12.2f, 3.2f, FloorTop + 0.45f, -10.8f, collar);
        Box(manifest, "surface.shaft.collar.s", -3.2f, FloorTop, -7.2f, 3.2f, FloorTop + 0.45f, -5.8f, collar);
        Box(manifest, "surface.shaft.collar.w", -3.2f, FloorTop, -10.8f, -1.8f, FloorTop + 0.45f, -7.2f, collar);
        Box(manifest, "surface.shaft.collar.e", 1.8f, FloorTop, -10.8f, 3.2f, FloorTop + 0.45f, -7.2f, collar);

        // A lined shaft rather than a dark lid.
        //
        // The hole used to be one black box whose top face sat five centimetres under the
        // ground, so looking into the mouth of the mine showed a dark tile at floor level. It
        // needs sides going down and a floor a long way below them before it reads as
        // somewhere you could be lowered into.
        const float shaftFloor = -9f;
        var lining = new WorldColor(62, 58, 54);

        // NOTE: this still reads far too bright for nine metres underground. Darkening it
        // needs a change to the cave shader, not to this file -- see WorldMaterials.
        Box(manifest, "surface.shaft.line.n", -2.1f, shaftFloor, -11.1f, 2.1f, FloorTop, -10.8f, lining);
        Box(manifest, "surface.shaft.line.s", -2.1f, shaftFloor, -7.2f, 2.1f, FloorTop, -6.9f, lining);
        Box(manifest, "surface.shaft.line.w", -2.1f, shaftFloor, -10.8f, -1.8f, FloorTop, -7.2f, lining);
        Box(manifest, "surface.shaft.line.e", 1.8f, shaftFloor, -10.8f, 2.1f, FloorTop, -7.2f, lining);

        // The dark at the bottom, far enough down that the sides do the work.
        Box(manifest, "surface.shaft.dark", -1.8f, shaftFloor - 0.4f, -10.8f, 1.8f, shaftFloor,
            -7.2f, new WorldColor(14, 13, 16));

        // Rungs down one side, so the depth has a scale to be read against.
        for (var rung = 0; rung < 7; rung++)
        {
            var y = FloorTop - 0.9f - rung * 1.15f;
            Decor(manifest, $"surface.shaft.rung.{rung:00}", -0.7f, y, -7.45f, 0.7f, y + 0.14f,
                -7.3f, timber, WorldMaterials.Timber);
        }

        // The windlass: two braced legs, a drum across them, a crank, and a rope going down.
        // A hole with gear over it reads as a way into somewhere. A hole on its own reads as
        // a missing texture.
        Box(manifest, "surface.shaft.post.w", -2.6f, FloorTop, -9.5f, -2.0f, 4.2f, -8.5f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.shaft.post.e", 2.0f, FloorTop, -9.5f, 2.6f, 4.2f, -8.5f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.shaft.brace.w", -3.4f, FloorTop, -9.2f, -2.4f, 2.2f, -8.8f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.shaft.brace.e", 2.4f, FloorTop, -9.2f, 3.4f, 2.2f, -8.8f, timber, WorldMaterials.Timber);

        Box(manifest, "surface.shaft.drum", -2.2f, 3.5f, -9.3f, 2.2f, 4.1f, -8.7f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.shaft.crank", 2.6f, 3.6f, -9.2f, 3.2f, 4.0f, -8.8f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.shaft.headbeam", -3.0f, 4.2f, -9.6f, 3.0f, 4.7f, -8.4f, timber, WorldMaterials.Timber);

        // Rope and bucket, hanging where the drum would let them down.
        Decor(manifest, "surface.shaft.rope", -0.12f, 1.6f, -9.06f, 0.12f, 3.5f, -8.94f, rope,
            WorldMaterials.Rope);
        Decor(manifest, "surface.shaft.bucket", -0.55f, 1.0f, -9.5f, 0.55f, 1.7f, -8.5f, timber,
            WorldMaterials.Timber);
    }

    private static void TheStall(WorldManifest manifest)
    {
        var timber = new WorldColor(104, 74, 48);
        var cloth = new WorldColor(126, 74, 62);

        var goods = new WorldColor(150, 132, 96);

        // A counter you stand behind, with a plank top proud of its front board, four posts
        // holding a cloth awning, and something actually on the shelf. The old stall was a
        // single box and a slab, both drawn as brick, which is why it read as a bench built
        // into the wall rather than as somewhere a person sells things.
        Box(manifest, "surface.stall.front", -12.2f, FloorTop, -0.6f, -11.6f, 1.05f, 2.6f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.stall.top", -12.6f, 1.05f, -0.9f, -11.0f, 1.25f, 2.9f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.stall.shelf", -12.5f, 0.5f, -0.5f, -11.7f, 0.65f, 2.5f, timber, WorldMaterials.Timber);

        Box(manifest, "surface.stall.post.nf", -11.9f, FloorTop, -1.0f, -11.5f, 3.1f, -0.6f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.stall.post.sf", -11.9f, FloorTop, 2.6f, -11.5f, 3.1f, 3.0f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.stall.post.nb", -13.2f, FloorTop, -1.0f, -12.8f, 3.4f, -0.6f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.stall.post.sb", -13.2f, FloorTop, 2.6f, -12.8f, 3.4f, 3.0f, timber, WorldMaterials.Timber);

        // The awning slopes by being two panels at different heights rather than one slab.
        Decor(manifest, "surface.stall.awning.back", -13.4f, 3.4f, -1.3f, -12.2f, 3.55f, 3.3f, cloth, WorldMaterials.Cloth);
        Decor(manifest, "surface.stall.awning.front", -12.2f, 3.1f, -1.3f, -10.4f, 3.25f, 3.3f, cloth, WorldMaterials.Cloth);
        Decor(manifest, "surface.stall.valance", -10.6f, 2.75f, -1.3f, -10.4f, 3.25f, 3.3f, cloth, WorldMaterials.Cloth);

        // Stock on the counter, so the shelf is not bare.
        Decor(manifest, "surface.stall.goods.a", -12.4f, 1.25f, 0.1f, -11.9f, 1.6f, 0.6f, goods, WorldMaterials.Timber);
        Decor(manifest, "surface.stall.goods.b", -12.3f, 1.25f, 1.1f, -11.7f, 1.5f, 1.7f, goods, WorldMaterials.Timber);
        Decor(manifest, "surface.stall.goods.c", -12.5f, 1.25f, 2.0f, -12.0f, 1.75f, 2.5f, goods, WorldMaterials.Timber);
    }

    /// <summary>The Stambha, carved with the verse. The trailer's opening shot, standing still.</summary>
    private static void ThePillar(WorldManifest manifest)
    {
        var plinth = new WorldColor(84, 78, 70);
        var shaft = new WorldColor(104, 97, 87);

        // Two metres square and four tall is a buttress, not a pillar: at that width the
        // blockwork courses line up with the yard wall behind it and the whole thing reads as
        // masonry that failed to become a wall. Slimmer and taller, on a stepped base, so it
        // stands as an object.
        Box(manifest, "surface.stambha.step", 8.3f, FloorTop, 0.1f, 10.7f, 0.35f, 2.5f, plinth);
        Box(manifest, "surface.stambha.plinth", 8.7f, 0.35f, 0.5f, 10.3f, 0.85f, 2.1f, plinth);
        Box(manifest, "surface.stambha.shaft", 9.1f, 0.85f, 0.9f, 9.9f, 5.4f, 1.7f, shaft);

        // A banded collar where the verse is carved, and a wider cap over it.
        Box(manifest, "surface.stambha.band", 8.95f, 2.0f, 0.75f, 10.05f, 3.4f, 1.85f, shaft);
        Box(manifest, "surface.stambha.capital", 8.75f, 5.4f, 0.55f, 10.25f, 5.8f, 2.05f, plinth);
        Box(manifest, "surface.stambha.finial", 9.15f, 5.8f, 0.95f, 9.85f, 6.3f, 1.65f, shaft);
    }

    /// <summary>
    /// Crates, barrels, a brazier and a notice board.
    ///
    /// Reported as looking empty, which it was: four walls and three fixtures reads as a test
    /// level rather than a place people work. None of this does anything, and that is fine —
    /// a yard that looks used is what makes the three things that *do* work look deliberate.
    /// </summary>
    /// <summary>
    /// The way into the fort: a framed doorway in the west wall.
    ///
    /// The wall behind it stays solid, and the client moves the player rather than letting
    /// them walk through — the fort is its own manifest, so this is a threshold rather than a
    /// hole. Lit, and framed in timber, because an unlit opening in a dark wall is invisible
    /// and that mistake has already cost this game a player.
    /// </summary>
    private static void TheFortGate(WorldManifest manifest)
    {
        var timber = new WorldColor(96, 68, 44);
        var x = -Half;
        var z = FortGate.Z;

        // Jambs and a lintel, standing proud of the wall so the doorway reads from across
        // the yard rather than only from in front of it.
        Box(manifest, "surface.fort.jamb.n", x, FloorTop, z - 2.2f, x + 0.7f, 4.2f, z - 1.6f, timber);
        Box(manifest, "surface.fort.jamb.s", x, FloorTop, z + 1.6f, x + 0.7f, 4.2f, z + 2.2f, timber);
        Box(manifest, "surface.fort.lintel", x, 3.9f, z - 2.2f, x + 0.7f, 4.5f, z + 2.2f, timber);

        // The dark of the passage beyond, so the frame has something behind it.
        Box(manifest, "surface.fort.threshold", x + 0.55f, FloorTop, z - 1.6f,
            x + 0.7f, 3.9f, z + 1.6f, new WorldColor(26, 24, 24));
    }

    private static void Dressing(WorldManifest manifest)
    {
        var timber = new WorldColor(110, 80, 52);
        var crate = new WorldColor(122, 96, 62);
        var iron = new WorldColor(74, 70, 68);

        // Spoil heaps beside the shaft, because something came out of it.
        //
        // Three stacked, shrinking, offset courses rather than one box. As a single cuboid in
        // the default masonry these read as two brick crates parked by the mine -- which was
        // invisible while boxes drew their own interiors, and obvious the moment they stopped.
        // Earth rather than stone, because spoil is what the mountain was, not what it was
        // built into.
        Heap(manifest, "surface.spoil.a", 6.4f, -10.8f, 1.8f, 1.6f, 1.35f,
            new WorldColor(92, 80, 62));
        Heap(manifest, "surface.spoil.b", -6.8f, -11.2f, 1.6f, 1.4f, 1.1f,
            new WorldColor(86, 76, 58));

        // Crates stacked by the stall.
        Box(manifest, "surface.crate.a", -13.4f, FloorTop, 5f, -11.9f, 1.5f, 6.5f, crate, WorldMaterials.Timber);
        Box(manifest, "surface.crate.b", -11.7f, FloorTop, 5.2f, -10.3f, 1.3f, 6.6f, crate, WorldMaterials.Timber);
        Box(manifest, "surface.crate.c", -13.2f, 1.5f, 5.2f, -11.9f, 2.7f, 6.4f, crate, WorldMaterials.Timber);

        // Barrels by the gate, with a hoop each so they are not just posts.
        Box(manifest, "surface.barrel.a", 10.6f, FloorTop, 9.4f, 11.8f, 1.6f, 10.6f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.barrel.b", 12.1f, FloorTop, 9.6f, 13.3f, 1.6f, 10.8f, timber, WorldMaterials.Timber);
        Decor(manifest, "surface.barrel.a.hoop", 10.52f, 0.9f, 9.32f, 11.88f, 1.1f, 10.68f, iron, WorldMaterials.Stone);
        Decor(manifest, "surface.barrel.b.hoop", 12.02f, 0.9f, 9.52f, 13.38f, 1.1f, 10.88f, iron, WorldMaterials.Stone);

        // A brazier, which is where the warm light is coming from.
        Box(manifest, "surface.brazier.stem", 5.6f, FloorTop, 8.6f, 6.2f, 1.4f, 9.2f, iron);
        Box(manifest, "surface.brazier.bowl", 5.1f, 1.4f, 8.1f, 6.7f, 2f, 9.7f,
            new WorldColor(198, 118, 58));

        // The notice board the order pins its work to.
        Box(manifest, "surface.board.post.a", -3.6f, FloorTop, 12.4f, -3.1f, 2.8f, 12.9f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.board.post.b", 0.1f, FloorTop, 12.4f, 0.6f, 2.8f, 12.9f, timber, WorldMaterials.Timber);
        Box(manifest, "surface.board.face", -3.8f, 1.4f, 12.5f, 0.8f, 2.9f, 12.8f, crate, WorldMaterials.Timber);

        // Pinned paper, so the board has something on it to read.
        Decor(manifest, "surface.board.note.a", -3.3f, 1.9f, 12.46f, -2.6f, 2.6f, 12.5f,
            new WorldColor(206, 196, 172), WorldMaterials.Cloth);
        Decor(manifest, "surface.board.note.b", -1.9f, 2.0f, 12.46f, -1.3f, 2.5f, 12.5f,
            new WorldColor(198, 188, 166), WorldMaterials.Cloth);
        Decor(manifest, "surface.board.note.c", -0.7f, 1.7f, 12.46f, -0.1f, 2.3f, 12.5f,
            new WorldColor(210, 200, 176), WorldMaterials.Cloth);
    }

    private static void Lanterns(WorldManifest manifest)
    {
        Light(manifest, "surface.light.shaft", 0f, 4f, -9f, 26f);
        Light(manifest, "surface.light.stall", -10.5f, 3f, 1f, 18f);
        Light(manifest, "surface.light.gate", 0f, 4f, 11f, 22f);
        Light(manifest, "surface.light.brazier", 5.9f, 2.2f, 8.9f, 14f);
        Light(manifest, "surface.light.fort", FortGate.X + 1.2f, 3f, FortGate.Z, 16f);
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

    /// <summary>
    /// A loose pile: courses that shrink and shuffle as they go up.
    ///
    /// Three boxes rather than a mesh, because the whole world is boxes and a heap only has to
    /// break its own silhouette to stop reading as a crate. The offsets are fixed rather than
    /// random so the yard is the same yard in every screenshot.
    /// </summary>
    private static void Heap(WorldManifest manifest, string id,
        float centreX, float centreZ, float halfX, float halfZ, float height, WorldColor colour)
    {
        // Small enough that each course still sits on the one below. The first pass shifted by
        // a third of the extent and the courses walked apart into three separate slabs.
        var shift = new[] { 0f, 0.12f, -0.14f };

        for (var course = 0; course < 3; course++)
        {
            var shrink = 1f - course * 0.34f;
            var top = FloorTop + height * (course + 1) / 3f;
            var x = centreX + shift[course] * halfX;
            var z = centreZ + shift[2 - course] * halfZ;

            Box(manifest, $"{id}.{course}",
                x - halfX * shrink, FloorTop, z - halfZ * shrink,
                x + halfX * shrink, top, z + halfZ * shrink,
                colour, WorldMaterials.Earth);
        }
    }

    private static void Box(WorldManifest manifest, string id,
        float minX, float minY, float minZ, float maxX, float maxY, float maxZ, WorldColor colour,
        string material = WorldMaterials.Stone)
    {
        manifest.Geometry.Add(new WorldGeometry
        {
            Id = id,
            Min = new WorldVector(minX, minY, minZ),
            Max = new WorldVector(maxX, maxY, maxZ),
            Color = colour,
            Solid = true,
            Visible = true,
            Material = material
        });
    }

    /// <summary>Something you can see but walk through: cloth, and the lip of an awning.</summary>
    private static void Decor(WorldManifest manifest, string id,
        float minX, float minY, float minZ, float maxX, float maxY, float maxZ, WorldColor colour,
        string material)
    {
        manifest.Geometry.Add(new WorldGeometry
        {
            Id = id,
            Min = new WorldVector(minX, minY, minZ),
            Max = new WorldVector(maxX, maxY, maxZ),
            Color = colour,
            Solid = false,
            Visible = true,
            Material = material
        });
    }

    /// <summary>Which fixture the player is standing at, if any.</summary>
    public static SurfaceFixture FixtureAt(WorldPoint player)
    {
        if (player.FlatDistanceTo(Shaft) <= InteractRange + 2f) return SurfaceFixture.Shaft;
        if (player.FlatDistanceTo(Trader) <= InteractRange) return SurfaceFixture.Trader;
        if (player.FlatDistanceTo(Stambha) <= InteractRange) return SurfaceFixture.Stambha;
        if (player.FlatDistanceTo(FortGate) <= InteractRange) return SurfaceFixture.Fort;
        return SurfaceFixture.None;
    }

    /// <summary>Where a fixture stands.</summary>
    public static WorldPoint PositionOf(SurfaceFixture fixture) => fixture switch
    {
        SurfaceFixture.Shaft => Shaft,
        SurfaceFixture.Trader => Trader,
        SurfaceFixture.Stambha => Stambha,
        SurfaceFixture.Fort => FortGate,
        _ => Spawn
    };

    /// <summary>
    /// The names a person types for the places in the yard.
    ///
    /// Here rather than in the console's command table because it is a fact about the yard,
    /// not about the console: the table held its own copy of every name and coordinate, so
    /// moving a fixture or adding one meant editing the place and then remembering to edit
    /// the way of getting to it. Several names each, because "stall" and "shop" are the same
    /// request and guessing which one the author chose is not a game.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, SurfaceFixture> Landmarks =
        new Dictionary<string, SurfaceFixture>(StringComparer.OrdinalIgnoreCase)
        {
            ["spawn"] = SurfaceFixture.None,
            ["shaft"] = SurfaceFixture.Shaft,
            // Deliberately not "mine": that is already the console's alias for 'descend', and
            // one word meaning two things is what this table exists to stop.
            ["well"] = SurfaceFixture.Shaft,
            ["stall"] = SurfaceFixture.Trader,
            ["trader"] = SurfaceFixture.Trader,
            ["shop"] = SurfaceFixture.Trader,
            ["stambha"] = SurfaceFixture.Stambha,
            ["pillar"] = SurfaceFixture.Stambha,
            ["fort"] = SurfaceFixture.Fort,
            ["gate"] = SurfaceFixture.Fort
        };

    /// <summary>Look up a landmark by the name somebody typed.</summary>
    public static bool TryLandmark(string? name, out WorldPoint position)
    {
        position = Spawn;
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!Landmarks.TryGetValue(name, out var fixture)) return false;

        position = PositionOf(fixture);
        return true;
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
    Stambha,

    /// <summary>The gate into the fort, where the order keeps its rooms.</summary>
    Fort
}
