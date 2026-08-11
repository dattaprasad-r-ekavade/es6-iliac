using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InvItem
{
    public string Id;
    public string Name;
    public int Count;
    public string Kind;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }
    public readonly List<InvItem> Items = new();
    public event Action OnChanged;

    private void Awake()
    {
        Instance = this;
        if (Items.Count != 0) return;
        Add("iron_sword", "Iron Sword", 1, "weapon");
        Add("health_potion", "Health Potion", 3, "potion");
        Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 3, SoulCrystals.ItemKind);
        Add("torch", "Torch", 1, "misc");
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Add(string id, string name, int count, string kind)
    {
        var existing = Items.Find(i => i.Id == id);
        if (existing != null) existing.Count += count;
        else Items.Add(new InvItem { Id = id, Name = name, Count = count, Kind = kind });
        if (Time.frameCount > 2) GameSfx.Instance?.PlayPickup();
        OnChanged?.Invoke();
    }

    /// <summary>Stack size held for an item id, or zero.</summary>
    public int CountOf(string id)
    {
        var existing = Items.Find(i => i.Id == id);
        return existing != null ? existing.Count : 0;
    }

    public bool Consume(string id, int count = 1)
    {
        var existing = Items.Find(i => i.Id == id);
        if (existing == null || existing.Count < count) return false;
        existing.Count -= count;
        if (existing.Count <= 0) Items.Remove(existing);
        OnChanged?.Invoke();
        return true;
    }

    public void UseHotPotion()
    {
        if (!Consume("health_potion"))
        {
            GameHud.Instance?.ShowToast("No health potions");
            return;
        }
        PlayerStats.Instance?.Heal(40f);
        GameHud.Instance?.ShowToast("Used Health Potion");
    }
}
