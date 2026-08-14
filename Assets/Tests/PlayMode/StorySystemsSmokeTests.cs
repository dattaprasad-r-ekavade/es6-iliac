using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StorySystemsSmokeTests : SmokeTestFixture
{
    [Test]
    public void StoryDirector_EnforcesRouteFallbackAndKeepsReadableEvidence()
    {
        var root = Track(new GameObject("StorySystems_Test"));
        var story = root.AddComponent<StoryDirector>();
        story.SetProfile(new CharacterProfile { Name = "Aren", AncestryId = "anc.southern" });
        story.SelectRoute("not-a-route");
        story.AddEvidence(new EvidenceRecord
        {
            Id = "ev.crystal_manifest", Title = "Crystal Manifest",
            DocumentBody = "Lot seven was amended: the source is a named prisoner, not a mine.", Inspected = true
        });

        Assert.AreEqual("route.refuse", story.State.RouteId);
        Assert.IsTrue(story.HasFlag("flag.profile_valid", "true"));
        Assert.AreEqual(1, story.State.Evidence.Count);
        StringAssert.Contains("named prisoner", story.State.Evidence[0].DocumentBody);
    }

    [Test]
    public void TopicDialogue_SelectsMostSpecificResponseIncludingEvidenceCount()
    {
        var root = Track(new GameObject("DialogueSystems_Test"));
        var story = root.AddComponent<StoryDirector>();
        var dialogue = root.AddComponent<TopicDialogueService>();
        for (int i = 0; i < 3; i++)
            story.AddEvidence(new EvidenceRecord { Id = $"ev.test_{i}", Title = "Proof", DocumentBody = "Full text" });

        dialogue.LearnTopic("black jiva");
        var response = dialogue.Respond("black jiva", new DialogueContext { Disposition = 50 });
        StringAssert.Contains("living prisoners", response);
        Assert.IsTrue(story.State.DialogueChoices.Exists(c => c.Id == "topic.topic_black_jiva"));
    }

    [UnityTest]
    public IEnumerator CinematicRunner_SkipAppliesEveryCueAndTheSameEndStateOnce()
    {
        var root = Track(new GameObject("CinematicSystems_Test"));
        var story = root.AddComponent<StoryDirector>();
        var runner = root.AddComponent<CinematicRunner>();
        var sequence = ScriptableObject.CreateInstance<CinematicSequence>();
        sequence.Configure("cin.title_crawl", 20f,
            new[]
            {
                new CinematicCue { AtSeconds = 3f, Action = "set_flag", Key = "flag.prince_located", Value = "true" },
                new CinematicCue { AtSeconds = 8f, Action = "advance_beat", Key = "stage.escape", Value = "B640" },
                new CinematicCue { AtSeconds = 12f, Action = "open_lock", Key = "lock.escape_gate" }
            },
            new[] { new StoryFlag { Id = "flag.title_crawl_shown", Value = "true" } });

        var routine = runner.Play(sequence);
        Assert.IsTrue(routine.MoveNext());
        runner.RequestSkip();
        while (routine.MoveNext()) yield return routine.Current;

        Assert.IsTrue(story.HasFlag("flag.prince_located", "true"));
        Assert.IsTrue(story.HasFlag("flag.title_crawl_shown", "true"));
        Assert.AreEqual("B640", story.State.BeatId);
        Assert.Contains("lock.escape_gate", story.State.OpenedLocks);
        Assert.Contains("cin.title_crawl", story.State.SkippedCinematics);

        // Replay is idempotent: no duplicate mutations and no second run.
        yield return runner.Play(sequence);
        Assert.AreEqual(1, story.State.OpenedLocks.Count);
        Object.DestroyImmediate(sequence);
    }
}
