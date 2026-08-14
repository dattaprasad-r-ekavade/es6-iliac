using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Can the game be played at all.
///
/// Every other suite in this project asserts against *data* — that a site is inside a coast,
/// that a scene id has no place name in it, that a palette stays saturated. All of it passed
/// green while the game was unplayable: entering a building drowned you, the spawn stood in a
/// four-metre trench, the only authored street was 250 m away with nothing in between, and the
/// pause menu could not quit. Every one of those was found by a human pressing Play.
///
/// The difference is where the assertion is made from. These measure **from where the player
/// stands**, which is the viewpoint the data can never see from. If a test here is hard to
/// write, that is usually the finding.
/// </summary>
public class PlayabilitySmokeTests : SmokeTestFixture
{
    private string _loaded;

    /// <summary>Long enough for gravity, a failed ground check or a rescue teleport to fire.</summary>
    private const float SettleSeconds = 2.5f;

    /// <summary>
    /// A teleport out of the level moves the player hundreds of metres. Ordinary settling onto
    /// a floor moves centimetres. Anything past this is the safety guard evicting the player.
    /// </summary>
    private const float EvictionDistance = 25f;

    [UnityTearDown]
    public IEnumerator UnloadScene()
    {
        var cleanup = SceneManager.CreateScene("PlayabilityCleanup_" + System.Guid.NewGuid().ToString("N"));
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

    /// <summary>Drops a real player capsule at a spawn point and lets physics run.</summary>
    private IEnumerator StandPlayerAt(Vector3 position, System.Action<GameObject, Vector3> onSettled)
    {
        var player = SpawnPlayer();
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;

        float elapsed = 0f;
        while (elapsed < SettleSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        onSettled(player, position);
    }

    // --- the one that drowned you --------------------------------------------

    /// <summary>
    /// Stand in every interior and still be there a few seconds later.
    ///
    /// This is the drowning bug as a test. PlayerSafetyGuard measured "has the player drowned"
    /// against the *bay's* water level and "is there ground here" against the world generator's
    /// terrain — neither of which an authored interior has. Its floor sits at y≈0, below the
    /// drown threshold, so walking into the docks read as drowning and threw the player out to
    /// the overworld spawn.
    /// </summary>
    [UnityTest]
    public IEnumerator TheFloorOfEveryInteriorHoldsThePlayer()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            yield return Load(spec.Name);

            var context = SceneContext.FindInScene(SceneManager.GetSceneByName(spec.Name));
            Assert.IsNotNull(context, $"{spec.Name} has no SceneContext.");
            Assert.IsTrue(context.TryGetDefaultSpawn(out var spawn) && spawn != null,
                $"{spec.Name} has no arrival spawn.");

            var start = spawn.transform.position;
            GameObject settledPlayer = null;
            yield return StandPlayerAt(start, (player, _) => settledPlayer = player);

            var end = settledPlayer.transform.position;

            Assert.Less(Vector3.Distance(start, end), EvictionDistance,
                $"Standing in {spec.Name} moved the player {Vector3.Distance(start, end):0} m. "
                + "Something threw them out of the interior.");
            Assert.Greater(end.y, start.y - 3f,
                $"The player fell through {spec.Name}'s floor.");

            yield return UnloadScene();
        }
    }

    // --- the one where there was nothing to do -------------------------------

    /// <summary>
    /// Something to talk to, something to take, and somewhere to go — all close enough to find.
    ///
    /// The region contained all three and the player spawned 250 m from the nearest, facing the
    /// other way, with generated blocks in between. Nothing in the data was wrong; the distance
    /// was the bug.
    /// </summary>
    [UnityTest]
    public IEnumerator TheOpeningHasSomeoneToTalkToSomethingToTakeAndADoor()
    {
        yield return Load(GreyThreadDirector.RegionScene);

        var spawn = CapitalRegion.PlayerSpawn;

        AssertReachable<SpeakingActor>(spawn, "someone to talk to", 80f);
        AssertReachable<WorldPickup>(spawn, "something to pick up", 120f);
        AssertReachable<RegionPortal>(spawn, "a door to go through", 150f);
    }

    private static void AssertReachable<T>(Vector3 from, string what, float limit) where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        Assert.IsNotEmpty(all, $"The opening region contains no {what} at all.");

        float nearest = all.Min(item => Vector3.Distance(from, item.transform.position));
        Assert.Less(nearest, limit,
            $"The nearest {what} is {nearest:0} m from the spawn. A player who has to walk "
            + "that far past nothing concludes the world is empty, and stops.");
    }

    // --- the one where you could not get out ---------------------------------

    /// <summary>
    /// Every interior the player may wander into on their own has a way back out.
    ///
    /// The one-way story spaces are exempt by design — the prologue ship and everything after
    /// the cave are sequences the director carries the player through.
    /// </summary>
    [UnityTest]
    public IEnumerator EveryInteriorYouCanWanderIntoHasAWayOut()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            if (!spec.HasExitDoor) continue;

            yield return Load(spec.Name);
            Assert.IsNotEmpty(
                Object.FindObjectsByType<InteriorExit>(FindObjectsSortMode.None),
                $"{spec.Name} has no exit, so a player who walks in is stuck there.");
            yield return UnloadScene();
        }
    }

    // --- the one that needed Alt+F4 ------------------------------------------

    /// <summary>
    /// The player can always stop playing.
    ///
    /// GameHud read the pause key *below* a state gate that returned early on Loading,
    /// Cinematic and Death — and quitting lives inside the pause menu, so any state that stuck
    /// left no way out of the program at all.
    /// </summary>
    [Test]
    public void PauseIsReachableFromEveryGameState()
    {
        foreach (GameState state in System.Enum.GetValues(typeof(GameState)))
        {
            Assert.IsTrue(GameHud.PauseReachableFrom(state),
                $"Pause cannot be opened from {state}, so a game that stops there cannot be quit.");
        }
    }
}
