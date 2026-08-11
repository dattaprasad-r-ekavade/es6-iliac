using System.Collections.Generic;
using UnityEngine;

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
/// Sight-based detection, crouch and concealment.
///
/// Deliberately forgiving: being caught is a setback, never a dead end. The plan's VS4 gate
/// requires every mechanic to be recoverable and unable to strand the player, and the beat
/// sheet requires B410's "being caught is recoverable, never terminal".
///
/// Suspicion builds while a watcher can see the player and decays when it cannot, so breaking
/// line of sight is always the answer. There is no state this cannot fall back out of.
/// </summary>
public sealed class DetectionSystem : MonoBehaviour
{
    public static DetectionSystem Instance { get; private set; }

    /// <summary>Suspicion needed to become Suspicious, then Alerted.</summary>
    private const float SuspiciousAt = 0.35f;
    private const float AlertedAt = 1f;

    /// <summary>Per-second rates. Decay is slower than build, so escapes take commitment.</summary>
    private const float BuildRate = 0.6f;
    private const float DecayRate = 0.35f;

    /// <summary>Crouching multiplies effective visibility.</summary>
    private const float CrouchVisibility = 0.4f;

    private readonly List<DetectionWatcher> _watchers = new();

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
            float v = IsCrouching ? CrouchVisibility : 1f;
            float stealth = SkillSystem.Instance != null
                ? SkillSystem.Instance.LevelOf(Skills.Stealth) / SkillSystem.MaxSkill
                : 0f;
            // Even a master is never invisible — 40% is the floor.
            return v * Mathf.Lerp(1f, 0.4f, stealth);
        }
    }

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void SetCrouching(bool crouching) => IsCrouching = crouching;

    public void Register(DetectionWatcher watcher)
    {
        if (watcher != null && !_watchers.Contains(watcher)) _watchers.Add(watcher);
    }

    public void Unregister(DetectionWatcher watcher) => _watchers.Remove(watcher);

    /// <summary>
    /// Push suspicion up directly — used by <see cref="CrimeWitness"/> when an act is seen
    /// rather than the player merely being visible.
    /// </summary>
    public void AddSuspicion(float amount)
    {
        if (amount <= 0f) return;
        Suspicion = Mathf.Clamp01(Suspicion + amount);
    }

    /// <summary>Wipe suspicion. Used on scene transitions and after a consequence resolves.</summary>
    public void Reset()
    {
        Suspicion = 0f;
        foreach (var watcher in _watchers) if (watcher != null) watcher.ResetView();
    }

    private void Update()
    {
        if (GameStateService.Instance != null && !GameStateService.Instance.GameplayInputAllowed) return;

        bool seen = false;
        foreach (var watcher in _watchers)
        {
            if (watcher == null) continue;
            if (watcher.CanSeePlayer(Visibility)) { seen = true; break; }
        }

        var before = Awareness;
        Suspicion = Mathf.Clamp01(Suspicion + (seen ? BuildRate : -DecayRate) * Time.deltaTime);
        var after = Awareness;

        if (after == before) return;

        switch (after)
        {
            case AwarenessLevel.Suspicious:
                GameHud.Instance?.ShowToast("Someone thinks they saw something.");
                break;
            case AwarenessLevel.Alerted:
                GameHud.Instance?.ShowToast("You have been seen.");
                break;
            case AwarenessLevel.Unaware when before != AwarenessLevel.Unaware:
                GameHud.Instance?.ShowToast("The alarm dies down.");
                break;
        }
    }
}
