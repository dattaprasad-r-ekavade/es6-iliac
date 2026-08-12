using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The playable path, driven through the real boot flow.
///
/// Everything else in this suite proves systems work. This proves the game can be *played*:
/// boot, start a new game, and end up standing in the region with an objective telling you
/// where to walk — rather than a coroutine advancing beats on the player's behalf.
///
/// It is the closest automated substitute for sitting down at the packaged build, which no
/// test can do because it requires clicking START.
/// </summary>
public class PlayableChapterSmokeTests : SmokeTestFixture
{
    private const float BootTimeout = 60f;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = SceneManager.CreateScene("PlayableCleanup_" + System.Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(cleanup);

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene != cleanup)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        RegionReturn.Clear();
        PlayerRef.Clear();
        WorldState.Reset();
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Boot and wait for the title to come up. Bootstrap loads Main asynchronously, so the
    /// flow controller does not exist on the frame the scene load returns.
    /// </summary>
    private static IEnumerator BootToTitle()
    {
        yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

        float deadline = Time.realtimeSinceStartup + BootTimeout;
        while ((GameFlowController.Instance == null
                || SceneTransitionService.Instance == null
                || SceneTransitionService.Instance.IsTransitioning)
               && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    /// <summary>
    /// New Game must land the player in the walkable region, holding an objective that names
    /// somewhere real — not in a cutscene, and not mid-transition.
    /// </summary>
    [UnityTest]
    public IEnumerator NewGame_LeavesThePlayerStandingInTheRegionWithSomethingToDo()
    {
        yield return BootToTitle();

        var flow = GameFlowController.Instance;
        Assert.IsNotNull(flow, "Bootstrap did not bring up the title flow.");

        flow.OnClickStart();
        flow.RequestSkip();

        // The director runs the prologue automatically, then hands control back in the region.
        float deadline = Time.realtimeSinceStartup + BootTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            var transition = SceneTransitionService.Instance;
            var objectives = ObjectiveService.Instance;

            bool inRegion = transition != null
                            && transition.ActiveContentSceneName == GreyThreadDirector.RegionScene
                            && !transition.IsTransitioning;

            if (inRegion && objectives != null && objectives.HasObjective) break;
            yield return null;
        }

        var finalTransition = SceneTransitionService.Instance;
        var finalObjectives = ObjectiveService.Instance;

        Assert.IsNotNull(finalTransition, "No scene transition service came online.");
        Assert.AreEqual(
            GreyThreadDirector.RegionScene, finalTransition.ActiveContentSceneName,
            $"New Game did not reach the walkable region within {BootTimeout}s.");

        Assert.IsNotNull(finalObjectives, "No objective service came online.");
        Assert.IsTrue(finalObjectives.HasObjective,
            "The player is standing in the region with nothing telling them where to go.");
        Assert.IsNotEmpty(finalObjectives.TargetAnchorId,
            "The objective names no place, so no directions can be generated for it.");

        Assert.IsNotNull(PlayerRef.Transform, "There is no player to walk anywhere.");
        Assert.IsTrue(
            CapitalRegion.IsOverLand(PlayerRef.Transform.position),
            $"The player is standing in open water at {PlayerRef.Transform.position}.");
    }

    /// <summary>
    /// The objective must produce usable directions from where the player actually stands.
    /// An objective with no bearing is a quest marker that forgot to render.
    /// </summary>
    [UnityTest]
    public IEnumerator TheOpeningObjectiveGivesWorkableDirections()
    {
        yield return BootToTitle();
        var flow = GameFlowController.Instance;
        Assert.IsNotNull(flow, "Bootstrap did not bring up the title flow.");
        flow.OnClickStart();
        flow.RequestSkip();

        float deadline = Time.realtimeSinceStartup + BootTimeout;
        while ((ObjectiveService.Instance == null || !ObjectiveService.Instance.HasObjective)
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        var objectives = ObjectiveService.Instance;
        Assert.IsNotNull(objectives);
        Assert.IsTrue(objectives.HasObjective, "No opening objective was ever set.");

        Assert.IsNotEmpty(objectives.Title, "The objective has no title to show.");
        Assert.IsNotEmpty(objectives.Directions, "The objective has no written directions.");

        string bearing = objectives.BearingLine();
        Assert.IsNotEmpty(bearing, "The objective produced no bearing from the player's position.");
    }
}
