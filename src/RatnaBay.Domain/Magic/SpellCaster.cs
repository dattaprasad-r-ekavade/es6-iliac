namespace RatnaBay.Domain;

/// <summary>
/// An enemy, as the spell layer sees it. The status effects are what make the three
/// Destruction elements mechanically different rather than recoloured damage.
/// </summary>
public interface IEnemy : IAttackable
{
    /// <summary>Damage that keeps ticking after the hit — the reason fire beats groups.</summary>
    void ApplyBurn(float damagePerSecond, float duration);

    /// <summary>Slow. Beats chargers.</summary>
    void ApplyChill(float slowFactor, float duration);

    /// <summary>Interrupt. Beats anything mid-action.</summary>
    void ApplyStagger(float duration);
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
    UnknownSpell
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

    public SpellCaster(PlayerVitals vitals, SkillProgression skills)
    {
        _vitals = vitals;
        _skills = skills;
    }

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

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || LightRemaining <= 0f) return;
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
        var spell = SpellCatalog.Get(spellId);
        if (spell is null) return new CastOutcome(CastResult.UnknownSpell, null, 0f);

        var cost = CostOf(spell);
        if (!_vitals.SpendPrana(cost)) return new CastOutcome(CastResult.NoCharge, spell, cost);

        SpellCast?.Invoke(spell);
        var landed = Apply(spell, target, chainTarget);

        // Restoration always trains — a heal that heals is a use. Destruction only trains on
        // something that can fight back, which is what stops casting at walls being practice.
        if (spell.School == SpellSchool.Restoration || landed)
            _skills.ReportUse(spell.SkillId, spell.Power, spell.Power);

        return new CastOutcome(landed ? CastResult.Landed : CastResult.Missed, spell, cost);
    }

    private bool Apply(SpellDefinition spell, IEnemy? target, IEnemy? chainTarget)
    {
        switch (spell.Effect)
        {
            case SpellEffect.Heal:
                _vitals.Heal(spell.Power);
                return true;

            case SpellEffect.Light:
                LightRemaining = MathF.Max(LightRemaining, spell.Duration);
                return true;

            default:
                if (target is null || !target.IsAlive) return false;
                ApplyTo(target, spell, chainTarget);
                return true;
        }
    }

    private static void ApplyTo(IEnemy enemy, SpellDefinition spell, IEnemy? chainTarget)
    {
        enemy.TakeDamage(spell.Power);

        switch (spell.Effect)
        {
            case SpellEffect.Fire:
                enemy.ApplyBurn(spell.Power * BurnFactor, spell.Duration);
                break;

            case SpellEffect.Frost:
                enemy.ApplyChill(ChillFactor, spell.Duration);
                break;

            case SpellEffect.Shock:
                enemy.ApplyStagger(spell.Duration);
                // One jump only, at reduced power.
                if (chainTarget is null || ReferenceEquals(chainTarget, enemy)) break;
                chainTarget.TakeDamage(spell.Power * ChainFactor);
                chainTarget.ApplyStagger(spell.Duration * ChainFactor);
                break;
        }
    }
}
