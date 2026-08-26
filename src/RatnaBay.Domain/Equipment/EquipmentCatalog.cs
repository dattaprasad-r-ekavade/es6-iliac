namespace RatnaBay.Domain;

/// <summary>
/// Four classes, each with a distinct verb. Variety comes from what a weapon *does*, not from
/// how many there are: a longer list of things that all swing is one weapon with four names.
/// </summary>
public enum WeaponClass
{
    /// <summary>Reliable, quick, and it leaves a hand free — for a shield, or for a spell.</summary>
    OneHanded,

    /// <summary>Slow and heavy. Cannot block, and must be shouldered before a spell.</summary>
    TwoHanded,

    /// <summary>Outranges everything. Also two-handed, with the same cost to casting.</summary>
    Ranged,

    /// <summary>
    /// Slower than a blade and it staggers what it lands on.
    ///
    /// The answer to a fight going badly that is not simply more damage: a staggered enemy is
    /// helpless, and the domain already rewards a blow landed in that window. It deliberately
    /// does not bleed — burn is Flame's identity, and a second damage-over-time on a mace
    /// would make the weapon a worse version of a spell the player may already have.
    /// </summary>
    Blunt
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

    /// <summary>How long a landed blow leaves the target helpless. Zero for everything but blunt.</summary>
    public float StaggerSeconds { get; init; }

    /// <summary>
    /// Seconds a swing costs the caster before a spell can follow it.
    ///
    /// The first trade between the warrior and the mage that lives in the equipment rather
    /// than in the spell table. A two-handed weapon has to be shouldered; a one-handed one
    /// leaves a hand free. Before this, a mage could carry a greatsword for nothing, which is
    /// the same imbalance the spell-damage pass fixed from the other end.
    ///
    /// A delay, never a ban. "Resistance, never immunity" is already the rule for cave themes
    /// and it is the right rule here: taxing an option keeps it a decision, removing it does
    /// not.
    /// </summary>
    public float CastDelaySeconds { get; init; }

    /// <summary>True when the weapon occupies both hands, and so refuses a shield.</summary>
    public bool IsTwoHanded => Class is WeaponClass.TwoHanded or WeaponClass.Ranged;

    /// <summary>
    /// True when a swing spends an arrow.
    ///
    /// The bow's cost was stamina, exactly like a sword's, which made a weapon that outranges
    /// everything strictly better at no price. Ammunition is the price: reach is now something
    /// bought in town and carried down, and a run that leans on the bow is a run that has to
    /// budget for it.
    /// </summary>
    public bool NeedsAmmunition => Class == WeaponClass.Ranged;

    public bool CanBlock => Class is WeaponClass.OneHanded or WeaponClass.Blunt;

    public string SkillId => Class switch
    {
        WeaponClass.OneHanded => Skills.Blade,
        WeaponClass.Blunt => Skills.Heavy,
        WeaponClass.TwoHanded => Skills.Heavy,
        _ => Skills.Marksman
    };
}

/// <summary>
/// Something carried in the off hand.
///
/// A shield is the only thing in the game that makes an existing verb better rather than
/// adding a new one, which is why it is a separate slot rather than a weapon: it has to be
/// combinable with the blade the player already chose.
/// </summary>
public sealed class ShieldDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>
    /// What a blocked blow is multiplied by while this is held.
    ///
    /// Lower is better. A bare hand blocks at <see cref="DamageMath.BlockReduction"/>; a shield
    /// takes it lower still. It is expressed as the whole factor rather than as a bonus so
    /// there is one number to read and no way for the two to drift apart.
    /// </summary>
    public required float BlockFactor { get; init; }

    /// <summary>Flat reduction, as armour, even when not actively blocking.</summary>
    public required float Armour { get; init; }

    public required int Tier { get; init; }
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

    /// <summary>What a bow spends. One id for every bow, so a tier is never also an ammo type.</summary>
    public const string ArrowId = "arrow";

    public const string ArrowName = "Arrows";
    public const string ArrowKind = "ammunition";

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
    private static readonly Dictionary<string, ShieldDefinition> Shields = new(StringComparer.Ordinal);

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

        // Blunt sits between the blade and the greatsword and is not a compromise between
        // them: it is slower and costlier than either per swing, and it buys a window in which
        // the target cannot answer. Damage a little above the blade so the stagger is the
        // reason to carry it rather than a bonus on top of already being better.
        AddWeapon("iron_mace", "Iron Mace", WeaponClass.Blunt, 24f, 2.2f, 0.72f, 12f, 1,
            stagger: 0.55f);
        AddWeapon("steel_mace", "Steel Mace", WeaponClass.Blunt, 33f, 2.2f, 0.72f, 12f, 2,
            stagger: 0.75f);

        AddArmour("padded_jerkin", "Padded Jerkin", 2f, 1);
        AddArmour("mail_hauberk", "Mail Hauberk", 5f, 2);

        // A bare guard already halves a blow. These take it further, and carry a little armour
        // of their own so the slot is not dead weight against anything unblockable.
        AddShield("wicker_shield", "Wicker Shield", blockFactor: 0.34f, armour: 1f, tier: 1);
        AddShield("bronze_shield", "Bronze Shield", blockFactor: 0.22f, armour: 3f, tier: 2);
    }

    private static void AddWeapon(string id, string name, WeaponClass cls,
        float damage, float range, float cooldown, float stamina, int tier,
        float stagger = 0f)
    {
        Weapons[id] = new WeaponDefinition
        {
            Id = id, DisplayName = name, Class = cls, Damage = damage,
            Range = range, Cooldown = cooldown, StaminaCost = stamina, Tier = tier,
            StaggerSeconds = stagger,

            // Derived rather than passed, so a new two-handed weapon cannot be added without
            // the cost to casting coming with it.
            CastDelaySeconds = cls is WeaponClass.TwoHanded or WeaponClass.Ranged ? 0.9f
                : cls is WeaponClass.Blunt ? 0.35f
                : 0f
        };
    }

    private static void AddShield(string id, string name, float blockFactor, float armour, int tier)
    {
        Shields[id] = new ShieldDefinition
        {
            Id = id, DisplayName = name, BlockFactor = blockFactor, Armour = armour, Tier = tier
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

    public static ShieldDefinition? GetShield(string? id)
    {
        if (!string.IsNullOrEmpty(id) && Shields.TryGetValue(id, out var shield)) return shield;
        return null;
    }

    public static bool IsShield(string? id) => !string.IsNullOrEmpty(id) && Shields.ContainsKey(id);

    public static bool IsWeapon(string? id) => !string.IsNullOrEmpty(id) && Weapons.ContainsKey(id);
    public static bool IsArmour(string? id) => !string.IsNullOrEmpty(id) && Armours.ContainsKey(id);

    public static IReadOnlyCollection<WeaponDefinition> AllWeapons => Weapons.Values;
    public static IReadOnlyCollection<ArmourDefinition> AllArmours => Armours.Values;
}
