using System;

namespace RatnaBay.Domain;

/// <summary>
/// What it costs to crack open a mine, and what is waiting down there.
///
/// This is the half of the loop that was missing. Banking stones did nothing: there was
/// nowhere to spend them and no deeper mine to open with them, so a player carrying
/// forty-five stones out of a cave was carrying out a number. Five playtests answered the
/// door in about a second, and the reason was never that the descent was too safe.
///
/// You spend the thing you go down to collect. That is the whole design in one sentence, and
/// it only becomes true here.
/// </summary>
public static class MineEntry
{
    /// <summary>The shallowest mine, and the only free one.</summary>
    public const int MinTier = 1;

    /// <summary>How deep the order will sell you a way into.</summary>
    public const int MaxTier = 5;

    /// <summary>
    /// Stones to open a mine of this tier.
    ///
    /// Quadratic, so each step down costs meaningfully more than the last while a tier's
    /// payout only scales linearly. That gap is what stops the deepest affordable mine from
    /// being the automatic answer: tier three costs twenty-four and pays three, six, nine and
    /// twelve for its first four rooms, so more than half a mine is spent paying for the door.
    /// </summary>
    public static int CostOf(int tier) =>
        tier <= MinTier ? 0 : 4 * Clamp(tier) * (Clamp(tier) - 1);

    /// <summary>Rooms cleared before a tier-T mine has paid for its own entry.</summary>
    public static int RoomsToBreakEven(int tier)
    {
        var cost = CostOf(tier);
        if (cost <= 0) return 0;

        // Cumulative payout of N rooms at tier T is T * N * (N + 1) / 2.
        var rooms = 0;
        var paid = 0;
        while (paid < cost && rooms < 99) paid += Clamp(tier) * ++rooms;
        return rooms;
    }

    public static int Clamp(int tier) => Math.Clamp(tier, MinTier, MaxTier);

    public static bool CanAfford(Inventory? inventory, int tier) =>
        inventory is not null && inventory.CountOf(SoulCrystals.LesserId) >= CostOf(tier);

    /// <summary>
    /// Pay the way in. Returns false and spends nothing when the stones are not there, so a
    /// refused descent can never leave a player poorer for having asked.
    /// </summary>
    public static bool TryOpen(Inventory? inventory, int tier)
    {
        if (inventory is null) return false;

        var cost = CostOf(tier);
        if (cost <= 0) return true;

        return inventory.CountOf(SoulCrystals.LesserId) >= cost
            && inventory.Consume(SoulCrystals.LesserId, cost);
    }

    /// <summary>What the order says about a given depth, for the panel at the shaft.</summary>
    public static string DescriptionOf(int tier) => Clamp(tier) switch
    {
        1 => "Worked out and shallow. Nothing down there has been dead long.",
        2 => "Past the old workings. The stones are still warm.",
        3 => "Below the water table. Bring more than one answer.",
        4 => "Nobody has brought anything back from this depth in a season.",
        _ => "The order does not write down what is at the bottom."
    };

    /// <summary>The deepest mine these stones will open.</summary>
    public static int DeepestAffordable(Inventory? inventory)
    {
        var deepest = MinTier;
        for (var tier = MinTier; tier <= MaxTier; tier++)
            if (CanAfford(inventory, tier)) deepest = tier;

        return deepest;
    }
}
