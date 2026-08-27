namespace RatnaBay.Domain;

/// <summary>
/// An enemy, as the spell layer sees it. The status effects are what make the three
/// Destruction elements mechanically different rather than recoloured damage.
/// </summary>
public interface IEnemy : IAttackable
{
}

public enum CastResult
{
    /// <summary>Paid for and applied to something.</summary>
    Landed,

    /// <summary>Paid for, but nothing was in front of the player. Trains nothing.</summary>
    Missed,

    /// <summary>No prana and no jiva stone to draw on. Never charged.</summary>
    NoCharge,

    /// <summary>The id is not a spell.</summary>
    UnknownSpell,

    /// <summary>
    /// The weapon is still in the way.
    ///
    /// A separate result from <see cref="NoCharge"/> on purpose: both refuse the cast, and
    /// they are refused for opposite reasons. One says find prana, the other says put the
    /// greatsword down — and a player told the wrong one goes looking for the wrong fix.
    /// </summary>
    Shouldering
}

public readonly record struct CastOutcome(CastResult Result, SpellDefinition? Spell, float Cost)
{
    /// <summary>True when the spell went off. False means it was never paid for.</summary>
    public bool WasCast => Result is CastResult.Landed or CastResult.Missed;
}

/// <summary>
/// Casting. Prana charge comes from jiva stones via <see cref="PlayerVitals.SpendPrana"/>.
/// Lawful stones do not contain a person, but every draw still adds to the burden measured
/// by the Stambha — the argument the player is walking through.
/// </summary>
public sealed class SpellCaster
{
    /// <summary>Burn ticks for half the spell's power per second.</summary>
    private const float BurnFactor = 0.5f;

    /// <summary>Rime takes 45% off a target's speed.</summary>
    private const float ChillFactor = 0.45f;

    /// <summary>Arc jumps to one nearby target at half power.</summary>
    private const float ChainFactor = 0.5f;

    private readonly PlayerVitals _vitals;
    private readonly SkillProgression _skills;
    private readonly LifePath _path;

    public SpellCaster(PlayerVitals vitals, SkillProgression skills, LifePath? path = null)
    {
        _vitals = vitals;
        _skills = skills;
        _path = path ?? new LifePath();
    }

    /// <summary>What a spell actually lands for, after the life path's gift.</summary>
    public float PowerOf(SpellDefinition? spell) =>
        spell is null ? 0f : spell.Power * _path.SpellMultiplier;

    /// <summary>The spell bound to the cast input. Defaults to fire.</summary>
    public string SelectedSpellId { get; private set; } = SpellCatalog.FireId;

    /// <summary>Seconds of Emberlight left. Zero when the dark has closed back in.</summary>
    public float LightRemaining { get; private set; }

    public bool LightActive => LightRemaining > 0f;

    public event Action<SpellDefinition>? SpellCast;

    public void SelectSpell(string? spellId)
    {
        if (SpellCatalog.Exists(spellId)) SelectedSpellId = spellId!;
    }

    /// <summary>
    /// Seconds until a spell can be cast, because a weapon is still being shouldered.
    ///
    /// Set by the game layer from the equipped weapon's <see cref="WeaponDefinition.CastDelaySeconds"/>
    /// when a swing happens. The countdown and the refusal live here so the rule is testable
    /// without a renderer, which is the only reason it is in the domain at all.
    /// </summary>
    public float ShoulderRemaining { get; private set; }

    public bool IsShouldering => ShoulderRemaining > 0f;

    /// <summary>Put the weapon in the way for a moment. Never shortens an existing delay.</summary>
    public void Encumber(float seconds)
    {
        if (seconds <= 0f) return;
        ShoulderRemaining = MathF.Max(ShoulderRemaining, seconds);
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        if (ShoulderRemaining > 0f)
            ShoulderRemaining = MathF.Max(0f, ShoulderRemaining - deltaSeconds);

        if (LightRemaining <= 0f) return;
        LightRemaining = MathF.Max(0f, LightRemaining - deltaSeconds);
    }

    /// <summary>
    /// Charge cost after skill mileage. An expert pays roughly a third of base, which is the
    /// 3–4x casts-per-crystal the economy is balanced around.
    /// </summary>
    public float CostOf(SpellDefinition? spell) =>
        spell is null ? 0f : spell.BaseCost * _skills.CostMultiplier(spell.SkillId);

    public CastOutcome Cast(IEnemy? target = null, IEnemy? chainTarget = null) =>
        Cast(SelectedSpellId, target, chainTarget);

