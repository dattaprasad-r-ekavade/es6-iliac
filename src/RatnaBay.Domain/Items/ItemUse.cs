namespace RatnaBay.Domain;

public enum ItemUseResult
{
    /// <summary>Consumed, and its effect applied.</summary>
    Used,

    /// <summary>Now held in hand or worn.</summary>
    Equipped,

    /// <summary>Nothing happens when you use this — a key, a purse, loot.</summary>
    NotUsable,

    /// <summary>Not carried.</summary>
    NotHeld,

    /// <summary>Using it would have done nothing, so it was not spent.</summary>
    NoEffect
}

/// <summary>
/// What happens when the player uses something in their pack.
///
/// This is a rule, not a menu: whether a potion is wasted at full health, and whether a
/// weapon can be swapped mid-fight, are answers the domain owes and the inventory screen
/// merely calls. Testers found the inventory unintuitive partly because it did nothing —
/// this is the half that makes it do something.
/// </summary>
public static class ItemUse
{
    /// <summary>What a health potion restores.</summary>
    public const float PotionHeal = 40f;

    /// <summary>A short description of what using this item will do, for the UI.</summary>
    public static string DescribeAction(string? itemId, string? kind)
    {
        if (EquipmentCatalog.IsWeapon(itemId)) return "Equip";
        if (EquipmentCatalog.IsArmour(itemId)) return "Wear";
        if (string.Equals(itemId, SoulCrystals.LesserId, StringComparison.Ordinal)) return "Draw";
        return string.Equals(kind, "potion", StringComparison.OrdinalIgnoreCase) ? "Drink" : "—";
    }

    /// <summary>A one-line explanation of the item itself, for the UI.</summary>
    public static string Describe(string? itemId, string? kind)
    {
        if (EquipmentCatalog.IsWeapon(itemId))
        {
            var weapon = EquipmentCatalog.GetWeapon(itemId);
            var guard = weapon.CanBlock ? "can guard" : "cannot guard";
            return $"{weapon.Damage:0} damage, {weapon.Range:0.0} m reach, {guard}.";
        }

        if (EquipmentCatalog.IsArmour(itemId))
            return $"{EquipmentCatalog.GetArmour(itemId)!.Armour:0} damage reduction.";

        if (string.Equals(itemId, SoulCrystals.LesserId, StringComparison.Ordinal))
            return $"Restores {SoulCrystals.LesserCharge:0} prana when drawn on.";

        if (string.Equals(kind, "potion", StringComparison.OrdinalIgnoreCase))
            return $"Restores {PotionHeal:0} health.";

        if (string.Equals(kind, "key", StringComparison.OrdinalIgnoreCase))
            return "Opens one specific lock.";

        return "Worth something to the right buyer.";
    }

    /// <summary>
    /// Use one item. Consumables are only spent when they would actually do something, so a
    /// misclick at full health does not cost a potion.
    /// </summary>
    public static ItemUseResult Use(string? itemId, PlayerCharacter player)
    {
        if (string.IsNullOrEmpty(itemId) || !player.Inventory.Has(itemId))
            return ItemUseResult.NotHeld;

        if (EquipmentCatalog.IsWeapon(itemId) || EquipmentCatalog.IsArmour(itemId))
            return player.Equipment.Equip(itemId) ? ItemUseResult.Equipped : ItemUseResult.NotHeld;

        if (string.Equals(itemId, SoulCrystals.LesserId, StringComparison.Ordinal))
        {
            if (player.Vitals.Prana >= player.Vitals.MaxPrana) return ItemUseResult.NoEffect;
            return player.Vitals.TryDrawCrystal() ? ItemUseResult.Used : ItemUseResult.NotHeld;
        }

        var stack = player.Inventory.Items.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal));

        if (!string.Equals(stack?.Kind, "potion", StringComparison.OrdinalIgnoreCase))
            return ItemUseResult.NotUsable;

        if (player.Vitals.Health >= player.Vitals.MaxHealth) return ItemUseResult.NoEffect;
        if (!player.Inventory.Consume(itemId)) return ItemUseResult.NotHeld;

        player.Vitals.Heal(PotionHeal);
        return ItemUseResult.Used;
    }
}
