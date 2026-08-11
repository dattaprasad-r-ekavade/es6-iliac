using UnityEngine;

/// <summary>
/// Casting. Charge comes from crystals via <see cref="PlayerStats.SpendMana"/>, so every
/// spell here eventually spends a soul — which is the whole argument of the story the player
/// is walking through.
/// </summary>
public sealed class SpellCaster : MonoBehaviour
{
    public static SpellCaster Instance { get; private set; }

    /// <summary>The spell bound to the cast input. Defaults to fire.</summary>
    public string SelectedSpellId { get; private set; } = SpellCatalog.FireId;

    /// <summary>True while Emberlight is up.</summary>
    public bool LightActive => Time.time < _lightUntil;

    private float _lightUntil;
    private Transform _cam;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var cam = GetComponentInChildren<Camera>(true);
        _cam = cam != null ? cam.transform : transform;
    }

    public void SelectSpell(string spellId)
    {
        if (SpellCatalog.Exists(spellId)) SelectedSpellId = spellId;
    }

    /// <summary>
    /// Charge cost after skill mileage. An expert pays roughly a third of base, which is the
    /// 3–4x casts-per-crystal the economy is balanced around.
    /// </summary>
    public static float CostOf(SpellDefinition spell)
    {
        if (spell == null) return 0f;
        float multiplier = SkillSystem.Instance != null
            ? SkillSystem.Instance.CostMultiplier(spell.SkillId)
            : 1f;
        return spell.BaseCost * multiplier;
    }

    public bool Cast() => Cast(SelectedSpellId);

    /// <summary>
    /// Returns true when the spell went off. False means it was never paid for — the caller
    /// must not apply effects, and skill training must not credit it.
    /// </summary>
    public bool Cast(string spellId)
    {
        var spell = SpellCatalog.Get(spellId);
        if (spell == null) return false;

        var stats = PlayerStats.Instance;
        if (stats == null) return false;

        if (!stats.SpendMana(CostOf(spell)))
        {
            GameHud.Instance?.ShowToast("No charge, and no crystal to draw on.");
            return false;
        }

        GameSfx.Instance?.PlayMagic();
        bool landed = Apply(spell);

        // Restoration always trains — a heal that heals is a use. Destruction only trains on
        // something that can fight back, which is what stops casting at walls being practice.
        if (spell.School == SpellSchool.Restoration || landed)
            SkillSystem.Instance?.ReportUse(spell.SkillId, spell.Power, ThreatOf(spell));

        return true;
    }

    private float ThreatOf(SpellDefinition spell) => spell.Power;

    private bool Apply(SpellDefinition spell)
    {
        switch (spell.Effect)
        {
            case SpellEffect.Heal:
                PlayerStats.Instance?.Heal(spell.Power);
                GameHud.Instance?.ShowToast($"{spell.DisplayName} — restored.");
                return true;

            case SpellEffect.Light:
                _lightUntil = Time.time + spell.Duration;
                GameHud.Instance?.ShowToast($"{spell.DisplayName} — the dark pulls back.");
                return true;

            default:
                return CastAtTarget(spell);
        }
    }

    private bool CastAtTarget(SpellDefinition spell)
    {
        var origin = _cam != null ? _cam.position : transform.position + Vector3.up;
        var dir = _cam != null ? _cam.forward : transform.forward;

        if (!Physics.SphereCast(origin, 0.5f, dir, out var hit, spell.Range,
                GameLayers.CombatMask, QueryTriggerInteraction.Ignore))
            return false;

        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        if (enemy == null) return false;

        ApplyTo(enemy, spell);
        PlayerCombat.Instance?.EnterCombat();
        return true;
    }

    private void ApplyTo(EnemyBrain enemy, SpellDefinition spell)
    {
        enemy.TakeDamage(spell.Power);
        if (enemy == null) return;

        switch (spell.Effect)
        {
            case SpellEffect.Fire:
                // Damage keeps ticking after the hit — the reason fire beats groups.
                enemy.ApplyBurn(spell.Power * 0.5f, spell.Duration);
                break;

            case SpellEffect.Frost:
                enemy.ApplyChill(0.45f, spell.Duration);
                break;

            case SpellEffect.Shock:
                enemy.ApplyStagger(spell.Duration);
                ChainFrom(enemy, spell);
                break;
        }
    }

    /// <summary>Shock jumps to one nearby second target at reduced power.</summary>
    private void ChainFrom(EnemyBrain source, SpellDefinition spell)
    {
        if (source == null) return;
        const float chainRadius = 6f;

        var hits = Physics.OverlapSphere(source.transform.position, chainRadius,
            GameLayers.CombatMask, QueryTriggerInteraction.Ignore);

        foreach (var collider in hits)
        {
            var other = collider.GetComponentInParent<EnemyBrain>();
            if (other == null || other == source) continue;
            other.TakeDamage(spell.Power * 0.5f);
            if (other != null) other.ApplyStagger(spell.Duration * 0.5f);
            return; // one jump only
        }
    }
}
