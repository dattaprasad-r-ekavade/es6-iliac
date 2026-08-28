using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System.Collections.Generic;

namespace RatnaBay.Client;

internal enum SessionAction
{
    None,
    Save,
    BlockedSave,
    Load,
    Pickpocket,
    OpenShop,
    Talk,
    UseFixture,
    TakePickup,
    DoorBarred,
    OpenDoor
}

internal readonly record struct SessionCommand(
    SessionAction Action,
    SpeakingActor? Actor = null,
    SurfaceFixture Fixture = SurfaceFixture.None,
    WorldPickup? Pickup = null)
{
    public static SessionCommand Idle => new(SessionAction.None);
}

/// <summary>
/// What the world-scene keys asked for this frame: save, load, talk, the stall, a fixture,
/// a pickup, a door.
///
/// Game1 still owns what those do. This type must not take a <c>Game1</c> reference.
/// </summary>
internal static class SessionInput
{
    public static SessionCommand Step(
        InputRouter input,
        KeyboardState keyboard,
        WorldPoint position,
        float yaw,
        bool runActive,
        DialogueRuntime? dialogue,
        WorldRuntime? world,
        bool onSurface,
        IReadOnlyList<WorldPickup> pickups)
    {
        if (input.Pressed(keyboard, Keys.F5))
            return new SessionCommand(runActive ? SessionAction.BlockedSave : SessionAction.Save);

        if (input.Pressed(keyboard, Keys.F9) && !runActive)
            return new SessionCommand(SessionAction.Load);

        var actor = dialogue?.FindActor(position, yaw);

        if (input.Pressed(keyboard, Keys.P) && actor is not null)
            return new SessionCommand(SessionAction.Pickpocket, actor);

        if (input.Pressed(keyboard, Keys.B)
            && actor is not null
            && actor.Palette.Equals("merchant", System.StringComparison.OrdinalIgnoreCase))
            return new SessionCommand(SessionAction.OpenShop, actor);

        if (!input.Pressed(keyboard, Keys.E)) return SessionCommand.Idle;

        if (actor is not null)
            return new SessionCommand(SessionAction.Talk, actor);

        if (world is null) return SessionCommand.Idle;

        var fixture = onSurface ? Surface.FixtureAt(position) : SurfaceFixture.None;
        if (fixture != SurfaceFixture.None)
            return new SessionCommand(SessionAction.UseFixture, Fixture: fixture);

        var pickup = FindPickup(pickups, position, yaw);
        if (pickup is not null)
            return new SessionCommand(SessionAction.TakePickup, Pickup: pickup);

        return new SessionCommand(SessionAction.OpenDoor);
    }

    /// <summary>
    /// OpenDoor is only barred while something in the room still moves. SessionInput cannot
    /// see the run, so Game1 upgrades OpenDoor to DoorBarred when the way is shut.
    /// </summary>
    public static WorldPickup? FindPickup(IReadOnlyList<WorldPickup> pickups, WorldPoint player,
        float yaw, float range = 3.2f)
    {
        var forward = Targeting.FlatForward(yaw);
        WorldPickup? best = null;
        var bestDistance = float.MaxValue;

        foreach (var pickup in pickups)
        {
            var distance = player.FlatDistanceTo(pickup.Position.ToWorldPoint());
            if (distance > range || distance >= bestDistance) continue;

            var dx = pickup.Position.X - player.X;
            var dz = pickup.Position.Z - player.Z;
            if (distance > 0.001f && (dx * forward.X + dz * forward.Z) / distance < 0.35f)
                continue;

            best = pickup;
            bestDistance = distance;
        }

        return best;
    }
}
