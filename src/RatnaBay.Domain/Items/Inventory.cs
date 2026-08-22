namespace RatnaBay.Domain;

/// <summary>A stack of one item id. Saves store these directly, so ids stay stable.</summary>
public sealed class ItemStack
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public int Count { get; set; }
}

/// <summary>
/// What the player is carrying.
///
/// Stats are deliberately not held here — <see cref="EquipmentCatalog"/> owns them, so a
/// save stores only ids and rebalancing a weapon cannot invalidate an existing save.
/// </summary>
public sealed class Inventory
{
    private readonly List<ItemStack> _items = new();

    public IReadOnlyList<ItemStack> Items => _items;

    /// <summary>Raised after any change, so UI can rebuild without polling.</summary>
    public event Action? Changed;

    /// <summary>The starting kit. Kept out of the constructor so a load starts empty.</summary>
    public static Inventory CreateStartingKit()
    {
        var inventory = new Inventory();
        inventory.Add("iron_sword", "Iron Sword", 1, "weapon");
        inventory.Add("health_potion", "Health Potion", 3, "potion");
        inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 3, SoulCrystals.ItemKind);
        inventory.Add("torch", "Torch", 1, "misc");
        return inventory;
    }

    public void Add(string id, string name, int count, string kind)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return;

        var existing = Find(id);
        if (existing is not null) existing.Count += count;
        else _items.Add(new ItemStack { Id = id, Name = name, Kind = kind, Count = count });

        Changed?.Invoke();
    }

    /// <summary>Stack size held for an item id, or zero.</summary>
    public int CountOf(string? id) => Find(id)?.Count ?? 0;

    public bool Has(string? id, int count = 1) => CountOf(id) >= count;

    /// <summary>Removes <paramref name="count"/> of an id. All-or-nothing.</summary>
    public bool Consume(string? id, int count = 1)
    {
        if (count <= 0) return false;

        var existing = Find(id);
        if (existing is null || existing.Count < count) return false;

        existing.Count -= count;
        if (existing.Count <= 0) _items.Remove(existing);

        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Changed?.Invoke();
    }

    private ItemStack? Find(string? id) =>
        string.IsNullOrEmpty(id) ? null : _items.Find(i => string.Equals(i.Id, id, StringComparison.Ordinal));
}
