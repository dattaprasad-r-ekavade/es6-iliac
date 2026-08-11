using UnityEngine;

/// <summary>
/// Crime response, kept deliberately small.
///
/// A crime only matters if someone sees it, so this routes through
/// <see cref="DetectionSystem"/> rather than owning its own notion of who is watching.
///
/// The consequence is suspicion, not arrest. The beat sheet requires B410's larceny lessons
/// to be recoverable and never terminal, and a system that jails the player mid-tutorial is
/// exactly the dead end VS4's gate forbids.
/// </summary>
public static class CrimeWitness
{
    /// <summary>Suspicion added when a crime is committed in view.</summary>
    private const float SeenPenalty = 0.5f;

    /// <summary>
    /// Report a crime. Nothing happens if no watcher can currently see the player — the
    /// unwitnessed theft is the point of stealth.
    /// </summary>
    /// <returns>True if it was witnessed.</returns>
    public static bool ReportIfSeen(string description)
    {
        var detection = DetectionSystem.Instance;
        if (detection == null) return false;

        // Anything above Unaware means someone is already looking in your direction.
        if (detection.Awareness == AwarenessLevel.Unaware) return false;

        detection.AddSuspicion(SeenPenalty);
        GameHud.Instance?.ShowToast($"You were seen {description}.");
        return true;
    }
}
