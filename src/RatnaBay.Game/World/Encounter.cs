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

    /// <summary>How long an enemy's lunge takes to play out after it swings.</summary>
    private const float LungeSeconds = 0.34f;

    /// <summary>How long a struck enemy is knocked back on its heels.</summary>
    private const float RecoilSeconds = 0.22f;

    private readonly List<Enemy> _enemies = new();
    private readonly Dictionary<Enemy, float> _hitFlash = new();
    private readonly Dictionary<Enemy, EnemyAnimation> _animation = new();
    private readonly GameSession _session;

    /// <summary>
    /// Enough motion for a sprite to read as alive.
    ///
    /// Deliberately not a rig: a walk is a sine on the vertical offset, a swing is a lunge
    /// toward the player and back, and a hit is a shove away from them. All three are curves
    /// over time applied to where the quad is drawn, which costs nothing per new enemy.
    /// </summary>
    private sealed class EnemyAnimation
    {
        public float WalkPhase;
        public float Lunge;
        public float Recoil;

        /// <summary>Direction the lunge or recoil pushes, set when it starts.</summary>
        public WorldPoint Facing;
    }

    public Encounter(GameSession session) => _session = session;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>Seconds left on the red screen edge. Zero when the player is not being hurt.</summary>
    public float DamageFlash { get; private set; }

    /// <summary>What the crosshair is currently over, for the enemy health bar.</summary>
    public Enemy? Focused { get; private set; }

    private WorldPoint _lastPlayerPosition;
    private float _lastPlayerYaw;

    /// <summary>The visual half of the fight: markers, numbers and damage direction.</summary>
    public CombatFeedback Feedback { get; } = new();

    private readonly List<SpellBolt> _bolts = new();

    /// <summary>Spells currently in flight.</summary>
    public IReadOnlyList<SpellBolt> Bolts => _bolts;

    /// <summary>How close a bolt must get to count as a hit.</summary>
    private const float BoltHitRadius = 1.1f;

    /// <summary>Seconds a bolt flies before it fizzles.</summary>
    private const float BoltLifetime = 2.2f;

    public void Spawn(EnemyArchetype archetype, Vector3 position, string spawnId)
    {
        // A save remembers the ones already killed, so they do not come back on reload.
        if (_session.Player.World.IsKilled(spawnId)) return;

        var point = new WorldPoint(position.X, position.Y, position.Z);
        var enemy = new Enemy(archetype, spawnId) { Position = point, Home = point };

        enemy.Died += Killed;
        _enemies.Add(enemy);
    }

    /// <summary>Two bandits waiting at the far end of the third room.</summary>
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

        // Room one (-10..18) is a safe, empty spawn and room two (-24..-10) belongs
        // to the traders. Combat starts only after the player enters room three.
        Spawn(bandit, new Vector3(-2.5f, 0f, -40.0f), "bandit.camp.01");
        Spawn(bandit, new Vector3(2.5f, 0f, -40.5f), "bandit.camp.02");
    }

    public void Update(float deltaSeconds, Vector3 playerPosition, float playerYaw)
    {
        if (deltaSeconds <= 0f) return;

        if (DamageFlash > 0f) DamageFlash = MathF.Max(0f, DamageFlash - deltaSeconds);

        var player = new WorldPoint(playerPosition.X, playerPosition.Y, playerPosition.Z);
        _lastPlayerPosition = player;
        _lastPlayerYaw = playerYaw;
        Feedback.Tick(deltaSeconds);
        UpdateBolts(deltaSeconds);

        foreach (var enemy in _enemies)
        {
            enemy.Tick(deltaSeconds);
            if (_hitFlash.ContainsKey(enemy)) _hitFlash[enemy] -= deltaSeconds;

            var animation = AnimationOf(enemy);
            animation.Lunge = MathF.Max(0f, animation.Lunge - deltaSeconds);
            animation.Recoil = MathF.Max(0f, animation.Recoil - deltaSeconds);

            if (!enemy.IsAlive) continue;

            switch (enemy.Decide(player))
            {
                case EnemyIntent.Chase:
                    Advance(enemy, player, deltaSeconds);
                    // Stride rate follows speed, so a chilled bandit visibly plods.
                    animation.WalkPhase += deltaSeconds * enemy.CurrentMoveSpeed * 2.4f;
                    break;

                case EnemyIntent.Attack:
                    var damage = enemy.Attack();
                    if (damage <= 0f) break;

                    animation.Lunge = LungeSeconds;
                    animation.Facing = Direction(enemy.Position, player);

                    var guarded = _session.Player.Combat.IsBlocking;
                    var landed = _session.Player.Combat.TakeHit(damage);
                    Feedback.PlayerHurt(landed,
                        Targeting.RelativeBearing(player, playerYaw, enemy.Position), guarded);

                    DamageFlash = DamageFlashSeconds;
                    break;
            }
        }

        foreach (var dead in _enemies.FindAll(e => !e.IsAlive))
        {
            _animation.Remove(dead);
            _hitFlash.Remove(dead);
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
        {
            Struck(target);
            Feedback.PlayerHit(target.Position, outcome.Damage, !target.IsAlive);
        }

        return outcome;
    }

    /// <summary>
    /// Move every bolt, and resolve the ones that arrive.
    ///
    /// A bolt homes gently toward what it was aimed at so a moving target is still hittable,
    /// but it can miss: an enemy that dies or steps aside leaves it to fly on and fizzle.
    /// </summary>
    private void UpdateBolts(float deltaSeconds)
    {
        for (var index = _bolts.Count - 1; index >= 0; index--)
        {
            var bolt = _bolts[index];
            bolt.Remaining -= deltaSeconds;
            bolt.Spin += deltaSeconds * 9f;

            var velocity = bolt.Velocity;

            if (bolt.Target is { IsAlive: true } target)
            {
                var toTarget = new Vector3(target.Position.X, target.Position.Y + 1f, target.Position.Z)
                    - bolt.Position;

                if (toTarget.LengthSquared() > 0.0001f)
                {
                    toTarget.Normalize();
                    // Gentle homing: enough to track a walking bandit, not enough to curve
                    // around cover or to make aiming pointless.
                    velocity = Vector3.Normalize(Vector3.Lerp(
                        Vector3.Normalize(velocity), toTarget, 0.14f)) * SpellCaster.ProjectileSpeed;
                }
            }

            bolt.Position += velocity * deltaSeconds;

            var hit = FindBoltHit(bolt);
            if (hit is null && bolt.Remaining > 0f) continue;

            _bolts.RemoveAt(index);
            ResolveBolt(bolt, hit);
        }
    }

    private Enemy? FindBoltHit(SpellBolt bolt)
    {
        var point = new WorldPoint(bolt.Position.X, bolt.Position.Y, bolt.Position.Z);

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;
            if (point.FlatDistanceTo(enemy.Position) > BoltHitRadius) continue;
            if (MathF.Abs(bolt.Position.Y - (enemy.Position.Y + FigureHeight * 0.5f)) > FigureHeight) continue;
            return enemy;
        }

        return null;
    }

    /// <summary>Apply a bolt that has arrived, or report the miss when it fizzled.</summary>
    private void ResolveBolt(SpellBolt bolt, Enemy? hit)
    {
        if (hit is null)
        {
            Feedback.Cast(bolt.Spell.DisplayName, "found no target", bolt.Colour);
            return;
        }

        var chain = Targeting.FindNearestOther(hit, _enemies, 6f);
        _session.Player.Spells.Deliver(bolt.Spell, hit, chain);

        Struck(hit);
        Feedback.PlayerHit(hit.Position, bolt.Spell.Power, !hit.IsAlive);
        Feedback.Cast(bolt.Spell.DisplayName, $"struck {hit.DisplayName}", bolt.Colour);

        var status = bolt.Spell.Effect switch
        {
            SpellEffect.Fire => "burning",
            SpellEffect.Frost => "chilled",
            SpellEffect.Shock => "staggered",
            _ => string.Empty
        };

        if (status.Length > 0) Feedback.PlayerEffect(hit.Position, status, bolt.Colour);

        if (chain is not null && bolt.Spell.Effect == SpellEffect.Shock)
            Feedback.PlayerEffect(chain.Position, "arced", bolt.Colour);
    }

    /// <summary>Cast at whatever is down the crosshair, with Arc's chain target resolved.</summary>
    public CastOutcome PlayerCast(Vector3 playerPosition, float playerYaw, Vector3 aimDirection)
    {
        var caster = _session.Player.Spells;
        var paid = caster.Pay(caster.SelectedSpellId);
        if (paid.Spell is not { } spell) return paid;

        var colour = ElementColour(spell.Effect);

        if (!paid.WasCast)
        {
            Feedback.Cast(spell.DisplayName, "no prana, and no stone to draw",
                new Color(200, 128, 122));
            return paid;
        }

        // Heals and light happen in the hand; the elements leave it.
        if (!SpellCaster.IsProjectile(spell))
        {
            caster.Deliver(spell, target: null);
            Feedback.Cast(spell.DisplayName, "cast", colour);
            return new CastOutcome(CastResult.Landed, spell, paid.Cost);
        }

        var origin = new WorldPoint(playerPosition.X, playerPosition.Y, playerPosition.Z);
        var target = Targeting.Find(origin, playerYaw, spell.Range, _enemies,
            Targeting.SpellConeRadians);

        var direction = aimDirection.LengthSquared() > 0.0001f
            ? Vector3.Normalize(aimDirection)
            : new Vector3(Targeting.FlatForward(playerYaw).X, 0f, Targeting.FlatForward(playerYaw).Z);

        _bolts.Add(new SpellBolt
        {
            Spell = spell,
            Colour = colour,
            // Launched a little below the eye so it reads as leaving the hand.
            Position = playerPosition + direction * 0.9f - Vector3.Up * 0.35f,
            Velocity = direction * SpellCaster.ProjectileSpeed,
            Target = target,
            Remaining = BoltLifetime
        });

        Feedback.Cast(spell.DisplayName, target is null ? "loosed" : $"loosed at {target.DisplayName}",
            colour);

        return new CastOutcome(CastResult.Missed, spell, paid.Cost);
    }

    /// <summary>
    /// Living enemies within <paramref name="radius"/>, with where each one is relative to
    /// the way the player is facing. This is what the on-screen threat arrows point along.
    /// </summary>
    public IEnumerable<(Enemy Enemy, float Bearing, float Distance)> NearbyThreats(float radius = 26f)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;

            var distance = _lastPlayerPosition.FlatDistanceTo(enemy.Position);
            if (distance > radius) continue;

            yield return (enemy,
                Targeting.RelativeBearing(_lastPlayerPosition, _lastPlayerYaw, enemy.Position),
                distance);
        }
    }

    /// <summary>One colour per element, shared by the banner, the tint and the status word.</summary>
    private static Color ElementColour(SpellEffect effect) => effect switch
    {
        SpellEffect.Fire => new Color(240, 150, 96),
        SpellEffect.Frost => new Color(150, 208, 240),
        SpellEffect.Shock => new Color(232, 214, 130),
        SpellEffect.Heal => new Color(140, 216, 156),
        _ => new Color(226, 206, 150)
    };

    /// <summary>Flash and shove an enemy that has just been hit.</summary>
    private void Struck(Enemy enemy)
    {
        _hitFlash[enemy] = HitFlashSeconds;

        var animation = AnimationOf(enemy);
        animation.Recoil = RecoilSeconds;
        animation.Facing = Direction(enemy.Position, _lastPlayerPosition);
    }

    /// <summary>
    /// Where to draw this enemy's feet, once the walk, the lunge and the recoil are applied.
    /// The domain position is untouched — animation must never move a hitbox.
    /// </summary>
    public Vector3 DrawPositionOf(Enemy enemy)
    {
        var position = new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z);
        if (!_animation.TryGetValue(enemy, out var animation)) return position;

        // Bounce on every stride. Absolute sine, so the figure never sinks into the ground.
        position.Y += MathF.Abs(MathF.Sin(animation.WalkPhase)) * 0.07f;

        // The lunge goes out fast and comes back slow.
        if (animation.Lunge > 0f)
        {
            var t = 1f - animation.Lunge / LungeSeconds;
            var reach = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
            position.X += animation.Facing.X * reach * 0.85f;
            position.Z += animation.Facing.Z * reach * 0.85f;
        }

        // The recoil goes the other way.
        if (animation.Recoil > 0f)
        {
            var push = animation.Recoil / RecoilSeconds * 0.4f;
            position.X -= animation.Facing.X * push;
            position.Z -= animation.Facing.Z * push;
        }

        return position;
    }

    /// <summary>A struck enemy dips; a lunging one rises onto its toes.</summary>
    public float DrawHeightOf(Enemy enemy)
    {
        if (!_animation.TryGetValue(enemy, out var animation)) return FigureHeight;

        var lunge = animation.Lunge > 0f ? 0.06f * (animation.Lunge / LungeSeconds) : 0f;
        var recoil = animation.Recoil > 0f ? -0.09f * (animation.Recoil / RecoilSeconds) : 0f;
        return FigureHeight * (1f + lunge + recoil);
    }

    /// <summary>True while an enemy is committed to a swing — the moment to be guarding.</summary>
    public bool IsLunging(Enemy enemy) =>
        _animation.TryGetValue(enemy, out var animation) && animation.Lunge > 0f;

    private EnemyAnimation AnimationOf(Enemy enemy)
    {
        if (_animation.TryGetValue(enemy, out var existing)) return existing;

        var created = new EnemyAnimation();
        _animation[enemy] = created;
        return created;
    }

    /// <summary>Flat unit direction from one point to another. Zero when they coincide.</summary>
    private static WorldPoint Direction(WorldPoint from, WorldPoint to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        var length = MathF.Sqrt(dx * dx + dz * dz);
        return length < 0.001f ? default : new WorldPoint(dx / length, 0f, dz / length);
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
