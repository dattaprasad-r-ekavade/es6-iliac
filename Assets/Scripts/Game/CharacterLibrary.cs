using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads Kenney MiniCharacters human models only (no aids / wheelchairs).
/// </summary>
public static class CharacterLibrary
{
    private static GameObject[] _humans;
    private static readonly Dictionary<string, GameObject> _byName = new(StringComparer.OrdinalIgnoreCase);

    public static void EnsureLoaded()
    {
        if (_humans != null) return;

        var list = new List<GameObject>();
        foreach (var g in Resources.LoadAll<GameObject>("Characters"))
        {
            if (g == null) continue;
            var n = g.name;
            if (!IsHumanModelName(n)) continue;
            list.Add(g);
            _byName[n] = g;
        }

        _humans = list.ToArray();
    }

    public static bool IsHumanModelName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var n = name.ToLowerInvariant();
        return (n.StartsWith("character-male-") || n.StartsWith("character-female-"))
               && !n.Contains("aid") && !n.Contains("wheelchair");
    }

    public static GameObject Instantiate(string modelId = null, float scale = 2.1f)
    {
        EnsureLoaded();
        GameObject prefab = null;
        if (!string.IsNullOrEmpty(modelId))
            _byName.TryGetValue(modelId, out prefab);
        if (prefab == null && _humans.Length > 0)
            prefab = _humans[UnityEngine.Random.Range(0, _humans.Length)];

        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab);
        go.transform.localScale = Vector3.one * scale;
        foreach (var col in go.GetComponentsInChildren<Collider>())
            UnityEngine.Object.Destroy(col);
        WorldVisualFix.FixCharacter(go);
        return go;
    }

    public static void AttachHumanVisual(Transform parent, string modelId, float scale = 2.1f)
    {
        var visual = Instantiate(modelId, scale);
        if (visual == null) return;
        visual.name = "CharacterVisual";
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
    }
}
