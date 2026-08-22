namespace RatnaBay.Domain;

public enum AttackResult
{
    /// <summary>Landed on something that can fight back. The only result that trains a skill.</summary>
    Hit,

    /// <summary>Swung, but nothing was there. Costs stamina and the cooldown; trains nothing.</summary>
    Missed,

    /// <summary>The previous swing has not finished.</summary>
    OnCooldown,

    /// <summary>Not enough stamina to swing.</summary>
    Exhausted
}

public readonly record struct AttackOutcome(AttackResult Result, float Damage)
{
    public bool Swung => Result is AttackResult.Hit or AttackResult.Missed;
}

/// <summary>
/// Swinging whatever is equipped.
///
/// Weapon numbers come from the equipped item via <see cref="EquipmentCatalog"/>. They used
/// to be hardcoded here, which is why the inventory was cosmetic.
/// </summary>
public sealed class PlayerCombat
{
    /// <summary>How long after the last blow the player is still considered in a fight.</summary>
    public const float CombatForgetTime = 6f;

    private readonly PlayerVitals _vitals;
    private readonly PlayerEquipment _equipment;
    private readonly SkillProgression _skills;

    private float _cooldown;
    private float _combatTimer;

    public PlayerCombat(PlayerVitals vitals, PlayerEquipment equipment, SkillProgression skills)
    {
        _vitals = vitals;
        _equipment = equipment;
        _skills = skills;
    }

    public bool InCombat { get; private set; }

    /// <summary>The weapon currently driving attacks. Falls back to unarmed.</summary>
    public WeaponDefinition ActiveWeapon => _equipment.Weapon;

    /// <summary>
    /// True while the block is held and the equipped weapon allows it. Two-handed weapons
    /// cannot block — that is the whole trade for their damage.
    /// </summary>
    public bool IsBlocking { get; private set; }

    public bool IsReady => _cooldown <= 0f;

    public event Action? CombatEntered;

    /// <summary>Raised when a fight goes quiet. Skill encounter caps reset off this.</summary>
    public event Action? CombatEnded;

    /// <summary>Set the block state. Ignored when the equipped weapon cannot block.</summary>
    public void SetBlocking(bool blocking) => IsBlocking = blocking && ActiveWeapon.CanBlock;

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        if (_cooldown > 0f) _cooldown = MathF.Max(0f, _cooldown - deltaSeconds);

        if (_combatTimer <= 0f) return;

        _combatTimer -= deltaSeconds;
        if (_combatTimer > 0f) return;

        _combatTimer = 0f;
        InCombat = false;
        // Per-encounter skill caps lift here, so one enemy cannot be farmed but the next
        // fight starts fresh.
        _skills.EndEncounter();
        CombatEnded?.Invoke();
    }

    /// <summary>
    /// Swing at whatever the game layer found under the crosshair. Pass null for a swing that
    /// hit nothing — it still costs stamina and cooldown, and it trains nothing, because gains
    /// come from effect rather than from action.
    /// </summary>
    public AttackOutcome TryAttack(IAttackable? target)
    {
        if (!IsReady) return new AttackOutcome(AttackResult.OnCooldown, 0f);

        var weapon = ActiveWeapon;
        if (!_vitals.SpendStamina(weapon.StaminaCost))
            return new AttackOutcome(AttackResult.Exhausted, 0f);

        // Attacking drops the guard, so a block cannot be held through a swing.
        SetBlocking(false);
        _cooldown = weapon.Cooldown;

        if (target is null || !target.IsAlive) return new AttackOutcome(AttackResult.Missed, 0f);

        var threat = target.MaxHealth;
        var dealt = target.TakeDamage(weapon.Damage);
        EnterCombat();

        // Advancement is use-based, so the swing trains the weapon's skill rather than paying
        // flat XP. Only landed hits get here.
        _skills.ReportUse(weapon.SkillId, dealt, threat);
        return new AttackOutcome(AttackResult.Hit, dealt);
    }

    /// <summary>Take a hit, applying worn armour and the current guard.</summary>
    public float TakeHit(float amount)
    {
        EnterCombat();
        return _vitals.TakeDamage(amount, _equipment.ArmourValue, IsBlocking);
    }

    public void EnterCombat()
    {
        var wasFighting = InCombat;
        InCombat = true;
        _combatTimer = CombatForgetTime;
        if (!wasFighting) CombatEntered?.Invoke();
    }

    public void ClearCombat()
    {
        if (!InCombat) return;
        InCombat = false;
        _combatTimer = 0f;
        _skills.EndEncounter();
        CombatEnded?.Invoke();
    }
}
