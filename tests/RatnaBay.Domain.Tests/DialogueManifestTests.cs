namespace RatnaBay.Domain.Tests;

public sealed class DialogueManifestTests
{
    [Test]
    public void ValidManifestParsesTopicsAndActors()
    {
        const string json = """
            {
              "version": 1,
              "id": "dialogue.test",
              "actors": [
                {
                  "id": "actor.guard",
                  "displayName": "Guard",
                  "position": { "x": 1, "y": 0, "z": 2 },
                  "opensWith": [ "road" ]
                }
              ],
              "topics": [
                {
                  "id": "topic.road",
                  "keyword": "road",
                  "conditions": [
                    { "key": "player.channeled", "operator": "min", "value": "1" }
                  ],
                  "response": "The road is north."
                }
              ]
            }
            """;

        var parsed = DialogueManifest.TryParse(json, out var manifest, out var error);

        Assert.That(parsed, Is.True, error);
        Assert.That(manifest!.Actors.Single().Position.ToWorldPoint(),
            Is.EqualTo(new WorldPoint(1f, 0f, 2f)));
        Assert.That(manifest.Topics.Single().ToDomain().Conditions.Single().Operator,
            Is.EqualTo(ConditionOperator.Min));
    }

    [Test]
    public void ActorCannotOpenAnUnansweredKeyword()
    {
        const string json = """
            {
              "version": 1,
              "id": "dialogue.bad",
              "actors": [
                {
                  "id": "actor.guard",
                  "displayName": "Guard",
                  "position": { "x": 0, "y": 0, "z": 0 },
                  "opensWith": [ "missing" ]
                }
              ],
              "topics": []
            }
            """;

        var parsed = DialogueManifest.TryParse(json, out _, out var error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Does.Contain("opens with unknown keyword 'missing'"));
    }
}
