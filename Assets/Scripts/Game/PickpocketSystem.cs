using System.Collections.Generic;
using UnityEngine;

public enum PickpocketResult
{
    Taken,
    /// <summary>Not skilled enough. Always retryable.</summary>
    TooDifficult,
    /// <summary>Target has nothing left worth taking.</summary>
    NothingToTake,
    /// <summary>Taken, but somebody saw.</summary>
    Caught
}

/// <summary>A pocket worth picking. Sits alongside <see cref="NpcInteractable"/>.</summary>
public sealed class PickpocketTarget : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float difficulty = 15f;

    [System.Serializable]
    public sealed class Holding
    {
        public string Id;
        public string Name;
        public int Count = 1;
        public string Kind = "loot";
    }

    [SerializeField] private List<Holding> holdings = new();

    public float Difficulty => difficulty;
    public int RemainingItems => holdings.Count;

    public void Configure(float lockDifficulty, params Holding[] contents)
    {
        difficulty = Mathf.Clamp(lockDifficulty, 0f, 100f);
        holdings = new List<Holding>(contents ?? System.Array.Empty<Holding>());
    }

    /// <summary>Remove and return the next holding, or null when empty.</summary>
    public Holding TakeNext()
    {
        if (holdings.Count == 0) return null;
        var item = holdings[0];
        holdings.RemoveAt(0);
        return item;
    }
}

/// <summary>
/// Lifting things out of pockets.
///
/// Success is Security against difficulty, deterministically, for the same reason locks are:
/// a hidden roll that fails is indistinguishable from a broken mechanic. Being caught costs
/// suspicion, never the run — B410 requires it to be recoverable.
/// </summary>
public static class PickpocketSystem
{
    public static PickpocketResult TryTake(PickpocketTarget target)
    {
        if (target == null) return PickpocketResult.NothingToTake;
        if (target.RemainingItems == 0)
        {
            GameHud.Instance?.ShowToast("Nothing worth taking.");
            return PickpocketResult.NothingToTake;
        }

        float security = SkillSystem.Instance != null
            ? SkillSystem.Instance.LevelOf(Skills.Security)
            : 0f;

        if (security < target.Difficulty)
        {
            GameHud.Instance?.ShowToast(
                $"Their pockets are beyond you — Security {Mathf.CeilToInt(target.Difficulty)} needed.");
            return PickpocketResult.TooDifficult;
        }

        var item = target.TakeNext();
        if (item == null) return PickpocketResult.NothingToTake;

        PlayerInventory.Instance?.Add(item.Id, item.Name, item.Count, item.Kind);
        SkillSystem.Instance?.ReportUse(Skills.Security, target.Difficulty, target.Difficulty);

        // The item is kept either way. Getting caught is a consequence, not a confiscation.
        bool seen = CrimeWitness.ReportIfSeen("with your hand in a pocket");
        if (seen) return PickpocketResult.Caught;

        GameHud.Instance?.ShowToast($"Lifted {item.Name}.");
        return PickpocketResult.Taken;
    }
}
