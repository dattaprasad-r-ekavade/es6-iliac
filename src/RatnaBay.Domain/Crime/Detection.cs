namespace RatnaBay.Domain;

public enum AwarenessLevel
{
    /// <summary>Nothing seen. The player is free.</summary>
    Unaware,

    /// <summary>Something was noticed. Recoverable by breaking line of sight.</summary>
    Suspicious,

    /// <summary>Found. The consequence fires.</summary>
    Alerted
}

/// <summary>
/// Something that can notice the player — a guard, a watchpost, a patrolling clerk.
///
/// The domain owns when suspicion rises; the game layer owns the geometry that answers
/// <see cref="CanSeePlayer"/> (view cone, range, and a raycast against sight blockers).
/// Sight only. Hearing is deliberately not modelled: it is the part of stealth players find
/// unreadable, and a cone plus a raycast is legible enough to teach without a tutorial.
/// </summary>
public interface IWatcher
{
    /// <param name="visibility">
    /// 0–1 from <see cref="Detection.Visibility"/>. Crouching and stealth skill shrink the
    /// effective range and cone rather than adding a hidden roll — the player can see why
    /// they were spotted.
    /// </param>
    bool CanSeePlayer(float visibility);

    /// <summary>Reset any per-watcher memory.</summary>
    void ResetView();
}

/// <summary>
/// Sight-based detection, crouch and concealment.
///
/// Deliberately forgiving: being caught is a setback, never a dead end. Every mechanic must
/// be recoverable and unable to strand the player.
///
/// Suspicion builds while a watcher can see the player and decays when it cannot, so breaking
/// line of sight is always the answer. There is no state this cannot fall back out of.
/// </summary>
public sealed class Detection
{
    /// <summary>Suspicion needed to become Suspicious, then Alerted.</summary>
    public const float SuspiciousAt = 0.35f;
    public const float AlertedAt = 1f;

    /// <summary>Per-second rates. Decay is slower than build, so escapes take commitment.</summary>
    private const float BuildRate = 0.6f;
    private const float DecayRate = 0.35f;

    /// <summary>Crouching multiplies effective visibility.</summary>
    private const float CrouchVisibility = 0.4f;

    /// <summary>Even a master is never invisible — this is the floor on the stealth discount.</summary>
    private const float MasteryVisibility = 0.4f;

    private readonly List<IWatcher> _watchers = new();
    private readonly SkillProgression _skills;

    public Detection(SkillProgression skills) => _skills = skills;

    /// <summary>Raised when the player crosses into a different awareness level.</summary>
    public event Action<AwarenessLevel>? AwarenessChanged;

    public bool IsCrouching { get; private set; }
    public float Suspicion { get; private set; }

    public AwarenessLevel Awareness => Suspicion >= AlertedAt ? AwarenessLevel.Alerted
        : Suspicion >= SuspiciousAt ? AwarenessLevel.Suspicious
        : AwarenessLevel.Unaware;

    /// <summary>Visibility 0–1 after crouch and skill. Lower is harder to see.</summary>
    public float Visibility
    {
        get
        {
            var v = IsCrouching ? CrouchVisibility : 1f;
            var stealth = _skills.LevelOf(Skills.Stealth) / SkillProgression.MaxSkill;
            return v * MathUtil.Lerp(1f, MasteryVisibility, stealth);
        }
    }

    public void SetCrouching(bool crouching) => IsCrouching = crouching;

    public void Register(IWatcher? watcher)
    {
        if (watcher is not null && !_watchers.Contains(watcher)) _watchers.Add(watcher);
    }

    public void Unregister(IWatcher? watcher)
    {
        if (watcher is not null) _watchers.Remove(watcher);
    }

    /// <summary>
    /// Push suspicion up directly — used by <see cref="CrimeWitness"/> when an act is seen
    /// rather than the player merely being visible.
    /// </summary>
    public void AddSuspicion(float amount)
    {
        if (amount <= 0f) return;
        ApplySuspicion(MathUtil.Clamp01(Suspicion + amount));
    }

    /// <summary>Wipe suspicion. Used on scene transitions and after a consequence resolves.</summary>
    public void Clear()
    {
        foreach (var watcher in _watchers) watcher.ResetView();
        ApplySuspicion(0f);
    }

    /// <summary>
    /// Advance detection by <paramref name="deltaSeconds"/>. The caller decides when time
    /// passes, so a paused game or a blocking dialogue simply stops calling this.
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        var visibility = Visibility;
        var seen = false;
        foreach (var watcher in _watchers)
        {
            if (!watcher.CanSeePlayer(visibility)) continue;
            seen = true;
            break;
        }

        var rate = seen ? BuildRate : -DecayRate;
        ApplySuspicion(MathUtil.Clamp01(Suspicion + rate * deltaSeconds));
    }

    private void ApplySuspicion(float value)
    {
        var before = Awareness;
        Suspicion = value;
        var after = Awareness;
        if (after != before) AwarenessChanged?.Invoke(after);
    }
}
