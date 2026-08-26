namespace RatnaBay.Domain;

public sealed class ShopItemDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public int Price { get; init; }
    public int Count { get; init; } = 1;
}

public sealed class ShopDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<ShopItemDefinition> Items { get; init; } = Array.Empty<ShopItemDefinition>();
}

public enum ShopPurchaseResult
{
    Bought,
    TooExpensive,
    SoldOut,
    InvalidItem
}

/// <summary>A small deterministic shop; stock is authored and each purchase is explicit.</summary>
public sealed class Shop
{
    private readonly ShopDefinition _definition;
    private readonly HashSet<string> _soldOut = new(StringComparer.Ordinal);

    public Shop(ShopDefinition definition) => _definition = definition;

    public ShopDefinition Definition => _definition;

    /// <summary>What a given path is charged for an item on the shelf.</summary>
    public int PriceFor(ShopItemDefinition item, LifePath? path) =>
        path?.PriceOf(item.Price) ?? item.Price;

    public ShopPurchaseResult Buy(int index, PlayerVitals vitals, Inventory inventory,
        out ShopItemDefinition? item, LifePath? path = null)
    {
        item = index >= 0 && index < _definition.Items.Count ? _definition.Items[index] : null;
        if (item is null) return ShopPurchaseResult.InvalidItem;
        if (_soldOut.Contains(item.Id)) return ShopPurchaseResult.SoldOut;
        if (!vitals.SpendGold(PriceFor(item, path))) return ShopPurchaseResult.TooExpensive;

        inventory.Add(item.Id, item.Name, item.Count, item.Kind);

        // Consumables never sell out. Arrows, potions and jiva stones are the things a player
        // comes back for, and a stall that sells one potion per save is a stall that stops
        // being a shop after the first visit.
        if (!IsConsumable(item)) _soldOut.Add(item.Id);

        return ShopPurchaseResult.Bought;
    }

    /// <summary>
    /// Restocked between descents.
    ///
    /// Gear is one to a shelf while the player is in town, so buying the steel sword is a
    /// decision and not a shopping list. But death takes half the pack, and a stall that sold
    /// out permanently would mean a player who lost a weapon could never replace it — a
    /// dead end reachable only by the players already having the worst time.
    /// </summary>
    public void Restock()
    {
        if (_soldOut.Count == 0) return;
        _soldOut.Clear();
    }

    /// <summary>Things bought over and over, as opposed to gear bought once.</summary>
    public static bool IsConsumable(ShopItemDefinition item) =>
        item.Kind is "potion" or "crystal" or "ammunition" or "misc";

    public void MarkSoldOut(string? itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId)) _soldOut.Add(itemId);
    }

    public bool IsSoldOut(string? itemId) =>
        itemId is not null && _soldOut.Contains(itemId);
}
