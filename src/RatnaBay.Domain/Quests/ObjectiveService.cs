namespace RatnaBay.Domain;

/// <summary>The objective as written to a save. The bearing is regenerated, never stored.</summary>
public sealed class SavedObjective
{
    public required string Title { get; init; }
    public string Directions { get; init; } = string.Empty;
    public string TargetAnchorId { get; init; } = string.Empty;
    public float TargetX { get; init; }
    public float TargetY { get; init; }
    public float TargetZ { get; init; }
    public bool HasTarget { get; init; }
}

/// <summary>
/// The current objective, expressed as written directions rather than a marker.
///
/// Navigation is directions-first, with markers derived from the same target data. Directions
/// are authored; the bearing line is generated from the player's own position, so a direction
/// is never stale and never has to be hand-written per approach — which is the failure mode
/// that makes Morrowind quests need a wiki.
/// </summary>
public sealed class ObjectiveService
{
    /// <summary>A pace is about three quarters of a metre.</summary>
    private const float MetresPerPace = 0.75f;

    /// <summary>Inside this many metres, a bearing is noise.</summary>
    public const float ArrivalRadius = 14f;

    public string? Title { get; private set; }
    public string? Directions { get; private set; }

    /// <summary>Anchor the objective points at, or empty when the objective is not a place.</summary>
    public string TargetAnchorId { get; private set; } = string.Empty;

    /// <summary>Where the anchor is, once the game layer has resolved it.</summary>
    public WorldPoint? TargetPosition { get; private set; }

    public bool HasObjective => !string.IsNullOrEmpty(Title);

    public event Action? Changed;

    public void Set(string title, string directions, string? targetAnchorId = null,
        WorldPoint? targetPosition = null)
    {
        Title = title;
        Directions = directions;
        TargetAnchorId = targetAnchorId ?? string.Empty;
        TargetPosition = targetPosition;
        Changed?.Invoke();
    }

    /// <summary>Called by the game layer once it knows where the anchor resolved to.</summary>
    public void SetTargetPosition(WorldPoint? position)
    {
        TargetPosition = position;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Title = null;
        Directions = null;
        TargetAnchorId = string.Empty;
        TargetPosition = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// A live bearing line — "north-west, about 300 paces". This is what replaces a marker,
    /// and it is generated rather than authored so it cannot go stale when a target moves.
    /// </summary>
    public string BearingLine(WorldPoint playerPosition)
    {
        if (TargetPosition is not { } target) return string.Empty;

        var distance = playerPosition.FlatDistanceTo(target);
        if (distance < ArrivalRadius) return "You are here.";

        var paces = (int)MathF.Round(distance / MetresPerPace);
        return $"{playerPosition.CompassTo(target)}, about {paces} paces.";
    }

    public SavedObjective? Capture() => HasObjective
        ? new SavedObjective
        {
            Title = Title!,
            Directions = Directions ?? string.Empty,
            TargetAnchorId = TargetAnchorId,
            HasTarget = TargetPosition is not null,
            TargetX = TargetPosition?.X ?? 0f,
            TargetY = TargetPosition?.Y ?? 0f,
            TargetZ = TargetPosition?.Z ?? 0f
        }
        : null;

    /// <summary>Restore from a save. A null objective clears whatever was showing.</summary>
    public void Restore(SavedObjective? saved)
    {
        if (saved is null)
        {
            Clear();
            return;
        }

        Set(saved.Title, saved.Directions, saved.TargetAnchorId,
            saved.HasTarget ? new WorldPoint(saved.TargetX, saved.TargetY, saved.TargetZ) : null);
    }

    /// <summary>True once the player is standing at the objective's target.</summary>
    public bool PlayerHasArrived(WorldPoint playerPosition, float radius = ArrivalRadius) =>
        TargetPosition is { } target && playerPosition.FlatDistanceTo(target) <= radius;
}
