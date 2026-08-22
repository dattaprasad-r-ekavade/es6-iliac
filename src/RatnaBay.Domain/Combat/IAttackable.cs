namespace RatnaBay.Domain;

/// <summary>
/// Something a swing or a spell can land on.
///
/// The game layer owns finding the target (a sphere cast down the camera forward); the
/// domain owns what happens once one is found. <see cref="MaxHealth"/> is the threat figure
/// skill progression keys off, which is what makes the fortieth identical bandit worthless.
/// </summary>
public interface IAttackable
{
    float Health { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }

    /// <summary>Apply damage. Returns the amount that actually landed.</summary>
    float TakeDamage(float amount);
}

/// <summary>
/// The one place incoming damage is resolved, so armour and blocking cannot drift apart
/// between the player and anything else that can be hit.
/// </summary>
public static class DamageMath
{
    /// <summary>Blocking halves what gets through. Block is the active defensive verb.</summary>
    public const float BlockReduction = 0.5f;

    /// <summary>A hit always lands for at least this much, so armour is never invulnerability.</summary>
    public const float MinimumDamage = 1f;

    public static float Resolve(float amount, float armour, bool blocking)
    {
        var incoming = amount;
        if (blocking) incoming *= BlockReduction;
        incoming -= armour;
        return MathF.Max(MinimumDamage, incoming);
    }
}
