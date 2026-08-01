using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Shared setup for the PlayMode smoke tests.
///
/// Two hazards these tests have to work around:
///
/// 1. <see cref="SaveLoadService.SaveFilePath"/> is a static pointing at
///    <c>Application.persistentDataPath</c>, so a test that saves would otherwise
///    overwrite the developer's real save slot. Every test runs against a backed-up
///    slot that is restored on teardown, pass or fail.
/// 2. Gameplay state lives in singletons and statics (<c>PlayerStats.Instance</c>,
///    <c>PlayerRef</c>, <c>WorldState</c>). Without an explicit reset, tests leak into
///    each other and start passing or failing based on run order.
/// </summary>
public abstract class SmokeTestFixture
{
    private readonly List<GameObject> _spawned = new();
    private string _testSavePath;

    [SetUp]
    public void BaseSetUp()
    {
        _testSavePath = Path.Combine(Application.temporaryCachePath,
            $"kessil-test-{GetType().Name}-{System.Guid.NewGuid():N}.json");
        SaveLoadService.ConfigureSaveFilePath(_testSavePath);

        WorldState.Reset();
        PlayerRef.Clear();
        Time.timeScale = 1f;
    }

    [TearDown]
    public void BaseTearDown()
    {
        // Dialogue and pause menus set timeScale to 0. Leaking that into the next test
        // would stall every coroutine it runs, so it is restored unconditionally.
        Time.timeScale = 1f;

        // DestroyImmediate rather than Destroy: singleton fields are cleared in
        // OnDestroy, and the next test needs them clear before it starts rather than
        // at the end of the frame.
        foreach (var go in _spawned)
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();

        WorldState.Reset();
        PlayerRef.Clear();

        foreach (var path in new[] { _testSavePath, _testSavePath + ".tmp", _testSavePath + ".bak" })
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        SaveLoadService.ResetSaveFilePath();
        _testSavePath = null;
    }

    /// <summary>Register an object for automatic teardown.</summary>
    protected GameObject Track(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    /// <summary>
    /// A minimal player. The name matters — <see cref="PlayerRef"/> falls back to
    /// <c>GameObject.Find("Player")</c>.
    /// </summary>
    protected GameObject SpawnPlayer()
    {
        var go = Track(new GameObject("Player"));
        go.AddComponent<PlayerStats>();
        go.AddComponent<PlayerInventory>();
        PlayerRef.Set(go.transform);
        return go;
    }

    protected SaveLoadService SpawnSaveService()
    {
        var go = Track(new GameObject("GameSystems_Test"));
        return go.AddComponent<SaveLoadService>();
    }

    protected static int CountOf(PlayerInventory inventory, string itemId)
    {
        var item = inventory.Items.Find(i => i.Id == itemId);
        return item != null ? item.Count : 0;
    }
}
