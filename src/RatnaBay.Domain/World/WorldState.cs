namespace RatnaBay.Domain;

/// <summary>
/// Persistent world facts that aren't part of the player: which one-off enemies
/// have been killed, and whether the world's content has been spawned this session.
///
/// Without this, loading a save re-ran the content spawner and every bandit the
/// player had already cleared was standing there again.
///
/// This is deliberately an instance rather than static state. A save slot owns one,
/// and a test can construct one without leaking kills into the next test.
/// </summary>
public sealed class WorldState
{
    private readonly HashSet<string> _killed = new(StringComparer.Ordinal);

    public bool IsKilled(string? spawnId) =>
        !string.IsNullOrEmpty(spawnId) && _killed.Contains(spawnId);

    public void MarkKilled(string? spawnId)
    {
        if (!string.IsNullOrEmpty(spawnId)) _killed.Add(spawnId);
    }

    /// <summary>Ordered so a save file is stable and diffable across writes.</summary>
    public IReadOnlyList<string> GetKilledIds()
    {
        var ids = new List<string>(_killed);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    public void LoadKilled(IEnumerable<string>? ids)
    {
        _killed.Clear();
        if (ids is null) return;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) _killed.Add(id);
    }

    /// <summary>Wipe on a fresh game.</summary>
    public void Reset() => _killed.Clear();
}
