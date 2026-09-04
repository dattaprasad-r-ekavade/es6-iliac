using RatnaBay.Domain;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The fort as a place you can actually walk through.
///
/// **These assertions stand in for walking it.** A hall is only correct if a person can get
/// from the entrance to each occupant, and that is a property of solid boxes rather than of
/// anything visible in a screenshot — a doorway that never got cut, a chamber wall closed
/// across its own entrance, or an occupant standing inside the masonry all look perfectly fine
/// from the middle of the hall and are all impassable.
///
/// The one real player this game has had spent 110 minutes unable to get out of a room because
/// nothing asserted that the way on was open. That is the failure this fixture exists to make
/// impossible in the fort.
/// </summary>
[TestFixture]
public sealed class FortHallTests
{
    private static WorldManifest Hall() => FortHall.Build();

    /// <summary>Standing height. A point clear at the floor and blocked at head height is blocked.</summary>
    private const float Head = 1.6f;

    private static bool IsSolidAt(WorldManifest manifest, float x, float y, float z) =>
        manifest.Geometry.Any(box =>
            box.Solid
            && x > box.Min.X && x < box.Max.X
            && y > box.Min.Y && y < box.Max.Y
            && z > box.Min.Z && z < box.Max.Z);

    [Test]
    public void ThereIsAChamberAndADoorForEveryRoomOnTheRoster()
    {
        var manifest = Hall();

        foreach (var room in FortRoster.All)
        {
            Assert.Multiple(() =>
            {
                Assert.That(manifest.Geometry.Any(g => g.Id.Contains(room.Id)), Is.True,
                    $"{room.Id} has no geometry");
                Assert.That(manifest.Doors.Any(d => d.Id == FortHall.DoorId(room.Id)), Is.True,
                    $"{room.Id} has no door");
            });
        }

        Assert.That(manifest.Doors, Has.Count.EqualTo(FortRoster.All.Count));
    }

    /// <summary>
    /// The fort is held to the same rules as any authored world.
    ///
    /// It is generated in code rather than loaded from a file, which is exactly why this is
    /// worth asserting: nothing on disk means nothing for `RatnaBay.Tools validate` to check,
    /// so the one gate that reads every other world never sees this one.
    /// </summary>
    [Test]
    public void TheFortIsAValidWorld()
    {
        Assert.That(Hall().Validate(), Is.Empty);
    }

