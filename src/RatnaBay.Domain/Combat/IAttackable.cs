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

    /// <summary>
    /// True while this cannot fight back — still rising, or staggered.
    ///
    /// The ambush, which is the one idea worth keeping out of the stealth pillar that was
    /// parked. A room's occupants rise when it is entered and a shock spell staggers what it
    /// hits; both are windows in which the thing in front of you is helpless, and rewarding a
    /// blow landed in one is what makes rushing into a room better than waiting at its door.
    /// </summary>
    bool IsVulnerable => false;

    /// <summary>Apply damage. Returns the amount that actually landed.</summary>
    float TakeDamage(float amount);

    /// <summary>
    /// Leave this helpless for a moment. A no-op for anything that cannot be staggered.
    ///
    /// Defaulted rather than required because most things a swing can land on are not
    /// enemies, and forcing every one of them to implement a combat verb they do not have is
    /// how an interface stops describing anything.
    /// </summary>
    void ApplyStagger(float seconds) { }

    /// <summary>
    /// Apply damage and remember what did it.
    ///
    /// Attribution exists so a recording can answer questions about *how* a fight was won —
    /// which weapon killed which enemy, and how much of the work a lingering burn did. That
    /// turns out to be where a player's actual tactics live, and none of it is visible from
    /// a log that only knows something died.
    /// </summary>
    float TakeDamage(float amount, string? source) => TakeDamage(amount);
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

    public static float Resolve(float amount, float armour, bool blocking) =>
        Resolve(amount, armour, blocking ? BlockReduction : 1f);

    /// <summary>
    /// The same rule with the guard's quality passed in rather than assumed.
    ///
    /// A bare guard halves a blow; a shield takes it further. Expressing it as the whole
    /// factor means there is one number to read and no second place for the two to disagree —
    /// which is exactly how a "block bonus" and a "block reduction" end up drifting apart.
    /// </summary>
    public static float Resolve(float amount, float armour, float blockFactor)
    {
        var incoming = amount * MathF.Max(0f, blockFactor);
        incoming -= armour;
        return MathF.Max(MinimumDamage, incoming);
    }
}
