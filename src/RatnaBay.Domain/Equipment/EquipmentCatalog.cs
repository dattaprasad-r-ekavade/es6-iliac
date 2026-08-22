namespace RatnaBay.Domain;

/// <summary>Three classes, each with a distinct verb. Variety comes from tiers, not count.</summary>
public enum WeaponClass
{
    /// <summary>Reliable, and the only class that can block.</summary>
    OneHanded,

    /// <summary>Slow and heavy. Cannot block — the trade is commitment for damage.</summary>
    TwoHanded,

    /// <summary>Weak in melee; the payoff for stealth.</summary>
    Ranged
}

public sealed class WeaponDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required WeaponClass Class { get; init; }
    public required float Damage { get; init; }
    public required float Range { get; init; }
    public required float Cooldown { get; init; }
    public required float StaminaCost { get; init; }

    /// <summary>Tier is a stat swap on the same mesh.</summary>
    public required int Tier { get; init; }

    public bool CanBlock => Class == WeaponClass.OneHanded;

    public string SkillId => Class switch
    {
        WeaponClass.OneHanded => Skills.Blade,
        WeaponClass.TwoHanded => Skills.Heavy,
        _ => Skills.Marksman
    };
}

public sealed class ArmourDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Flat damage reduction. Armour has no skill — Block is the active verb.</summary>
    public required float Armour { get; init; }

    public required int Tier { get; init; }
}

/// <summary>
/// Stats live here rather than on the inventory item so that saves store only ids.
/// Rebalancing a weapon then cannot invalidate a save, and an unknown id degrades to
/// unarmed rather than throwing.
/// </summary>
public static class EquipmentCatalog
{
    public const string UnarmedId = "unarmed";

    public static readonly WeaponDefinition Unarmed = new()
    {
        Id = UnarmedId,
        DisplayName = "Bare Hands",
        Class = WeaponClass.OneHanded,
        Damage = 6f,
        Range = 1.8f,
        Cooldown = 0.5f,
        StaminaCost = 6f,
        Tier = 0
    };

    private static readonly Dictionary<string, WeaponDefinition> Weapons = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ArmourDefinition> Armours = new(StringComparer.Ordinal);

    static EquipmentCatalog()
    {
        // One-handed: the baseline. Modest damage, quick, can block.
        AddWeapon("iron_sword", "Iron Sword", WeaponClass.OneHanded, 18f, 2.4f, 0.45f, 8f, 1);
        AddWeapon("steel_sword", "Steel Sword", WeaponClass.OneHanded, 26f, 2.4f, 0.45f, 8f, 2);

        // Two-handed: slower and dearer in stamina, but it hits far harder and reaches further.
        AddWeapon("iron_greatsword", "Iron Greatsword", WeaponClass.TwoHanded, 34f, 3.0f, 0.85f, 16f, 1);
        AddWeapon("steel_greatsword", "Steel Greatsword", WeaponClass.TwoHanded, 46f, 3.0f, 0.85f, 16f, 2);

        // Ranged: reach at the cost of per-hit damage. The thief route's payoff.
        AddWeapon("hunting_bow", "Hunting Bow", WeaponClass.Ranged, 14f, 30f, 0.7f, 10f, 1);
        AddWeapon("war_bow", "War Bow", WeaponClass.Ranged, 20f, 34f, 0.7f, 10f, 2);

        AddArmour("padded_jerkin", "Padded Jerkin", 2f, 1);
        AddArmour("mail_hauberk", "Mail Hauberk", 5f, 2);
    }

    private static void AddWeapon(string id, string name, WeaponClass cls,
        float damage, float range, float cooldown, float stamina, int tier)
    {
        Weapons[id] = new WeaponDefinition
        {
            Id = id, DisplayName = name, Class = cls, Damage = damage,
            Range = range, Cooldown = cooldown, StaminaCost = stamina, Tier = tier
        };
    }

    private static void AddArmour(string id, string name, float armour, int tier)
    {
        Armours[id] = new ArmourDefinition { Id = id, DisplayName = name, Armour = armour, Tier = tier };
    }

    /// <summary>Never returns null — an unknown or empty id resolves to unarmed.</summary>
    public static WeaponDefinition GetWeapon(string? id)
    {
        if (!string.IsNullOrEmpty(id) && Weapons.TryGetValue(id, out var weapon)) return weapon;
        return Unarmed;
    }

    public static ArmourDefinition? GetArmour(string? id)
    {
        if (!string.IsNullOrEmpty(id) && Armours.TryGetValue(id, out var armour)) return armour;
        return null;
    }

    public static bool IsWeapon(string? id) => !string.IsNullOrEmpty(id) && Weapons.ContainsKey(id);
    public static bool IsArmour(string? id) => !string.IsNullOrEmpty(id) && Armours.ContainsKey(id);

    public static IReadOnlyCollection<WeaponDefinition> AllWeapons => Weapons.Values;
    public static IReadOnlyCollection<ArmourDefinition> AllArmours => Armours.Values;
}
