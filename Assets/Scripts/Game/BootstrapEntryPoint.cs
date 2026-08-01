using System.Collections;
using UnityEngine;

/// <summary>Loads the first content scene while the Bootstrap scene remains resident.</summary>
public sealed class BootstrapEntryPoint : MonoBehaviour
{
    [SerializeField] private string initialSceneName = "Main";
    [SerializeField] private string initialSpawnId = "spawn.caldemar";

    public void Configure(string sceneName, string spawnId)
    {
        initialSceneName = sceneName;
        initialSpawnId = spawnId;
    }

    private IEnumerator Start()
    {
        // Let every Bootstrap service finish Awake before content begins loading.
        yield return null;

        var transitions = SceneTransitionService.Instance;
        if (transitions == null)
        {
            Debug.LogError("[Bootstrap] SceneTransitionService is missing.");
            yield break;
        }

        yield return transitions.TransitionTo(initialSceneName, initialSpawnId, unloadPrevious: false);
    }
}
