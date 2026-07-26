using UnityEngine;

/// <summary>
/// Cached access to the player transform.
///
/// Eight scripts used to call <c>GameObject.Find("Player")</c>, several of them
/// every frame and one of them on all ~640 foliage props. Find walks the whole
/// hierarchy and is not free at this scene size. This resolves once, survives
/// scene reloads (the reference goes null and is re-resolved), and throttles
/// lookups to at most one per frame while the player genuinely doesn't exist.
/// </summary>
public static class PlayerRef
{
    private const string PlayerObjectName = "Player";

    private static Transform _player;
    private static int _lastSearchFrame = -1;

    /// <summary>The player transform, or null if there isn't one yet.</summary>
    public static Transform Transform
    {
        get
        {
            // Unity's overloaded == catches destroyed objects after a scene reload.
            if (_player != null) return _player;

            if (_lastSearchFrame == Time.frameCount) return null;
            _lastSearchFrame = Time.frameCount;

            var go = GameObject.Find(PlayerObjectName);
            _player = go != null ? go.transform : null;
            return _player;
        }
    }

    public static bool TryGet(out Transform player)
    {
        player = Transform;
        return player != null;
    }

    /// <summary>Position, or <see cref="Vector3.zero"/> when there is no player.</summary>
    public static Vector3 Position
    {
        get
        {
            var t = Transform;
            return t != null ? t.position : Vector3.zero;
        }
    }

    /// <summary>Register a freshly spawned player so nothing has to search for it.</summary>
    public static void Set(Transform player)
    {
        _player = player;
    }

    public static void Clear()
    {
        _player = null;
        _lastSearchFrame = -1;
    }
}
