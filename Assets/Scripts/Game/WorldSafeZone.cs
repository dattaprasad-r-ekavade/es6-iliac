using UnityEngine;

/// <summary>
/// Daggerfall starter area — enemies do not aggro here.
/// </summary>
public static class WorldSafeZone
{
    private static readonly Vector3 DaggerfallCenter = new(-2000f, 0f, 1450f);
    private const float SafeRadius = 400f;

    public static bool Contains(Vector3 pos)
    {
        var d = new Vector2(pos.x - DaggerfallCenter.x, pos.z - DaggerfallCenter.z);
        return d.sqrMagnitude <= SafeRadius * SafeRadius;
    }
}
