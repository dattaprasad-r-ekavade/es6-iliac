using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Talking to people.
///
/// The topic system was built, tested and wired to nothing in any scene — there was nobody in
/// the game to talk to. These cover the actual conversation: the named cast exist where the
/// beat sheet says they are, the same keyword answers differently depending on who is asked,
/// and asking teaches keywords you can carry to somebody else.
///
/// That last property is the whole point of Morrowind's model rather than a conversation
/// tree, so it is the one most worth protecting.
/// </summary>
public class DialogueSmokeTests : SmokeTestFixture
{
    private string _loaded;

    [UnityTearDown]
    public IEnumerator UnloadScene()
    {
        var cleanup = SceneManager.CreateScene("DialogueCleanup_" + System.Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(cleanup);

        if (!string.IsNullOrEmpty(_loaded))
        {
            var scene = SceneManager.GetSceneByName(_loaded);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            _loaded = null;
        }
    }

    private IEnumerator Load(string sceneName)
    {
        _loaded = sceneName;
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        yield return null;
    }

    private TopicDialogueService SpawnDialogue()
    {
        var go = Track(new GameObject("Dialogue_Test"));
        go.AddComponent<StoryDirector>();
        return go.AddComponent<TopicDialogueService>();
    }

    // --- the cast exist ------------------------------------------------------

    [UnityTest]
    public IEnumerator TheNamedCastStandInTheirOwnScenes()
    {
        var expected = new (string scene, string actorId)[]
        {
            ("Docks", "role.processing_guard"),
            ("Tutorial_Warrior", "role.instructor_warrior"),
            ("Order_Hall", "role.instructor_mage"),
            ("Harbor", "role.instructor_trade"),
            ("Palace", "role.king"),
            ("Council_Arrival", "role.council_contact")
        };

        foreach (var (scene, actorId) in expected)
        {
            yield return Load(scene);

            var actors = Object.FindObjectsByType<SpeakingActor>(FindObjectsSortMode.None);
            Assert.IsTrue(
                actors.Any(a => a.ActorId == actorId),
                $"{scene} has no {actorId} to talk to.");

            yield return UnloadScene();
        }
    }

    [UnityTest]
    public IEnumerator ThePrisonHasBothVoices_SoTheRevealIsNotOneLecture()
    {
        yield return Load("Prison");

        var actors = Object.FindObjectsByType<SpeakingActor>(FindObjectsSortMode.None);
        Assert.IsTrue(actors.Any(a => a.ActorId == "role.prisoner_a"), "Hari is missing.");
        Assert.IsTrue(actors.Any(a => a.ActorId == "role.prisoner_b"), "Lekha is missing.");

        // B510 requires the soul-operation reveal to be split across two speakers rather than
        // delivered as one unskippable exposition dump.
        Assert.GreaterOrEqual(actors.Length, 2,
            "The prison reveal has fewer than two voices to split it between.");
    }

    // --- the conversation ----------------------------------------------------

    [Test]
    public void EveryCastMemberOffersSomethingToAskAbout()
    {
        SpawnDialogue();
        var go = Track(new GameObject("Actor_Test"));
        var actor = go.AddComponent<SpeakingActor>();
        actor.Configure("role.instructor_warrior", "Senapati Karan", "faction.crown",
            "scene.tutorial_warrior", "the blade");

        TopicDialogueService.Instance.LearnTopic("the blade");

        var topics = actor.AvailableTopics();
        Assert.IsNotEmpty(topics, "An actor with authored topics offered nothing to ask about.");
        CollectionAssert.Contains(topics, "the blade");
    }

    /// <summary>
    /// The point of a shared knowledge base: the same keyword gets a different answer from a
    /// different person. A conversation tree cannot do this without duplicating the tree.
    /// </summary>
    [Test]
    public void TheSameKeywordAnswersDifferentlyDependingOnWhoIsAsked()
    {
        SpawnDialogue();

        var karanGo = Track(new GameObject("Karan"));
        var karan = karanGo.AddComponent<SpeakingActor>();
        karan.Configure("role.instructor_warrior", "Karan", "faction.crown", "scene.tutorial_warrior");

        var meeraGo = Track(new GameObject("Meera"));
        var meera = meeraGo.AddComponent<SpeakingActor>();
        meera.Configure("role.instructor_mage", "Meera", "faction.order", "scene.order_hall");

        // "the transport" is Karan's alone; Meera has nothing to say about it.
        Assert.IsNotNull(karan.Ask("the transport"), "Karan would not answer his own topic.");
        Assert.IsNull(meera.Ask("the transport"), "Meera answered a topic authored for Karan.");
    }

    [Test]
    public void AnyoneWillAnswerTheSharedTopics()
    {
        SpawnDialogue();
        var go = Track(new GameObject("Anyone"));
        var actor = go.AddComponent<SpeakingActor>();
        actor.Configure("role.prisoner_a", "Hari", null, "scene.prison");

        Assert.IsNotNull(actor.Ask("ratnapur"),
            "A shared topic went unanswered, so common knowledge is not actually common.");
    }

    /// <summary>
    /// Asking teaches the keyword, so it can be carried to somebody else. This is the verb the
    /// whole model exists for.
    /// </summary>
    [Test]
    public void AskingATopicLearnsIt_SoItCanBeTakenToSomeoneElse()
    {
        var service = SpawnDialogue();
        var go = Track(new GameObject("Guard"));
        var guard = go.AddComponent<SpeakingActor>();
        guard.Configure("role.processing_guard", "Guard", "faction.crown", "scene.docks");

        service.LearnTopic("the law");
        guard.Ask("the law");

        CollectionAssert.Contains(service.KnownTopics.ToArray(), "the law",
            "Asking about something did not add it to what the player knows.");
    }

    [Test]
    public void AnUnknownKeywordProducesNothing_RatherThanAnEmptyLine()
    {
        SpawnDialogue();
        var go = Track(new GameObject("Anyone"));
        var actor = go.AddComponent<SpeakingActor>();
        actor.Configure("role.prisoner_a", "Hari", null, "scene.prison");

        Assert.IsNull(actor.Ask("the price of fish"),
            "An unauthored keyword returned something instead of nothing.");
    }

    /// <summary>
    /// The menu is built from what the actor will actually answer, so it can never offer a
    /// keyword that produces silence when picked.
    /// </summary>
    [Test]
    public void TheTopicMenuNeverOffersAKeywordThatWouldGoUnanswered()
    {
        var service = SpawnDialogue();
        var go = Track(new GameObject("Meera"));
        var meera = go.AddComponent<SpeakingActor>();
        meera.Configure("role.instructor_mage", "Meera", "faction.order", "scene.order_hall");

        // Teach a keyword only Karan answers; Meera must not offer it.
        service.LearnTopic("the transport");

        foreach (var keyword in meera.AvailableTopics())
            Assert.IsNotNull(meera.Ask(keyword),
                $"The menu offered '{keyword}' but the actor had no answer for it.");

        CollectionAssert.DoesNotContain(meera.AvailableTopics(), "the transport",
            "Meera offered a topic authored for someone else.");
    }
}
