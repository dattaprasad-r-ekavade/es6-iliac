using System.Collections.Generic;

/// <summary>
/// Persistent world facts that aren't part of the player: which one-off enemies
/// have been killed, and whether the world's content has been spawned this session.
///
/// Without this, loading a save re-ran the bootstrap spawner and every bandit the
/// player had already cleared was standing there again.
/// </summary>
public static class WorldState
{
    private static readonly HashSet<string> Killed = new();

    public static bool IsKilled(string spawnId) =>
        !string.IsNullOrEmpty(spawnId) && Killed.Contains(spawnId);

    public static void MarkKilled(string spawnId)
    {
        if (!string.IsNullOrEmpty(spawnId)) Killed.Add(spawnId);
    }

    public static List<string> GetKilledIds() => new(Killed);

    public static void LoadKilled(IEnumerable<string> ids)
    {
        Killed.Clear();
        if (ids == null) return;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) Killed.Add(id);
    }

    /// <summary>Wipe on a fresh game / scene reload.</summary>
    public static void Reset() => Killed.Clear();
}
