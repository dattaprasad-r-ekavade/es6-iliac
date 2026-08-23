using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>An axis-aligned solid in the horizontal world plane.</summary>
public readonly record struct CollisionBox(
    string Id,
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ)
{
    public float CentreX => (MinX + MaxX) * 0.5f;
    public float CentreZ => (MinZ + MaxZ) * 0.5f;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id)
        && float.IsFinite(MinX) && float.IsFinite(MinY) && float.IsFinite(MinZ)
        && float.IsFinite(MaxX) && float.IsFinite(MaxY) && float.IsFinite(MaxZ)
        && MinX < MaxX && MinY < MaxY && MinZ < MaxZ;
}

/// <summary>
/// A small static BVH and a swept, sliding player mover.
///
/// The player is treated as a horizontal capsule whose round end is conservatively expanded
/// to a box for axis-aligned movement. Each axis is swept independently, so a long frame
/// cannot tunnel through a wall and a diagonal move slides along it instead of stopping dead.
/// </summary>
public sealed class StaticCollisionIndex
{
    private const int LeafSize = 4;
    private readonly List<CollisionBox> _boxes = new();
    private Node? _root;

    public IReadOnlyList<CollisionBox> Boxes => _boxes;

    public void Rebuild(IEnumerable<CollisionBox> boxes)
    {
        _boxes.Clear();
        _boxes.AddRange(boxes.Where(box => box.IsValid));
        _root = Build(new List<CollisionBox>(_boxes));
    }

    /// <summary>True when a line segment crosses a solid, used by watcher sight.</summary>
    public bool RaycastBlocked(WorldPoint origin, WorldPoint target, out CollisionBox blocker)
    {
        blocker = default;
        if (_root is null) return false;

        var minX = MathF.Min(origin.X, target.X);
        var maxX = MathF.Max(origin.X, target.X);
        var minZ = MathF.Min(origin.Z, target.Z);
        var maxZ = MathF.Max(origin.Z, target.Z);
        var candidates = new List<CollisionBox>();
        Query(_root, minX, minZ, maxX, maxZ, candidates);

        foreach (var box in candidates)
        {
            if (SegmentIntersectsBox(origin, target, box))
            {
                blocker = box;
                return true;
            }
        }

        return false;
    }

    /// <summary>Move a player centre, stopping at solids on all three axes.</summary>
    public WorldPoint Move(WorldPoint position, WorldPoint delta, float radius, float height = 1.8f)
    {
        if (!float.IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(height) || height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(height));

        var x = MoveAxis(position.X, position.Z, position.Y, height, delta.X, radius, horizontal: true);
        var z = MoveAxis(x, position.Z, position.Y, height, delta.Z, radius, horizontal: false);
        var y = MoveVertical(x, z, position.Y, height, delta.Y, radius);
        return new WorldPoint(x, y, z);
    }

    /// <summary>
    /// Sweep the head and feet against solids.
    ///
    /// Vertical movement used to pass straight through anything: with jumping added that
    /// meant a player could rise through a ceiling and stand on top of the level. Every
    /// overhead beam, low doorway or upper floor depends on this.
    /// </summary>
    private float MoveVertical(float x, float z, float y, float height, float delta, float radius)
    {
        if (MathF.Abs(delta) < 0.000001f || _root is null) return y;

        var candidates = new List<CollisionBox>();
        Query(_root, x - radius, z - radius, x + radius, z + radius, candidates);

        var head = y;
        var feet = y - height;
        var resolved = y + delta;

        foreach (var box in candidates)
        {
            if (x < box.MinX - radius || x > box.MaxX + radius) continue;
            if (z < box.MinZ - radius || z > box.MaxZ + radius) continue;

            if (delta > 0f)
            {
                // Rising: the head stops under anything above it.
                if (box.MinY < head || box.MinY > head + delta) continue;
                resolved = MathF.Min(resolved, box.MinY - 0.0001f);
            }
            else
            {
                // Falling: the feet stop on top of anything below them.
                if (box.MaxY > feet || box.MaxY < feet + delta) continue;
                resolved = MathF.Max(resolved, box.MaxY + height + 0.0001f);
            }
        }

        return resolved;
    }

    private float MoveAxis(float x, float z, float playerY, float height, float delta,
        float radius, bool horizontal)
    {
        if (MathF.Abs(delta) < 0.000001f || _root is null)
            return horizontal ? x : z;

        var minX = horizontal ? MathF.Min(x, x + delta) - radius : x - radius;
        var maxX = horizontal ? MathF.Max(x, x + delta) + radius : x + radius;
        var minZ = horizontal ? z - radius : MathF.Min(z, z + delta) - radius;
        var maxZ = horizontal ? z + radius : MathF.Max(z, z + delta) + radius;
        var candidates = new List<CollisionBox>();
        // The query must include solids just outside the centre path: their expanded bounds
        // can still touch the player's capsule while it slides along a wall.
        Query(_root, minX - radius, minZ - radius, maxX + radius, maxZ + radius, candidates);

        var current = horizontal ? x : z;
        var target = current + delta;
        var resolved = target;
        foreach (var box in candidates)
        {
            if (box.MaxY < playerY - height || box.MinY > playerY) continue;

            var expandedMinX = box.MinX - radius;
            var expandedMaxX = box.MaxX + radius;
            var expandedMinZ = box.MinZ - radius;
            var expandedMaxZ = box.MaxZ + radius;

            if (horizontal)
            {
                if (z < expandedMinZ || z > expandedMaxZ) continue;
                resolved = Choose(resolved, Sweep(current, delta, expandedMinX, expandedMaxX), delta);
            }
            else
            {
                if (x < expandedMinX || x > expandedMaxX) continue;
                resolved = Choose(resolved, Sweep(current, delta, expandedMinZ, expandedMaxZ), delta);
            }
        }

        return resolved;
    }

