namespace RatnaBay.Domain;

/// <summary>Statistics for one kind of enemy. Spawned instances share these.</summary>
public sealed class EnemyArchetype
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public float MaxHealth { get; init; } = 55f;

    /// <summary>
    /// Must stay above the player's default travel speed. This was 4.2 when the player walked
    /// at 3.5; making run the default would silently have made every fight optional, because
    /// nothing in the world could close the distance any more.
    /// </summary>
    public float MoveSpeed { get; init; } = 5.6f;

    public float AggroRange { get; init; } = 14f;
    public float AttackRange { get; init; } = 2.1f;
    public float AttackDamage { get; init; } = 4f;
    public float AttackCooldown { get; init; } = 1.4f;
    public int XpReward { get; init; } = 20;
    public bool DropsLoot { get; init; } = true;
}

/// <summary>
/// One enemy's fight state: health, elemental status, and the attack cooldown.
///
/// Chasing, line of sight and leashing stay in the game layer — this owns what is true about
/// the enemy, not where it is standing. <see cref="WantsToAttack"/> is the decision the game
/// layer asks for once it knows the enemy is in range and can see the player.
/// </summary>
public sealed class Enemy : IEnemy
{
    /// <summary>A chill can never take more than 90% off a target's speed.</summary>
    private const float SlowestChill = 0.1f;

    private float _burnDamagePerSecond;
    private float _burnRemaining;
    private float _chillFactor = 1f;
    private float _chillRemaining;
    private float _staggerRemaining;
    private float _attackCooldown;

    public Enemy(EnemyArchetype archetype, string spawnId)
    {
        Archetype = archetype;
        SpawnId = spawnId;
        Health = archetype.MaxHealth;
    }

    public EnemyArchetype Archetype { get; }

    /// <summary>Stable id so a save can remember this one stayed dead.</summary>
    public string SpawnId { get; }

    public string DisplayName => Archetype.DisplayName;
    public float Health { get; private set; }
    public float MaxHealth => Archetype.MaxHealth;
    public bool IsAlive => Health > 0f;

    public bool IsBurning => _burnRemaining > 0f;
    public bool IsChilled => _chillRemaining > 0f;

    /// <summary>Staggered enemies neither close nor swing — shock is control, not damage.</summary>
    public bool IsStaggered => _staggerRemaining > 0f;

    /// <summary>Current move speed after chill. Never negative.</summary>
    public float CurrentMoveSpeed => Archetype.MoveSpeed * (IsChilled ? _chillFactor : 1f);

    /// <summary>True when the cooldown has elapsed and the enemy is not staggered.</summary>
    public bool CanAttack => !IsStaggered && _attackCooldown <= 0f && IsAlive;

    public event Action<float, float>? HealthChanged;
    public event Action<Enemy>? Died;

    /// <summary>Fire: damage over time. Beats groups and the unarmoured.</summary>
    public void ApplyBurn(float damagePerSecond, float duration)
    {
        _burnDamagePerSecond = MathF.Max(_burnDamagePerSecond, damagePerSecond);
        _burnRemaining = MathF.Max(_burnRemaining, duration);
    }

    /// <summary>Frost: slows. Beats chargers.</summary>
    public void ApplyChill(float speedFactor, float duration)
    {
        _chillFactor = Math.Clamp(MathF.Min(_chillFactor, speedFactor), SlowestChill, 1f);
        _chillRemaining = MathF.Max(_chillRemaining, duration);
    }

    /// <summary>Shock: interrupts. Beats anything mid-action.</summary>
    public void ApplyStagger(float duration) =>
        _staggerRemaining = MathF.Max(_staggerRemaining, duration);

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || !IsAlive) return;

        if (_attackCooldown > 0f) _attackCooldown = MathF.Max(0f, _attackCooldown - deltaSeconds);
        if (_staggerRemaining > 0f) _staggerRemaining = MathF.Max(0f, _staggerRemaining - deltaSeconds);

        if (_chillRemaining > 0f)
        {
            _chillRemaining = MathF.Max(0f, _chillRemaining - deltaSeconds);
            if (_chillRemaining <= 0f) _chillFactor = 1f;
        }

        if (_burnRemaining <= 0f) return;

        _burnRemaining = MathF.Max(0f, _burnRemaining - deltaSeconds);
        TakeDamage(_burnDamagePerSecond * deltaSeconds);
        if (_burnRemaining <= 0f) _burnDamagePerSecond = 0f;
    }

    /// <summary>
    /// Commit to a swing. The game layer calls this once it knows the player is in range and
    /// in line of sight; the damage is the caller's to apply.
    /// </summary>
    public float Attack()
    {
        if (!CanAttack) return 0f;
        _attackCooldown = Archetype.AttackCooldown;
        return Archetype.AttackDamage;
    }

    public float TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return 0f;

        var before = Health;
        Health = MathF.Max(0f, Health - amount);
        HealthChanged?.Invoke(Health, MaxHealth);

        if (!IsAlive) Died?.Invoke(this);
        return before - Health;
    }
}
