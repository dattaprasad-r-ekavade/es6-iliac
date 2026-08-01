using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Declares the stable identity and arrival points owned by one content scene.
/// Bootstrap is deliberately not a content scene and has no SceneContext.
/// </summary>
public sealed class SceneContext : MonoBehaviour
{
    [SerializeField] private string sceneId = "scene.unassigned";
    [SerializeField] private string defaultSpawnId = "spawn.entry";

    private readonly Dictionary<string, SceneSpawnPoint> _spawns = new(StringComparer.Ordinal);
    private bool _cacheBuilt;

    public string SceneId => sceneId;
    public string DefaultSpawnId => defaultSpawnId;

    public void Configure(string id, string defaultSpawn)
    {
        sceneId = id;
        defaultSpawnId = defaultSpawn;
        _cacheBuilt = false;
    }

    public bool TryGetSpawn(string id, out SceneSpawnPoint spawn)
    {
        spawn = null;
        BuildSpawnCache();
        return !string.IsNullOrWhiteSpace(id) && _spawns.TryGetValue(id, out spawn);
    }

    public bool TryGetDefaultSpawn(out SceneSpawnPoint spawn)
    {
        return TryGetSpawn(defaultSpawnId, out spawn);
    }

    public static SceneContext FindInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var context = root.GetComponentInChildren<SceneContext>(true);
            if (context != null) return context;
        }

        return null;
    }

    private void BuildSpawnCache()
    {
        if (_cacheBuilt) return;

        _cacheBuilt = true;
        _spawns.Clear();
        foreach (var spawn in GetComponentsInChildren<SceneSpawnPoint>(true))
        {
            if (spawn == null || string.IsNullOrWhiteSpace(spawn.SpawnId)) continue;
            if (!_spawns.ContainsKey(spawn.SpawnId))
                _spawns.Add(spawn.SpawnId, spawn);
        }
    }
}
