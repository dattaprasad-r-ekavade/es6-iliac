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

    /// <summary>Sockets, when there are any. Null in the isolated tests that predate them.</summary>
    private readonly StoneSlots? _stones;

    /// <summary>The order's permanent gains. Null in isolated tests.</summary>
    private readonly Legacy? _legacy;

    /// <summary>
    /// False until the first blow lands in the current room.
    ///
    /// Steady Hand makes that blow an opening, which is worth double. Tracked per room rather
    /// than per fight so it cannot be farmed by backing out of a doorway and re-entering — a
    /// room is entered once, and the game layer says when.
    /// </summary>
    private bool _firstBloodSpent;

    /// <summary>Called when a room is entered, so Steady Hand can arm again.</summary>
    public void EnterRoom() => _firstBloodSpent = false;

    public PlayerCombat(PlayerVitals vitals, PlayerEquipment equipment, SkillProgression skills,
        LifePath? path = null, Inventory? inventory = null, StoneSlots? stones = null,
        Legacy? legacy = null)
    {
        _vitals = vitals;
        _equipment = equipment;
        _skills = skills;
        _path = path ?? new LifePath();
        _inventory = inventory;
        _stones = stones;
        _legacy = legacy;
    }

    /// <summary>True when a stone with this effect is socketed right now.</summary>
    public bool HasStone(StoneEffect effect) => _stones?.Has(effect) ?? false;

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

        // Steady Hand: the first blow landed in a room is an opening whether or not the target
        // was actually helpless. Spent even when the target was already vulnerable, so the
        // amulet cannot be banked through an ambush and used later in the same room.
        if (!_firstBloodSpent && _legacy?.Has(AmuletEffect.FirstBlood) == true)
        {
            opening = true;
            _firstBloodSpent = true;
        }

        var damage = opening ? WeaponDamage * OpeningStrikeMultiplier : WeaponDamage;
        var dealt = target.TakeDamage(damage, ActiveWeapon.DisplayName);

        // A blunt weapon leaves the target unable to answer, which the domain already rewards:
        // the next blow lands on something vulnerable at double. That loop — stagger, then
        // strike the opening — is the mace's whole reason to exist, and it is why it does not
        // also need to bleed.
        // The weapon's own stagger, then whatever the stones add. Longest wins rather than
        // stacking, so a mace with a Thunder stone is a mace rather than a lockdown.
        var stagger = weapon.StaggerSeconds;
        if (HasStone(StoneEffect.Thunder))
            stagger = MathF.Max(stagger, StoneCatalog.ThunderSeconds);

        if (stagger > 0f) target.ApplyStagger(stagger);

        // Cinder and Rime hand melee the verbs that were previously only a spell's. That is
        // the point of them: a warrior who finds one fights the way a mage does for a run,
        // without the prana.
        if (HasStone(StoneEffect.Cinder))
            target.ApplyBurn(StoneCatalog.CinderDamagePerSecond, StoneCatalog.CinderSeconds,
                ActiveWeapon.DisplayName);

        if (HasStone(StoneEffect.Rime))
            target.ApplyChill(StoneCatalog.RimeSpeedFactor, StoneCatalog.RimeSeconds);

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

    /// <summary>
    /// True when the equipped weapon sweeps rather than stabs.
    ///
    /// A Splitting stone grants it to anything, which is the clearest stone in the set: the
    /// first swing after socketing it hits three things instead of one, and the player changes
    /// where they stand for the rest of the run.
    /// </summary>
    public bool WeaponSweeps =>
        ActiveWeapon.Class == WeaponClass.TwoHanded || HasStone(StoneEffect.Splitting);

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

    /// <summary>
    /// Set by the console's 'god'. The blow still lands, still counts and still trains, so a
    /// fight can be watched through without the watcher dying halfway.
    /// </summary>
    public bool Invulnerable { get; set; }

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

        // Reported as though it landed, because a console flag that also silences the
        // feedback would hide the very thing somebody turned it on to watch.
        if (Invulnerable) return amount;

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
