using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Proves the VS1 scene boundary with real build-settings scenes. These tests exercise
/// additive A -> B -> C travel as well as the transactional recovery paths that keep
/// the player in the last valid scene when content is missing or malformed.
/// </summary>
public sealed class SceneTransitionSmokeTests : SmokeTestFixture
{
    private string _harnessSceneName;

    private static readonly string[] TestScenes =
    {
        "TransitionTest_A",
        "TransitionTest_B",
        "TransitionTest_C",
        "TransitionTest_Invalid"
    };

    [UnityTearDown]
    public IEnumerator UnloadTransitionScenes()
    {
        var cleanup = SceneManager.CreateScene("TransitionCleanup_" + Time.frameCount);
        SceneManager.SetActiveScene(cleanup);

        foreach (var sceneName in TestScenes)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        if (!string.IsNullOrEmpty(_harnessSceneName))
        {
            var harness = SceneManager.GetSceneByName(_harnessSceneName);
            if (harness.IsValid() && harness.isLoaded)
                yield return SceneManager.UnloadSceneAsync(harness);
            _harnessSceneName = null;
        }
    }

    [UnityTest]
    public IEnumerator ThreeSceneTravel_UsesStableSpawnsAndUnloadsPreviousContent()
    {
        var player = SpawnPlayer();
        var service = SpawnTransitionService(player.transform);

        yield return service.TransitionTo("TransitionTest_A", "spawn.entry");
        AssertTransition(service, "TransitionTest_A", new Vector3(10f, 1f, 0f));

        yield return service.TransitionTo("TransitionTest_B", "spawn.entry");
        AssertTransition(service, "TransitionTest_B", new Vector3(20f, 2f, 0f));
        Assert.IsFalse(SceneManager.GetSceneByName("TransitionTest_A").isLoaded,
            "A remained loaded after committing the transition to B.");

        yield return service.TransitionTo("TransitionTest_C", "spawn.entry");
        AssertTransition(service, "TransitionTest_C", new Vector3(30f, 3f, 0f));
        Assert.IsFalse(SceneManager.GetSceneByName("TransitionTest_B").isLoaded,
            "B remained loaded after committing the transition to C.");
    }

    [UnityTest]
    public IEnumerator MissingScene_LeavesPreviousSceneAndPlayerIntact()
    {
        var player = SpawnPlayer();
        var service = SpawnTransitionService(player.transform);
        yield return service.TransitionTo("TransitionTest_A", "spawn.entry");
        var positionBeforeFailure = player.transform.position;

        LogAssert.Expect(LogType.Warning,
            "[SceneTransition] Scene 'TransitionTest_DoesNotExist' is not available in build settings.");
        yield return service.TransitionTo("TransitionTest_DoesNotExist", "spawn.entry");

        Assert.AreEqual("TransitionTest_A", service.ActiveContentSceneName);
        Assert.AreEqual("TransitionTest_A", SceneManager.GetActiveScene().name);
        Assert.AreEqual(positionBeforeFailure, player.transform.position);
        Assert.IsNotEmpty(service.LastError);
        Assert.IsFalse(service.IsTransitioning, "A failed load left the service permanently locked.");
    }

    [UnityTest]
    public IEnumerator SceneWithoutContext_IsRolledBackAndUnloaded()
    {
        var player = SpawnPlayer();
        var service = SpawnTransitionService(player.transform);
        yield return service.TransitionTo("TransitionTest_A", "spawn.entry");

        LogAssert.Expect(LogType.Warning,
            "[SceneTransition] Scene 'TransitionTest_Invalid' has no SceneContext.");
        yield return service.TransitionTo("TransitionTest_Invalid", "spawn.entry");

        Assert.AreEqual("TransitionTest_A", service.ActiveContentSceneName);
        Assert.AreEqual("TransitionTest_A", SceneManager.GetActiveScene().name);
        Assert.IsFalse(SceneManager.GetSceneByName("TransitionTest_Invalid").isLoaded,
            "Malformed destination remained loaded after rollback.");
        Assert.IsFalse(service.IsTransitioning, "Rollback left the service permanently locked.");
    }

