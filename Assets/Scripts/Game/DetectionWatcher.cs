using UnityEngine;

/// <summary>
/// Something that can notice the player — a guard, a watchpost, a patrolling clerk.
///
/// Sight only. Hearing is deliberately not modelled: it is the part of stealth players find
/// unreadable, and a cone plus a raycast is legible enough to teach in the Chapter 01 trade
/// route without a tutorial explaining it.
/// </summary>
public sealed class DetectionWatcher : MonoBehaviour
{
    [SerializeField] private float viewRange = 14f;
    [SerializeField, Range(10f, 180f)] private float viewAngle = 100f;
    [SerializeField] private float eyeHeight = 1.6f;

    /// <summary>How much of the view cone is used. Below this the player is not seen at all.</summary>
    private const float VisibilityFloor = 0.25f;

    public float ViewRange => viewRange;
    public float ViewAngle => viewAngle;

    private void OnEnable() => DetectionSystem.Instance?.Register(this);
    private void OnDisable() => DetectionSystem.Instance?.Unregister(this);

    /// <summary>Reset any per-watcher memory. Currently stateless; kept as the hook.</summary>
    public void ResetView() { }

    /// <summary>
    /// Can this watcher currently see the player?
    /// </summary>
    /// <param name="visibility">
    /// 0–1 from <see cref="DetectionSystem.Visibility"/>. Crouching and stealth skill shrink
    /// the effective range and cone rather than adding a hidden roll — the player can see why
    /// they were spotted.
    /// </param>
    public bool CanSeePlayer(float visibility)
    {
        if (visibility <= VisibilityFloor) return false;
        if (!PlayerRef.TryGet(out var player)) return false;

        var eye = transform.position + Vector3.up * eyeHeight;
        var target = player.position + Vector3.up * 1.0f;
        var delta = target - eye;

        float range = viewRange * visibility;
        if (delta.sqrMagnitude > range * range) return false;

        var flat = new Vector3(delta.x, 0f, delta.z);
        if (flat.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(transform.forward, flat.normalized);
            if (angle > viewAngle * 0.5f) return false;
        }

        // Walls block sight. Without this, guards see through the prison they are guarding.
        return !Physics.Raycast(eye, delta.normalized, delta.magnitude,
            GameLayers.SightBlockerMask, QueryTriggerInteraction.Ignore);
    }
}
