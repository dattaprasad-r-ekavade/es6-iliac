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

    /// <summary>
    /// Forget everything killed in one place.
    ///
    /// A mine is rebuilt from its seed on every descent, so what died in it last time must not
    /// still be dead this time. Succession sends a successor back into the mine that killed
    /// their predecessor, and without this they walked into rooms that were already empty:
    /// eight rooms cleared for five kills, thirty-six stones banked, and a cache mechanic
    /// turned into free money.
    ///
    /// An authored place is different and keeps its dead — a bandit cleared off a road stays
    /// cleared, which is what makes progress through a hand-made world mean anything.
    /// </summary>
    public int ForgetKilledIn(string? locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId)) return 0;

        return _killed.RemoveWhere(id => id.StartsWith(locationId, StringComparison.Ordinal));
    }
}
