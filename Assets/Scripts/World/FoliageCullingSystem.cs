using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-culls every registered prop from a single time-sliced Update.
///
/// Replaces ~640 individual MonoBehaviour.Update calls (each doing its own
/// GameObject.Find) with one, spreading the distance tests over several frames so
/// no single frame pays for the whole world.
/// </summary>
[DefaultExecutionOrder(-50)]
public class FoliageCullingSystem : MonoBehaviour
{
    /// <summary>How many frames one full sweep of the prop list is spread across.</summary>
    private const int SweepFrames = 8;

    private static FoliageCullingSystem _instance;
    private static readonly List<FoliageDistanceCull> Props = new();

    private int _cursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Re-created on every scene load: this object lives in the scene, so the
        // reload behind "Main Menu" destroys it.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureExists();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode) => EnsureExists();

    private static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject("FoliageCullingSystem");
        _instance = go.AddComponent<FoliageCullingSystem>();
    }

    public static void Register(FoliageDistanceCull prop)
    {
        if (prop == null) return;
        Props.Add(prop);
    }

    public static void Unregister(FoliageDistanceCull prop)
    {
        if (prop == null) return;
        int i = Props.IndexOf(prop);
        if (i < 0) return;
        // Swap-remove: order doesn't matter and this stays O(1).
        Props[i] = Props[Props.Count - 1];
        Props.RemoveAt(Props.Count - 1);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        // Deliberately not clearing Props here: destruction order between this object
        // and the next scene's props isn't guaranteed, and Update already prunes
        // entries that have gone null.
    }

    private void Update()
    {
        if (Props.Count == 0) return;
        if (!PlayerRef.TryGet(out var player)) return;

        var origin = player.position;
        int perFrame = Mathf.Max(1, Props.Count / SweepFrames);

        for (int n = 0; n < perFrame; n++)
        {
            if (_cursor >= Props.Count) _cursor = 0;

            var prop = Props[_cursor];
            if (prop == null)
            {
                Props[_cursor] = Props[Props.Count - 1];
                Props.RemoveAt(Props.Count - 1);
                if (Props.Count == 0) return;
                continue;
            }

            float max = prop.maxDistance;
            prop.SetVisible((origin - prop.transform.position).sqrMagnitude < max * max);
            _cursor++;
        }
    }
}
