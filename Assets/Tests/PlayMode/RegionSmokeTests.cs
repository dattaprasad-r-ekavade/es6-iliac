using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The generated Estmere region, loaded as a real scene.
///
/// The data contract is covered by <c>CapitalRegionTests</c> in EditMode; this is about
/// whether the geometry that comes out of the generator is actually standable and whether
/// every door leads somewhere. Both are things a data test cannot see.
/// </summary>
public class RegionSmokeTests : SmokeTestFixture
{
    private const string RegionScene = "Capital_Region";
    private string _harnessSceneName;

    [UnityTearDown]
    public IEnumerator UnloadRegion()
    {
        var cleanup = SceneManager.CreateScene("RegionCleanup_" + System.Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(cleanup);

        var region = SceneManager.GetSceneByName(RegionScene);
        if (region.IsValid() && region.isLoaded)
            yield return SceneManager.UnloadSceneAsync(region);

        var palace = SceneManager.GetSceneByName("Palace");
        if (palace.IsValid() && palace.isLoaded)
            yield return SceneManager.UnloadSceneAsync(palace);

        if (!string.IsNullOrEmpty(_harnessSceneName))
        {
            var harness = SceneManager.GetSceneByName(_harnessSceneName);
            if (harness.IsValid() && harness.isLoaded)
                yield return SceneManager.UnloadSceneAsync(harness);
            _harnessSceneName = null;
        }

        RegionReturn.Clear();
    }

    private static IEnumerator LoadRegion()
    {
        yield return SceneManager.LoadSceneAsync(RegionScene, LoadSceneMode.Single);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TheRegionLoadsAndHasGroundUnderTheSpawn()
    {
        yield return LoadRegion();

        // Physics needs a frame to register the freshly loaded colliders.
        yield return new WaitForFixedUpdate();

        var above = CapitalRegion.PlayerSpawn + Vector3.up * 50f;
        bool grounded = Physics.Raycast(above, Vector3.down, out var hit, 200f,
            1 << GameLayers.Ground, QueryTriggerInteraction.Ignore);

        Assert.IsTrue(grounded, "Nothing solid under the player spawn — the player would fall forever.");
        Assert.Greater(hit.point.y, CapitalRegion.WaterLevel,
            "The spawn is below the waterline.");
    }

    [UnityTest]
    public IEnumerator EveryAnchorHasAPortalWiredToItsScene()
    {
        yield return LoadRegion();

        var portals = Object.FindObjectsByType<RegionPortal>(FindObjectsSortMode.None);

        // Every anchor needs a door; the region is allowed *more* doors than anchors. The
        // authored market street puts entrances on two of its facades, which is content, not a
        // fault — this asserted an exact count and so treated adding a door as a regression.
        Assert.GreaterOrEqual(
            portals.Length, CapitalRegion.Anchors.Length,
            "The generated region has fewer doors than it has anchors.");

        foreach (var anchor in CapitalRegion.Anchors)
        {
            var portal = portals.FirstOrDefault(p => p.AnchorId == anchor.Id);
            Assert.IsNotNull(portal, $"No portal was generated for {anchor.Id}.");
            Assert.AreEqual(anchor.SceneName, portal.SceneName,
                $"{anchor.Id}'s door leads to the wrong scene.");
            Assert.AreEqual(anchor.SpawnId, portal.SpawnId,
                $"{anchor.Id}'s door names the wrong spawn.");
        }
    }

    [UnityTest]
    public IEnumerator PortalsAreTriggers_SoTheyDoNotBlockWalking()
    {
        yield return LoadRegion();

        foreach (var portal in Object.FindObjectsByType<RegionPortal>(FindObjectsSortMode.None))
        {
            var collider = portal.GetComponent<Collider>();
            Assert.IsNotNull(collider, $"{portal.AnchorId} has no trigger volume.");
            Assert.IsTrue(collider.isTrigger,
                $"{portal.AnchorId}'s portal is solid — the player would bounce off the doorway.");
        }
    }

    [UnityTest]
    public IEnumerator TheCityWallHasWalkableGates()
    {
        yield return LoadRegion();
        yield return new WaitForFixedUpdate();

        foreach (var gate in CapitalRegion.Gates)
        {
            var world = CapitalRegion.CityCenter + new Vector3(gate.x, 0f, gate.z);
            bool alongX = Mathf.Abs(gate.x) > Mathf.Abs(gate.z);
            var through = alongX ? Vector3.right : Vector3.forward;

            var from = world - through * 30f + Vector3.up * 2f;
            bool blocked = Physics.Raycast(from, through, 60f,
                1 << GameLayers.Structure, QueryTriggerInteraction.Ignore);

            Assert.IsFalse(blocked, $"The gate at {world} is walled shut.");
        }
    }

    /// <summary>
    /// Leaving an interior must put the player back at the door they used, not at the region's
    /// default spawn — otherwise every building exit teleports them across the city.
    /// </summary>
    [Test]
    public void LeavingAnInteriorReturnsToTheDoorUsed()
    {
        RegionReturn.Remember("anchor.palace");
        var palace = CapitalRegion.FindAnchor("anchor.palace").Value;

        var back = RegionReturn.ReturnPosition();

        Assert.Less(
            Vector3.Distance(back, palace.Position), palace.Footprint,
            "Exiting the palace did not return the player to the palace door.");
        Assert.Greater(
            Vector3.Distance(back, palace.Position), palace.Footprint * 0.4f,
            "The return point is inside the building's own footprint.");
    }

    [Test]
    public void WithNoRecordedDoor_ReturnFallsBackToTheRegionSpawn()
    {
        RegionReturn.Clear();

        Assert.AreEqual(
            CapitalRegion.PlayerSpawn, RegionReturn.ReturnPosition(),
            "A save loaded straight into an interior would have nowhere to exit to.");
    }

    [Test]
    public void ReturnPlacementMovesThePlayerOutsideTheRememberedDoor()
    {
        RegionReturn.Remember("anchor.prison");
        var player = Track(new GameObject("ReturnPlayer"));

        RegionReturn.PlacePlayerAtReturn(player.transform);

        Assert.AreEqual(RegionReturn.ReturnPosition(), player.transform.position);
    }

    [Test]
    public void CapitalRegionFailuresRespawnInsideTheRegion()
    {
        var respawn = PlayerSafetyGuard.RespawnPositionForScene("Capital_Region");
        Assert.AreEqual(CapitalRegion.PlayerSpawn, respawn);
        Assert.IsTrue(CapitalRegion.IsOverLand(respawn));
    }

    [UnityTest]
    public IEnumerator InteriorExitTransitionPlacesPlayerAtTheRememberedDoor()
    {
        _harnessSceneName = "RegionHarness_" + Time.frameCount;
        var harness = SceneManager.CreateScene(_harnessSceneName);
        SceneManager.SetActiveScene(harness);
        var player = SpawnPlayer();
        SceneManager.MoveGameObjectToScene(player, harness);
        Track(new GameObject("GameState_Region"))
            .AddComponent<GameStateService>().SetState(GameState.Gameplay);
        var service = Track(new GameObject("Transition_Region")).AddComponent<SceneTransitionService>();
        service.Configure(0f, player.transform, persistentScene: _harnessSceneName);

        yield return service.TransitionTo("Palace", "spawn.entry");
        Assert.AreEqual("Palace", service.ActiveContentSceneName);
        RegionReturn.Remember("anchor.palace");
        var exit = Track(new GameObject("InteriorExit_Test")).AddComponent<InteriorExit>();
        Assert.IsTrue(exit.Leave());

        float deadline = Time.realtimeSinceStartup + 10f;
        while ((service.ActiveContentSceneName != RegionScene || service.IsTransitioning)
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.AreEqual(RegionScene, service.ActiveContentSceneName);
        Assert.Less(Vector3.Distance(player.transform.position, RegionReturn.ReturnPosition()), 0.01f,
            "Leaving the palace returned the player to the docks instead of its doorway.");
    }
}