    private static float Choose(float current, float candidate, float delta) =>
        delta > 0f ? MathF.Min(current, candidate) : MathF.Max(current, candidate);

    private static float Sweep(float current, float delta, float min, float max)
    {
        const float epsilon = 0.0001f;
        var target = current + delta;

        if (delta > 0f)
        {
            if (current <= min + epsilon && target > min)
                return MathF.Min(target, min - epsilon);
            return current > min && current < max ? max + epsilon : target;
        }

        if (current >= max - epsilon && target < max)
            return MathF.Max(target, max + epsilon);
        return current > min && current < max ? min - epsilon : target;
    }

    private static bool SegmentIntersectsBox(WorldPoint origin, WorldPoint target, CollisionBox box)
    {
        var tMin = 0f;
        var tMax = 1f;
        var dx = target.X - origin.X;
        var dy = target.Y - origin.Y;
        var dz = target.Z - origin.Z;

        if (!Clip(origin.X, dx, box.MinX, box.MaxX, ref tMin, ref tMax)) return false;
        if (!Clip(origin.Y, dy, box.MinY, box.MaxY, ref tMin, ref tMax)) return false;
        if (!Clip(origin.Z, dz, box.MinZ, box.MaxZ, ref tMin, ref tMax)) return false;
        return true;
    }

    private static bool Clip(float origin, float delta, float min, float max,
        ref float tMin, ref float tMax)
    {
        const float epsilon = 0.000001f;
        if (MathF.Abs(delta) < epsilon) return origin >= min && origin <= max;

        var enter = (min - origin) / delta;
        var exit = (max - origin) / delta;
        if (enter > exit) (enter, exit) = (exit, enter);
        tMin = MathF.Max(tMin, enter);
        tMax = MathF.Min(tMax, exit);
        return tMin <= tMax;
    }

    private static Node Build(List<CollisionBox> boxes)
    {
        var bounds = BoundsOf(boxes);
        if (boxes.Count <= LeafSize)
            return new Node(bounds, boxes, null, null);

        var splitOnX = bounds.Width >= bounds.Depth;
        boxes.Sort((left, right) =>
        {
            var a = splitOnX ? left.CentreX : left.CentreZ;
            var b = splitOnX ? right.CentreX : right.CentreZ;
            return a.CompareTo(b);
        });

        var middle = boxes.Count / 2;
        return new Node(bounds, null,
            Build(boxes.GetRange(0, middle)),
            Build(boxes.GetRange(middle, boxes.Count - middle)));
    }

    private static Bounds BoundsOf(List<CollisionBox> boxes)
    {
        var minX = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxZ = float.MinValue;
        foreach (var box in boxes)
        {
            minX = MathF.Min(minX, box.MinX);
            minZ = MathF.Min(minZ, box.MinZ);
            maxX = MathF.Max(maxX, box.MaxX);
            maxZ = MathF.Max(maxZ, box.MaxZ);
        }

        return new Bounds(minX, minZ, maxX, maxZ);
    }

    private static void Query(Node node, float minX, float minZ, float maxX, float maxZ,
        List<CollisionBox> results)
    {
        if (!node.Bounds.Overlaps(minX, minZ, maxX, maxZ)) return;

        if (node.Items is not null)
        {
            foreach (var box in node.Items)
            {
                if (box.MaxX >= minX && box.MinX <= maxX
                    && box.MaxZ >= minZ && box.MinZ <= maxZ)
                    results.Add(box);
            }
            return;
        }

        if (node.Left is not null) Query(node.Left, minX, minZ, maxX, maxZ, results);
        if (node.Right is not null) Query(node.Right, minX, minZ, maxX, maxZ, results);
    }

    private sealed class Node
    {
        public Node(Bounds bounds, List<CollisionBox>? items, Node? left, Node? right)
        {
            Bounds = bounds;
            Items = items;
            Left = left;
            Right = right;
        }

        public Bounds Bounds { get; }
        public List<CollisionBox>? Items { get; }
        public Node? Left { get; }
        public Node? Right { get; }
    }

    private readonly record struct Bounds(float MinX, float MinZ, float MaxX, float MaxZ)
    {
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;

        public bool Overlaps(float minX, float minZ, float maxX, float maxZ) =>
            MaxX >= minX && MinX <= maxX && MaxZ >= minZ && MinZ <= maxZ;
    }
}
