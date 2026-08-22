using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>The enemies currently in the scene, and the fight between them and the player.</summary>
public sealed class Encounter
{
    /// <summary>How long a hit tints a sprite white.</summary>
    private const float HitFlashSeconds = 0.12f;

    /// <summary>How long the screen edge stays red after the player is hit.</summary>
    public const float DamageFlashSeconds = 0.35f;

    /// <summary>Sprites stand this tall in metres.</summary>
    public const float FigureHeight = 1.85f;

    /// <summary>Enemies stop this far short of each other so they do not stack into one body.</summary>
    private const float Separation = 1.1f;

    private readonly List<Enemy> _enemies = new();
    private readonly Dictionary<Enemy, float> _hitFlash = new();
    private readonly GameSession _session;

    public Encounter(GameSession session) => _session = session;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>Seconds left on the red screen edge. Zero when the player is not being hurt.</summary>
    public float DamageFlash { get; private set; }

    /// <summary>What the crosshair is currently over, for the enemy health bar.</summary>
    public Enemy? Focused { get; private set; }

    public void Spawn(EnemyArchetype archetype, Vector3 position, string spawnId)
    {
        // A save remembers the ones already killed, so they do not come back on reload.
        if (_session.Player.World.IsKilled(spawnId)) return;

        var point = new WorldPoint(position.X, position.Y, position.Z);
        var enemy = new Enemy(archetype, spawnId) { Position = point, Home = point };

        enemy.Died += Killed;
        _enemies.Add(enemy);
    }

    /// <summary>The default camp: three bandits, spread so they arrive one at a time.</summary>
    public void SpawnDefaultCamp()
    {
        var bandit = new EnemyArchetype
        {
            Id = "bandit", DisplayName = "Bandit",
            MaxHealth = 55f, MoveSpeed = 4.4f,
            AggroRange = 16f, AttackRange = 2.2f,
            AttackDamage = 7f, AttackCooldown = 1.4f,
            XpReward = 20
        };

        Spawn(bandit, new Vector3(3.5f, 0f, -6f), "bandit.camp.01");
        Spawn(bandit, new Vector3(-4.5f, 0f, -9f), "bandit.camp.02");
        Spawn(bandit, new Vector3(7.5f, 0f, -12f), "bandit.camp.03");
    }

    public void Update(float deltaSeconds, Vector3 playerPosition, float playerYaw)
    {
        if (deltaSeconds <= 0f) return;

        if (DamageFlash > 0f) DamageFlash = MathF.Max(0f, DamageFlash - deltaSeconds);

        var player = new WorldPoint(playerPosition.X, playerPosition.Y, playerPosition.Z);

        foreach (var enemy in _enemies)
        {
            enemy.Tick(deltaSeconds);
            if (_hitFlash.ContainsKey(enemy)) _hitFlash[enemy] -= deltaSeconds;
            if (!enemy.IsAlive) continue;

            switch (enemy.Decide(player))
            {
                case EnemyIntent.Chase:
                    Advance(enemy, player, deltaSeconds);
                    break;

                case EnemyIntent.Attack:
                    var damage = enemy.Attack();
                    if (damage <= 0f) break;
                    _session.Player.Combat.TakeHit(damage);
                    DamageFlash = DamageFlashSeconds;
                    break;
            }
        }

        _enemies.RemoveAll(e => !e.IsAlive);
        Focused = Targeting.Find(player, playerYaw,
            _session.Player.Combat.ActiveWeapon.Range, _enemies);
    }

    /// <summary>Slide toward the player, stopping short of anything already standing there.</summary>
    private void Advance(Enemy enemy, WorldPoint player, float deltaSeconds)
    {
        var dx = player.X - enemy.Position.X;
        var dz = player.Z - enemy.Position.Z;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance < 0.001f) return;

        var step = enemy.CurrentMoveSpeed * deltaSeconds;
        var next = new WorldPoint(
            enemy.Position.X + dx / distance * step,
            enemy.Position.Y,
            enemy.Position.Z + dz / distance * step);

        // Without this the whole camp converges into a single overlapping sprite.
        foreach (var other in _enemies)
        {
            if (ReferenceEquals(other, enemy) || !other.IsAlive) continue;
            if (next.FlatDistanceTo(other.Position) < Separation) return;
        }

        enemy.Position = next;
    }

    /// <summary>
    /// Swing at whatever the crosshair is over. Returns what happened so the caller can say
    /// so on screen.
    /// </summary>
    public AttackOutcome PlayerAttack()
    {
        var target = Focused;
        var outcome = _session.Player.Combat.TryAttack(target);

        if (outcome.Result == AttackResult.Hit && target is not null)
            _hitFlash[target] = HitFlashSeconds;

        return outcome;
    }

    /// <summary>Cast at whatever is down the crosshair, with Arc's chain target resolved.</summary>
    public CastOutcome PlayerCast(Vector3 playerPosition, float playerYaw)
    {
        var origin = new WorldPoint(playerPosition.X, playerPosition.Y, playerPosition.Z);
        var spell = SpellCatalog.Get(_session.Player.Spells.SelectedSpellId);
        var range = spell?.Range ?? 0f;

        var target = range > 0f
            ? Targeting.Find(origin, playerYaw, range, _enemies, Targeting.SpellConeRadians)
            : null;

        var chain = target is null ? null : Targeting.FindNearestOther(target, _enemies, 6f);
        var outcome = _session.Player.Spells.Cast(target, chain);

        if (target is not null && outcome.Result == CastResult.Landed)
            _hitFlash[target] = HitFlashSeconds;

        return outcome;
    }

    /// <summary>White while a sprite is being struck, so a landed blow is visible.</summary>
    public Color TintOf(Enemy enemy) =>
        _hitFlash.TryGetValue(enemy, out var remaining) && remaining > 0f
            ? new Color(255, 236, 236)
            : Color.White;

    private void Killed(Enemy enemy)
    {
        _session.Player.NotifyEnemyKilled(enemy);
        _session.ShowToast($"{enemy.DisplayName} falls.");

        if (!enemy.Archetype.DropsLoot) return;
        _session.Player.Inventory.Add("bandit_loot", "Bandit Satchel", 1, "loot");
        _session.Player.Vitals.AddGold(Random.Shared.Next(5, 18));
    }
}
