using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RatnaBay.Domain;

/// <summary>JSON-authored geometry, props, spawns and doors for one playable location.</summary>
public sealed class WorldManifest
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public WorldSpawn PlayerSpawn { get; set; } = new();
    public List<WorldGeometry> Geometry { get; set; } = new();
    public List<WorldProp> Props { get; set; } = new();
    public List<WorldLight> Lights { get; set; } = new();
    public List<WorldDoor> Doors { get; set; } = new();
    public List<WorldWatcher> Watchers { get; set; } = new();
    public List<WorldPickup> Pickups { get; set; } = new();

    /// <summary>
    /// Where enemies stand when the location is entered.
    ///
    /// Added so a generated mine can place its own fights. Authored worlds that omit it simply
    /// have none, which is why this stays version 1: the field is additive and its absence is a
    /// valid, meaningful state.
    /// </summary>
    public List<WorldEnemySpawn> Spawns { get; set; } = new();

    /// <summary>
    /// The rooms this location is divided into, if it has any.
    ///
    /// A run needs to know which room the player is standing in to know when it is clear, and
    /// deriving that from geometry would mean re-deducing the level's structure every frame
    /// from the boxes it was flattened into. The generator already knows; it just says so.
    /// </summary>
    public List<WorldRoom> Rooms { get; set; } = new();

    public static bool TryLoad(string path, out WorldManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                error = $"World manifest not found: {path}";
                return false;
            }

            return TryParse(File.ReadAllText(path), out manifest, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Could not read world manifest: {exception.Message}";
            return false;
        }
    }

    public static bool TryParse(string json, out WorldManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            manifest = JsonSerializer.Deserialize<WorldManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Invalid world manifest JSON: {exception.Message}";
            return false;
        }

        if (manifest is null)
        {
            error = "World manifest is empty.";
            return false;
        }

        var failures = manifest.Validate();
        if (failures.Count > 0)
        {
            error = string.Join(" ", failures);
            manifest = null;
            return false;
        }

        return true;
    }

    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Version != 1) failures.Add($"version must be 1, got {Version}.");
        if (string.IsNullOrWhiteSpace(Id)) failures.Add("id is required.");
        if (PlayerSpawn is null || !PlayerSpawn.Position.IsFinite())
            failures.Add("playerSpawn.position must contain finite coordinates.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var geometry in Geometry ?? new List<WorldGeometry>())
        {
            ValidateId(geometry?.Id, "geometry", ids, failures);
            if (geometry is null || geometry.Min is null || geometry.Max is null
                || !geometry.Min.IsFinite() || !geometry.Max.IsFinite()
                || geometry.Min.X >= geometry.Max.X
                || geometry.Min.Y >= geometry.Max.Y
                || geometry.Min.Z >= geometry.Max.Z)
                failures.Add($"geometry '{geometry?.Id ?? "<null>"}' has invalid bounds.");
        }

        foreach (var prop in Props ?? new List<WorldProp>())
        {
            ValidateId(prop?.Id, "prop", ids, failures);
            if (prop is null || string.IsNullOrWhiteSpace(prop.Model))
                failures.Add($"prop '{prop?.Id ?? "<null>"}' needs a model.");
            if (prop?.Position is null || !prop.Position.IsFinite())
                failures.Add($"prop '{prop?.Id ?? "<null>"}' has invalid position.");
            if (prop is not null && (!float.IsFinite(prop.Scale) || prop.Scale <= 0f))
                failures.Add($"prop '{prop.Id}' scale must be positive.");
        }

        foreach (var door in Doors ?? new List<WorldDoor>())
        {
            ValidateId(door?.Id, "door", ids, failures);
            if (door is null || door.Min is null || door.Max is null
                || !door.Min.IsFinite() || !door.Max.IsFinite()
                || door.Min.X >= door.Max.X
                || door.Min.Y >= door.Max.Y
                || door.Min.Z >= door.Max.Z)
                failures.Add($"door '{door?.Id ?? "<null>"}' has invalid bounds.");
            if (door is not null && (!float.IsFinite(door.Difficulty)
                || door.Difficulty < 0f || door.Difficulty > 100f))
                failures.Add($"door '{door.Id}' difficulty must be between 0 and 100.");
        }

        foreach (var watcher in Watchers ?? new List<WorldWatcher>())
        {
            ValidateId(watcher?.Id, "watcher", ids, failures);
            if (watcher is null || watcher.Position is null || !watcher.Position.IsFinite())
                failures.Add($"watcher '{watcher?.Id ?? "<null>"}' has invalid position.");
            if (watcher is not null && (!float.IsFinite(watcher.Speed) || watcher.Speed < 0f))
                failures.Add($"watcher '{watcher.Id}' speed cannot be negative.");
            if (watcher is not null && (!float.IsFinite(watcher.ViewRange) || watcher.ViewRange <= 0f))
                failures.Add($"watcher '{watcher.Id}' viewRange must be positive.");
            if (watcher is not null && (!float.IsFinite(watcher.ViewConeDegrees)
                || watcher.ViewConeDegrees <= 0f || watcher.ViewConeDegrees > 360f))
                failures.Add($"watcher '{watcher.Id}' viewConeDegrees must be between 0 and 360.");

            if (watcher is not null)
            {
                foreach (var waypoint in watcher.Waypoints ?? new List<WorldVector>())
                    if (waypoint is null || !waypoint.IsFinite())
                        failures.Add($"watcher '{watcher.Id}' has an invalid waypoint.");
            }
        }

        foreach (var pickup in Pickups ?? new List<WorldPickup>())
        {
            ValidateId(pickup?.Id, "pickup", ids, failures);
            if (pickup is null || string.IsNullOrWhiteSpace(pickup.ItemId)
                || string.IsNullOrWhiteSpace(pickup.Name) || string.IsNullOrWhiteSpace(pickup.Kind))
                failures.Add($"pickup '{pickup?.Id ?? "<null>"}' needs an item, name and kind.");
            if (pickup?.Position is null || !pickup.Position.IsFinite())
                failures.Add($"pickup '{pickup?.Id ?? "<null>"}' has invalid position.");
            if (pickup is not null && pickup.Count <= 0)
                failures.Add($"pickup '{pickup.Id}' count must be positive.");
            if (pickup is not null && (!float.IsFinite(pickup.Scale) || pickup.Scale <= 0f))
                failures.Add($"pickup '{pickup.Id}' scale must be positive.");
        }

        foreach (var room in Rooms ?? new List<WorldRoom>())
        {
            ValidateId(room?.Id, "room", ids, failures);
            if (room is null || room.Centre is null || !room.Centre.IsFinite())
                failures.Add($"room '{room?.Id ?? "<null>"}' has an invalid centre.");
            if (room is not null && (!float.IsFinite(room.HalfExtent) || room.HalfExtent <= 0f))
                failures.Add($"room '{room.Id}' halfExtent must be positive.");
            if (room is not null && room.Index < 0)
                failures.Add($"room '{room.Id}' index cannot be negative.");
        }

        foreach (var spawn in Spawns ?? new List<WorldEnemySpawn>())
        {
            ValidateId(spawn?.Id, "spawn", ids, failures);
            if (spawn is null || string.IsNullOrWhiteSpace(spawn.ArchetypeId))
                failures.Add($"spawn '{spawn?.Id ?? "<null>"}' needs an archetype.");
            if (spawn?.Position is null || !spawn.Position.IsFinite())
                failures.Add($"spawn '{spawn?.Id ?? "<null>"}' has invalid position.");
            if (spawn is not null && spawn.Level < 1)
                failures.Add($"spawn '{spawn.Id}' level must be at least 1.");
        }

        return failures;
    }

    public static string Serialize(WorldManifest manifest) =>
        JsonSerializer.Serialize(manifest, JsonOptions);

    private static void ValidateId(string? id, string kind, HashSet<string> ids,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            failures.Add($"{kind} id is required.");
            return;
        }

        if (!ids.Add(id)) failures.Add($"duplicate world id '{id}'.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}

public sealed class WorldSpawn
{
    public WorldVector Position { get; set; } = new();
    public float Yaw { get; set; }
}

public sealed class WorldGeometry
{
    public string Id { get; set; } = string.Empty;
    public WorldVector Min { get; set; } = new();
    public WorldVector Max { get; set; } = new();
    public WorldColor Color { get; set; } = new();
    public bool Solid { get; set; } = true;
    public bool Visible { get; set; } = true;

    public CollisionBox ToCollisionBox() => new(Id, Min.X, Min.Y, Min.Z, Max.X, Max.Y, Max.Z);
}

public sealed class WorldProp
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public WorldVector Position { get; set; } = new();
    public float Scale { get; set; } = 1f;
    public float Rotation { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class WorldDoor
{
    public string Id { get; set; } = string.Empty;
    public WorldVector Min { get; set; } = new();
    public WorldVector Max { get; set; } = new();
    public WorldColor Color { get; set; } = new(134, 93, 58, 255);
    public bool Locked { get; set; } = true;
    public float Difficulty { get; set; } = 10f;
    public string KeyItemId { get; set; } = string.Empty;
    public bool PickingIsCrime { get; set; } = true;
    public float InteractDistance { get; set; } = 2.8f;

    /// <summary>
    /// Whether opening this door is a fact about the save or only about this visit.
    ///
    /// An authored door stays open once forced — that is progress through a place that
    /// persists. A door in a generated mine must not: the mine is rebuilt every descent, and
    /// remembering it opened means arriving to find the way already cleared.
    /// </summary>
    public bool Remembered { get; set; } = true;

    public CollisionBox ToCollisionBox() => new(Id, Min.X, Min.Y, Min.Z, Max.X, Max.Y, Max.Z);
}

public sealed class WorldLight
{
    public string Id { get; set; } = string.Empty;
    public WorldVector Position { get; set; } = new();
    public WorldColor Color { get; set; } = new(255, 220, 170, 255);
    public float Intensity { get; set; } = 1f;
    public float Range { get; set; } = 8f;
}

public sealed class WorldWatcher
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Watcher";
    public WorldVector Position { get; set; } = new();
    public List<WorldVector> Waypoints { get; set; } = new();
    public float Yaw { get; set; }
    public float Speed { get; set; } = 1.5f;
    public float ViewRange { get; set; } = 14f;
    public float ViewConeDegrees { get; set; } = 100f;
}

public sealed class WorldPickup
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
    public WorldVector Position { get; set; } = new();
    public string Model { get; set; } = "cheeseBox";
    public float Scale { get; set; } = 0.45f;
}

