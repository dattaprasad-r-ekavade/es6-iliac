namespace RatnaBay.Domain;

/// <summary>One skill's value, as written to a save.</summary>
public sealed class SavedSkill
{
    public required string Id { get; init; }
    public required float Value { get; init; }
}

/// <summary>
/// Eight use-based skills.
///
/// Skills grow by doing, and character level derives from total skill progress rather than
/// from a separate XP track. Morrowind's system aged badly for specific and fixable reasons,
/// so the five anti-grind rules are implemented here and are not optional:
///
/// 1. Gains come from effect, not action. Callers only report *landed* uses — swinging at
///    air and casting at walls report nothing. Enforced at the call sites.
/// 2. Gains scale with threat. A trivial target trains nothing once you are good.
/// 3. Diminishing returns per encounter, so one enemy cannot be farmed.
/// 4. No attribute multipliers on level-up. There are no attributes to multiply.
/// 5. Magic is self-limiting, because casting costs crystals which cost gold.
/// </summary>
public sealed class SkillProgression
{
    public const float MaxSkill = 100f;

    /// <summary>Total skill points needed per character level.</summary>
    private const float PointsPerLevel = 40f;

    /// <summary>Ceiling on how much one skill can gain within a single fight.</summary>
    private const float EncounterGainCap = 6f;

    /// <summary>Head start granted in each of a route's two skills at assignment.</summary>
    public const float RouteGrant = 10f;

    private readonly Dictionary<string, float> _levels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _encounterGain = new(StringComparer.Ordinal);

    private float _creditedLevelPoints;

    public SkillProgression()
    {
        foreach (var id in Skills.All) _levels[id] = 0f;
    }

    /// <summary>Raised when a skill's whole-number level goes up. Carries the new level.</summary>
    public event Action<string, int>? SkillRaised;

    /// <summary>Raised once per character level earned from total skill progress.</summary>
    public event Action? CharacterLevelGained;

    /// <summary>Raised when levels change wholesale — a route grant or a load.</summary>
    public event Action? Reset;

    public float LevelOf(string? skillId) =>
        !string.IsNullOrEmpty(skillId) && _levels.TryGetValue(skillId, out var v) ? v : 0f;

    /// <summary>Whole-number skill level, for display.</summary>
    public int DisplayLevelOf(string? skillId) => (int)MathF.Floor(LevelOf(skillId));

    public float TotalPoints
    {
        get
        {
            var total = 0f;
            foreach (var value in _levels.Values) total += value;
            return total;
        }
    }

    /// <summary>
    /// Charge cost scaling for a magic school. At mastery a spell costs 30% of base, which is
    /// the 3–4x casts-per-crystal the economy is balanced around.
    /// </summary>
    public float CostMultiplier(string? skillId) =>
        MathUtil.Lerp(1f, SoulCrystals.MinCostMultiplier, LevelOf(skillId) / MaxSkill);

    /// <summary>
    /// Report a landed, meaningful use.
    /// </summary>
    /// <param name="skillId">The skill exercised.</param>
    /// <param name="magnitude">How much was done — damage dealt, or amount healed.</param>
    /// <param name="threat">
    /// How dangerous the target was. Enemy max health for combat. This is what makes the
    /// fortieth identical bandit worthless.
    /// </param>
    public void ReportUse(string? skillId, float magnitude, float threat)
    {
        if (string.IsNullOrEmpty(skillId) || !_levels.ContainsKey(skillId)) return;
        if (magnitude <= 0f) return;

        var current = _levels[skillId];
        if (current >= MaxSkill) return;

        // Rule 2: a target must be dangerous relative to how good you already are.
        var relevance = MathUtil.Clamp01(threat / (10f + current * 1.5f));
        if (relevance <= 0.01f) return;

        // Rule 3: diminishing returns within one encounter.
        _encounterGain.TryGetValue(skillId, out var already);
        var headroom = MathUtil.Clamp01(1f - already / EncounterGainCap);
        if (headroom <= 0f) return;

        var gain = 0.35f * relevance * headroom;
        if (gain <= 0f) return;

        _levels[skillId] = MathF.Min(MaxSkill, current + gain);
        _encounterGain[skillId] = already + gain;

        var before = (int)MathF.Floor(current);
        var after = DisplayLevelOf(skillId);
        if (after > before) SkillRaised?.Invoke(skillId, after);

        CreditCharacterLevel();
    }

    /// <summary>
    /// Call when a fight ends. Per-encounter caps reset here rather than on a timer —
    /// otherwise waiting in place next to one enemy is a grind.
    /// </summary>
    public void EndEncounter() => _encounterGain.Clear();

    /// <summary>
    /// Character level derives from total skill progress rather than a separate XP track.
    /// </summary>
    private void CreditCharacterLevel()
    {
        while (TotalPoints - _creditedLevelPoints >= PointsPerLevel)
        {
            _creditedLevelPoints += PointsPerLevel;
            CharacterLevelGained?.Invoke();
        }
    }

    /// <summary>
    /// Route assignment grants a head start in that route's two skills. `route.refuse` grants
    /// nothing — the fastest route gives the least, which is its continuing price.
    /// </summary>
    public void GrantRouteSkills(string? routeId, float amount = RouteGrant)
    {
        foreach (var skillId in Skills.GrantedBy(routeId))
        {
            if (!_levels.ContainsKey(skillId)) continue;
            // Never a demotion: a grant can only raise a skill the player already trained.
            _levels[skillId] = MathF.Min(MaxSkill, MathF.Max(_levels[skillId], amount));
        }

        Reset?.Invoke();
    }

    public IReadOnlyList<SavedSkill> Capture()
    {
        var list = new List<SavedSkill>();
        foreach (var id in Skills.All) list.Add(new SavedSkill { Id = id, Value = _levels[id] });
        return list;
    }

    public void Restore(IEnumerable<SavedSkill>? saved)
    {
        foreach (var id in Skills.All) _levels[id] = 0f;
        _encounterGain.Clear();

        if (saved is not null)
            foreach (var entry in saved)
                if (entry is not null && _levels.ContainsKey(entry.Id))
                    _levels[entry.Id] = Math.Clamp(entry.Value, 0f, MaxSkill);

        // Level was already granted when those points were first earned; re-crediting here
        // would hand out a free level on every load.
        _creditedLevelPoints = MathF.Floor(TotalPoints / PointsPerLevel) * PointsPerLevel;

        Reset?.Invoke();
    }
}
