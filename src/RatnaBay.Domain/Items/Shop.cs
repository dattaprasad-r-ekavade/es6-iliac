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

    public ShopPurchaseResult Buy(int index, PlayerVitals vitals, Inventory inventory,
        out ShopItemDefinition? item)
    {
        item = index >= 0 && index < _definition.Items.Count ? _definition.Items[index] : null;
        if (item is null) return ShopPurchaseResult.InvalidItem;
        if (_soldOut.Contains(item.Id)) return ShopPurchaseResult.SoldOut;
        if (!vitals.SpendGold(item.Price)) return ShopPurchaseResult.TooExpensive;

        inventory.Add(item.Id, item.Name, item.Count, item.Kind);
        _soldOut.Add(item.Id);
        return ShopPurchaseResult.Bought;
    }

    public void MarkSoldOut(string? itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId)) _soldOut.Add(itemId);
    }

    public bool IsSoldOut(string? itemId) =>
        itemId is not null && _soldOut.Contains(itemId);
}
