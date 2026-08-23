using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>What to generate. A seed and a shape; everything else is derived.</summary>
public sealed record MineRequest(int Seed, int Rooms = 4, int Depth = 1)
{
    public const int MinRooms = 2;

    /// <summary>
    /// Deep enough that a run ends because the player stopped, not because the mine did.
    ///
    /// Press-your-luck cannot work in a mine you can exhaust. A recorded session cleared every
    /// room of a six-room mine and was then simply told there was nothing deeper — four real
    /// decisions and a wall. The greed has to be bounded by nerve.
    /// </summary>
    public const int MaxRooms = 30;

    /// <summary>The request with its numbers forced into the range the generator can honour.</summary>
    public MineRequest Clamped() => this with
    {
        Rooms = Math.Clamp(Rooms, MinRooms, MaxRooms),
        Depth = Math.Max(1, Depth)
    };

    public string MineId => $"mine.{Depth:00}.{unchecked((uint)Seed):x8}";
}

/// <summary>
/// A seed in, a <see cref="WorldManifest"/> out.
///
/// The whole point of emitting the authored format rather than a new one is that generated
/// mines inherit everything already built for hand-made levels: collision, doors and locks,
/// validation, the tools' validate command, and hot reload. A bespoke generated-level pipeline
/// would have had to re-earn all of it.
///
/// Rooms are laid out on a coarse grid and joined in a single chain. One entrance and one exit
/// each, no loops, no dead ends — the simplest topology that is still a place rather than a
/// corridor. Branching is a later problem, and adding it here before the run loop has been
/// played by anybody would be building on an unvalidated guess.
/// </summary>
public static class MineGenerator
{
    /// <summary>Grid pitch. Rooms sit inside it with a gap left over for the corridors.</summary>
    private const float CellSize = 22f;

    private const float RoomHalf = 8f;
    private const float WallThickness = 0.5f;

    /// <summary>Half-width of a doorway, and so of the corridor behind it.</summary>
    private const float DoorwayHalf = 2f;

    private const float FloorBottom = -0.6f;
    private const float FloorTop = -0.2f;
    private const float WallTop = 6f;
    private const float CeilingTop = 6.5f;

    /// <summary>Matches the authored world's spawn height; the player falls the last inch.</summary>
    private const float PlayerSpawnHeight = 2.4f;

    /// <summary>Enemies never spawn nearer than this to a wall.</summary>
    private const float SpawnMargin = 2.5f;
    private const float SpawnSeparation = 3f;

    /// <summary>
    /// How far every fight must stand from a doorway.
    ///
    /// This was a band across the end of the room, which let an enemy stand three metres inside
    /// the door the player was about to walk through — close enough to read as spawning on top
    /// of them. A radius from the doorway itself is what the rule was always meant to be.
    /// </summary>
    private const float DoorwayClearance = 6f;

    /// <summary>The most fights one room is allowed to hold, however deep it is.</summary>
    private const int MaxEnemiesPerRoom = 5;

    /// <summary>Rooms per level of escalation. Every few rooms, what waits gets harder.</summary>
    private const int RoomsPerLevel = 4;

    private enum Side { North, East, South, West }

    private sealed class Cell
    {
        public int X;
        public int Z;
        public readonly HashSet<Side> Openings = new();

        public float CentreX => X * CellSize;
        public float CentreZ => Z * CellSize;
    }

    public static WorldManifest Generate(MineRequest request)
    {
        var settings = (request ?? new MineRequest(0)).Clamped();
        var random = new Prng(settings.Seed);
        var cells = Walk(settings.Rooms, random);

        var manifest = new WorldManifest { Version = 1, Id = settings.MineId };

        // The chain, then the way out of it. Openings are recorded before any geometry is cut,
        // because a wall has to know about its doorway before it is emitted, not after.
        var links = LinkChain(cells);
        var exit = OpenExit(cells);

        var first = cells[0];
        manifest.PlayerSpawn = new WorldSpawn
        {
            Position = new WorldVector(first.CentreX, PlayerSpawnHeight, first.CentreZ + RoomHalf - 3f),
            Yaw = 0f
        };

        for (var index = 0; index < cells.Count; index++)
            EmitRoom(manifest, cells[index], index, settings);

        foreach (var link in links)
            EmitCorridor(manifest, link.From, link.To, link.Side, $"link{link.Index:00}", locked: true);

        EmitStub(manifest, cells[^1], exit);

        PlaceEnemies(manifest, cells, settings, random);
        return manifest;
    }

