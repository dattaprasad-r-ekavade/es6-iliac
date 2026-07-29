using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the Kessil Bay map (Halbrand + Sarrakh) into the Main scene.
/// </summary>
public static class BuildKessilWorld
{
    private const string NaturePath = "Assets/ThirdParty/Kenney/NatureKit";
    private const string CastlePath = "Assets/ThirdParty/Kenney/CastleKit";
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string MaterialsPath = "Assets/Art/Materials";

    [MenuItem("Kessil/World/Build Kessil Bay Map (Halbrand + Sarrakh)")]
    public static void BuildKessil()
    {
        AssetDatabase.Refresh();
        EnsureFolders();

        var ocean = GetOrCreateLit($"{MaterialsPath}/M_Ocean.mat", new Color(0.08f, 0.28f, 0.48f), 0.0f, 0.9f);
        var halbrand = GetOrCreateLit($"{MaterialsPath}/M_Halbrand.mat", new Color(0.27f, 0.48f, 0.24f), 0f, 0.7f);
        var sarrakh = GetOrCreateLit($"{MaterialsPath}/M_Sarrakh.mat", new Color(0.72f, 0.55f, 0.32f), 0f, 0.65f);
        var sand = GetOrCreateLit($"{MaterialsPath}/M_Sand.mat", new Color(0.84f, 0.74f, 0.52f), 0f, 0.8f);
        var city = GetOrCreateLit($"{MaterialsPath}/M_CityStone.mat", new Color(0.55f, 0.52f, 0.48f), 0.05f, 0.7f);
        var mountain = GetOrCreateLit($"{MaterialsPath}/M_Mountain.mat", new Color(0.42f, 0.43f, 0.4f), 0.05f, 0.55f);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        var light = new GameObject("Directional Light");
        var lightComp = light.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.color = new Color(1f, 0.95f, 0.85f);
        lightComp.intensity = 1.25f;
        light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        var world = new GameObject("WorldRoot");
        var gen = world.AddComponent<KessilWorldGenerator>();

        var trees = LoadModels(NaturePath, n =>
            n.StartsWith("tree_") &&
            (n.Contains("pine") || n.Contains("oak") || n.Contains("default") || n.Contains("tall") || n.Contains("simple") || n.Contains("detailed")));
        var desert = LoadModels(NaturePath, n =>
            n.StartsWith("cactus") || n.StartsWith("rock_") || n.StartsWith("stone_tall") || n.StartsWith("plant_bush"));
        var rocks = LoadModels(NaturePath, n => n.StartsWith("rock_") || n.StartsWith("stone_large") || n.StartsWith("cliff_"));
        var buildings = LoadModels(CastlePath, n =>
            n.StartsWith("wall") || n.StartsWith("tower-square") || n.Contains("roof") || n == "tower-base");
        var towers = LoadModels(CastlePath, n =>
            n.StartsWith("tower-") || n == "tower-top" || n.Contains("hexagon"));

        trees = trees.Take(14).ToArray();
        desert = desert.Take(12).ToArray();
        rocks = rocks.Take(10).ToArray();
        buildings = buildings.Take(16).ToArray();
        towers = towers.Take(8).ToArray();

        var so = new SerializedObject(gen);
        so.FindProperty("propSeed").intValue = 4242;
        so.FindProperty("waterSize").floatValue = 1400f;
        so.FindProperty("spawnPlayer").boolValue = true;
        so.FindProperty("oceanMaterial").objectReferenceValue = ocean;
        so.FindProperty("halbrandMaterial").objectReferenceValue = halbrand;
        so.FindProperty("sarrakhMaterial").objectReferenceValue = sarrakh;
        so.FindProperty("sandMaterial").objectReferenceValue = sand;
        so.FindProperty("cityMaterial").objectReferenceValue = city;
        so.FindProperty("mountainMaterial").objectReferenceValue = mountain;
        AssignArray(so, "treePrefabs", trees);
        AssignArray(so, "desertPrefabs", desert);
        AssignArray(so, "rockPrefabs", rocks);
        AssignArray(so, "buildingPrefabs", buildings);
        AssignArray(so, "towerPrefabs", towers);
        so.ApplyModifiedPropertiesWithoutUndo();

        gen.GenerateWorld();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        var player = GameObject.Find("Player");
        if (player != null)
        {
            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        Debug.Log("[BuildKessilWorld] Kessil Bay map ready — Halbrand (N), Sarrakh (S), Caldemar/Estmere/Qadris, Tolm/Corrath/Sarn. Press Play at Caldemar.");
    }

    [MenuItem("Kessil/World/Build Island Map")]
    public static void BuildLegacyRandomIslands()
    {
        // Keep old entry pointing to Kessil Bay — better default.
        BuildKessil();
    }

    [MenuItem("Kessil/World/Rebuild With New Seed")]
    public static void RebuildRandomProps()
    {
        var gen = Object.FindAnyObjectByType<KessilWorldGenerator>();
        if (gen == null)
        {
            BuildKessil();
            return;
        }

        var so = new SerializedObject(gen);
        so.FindProperty("propSeed").intValue = Random.Range(1000, 99999);
        so.ApplyModifiedPropertiesWithoutUndo();
        gen.GenerateWorld();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BuildKessilWorld] Rebuilt Kessil Bay props with seed {so.FindProperty("propSeed").intValue}");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
    }

    private static Material GetOrCreateLit(string path, Color color, float metallic, float smoothness)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (existing == null)
        {
            existing = new Material(shader);
            AssetDatabase.CreateAsset(existing, path);
        }

        existing.shader = shader;
        if (existing.HasProperty("_BaseColor")) existing.SetColor("_BaseColor", color);
        else existing.color = color;
        if (existing.HasProperty("_Metallic")) existing.SetFloat("_Metallic", metallic);
        if (existing.HasProperty("_Smoothness")) existing.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static GameObject[] LoadModels(string folder, System.Func<string, bool> predicate)
    {
        var guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        var list = new List<GameObject>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!predicate(name)) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }

        return list.OrderBy(g => g.name).ToArray();
    }

    private static void AssignArray(SerializedObject so, string propName, GameObject[] values)
    {
        var prop = so.FindProperty(propName);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
