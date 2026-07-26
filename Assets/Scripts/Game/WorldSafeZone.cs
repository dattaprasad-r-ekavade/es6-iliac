using UnityEngine;

/// <summary>
/// Daggerfall starter area — enemies do not aggro here.
/// Geometry lives in <see cref="WorldLayout"/>; this used to be one of two
/// safe-zone definitions that disagreed on the radius.
/// </summary>
public static class WorldSafeZone
{
    public static bool Contains(Vector3 pos) => WorldLayout.IsInSafeZone(pos);
}