    public static WorldManifest Generate(int seed, int rooms = 4, int depth = 1) =>
        Generate(new MineRequest(seed, rooms, depth));

    // ------------------------------------------------------------------ topology

    /// <summary>
    /// A self-avoiding walk that never steps south. Because depth only ever increases, the
    /// walk can never paint itself into a corner, so the generator has no retry loop and no
    /// failure case — it always returns exactly the number of rooms it was asked for.
    /// </summary>
    private static List<Cell> Walk(int rooms, Prng random)
    {
        var cells = new List<Cell> { new() { X = 0, Z = 0 } };
        var taken = new HashSet<(int, int)> { (0, 0) };
        var current = cells[0];

        for (var step = 1; step < rooms; step++)
        {
            // North is weighted heavily so a mine reads as a descent, but the lateral steps are
            // what stop every seed from looking like the same straight line.
            var candidates = new List<(int X, int Z)>
            {
                (current.X, current.Z - 1),
                (current.X, current.Z - 1),
                (current.X, current.Z - 1),
                (current.X + 1, current.Z),
                (current.X - 1, current.Z)
            }
            .Where(candidate => !taken.Contains(candidate))
            .ToList();

            var chosen = candidates[random.Next(candidates.Count)];
            var cell = new Cell { X = chosen.X, Z = chosen.Z };

            cells.Add(cell);
            taken.Add(chosen);
            current = cell;
        }

        return cells;
    }

    private static List<(Cell From, Cell To, Side Side, int Index)> LinkChain(List<Cell> cells)
    {
        var links = new List<(Cell From, Cell To, Side Side, int Index)>();

        for (var index = 0; index < cells.Count - 1; index++)
        {
            var from = cells[index];
            var to = cells[index + 1];
            var side = SideBetween(from, to);

            from.Openings.Add(side);
            to.Openings.Add(Opposite(side));
            links.Add((from, to, side, index));
        }

        return links;
    }

    /// <summary>Cut the way out of the last room, on a wall the chain did not already use.</summary>
    private static Side OpenExit(List<Cell> cells)
    {
        var last = cells[^1];
        var side = last.Openings.Contains(Side.North) ? Side.East : Side.North;

        last.Openings.Add(side);
        return side;
    }

    private static Side SideBetween(Cell from, Cell to)
    {
        if (to.Z < from.Z) return Side.North;
        if (to.Z > from.Z) return Side.South;
        return to.X > from.X ? Side.East : Side.West;
    }

    private static Side Opposite(Side side) => side switch
    {
        Side.North => Side.South,
        Side.South => Side.North,
        Side.East => Side.West,
        _ => Side.East
    };

    // ------------------------------------------------------------------ geometry

    private static void EmitRoom(WorldManifest manifest, Cell cell, int index, MineRequest request)
    {
        var prefix = $"{request.MineId}.room{index:00}";
        var cx = cell.CentreX;
        var cz = cell.CentreZ;

        const float outer = RoomHalf + WallThickness;
        var stone = StoneColour(request.Depth);
        var floor = FloorColour(request.Depth);

        Box(manifest, $"{prefix}.floor",
            cx - outer, FloorBottom, cz - outer,
            cx + outer, FloorTop, cz + outer, floor);

        Box(manifest, $"{prefix}.ceiling",
            cx - outer, WallTop, cz - outer,
            cx + outer, CeilingTop, cz + outer, stone);

        // North and south walls run the full outer width so the corners are filled; the east
        // and west walls then only need to span between them.
        EmitWall(manifest, $"{prefix}.north", cell.Openings.Contains(Side.North),
            cx - outer, cx + outer, cz - outer, cz - RoomHalf, horizontal: true, cx, stone);

        EmitWall(manifest, $"{prefix}.south", cell.Openings.Contains(Side.South),
            cx - outer, cx + outer, cz + RoomHalf, cz + outer, horizontal: true, cx, stone);

        EmitWall(manifest, $"{prefix}.west", cell.Openings.Contains(Side.West),
            cx - outer, cx - RoomHalf, cz - RoomHalf, cz + RoomHalf, horizontal: false, cz, stone);

        EmitWall(manifest, $"{prefix}.east", cell.Openings.Contains(Side.East),
            cx + RoomHalf, cx + outer, cz - RoomHalf, cz + RoomHalf, horizontal: false, cz, stone);

        manifest.Rooms.Add(new WorldRoom
        {
            Id = $"{prefix}.room",
            Index = index,
            Centre = new WorldVector(cx, 0f, cz),
            HalfExtent = RoomHalf
        });

        manifest.Lights.Add(new WorldLight
        {
            Id = $"{prefix}.light",
            Position = new WorldVector(cx, 3.6f, cz),
            Color = new WorldColor(255, 196, 132),
            Intensity = 1f,
            Range = RoomHalf * 2.2f
        });
    }

