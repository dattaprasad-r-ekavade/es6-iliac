using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// Every kind of enemy that can be placed, by id.
///
/// A manifest names an archetype rather than describing one, so rebalancing an enemy is a
/// change here and nowhere else. The alternative — statistics embedded in each level file —
/// means every generated mine ever written to disk carries a frozen copy of the old numbers.
/// </summary>
public static class EnemyCatalog
{
    public const string BanditId = "bandit";
    public const string PretaId = "preta";

    private static readonly Dictionary<string, EnemyArchetype> Archetypes =
        new(StringComparer.Ordinal)
        {
            [BanditId] = new EnemyArchetype
            {
                Id = BanditId,
                DisplayName = "Bandit",
                MaxHealth = 55f,
                MoveSpeed = 4.4f,
                AggroRange = 16f,
                AttackRange = 2.2f,
                AttackDamage = 7f,
                AttackCooldown = 1.4f,
                XpReward = 20
            },

            // Faster, frailer and it hits harder: a preta is a pressure enemy, and pressure is
            // what makes the decision at the camp a decision.
            [PretaId] = new EnemyArchetype
            {
                Id = PretaId,
                DisplayName = "Preta",
                MaxHealth = 38f,
                MoveSpeed = 5.2f,
                AggroRange = 18f,
                AttackRange = 2.0f,
                AttackDamage = 9f,
                AttackCooldown = 1.2f,
                XpReward = 26
            }
        };

    public static IEnumerable<EnemyArchetype> All => Archetypes.Values;

    public static IReadOnlyCollection<string> Ids => Archetypes.Keys;

    public static EnemyArchetype? Find(string? id) =>
        id is not null && Archetypes.TryGetValue(id, out var archetype) ? archetype : null;

    /// <summary>
    /// The archetype a spawn asks for, already scaled to its level. Null when the manifest
    /// names an enemy that no longer exists, so a stale level file loses one fight rather than
    /// taking the room down with it.
    /// </summary>
    public static EnemyArchetype? Resolve(WorldEnemySpawn spawn)
    {
        var archetype = Find(spawn?.ArchetypeId);
        return archetype?.AtLevel(Math.Max(1, spawn!.Level));
    }
}
