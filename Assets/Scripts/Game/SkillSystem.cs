using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SavedSkill
{
    public string Id;
    public float Value;
}

/// <summary>
/// Eight use-based skills.
///
/// Skills grow by doing, and character level derives from total skill progress rather than
/// from a separate XP track. Morrowind's system aged badly for specific and fixable reasons,
/// so the five anti-grind rules from Docs/GAMEPLAY_DESIGN.md are implemented here and are
/// not optional:
///
/// 1. Gains come from effect, not action. Callers only report *landed* uses — swinging at
///    air and casting at walls report nothing. Enforced at the call sites.
/// 2. Gains scale with threat. A trivial target trains nothing once you are good.
/// 3. Diminishing returns per encounter, so one enemy cannot be farmed.
/// 4. No attribute multipliers on level-up. There are no attributes to multiply.
/// 5. Magic is self-limiting, because casting costs crystals which cost gold.
/// </summary>
public sealed class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    public const float MaxSkill = 100f;

    /// <summary>Total skill points needed per character level.</summary>
    private const float PointsPerLevel = 40f;

    /// <summary>Ceiling on how much one skill can gain within a single fight.</summary>
    private const float EncounterGainCap = 6f;

    private readonly Dictionary<string, float> _levels = new();
    private readonly Dictionary<string, float> _encounterGain = new();

    private float _creditedLevelPoints;
    private bool _wasInCombat;

    public event Action<string, float> OnSkillRaised;

    private void Awake()
    {
        Instance = this;
        foreach (var id in Skills.All) _levels[id] = 0f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        // Per-encounter caps reset when the fight ends, not on a timer — otherwise waiting
        // in place next to one enemy is a grind.
        bool inCombat = PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat;
        if (_wasInCombat && !inCombat) _encounterGain.Clear();
        _wasInCombat = inCombat;
    }

    public float LevelOf(string skillId) =>
        !string.IsNullOrEmpty(skillId) && _levels.TryGetValue(skillId, out var v) ? v : 0f;

    /// <summary>Whole-number skill level, for display.</summary>
    public int DisplayLevelOf(string skillId) => Mathf.FloorToInt(LevelOf(skillId));

    public float TotalPoints
    {
        get
        {
            float total = 0f;
            foreach (var kv in _levels) total += kv.Value;
            return total;
        }
    }

    /// <summary>
    /// Charge cost scaling for a magic school. At mastery a spell costs 30% of base, which is
    /// the 3–4x casts-per-crystal the economy is balanced around.
    /// </summary>
    public float CostMultiplier(string skillId)
    {
        float t = Mathf.Clamp01(LevelOf(skillId) / MaxSkill);
        return Mathf.Lerp(1f, SoulCrystals.MinCostMultiplier, t);
    }

    /// <summary>
    /// Report a landed, meaningful use.
    /// </summary>
    /// <param name="skillId">The skill exercised.</param>
    /// <param name="magnitude">How much was done — damage dealt, or amount healed.</param>
    /// <param name="threat">
    /// How dangerous the target was. Enemy max health for combat. This is what makes the
    /// fortieth identical bandit worthless.
    /// </param>
    public void ReportUse(string skillId, float magnitude, float threat)
    {
        if (string.IsNullOrEmpty(skillId) || !_levels.ContainsKey(skillId)) return;
        if (magnitude <= 0f) return;

        float current = _levels[skillId];
        if (current >= MaxSkill) return;

        // Rule 2: a target must be dangerous relative to how good you already are.
        float relevance = Mathf.Clamp01(threat / (10f + current * 1.5f));
        if (relevance <= 0.01f) return;

        // Rule 3: diminishing returns within one encounter.
        _encounterGain.TryGetValue(skillId, out float already);
        float headroom = Mathf.Clamp01(1f - already / EncounterGainCap);
        if (headroom <= 0f) return;

        float gain = 0.35f * relevance * headroom;
        if (gain <= 0f) return;

        _levels[skillId] = Mathf.Min(MaxSkill, current + gain);
        _encounterGain[skillId] = already + gain;

        int before = Mathf.FloorToInt(current);
        int after = DisplayLevelOf(skillId);
        if (after > before)
        {
            OnSkillRaised?.Invoke(skillId, after);
            GameHud.Instance?.ShowToast($"{Skills.Label(skillId)} increased to {after}.");
        }

        CreditCharacterLevel();
    }

    /// <summary>
    /// Character level derives from total skill progress. PlayerStats keeps its existing
    /// level-up behaviour; only the source of advancement changed.
    /// </summary>
    private void CreditCharacterLevel()
    {
        var stats = PlayerStats.Instance;
        if (stats == null) return;

        float earned = TotalPoints - _creditedLevelPoints;
        while (earned >= PointsPerLevel)
        {
            _creditedLevelPoints += PointsPerLevel;
            earned -= PointsPerLevel;
            stats.AddXp(stats.XpToLevel);
        }
    }

    /// <summary>
    /// Route assignment grants a head start in that route's two skills. `route.refuse` grants
    /// nothing — the fastest route gives the least, which is its continuing price.
    /// </summary>
    public void GrantRouteSkills(string routeId, float amount = 10f)
    {
        foreach (var skillId in Skills.GrantedBy(routeId))
        {
            if (!_levels.ContainsKey(skillId)) continue;
            _levels[skillId] = Mathf.Min(MaxSkill, Mathf.Max(_levels[skillId], amount));
        }
        OnSkillRaised?.Invoke(null, 0f);
    }

    public List<SavedSkill> Capture()
    {
        var list = new List<SavedSkill>();
        foreach (var kv in _levels) list.Add(new SavedSkill { Id = kv.Key, Value = kv.Value });
        return list;
    }

    public void Restore(List<SavedSkill> saved)
    {
        foreach (var id in Skills.All) _levels[id] = 0f;
        _encounterGain.Clear();

        if (saved != null)
            foreach (var entry in saved)
                if (entry != null && !string.IsNullOrEmpty(entry.Id) && _levels.ContainsKey(entry.Id))
                    _levels[entry.Id] = Mathf.Clamp(entry.Value, 0f, MaxSkill);

        // Level was already granted when those points were first earned; re-crediting here
        // would hand out a free level on every load.
        _creditedLevelPoints = Mathf.Floor(TotalPoints / PointsPerLevel) * PointsPerLevel;
    }
}
