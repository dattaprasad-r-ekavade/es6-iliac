using UnityEngine;

/// <summary>
/// Authored, rename-safe definition for a persistent friendly NPC spawn.
/// Position is relative to a stable WorldLayout site id so geography can move
/// without requiring code changes or invalidating save-facing identifiers.
/// </summary>
[CreateAssetMenu(menuName = "Kessil/NPC Archetype", fileName = "NpcArchetype")]
public sealed class NpcArchetype : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private string modelId;
    [SerializeField] private string anchorSiteId;
    [SerializeField] private Vector3 offset;
    [SerializeField] private Color tint = Color.white;
    [SerializeField, TextArea] private string[] lines;
    [SerializeField] private bool merchant;
    [SerializeField] private bool questGiver;

    public string Id => id;
    public string DisplayName => displayName;
    public string ModelId => modelId;
    public string AnchorSiteId => anchorSiteId;
    public Vector3 Offset => offset;
    public Color Tint => tint;
    public string[] Lines => lines;
    public bool Merchant => merchant;
    public bool QuestGiver => questGiver;
}
