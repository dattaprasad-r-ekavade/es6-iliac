using System;
using UnityEngine;

/// <summary>
/// The current objective, expressed as written directions rather than a marker.
///
/// GAMEPLAY_DESIGN.md locks navigation as directions-first with markers derived from the same
/// target data and defaulting to an approximate Area setting. Directions are authored here;
/// the marker layer reads <see cref="TargetPosition"/> when the player asks for it.
///
/// The compass line is generated from the player's own position, so a direction is never
/// stale and never has to be hand-written per approach — which is the failure mode that makes
/// Morrowind quests need a wiki.
/// </summary>
public sealed class ObjectiveService : MonoBehaviour
{
    public static ObjectiveService Instance { get; private set; }

    public string Title { get; private set; }
    public string Directions { get; private set; }

    /// <summary>Anchor the objective points at, or empty when the objective is not a place.</summary>
    public string TargetAnchorId { get; private set; }

    public bool HasObjective => !string.IsNullOrEmpty(Title);

    public event Action Changed;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Set(string title, string directions, string targetAnchorId = null)
    {
        Title = title;
        Directions = directions;
        TargetAnchorId = targetAnchorId ?? string.Empty;
        Changed?.Invoke();
        if (!string.IsNullOrEmpty(title)) GameHud.Instance?.ShowToast($"Objective: {title}");
    }

    public void Clear()
    {
        Title = null;
        Directions = null;
        TargetAnchorId = string.Empty;
        Changed?.Invoke();
    }

    /// <summary>World position of the objective, when it has one.</summary>
    public Vector3? TargetPosition
    {
        get
        {
            var anchor = CapitalRegion.FindAnchor(TargetAnchorId);
            return anchor?.Position;
        }
    }

    /// <summary>
    /// A live bearing line — "north-west, about 300 paces". This is what replaces a marker,
    /// and it is generated rather than authored so it cannot go stale when a target moves.
    /// </summary>
    public string BearingLine()
    {
        var target = TargetPosition;
        if (target == null || !PlayerRef.TryGet(out var player)) return string.Empty;

        var delta = target.Value - player.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 12f) return "You are here.";

        return $"{Compass(delta)}, about {Mathf.RoundToInt(distance / 0.75f)} paces.";
    }

    /// <summary>Eight-point compass. Finer than that is precision the player cannot use.</summary>
    private static string Compass(Vector3 delta)
    {
        float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        int index = Mathf.RoundToInt(angle / 45f) % 8;
        return points[index];
    }

    /// <summary>True once the player is standing at the objective's target.</summary>
    public bool PlayerHasArrived(float radius = 14f)
    {
        var target = TargetPosition;
        if (target == null || !PlayerRef.TryGet(out var player)) return false;

        var delta = target.Value - player.position;
        delta.y = 0f;
        return delta.magnitude <= radius;
    }
}
