using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// The live, reloadable version of one JSON world. It owns only scene facts; player rules stay
/// in the domain and rendering stays in Game1.
/// </summary>
public sealed class WorldRuntime
{
    private readonly StaticCollisionIndex _collision = new();
    private readonly string? _manifestPath;
    private readonly HashSet<string> _openedDoors = new(StringComparer.Ordinal);
    private DateTime _lastWriteUtc;
    private List<WorldDoorRuntime> _doors = new();

    private WorldRuntime(string? manifestPath, WorldManifest manifest)
    {
        _manifestPath = manifestPath;
        Manifest = manifest;
        ApplyManifest(manifest);
    }

    public WorldManifest Manifest { get; private set; }
    public IReadOnlyList<WorldDoorRuntime> Doors => _doors;
    public StaticCollisionIndex Collision => _collision;
    /// <summary>Null for generated worlds created in memory rather than loaded from disk.</summary>
    public string? ManifestPath => _manifestPath;

    public static bool TryLoad(string path, out WorldRuntime? world, out string error)
    {
        world = null;
        if (!WorldManifest.TryLoad(path, out var manifest, out error)) return false;

        world = new WorldRuntime(Path.GetFullPath(path), manifest!);
        return true;
    }

    /// <summary>
    /// Validate and load a generated world without writing into the installed game directory.
    /// Serializing through the public contract keeps this path identical to authored JSON while
    /// avoiding one permanent file per descent.
    /// </summary>
    public static bool TryCreate(WorldManifest manifest, out WorldRuntime? world, out string error)
    {
        world = null;
        if (!WorldManifest.TryParse(WorldManifest.Serialize(manifest), out var validated, out error))
            return false;

        world = new WorldRuntime(manifestPath: null, validated!);
        return true;
    }

    /// <summary>Reload once after an edit; a malformed edit leaves the current room playable.</summary>
    public bool TryReloadIfChanged(out string message)
    {
        message = string.Empty;
        if (_manifestPath is null) return false;

        DateTime writeUtc;
        try { writeUtc = File.GetLastWriteTimeUtc(_manifestPath); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

        if (writeUtc == _lastWriteUtc) return false;
        _lastWriteUtc = writeUtc;

        if (!WorldManifest.TryLoad(_manifestPath, out var manifest, out var error))
        {
            message = error;
            return false;
        }

        Manifest = manifest!;
        ApplyManifest(Manifest);
        message = $"Reloaded {Manifest.Id}.";
        return true;
    }

    public WorldPoint Move(WorldPoint position, WorldPoint delta, float radius) =>
        _collision.Move(position, delta, radius);

    public WorldDoorRuntime? FindDoor(WorldPoint player, float yaw)
    {
        var forward = Targeting.FlatForward(yaw);
        WorldDoorRuntime? best = null;
        var bestDistance = float.MaxValue;

        foreach (var door in _doors)
        {
            if (door.Lock.IsOpen) continue;
            var centre = door.Definition.Centre();
            var distance = player.FlatDistanceTo(centre);
            if (distance > door.Definition.InteractDistance || distance >= bestDistance)
                continue;

            var dx = centre.X - player.X;
            var dz = centre.Z - player.Z;
            if (distance > 0.001f && (dx * forward.X + dz * forward.Z) / distance < 0.35f)
                continue;

            best = door;
            bestDistance = distance;
        }

        return best;
    }

    public LockResult TryOpenDoor(WorldPoint player, float yaw, PlayerCharacter character,
        out WorldDoorRuntime? door)
    {
        door = FindDoor(player, yaw);
        return door is null ? LockResult.NotLocked : OpenDoor(door, character);
    }

    /// <summary>
    /// Open one named door, rather than whichever one the player happens to be facing. The run
    /// loop already knows which door it is asking about, and making it aim at it would turn a
    /// decision the player has already made into a thing they can miss.
    /// </summary>
    public LockResult OpenDoor(WorldDoorRuntime door, PlayerCharacter character)
    {
        var result = door.Lock.TryOpen(character.Skills, character.Inventory, character.Detection);
        if (!door.Lock.IsOpen) return result;

        RebuildCollision();

        // A door in a generated mine is opened for this visit only. Writing it to the save
        // meant the next mine — which reuses the same door ids — began with its way already
        // cleared, and the player walked nine rooms without being asked anything.
        if (!door.Definition.Remembered) return result;

        _openedDoors.Add(door.Definition.Id);
        character.Story.MarkOpened(door.Definition.Id);
        character.Story.SetFlag($"flag.opened.{door.Definition.Id}");
        return result;
    }

    /// <summary>Apply opened door ids from the player's persisted story state.</summary>
    public void RestoreOpenedDoors(IEnumerable<string>? openedDoors)
    {
        _openedDoors.Clear();
        if (openedDoors is not null)
        {
            foreach (var id in openedDoors)
                if (!string.IsNullOrWhiteSpace(id)) _openedDoors.Add(id);
        }

        // Belt and braces alongside not writing them in the first place: a save made before
        // this was fixed still carries the old shared mine-door ids, and must not be allowed
        // to fling open the doors of every mine generated from now on.
        foreach (var door in _doors)
            if (door.Definition.Remembered && _openedDoors.Contains(door.Definition.Id))
                door.Lock.RestoreOpened();

        RebuildCollision();
    }

    private void ApplyManifest(WorldManifest manifest)
    {
        _doors = (manifest.Doors ?? new List<WorldDoor>())
            .Select(definition => new WorldDoorRuntime(definition))
            .ToList();
        foreach (var door in _doors)
            if (_openedDoors.Contains(door.Definition.Id)) door.Lock.RestoreOpened();
        RebuildCollision();

        if (_manifestPath is null)
        {
            _lastWriteUtc = DateTime.MinValue;
            return;
        }

        try { _lastWriteUtc = File.GetLastWriteTimeUtc(_manifestPath); }
        catch (IOException) { _lastWriteUtc = DateTime.MinValue; }
        catch (UnauthorizedAccessException) { _lastWriteUtc = DateTime.MinValue; }
    }

    /// <summary>Rebuild the solids after a door has been opened or shut from outside.</summary>
    public void RefreshCollision() => RebuildCollision();

    private void RebuildCollision()
    {
        var boxes = (Manifest.Geometry ?? new List<WorldGeometry>())
            .Where(geometry => geometry.Solid)
            .Select(geometry => geometry.ToCollisionBox())
            .Concat(_doors.Where(door => !door.Lock.IsOpen).Select(door => door.Definition.ToCollisionBox()));
        _collision.Rebuild(boxes);
    }
}

public sealed class WorldDoorRuntime
{
    public WorldDoorRuntime(WorldDoor definition)
    {
        Definition = definition;
        Lock = new Lockable(definition.Locked, definition.Difficulty,
            definition.KeyItemId, definition.PickingIsCrime);
    }

    public WorldDoor Definition { get; }
    public Lockable Lock { get; }
}

public static class WorldManifestExtensions
{
    public static WorldPoint Centre(this WorldDoor door) => new(
        (door.Min.X + door.Max.X) * 0.5f,
        (door.Min.Y + door.Max.Y) * 0.5f,
        (door.Min.Z + door.Max.Z) * 0.5f);
}
