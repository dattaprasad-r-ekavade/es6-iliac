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

    /// <summary>What rank this archetype represents. Display only; scaling is applied.</summary>
    public int Level { get; init; } = 1;

    /// <summary>Health added per level above the first.</summary>
    public const float HealthPerLevel = 0.22f;

    /// <summary>Damage added per level above the first — slower than health on purpose.</summary>
    public const float DamagePerLevel = 0.16f;

    /// <summary>Experience added per level, which is why deeper floors are worth the risk.</summary>
    public const float XpPerLevel = 0.35f;

    /// <summary>
    /// The same enemy, at a deeper floor.
    ///
    /// Health grows faster than damage so a run gets longer before it gets lethal: a level
    /// that doubles both at once turns a survivable fight into an unwinnable one in a single
    /// step, which is the classic way a scaling curve ruins a roguelike.
    /// </summary>
    public EnemyArchetype AtLevel(int level, string? title = null)
    {
        var steps = Math.Max(0, level - 1);
        if (steps == 0 && title is null) return this;

        return new EnemyArchetype
        {
            Id = Id,
            DisplayName = title ?? DisplayName,
            Level = Math.Max(1, level),
            MaxHealth = MaxHealth * (1f + HealthPerLevel * steps),
            MoveSpeed = MoveSpeed,
            AggroRange = AggroRange,
            AttackRange = AttackRange,
            AttackDamage = AttackDamage * (1f + DamagePerLevel * steps),
            AttackCooldown = AttackCooldown,
            XpReward = (int)MathF.Round(XpReward * (1f + XpPerLevel * steps)),
            DropsLoot = DropsLoot
        };
    }
}

/// <summary>What an enemy wants to do this frame.</summary>
public enum EnemyIntent
{
    /// <summary>Out of range, staggered, or dead. Stand still.</summary>
    Idle,

    /// <summary>Close the distance. The game layer owns the actual movement.</summary>
    Chase,

    /// <summary>In reach and off cooldown.</summary>
    Attack
}

/// <summary>
/// One enemy: where it stands, what it wants, and what is true about it.
///
/// The decision is here and the movement is not. <see cref="Decide"/> answers "chase, swing,
/// or stand" from positions alone, which runs headlessly and can be asserted; the game layer
/// then slides the body and draws it. Line of sight stays in the game layer, because that
/// needs the level geometry.
/// </summary>
public sealed class Enemy : IEnemy, ITargetable
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

    /// <summary>Where the body is. The game layer moves it; the domain decides whether to.</summary>
    public WorldPoint Position { get; set; }

    /// <summary>Where it started, so it can be kept from being led across the map.</summary>
    public WorldPoint Home { get; set; }

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
    /// Chase, swing, or stand.
    /// </summary>
    /// <param name="playerPosition">Where the player is now.</param>
    /// <param name="canSeePlayer">
    /// False when the level geometry is in the way. Without this, enemies attack the player
    /// through the wall of the building they are guarding.
    /// </param>
    public EnemyIntent Decide(WorldPoint playerPosition, bool canSeePlayer = true)
    {
        // A staggered enemy neither closes nor swings — that is what makes shock a control
        // element rather than a third damage number.
        if (!IsAlive || IsStaggered || !canSeePlayer) return EnemyIntent.Idle;

        var distance = Position.FlatDistanceTo(playerPosition);
        if (distance > Archetype.AggroRange) return EnemyIntent.Idle;

        // Leashing: an enemy led far enough from home gives up rather than following the
        // player across the world.
        if (Home.FlatDistanceTo(Position) > Archetype.AggroRange * LeashFactor) return EnemyIntent.Idle;

        if (distance > Archetype.AttackRange) return EnemyIntent.Chase;
        return _attackCooldown <= 0f ? EnemyIntent.Attack : EnemyIntent.Idle;
    }

    /// <summary>How far past its aggro range an enemy will follow before giving up.</summary>
    private const float LeashFactor = 2.8f;

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
