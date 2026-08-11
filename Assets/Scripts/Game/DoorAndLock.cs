using UnityEngine;

public enum LockResult
{
    Opened,
    /// <summary>Wrong tools or not skilled enough yet. Always retryable.</summary>
    Failed,
    /// <summary>No lock to pick.</summary>
    NotLocked,
    /// <summary>A key opened it outright.</summary>
    Unlocked
}

/// <summary>
/// A door, chest or gate that may be locked.
///
/// Picking is deterministic against skill rather than a dice roll: the player is told the
/// difficulty, and either has enough Security to attempt it or does not. Morrowind's hidden
/// rolls are the single most disliked thing about its interaction, and a lock that fails
/// invisibly reads as a broken door rather than a challenge.
///
/// Failure never consumes the attempt permanently — B410 requires being caught to be
/// recoverable and never terminal, and a lock that eats your only pick is a dead end.
/// </summary>
public sealed class DoorAndLock : MonoBehaviour
{
    [SerializeField] private bool locked = true;

    /// <summary>Security needed to pick. 0–100, matching skill scale.</summary>
    [SerializeField, Range(0f, 100f)] private float difficulty = 20f;

    /// <summary>Item id that opens this outright. Empty means no key exists.</summary>
    [SerializeField] private string keyItemId = "";

    /// <summary>Picking in view of a watcher is a crime.</summary>
    [SerializeField] private bool pickingIsACrime = true;

    public bool IsLocked => locked;
    public float Difficulty => difficulty;
    public string KeyItemId => keyItemId;
    public bool IsOpen { get; private set; }

    public void Configure(bool isLocked, float lockDifficulty, string key = "")
    {
        locked = isLocked;
        difficulty = Mathf.Clamp(lockDifficulty, 0f, 100f);
        keyItemId = key ?? string.Empty;
    }

    /// <summary>
    /// Try to open. Uses a key if the player holds one, otherwise attempts a pick.
    /// </summary>
    public LockResult TryOpen()
    {
        if (!locked)
        {
            IsOpen = true;
            return LockResult.NotLocked;
        }

        var inventory = PlayerInventory.Instance;
        if (!string.IsNullOrEmpty(keyItemId) && inventory != null && inventory.CountOf(keyItemId) > 0)
        {
            locked = false;
            IsOpen = true;
            GameHud.Instance?.ShowToast("The key turns.");
            return LockResult.Unlocked;
        }

        return TryPick();
    }

    /// <summary>
    /// Attempt to pick. Success is skill against difficulty, and the player is told the gap
    /// rather than being left guessing.
    /// </summary>
    public LockResult TryPick()
    {
        if (!locked)
        {
            IsOpen = true;
            return LockResult.NotLocked;
        }

        float security = SkillSystem.Instance != null
            ? SkillSystem.Instance.LevelOf(Skills.Security)
            : 0f;

        if (security < difficulty)
        {
            GameHud.Instance?.ShowToast(
                $"This lock is beyond you — Security {Mathf.CeilToInt(difficulty)} needed.");
            return LockResult.Failed;
        }

        locked = false;
        IsOpen = true;

        if (pickingIsACrime) CrimeWitness.ReportIfSeen("picking a lock");
        SkillSystem.Instance?.ReportUse(Skills.Security, difficulty, difficulty);
        GameHud.Instance?.ShowToast("The lock gives.");
        return LockResult.Opened;
    }

    /// <summary>Re-lock, for world reset and for testing.</summary>
    public void Relock()
    {
        locked = true;
        IsOpen = false;
    }
}
