using System.Collections.Generic;
using UnityEngine;

// NOTE: this type lives in its own file on purpose. Unity only serialises a MonoBehaviour
// into a scene when the file name matches the class name, so while PickpocketTarget shared
// PickpocketSystem.cs the component silently failed to survive scene generation - the prison
// had a pickpocket holder object with no component on it.

/// <summary>A pocket worth picking. Sits alongside <see cref="NpcInteractable"/>.</summary>
public sealed class PickpocketTarget : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float difficulty = 15f;

    [System.Serializable]
    public sealed class Holding
    {
        public string Id;
        public string Name;
        public int Count = 1;
        public string Kind = "loot";
    }

    [SerializeField] private List<Holding> holdings = new();

    public float Difficulty => difficulty;
    public int RemainingItems => holdings.Count;

    public void Configure(float lockDifficulty, params Holding[] contents)
    {
        difficulty = Mathf.Clamp(lockDifficulty, 0f, 100f);
        holdings = new List<Holding>(contents ?? System.Array.Empty<Holding>());
    }

    /// <summary>Remove and return the next holding, or null when empty.</summary>
    public Holding TakeNext()
    {
        if (holdings.Count == 0) return null;
        var item = holdings[0];
        holdings.RemoveAt(0);
        return item;
    }
}
