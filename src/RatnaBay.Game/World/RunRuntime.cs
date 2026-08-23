using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// The bridge between where the player is standing and what the run ledger believes.
///
/// <see cref="RunState"/> owns the arithmetic and the rules and knows nothing about geometry.
/// This is the part that has to look at the world: which room the body is in, whether anything
/// in it is still alive, and whether the player is close enough to the way onward to be asked
/// the question. Keeping the split here is what lets the entire economy be tested without a
/// graphics device attached.
/// </summary>
public sealed class RunRuntime
{
    /// <summary>How near the way onward the player must be before the decision is offered.</summary>
    private const float DecisionRange = 4.5f;

    private readonly Dictionary<string, int> _roomOfSpawn = new(StringComparer.Ordinal);
    private readonly List<WorldRoom> _rooms;

    public RunRuntime(WorldManifest manifest, int seed, int tier)
    {
        _rooms = (manifest.Rooms ?? new List<WorldRoom>()).OrderBy(room => room.Index).ToList();

        foreach (var spawn in manifest.Spawns ?? new List<WorldEnemySpawn>())
            _roomOfSpawn[spawn.Id] = spawn.RoomIndex;

        // The entrance is not a payable room, so a five-room mine is four rooms of work.
        Run = RunState.Begin(seed, tier, Math.Max(1, _rooms.Count - 1));
    }

    public RunState Run { get; }

    /// <summary>Which room the player is standing in. Sticky: corridors keep the last answer.</summary>
    public int CurrentRoom { get; private set; }

    /// <summary>The shut door leading deeper, once the current room is clear.</summary>
    public WorldDoorRuntime? WayOnward { get; private set; }

    /// <summary>True while the player is stood at a cleared room's exit, being asked.</summary>
    public bool AtDecision => Run.CanCamp && WayOnward is not null;

    /// <summary>Raised when a room is cleared, carrying what it paid.</summary>
    public event Action<int>? RoomCleared;

    public bool HasRooms => _rooms.Count > 1;

    public void Update(WorldRuntime world, Vector3 playerPosition, Encounter encounter)
    {
        if (!Run.IsActive || !HasRooms) return;

        TrackRoom(playerPosition);
        TrackClearance(encounter);
        TrackDecisionPoint(world, playerPosition);
    }

    /// <summary>
    /// Corridors belong to no room, so the last room the player was actually inside is kept.
    /// Without that the run would flicker out of its room every time somebody walked through a
    /// doorway, and clear the same room twice.
    /// </summary>
    private void TrackRoom(Vector3 player)
    {
        var room = _rooms.FirstOrDefault(candidate => candidate.Contains(player.X, player.Z));
        if (room is null || room.Index == CurrentRoom) return;

        // Only ever forward. Walking back into a cleared room is not a new fight.
        if (room.Index > CurrentRoom) Run.EnterRoom();
        CurrentRoom = room.Index;
    }

    private void TrackClearance(Encounter encounter)
    {
        if (CurrentRoom <= 0 || Run.RoomIsClear) return;

        var stillFighting = encounter.Enemies.Any(enemy =>
            _roomOfSpawn.TryGetValue(enemy.SpawnId, out var room) && room == CurrentRoom);

        if (stillFighting) return;

        var paid = Run.ClearRoom();
        if (paid > 0) RoomCleared?.Invoke(paid);
    }

    private void TrackDecisionPoint(WorldRuntime world, Vector3 player)
    {
        WayOnward = null;
        if (!Run.CanCamp) return;

        var here = new WorldPoint(player.X, player.Y, player.Z);

        // The door the player came through is already open, so the nearest shut one is the way
        // deeper — no bookkeeping needed to work out which door belongs to which room.
        foreach (var door in world.Doors)
        {
            if (door.Lock.IsOpen) continue;
            if (here.FlatDistanceTo(door.Definition.Centre()) > DecisionRange) continue;

            WayOnward = door;
            return;
        }
    }

    /// <summary>Bank the pot and end the run here.</summary>
    public RunResult Camp() => Run.Camp();

    /// <summary>Open the way onward and commit to the room behind it.</summary>
    public bool PressOn(WorldRuntime world, PlayerCharacter character)
    {
        if (WayOnward is null || !Run.CanPressOn) return false;

        // Mine doors are shut rather than locked, so this never fails on skill.
        world.OpenDoor(WayOnward, character);
        return WayOnward.Lock.IsOpen;
    }

    public RunResult Die() => Run.Die();
}
