using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>One of the fort's occupants, standing in their own chamber.</summary>
internal sealed record FortOccupant(FortRoom Room, WorldPoint Position);

/// <summary>
/// The fort while the player is standing in it.
///
/// Thin on purpose. Where everybody stands is <see cref="FortHall"/>'s answer, whether a room
/// is open is <see cref="FortRoom.IsOpen"/>'s, and what is said is the roster's — this only
/// holds the list for the frame and answers "who is in front of me".
///
/// Built once per visit rather than once per session, because rank can change between visits
/// and the set of doors that stand open with it.
/// </summary>
internal sealed class FortRuntime
{
    private readonly List<FortOccupant> _occupants;

    public FortRuntime()
    {
        _occupants = FortRoster.All
            .Select(room => new FortOccupant(room, FortHall.OccupantPosition(room.Id)))
            .ToList();
    }

    public IReadOnlyList<FortOccupant> Occupants => _occupants;

    /// <summary>
    /// Who the player is close enough to, and facing.
    ///
    /// The same shape as DialogueRuntime.FindActor, and deliberately so: two ways of deciding
    /// "am I talking to this person" would eventually disagree, and the one that disagreed
    /// would be the one nobody had a test for.
    /// </summary>
    public FortOccupant? FindOccupant(WorldPoint player, float yaw, float range = 3.2f)
    {
        var forward = Targeting.FlatForward(yaw);
        FortOccupant? best = null;
        var bestDistance = float.MaxValue;

        foreach (var occupant in _occupants)
        {
            var distance = player.FlatDistanceTo(occupant.Position);
            if (distance > range || distance >= bestDistance) continue;

            var dx = occupant.Position.X - player.X;
            var dz = occupant.Position.Z - player.Z;
            if (distance > 0.001f && (dx * forward.X + dz * forward.Z) / distance < 0.45f)
                continue;

            best = occupant;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// Which doors should stand open for this rank.
    ///
    /// Answered here and applied to the world, rather than baked into the manifest, so the
    /// fort is one cacheable world rather than one per rank.
    /// </summary>
    public IEnumerable<string> OpenDoorsFor(Rank rank) =>
        _occupants.Where(o => o.Room.IsOpen(rank)).Select(o => FortHall.DoorId(o.Room.Id));

    /// <summary>Where to draw somebody, in the renderer's space.</summary>
    public static Vector3 Feet(FortOccupant occupant) =>
        new(occupant.Position.X, occupant.Position.Y, occupant.Position.Z);
}
