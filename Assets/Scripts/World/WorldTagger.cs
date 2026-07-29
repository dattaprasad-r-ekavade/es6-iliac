using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Assigns physics layers to the generated world once per scene load.
///
/// The world in <c>Main.unity</c> is a baked artifact of the editor rebuild
/// command, and its objects carry no layers. Rather than requiring a full scene
/// rebuild before any layer-based fix works, this pass classifies the existing
/// hierarchy at startup.
///
/// It is deliberately the <b>only</b> place in the project that interprets a
/// GameObject's name. Everything downstream asks about layers instead, so a
/// rename can no longer silently break falling, spawning or out-of-bounds
/// detection. New objects created by <see cref="IliacBayWorldGenerator"/> get
/// their layer at creation time; this pass is idempotent and simply confirms it.
/// </summary>
public static class WorldTagger
{
    private static int _taggedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnGameStart()
    {
        // RuntimeInitializeOnLoadMethod fires once per play session, but "Main Menu"
        // reloads the scene — and a reloaded scene comes back with the layers it has
        // on disk (none). Without re-tagging, every ground query would fail and the
        // safety guard would teleport the player on a loop.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    private static void Apply()
    {
        TagActiveScene();
        ConfigureCameras();
    }

    /// <summary>Classify every root object in the active scene.</summary>
    public static void TagActiveScene()
    {
        // Guard against double-tagging when the generator also calls us.
        if (_taggedFrame == Time.frameCount) return;
        _taggedFrame = Time.frameCount;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        foreach (var root in scene.GetRootGameObjects())
            TagHierarchy(root);
    }

    /// <summary>Classify a subtree. Safe to call repeatedly.</summary>
    public static void TagHierarchy(GameObject root)
    {
        if (root == null) return;

        int layer = Classify(root.name);
        if (layer >= 0) SetLayerRecursive(root, layer);

        // Children can override the parent's classification (a prop parented
        // under a landmass is still a prop).
        foreach (Transform child in root.transform)
            TagHierarchy(child.gameObject);
    }

    /// <summary>Name → layer. Returns -1 for "no opinion, leave as-is".</summary>
    public static int Classify(string name)
    {
        if (string.IsNullOrEmpty(name)) return -1;

        // Never counts as ground: the safety slab under the map.
        if (name.StartsWith("FallCatcher")) return GameLayers.Void;
        if (name.Contains("Ocean")) return GameLayers.Water;

        // Scatter: trees, rocks, desert brush.
        if (name.StartsWith("Prop_")) return GameLayers.Prop;

        // Walkable surfaces.
        if (name.StartsWith("TerrainSurface")
            || name.StartsWith("SpawnPad")
            || name.StartsWith("CityGround")
            || name.StartsWith("KeepYard")
            || name.StartsWith("Road")
            || name.StartsWith("Pier")
            || name.StartsWith("Plaza"))
            return GameLayers.Ground;

        // Solid but not walkable-by-design.
        if (name.StartsWith("Wall_")
            || name.StartsWith("Walls")
            || name.StartsWith("GateTower")
            || name.StartsWith("GateLintel")
            || name.StartsWith("Citadel")
            || name.StartsWith("Keep")
            || name.StartsWith("Building")
            || name.StartsWith("Stall_")
            || name.StartsWith("AdamantineTower"))
            return GameLayers.Structure;

        // Characters are tagged by their spawners, not by name.
        return -1;
    }

    public static void SetLayerRecursive(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    /// <summary>
    /// The world spans ~6.8 km but cameras default to a 1000 m far plane, so
    /// distant cities popped in and out. Applied at runtime so the baked scene
    /// benefits without a rebuild.
    /// </summary>
    public static void ConfigureCameras()
    {
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            if (cam.farClipPlane < WorldLayout.CameraFarPlane)
                cam.farClipPlane = WorldLayout.CameraFarPlane;

            // First-person cameras must not render the player's full-body model;
            // otherwise its head and shoulders clip through the near plane.
            bool isPlayerCamera = cam.CompareTag("MainCamera")
                || (PlayerRef.Transform != null && cam.transform.IsChildOf(PlayerRef.Transform));
            if (isPlayerCamera)
                cam.cullingMask &= ~(1 << GameLayers.Player);
        }
    }
}
