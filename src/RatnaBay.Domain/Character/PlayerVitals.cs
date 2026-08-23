namespace RatnaBay.Domain;

/// <summary>
/// Player health, resources, XP and death.
///
/// Prana is the one resource that does not regenerate: it is charge drawn from jiva stones,
/// and the setting's scarcity is not real if the player's own bar refills for free.
/// </summary>
public sealed class PlayerVitals
{
    /// <summary>Stamina regained per second while a fight is live.</summary>
    public const float CombatStaminaRegen = 4f;

    /// <summary>Stamina regained per second out of combat.</summary>
    public const float RestStaminaRegen = 12f;

    private const float HealthPerLevel = 12f;
    private const float PranaPerLevel = 8f;
    private const float StaminaPerLevel = 8f;

    private readonly Inventory _inventory;

    public PlayerVitals(Inventory inventory) => _inventory = inventory;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToLevel => 40 + Level * 35;

    public float Health { get; private set; } = 100f;
    public float MaxHealth { get; private set; } = 100f;
    public float Prana { get; private set; } = 80f;
    public float MaxPrana { get; private set; } = 80f;
    public float Stamina { get; private set; } = 100f;
    public float MaxStamina { get; private set; } = 100f;
    public int Gold { get; private set; }

    public bool IsAlive => Health > 0f;

    /// <summary>
    /// Lifetime crystals burned. Tracked, never punished — it changes what the world says to
    /// the player, never how hard enemies hit.
    /// </summary>
    public int Channeled { get; private set; }

    public event Action? Changed;
    public event Action<int>? LevelGained;
    public event Action? CrystalDrawn;
    public event Action? Died;

    /// <summary>Stamina recovers over time; prana deliberately does not.</summary>
    public void Tick(float deltaSeconds, bool inCombat)
    {
        if (deltaSeconds <= 0f || !IsAlive) return;

        // Combat regen used to be zero, which gave twelve swings and then six seconds of
        // standing there unable to attack. Reduced, not absent.
        var regen = inCombat ? CombatStaminaRegen : RestStaminaRegen;
        var before = Stamina;
        Stamina = MathF.Min(MaxStamina, Stamina + regen * deltaSeconds);
        if (Stamina != before) Changed?.Invoke();
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        Xp += amount;
        while (Xp >= XpToLevel)
        {
            Xp -= XpToLevel;
            Level++;
            MaxHealth += HealthPerLevel;
            MaxPrana += PranaPerLevel;
            MaxStamina += StaminaPerLevel;
            Health = MaxHealth;
            Stamina = MaxStamina;
            // Prana is charge, not a pool: levelling raises the ceiling but hands out no free
            // stones. Refilling here would make every level-up a silent resupply.
            LevelGained?.Invoke(Level);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Take a hit. Worn armour is flat reduction and blocking halves what gets through —
    /// armour has no skill of its own because Block is the active defensive verb.
    /// </summary>
    public float TakeDamage(float amount, float armour = 0f, bool blocking = false)
    {
        if (!IsAlive) return 0f;

        var incoming = DamageMath.Resolve(amount, armour, blocking);
        Health = MathF.Max(0f, Health - incoming);
        Changed?.Invoke();

        if (!IsAlive) Died?.Invoke();
        return incoming;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        Health = MathF.Min(MaxHealth, Health + amount);
        Changed?.Invoke();
    }

    public bool SpendStamina(float amount)
    {
        if (amount <= 0f || Stamina < amount) return false;
        Stamina -= amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Spend charge, drawing on a jiva stone if the reserve is short. Returns false only when
    /// the player has neither enough charge nor a stone to draw.
    /// </summary>
    public bool SpendPrana(float amount)
    {
        if (amount <= 0f || amount > MaxPrana) return false;

        if (Prana < amount)
        {
            var stonesNeeded = (int)MathF.Ceiling((amount - Prana) / SoulCrystals.LesserCharge);
            if (_inventory.CountOf(SoulCrystals.LesserId) < stonesNeeded) return false;

            for (var stone = 0; stone < stonesNeeded; stone++)
                if (!TryDrawCrystal()) return false;
        }

        if (Prana < amount) return false;

        Prana -= amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Draw one jiva stone for charge. Deliberately announced: the player should feel every
    /// one of these, because the whole arc is an argument about what they cost.
    /// </summary>
    public bool TryDrawCrystal()
    {
        if (Prana >= MaxPrana) return false;

        if (!_inventory.Consume(SoulCrystals.LesserId)) return false;

        Prana = MathF.Min(MaxPrana, Prana + SoulCrystals.LesserCharge);
        Channeled++;
        CrystalDrawn?.Invoke();
        Changed?.Invoke();
        return true;
    }

    public void AddGold(int amount)
    {
        Gold = Math.Max(0, Gold + amount);
        Changed?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || Gold < amount) return false;
        Gold -= amount;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Recovery after death or rescue. Health and stamina come back; charge does not, because
    /// dying is not a way to refill crystals you did not spend gold on.
    /// </summary>
    public void FullRestore()
    {
        Health = MaxHealth;
        Stamina = MaxStamina;
        Changed?.Invoke();
    }

    /// <summary>Restore from a save. Values are clamped rather than trusted.</summary>
    public void Restore(SavedVitals saved)
    {
        Level = Math.Max(1, saved.Level);
        Xp = Math.Max(0, saved.Xp);
        Gold = Math.Max(0, saved.Gold);
        Channeled = Math.Max(0, saved.Channeled);

        MaxHealth = MathF.Max(1f, saved.MaxHealth);
        MaxPrana = MathF.Max(0f, saved.MaxPrana);
        MaxStamina = MathF.Max(1f, saved.MaxStamina);

        Health = Math.Clamp(saved.Health, 0f, MaxHealth);
        Prana = Math.Clamp(saved.Prana, 0f, MaxPrana);
        Stamina = Math.Clamp(saved.Stamina, 0f, MaxStamina);

        Changed?.Invoke();
    }

    public SavedVitals Capture() => new()
    {
        Level = Level, Xp = Xp, Gold = Gold, Channeled = Channeled,
        Health = Health, MaxHealth = MaxHealth,
        Prana = Prana, MaxPrana = MaxPrana,
        Stamina = Stamina, MaxStamina = MaxStamina
    };
}

/// <summary>The vitals block as written to a save.</summary>
public sealed class SavedVitals
{
    public int Level { get; init; } = 1;
    public int Xp { get; init; }
    public int Gold { get; init; }
    public int Channeled { get; init; }
    public float Health { get; init; } = 100f;
    public float MaxHealth { get; init; } = 100f;
    public float Prana { get; init; } = 80f;
    public float MaxPrana { get; init; } = 80f;
    public float Stamina { get; init; } = 100f;
    public float MaxStamina { get; init; } = 100f;
}
