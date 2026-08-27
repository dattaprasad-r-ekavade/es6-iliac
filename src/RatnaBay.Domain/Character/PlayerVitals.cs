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

    /// <summary>
    /// Prana recovered per second while out of a fight.
    ///
    /// Deliberately slow, and deliberately never during combat. At this rate a full reserve
    /// takes a hundred seconds of walking, so a jiva stone is still the only way to pay for
    /// a spell in the middle of a fight — the scarcity the setting rests on survives, while
    /// a player who has run dry is no longer stuck with an unusable school forever.
    /// </summary>
    public const float RestPranaRegen = 0.8f;

    /// <summary>
    /// Halved, because levels were outgrowing the mines.
    ///
    /// Max health compounds across successors — a rank once held is never lost — and a
    /// recorded run reached 220 against enemies tuned for 100. Twelve a level made tier one
    /// permanently solved. Difficulty is the tier's job now that tiers can be bought; a level
    /// should widen what you can do, not quietly remove the need to do it.
    /// </summary>
    private const float HealthPerLevel = 6f;
    private const float PranaPerLevel = 8f;
    private const float StaminaPerLevel = 8f;

    private readonly Inventory _inventory;

    /// <summary>The order's permanent gains, which can change how fast wind returns.</summary>
    private readonly Legacy? _legacy;

    public PlayerVitals(Inventory inventory, Legacy? legacy = null)
    {
        _inventory = inventory;
        _legacy = legacy;
    }

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

    /// <summary>
    /// Stamina recovers in and out of a fight; prana only recovers out of one, and slowly.
    /// </summary>
    public void Tick(float deltaSeconds, bool inCombat)
    {
        if (deltaSeconds <= 0f || !IsAlive) return;

        var beforeStamina = Stamina;
        var beforePrana = Prana;

        // Combat regen used to be zero, which gave twelve swings and then six seconds of
        // standing there unable to attack. Reduced, not absent.
        var staminaRegen = inCombat ? CombatStaminaRegen : RestStaminaRegen;

        // Second Breath only helps out of combat, deliberately. Speeding recovery mid-fight
        // would remove the reason stamina exists; speeding it between fights shortens the
        // standing around, which is the part nobody was enjoying.
        if (!inCombat && _legacy?.Has(AmuletEffect.SecondBreath) == true)
            staminaRegen *= AmuletCatalog.SecondBreathFactor;
        Stamina = MathF.Min(MaxStamina, Stamina + staminaRegen * deltaSeconds);

        if (!inCombat)
            Prana = MathF.Min(MaxPrana, Prana + RestPranaRegen * deltaSeconds);

        if (Stamina != beforeStamina || Prana != beforePrana) Changed?.Invoke();
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

            // Levelling raises the ceiling and heals nothing.
            //
            // Health used to be set to the new maximum here, which meant a kill that levelled
            // you up was a full heal in the middle of a fight — and in a press-your-luck run
            // that is fatal to the whole mechanic. A recorded session showed health going from
            // 72 to 112 on a kill, twice in under two minutes; the player was never in danger
            // and so pressed on at every door without reading the panel. Attrition across
            // rooms is the pressure the camp decision is built on, and this quietly removed it.
            //
            // This is the same rule prana already followed: raise the ceiling, hand out
            // nothing. Stamina refills itself in seconds, so it needs no help either.
            LevelGained?.Invoke(Level);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Take a hit. Worn armour is flat reduction and blocking halves what gets through —
    /// armour has no skill of its own because Block is the active defensive verb.
    /// </summary>
    public float TakeDamage(float amount, float armour = 0f, bool blocking = false) =>
        TakeDamage(amount, armour, blocking ? DamageMath.BlockReduction : 1f);

    /// <summary>The same, with the guard's quality passed in rather than assumed.</summary>
    public float TakeDamage(float amount, float armour, float blockFactor)
    {
        if (!IsAlive) return 0f;

        var incoming = DamageMath.Resolve(amount, armour, blockFactor);
        Health = MathF.Max(0f, Health - incoming);
        Changed?.Invoke();

        if (!IsAlive) Died?.Invoke();
        return incoming;
    }

    /// <summary>
    /// Wipe progress toward the next level, keeping the level itself.
    ///
    /// The successor is trained to the standard the order has reached but not yet promoted.
    /// It is the only death cost that cannot produce a wall: a player who dies repeatedly
    /// stops advancing, and never goes backwards past a rank they have held.
    /// </summary>
    public void ClearUnspentXp()
    {
        if (Xp == 0) return;

        Xp = 0;
        Changed?.Invoke();
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
    /// Give charge back, capped at the reserve. Never announced as a drawn stone, because
    /// nothing was spent to produce it.
    /// </summary>
    public void RestorePrana(float amount)
    {
        if (amount <= 0f || Prana >= MaxPrana) return;

        Prana = MathF.Min(MaxPrana, Prana + amount);
        Changed?.Invoke();
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
