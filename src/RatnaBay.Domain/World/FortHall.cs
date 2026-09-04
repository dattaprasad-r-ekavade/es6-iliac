using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// The fort as somewhere to walk, rather than a list of doors on a panel.
///
/// **Why this was staged second.** Iteration 19 existed to retire one risk — content authoring
/// throughput, the number that decides whether the game is finishable — and that risk is
/// answered by writing the roster, not by building corridors. Corridors first would have spent
/// the expensive weeks before learning anything about the cheap ones. The number came back at
/// roughly seven hundred words an hour, so the writing is hours rather than months, and the
/// place is now worth building.
///
/// A hall with five chambers down each side. The shape is deliberately dull: this is somewhere
/// a player passes through between runs, and a fort that needs navigating is a fort that costs
/// time on every single visit. Every door is visible from the middle of the hall, and the
/// walk from the entrance to the far end is about thirty metres.
///
/// **Nothing here decides who may enter.** Geometry is geometry; whether a door opens is
/// <see cref="FortRoom.IsOpen"/>, which the client asks per visit. Baking rank into the world
/// would mean rebuilding the fort every time the player was promoted, and a manifest that
/// depends on progress is a manifest that cannot be cached, validated or hot-reloaded.
/// </summary>
public static class FortHall
{
    public const string Id = "fort.hall";

    /// <summary>Half the hall's width. Six metres across, which is two people and a doorway.</summary>
    private const float HallHalf = 3f;

    /// <summary>How far apart the chamber centres sit along the hall.</summary>
    private const float Pitch = 7f;

    /// <summary>Half a chamber's floor. Small on purpose — one occupant, one table.</summary>
    private const float ChamberHalf = 3.5f;

    private const float WallThickness = 0.5f;
    private const float FloorBottom = -0.6f;
    private const float FloorTop = -0.2f;
    private const float WallTop = 5.5f;
    private const float CeilingTop = 6f;

    /// <summary>Half-width of a doorway, matching the mine's so the two read as one world.</summary>
    private const float DoorwayHalf = 1.5f;

    private const float DoorwayHeight = 3.4f;

    /// <summary>Where the player stands on arriving, just inside the entrance.</summary>
    public static readonly WorldPoint Spawn = new(0f, 2.4f, EntranceZ - 3f);

    /// <summary>
    /// Facing down the hall, not back out of it.
    ///
    /// Forward is (sin yaw, 0, -cos yaw), so zero looks along -Z, which is where the chambers
    /// are. The first version spawned at pi and the player arrived looking at the wall behind
    /// them, one step from walking straight back out again.
    /// </summary>
    public const float SpawnYaw = 0f;

    private static readonly WorldColor Stone = new(78, 74, 70);
    private static readonly WorldColor Trim = new(96, 82, 62);
    private static readonly WorldColor Floor = new(64, 60, 58);

    /// <summary>Rooms per side. Ten rooms, five each way.</summary>
    private static int PerSide => (FortRoster.All.Count + 1) / 2;

    /// <summary>The hall's far end, measured from the roster rather than assumed.</summary>
    private static float FarZ => -(PerSide - 1) * Pitch * 0.5f - Pitch;

    private static float EntranceZ => (PerSide - 1) * Pitch * 0.5f + Pitch;

    public static WorldManifest Build()
    {
        var manifest = new WorldManifest
        {
            Version = 1,
            Id = Id,
            PlayerSpawn = new WorldSpawn
            {
                Position = new WorldVector(Spawn.X, Spawn.Y, Spawn.Z),
                Yaw = SpawnYaw
            }
        };

        Hall(manifest);

        for (var index = 0; index < FortRoster.All.Count; index++)
            Chamber(manifest, FortRoster.All[index], index);

        return manifest;
    }

    /// <summary>
    /// Which side of the hall a room sits on, and how far along it.
    ///
    /// Alternating rather than filling one side first, so the first two rooms a new player can
    /// open face each other across the hall instead of both being at one end.
    /// </summary>
    private static (float X, float Z) Placement(int index)
    {
        var side = index % 2 == 0 ? -1f : 1f;
        var step = index / 2;
        var z = (PerSide - 1) * Pitch * 0.5f - step * Pitch;

        return (side * (HallHalf + WallThickness + ChamberHalf), z);
    }

    /// <summary>Where a room's occupant stands: at the back of their own chamber.</summary>
    public static WorldPoint OccupantPosition(string roomId)
    {
        var index = IndexOf(roomId);
        if (index < 0) return Spawn;

        var (x, z) = Placement(index);
        var inward = x < 0f ? -1f : 1f;

        // Against the far wall, facing the door, with room to walk around them.
        return new WorldPoint(x + inward * (ChamberHalf - 1.4f), 0f, z);
    }

    /// <summary>The door that stands between the hall and a given room.</summary>
    public static string DoorId(string roomId) => $"{Id}.{roomId}.door";

    /// <summary>Which room a door belongs to, or null if it is not one of the fort's.</summary>
    public static string? RoomOfDoor(string? doorId) =>
        doorId is null
            ? null
            : FortRoster.All.Select(room => room.Id).FirstOrDefault(id => DoorId(id) == doorId);

    private static int IndexOf(string roomId)
    {
        for (var index = 0; index < FortRoster.All.Count; index++)
            if (string.Equals(FortRoster.All[index].Id, roomId, StringComparison.Ordinal))
                return index;

        return -1;
    }

