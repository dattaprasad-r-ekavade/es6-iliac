using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// A mine with no bottom.
///
/// Press-your-luck cannot work in a level you can finish. A mine of a fixed length ends the
/// run for you, and once it does, pressing on stops being a risk and becomes the way forward
/// until the game says stop — which is exactly what an earlier recording showed. The run has
/// to end because the player decided it did, or because they were wrong.
/// </summary>
public class EndlessMineTests
{
    private static readonly int[] Seeds = { 1, 42, 4211, -808, int.MaxValue };

    private static WorldManifest Deepened(int seed, int segments, int perSegment = 8)
    {
        var request = new MineRequest(seed, perSegment, Depth: 1);
        var mine = MineGenerator.Generate(request);

        for (var segment = 1; segment <= segments; segment++)
            Merge(mine, MineGenerator.Extend(mine, request, segment));

        return mine;
    }

    /// <summary>What the game layer does when a segment arrives: adds, never rewrites.</summary>
    private static void Merge(WorldManifest into, WorldManifest delta)
    {
        into.Geometry.AddRange(delta.Geometry);
        into.Doors.AddRange(delta.Doors);
        into.Lights.AddRange(delta.Lights);
        into.Rooms.AddRange(delta.Rooms);
        into.Spawns.AddRange(delta.Spawns);
    }

    // ---------------------------------------------------------------- it never ends

    [Test]
    public void ThereIsAlwaysAWayDeeper()
    {
        // The property that makes it endless: however far down you are, there is one shut door
        // past the deepest room and nothing behind it yet.
        foreach (var seed in Seeds)
        for (var segments = 0; segments <= 3; segments++)
        {
            var mine = Deepened(seed, segments);
            var deepest = mine.Rooms.Max(room => room.Index);

            Assert.That(mine.Doors.Count(door => door.Id.Contains(".onward.")), Is.EqualTo(segments + 1),
                $"seed {seed}: one way on per segment built");
            Assert.That(deepest, Is.EqualTo((segments + 1) * 8 - 1), $"seed {seed}");
        }
    }

    [Test]
    public void NoMineHasAnExit()
    {
        // Camping and dying are the only ways out of a descent. A door marked "exit" would be
        // a third, and it would be the one a player took every time.
        var mine = Deepened(4211, segments: 2);

        Assert.That(mine.Doors.Any(door => door.Id.Contains("exit", StringComparison.Ordinal)),
            Is.False);
    }

    // ---------------------------------------------------------------- extending is additive

    [Test]
    public void ExtendingAddsAndNeverRewrites()
    {
        // The rule the whole approach rests on. If a segment could rewrite geometry, the door
        // the player had just walked through could swing shut behind them mid-descent.
        var request = new MineRequest(4211, 8, 1);
        var mine = MineGenerator.Generate(request);

        var before = mine.Geometry.Select(box => box.Id).ToHashSet(StringComparer.Ordinal);
        var delta = MineGenerator.Extend(mine, request, 1);

        Assert.That(delta.Geometry.Select(box => box.Id).Any(before.Contains), Is.False,
            "a segment must not name anything that already exists");
    }

    [Test]
    public void ADeepenedMineStillValidates()
    {
        foreach (var seed in Seeds)
        {
            var failures = Deepened(seed, segments: 3).Validate();
            Assert.That(failures, Is.Empty, $"seed {seed}: {string.Join(" ", failures)}");
        }
    }

    [Test]
    public void RoomsAreNumberedStraightThroughTheJoins()
    {
        var rooms = Deepened(42, segments: 2).Rooms.Select(room => room.Index).OrderBy(i => i).ToList();

        Assert.That(rooms, Is.EqualTo(Enumerable.Range(0, rooms.Count).ToList()),
            "a join must not skip or repeat a room number");
    }

    [Test]
    public void NoTwoRoomsEverShareACell()
    {
        // The walk avoids itself within a segment; across segments it has to be told what is
        // already taken, and getting that wrong would build a room inside another one.
        foreach (var seed in Seeds)
        {
            var centres = Deepened(seed, segments: 3).Rooms
                .Select(room => (room.Centre.X, room.Centre.Z))
                .ToList();

            Assert.That(centres.Distinct().Count(), Is.EqualTo(centres.Count), $"seed {seed}");
        }
    }

    // ---------------------------------------------------------------- it keeps getting worse

    [Test]
    public void EveryRoomOfEverySegmentHoldsAFight()
    {
        // Only the very first room of the very first segment is empty. If a segment started
        // with a breather the mine would exhale every eight rooms, and the pressure that makes
        // the door a question would go with it.
        var mine = Deepened(4211, segments: 2);

        for (var room = 1; room <= mine.Rooms.Max(r => r.Index); room++)
        {
            Assert.That(mine.Spawns.Any(spawn => spawn.RoomIndex == room), Is.True,
                $"room {room} is an empty walk");
        }
    }

    [Test]
    public void TheDeepEndIsFarWorseThanTheEntrance()
    {
        var mine = Deepened(4211, segments: 3);
        var deepest = mine.Rooms.Max(room => room.Index);

        var early = mine.Spawns.Where(spawn => spawn.RoomIndex == 1).ToList();
        var late = mine.Spawns.Where(spawn => spawn.RoomIndex == deepest).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(late.Max(spawn => spawn.Level),
                Is.GreaterThan(early.Max(spawn => spawn.Level) + 4),
                "thirty rooms down should be several ranks worse");
            Assert.That(late.Count, Is.GreaterThan(early.Count));
        });
    }

    [Test]
    public void ItGetsHarderWithoutEverGettingEasier()
    {
        var mine = Deepened(99, segments: 3);
        var byRoom = mine.Spawns
            .GroupBy(spawn => spawn.RoomIndex)
            .OrderBy(group => group.Key)
            .Select(group => group.Max(spawn => spawn.Level))
            .ToList();

        foreach (var pair in byRoom.Zip(byRoom.Skip(1)))
            Assert.That(pair.Second, Is.GreaterThanOrEqualTo(pair.First),
                "a segment join must not be a step backwards");
    }

    // ---------------------------------------------------------------- determinism

    [Test]
    public void OneSeedIsOneEndlessMine()
    {
        // Seeds are quoted in bug reports and shared between players, and an endless mine is
        // only reproducible if every segment of it is.
        foreach (var seed in Seeds)
        {
            var first = WorldManifest.Serialize(Deepened(seed, segments: 2));
            var second = WorldManifest.Serialize(Deepened(seed, segments: 2));

            Assert.That(second, Is.EqualTo(first), $"seed {seed} is not reproducible");
        }
    }

    [Test]
    public void DifferentSegmentsOfOneMineAreNotTheSameStretchTwice()
    {
        var request = new MineRequest(4211, 8, 1);
        var mine = MineGenerator.Generate(request);

        var one = MineGenerator.Extend(mine, request, 1);
        Merge(mine, one);
        var two = MineGenerator.Extend(mine, request, 2);

        var shapeOne = string.Join('|', one.Rooms.Select(r => $"{r.Centre.X - one.Rooms[0].Centre.X}"));
        var shapeTwo = string.Join('|', two.Rooms.Select(r => $"{r.Centre.X - two.Rooms[0].Centre.X}"));

        Assert.That(shapeTwo, Is.Not.EqualTo(shapeOne));
    }

    [Test]
    public void ExtendingNothingReturnsNothingRatherThanThrowing()
    {
        var empty = new WorldManifest { Version = 1, Id = "mine.empty" };

        Assert.That(MineGenerator.Extend(empty, new MineRequest(1), 1).Rooms, Is.Empty);
    }
}
