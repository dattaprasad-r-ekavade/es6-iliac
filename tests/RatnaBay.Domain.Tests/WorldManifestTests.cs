namespace RatnaBay.Domain.Tests;

public sealed class WorldManifestTests
{
    [Test]
    public void ValidManifestParsesAndExposesSolids()
    {
        const string json = """
            {
              "version": 1,
              "id": "test.room",
              "playerSpawn": { "position": { "x": 0, "y": 2.4, "z": 4 }, "yaw": 0 },
              "geometry": [
                { "id": "floor", "min": { "x": -5, "y": -0.5, "z": -5 }, "max": { "x": 5, "y": 0, "z": 5 } }
              ],
              "props": [],
              "pickups": [
                {
                  "id": "potion",
                  "itemId": "health_potion",
                  "name": "Health Potion",
                  "kind": "potion",
                  "count": 2,
                  "position": { "x": 1, "y": 0.5, "z": 1 },
                  "model": "cheeseBox",
                  "scale": 0.5
                }
              ],
              "doors": []
            }
            """;

        var parsed = WorldManifest.TryParse(json, out var manifest, out var error);

        Assert.That(parsed, Is.True, error);
        Assert.That(manifest!.Geometry.Single().ToCollisionBox().Id, Is.EqualTo("floor"));
        Assert.That(manifest.PlayerSpawn.Position.ToWorldPoint(), Is.EqualTo(new WorldPoint(0f, 2.4f, 4f)));
        Assert.That(manifest.Pickups.Single().ItemId, Is.EqualTo("health_potion"));
        Assert.That(manifest.Pickups.Single().Count, Is.EqualTo(2));
    }

    [Test]
    public void InvalidPickupScaleIsRejected()
    {
        var manifest = new WorldManifest
        {
            Id = "bad.pickup",
            Pickups = new List<WorldPickup>
            {
                new() { Id = "potion", ItemId = "health_potion", Name = "Potion",
                    Kind = "potion", Scale = 0f }
            }
        };

        Assert.That(manifest.Validate(), Has.Some.Contains("scale must be positive"));
    }

    [Test]
    public void DuplicateIdsAreRejected()
    {
        const string json = """
            {
              "version": 1,
              "id": "bad.room",
              "playerSpawn": { "position": { "x": 0, "y": 2.4, "z": 4 } },
              "geometry": [
                { "id": "same", "min": { "x": -1, "y": 0, "z": -1 }, "max": { "x": 1, "y": 1, "z": 1 } }
              ],
              "props": [
                { "id": "same", "model": "rock", "position": { "x": 0, "y": 0, "z": 0 } }
              ]
            }
            """;

        var parsed = WorldManifest.TryParse(json, out _, out var error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Does.Contain("duplicate world id 'same'"));
    }
}
