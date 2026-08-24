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

    /// <summary>
    /// The deepest room reached, which is the one the run is actually about.
    ///
    /// Clearance used to be judged against wherever the player happened to be standing, and a
    /// room was entered afresh every time they walked into it. A recorded run walked back out
    /// of room six into cleared room five — so the "is it clear?" test looked at room five,
    /// found it empty, and paid for room six while room six was still full. Stepping back in
    /// then counted as entering it again and paid a second time. The player reached room eight
    /// and was paid for nine.
    ///
    /// A room is entered once, and the fight it holds is the fight, wherever the body is.
    /// </summary>
    public int DeepestRoom { get; private set; }

    /// <summary>The shut door leading deeper, once the current room is clear.</summary>
    public WorldDoorRuntime? WayOnward { get; private set; }

    /// <summary>True while the player is stood at a cleared room's exit, being asked.</summary>
    public bool AtDecision => Run.CanCamp && WayOnward is not null;

    /// <summary>
    /// The way deeper is shut while anything in this room still moves.
    ///
    /// Without this the camp decision is simply bypassable, and a recorded run proved it:
    /// nine rooms cleared and only six doors ever asked the question. The player opened each
    /// door early, fought the next room from the corridor behind it, and by the time the room
    /// counted as clear there was no shut door left nearby to be asked about.
    ///
    /// The design already said so — "once it opens you are in that room until it is clear or
    /// you are not" — and it turns out that only holds if the door is actually barred.
    /// </summary>
    public bool BarsTheWay => Run.IsActive && HasRooms && !Run.RoomIsClear;

    /// <summary>Raised when a room is cleared, carrying what it paid.</summary>
    public event Action<int>? RoomCleared;

    /// <summary>
    /// Raised when the player walks into a new room, carrying its index.
    ///
    /// Recorded here rather than at the camp panel because the first room is not entered
    /// through that panel — there is nothing banked yet, so it is opened by the ordinary door
    /// prompt, and a recorder hooked to the panel misses it every single run.
    /// </summary>
    public event Action<int>? RoomEntered;

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
        if (room is null) return;

        CurrentRoom = room.Index;

        // Measured against the deepest room ever reached, not the last one stood in, so
        // retreating and coming back is not a second arrival.
        if (room.Index <= DeepestRoom) return;

        DeepestRoom = room.Index;
        Run.EnterRoom();
        RoomEntered?.Invoke(room.Index);
    }

    private void TrackClearance(Encounter encounter)
    {
        if (DeepestRoom <= 0 || Run.RoomIsClear) return;

        // The fight in progress belongs to the room the run is on, not to wherever the player
        // has wandered. Retreating into a cleared room must not clear the one behind you.
        var stillFighting = encounter.Enemies.Any(enemy =>
            _roomOfSpawn.TryGetValue(enemy.SpawnId, out var room) && room == DeepestRoom);

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

    /// <summary>Write the descent down so it can be walked back into.</summary>
    public SavedDescent Capture(int seed, int rooms, int depth) => new()
    {
        Seed = seed,
        Rooms = rooms,
        Depth = depth,
        DeepestRoom = DeepestRoom,
        Run = Run.Capture()
    };

    /// <summary>
    /// Put the player back where they left off.
    ///
    /// The mine is already rebuilt from its seed by the time this runs; what is restored here
    /// is the ledger and how far in they had got. Enemies already killed are remembered by the
    /// world state, so a resumed room is as empty as it was left.
    /// </summary>
    public void Resume(SavedDescent saved)
    {
        if (saved is null || !saved.IsValid) return;

        DeepestRoom = Math.Clamp(saved.DeepestRoom, 0, Math.Max(0, _rooms.Count - 1));
        CurrentRoom = DeepestRoom;
        Run.Adopt(saved.Run);
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