    [Test]
    public void EveryIdIsUnique()
    {
        var manifest = Hall();

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Geometry.Select(g => g.Id).Distinct().Count(),
                Is.EqualTo(manifest.Geometry.Count), "two boxes share an id");
            Assert.That(manifest.Doors.Select(d => d.Id).Distinct().Count(),
                Is.EqualTo(manifest.Doors.Count), "two doors share an id");
            Assert.That(manifest.Lights.Select(l => l.Id).Distinct().Count(),
                Is.EqualTo(manifest.Lights.Count), "two lights share an id");
        });
    }

    /// <summary>
    /// The player does not begin inside a wall.
    ///
    /// Cheap to assert and unpleasant to discover: a spawn a hand's width into the masonry
    /// looks like the game failing to start.
    /// </summary>
    [Test]
    public void TheSpawnIsStandingInTheHall()
    {
        var manifest = Hall();
        var spawn = FortHall.Spawn;

        Assert.That(IsSolidAt(manifest, spawn.X, Head, spawn.Z), Is.False,
            "the player spawns inside solid geometry");
    }

    /// <summary>
    /// Every occupant is standing in their own room rather than in its wall.
    /// </summary>
    [Test]
    public void NoOccupantIsStandingInsideTheMasonry()
    {
        var manifest = Hall();

        foreach (var room in FortRoster.All)
        {
            var at = FortHall.OccupantPosition(room.Id);

            Assert.That(IsSolidAt(manifest, at.X, Head, at.Z), Is.False,
                $"{room.Occupant} is inside a wall in {room.Id}");
        }
    }

    /// <summary>
    /// **The one that matters: every doorway is actually cut.**
    ///
    /// A chamber whose hall wall was never opened is a room nobody can enter, and it is
    /// invisible — the door still draws, the occupant still exists, the panel still lists them.
    /// Checked at head height across the full thickness of the wall, because a gap cut in the
    /// floor course and not the one above it stops a person just as well.
    /// </summary>
    [Test]
    public void EveryChamberCanBeWalkedIntoFromTheHall()
    {
        var manifest = Hall();

        foreach (var room in FortRoster.All)
        {
            var door = manifest.Doors.Single(d => d.Id == FortHall.DoorId(room.Id));
            var z = (door.Min.Z + door.Max.Z) * 0.5f;

            // Step across the wall from the hall side to the chamber side.
            for (var t = 0f; t <= 1f; t += 0.1f)
            {
                var x = door.Min.X + (door.Max.X - door.Min.X) * t;

                Assert.That(IsSolidAt(manifest, x, Head, z), Is.False,
                    $"{room.Id}: the doorway is blocked at x={x:0.00}");
            }
        }
    }

    /// <summary>
    /// And the walk continues: from inside the doorway to where the occupant stands.
    ///
    /// Cutting the doorway is not enough if the chamber's own side wall runs across it.
    /// </summary>
    [Test]
    public void EveryOccupantCanBeReachedFromTheirDoorway()
    {
        var manifest = Hall();

        foreach (var room in FortRoster.All)
        {
            var door = manifest.Doors.Single(d => d.Id == FortHall.DoorId(room.Id));
            var from = new WorldPoint((door.Min.X + door.Max.X) * 0.5f, 0f,
                (door.Min.Z + door.Max.Z) * 0.5f);
            var to = FortHall.OccupantPosition(room.Id);

            for (var step = 0; step <= 20; step++)
            {
                var t = step / 20f;
                var x = from.X + (to.X - from.X) * t;
                var z = from.Z + (to.Z - from.Z) * t;

                Assert.That(IsSolidAt(manifest, x, Head, z), Is.False,
                    $"{room.Id}: blocked between the door and {room.Occupant} at t={t:0.00}");
            }
        }
    }

    /// <summary>
    /// The hall itself is walkable end to end, so every door can be reached without opening
    /// any other. A fort where room nine is behind room eight is a fort with a progression the
    /// roster does not know about.
    /// </summary>
    [Test]
    public void TheHallIsClearFromTheEntranceToEveryDoor()
    {
        var manifest = Hall();
        var doors = manifest.Doors.Select(d => (d.Min.Z + d.Max.Z) * 0.5f).ToList();
        var from = FortHall.Spawn.Z;

        foreach (var z in doors)
        {
            var steps = 40;
            for (var step = 0; step <= steps; step++)
            {
                var at = from + (z - from) * (step / (float)steps);

                Assert.That(IsSolidAt(manifest, 0f, Head, at), Is.False,
                    $"the hall is blocked at z={at:0.00}");
            }
        }
    }

    /// <summary>
    /// No two occupants share a spot, which would mean two chambers had been placed on top of
    /// each other and only one of them would ever be seen.
    /// </summary>
    [Test]
    public void EveryOccupantStandsSomewhereDifferent()
    {
        var seen = new List<WorldPoint>();

        foreach (var room in FortRoster.All)
        {
            var at = FortHall.OccupantPosition(room.Id);

            Assert.That(seen.Any(other => other.FlatDistanceTo(at) < 1f), Is.False,
                $"{room.Id} shares a spot with another room");

            seen.Add(at);
        }
    }

    /// <summary>
    /// A fort door is shut, not locked — the same rule the mine follows, and for the reason
    /// that cost the alpha its first player. Whether a room opens is rank, which the roster
    /// answers; a door reporting itself locked shows a refusal with no verb in it.
    /// </summary>
    [Test]
    public void NoFortDoorIsLocked()
    {
        Assert.That(Hall().Doors.Where(d => d.Locked), Is.Empty);
    }

    /// <summary>
    /// A fort door stays open once opened. The mine's doors must not — it is rebuilt every
    /// descent — but this is a place that persists, and a room the player has earned should
    /// not shut itself between runs.
    /// </summary>
    [Test]
    public void AFortDoorIsRememberedUnlikeAMineDoor()
    {
        Assert.That(Hall().Doors, Has.All.Matches<WorldDoor>(d => d.Remembered));
    }

    /// <summary>Rank lives on the roster, never in the geometry, or the fort would have to be
    /// rebuilt on every promotion and could never be cached or validated.</summary>
    [Test]
    public void TheGeometryIsTheSameWhateverTheRank()
    {
        var first = Hall();
        var second = Hall();

        Assert.That(first.Geometry.Count, Is.EqualTo(second.Geometry.Count));
        Assert.That(first.Doors.Select(d => d.Id), Is.EqualTo(second.Doors.Select(d => d.Id)));
    }

    /// <summary>A door names its room, and a room names its door, without a lookup table.</summary>
    [Test]
    public void ADoorCanBeTracedBackToItsRoom()
    {
        foreach (var room in FortRoster.All)
        {
            Assert.That(FortHall.RoomOfDoor(FortHall.DoorId(room.Id)), Is.EqualTo(room.Id));
        }

        Assert.That(FortHall.RoomOfDoor("mine.01.link00.door"), Is.Null);
        Assert.That(FortHall.RoomOfDoor(null), Is.Null);
    }

    /// <summary>Every chamber is lit. An unlit doorway is the fault that stopped the one real
    /// player this game has had.</summary>
    [Test]
    public void EveryChamberIsLit()
    {
        var manifest = Hall();

        foreach (var room in FortRoster.All)
        {
            Assert.That(manifest.Lights.Any(l => l.Id.Contains(room.Id)), Is.True,
                $"{room.Id} is dark");
        }
    }
}
