using System;
using System.Collections.Generic;

namespace RatnaBay.Domain;

/// <summary>One thing a camp trader will part with, priced in stones.</summary>
public sealed record CampGood(string ItemId, string Name, string Kind, int Stones, int Count = 1);

/// <summary>
/// The trader you whistle down a cleared shaft.
///
/// Lore first, and the mechanic falls out of it: the order clears the ground and the miners and
/// traders follow the cleared ground. Somebody will always walk into a mine that has just been
/// made safe, because that is where the stones are.
///
/// Calling one costs stones, and the price rises with the depth and with every call already
/// made this descent. That is the whole balance: the pot is the budget, so every stone spent
/// on help is a stone not carried out, and the second trader is dearer than the first.
///
/// One rule holds the press-your-luck mechanic together, and it is not negotiable: a camp
/// trader deals only in what is spent before the run ends. Permanent gear stays at the surface
/// stall. If at-risk stones could be turned into something that survives death, pressing on
/// would become strictly safer — the pot would be launderable, and the decision the whole game
/// is built on would quietly stop being a decision.
/// </summary>
public static class CampTrader
{
    /// <summary>
    /// Stones to call one down, given the tier and how many have already come.
    ///
    /// Five a call at tier one, doubling at tier two, and each summons costing another five on
    /// top: 5, 10, 15 at the shallowest and 15, 30, 45 three tiers down. Deliberately legible —
    /// a player should be able to predict the next price without being told it.
    /// </summary>
    public static int CostToCall(int tier, int alreadyCalled) =>
        5 * Math.Clamp(tier, MineEntry.MinTier, MineEntry.MaxTier)
          * (Math.Max(0, alreadyCalled) + 1);

    /// <summary>
    /// What a cleared room's leavings fetch.
    ///
    /// Loot has been dropping since the first bandit died and has never had a use — no shop in
    /// the game buys anything. This is what it was always for: a pack full of satchels is what
    /// makes calling a trader pay for itself, so the summons is a judgement about what you are
    /// carrying rather than a toll.
    /// </summary>
    public const int StonesPerLoot = 1;

    /// <summary>Item kinds the trader will take off your hands.</summary>
    public static bool Buys(string? kind) =>
        string.Equals(kind, "loot", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What they carry. Consumables only, and never anything that outlives the descent.
    ///
    /// Prana is deliberately absent: the pot is jiva stones, so selling stones for stones is a
    /// circle. Prana is what the stones in your pack are already for.
    /// </summary>
    public static IReadOnlyList<CampGood> Stock { get; } = new[]
    {
        new CampGood("health_potion", "Health Potion", "potion", Stones: 3),
        new CampGood("health_potion", "Two Health Potions", "potion", Stones: 5, Count: 2),
        new CampGood("torch_bundle", "Torch Bundle", "misc", Stones: 1, Count: 2)
    };

    /// <summary>Everything in the pack this trader would take, and what it is worth.</summary>
    public static (int Items, int Stones) ValueOfLoot(Inventory? inventory)
    {
        if (inventory is null) return (0, 0);

        var items = 0;
        foreach (var stack in inventory.Items)
            if (Buys(stack.Kind)) items += stack.Count;

        return (items, items * StonesPerLoot);
    }

    /// <summary>
    /// Hand over everything the trader buys, and add what it fetched to the pot.
    ///
    /// Into the pot rather than the pack, because down here the pot is the purse: what is sold
    /// at a camp is at risk exactly like everything else earned on this descent.
    /// </summary>
    public static int SellLoot(Inventory? inventory, RunState? run)
    {
        if (inventory is null || run is null || !run.IsActive) return 0;

        var (items, stones) = ValueOfLoot(inventory);
        if (items <= 0) return 0;

        foreach (var stack in new List<ItemStack>(inventory.Items))
            if (Buys(stack.Kind)) inventory.Consume(stack.Id, stack.Count);

        run.Collect(stones);
        return stones;
    }
}
