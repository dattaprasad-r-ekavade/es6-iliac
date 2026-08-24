using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The generator's contract.
///
/// These are deliberately written against the manifest rather than against the generator's
/// internals: what matters is that the thing it produces is a level the existing game can load,
/// validate and walk through. A generator that satisfies its own private invariants and emits an
/// unplayable room has done nothing.
/// </summary>
public class MineGeneratorTests
{
    /// <summary>A spread of seeds, so a property is asserted rather than one lucky layout.</summary>
    private static readonly int[] Seeds =
        { 1, 2, 7, 42, 99, 1234, 4211, 65535, -3, -808, int.MaxValue, int.MinValue };

    private static IEnumerable<WorldGeometry> Solids(WorldManifest mine) =>
        mine.Geometry.Where(geometry => geometry.Solid);

    /// <summary>Room centres, in the order the mine is walked.</summary>
    private static List<WorldPoint> RoomCentres(WorldManifest mine) => mine.Rooms
        .OrderBy(room => room.Index)
        .Select(room => new WorldPoint(room.Centre.X, 1.5f, room.Centre.Z))
        .ToList();

    private static StaticCollisionIndex IndexOf(WorldManifest mine, bool includeDoors)
    {
        var boxes = Solids(mine).Select(geometry => geometry.ToCollisionBox());
        if (includeDoors) boxes = boxes.Concat(mine.Doors.Select(door => door.ToCollisionBox()));

        var index = new StaticCollisionIndex();
        index.Rebuild(boxes);
        return index;
    }

    // ---------------------------------------------------------------- the format

    [Test]
    public void EverySeedProducesAManifestTheGameCanAlreadyLoad()
    {
        // The entire reason for emitting the authored format: a generated mine has to survive
        // the same parse and validation path as a hand-written one, with no special case.
        foreach (var seed in Seeds)
        {
            var json = WorldManifest.Serialize(MineGenerator.Generate(seed, rooms: 5));
            var loaded = WorldManifest.TryParse(json, out var mine, out var error);

            Assert.That(loaded, Is.True, $"seed {seed}: {error}");
            Assert.That(mine!.Geometry, Is.Not.Empty, $"seed {seed}");
        }
    }

    [Test]
    public void EverySeedAndShapeValidates()
    {
        foreach (var seed in Seeds)
        foreach (var rooms in new[] { 2, 3, 5, 8, 12, 18, 30 })
        {
            var failures = MineGenerator.Generate(seed, rooms).Validate();
            Assert.That(failures, Is.Empty, $"seed {seed}, {rooms} rooms: {string.Join(" ", failures)}");
        }
    }

    [Test]
    public void TwoMinesNeverShareADoorId()
    {
        // The bug this exists to prevent, found in play: every mine named its doors
        // "link00.door" upward, the save remembered which doors had been opened, and so every
        // descent after the first began with its first nine doors already standing open. Nine
        // rooms were cleared and the camp decision was offered exactly once.
        var first = MineGenerator.Generate(1, rooms: 12).Doors.Select(door => door.Id).ToList();
        var second = MineGenerator.Generate(2, rooms: 12).Doors.Select(door => door.Id).ToList();

        Assert.That(first.Intersect(second, StringComparer.Ordinal), Is.Empty);
    }

    [Test]
    public void AMineDoorIsNeverWrittenToTheSave()
    {
        // Unique ids alone are not enough. A mine is rebuilt from its seed on every descent,
        // so returning to the same seed for a fallen cache must still find its doors shut.
        var mine = MineGenerator.Generate(4211, rooms: 10);

        Assert.That(mine.Doors, Has.All.Property(nameof(WorldDoor.Remembered)).False);
    }

    [Test]
    public void NoTwoThingsInAMineShareAnId()
    {
        // Validation already rejects duplicates, so this is really a test that the generator
        // never leans on collisions being silently tolerated.
        var mine = MineGenerator.Generate(4211, rooms: 12);
        var ids = mine.Geometry.Select(item => item.Id)
            .Concat(mine.Doors.Select(item => item.Id))
            .Concat(mine.Lights.Select(item => item.Id))
            .Concat(mine.Spawns.Select(item => item.Id))
            .ToList();

        Assert.That(ids.Distinct(StringComparer.Ordinal).ToList(), Has.Count.EqualTo(ids.Count));
    }

    // ---------------------------------------------------------------- determinism