    /// <summary>
    /// One wall, as either a single slab or a pair with a doorway between them. Splitting here
    /// rather than punching holes later keeps every emitted solid a plain box, which is the
    /// only shape the collision index knows about.
    /// </summary>
    private static void EmitWall(WorldManifest manifest, string id, bool opening,
        float minX, float maxX, float minZ, float maxZ, bool horizontal, float centre,
        WorldColor colour)
    {
        if (!opening)
        {
            Box(manifest, id, minX, FloorTop, minZ, maxX, WallTop, maxZ, colour);
            return;
        }

        if (horizontal)
        {
            Box(manifest, $"{id}.a", minX, FloorTop, minZ, centre - DoorwayHalf, WallTop, maxZ, colour);
            Box(manifest, $"{id}.b", centre + DoorwayHalf, FloorTop, minZ, maxX, WallTop, maxZ, colour);
            return;
        }

        Box(manifest, $"{id}.a", minX, FloorTop, minZ, maxX, WallTop, centre - DoorwayHalf, colour);
        Box(manifest, $"{id}.b", minX, FloorTop, centre + DoorwayHalf, maxX, WallTop, maxZ, colour);
    }

    /// <summary>The passage between two rooms, and the door standing in it.</summary>
    private static void EmitCorridor(WorldManifest manifest, Cell from, Cell to, Side side,
        string prefix, bool locked)
    {
        var stone = new WorldColor(78, 72, 66);
        const float outer = RoomHalf + WallThickness;

        if (side is Side.North or Side.South)
        {
            var cx = from.CentreX;
            var minZ = side == Side.North ? to.CentreZ + outer : from.CentreZ + outer;
            var maxZ = side == Side.North ? from.CentreZ - outer : to.CentreZ - outer;

            Passage(manifest, prefix, cx - DoorwayHalf, cx + DoorwayHalf, minZ, maxZ, true, stone);
            Door(manifest, $"{prefix}.door", cx - DoorwayHalf, cx + DoorwayHalf,
                (minZ + maxZ) * 0.5f, true, locked);
            return;
        }

        var cz = from.CentreZ;
        var minX = side == Side.East ? from.CentreX + outer : to.CentreX + outer;
        var maxX = side == Side.East ? to.CentreX - outer : from.CentreX - outer;

        Passage(manifest, prefix, minX, maxX, cz - DoorwayHalf, cz + DoorwayHalf, false, stone);
        Door(manifest, $"{prefix}.door", cz - DoorwayHalf, cz + DoorwayHalf,
            (minX + maxX) * 0.5f, false, locked);
    }

