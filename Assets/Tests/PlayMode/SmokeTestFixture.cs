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
    private string _backupPath;

    [SetUp]
    public void BaseSetUp()
    {
        // Preserve any real save, then start every test from "no save exists".
        var path = SaveLoadService.SaveFilePath;
        if (File.Exists(path))
        {
            _backupPath = path + ".testbackup";
            File.Copy(path, _backupPath, true);
            File.Delete(path);
        }

        WorldState.Reset();
        PlayerRef.Clear();
    }

    [TearDown]
    public void BaseTearDown()
    {
        // DestroyImmediate rather than Destroy: singleton fields are cleared in
        // OnDestroy, and the next test needs them clear before it starts rather than
        // at the end of the frame.
        foreach (var go in _spawned)
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();

        WorldState.Reset();
        PlayerRef.Clear();

        var path = SaveLoadService.SaveFilePath;
        if (File.Exists(path)) File.Delete(path);

        if (_backupPath != null && File.Exists(_backupPath))
        {
            File.Copy(_backupPath, path, true);
            File.Delete(_backupPath);
            _backupPath = null;
        }
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
