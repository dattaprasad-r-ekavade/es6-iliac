using UnityEngine;

/// <summary>
/// The way out of an interior, back to the region.
///
/// Before this existed the director returned the player automatically, which meant a human
/// who wandered off the critical path had no way to leave under their own steam — the only
/// exit was the story advancing. That is fine for an automated run and unacceptable for
/// someone actually playing.
///
/// Leaving puts the player back at the door they came in by, via <see cref="RegionReturn"/>,
/// so stepping outside never teleports them across the city.
/// </summary>
public sealed class InteriorExit : MonoBehaviour
{
    [SerializeField] private string label = "Outside";

    public bool PlayerIsInRange { get; private set; }

    public void Configure(string exitLabel) => label = exitLabel;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        PlayerIsInRange = true;
        GameHud.Instance?.ShowToast($"{label} — press E to leave.");
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
        Leave();
    }

    /// <summary>
    /// Step outside. Public so a test can drive it, and so a scripted beat can push the
    /// player out of a room it has finished with.
    /// </summary>
    public bool Leave()
    {
        var transition = SceneTransitionService.Instance;
        if (transition == null || transition.IsTransitioning) return false;

        transition.StartCoroutine(LeaveRoutine(transition));
        return true;
    }

    private static System.Collections.IEnumerator LeaveRoutine(SceneTransitionService transition)
    {
        yield return transition.TransitionTo(GreyThreadDirector.RegionScene, "spawn.region");
        if (!string.IsNullOrEmpty(transition.LastError)) yield break;
        RegionReturn.PlacePlayerAtReturn(PlayerRef.Transform);
    }

    private static bool IsPlayer(Collider other) =>
        other != null && other.transform.root == PlayerRef.Transform;
}
