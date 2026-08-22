namespace RatnaBay.Domain;

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

/// <summary>What the attempt produced. <see cref="Item"/> is set whenever something was lifted.</summary>
public readonly record struct PickpocketOutcome(PickpocketResult Result, ItemStack? Item)
{
    public bool TookSomething => Result is PickpocketResult.Taken or PickpocketResult.Caught;
}

/// <summary>A pocket worth picking.</summary>
public sealed class PickpocketTarget
{
    private readonly List<ItemStack> _holdings = new();

    public PickpocketTarget(float difficulty, params ItemStack[] holdings)
    {
        Difficulty = Math.Clamp(difficulty, 0f, 100f);
        if (holdings is not null) _holdings.AddRange(holdings);
    }

    public float Difficulty { get; }

    public int RemainingItems => _holdings.Count;

    /// <summary>Remove and return the next holding, or null when empty.</summary>
    public ItemStack? TakeNext()
    {
        if (_holdings.Count == 0) return null;
        var item = _holdings[0];
        _holdings.RemoveAt(0);
        return item;
    }
}

/// <summary>
/// Lifting things out of pockets.
///
/// Success is Security against difficulty, deterministically, for the same reason locks are:
/// a hidden roll that fails is indistinguishable from a broken mechanic. Being caught costs
/// suspicion, never the run — the design requires it to be recoverable.
/// </summary>
public static class Pickpocketing
{
    public static PickpocketOutcome TryTake(
        PickpocketTarget? target,
        SkillProgression skills,
        Inventory inventory,
        Detection? detection)
    {
        if (target is null || target.RemainingItems == 0)
            return new PickpocketOutcome(PickpocketResult.NothingToTake, null);

        if (skills.LevelOf(Skills.Security) < target.Difficulty)
            return new PickpocketOutcome(PickpocketResult.TooDifficult, null);

        var item = target.TakeNext();
        if (item is null) return new PickpocketOutcome(PickpocketResult.NothingToTake, null);

        inventory.Add(item.Id, item.Name, item.Count, item.Kind);
        skills.ReportUse(Skills.Security, target.Difficulty, target.Difficulty);

        // The item is kept either way. Getting caught is a consequence, not a confiscation.
        var seen = CrimeWitness.ReportIfSeen(detection);
        return new PickpocketOutcome(
            seen ? PickpocketResult.Caught : PickpocketResult.Taken, item);
    }
}
