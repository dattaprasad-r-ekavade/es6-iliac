using UnityEngine;

/// <summary>
/// A doorway in the region exterior that loads an interior scene.
///
/// This is the Morrowind cell model: a large generated exterior plus discrete interiors,
/// joined by doors. It means an interior never has to be modelled through the wall of the
/// building containing it, and it reuses <see cref="SceneTransitionService"/> — already
/// proven by the VS2 gate — rather than inventing a second way to move between spaces.
///
/// Entry is by interaction, never by walking through. A trigger that teleports on touch is
/// how players end up in a building they were walking past.
/// </summary>
public sealed class RegionPortal : MonoBehaviour
{
    [SerializeField] private string anchorId;
    [SerializeField] private string displayName;
    [SerializeField] private string sceneName;
    [SerializeField] private string spawnId;

    public string AnchorId => anchorId;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public string SpawnId => spawnId;

    /// <summary>True while the player is close enough to use this door.</summary>
    public bool PlayerIsInRange { get; private set; }

    public void Configure(string anchor, string display, string scene, string spawn)
    {
        anchorId = anchor;
        displayName = display;
        sceneName = scene;
        spawnId = spawn;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        PlayerIsInRange = true;
        if (!string.IsNullOrEmpty(sceneName))
            GameHud.Instance?.ShowToast($"{displayName} — press E to enter.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other)) PlayerIsInRange = false;
    }

    private void Update()
    {
        if (!PlayerIsInRange) return;
        if (GameStateService.Instance != null && !GameStateService.Instance.GameplayInputAllowed) return;
        if (!GameInput.Interact.WasPressedThisFrame()) return;
        Enter();
    }

    /// <summary>
    /// Walk through the door. Public so tutorials and tests can drive it without synthesising
    /// input, and so a quest can push the player through a door it just unlocked.
    /// </summary>
    public bool Enter()
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        var transition = SceneTransitionService.Instance;
        if (transition == null)
        {
            Debug.LogError($"[RegionPortal] {anchorId} has no SceneTransitionService to load {sceneName}.");
            return false;
        }

        if (transition.IsTransitioning) return false;

        // Where the player came from, so the interior's exit can put them back outside this
        // door rather than at the region's default spawn.
        RegionReturn.Remember(anchorId);
        transition.StartCoroutine(transition.TransitionTo(sceneName, spawnId));
        return true;
    }

    private static bool IsPlayer(Collider other) =>
        other != null && other.transform.root == PlayerRef.Transform;
}

/// <summary>
/// Remembers which door the player last used, so leaving an interior returns them to it.
///
/// Static because it must survive the scene unload that happens between entering and leaving.
/// </summary>
public static class RegionReturn
{
    public static string LastAnchorId { get; private set; }

    public static void Remember(string anchorId) => LastAnchorId = anchorId;

    public static void Clear() => LastAnchorId = null;

    /// <summary>
    /// Where to put the player when they step back outside. Falls back to the region spawn
    /// when there is no record — a save loaded directly into an interior, for instance.
    /// </summary>
    public static Vector3 ReturnPosition()
    {
        var anchor = EstmereRegion.FindAnchor(LastAnchorId);
        if (anchor == null) return EstmereRegion.PlayerSpawn;

        // Just outside the doorway, facing away from the building.
        var facing = Quaternion.Euler(0f, anchor.Value.FacingDegrees, 0f) * Vector3.forward;
        return anchor.Value.Position + facing * (anchor.Value.Footprint * 0.5f + 4f);
    }
}
