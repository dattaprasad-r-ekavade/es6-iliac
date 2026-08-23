namespace RatnaBay.Domain.Tests;

public sealed class QuestManifestTests
{
    [Test]
    public void ValidManifestCreatesAnEngineFreeDefinition()
    {
        const string json = """
            {
              "version": 1,
              "id": "quests.test",
              "quests": [
                {
                  "id": "quest.bandits",
                  "title": "Clear the road",
                  "initialStageText": "Slay three bandits",
                  "objectiveAnchorId": "anchor.road",
                  "objectivePosition": { "x": 0, "y": 0, "z": -35 },
                  "targetCount": 3,
                  "targetEnemy": "Bandit",
                  "goldReward": 80
                }
              ]
            }
            """;

        var parsed = QuestManifest.TryParse(json, out var manifest, out var error);

        Assert.That(parsed, Is.True, error);
        var definition = manifest!.ToDefinitions().Single();
        Assert.Multiple(() =>
        {
            Assert.That(definition.IsKillQuest, Is.True);
            Assert.That(definition.ObjectivePosition, Is.EqualTo(new WorldPoint(0f, 0f, -35f)));
            Assert.That(definition.GoldReward, Is.EqualTo(80));
        });
    }

    [Test]
    public void KillQuestWithoutAnEnemyIsRejected()
    {
        const string json = """
            {
              "version": 1,
              "id": "quests.bad",
              "quests": [
                {
                  "id": "quest.bad",
                  "title": "Bad quest",
                  "initialStageText": "Do something",
                  "targetCount": 3
                }
              ]
            }
            """;

        var parsed = QuestManifest.TryParse(json, out _, out var error);

        Assert.That(parsed, Is.False);
        Assert.That(error, Does.Contain("needs targetEnemy"));
    }
}