    [UnityTest]
    public IEnumerator Vs1Gate_ThreeScenesBranchEvidenceSaveContinueCompanionAndRollback()
    {
        var player = SpawnPlayer();
        var transition = SpawnTransitionService(player.transform);
        var systems = Track(new GameObject("VS1_StorySystems"));
        var story = systems.AddComponent<StoryDirector>();
        var save = systems.AddComponent<SaveLoadService>();

        yield return transition.TransitionTo("TransitionTest_A", "spawn.entry");
        story.SetProfile(new CharacterProfile { Name = "Gate Runner", AncestryId = "anc.isleborn" });
        story.SelectRoute("route.mage");
        story.RecordChoice("choice.audience_assignment", "accept_arcanum");

        yield return transition.TransitionTo("TransitionTest_B", "spawn.entry");
        story.AddEvidence(new EvidenceRecord
        {
            Id = "ev.crystal_manifest", Title = "Crystal Manifest",
            DocumentBody = "The source column names prisoners transferred under royal seal.", Inspected = true
        });
        story.MarkOpened("lock.arcanum_archive");

        yield return transition.TransitionTo("TransitionTest_C", "spawn.entry");
        story.AdvanceTo("chapter.01", "stage.convergence", "B630");
        story.SetCompanion("role.prince", true, "TransitionTest_C", "spawn.entry", 64f);
        story.MarkLooted("loot.black_crystal");
        WorldState.MarkKilled("gate.enemy.before_save");
        save.Save();

        // Mutations after the checkpoint must be discarded by Continue.
        story.SelectRoute("route.refuse");
        story.SetCompanion("role.prince", false, "bad.scene", "bad.spawn", 1f);
        story.MarkOpened("lock.after_save");
        WorldState.MarkKilled("gate.enemy.after_save");
        yield return transition.TransitionTo("TransitionTest_A", "spawn.entry");
        Object.DestroyImmediate(systems); // quit: all runtime singletons disappear

        var continuedSystems = Track(new GameObject("VS1_ContinuedSystems"));
        SceneManager.MoveGameObjectToScene(
            continuedSystems, SceneManager.GetSceneByName(_harnessSceneName));
        var continuedStory = continuedSystems.AddComponent<StoryDirector>();
        var continuedSave = continuedSystems.AddComponent<SaveLoadService>();
        continuedSave.Load();
        float deadline = Time.realtimeSinceStartup + 5f;
        while ((transition.ActiveContentSceneName != "TransitionTest_C" || transition.IsTransitioning)
               && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;

        Assert.AreEqual("route.mage", continuedStory.State.RouteId);
        Assert.AreEqual("B630", continuedStory.State.BeatId);
        Assert.IsTrue(continuedStory.State.Companion.Following);
        Assert.AreEqual("role.prince", continuedStory.State.Companion.ActorId);
        Assert.IsTrue(continuedStory.State.Evidence.Exists(e => e.Id == "ev.crystal_manifest" && e.Inspected));
        Assert.Contains("lock.arcanum_archive", continuedStory.State.OpenedLocks);
        Assert.False(continuedStory.State.OpenedLocks.Contains("lock.after_save"));
        Assert.Contains("loot.black_crystal", continuedStory.State.LootedObjects);
        Assert.IsTrue(WorldState.IsKilled("gate.enemy.before_save"));
        Assert.IsFalse(WorldState.IsKilled("gate.enemy.after_save"));
        Assert.AreEqual("TransitionTest_C", transition.ActiveContentSceneName);
        Assert.AreEqual("spawn.entry", transition.ActiveSpawnId);
        Assert.Less(Vector3.Distance(player.transform.position, new Vector3(30f, 3f, 0f)), 0.001f);
    }

    private SceneTransitionService SpawnTransitionService(Transform player)
    {
        Assert.IsNull(SceneTransitionService.Instance,
            "A transition service leaked from an earlier test or scene.");
        _harnessSceneName = "TransitionHarness_" + Time.frameCount;
        var harness = SceneManager.CreateScene(_harnessSceneName);
        SceneManager.SetActiveScene(harness);
        SceneManager.MoveGameObjectToScene(player.gameObject, harness);
        Track(new GameObject("GameStateService_TransitionTest"))
            .AddComponent<GameStateService>()
            .SetState(GameState.Gameplay);

        var root = Track(new GameObject("SceneTransitionService_Test"));
        var service = root.AddComponent<SceneTransitionService>();
        service.Configure(0f, player, persistentScene: _harnessSceneName);
        return service;
    }

    private static void AssertTransition(
        SceneTransitionService service, string expectedScene, Vector3 expectedPosition)
    {
        Assert.IsNull(service.LastError);
        Assert.IsFalse(service.IsTransitioning);
        Assert.AreEqual(GameState.Gameplay, GameStateService.Instance.CurrentState,
            "The loading state was not released after the transition.");
        Assert.AreEqual(expectedScene, service.ActiveContentSceneName);
        Assert.AreEqual(expectedScene, SceneManager.GetActiveScene().name);
        Assert.Less(Vector3.Distance(PlayerRef.Transform.position, expectedPosition), 0.001f,
            $"Player did not arrive at the stable spawn in {expectedScene}.");
    }
}
