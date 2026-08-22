namespace RatnaBay.Domain;

public enum SpellSchool
{
    /// <summary>Offensive elements. Trained by casting them at things that fight back.</summary>
    Destruction,

    /// <summary>Healing and light.</summary>
    Restoration
}

public enum SpellEffect
{
    /// <summary>Burn over time. Beats groups and the unarmoured.</summary>
    Fire,

    /// <summary>Slow. Beats chargers.</summary>
    Frost,

    /// <summary>Interrupt, and chain to a nearby second target. Beats anything mid-action.</summary>
    Shock,

    /// <summary>Restore health.</summary>
    Heal,

    /// <summary>Utility light. In a crystal-lit world, seeing costs the same resource.</summary>
    Light
}

public sealed class SpellDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required SpellSchool School { get; init; }
    public required SpellEffect Effect { get; init; }

    /// <summary>Base charge cost before skill mileage is applied.</summary>
    public required float BaseCost { get; init; }

    public required float Range { get; init; }

    /// <summary>Immediate damage, or heal amount for Restoration.</summary>
    public required float Power { get; init; }

    /// <summary>Seconds the status lasts. Zero for instant spells.</summary>
    public required float Duration { get; init; }

    public string SkillId =>
        School == SpellSchool.Destruction ? Skills.Destruction : Skills.Restoration;
}

/// <summary>
/// Five spells, each doing something mechanically different.
///
/// The trap this avoids is Oblivion's: elements that are damage types with different particle
/// colours. Fire burns, frost slows, shock interrupts and chains. A player should pick one
/// because of what the enemy is doing, not because of a resistance table.
/// </summary>
public static class SpellCatalog
{
    public const string FireId = "spell.fire";
    public const string FrostId = "spell.frost";
    public const string ShockId = "spell.shock";
    public const string HealId = "spell.heal";
    public const string LightId = "spell.light";

    private static readonly Dictionary<string, SpellDefinition> Spells = new(StringComparer.Ordinal);

    static SpellCatalog()
    {
        Add(FireId, "Flame", SpellSchool.Destruction, SpellEffect.Fire,
            cost: 16f, range: 18f, power: 10f, duration: 4f);

        Add(FrostId, "Rime", SpellSchool.Destruction, SpellEffect.Frost,
            cost: 14f, range: 18f, power: 12f, duration: 4f);

        Add(ShockId, "Arc", SpellSchool.Destruction, SpellEffect.Shock,
            cost: 18f, range: 18f, power: 16f, duration: 1.2f);

        Add(HealId, "Mend", SpellSchool.Restoration, SpellEffect.Heal,
            cost: 20f, range: 0f, power: 35f, duration: 0f);

        // Light is deliberately cheap but not free: carrying a light in a crystal-lit world
        // is consuming the resource, so every dark corridor is a small decision.
        Add(LightId, "Emberlight", SpellSchool.Restoration, SpellEffect.Light,
            cost: 6f, range: 0f, power: 0f, duration: 60f);
    }

    private static void Add(string id, string name, SpellSchool school, SpellEffect effect,
        float cost, float range, float power, float duration)
    {
        Spells[id] = new SpellDefinition
        {
            Id = id, DisplayName = name, School = school, Effect = effect,
            BaseCost = cost, Range = range, Power = power, Duration = duration
        };
    }

    public static SpellDefinition? Get(string? id) =>
        !string.IsNullOrEmpty(id) && Spells.TryGetValue(id, out var spell) ? spell : null;

    public static bool Exists(string? id) => Get(id) is not null;

    public static IReadOnlyCollection<SpellDefinition> All => Spells.Values;
}
