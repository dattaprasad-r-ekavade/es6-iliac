using UnityEngine;

/// <summary>
/// A stable arrival point inside an authored content scene.
/// The id is persisted; the GameObject name is presentation only.
/// </summary>
public sealed class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId = "spawn.entry";

    public string SpawnId => spawnId;

    public void Configure(string id)
    {
        spawnId = id;
    }
}
