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
    private static Camera[] _cameraBuffer = new Camera[2];

    private int _cursor;
    private Camera _cullingCamera;

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
        if (!TryGetCullOrigin(out var origin)) return;

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

    private bool TryGetCullOrigin(out Vector3 origin)
    {
        if (_cullingCamera == null || !_cullingCamera.isActiveAndEnabled)
        {
            _cullingCamera = FindActiveRenderingCamera();
        }

        if (_cullingCamera != null)
        {
            origin = _cullingCamera.transform.position;
            return true;
        }

        // The title screen intentionally has no active camera. Retain the previous
        // player-based behaviour there so props are culled before the intro begins.
        if (PlayerRef.TryGet(out var player))
        {
            origin = player.position;
            return true;
        }

        origin = default;
        return false;
    }

    private static Camera FindActiveRenderingCamera()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
        {
            return main;
        }

        int cameraCount = Camera.allCamerasCount;
        if (cameraCount == 0) return null;

        if (_cameraBuffer.Length < cameraCount)
        {
            _cameraBuffer = new Camera[Mathf.NextPowerOfTwo(cameraCount)];
        }

        cameraCount = Camera.GetAllCameras(_cameraBuffer);
        Camera best = null;
        for (int i = 0; i < cameraCount; i++)
        {
            var candidate = _cameraBuffer[i];
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            if (best == null || candidate.depth > best.depth) best = candidate;
        }

        return best;
    }
}