    private static void Hall(WorldManifest manifest)
    {
        var near = EntranceZ;
        var far = FarZ;

        Box(manifest, $"{Id}.floor", -HallHalf, FloorBottom, far, HallHalf, FloorTop, near, Floor);
        Box(manifest, $"{Id}.ceiling", -HallHalf - WallThickness, WallTop, far,
            HallHalf + WallThickness, CeilingTop, near, Stone);

        // The two long walls, each cut by the doorways on its side.
        for (var side = 0; side < 2; side++)
        {
            var sign = side == 0 ? -1f : 1f;
            var inner = sign * HallHalf;
            var outer = sign * (HallHalf + WallThickness);

            var gaps = Enumerable.Range(0, FortRoster.All.Count)
                .Where(index => (index % 2 == 0 ? -1f : 1f) == sign)
                .Select(index => Placement(index).Z)
                .OrderByDescending(z => z)
                .ToList();

            var cursor = near;
            for (var gap = 0; gap < gaps.Count; gap++)
            {
                Box(manifest, $"{Id}.wall{side}.{gap}",
                    MathF.Min(inner, outer), FloorTop, gaps[gap] + DoorwayHalf,
                    MathF.Max(inner, outer), WallTop, cursor, Stone);

                // The masonry carried over each doorway, so an opening is a door rather than a
                // slot from floor to ceiling.
                Box(manifest, $"{Id}.lintel{side}.{gap}",
                    MathF.Min(inner, outer), FloorTop + DoorwayHeight, gaps[gap] - DoorwayHalf,
                    MathF.Max(inner, outer), WallTop, gaps[gap] + DoorwayHalf, Stone);

                cursor = gaps[gap] - DoorwayHalf;
            }

            Box(manifest, $"{Id}.wall{side}.end",
                MathF.Min(inner, outer), FloorTop, far, MathF.Max(inner, outer), WallTop, cursor,
                Stone);
        }

        // Both ends are walled.
        //
        // The entrance was left open at first, on the theory that the client would decide what
        // stepping through it meant -- and the first screenshot from inside was half brickwork
        // and half empty sky, because an open end is a hole with nothing behind it. A place is
        // enclosed. Leaving is a threshold just inside this wall, which the client watches for.
        Box(manifest, $"{Id}.wall.far", -HallHalf - WallThickness, FloorTop, far - WallThickness,
            HallHalf + WallThickness, WallTop, far, Trim);

        Box(manifest, $"{Id}.wall.near", -HallHalf - WallThickness, FloorTop, near,
            HallHalf + WallThickness, WallTop, near + WallThickness, Trim);

        Light(manifest, $"{Id}.light.entrance", 0f, 3.6f, EntranceZ - 2f, 20f);
        Light(manifest, $"{Id}.light.middle", 0f, 3.6f, 0f, 24f);
        Light(manifest, $"{Id}.light.far", 0f, 3.6f, FarZ + 3f, 20f);
    }

    private static void Chamber(WorldManifest manifest, FortRoom room, int index)
    {
        var (x, z) = Placement(index);
        var prefix = $"{Id}.{room.Id}";

        var innerX = x < 0f ? x + ChamberHalf : x - ChamberHalf;
        var outerX = x < 0f ? x - ChamberHalf : x + ChamberHalf;
        var minX = MathF.Min(innerX, outerX);
        var maxX = MathF.Max(innerX, outerX);

        Box(manifest, $"{prefix}.floor", minX, FloorBottom, z - ChamberHalf,
            maxX, FloorTop, z + ChamberHalf, Floor);
        Box(manifest, $"{prefix}.ceiling", minX - WallThickness, WallTop, z - ChamberHalf - WallThickness,
            maxX + WallThickness, CeilingTop, z + ChamberHalf + WallThickness, Stone);

        // Three walls; the fourth side is the hall wall, which is already cut for the doorway.
        Box(manifest, $"{prefix}.back",
            x < 0f ? minX - WallThickness : maxX, FloorTop, z - ChamberHalf - WallThickness,
            x < 0f ? minX : maxX + WallThickness, WallTop, z + ChamberHalf + WallThickness, Stone);

        Box(manifest, $"{prefix}.side.a", minX - WallThickness, FloorTop,
            z - ChamberHalf - WallThickness, maxX + WallThickness, WallTop, z - ChamberHalf, Stone);

        Box(manifest, $"{prefix}.side.b", minX - WallThickness, FloorTop, z + ChamberHalf,
            maxX + WallThickness, WallTop, z + ChamberHalf + WallThickness, Stone);

        // The door itself, standing in the hall wall.
        //
        // Locked is false and Difficulty is zero for the same reason every mine door is: the
        // gate on this door is rank, which the client asks the roster about, and a door that
        // reports itself locked shows the player a refusal with no verb in it. That mistake
        // cost the alpha its first outside player 110 minutes.
        var doorX = x < 0f ? -HallHalf - WallThickness : HallHalf;

        manifest.Doors.Add(new WorldDoor
        {
            Id = DoorId(room.Id),
            Min = new WorldVector(doorX, FloorTop, z - DoorwayHalf),
            Max = new WorldVector(doorX + WallThickness, FloorTop + DoorwayHeight, z + DoorwayHalf),
            Color = new WorldColor(120, 84, 54),
            Locked = false,
            Difficulty = 0f,
            KeyItemId = string.Empty,
            PickingIsCrime = false,
            InteractDistance = 3f,

            // A fort door stays open once opened. Unlike a mine, this is a place that persists,
            // and a room the player has earned should not shut itself between runs.
            Remembered = true
        });

        Light(manifest, $"{prefix}.light", x, 3.2f, z, ChamberHalf * 3f);
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
            Visible = true,
            Material = WorldMaterials.Stone
        });
    }
}