    /// <summary>The way out: a short passage past the last door, ending in the dark.</summary>
    private static void EmitStub(WorldManifest manifest, Cell last, Side side)
    {
        const float outer = RoomHalf + WallThickness;
        const float length = 6f;
        var stone = new WorldColor(70, 66, 62);

        if (side is Side.North or Side.South)
        {
            var cx = last.CentreX;
            var minZ = side == Side.North ? last.CentreZ - outer - length : last.CentreZ + outer;
            var maxZ = side == Side.North ? last.CentreZ - outer : last.CentreZ + outer + length;

            Passage(manifest, "exit", cx - DoorwayHalf, cx + DoorwayHalf, minZ, maxZ, true, stone);
            Door(manifest, "exit.door", cx - DoorwayHalf, cx + DoorwayHalf,
                side == Side.North ? minZ + 1.2f : maxZ - 1.2f, true, locked: false);
            return;
        }

        var cz = last.CentreZ;
        var minX = side == Side.East ? last.CentreX + outer : last.CentreX - outer - length;
        var maxX = side == Side.East ? last.CentreX + outer + length : last.CentreX - outer;

        Passage(manifest, "exit", minX, maxX, cz - DoorwayHalf, cz + DoorwayHalf, false, stone);
        Door(manifest, "exit.door", cz - DoorwayHalf, cz + DoorwayHalf,
            side == Side.East ? maxX - 1.2f : minX + 1.2f, false, locked: false);
    }

    /// <summary>Floor, ceiling and the two side walls of a passage.</summary>
    private static void Passage(WorldManifest manifest, string prefix,
        float minX, float maxX, float minZ, float maxZ, bool alongZ, WorldColor colour)
    {
        var floorMinX = alongZ ? minX - WallThickness : minX;
        var floorMaxX = alongZ ? maxX + WallThickness : maxX;
        var floorMinZ = alongZ ? minZ : minZ - WallThickness;
        var floorMaxZ = alongZ ? maxZ : maxZ + WallThickness;

        Box(manifest, $"{prefix}.floor",
            floorMinX, FloorBottom, floorMinZ, floorMaxX, FloorTop, floorMaxZ, colour);
        Box(manifest, $"{prefix}.ceiling",
            floorMinX, WallTop, floorMinZ, floorMaxX, CeilingTop, floorMaxZ, colour);

        if (alongZ)
        {
            Box(manifest, $"{prefix}.left", minX - WallThickness, FloorTop, minZ,
                minX, WallTop, maxZ, colour);
            Box(manifest, $"{prefix}.right", maxX, FloorTop, minZ,
                maxX + WallThickness, WallTop, maxZ, colour);
            return;
        }

        Box(manifest, $"{prefix}.left", minX, FloorTop, minZ - WallThickness,
            maxX, WallTop, minZ, colour);
        Box(manifest, $"{prefix}.right", minX, FloorTop, maxZ,
            maxX, WallTop, maxZ + WallThickness, colour);
    }