/// <summary>
/// One enemy, placed. The archetype is named rather than described so that a rebalance happens
/// in one place in the domain instead of in every manifest that ever spawned that enemy.
/// </summary>
public sealed class WorldEnemySpawn
{
    public string Id { get; set; } = string.Empty;
    public string ArchetypeId { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public WorldVector Position { get; set; } = new();

    /// <summary>Which room this fight belongs to. Zero for locations that have no rooms.</summary>
    public int RoomIndex { get; set; }
}

/// <summary>One room of a generated location, in plan view.</summary>
public sealed class WorldRoom
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Position along the mine. Room zero is the entrance and is never payable.</summary>
    public int Index { get; set; }

    public WorldVector Centre { get; set; } = new();

    /// <summary>Half the width of the room's floor, walls excluded.</summary>
    public float HalfExtent { get; set; } = 8f;

    public bool Contains(float x, float z) =>
        MathF.Abs(x - Centre.X) <= HalfExtent && MathF.Abs(z - Centre.Z) <= HalfExtent;

    public WorldPoint CentrePoint() => Centre.ToWorldPoint();
}

public sealed class WorldVector
{
    public WorldVector() { }

    public WorldVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    public WorldPoint ToWorldPoint() => new(X, Y, Z);
}

public sealed class WorldColor
{
    public WorldColor() { }

    public WorldColor(int r, int g, int b, int a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
    public int A { get; set; } = 255;
}
