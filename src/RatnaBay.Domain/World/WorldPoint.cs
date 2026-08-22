namespace RatnaBay.Domain;

/// <summary>
/// A position in the world, engine-free. The game layer converts to and from its own vector
/// type at the boundary, so the domain never takes a dependency on a math library.
/// </summary>
public readonly record struct WorldPoint(float X, float Y, float Z)
{
    /// <summary>Distance ignoring height. Nearly every gameplay question is a flat one.</summary>
    public float FlatDistanceTo(WorldPoint other)
    {
        var dx = other.X - X;
        var dz = other.Z - Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Eight-point compass bearing from here to <paramref name="target"/>. Finer than that is
    /// precision the player cannot use.
    /// </summary>
    public string CompassTo(WorldPoint target)
    {
        var angle = MathF.Atan2(target.X - X, target.Z - Z) * (180f / MathF.PI);
        if (angle < 0f) angle += 360f;

        string[] points =
        {
            "north", "north-east", "east", "south-east",
            "south", "south-west", "west", "north-west"
        };

        return points[(int)MathF.Round(angle / 45f) % 8];
    }
}
