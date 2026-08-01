using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class GreyThreadSmokeTests : SmokeTestFixture
{
    private string _harnessSceneName;

    [UnityTearDown]
    public IEnumerator UnloadGreyScenes()
    {
        var cleanup = SceneManager.CreateScene("Vs2Cleanup_" + Time.frameCount);
        SceneManager.SetActiveScene(cleanup);
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            var scene = SceneManager.GetSceneByName(spec.Name);
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

        if (cleanup.IsValid() && cleanup.isLoaded)
            yield return SceneManager.UnloadSceneAsync(cleanup);
    }

    [UnityTest]
    public IEnumerator Vs2Gate_AllFourRoutesReachCaldemarHandoff()
    {
        var player = SpawnPlayer();
        _harnessSceneName = "Vs2Harness_" + Time.frameCount;
        var harness = SceneManager.CreateScene(_harnessSceneName);
        SceneManager.SetActiveScene(harness);
        SceneManager.MoveGameObjectToScene(player, harness);

        Track(new GameObject("GameStateService_Vs2")).AddComponent<GameStateService>().SetState(GameState.Gameplay);
        var transitionRoot = Track(new GameObject("SceneTransitionService_Vs2"));
        var transition = transitionRoot.AddComponent<SceneTransitionService>();
        transition.Configure(0f, player.transform, persistentScene: _harnessSceneName);

        var systems = Track(new GameObject("GameSystems_Vs2"));
        var story = systems.AddComponent<StoryDirector>();
        var director = systems.AddComponent<GreyThreadDirector>();

        string[] routes = { "route.warrior", "route.mage", "route.trade", "route.refuse" };
        foreach (var route in routes)
        {
            yield return director.RunRoute(route);

            Assert.IsFalse(director.IsRunning, route);
            Assert.IsNull(director.LastError, route);
            Assert.AreEqual(route, story.State.RouteId);
            Assert.AreEqual("B830", story.State.BeatId);
            Assert.IsTrue(story.HasFlag("flag.chapter_complete"));
            Assert.AreEqual("Caldemar_Arrival", transition.ActiveContentSceneName);
            Assert.AreEqual("spawn.council", transition.ActiveSpawnId);
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(0f, 1.1f, -6f)), 0.01f);
            Assert.IsTrue(story.State.Evidence.Exists(e => e.Id == "ev.black_crystal"));
        }
    }
}
