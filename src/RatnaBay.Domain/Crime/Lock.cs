namespace RatnaBay.Domain;

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
/// invisibly reads as a broken door rather than as a challenge.
///
/// Failure never consumes the attempt permanently — being caught must be recoverable and
/// never terminal, and a lock that eats your only pick is a dead end.
/// </summary>
public sealed class Lockable
{
    public Lockable(bool locked = true, float difficulty = 20f, string keyItemId = "",
        bool pickingIsACrime = true)
    {
        IsLocked = locked;
        Difficulty = Math.Clamp(difficulty, 0f, 100f);
        KeyItemId = keyItemId ?? string.Empty;
        PickingIsACrime = pickingIsACrime;
    }

    public bool IsLocked { get; private set; }

    /// <summary>Security needed to pick. 0–100, matching the skill scale.</summary>
    public float Difficulty { get; }

    /// <summary>Item id that opens this outright. Empty means no key exists.</summary>
    public string KeyItemId { get; }

    /// <summary>Picking in view of a watcher is a crime.</summary>
    public bool PickingIsACrime { get; }

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Try to open. Uses a key if the player holds one, otherwise attempts a pick.
    /// </summary>
    public LockResult TryOpen(SkillProgression skills, Inventory inventory, Detection? detection = null)
    {
        if (!IsLocked)
        {
            IsOpen = true;
            return LockResult.NotLocked;
        }

        if (!string.IsNullOrEmpty(KeyItemId) && inventory.Has(KeyItemId))
        {
            IsLocked = false;
            IsOpen = true;
            return LockResult.Unlocked;
        }

        return TryPick(skills, detection);
    }

    /// <summary>
    /// Attempt to pick. Success is skill against difficulty, and the player is told the gap
    /// rather than being left guessing.
    /// </summary>
    public LockResult TryPick(SkillProgression skills, Detection? detection = null)
    {
        if (!IsLocked)
        {
            IsOpen = true;
            return LockResult.NotLocked;
        }

        if (skills.LevelOf(Skills.Security) < Difficulty) return LockResult.Failed;

        IsLocked = false;
        IsOpen = true;

        if (PickingIsACrime) CrimeWitness.ReportIfSeen(detection);
        skills.ReportUse(Skills.Security, Difficulty, Difficulty);
        return LockResult.Opened;
    }

    /// <summary>Re-lock, for world reset and for testing.</summary>
    public void Relock()
    {
        IsLocked = true;
        IsOpen = false;
    }

    /// <summary>Restore a lock already opened in a save without replaying a skill check.</summary>
    public void RestoreOpened()
    {
        IsLocked = false;
        IsOpen = true;
    }

    /// <summary>
    /// Swing it shut again.
    ///
    /// Used by the mine, where a door closing behind the player is the commitment the run
    /// loop is built on: once it opens you are in that room until it is clear or you are not.
    /// </summary>
    public void Shut()
    {
        IsOpen = false;
        IsLocked = false;
    }
}
