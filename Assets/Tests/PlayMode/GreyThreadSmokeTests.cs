using System.Collections;
using System.Collections.Generic;
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

        var exterior = SceneManager.GetSceneByName("Capital_Exterior");
        if (exterior.IsValid() && exterior.isLoaded)
            yield return SceneManager.UnloadSceneAsync(exterior);

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
        // VS4 mechanics now hang off the route beats, so the gate exercises them too.
        var equipment = player.AddComponent<PlayerEquipment>();
        var skills = player.AddComponent<SkillSystem>();
        var director = systems.AddComponent<GreyThreadDirector>();
        systems.AddComponent<SaveLoadService>();

        var allVisited = new HashSet<string>();
        HashSet<string> routeVisited = null;
        director.BeatVisited += beatId =>
        {
            allVisited.Add(beatId);
            routeVisited?.Add(beatId);
        };

        string[] routes = { "route.warrior", "route.mage", "route.trade", "route.refuse" };
        foreach (var route in routes)
        {
            routeVisited = new HashSet<string>();
            yield return director.RunRoute(route);

            Assert.IsFalse(director.IsRunning, route);
            Assert.IsNull(director.LastError, route);
            Assert.AreEqual(route, story.State.RouteId);
            Assert.AreEqual(route, story.State.Profile.DeclaredInclination);
            Assert.AreEqual("B830", story.State.BeatId);
            Assert.IsTrue(story.HasFlag("flag.chapter_complete"));
            Assert.AreEqual("Council_Arrival", transition.ActiveContentSceneName);
            Assert.AreEqual("spawn.council", transition.ActiveSpawnId);
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(0f, 1.1f, -6f)), 0.01f);
            Assert.IsTrue(story.State.Evidence.Exists(e => e.Id == "ev.black_crystal"));
            Assert.IsTrue(story.State.Evidence.Exists(e => e.Id == "ev.prince_testimony"));
            Assert.IsTrue(story.HasFlag("flag.title_crawl_shown"));
            Assert.IsTrue(story.HasFlag("cinematic.cin.title_crawl.complete"));
            Assert.AreEqual(route == "route.refuse" ? "imprisoned" : "killed", story.State.KingOutcome);
            Assert.AreEqual("role.prince", story.State.RulerId);
            Assert.AreEqual("title.crown_envoy", story.State.GrantedTitle);
            Assert.IsTrue(routeVisited.Contains("B640"), "Every route must pass the title-crawl beat.");

            // VS4 wiring. Route assignment grants two skills; refuse grants none, which is
            // the continuing price of the fastest path.
            var granted = Skills.GrantedBy(route);
            if (route == "route.refuse")
                Assert.IsEmpty(granted, "Refuse should grant no skills.");
            foreach (var skillId in granted)
                Assert.Greater(skills.LevelOf(skillId), 0f,
                    $"{route} did not grant {skillId} at assignment.");

            // Gear taken at the prison must have been handed back on the way out, or the
            // player reaches the confrontation stripped.
            Assert.IsFalse(equipment.GearIsStashed,
                $"{route} finished with the player's gear still confiscated.");
        }

        string[] expectedBeats =
        {
            "B010", "B020", "B030", "B040", "B050", "B060", "B070", "B080", "B090", "B100", "B110", "B120", "B130",
            "B200", "B210", "B220", "B300", "B310", "B400", "B410", "B420", "B500", "B510", "B520",
            "B320", "B600", "B610", "B615", "B620", "B630", "B640", "B700", "B710", "B720", "B730", "B740",
            "B750", "B760", "B800", "B810", "B820", "B830"
        };
        CollectionAssert.AreEquivalent(expectedBeats, allVisited, "The four playable routes must cover the complete 42-beat VS2 contract.");
        Assert.Greater(director.CheckpointCount, 0);
        Assert.IsTrue(SaveLoadService.HasValidSave, "Route checkpoints must produce a valid V4 save.");
    }
}
