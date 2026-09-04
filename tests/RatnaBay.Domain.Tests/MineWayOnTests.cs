using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// Whether a stranger can find, and get through, the way out of the first room.
///
/// **This is the test the alpha needed and did not have.** The one outside player this build
/// has had spent 119 minutes in a tier-1 mine, walked back into the entrance room eight times,
/// swung an Iron Sword sixty times, took no damage, cleared nothing, and quit at full health.
/// Their recording contains no `room.entered` at all. Two separate faults put them there, and
/// both are asserted below.
///
/// Neither was reachable by the gate at the time. The domain tests never asked what a door
/// says; the packaged self-test renders portraits through a pure pixel function; and
/// `smoke.rbs` turned on noclip and teleported into the room, so it proved a room existed and
/// was fightable without ever proving anybody could reach it.
/// </summary>
[TestFixture]
public sealed class MineWayOnTests
{
    private static WorldManifest Mine(int seed = 4242, int rooms = 4, int depth = 1) =>
        MineGenerator.Generate(seed, rooms, depth);

    /// <summary>
    /// No mine door is locked.
    ///
    /// The generator said so in a comment — "Mine doors are shut, not locked. The gate on
    /// pressing deeper is meant to be the player's nerve, not their Security skill" — while
    /// setting <c>Locked = locked</c> with every caller passing <c>true</c>.
    ///
    /// The door still opened on the first press, because <c>Difficulty</c> is 0 and a pick
    /// succeeds when Security is not below it. What broke was the sentence: the prompt is
    /// chosen on <c>IsLocked</c>, not on whether the pick would succeed, so every door in
    /// every mine read "Locked  |  a key, or Security 0" — a refusal, with no verb, no key
    /// named, and nothing to say that E would open it. It was openable and it read as sealed.
    /// </summary>
    [Test]
    public void NoMineDoorIsLocked()
    {
        foreach (var seed in new[] { 1, 4242, -1054389700, int.MaxValue })
        {
            var locked = Mine(seed).Doors.Where(door => door.Locked).ToList();

            Assert.That(locked, Is.Empty,
                $"seed {seed}: {locked.Count} locked door(s), first '{locked.FirstOrDefault()?.Id}'. "
                + "A locked mine door shows the player a refusal instead of 'Click / E Open door'.");
        }
    }

    /// <summary>
    /// A locked door with no key and no difficulty is the specific shape of the trap, so it is
    /// worth naming separately: it cannot be opened with a key that does not exist, and the
    /// prompt will never mention the pick that would in fact succeed.
    /// </summary>
    [Test]
    public void NoMineDoorAsksForAKeyThatDoesNotExist()
    {
        var impossible = Mine().Doors
            .Where(door => door.Locked && string.IsNullOrEmpty(door.KeyItemId))
            .ToList();

        Assert.That(impossible, Is.Empty,
            "A locked door with no KeyItemId can only ever be picked, and the prompt does not "
            + "say so.");
    }

    /// <summary>
    /// Every corridor is lit at both mouths.
    ///
    /// Rooms have had a light since the generator was written. Corridors never did, so the way
    /// on was an unlit black rectangle in a brown wall, indistinguishable from the wall beside
    /// it. The lamps sit inside the passage: it is light spilling from behind an opening that
    /// makes it read as somewhere to go, and a lamp on the near face would light the wall and
    /// leave the hole just as black.
    /// </summary>
    [Test]
    public void EveryCorridorIsLitAtBothMouths()
    {
        var manifest = Mine();

        var corridors = manifest.Doors
            .Select(door => door.Id[..door.Id.LastIndexOf('.')])
            .Distinct()
            .ToList();

        Assert.That(corridors, Is.Not.Empty, "the fixture mine has no corridors to check");

        foreach (var corridor in corridors)
        {
            var lamps = manifest.Lights.Count(light => light.Id.StartsWith(corridor + ".light"));

            Assert.That(lamps, Is.EqualTo(2),
                $"corridor '{corridor}' has {lamps} lamp(s); it needs one at each mouth, or the "
                + "doorway at the unlit end is a black rectangle in a brown wall.");
        }
    }

    /// <summary>
    /// A corridor lamp is dimmer than a room's, so a room still reads as the destination and
    /// the corridor as the way between. Lighting them equally would make the mine one flat
    /// brightness, which is a different way of giving the player nothing to steer by.
    /// </summary>
    [Test]
    public void CorridorLampsAreDimmerThanRooms()
    {
        var manifest = Mine();

        var corridorLamps = manifest.Lights
            .Where(light => light.Id.Contains(".link") && light.Id.Contains(".light"))
            .ToList();

        var roomLamps = manifest.Lights
            .Where(light => light.Id.EndsWith(".room.light") || light.Id.Contains(".r"))
            .Where(light => !light.Id.Contains(".link"))
            .ToList();

        Assert.That(corridorLamps, Is.Not.Empty, "no corridor lamps found");
        Assert.That(roomLamps, Is.Not.Empty, "no room lamps found");

        var brightestCorridor = corridorLamps.Max(light => light.Intensity);
        var dimmestRoom = roomLamps.Min(light => light.Intensity);

        Assert.That(brightestCorridor, Is.LessThan(dimmestRoom),
            "a corridor lamp is at least as bright as a room's, so the room no longer reads as "
            + "the place to head for.");
    }
}
