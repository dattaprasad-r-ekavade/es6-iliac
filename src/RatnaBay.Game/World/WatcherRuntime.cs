using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Runtime guards backed by authored watcher waypoints.
///
/// Detection owns suspicion and recovery. This class supplies the geometry-dependent part:
/// patrol movement, a view cone and a sight ray through the world's static collision index.
/// </summary>
public sealed class WatcherRuntime
{
    private readonly List<GuardWatcher> _watchers = new();
    private readonly Detection _detection;
    private readonly StaticCollisionIndex _collision;

    public WatcherRuntime(WorldManifest manifest, StaticCollisionIndex collision, Detection detection)
    {
        _collision = collision;
        _detection = detection;
        Reload(manifest);
    }

    public IReadOnlyList<GuardWatcher> Watchers => _watchers;

    /// <summary>Replace authored patrols without accumulating duplicate detection watchers.</summary>
    public void Reload(WorldManifest manifest)
    {
        foreach (var watcher in _watchers) _detection.Unregister(watcher);
        _watchers.Clear();

        foreach (var definition in manifest.Watchers ?? new List<WorldWatcher>())
        {
            var watcher = new GuardWatcher(definition, _collision);
            _watchers.Add(watcher);
            _detection.Register(watcher);
        }
    }

    public void Update(float deltaSeconds, WorldPoint player)
    {
        foreach (var watcher in _watchers)
        {
            watcher.SetPlayer(player);
            watcher.Update(deltaSeconds, player);
        }
    }

    public sealed class GuardWatcher : IWatcher
    {
        private readonly StaticCollisionIndex _collision;
        private int _waypointIndex;

        internal GuardWatcher(WorldWatcher definition, StaticCollisionIndex collision)
        {
            Definition = definition;
            _collision = collision;
            Position = definition.Position.ToWorldPoint();
            FacingYaw = definition.Yaw;
        }

        public WorldWatcher Definition { get; }
        public WorldPoint Position { get; private set; }
        public float FacingYaw { get; private set; }
        public bool LastSeen { get; private set; }
        public float LastVisibility { get; private set; }

        public void Update(float deltaSeconds, WorldPoint player)
        {
            if (deltaSeconds <= 0f) return;

            var waypoints = Definition.Waypoints ?? new List<WorldVector>();
            if (waypoints.Count == 0) return;

            var target = waypoints[_waypointIndex].ToWorldPoint();
            var dx = target.X - Position.X;
            var dz = target.Z - Position.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= 0.1f)
            {
                _waypointIndex = (_waypointIndex + 1) % waypoints.Count;
                return;
            }

            FacingYaw = MathF.Atan2(dx, -dz);
            var step = MathF.Min(distance, Definition.Speed * deltaSeconds);
            Position = new WorldPoint(
                Position.X + dx / distance * step,
                Position.Y,
                Position.Z + dz / distance * step);
        }

        public bool CanSeePlayer(float visibility)
        {
            LastVisibility = visibility;
            var player = _lastPlayer;
            var dx = player.X - Position.X;
            var dz = player.Z - Position.Z;
            var distance = MathF.Sqrt(dx * dx + dz * dz);
            var visibilityFactor = 0.5f + visibility * 0.5f;
            var range = Definition.ViewRange * visibilityFactor;
            if (distance > range)
            {
                LastSeen = false;
                return false;
            }

            if (distance > 0.001f)
            {
                var forward = Targeting.FlatForward(FacingYaw);
                var dot = (dx / distance) * forward.X + (dz / distance) * forward.Z;
                var coneFactor = 0.6f + visibility * 0.4f;
                var halfCone = Definition.ViewConeDegrees * MathF.PI / 360f * coneFactor;
                if (dot < MathF.Cos(halfCone))
                {
                    LastSeen = false;
                    return false;
                }
            }

            var eye = new WorldPoint(Position.X, Position.Y + 1.6f, Position.Z);
            var target = new WorldPoint(player.X, player.Y, player.Z);
            LastSeen = !_collision.RaycastBlocked(eye, target, out _);
            return LastSeen;
        }

        public void ResetView() => LastSeen = false;

        private WorldPoint _lastPlayer;

        internal void SetPlayer(WorldPoint player) => _lastPlayer = player;
    }
}
