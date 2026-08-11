using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Interiors as places rather than boxes.
///
/// Three things this protects, all of which were real gaps:
/// the interiors were one room each; there was no way out except the story advancing, so a
/// human who wandered was stuck; and the VS4 mechanics were systems nothing in the game
/// touched.
/// </summary>
public class InteriorSmokeTests : SmokeTestFixture
{
    private string _loaded;

    [UnityTearDown]
    public IEnumerator UnloadInterior()
    {
        var cleanup = SceneManager.CreateScene("InteriorCleanup_" + System.Guid.NewGuid().ToString("N"));
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

    [UnityTest]
    public IEnumerator EveryInteriorThePlayerCanWanderHasAWayOut()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes.Where(s => s.HasExitDoor))
        {
            yield return Load(spec.Name);

            var exit = Object.FindFirstObjectByType<InteriorExit>();
            Assert.IsNotNull(exit,
                $"{spec.Name} has no exit. A player who wanders off the critical path is stuck there.");

            var collider = exit.GetComponent<Collider>();
            Assert.IsNotNull(collider, $"{spec.Name}'s exit has no trigger volume.");
            Assert.IsTrue(collider.isTrigger, $"{spec.Name}'s exit is solid.");

            yield return UnloadInterior();
        }
    }

    /// <summary>
    /// One-way story spaces deliberately have no exit — the prologue, the cave and everything
    /// after it. A door there would let the player walk out of the ending.
    /// </summary>
    [UnityTest]
    public IEnumerator OneWayStorySpacesHaveNoExit()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes.Where(s => !s.HasExitDoor))
        {
            yield return Load(spec.Name);

            Assert.IsNull(
                Object.FindFirstObjectByType<InteriorExit>(),
                $"{spec.Name} is a one-way story space but offers a way out of it.");

            yield return UnloadInterior();
        }
    }

    [UnityTest]
    public IEnumerator MultiRoomInteriorsActuallyContainTheirRooms()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes.Where(s => s.Rooms > 1))
        {
            yield return Load(spec.Name);

            for (int room = 1; room < spec.Rooms; room++)
            {
                var found = GameObject.Find($"Room_{room}");
                Assert.IsNotNull(found, $"{spec.Name} declares {spec.Rooms} rooms but Room_{room} is missing.");
            }

            yield return UnloadInterior();
        }
    }

    /// <summary>
    /// The doorway between the entrance hall and the first chamber must be open. A sealed
    /// back wall means every deeper room is unreachable geometry.
    /// </summary>
    [UnityTest]
    public IEnumerator TheCorridorIntoTheDeeperRoomsIsOpen()
    {
        yield return Load("Estmere_Prison");
        yield return new WaitForFixedUpdate();

        // Cast the doorway itself rather than the length of the hall: starting further back
        // hits the raised stage and the decorative story marker, both of which a player simply
        // walks up or around. The question is whether the back wall actually has a gap in it.
        var from = new Vector3(0f, 2f, 9f);
        bool blocked = Physics.Raycast(from, Vector3.forward, 6f,
            1 << GameLayers.Structure, QueryTriggerInteraction.Ignore);

        Assert.IsFalse(blocked,
            "The way into the prison's deeper rooms is walled off; those rooms cannot be reached.");
    }

    [UnityTest]
    public IEnumerator TheTowerHasALockWorthPicking()
    {
        yield return Load("Estmere_SecuredTower");

        var door = Object.FindFirstObjectByType<DoorAndLock>();
        Assert.IsNotNull(door, "B420 infiltrates a secured tower that has nothing to unlock.");
        Assert.IsTrue(door.IsLocked, "The tower door is already open.");
        Assert.Greater(door.Difficulty, 0f, "The lock has no difficulty, so Security is irrelevant.");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<DetectionWatcher>(),
            "Nobody is watching the tower, so picking the lock is not a crime.");
    }

    [UnityTest]
    public IEnumerator ThePrisonHasAPocketWorthLifting()
    {
        yield return Load("Estmere_Prison");

        var mark = Object.FindFirstObjectByType<PickpocketTarget>();
        Assert.IsNotNull(mark, "The prison has nobody to steal from.");
        Assert.Greater(mark.RemainingItems, 0, "The mark's pockets are empty.");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<DetectionWatcher>(),
            "No guard is watching, so the theft carries no risk at all.");
    }

    [UnityTest]
    public IEnumerator TheHarbourHasABoat()
    {
        yield return Load("Estmere_Harbor");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<SailingController>(),
            "B400 teaches sailing in a harbour with no boat in it.");
    }

    [UnityTest]
    public IEnumerator TheTrainingSpacesHaveSomethingToPractiseOn()
    {
        yield return Load("Tutorial_Warrior");
        Assert.IsNotNull(Object.FindFirstObjectByType<EnemyBrain>(),
            "The guard yard has nothing to spar against.");
        yield return UnloadInterior();

        yield return Load("Estmere_Arcanum");
        Assert.IsNotNull(Object.FindFirstObjectByType<EnemyBrain>(),
            "The Arcanum has nothing to cast at.");
    }
}
