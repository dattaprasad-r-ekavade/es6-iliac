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
    Exhausted,

    /// <summary>A bow, and no arrows. Costs nothing — not the stamina, not the cooldown.</summary>
    NoAmmunition
}

public readonly record struct AttackOutcome(AttackResult Result, float Damage,
    bool WasOpening = false)
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
    private readonly LifePath _path;

    private float _cooldown;
    private float _combatTimer;

    /// <summary>
    /// The pack, so a bow can spend from it.
    ///
    /// Optional so that every existing test that builds a combat component in isolation keeps
    /// working — without one, a ranged weapon simply never runs out, which is the behaviour
    /// those tests were written against.
    /// </summary>
    private readonly Inventory? _inventory;

    public PlayerCombat(PlayerVitals vitals, PlayerEquipment equipment, SkillProgression skills,
        LifePath? path = null, Inventory? inventory = null)
    {
        _vitals = vitals;
        _equipment = equipment;
        _skills = skills;
        _path = path ?? new LifePath();
        _inventory = inventory;
    }

    /// <summary>What the equipped weapon lands for, after the life path's gift.</summary>
    public float WeaponDamage => ActiveWeapon.Damage * _path.WeaponMultiplier;

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

        // Checked before stamina, so a shot that cannot be taken costs nothing at all. The
        // opposite order would spend the breath and then refuse to loose the arrow.
        if (weapon.NeedsAmmunition && _inventory is not null
            && !_inventory.Has(EquipmentCatalog.ArrowId))
            return new AttackOutcome(AttackResult.NoAmmunition, 0f);

        if (!_vitals.SpendStamina(weapon.StaminaCost))
            return new AttackOutcome(AttackResult.Exhausted, 0f);

        // Spent on the swing, not on the hit. A missed arrow is gone, which is the whole
        // reason a bow asks the player to aim.
        if (weapon.NeedsAmmunition) _inventory?.Consume(EquipmentCatalog.ArrowId);

        // Attacking drops the guard, so a block cannot be held through a swing.
        SetBlocking(false);
        _cooldown = weapon.Cooldown;

        if (target is null || !target.IsAlive) return new AttackOutcome(AttackResult.Missed, 0f);

        var threat = target.MaxHealth;

        // Twice as hard on something that cannot answer yet.
        //
        // Enough to change where a player stands. A room's five occupants rise one after
        // another over about two seconds, so rushing in buys three or four of these — which is
        // the reward for being in the room rather than waiting in the doorway, and it is what
        // the parked stealth pillar was really for.
        var opening = target.IsVulnerable;
        var damage = opening ? WeaponDamage * OpeningStrikeMultiplier : WeaponDamage;
        var dealt = target.TakeDamage(damage, ActiveWeapon.DisplayName);

        // A blunt weapon leaves the target unable to answer, which the domain already rewards:
        // the next blow lands on something vulnerable at double. That loop — stagger, then
        // strike the opening — is the mace's whole reason to exist, and it is why it does not
        // also need to bleed.
        if (weapon.StaggerSeconds > 0f) target.ApplyStagger(weapon.StaggerSeconds);

        EnterCombat();

        // Advancement is use-based, so the swing trains the weapon's skill rather than paying
        // flat XP. Only landed hits get here.
        _skills.ReportUse(weapon.SkillId, dealt, threat);
        return new AttackOutcome(AttackResult.Hit, dealt, opening);
    }

    /// <summary>What a blow on something helpless is worth.</summary>
    public const float OpeningStrikeMultiplier = 2f;

    /// <summary>What everything else in the arc takes from a two-handed sweep.</summary>
    public const float CleaveFactor = 0.6f;

    /// <summary>True when the equipped weapon sweeps rather than stabs.</summary>
    public bool WeaponSweeps => ActiveWeapon.Class == WeaponClass.TwoHanded;

    /// <summary>
    /// Carry the swing through everything else in the arc.
    ///
    /// The only thing that makes a greatsword a different weapon. A sword deals eighteen every
    /// 0.45 seconds and a greatsword thirty-four every 0.85 — the same damage a second — so
    /// with one target in front of you the choice has never mattered. Against the five bodies
    /// that rise when a room is entered it decides the fight, and it is paid for in stamina, in
    /// speed, and in not being able to guard.
    ///
    /// The primary target is the caller's business; this is everything after it.
    /// </summary>
    public int Sweep(IEnumerable<IAttackable>? others)
    {
        if (others is null || !WeaponSweeps) return 0;

        var struck = 0;
        foreach (var other in others)
        {
            if (other is null || !other.IsAlive) continue;

            var damage = WeaponDamage * CleaveFactor;
            if (other.IsVulnerable) damage *= OpeningStrikeMultiplier;

            other.TakeDamage(damage, ActiveWeapon.DisplayName);
            struck++;
        }

        return struck;
    }

    /// <summary>Take a hit, applying worn armour and the current guard.</summary>
    public float TakeHit(float amount)
    {
        EnterCombat();

        // Guarding trains Block.
        //
        // It never did: Skills.Block existed in the list of skills and appeared nowhere else
        // in the codebase, so a player could hold their guard for a hundred fights and the
        // number beside it stayed where it started. Block is the one defensive verb there is,
        // and it is used in every fight — a skill nothing trains is worse than no skill,
        // because it looks like progress that is not happening.
        if (IsBlocking && amount > 0f)
            _skills.ReportUse(Skills.Block, amount * (1f - _equipment.BlockFactor), amount);

        return _vitals.TakeDamage(amount, _equipment.ArmourValue,
            IsBlocking ? _equipment.BlockFactor : 1f);
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
