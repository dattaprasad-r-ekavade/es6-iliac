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
    public const string ArcherId = "bandit_archer";

    /// <summary>
    /// The three tiers of risen dead, weakest first.
    ///
    /// One creature at three scales rather than three creatures: a chhaya is what is left of a
    /// miner, a vetala is one that has kept enough of itself to be deliberate, and a pishacha
    /// is something the mountain took long before there was a town. They are told apart by
    /// size and by how much they still want, which is also exactly how the sprites are built.
    /// </summary>
    public const string ChhayaId = "chhaya";

    public const string VetalaId = "vetala";
    public const string PishachaId = "pishacha";

    /// <summary>
    /// The old id for the common tier.
    ///
    /// Kept so that saves and mine manifests written before the rename still load. Nothing new
    /// should use it; <see cref="Find"/> resolves it to <see cref="ChhayaId"/>.
    /// </summary>
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

            // Faster, frailer and it hits harder: a chhaya is a pressure enemy, and pressure
            // is what makes the decision at the camp a decision.
            [ChhayaId] = new EnemyArchetype
            {
                Id = ChhayaId,
                DisplayName = "Chhaya",
                MaxHealth = 38f,
                MoveSpeed = 5.2f,
                AggroRange = 18f,
                AttackRange = 2.0f,
                AttackDamage = 9f,
                AttackCooldown = 1.2f,
                XpReward = 26
            },

            // A chhaya that kept enough of itself to be deliberate. Slower than the common
            // tier on purpose: it does not need to rush, and a player who has learned to
            // kite chhaya has to learn something else.
            [VetalaId] = new EnemyArchetype
            {
                Id = VetalaId,
                DisplayName = "Vetala",
                MaxHealth = 96f,
                MoveSpeed = 4.6f,
                AggroRange = 20f,
                AttackRange = 2.4f,
                AttackDamage = 16f,
                AttackCooldown = 1.5f,
                XpReward = 70
            },

            // Something the mountain took long before there was a town. Slow, enormously
            // durable, and it hits hard enough that trading blows is never the answer.
            [PishachaId] = new EnemyArchetype
            {
                Id = PishachaId,
                DisplayName = "Pishacha",
                MaxHealth = 260f,
                MoveSpeed = 3.6f,
                AggroRange = 24f,
                AttackRange = 3.0f,
                AttackDamage = 27f,
                AttackCooldown = 2.0f,
                XpReward = 220
            },

            // The answer to fighting every room from its doorway.
            //
            // It shoots from across the room and gives ground when approached, so standing in
            // a corridor is the worst place to be rather than the best: the queue of melee
            // walks into you while this thing keeps hitting you from the dark. Frail on
            // purpose — it is meant to be rushed, which is exactly the behaviour the room
            // shapes need from the player before they are worth designing.
            [ArcherId] = new EnemyArchetype
            {
                Id = ArcherId,
                DisplayName = "Bandit Archer",
                MaxHealth = 30f,
                MoveSpeed = 4.0f,
                AggroRange = 22f,
                AttackRange = 15f,
                StandOffRange = 9f,
                AttackDamage = 8f,
                AttackCooldown = 2.1f,
                XpReward = 24
            }
        };

    public static IEnumerable<EnemyArchetype> All => Archetypes.Values;

    public static IReadOnlyCollection<string> Ids => Archetypes.Keys;

    public static EnemyArchetype? Find(string? id)
    {
        if (id is null) return null;

        // "preta" was the common tier's id before the three tiers existed. Manifests and saves
        // on disk still carry it, and a mine that fails to spawn its enemies is a worse
        // outcome than a legacy alias.
        if (string.Equals(id, PretaId, StringComparison.Ordinal)) id = ChhayaId;

        return Archetypes.TryGetValue(id, out var archetype) ? archetype : null;
    }

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
