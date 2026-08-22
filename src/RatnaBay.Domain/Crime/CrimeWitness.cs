namespace RatnaBay.Domain;

/// <summary>
/// Crime response, kept deliberately small.
///
/// A crime only matters if someone sees it, so this routes through <see cref="Detection"/>
/// rather than owning its own notion of who is watching.
///
/// The consequence is suspicion, not arrest. Larceny lessons must be recoverable and never
/// terminal, and a system that jails the player mid-tutorial is exactly the dead end the
/// design forbids.
/// </summary>
public static class CrimeWitness
{
    /// <summary>Suspicion added when a crime is committed in view.</summary>
    public const float SeenPenalty = 0.5f;

    /// <summary>
    /// Report a crime. Nothing happens if no watcher can currently see the player — the
    /// unwitnessed theft is the point of stealth.
    /// </summary>
    /// <returns>True if it was witnessed.</returns>
    public static bool ReportIfSeen(Detection? detection)
    {
        // Anything above Unaware means someone is already looking in your direction.
        if (detection is null || detection.Awareness == AwarenessLevel.Unaware) return false;

        detection.AddSuspicion(SeenPenalty);
        return true;
    }
}
