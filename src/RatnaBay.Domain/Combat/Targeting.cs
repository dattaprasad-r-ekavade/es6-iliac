namespace RatnaBay.Domain;

/// <summary>Something that occupies a place and can be aimed at.</summary>
public interface ITargetable
{
    WorldPoint Position { get; }
    bool IsAlive { get; }
}

/// <summary>
/// Choosing what a swing or a spell lands on.
///
/// This replaces the sphere cast the Unity build used. A cast needs a physics world; a cone
/// test needs a position and a facing, which means it runs headlessly and its edges can be
/// asserted rather than felt out in the running game.
/// </summary>
public static class Targeting
{
    /// <summary>Half-angle of the melee cone. Generous enough not to feel like a needle.</summary>
    public const float MeleeConeRadians = 0.6f;

    /// <summary>Half-angle for a cast. Tighter — a spell is aimed, not swung.</summary>
    public const float SpellConeRadians = 0.28f;

    /// <summary>
    /// The direction a given yaw faces, flat on the ground plane.
    ///
    /// Yaw increases clockwise, so zero looks down -Z and a quarter turn looks down +X. This
    /// is the same convention the camera uses; keeping one definition is what stops the
    /// crosshair and the hit test disagreeing about where "forward" is.
    /// </summary>
    public static WorldPoint FlatForward(float yaw) =>
        new(MathF.Sin(yaw), 0f, -MathF.Cos(yaw));

    /// <summary>
    /// The nearest living candidate within <paramref name="range"/> and inside the cone.
    /// Null when the player swung at nothing.
    /// </summary>
    public static T? Find<T>(
        WorldPoint origin,
        float yaw,
        float range,
        IEnumerable<T> candidates,
        float coneRadians = MeleeConeRadians) where T : class, ITargetable
    {
        var forward = FlatForward(yaw);
        var minimumDot = MathF.Cos(coneRadians);

        T? best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (!candidate.IsAlive) continue;

            var distance = origin.FlatDistanceTo(candidate.Position);
            if (distance > range || distance >= bestDistance) continue;

            // Anything standing on top of you is in front of you by definition; normalising a
            // zero-length delta would otherwise decide it is not.
            if (distance > 0.001f)
            {
                var dx = (candidate.Position.X - origin.X) / distance;
                var dz = (candidate.Position.Z - origin.Z) / distance;
                if (dx * forward.X + dz * forward.Z < minimumDot) continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// Where something is relative to the way the player is facing, in radians.
    ///
    /// Zero is dead ahead, positive is to the right, and the result is wrapped to [-pi, pi]
    /// so a target slightly to the left reads as a small negative angle rather than nearly a
    /// full turn. This is what an on-screen direction indicator points along.
    /// </summary>
    public static float RelativeBearing(WorldPoint origin, float yaw, WorldPoint target)
    {
        var dx = target.X - origin.X;
        var dz = target.Z - origin.Z;
        if (MathF.Abs(dx) < 0.0001f && MathF.Abs(dz) < 0.0001f) return 0f;

        // Matches FlatForward: yaw zero looks down -Z and increases clockwise.
        var absolute = MathF.Atan2(dx, -dz);
        var relative = absolute - yaw;

        while (relative > MathF.PI) relative -= MathF.Tau;
        while (relative < -MathF.PI) relative += MathF.Tau;
        return relative;
    }

    /// <summary>
    /// The nearest other living candidate to <paramref name="source"/>, for Arc's one jump.
    /// </summary>
    public static T? FindNearestOther<T>(T source, IEnumerable<T> candidates, float radius)
        where T : class, ITargetable
    {
        T? best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate, source) || !candidate.IsAlive) continue;

            var distance = source.Position.FlatDistanceTo(candidate.Position);
            if (distance > radius || distance >= bestDistance) continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }
}