    [Test]
    public void TheSameSeedAlwaysProducesTheSameMine()
    {
        // Seeds get quoted in bug reports and shared between players. If this ever fails, every
        // "try seed 4211" is worthless and no crash report can be reproduced.
        foreach (var seed in Seeds)
        {
            var first = WorldManifest.Serialize(MineGenerator.Generate(seed, rooms: 6, depth: 2));
            var second = WorldManifest.Serialize(MineGenerator.Generate(seed, rooms: 6, depth: 2));

            Assert.That(second, Is.EqualTo(first), $"seed {seed} is not reproducible");
        }
    }

    [Test]
    public void DifferentSeedsProduceDifferentMines()
    {
        var layouts = Seeds
            .Select(seed => string.Join('|', RoomCentres(MineGenerator.Generate(seed, rooms: 6))
                .Select(centre => $"{centre.X:0.#},{centre.Z:0.#}")))
            .ToList();

        // Not every seed need differ — with a weighted walk some collisions are expected — but
        // a generator producing one shape for every seed is the failure this exists to catch.
        Assert.That(layouts.Distinct(StringComparer.Ordinal).Count(),
            Is.GreaterThan(Seeds.Length / 2), "seeds barely change the layout");
    }

    [Test]
    public void MinesActuallyTurnCorners()
    {
        // The bug this exists to prevent, which shipped once: the walk was correct but its
        // random source was a bare xorshift, and the low bits it selected directions with were
        // correlated enough to return the same step several times running. Every seed produced
        // a straight corridor. Every other test in this file passed.
        var withCorners = 0;

        foreach (var seed in Seeds)
        {
            var centres = RoomCentres(MineGenerator.Generate(seed, rooms: 8));
            var steps = centres.Zip(centres.Skip(1),
                (a, b) => (X: MathF.Sign(b.X - a.X), Z: MathF.Sign(b.Z - a.Z))).ToList();

            if (steps.Zip(steps.Skip(1), (a, b) => a != b).Any(changed => changed))
                withCorners++;
        }

        Assert.That(withCorners, Is.GreaterThan(Seeds.Length / 2),
            $"only {withCorners} of {Seeds.Length} mines bend at all — these are corridors, not caves");
    }