    /// <summary>
    /// Cast at whatever the game layer found down the crosshair.
    /// </summary>
    /// <param name="chainTarget">
    /// The nearest other enemy, for Arc's single jump. Ignored by every other spell.
    /// </param>
    public CastOutcome Cast(string? spellId, IEnemy? target = null, IEnemy? chainTarget = null)
    {
        // Checked before paying, so a refused cast never costs prana.
        if (IsShouldering)
            return new CastOutcome(CastResult.Shouldering, SpellCatalog.Get(spellId), 0f);

        var paid = Pay(spellId);
        if (!paid.WasCast) return paid;

        var landed = Deliver(paid.Spell!, target, chainTarget);
        return new CastOutcome(landed ? CastResult.Landed : CastResult.Missed, paid.Spell, paid.Cost);
    }

    /// <summary>
    /// Charge for a spell without applying it.
    ///
    /// Split from delivery so a spell can leave the hand as a projectile and arrive a moment
    /// later: the prana is spent when the player casts, and the effect happens where the bolt
    /// lands. A returned result of <see cref="CastResult.Missed"/> means "paid for, not yet
    /// delivered" — the caller owns it from here.
    /// </summary>
    public CastOutcome Pay(string? spellId)
    {
        var spell = SpellCatalog.Get(spellId);
        if (spell is null) return new CastOutcome(CastResult.UnknownSpell, null, 0f);

        var cost = CostOf(spell);
        if (!_vitals.SpendPrana(cost)) return new CastOutcome(CastResult.NoCharge, spell, cost);

        SpellCast?.Invoke(spell);
        return new CastOutcome(CastResult.Missed, spell, cost);
    }

    /// <summary>
    /// Apply a spell that has already been paid for. Returns true when it found something.
    /// </summary>
    /// <summary>
    /// The cave the caster is currently standing in, or null above ground.
    ///
    /// Set by the game layer when a descent begins. Held here rather than passed to every call
    /// because a spell's power is the caster's business and the cave is a property of where
    /// they are, not of the individual cast.
    /// </summary>
    public CaveTheme? Cave { get; set; }

    public bool Deliver(SpellDefinition spell, IEnemy? target, IEnemy? chainTarget = null)
    {
        var landed = Apply(spell, target, chainTarget);

        // Restoration always trains — a heal that heals is a use. Destruction only trains on
        // something that can fight back, which is what stops casting at walls being practice.
        if (spell.School == SpellSchool.Restoration || landed)
            _skills.ReportUse(spell.SkillId, PowerOf(spell), PowerOf(spell));

        return landed;
    }

    /// <summary>True when this spell travels to its target rather than happening at once.</summary>
    public static bool IsProjectile(SpellDefinition spell) =>
        spell.School == SpellSchool.Destruction;

    /// <summary>Metres per second a bolt travels. Fast, but dodgeable at range.</summary>
    public const float ProjectileSpeed = 26f;

    private bool Apply(SpellDefinition spell, IEnemy? target, IEnemy? chainTarget)
    {
        var power = PowerOf(spell);

        switch (spell.Effect)
        {
            case SpellEffect.Heal:
                _vitals.Heal(power);
                return true;

            case SpellEffect.Light:
                LightRemaining = MathF.Max(LightRemaining, spell.Duration);
                return true;

            default:
                if (target is null || !target.IsAlive) return false;
                ApplyTo(target, spell, power, chainTarget);
                return true;
        }
    }

    private void ApplyTo(IEnemy enemy, SpellDefinition spell, float power,
        IEnemy? chainTarget)
    {
        // The cave's opinion of this element, applied once and to everything the spell does —
        // the hit, the burn it leaves, and the jump to a second target. Applying it only to
        // the direct damage would make Flame's burn ignore a lava cave entirely, which is the
        // half of Flame that matters.
        power *= CaveThemeCatalog.DamageFactor(Cave, spell.Effect);

        enemy.TakeDamage(power, spell.DisplayName);

        switch (spell.Effect)
        {
            case SpellEffect.Fire:
                enemy.ApplyBurn(power * BurnFactor, spell.Duration,
                    $"{spell.DisplayName} (burning)");
                break;

            case SpellEffect.Frost:
                enemy.ApplyChill(ChillFactor, spell.Duration);
                break;

            case SpellEffect.Shock:
                enemy.ApplyStagger(spell.Duration);
                // One jump only, at reduced power.
                if (chainTarget is null || ReferenceEquals(chainTarget, enemy)) break;
                chainTarget.TakeDamage(power * ChainFactor, $"{spell.DisplayName} (chained)");
                chainTarget.ApplyStagger(spell.Duration * ChainFactor);
                break;
        }
    }
}
