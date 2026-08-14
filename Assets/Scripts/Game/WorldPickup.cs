using UnityEngine;

/// <summary>
/// Something lying in the world that the player can pick up.
///
/// The project had inventory, an item catalogue and prices, and no way to acquire anything by
/// hand — every item arrived by script. So the walkable world contained nothing you could take,
/// which is most of why it read as empty regardless of how much was in it.
///
/// Interact with <b>E</b>, same verb as talking. It goes into the inventory and disappears.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class WorldPickup : MonoBehaviour
{
    [SerializeField] private string itemId = "";
    [SerializeField] private string displayName = "";
    [SerializeField] private int quantity = 1;
    [SerializeField] private string category = "loot";

    public string ItemId => itemId;
    public string DisplayName => displayName;

    public void Configure(string id, string label, int amount = 1, string itemCategory = "loot")
    {
        itemId = id;
        displayName = label;
        quantity = Mathf.Max(1, amount);
        category = itemCategory;
    }

    /// <summary>Take it. Safe to call when there is no inventory yet — nothing is lost.</summary>
    public void Take()
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            GameHud.Instance?.ShowToast("You have nowhere to put that.");
            return;
        }

        inventory.Add(itemId, displayName, quantity, category);
        GameHud.Instance?.ShowToast(quantity > 1
            ? $"Taken: {displayName} x{quantity}"
            : $"Taken: {displayName}");

        // Destroyed rather than disabled: a picked-up item that still blocks the doorway it
        // was sitting in is worse than one that vanishes.
        Destroy(gameObject);
    }
}