    [Test]
    public void TheIdCarriesTheSeedAndTheDepth()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MineGenerator.Generate(4211, depth: 3).Id, Does.Contain("03"));
            Assert.That(MineGenerator.Generate(4211, depth: 3).Id,
                Is.Not.EqualTo(MineGenerator.Generate(4212, depth: 3).Id));
        });
    }

    // ---------------------------------------------------------------- shape

    [Test]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(12)]
    public void TheRequestedNumberOfRoomsIsBuilt(int rooms)
    {
        Assert.That(RoomCentres(MineGenerator.Generate(99, rooms)), Has.Count.EqualTo(rooms));
    }

    [Test]
    public void AnAbsurdRequestIsClampedRatherThanRefused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RoomCentres(MineGenerator.Generate(7, rooms: 0)),
                Has.Count.EqualTo(MineRequest.MinRooms));
            Assert.That(RoomCentres(MineGenerator.Generate(7, rooms: 500)),
                Has.Count.EqualTo(MineRequest.MaxRooms));
            Assert.That(MineGenerator.Generate(7, depth: -4).Spawns,
                Has.All.Property(nameof(WorldEnemySpawn.Level)).EqualTo(1));
        });
    }

    [Test]
    public void RoomsNeverOverlap()
    {
        foreach (var seed in Seeds)
        {
            var centres = RoomCentres(MineGenerator.Generate(seed, rooms: 12));
            foreach (var pair in centres.SelectMany(
                (a, i) => centres.Skip(i + 1).Select(b => (a, b))))
            {
                Assert.That(pair.a.FlatDistanceTo(pair.b), Is.GreaterThan(17f),
                    $"seed {seed}: two rooms are on top of each other");
            }
        }
    }

    [Test]
    public void NothingIsWaitingAtADoorway()
    {
        // Reported from play: enemies appeared to spawn on top of the door the player was
        // walking through. The old rule excluded a band across the end of the room, which
        // still allowed a body three metres inside the doorway.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 8);

            foreach (var spawn in mine.Spawns)
            foreach (var door in mine.Doors)
            {
                var centre = new WorldPoint(
                    (door.Min.X + door.Max.X) * 0.5f, 0f, (door.Min.Z + door.Max.Z) * 0.5f);
                var at = new WorldPoint(spawn.Position.X, 0f, spawn.Position.Z);

                Assert.That(at.FlatDistanceTo(centre), Is.GreaterThan(5f),
                    $"seed {seed}: '{spawn.Id}' is loitering at '{door.Id}'");
            }
        }
    }

    [Test]
    public void EveryRoomPastTheThirdHasSomethingThatShootsBack()
    {
        // The structural answer to a recorded run that cleared seven of nine rooms from the
        // previous doorway. Every enemy chasing in a straight line makes a corridor the
        // safest place in the mine; something that shoots and gives ground makes it the worst.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 12);
            for (var room = 3; room < 12; room++)
            {
                var archers = mine.Spawns.Count(spawn =>
                    spawn.RoomIndex == room && spawn.ArchetypeId == EnemyCatalog.ArcherId);

                Assert.That(archers, Is.GreaterThan(0),
                    $"seed {seed}: room {room} can still be fought from the doorway");
            }
        }
    }

    [Test]
    public void TheFirstRoomsTeachMeleeBeforeAnythingShoots()
    {
        // A shooter in room one, before the player has found the attack button, is not a
        // lesson — it is an ambush.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 12);
            var early = mine.Spawns.Where(spawn => spawn.RoomIndex < 3);

            Assert.That(early.Any(spawn => spawn.ArchetypeId == EnemyCatalog.ArcherId), Is.False,
                $"seed {seed}: something is shooting in the opening rooms");
        }
    }

    [Test]
    public void EveryFightKnowsWhichRoomItIsIn()
    {
        // The run ledger needs this to know when a room is clear. Without it the game layer
        // would have to re-derive the level's structure from the boxes it was flattened into.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 8);
            foreach (var spawn in mine.Spawns)
            {
                var room = mine.Rooms.SingleOrDefault(candidate => candidate.Index == spawn.RoomIndex);
                Assert.That(room, Is.Not.Null, $"seed {seed}: '{spawn.Id}' names no room");
                Assert.That(room!.Contains(spawn.Position.X, spawn.Position.Z), Is.True,
                    $"seed {seed}: '{spawn.Id}' is not actually inside room {spawn.RoomIndex}");
            }
        }
    }

    [Test]
    public void EveryRoomIsJoinedToTheNextByADoor()
    {
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 6);

            // Five doors between six rooms, plus the one on the way out.
            Assert.That(mine.Doors, Has.Count.EqualTo(6), $"seed {seed}");
        }
    }

    // ---------------------------------------------------------------- playability

    /// <summary>
    /// Walk, rather than raycast.
    ///
    /// The first version of these tests fired a ray between room centres, and passed happily
    /// with the doorways sealed to two millimetres — an infinitely thin ray threads any gap at
    /// all. Only the real swept mover, at the real body radius, answers the question actually
    /// being asked: can a player get through there.
    /// </summary>
    private static bool Walk(StaticCollisionIndex index, ref WorldPoint position, WorldPoint target)
    {
        const float radius = 0.45f;
        const float stride = 0.12f;

        for (var step = 0; step < 6000; step++)
        {
            var dx = target.X - position.X;
            var dz = target.Z - position.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance < 0.7f) return true;

            var scale = MathF.Min(stride, distance) / distance;
            var moved = index.Move(position, new WorldPoint(dx * scale, 0f, dz * scale), radius);

            // Pressed against something and going nowhere.
            if (moved.FlatDistanceTo(position) < 0.0005f) return false;
            position = moved;
        }

        return false;
    }

    private static WorldPoint Standing(float x, float z) => new(x, 1.7f, z);

    [Test]
    public void TheWholeMineCanBeWalkedFromTheSpawnToTheWayOut()
    {
        // The claim that matters, and the iteration's definition of done: with the doors open,
        // a body of the player's actual width can get from the entrance to the far end. A
        // generator that produces a sealed room passes every other test in this file.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 8);
            var geometry = IndexOf(mine, includeDoors: false);
            var route = RoomCentres(mine);
            var position = Standing(mine.PlayerSpawn.Position.X, mine.PlayerSpawn.Position.Z);

            for (var index = 0; index < route.Count; index++)
            {
                Assert.That(Walk(geometry, ref position, route[index]), Is.True,
                    $"seed {seed}: the player cannot reach room {index}");
            }

            var exit = mine.Doors.Single(door => door.Id.EndsWith(".exit.door", StringComparison.Ordinal));
            Assert.That(
                Walk(geometry, ref position,
                    Standing((exit.Min.X + exit.Max.X) * 0.5f, (exit.Min.Z + exit.Max.Z) * 0.5f)),
                Is.True, $"seed {seed}: the mine has no way out");
        }
    }

    [Test]
    public void EveryDoorActuallyStandsInTheWay()
    {
        // The mirror of the test above. If a door were emitted beside the passage rather than
        // in it, the mine would still be walkable — and the press-on decision would be a lie,
        // because the player could stroll past the choice.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 6);
            var sealedOff = IndexOf(mine, includeDoors: true);
            var route = RoomCentres(mine);
            var position = Standing(mine.PlayerSpawn.Position.X, mine.PlayerSpawn.Position.Z);

            Assert.That(Walk(sealedOff, ref position, route[0]), Is.True,
                $"seed {seed}: the player is trapped on the spot");
            Assert.That(Walk(sealedOff, ref position, route[1]), Is.False,
                $"seed {seed}: the second room can be reached without opening anything");
        }
    }

    [Test]
    public void ThePlayerNeverSpawnsInsideSomething()
    {
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 6);
            var spawn = mine.PlayerSpawn.Position;

            Assert.That(Solids(mine).Any(solid => Contains(solid, spawn.X, spawn.Z)), Is.False,
                $"seed {seed}: the player spawns inside a wall");
        }
    }

    [Test]
    public void NoEnemySpawnsInsideAWall()
    {
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 10, depth: 3);
            foreach (var spawn in mine.Spawns)
            {
                Assert.That(
                    Solids(mine).Any(solid => Contains(solid, spawn.Position.X, spawn.Position.Z)),
                    Is.False, $"seed {seed}: '{spawn.Id}' is buried in geometry");
            }
        }
    }

    /// <summary>Inside in plan view, allowing for the width of a body.</summary>
    private static bool Contains(WorldGeometry solid, float x, float z)
    {
        const float body = 0.45f;

        // Floors and ceilings cover the whole room in plan; only things at body height count.
        if (solid.Max.Y <= 0.2f || solid.Min.Y >= 3f) return false;

        return x > solid.Min.X - body && x < solid.Max.X + body
            && z > solid.Min.Z - body && z < solid.Max.Z + body;
    }

    // ---------------------------------------------------------------- population

    [Test]
    public void TheFirstRoomIsAlwaysEmpty()
    {
        // A run that can be hit before the player has finished reading the screen is not
        // difficult, it is unfair. The authored world made the same call for the same reason.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 6);
            Assert.That(mine.Spawns.Any(spawn => spawn.Id.Contains("room00")), Is.False,
                $"seed {seed}: something is waiting in the entrance room");
        }
    }

    [Test]
    public void EveryRoomAfterTheEntranceHasAFightInIt()
    {
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 6);
            for (var index = 1; index < 6; index++)
            {
                Assert.That(mine.Spawns.Any(spawn => spawn.Id.Contains($"room{index:00}")),
                    Is.True, $"seed {seed}: room {index} is an empty walk");
            }
        }
    }

    [Test]
    public void EverySpawnNamesAnEnemyThatExists()
    {
        foreach (var seed in Seeds)
        foreach (var spawn in MineGenerator.Generate(seed, rooms: 10, depth: 4).Spawns)
        {
            Assert.That(EnemyCatalog.Resolve(spawn), Is.Not.Null,
                $"seed {seed}: '{spawn.ArchetypeId}' is not in the catalogue");
        }
    }

    [Test]
    public void ADeeperMineSpawnsTougherEnemies()
    {
        var shallow = EnemyCatalog.Resolve(MineGenerator.Generate(42, depth: 1).Spawns[0])!;
        var deep = EnemyCatalog.Resolve(MineGenerator.Generate(42, depth: 5).Spawns[0])!;

        Assert.Multiple(() =>
        {
            Assert.That(deep.MaxHealth, Is.GreaterThan(shallow.MaxHealth));
            Assert.That(deep.AttackDamage, Is.GreaterThan(shallow.AttackDamage));
            Assert.That(deep.XpReward, Is.GreaterThan(shallow.XpReward),
                "the reward has to grow with the risk, or nobody presses on");
        });
    }

    [Test]
    public void EachStretchOfTheMineIsWorseThanTheOneBefore()
    {
        // Depth has to bite, or a long mine is only a long walk. This was the second half of
        // the fix for a session where the player pressed on at every door: with the payout
        // rising and the danger flat, pressing on was simply correct every time.
        foreach (var seed in Seeds)
        {
            var mine = MineGenerator.Generate(seed, rooms: 20);

            var byRoom = mine.Spawns
                .GroupBy(spawn => spawn.RoomIndex)
                .OrderBy(group => group.Key)
                .Select(group => (Room: group.Key, Count: group.Count(),
                    Level: group.Max(spawn => spawn.Level)))
                .ToList();

            var first = byRoom[0];
            var last = byRoom[^1];

            Assert.Multiple(() =>
            {
                Assert.That(last.Count, Is.GreaterThan(first.Count),
                    $"seed {seed}: the far end is no busier than the entrance");
                Assert.That(last.Level, Is.GreaterThan(first.Level),
                    $"seed {seed}: the far end is no more dangerous than the entrance");
            });

            // Never gets easier on the way down, at any point.
            foreach (var pair in byRoom.Zip(byRoom.Skip(1)))
            {
                Assert.That(pair.Second.Level, Is.GreaterThanOrEqualTo(pair.First.Level),
                    $"seed {seed}: room {pair.Second.Room} is a step backwards");
            }
        }
    }

    [Test]
    public void ADeepMineKeepsGettingHarderRatherThanPlateauing()
    {
        // The count of bodies has a ceiling — a room only holds so many — so the escalation
        // past that point has to come from their level, or the mine has a difficulty cap and
        // a good player can walk to the bottom of it.
        var mine = MineGenerator.Generate(4211, rooms: 28);
        var deepest = mine.Spawns.Max(spawn => spawn.Level);
        var atRoomTen = mine.Spawns.Where(spawn => spawn.RoomIndex == 10).Max(spawn => spawn.Level);

        Assert.That(deepest, Is.GreaterThan(atRoomTen),
            "past the point where rooms are full, only levels can carry the difficulty");
    }

    [Test]
    public void EveryRoomOfADeepMineIsActuallyPopulated()
    {
        // Deep rooms hold five bodies in a space that must also keep its doorways clear. If
        // placement quietly gives up, the mine gets easier the deeper it goes — the exact
        // opposite of the intent, and invisible without this.
        // Swept wide rather than over the usual dozen seeds. Placement is rejection sampling
        // against two doorway exclusion zones, so it fails rarely and unluckily — a handful of
        // seeds would have called it fine right up until a player found the seed that was not.
        var shortfalls = new List<string>();

        for (var seed = -250; seed < 250; seed++)
        {
            var mine = MineGenerator.Generate(seed, rooms: 20);
            for (var room = 1; room < 20; room++)
            {
                var count = mine.Spawns.Count(spawn => spawn.RoomIndex == room);
                var wanted = Math.Min(5, 1 + room / 2 + (room == 19 ? 1 : 0));

                if (count < wanted) shortfalls.Add($"seed {seed} room {room}: {count}/{wanted}");
            }
        }

        Assert.That(shortfalls, Is.Empty,
            $"{shortfalls.Count} under-filled rooms, e.g. {string.Join("; ", shortfalls.Take(5))}");
    }

    [Test]
    public void TheLastRoomIsTheBusiest()
    {
        var mine = MineGenerator.Generate(4211, rooms: 6);
        var lastRoom = mine.Spawns.Count(spawn => spawn.Id.Contains("room05"));
        var secondRoom = mine.Spawns.Count(spawn => spawn.Id.Contains("room01"));

        Assert.That(lastRoom, Is.GreaterThan(secondRoom));
    }

    [Test]
    public void NoTwoEnemiesAreStandingInEachOther()
    {
        foreach (var seed in Seeds)
        {
            var spawns = MineGenerator.Generate(seed, rooms: 8).Spawns;
            foreach (var pair in spawns.SelectMany(
                (a, i) => spawns.Skip(i + 1).Select(b => (a, b))))
            {
                var distance = new WorldPoint(pair.a.Position.X, 0f, pair.a.Position.Z)
                    .FlatDistanceTo(new WorldPoint(pair.b.Position.X, 0f, pair.b.Position.Z));

                // Against the generator's own rule rather than a number copied out of it,
                // so tuning the packing does not quietly turn this test into a formality.
                Assert.That(distance,
                    Is.GreaterThanOrEqualTo(MineGenerator.SpawnSeparation).Within(0.01f),
                    $"seed {seed}: '{pair.a.Id}' and '{pair.b.Id}' overlap");
            }
        }
    }
}
