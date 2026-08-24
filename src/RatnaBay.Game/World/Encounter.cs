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

    /// <summary>How far a landed blow shoves an enemy, in metres.</summary>
    private const float KnockbackMetres = 0.85f;

    /// <summary>Roughly a body's width, for sliding an enemy along a wall.</summary>
    private const float BodyRadius = 0.42f;

    private readonly List<Enemy> _enemies = new();
    private readonly Dictionary<Enemy, float> _hitFlash = new();
    private readonly Dictionary<Enemy, EnemyAnimation> _animation = new();
    private readonly GameSession _session;

    /// <summary>
    /// The walls, so enemies have to go round them.
    ///
    /// Without this an enemy walked straight at the player through whatever was in the way,
    /// which in a mine of small rooms meant bandits stepping out of solid rock. It is the same
    /// swept mover the player uses, so an enemy slides along a wall rather than sticking to it.
    /// </summary>
    private StaticCollisionIndex? _collision;

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

    /// <summary>Give the fight the world to walk around. Without it enemies ignore geometry.</summary>
    public void UseCollision(StaticCollisionIndex collision) => _collision = collision;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    /// <summary>Seconds left on the red screen edge. Zero when the player is not being hurt.</summary>
    public float DamageFlash { get; private set; }

    /// <summary>What the crosshair is currently over, for the enemy health bar.</summary>
    public Enemy? Focused { get; private set; }

    private WorldPoint _lastPlayerPosition;
    private float _lastPlayerYaw;

    /// <summary>The visual half of the fight: markers, numbers and damage direction.</summary>
    public CombatFeedback Feedback { get; } = new();

    /// <summary>An enemy went down: what it was, and what level it was at.</summary>
    public event Action<Enemy>? EnemyDefeated;

    /// <summary>A cast bolt arrived: what it was, what it hit, and how far it flew.</summary>
    public event Action<SpellDefinition, Enemy, float>? SpellLanded;

    /// <summary>The player took a blow: how much landed, and whether it was guarded.</summary>
    public event Action<float, bool>? PlayerStruck;

    private readonly List<SpellBolt> _bolts = new();
    private readonly List<EnemyShot> _shots = new();

    /// <summary>
    /// An arrow in flight.
    ///
    /// Ranged damage is delivered by something the player can see coming, not applied the
    /// instant an enemy decides to shoot. A hit from fifteen metres with no travel reads as
    /// the game cheating; a shaft crossing the room reads as being shot at, and can be broken
    /// by moving. It is also the only honest way to make standing still expensive.
    /// </summary>
    private sealed class EnemyShot
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Damage;
        public float Remaining;
    }

    /// <summary>Arrows currently in the air, for drawing.</summary>
    public IEnumerable<(Vector3 Position, float Spin)> Shots =>
        _shots.Select(shot => (shot.Position, shot.Remaining * 7f));

    /// <summary>How near an arrow must pass to count as a hit.</summary>
    private const float ShotRadius = 0.75f;

    private const float ShotSpeed = 19f;
    private const float ShotLifetime = 2.6f;

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

    /// <summary>
    /// Every fight the level file asks for.
    ///
    /// Generated mines place their own enemies, so this is the path that matters now: the
    /// manifest names an archetype and a level, and the catalogue turns that into statistics.
    /// A spawn naming an enemy that no longer exists is skipped rather than fatal, so an old
    /// saved mine loses one fight instead of failing to load.
    /// </summary>
    /// <summary>
    /// Fill the scene from the level file.
    ///
    /// With <paramref name="deferToRooms"/> a room's occupants are held back until the player
    /// walks into it. Four recorded runs cleared nearly every room in the instant they entered
    /// it, because a room's fight could be taken from the doorway of the room before — nothing
    /// about a room mattered except the gap you shot through. Holding them back means there is
    /// nothing to shoot at until you are inside, and a preta that rises when the room is
    /// disturbed is what the fiction said was happening anyway.
    /// </summary>
    public int SpawnFrom(WorldManifest manifest, bool deferToRooms = false)
    {
        var spawned = 0;
        _pending.Clear();

        foreach (var spawn in manifest.Spawns ?? new List<WorldEnemySpawn>())
        {
            if (EnemyCatalog.Resolve(spawn) is null) continue;

            if (deferToRooms && spawn.RoomIndex > 0)
            {
                if (!_pending.TryGetValue(spawn.RoomIndex, out var waiting))
                    waiting = _pending[spawn.RoomIndex] = new List<WorldEnemySpawn>();

                waiting.Add(spawn);
                spawned++;
                continue;
            }

            Wake(spawn);
            spawned++;
        }

        return spawned;
    }

    /// <summary>Enemies waiting for the room they are in to be walked into.</summary>
    private readonly Dictionary<int, List<WorldEnemySpawn>> _pending = new();

    /// <summary>Somebody has entered: whatever was waiting in there stands up.</summary>
    public int AwakenRoom(int roomIndex)
    {
        if (!_pending.Remove(roomIndex, out var waiting)) return 0;

        foreach (var spawn in waiting) Wake(spawn);
        return waiting.Count;
    }

    private void Wake(WorldEnemySpawn spawn)
    {
        var archetype = EnemyCatalog.Resolve(spawn);
        if (archetype is null) return;

        Spawn(archetype, new Vector3(spawn.Position.X, spawn.Position.Y, spawn.Position.Z),
            spawn.Id);
    }

    /// <summary>Two bandits waiting at the far end of the third room.</summary>
    public void SpawnDefaultCamp()
    {
        var bandit = EnemyCatalog.Find(EnemyCatalog.BanditId)!;

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
        UpdateShots(deltaSeconds, playerPosition, playerYaw);

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

                case EnemyIntent.Withdraw:
                    Retreat(enemy, player, deltaSeconds);
                    animation.WalkPhase += deltaSeconds * enemy.CurrentMoveSpeed * 2.4f;
                    break;

                case EnemyIntent.Attack:
                    var damage = enemy.Attack();
                    if (damage <= 0f) break;

                    animation.Lunge = LungeSeconds;
                    animation.Facing = Direction(enemy.Position, player);

                    // A shooter looses something the player can watch arrive and step out of.
                    if (enemy.Archetype.IsRanged)
                    {
                        Loose(enemy, playerPosition, damage);
                        break;
                    }

                    var guarded = _session.Player.Combat.IsBlocking;
                    var landed = _session.Player.Combat.TakeHit(damage);
                    PlayerStruck?.Invoke(landed, guarded);
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

    /// <summary>
    /// Give ground, keeping the player in front.
    ///
    /// Backing into a wall is fine — it is stopped by the same mover as everything else, and a
    /// shooter cornered against the rock is exactly the moment the player has earned.
    /// </summary>
    private void Retreat(Enemy enemy, WorldPoint player, float deltaSeconds)
    {
        var away = Direction(player, enemy.Position);
        var step = enemy.CurrentMoveSpeed * 0.8f * deltaSeconds;

        var next = Nudge(enemy.Position, away.X * step, away.Z * step);
        foreach (var other in _enemies)
        {
            if (ReferenceEquals(other, enemy) || !other.IsAlive) continue;
            if (next.FlatDistanceTo(other.Position) < Separation) return;
        }

        enemy.Position = next;
    }

    /// <summary>Loose an arrow at where the player is standing now.</summary>
    private void Loose(Enemy enemy, Vector3 playerPosition, float damage)
    {
        var from = new Vector3(enemy.Position.X, enemy.Position.Y + FigureHeight * 0.62f,
            enemy.Position.Z);
        var to = new Vector3(playerPosition.X, playerPosition.Y - 0.35f, playerPosition.Z);
        var direction = to - from;

        if (direction.LengthSquared() < 0.0001f) return;
        direction.Normalize();

        _shots.Add(new EnemyShot
        {
            Position = from,
            // No homing. An arrow is aimed where the player was, so moving beats it.
            Velocity = direction * ShotSpeed,
            Damage = damage,
            Remaining = ShotLifetime
        });
    }

    /// <summary>Move every arrow, and resolve the ones that arrive.</summary>
    private void UpdateShots(float deltaSeconds, Vector3 playerPosition, float playerYaw)
    {
        var player = new WorldPoint(playerPosition.X, playerPosition.Y, playerPosition.Z);

        for (var index = _shots.Count - 1; index >= 0; index--)
        {
            var shot = _shots[index];
            shot.Remaining -= deltaSeconds;

            var from = shot.Position;
            shot.Position += shot.Velocity * deltaSeconds;

            var origin = new WorldPoint(from.X, from.Y, from.Z);
            var at = new WorldPoint(shot.Position.X, shot.Position.Y, shot.Position.Z);

            // Stopped by the world, so a doorway is cover rather than a firing slit.
            if (_collision is not null && _collision.RaycastBlocked(origin, at, out _))
            {
                _shots.RemoveAt(index);
                continue;
            }

            // Swept against the whole step, not just its endpoint.
            //
            // An arrow moves nearly a metre a frame, so testing only where it ended up let it
            // step clean through a body: at fifteen metres the shot simply never landed. This
            // is the same mistake as timing a doorway with an infinitely thin ray, and it
            // fails the same silent way — everything looks fine, nothing connects.
            if (DistanceToSegment(playerPosition, from, shot.Position) <= ShotRadius)
            {
                _shots.RemoveAt(index);

                var guarded = _session.Player.Combat.IsBlocking;
                var landed = _session.Player.Combat.TakeHit(shot.Damage);
                PlayerStruck?.Invoke(landed, guarded);
                Feedback.PlayerHurt(landed,
                    Targeting.RelativeBearing(player, playerYaw, origin), guarded);
                DamageFlash = DamageFlashSeconds;
                continue;
            }

            if (shot.Remaining <= 0f) _shots.RemoveAt(index);
        }
    }

    /// <summary>Nearest approach of a travelling point to a standing one.</summary>
    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var along = to - from;
        var lengthSquared = along.LengthSquared();
        if (lengthSquared < 0.000001f) return Vector3.Distance(point, from);

        var t = MathHelper.Clamp(Vector3.Dot(point - from, along) / lengthSquared, 0f, 1f);
        return Vector3.Distance(point, from + along * t);
    }

    /// <summary>Slide toward the player, stopping short of anything already standing there.</summary>
    private void Advance(Enemy enemy, WorldPoint player, float deltaSeconds)
    {
        var dx = player.X - enemy.Position.X;
        var dz = player.Z - enemy.Position.Z;
        var distance = MathF.Sqrt(dx * dx + dz * dz);
        if (distance < 0.001f) return;

        var step = enemy.CurrentMoveSpeed * deltaSeconds;
        var next = Nudge(enemy.Position, dx / distance * step, dz / distance * step);

        // Without this the whole camp converges into a single overlapping sprite.
        foreach (var other in _enemies)
        {
            if (ReferenceEquals(other, enemy) || !other.IsAlive) continue;
            if (next.FlatDistanceTo(other.Position) < Separation) return;
        }

        enemy.Position = next;
    }

    /// <summary>
    /// Move a body by a step, stopped and slid by the world.
    ///
    /// The mover works on eye height with the feet a body below, so an enemy standing at ground
    /// level is lifted to the same frame of reference before being swept and put back.
    /// </summary>
    private WorldPoint Nudge(WorldPoint position, float dx, float dz)
    {
        if (_collision is null)
            return new WorldPoint(position.X + dx, position.Y, position.Z + dz);

        var standing = new WorldPoint(position.X, position.Y + FigureHeight, position.Z);
        var moved = _collision.Move(standing, new WorldPoint(dx, 0f, dz), BodyRadius, FigureHeight);
        return new WorldPoint(moved.X, position.Y, moved.Z);
    }

    /// <summary>
    /// Swing at whatever the crosshair is over. Returns what happened so the caller can say
    /// so on screen.
    /// </summary>
    /// <summary>Where the player was standing on the last tick, for measuring range.</summary>
    public WorldPoint PlayerPosition => _lastPlayerPosition;

    /// <summary>Metres to the nearest living enemy, or -1 when the room is empty.</summary>
    public float NearestEnemyRange()
    {
        var nearest = -1f;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;
            var distance = _lastPlayerPosition.FlatDistanceTo(enemy.Position);
            if (nearest < 0f || distance < nearest) nearest = distance;
        }

        return nearest;
    }

    public AttackOutcome PlayerAttack()
    {
        var target = Focused;
        var outcome = _session.Player.Combat.TryAttack(target);

        if (outcome.Result == AttackResult.Hit && target is not null)
        {
            Struck(target);
            Knock(target, KnockbackMetres);
            Feedback.PlayerHit(target.Position, outcome.Damage, !target.IsAlive);
        }

        return outcome;
    }

    /// <summary>
    /// Shove a struck enemy away from the player.
    ///
    /// The recoil animation only ever offset where the sprite was drawn, so a blow looked like
    /// it landed but changed nothing about the fight. Actually displacing the body is what
    /// makes a swing feel like it weighs something — and it buys back a fraction of a second
    /// of distance, which is the difference between trading blows and controlling a fight.
    /// </summary>
    private void Knock(Enemy enemy, float metres)
    {
        if (!enemy.IsAlive || metres <= 0f) return;

        var away = Direction(_lastPlayerPosition, enemy.Position);
        enemy.Position = Nudge(enemy.Position, away.X * metres, away.Z * metres);
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
        SpellLanded?.Invoke(bolt.Spell, hit, _lastPlayerPosition.FlatDistanceTo(hit.Position));
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
        EnemyDefeated?.Invoke(enemy);

        if (!enemy.Archetype.DropsLoot) return;
        _session.Player.Inventory.Add("bandit_loot", "Bandit Satchel", 1, "loot");
        _session.Player.Vitals.AddGold(Random.Shared.Next(5, 18));
    }
}