    private static void Door(WorldManifest manifest, string id, float spanMin, float spanMax,
        float centre, bool alongZ, bool locked)
    {
        const float half = 0.2f;
        const float height = 3.4f;

        manifest.Doors.Add(new WorldDoor
        {
            Id = id,
            Min = alongZ
                ? new WorldVector(spanMin, FloorTop, centre - half)
                : new WorldVector(centre - half, FloorTop, spanMin),
            Max = alongZ
                ? new WorldVector(spanMax, FloorTop + height, centre + half)
                : new WorldVector(centre + half, FloorTop + height, spanMax),
            Color = new WorldColor(120, 84, 54),
            Locked = locked,

            // Mine doors are shut, not locked. The gate on pressing deeper is meant to be the
            // player's nerve, not their Security skill.
            Difficulty = 0f,
            KeyItemId = string.Empty,
            PickingIsCrime = false,
            InteractDistance = 3f
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

    // ------------------------------------------------------------------ population

    /// <summary>
    /// Fights, from the second room onward.
    ///
    /// The first room is always empty. A roguelike that can hit you before you have finished
    /// reading the screen is not difficult, it is unfair, and the authored world already made
    /// the same call for the same reason.
    /// </summary>
    private static void PlaceEnemies(WorldManifest manifest, List<Cell> cells,
        MineRequest request, Prng random)
    {
        for (var index = 1; index < cells.Count; index++)
        {
            var cell = cells[index];
            var isLast = index == cells.Count - 1;
            var count = Math.Min(MaxEnemiesPerRoom, 1 + index / 2 + (isLast ? 1 : 0));

            // Depth has to bite, or a long mine is only a long walk. Enemies gain a level
            // every few rooms, so the room after the door is always worse than the one behind
            // it — which is the only thing that makes pressing on a risk rather than a chore.
            var level = request.Depth + index / RoomsPerLevel;
            var placed = new List<WorldVector>();

            for (var slot = 0; slot < count; slot++)
            {
                var position = FindSpawnPoint(cell, placed, random);
                if (position is null) continue;

                placed.Add(position);
                manifest.Spawns.Add(new WorldEnemySpawn
                {
                    Id = $"{request.MineId}.room{index:00}.enemy{slot:00}",
                    ArchetypeId = random.Next(4) == 0 ? EnemyCatalog.PretaId : EnemyCatalog.BanditId,
                    Level = level,
                    Position = position,
                    RoomIndex = index
                });
            }
        }
    }

    /// <summary>
    /// Somewhere in the room that is not a wall, a doorway, or on top of another enemy. Gives
    /// up rather than looping forever; a room one fight short beats a generator that hangs.
    /// </summary>
    private static WorldVector? FindSpawnPoint(Cell cell, List<WorldVector> placed, Prng random)
    {
        const float reach = RoomHalf - SpawnMargin;

        // Deep rooms hold five bodies in a space that also has to keep its doorways clear,
        // so the search is given room to fail and retry rather than quietly under-filling.
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var x = cell.CentreX + random.NextFloat(-reach, reach);
            var z = cell.CentreZ + random.NextFloat(-reach, reach);

            // Nothing waits at a doorway. The player has to be able to walk in and see the
            // room before it is on top of them.
            if (cell.Openings.Any(side => Near(DoorwayOf(cell, side), x, z, DoorwayClearance)))
                continue;

            var crowded = placed.Any(other =>
                MathF.Sqrt((other.X - x) * (other.X - x) + (other.Z - z) * (other.Z - z))
                    < SpawnSeparation);

            if (!crowded) return new WorldVector(x, 0f, z);
        }

        return null;
    }

    /// <summary>The middle of the opening on one side of a room.</summary>
    private static (float X, float Z) DoorwayOf(Cell cell, Side side) => side switch
    {
        Side.North => (cell.CentreX, cell.CentreZ - RoomHalf),
        Side.South => (cell.CentreX, cell.CentreZ + RoomHalf),
        Side.East => (cell.CentreX + RoomHalf, cell.CentreZ),
        _ => (cell.CentreX - RoomHalf, cell.CentreZ)
    };

    private static bool Near((float X, float Z) point, float x, float z, float range)
    {
        var dx = point.X - x;
        var dz = point.Z - z;
        return dx * dx + dz * dz < range * range;
    }

    private static WorldColor StoneColour(int depth) => depth switch
    {
        1 => new WorldColor(96, 90, 82),
        2 => new WorldColor(88, 84, 90),
        _ => new WorldColor(82, 76, 72)
    };

    private static WorldColor FloorColour(int depth) => depth switch
    {
        1 => new WorldColor(74, 70, 64),
        2 => new WorldColor(68, 66, 72),
        _ => new WorldColor(62, 58, 56)
    };

    /// <summary>
    /// A deliberately small generator, written out rather than taken from the framework.
    ///
    /// Seeds get quoted in bug reports and shared between players, so "seed 4211 is the same
    /// mine everywhere, forever" has to be a property of this file — not of whichever runtime
    /// happens to be installed.
    ///
    /// This is SplitMix32: a counter plus an avalanche. The first version here was a bare
    /// xorshift, and its low bits were correlated enough that `% 5` returned the same direction
    /// several steps running — every mine came out a straight corridor. The mixing step is not
    /// decoration; without it the layout variety this whole class exists for does not happen.
    /// </summary>
    private sealed class Prng
    {
        private uint _state;

        public Prng(int seed) => _state = unchecked((uint)seed);

        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                var z = _state;
                z = (z ^ (z >> 16)) * 0x21F0AAADu;
                z = (z ^ (z >> 15)) * 0x735A2D97u;
                return z ^ (z >> 15);
            }
        }

        /// <summary>Scaled from the high bits, which are the well-mixed ones.</summary>
        public int Next(int exclusiveMax) =>
            exclusiveMax <= 1 ? 0 : (int)(((ulong)NextUInt() * (ulong)exclusiveMax) >> 32);

        public float NextFloat(float min, float max) =>
            min + (max - min) * (NextUInt() / (float)uint.MaxValue);
    }
}
