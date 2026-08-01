using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Regenerates the authored Chapter 01 grey-thread spaces. These are intentionally
/// simple, collision-backed modular rooms with elevation and silhouettes so the route
/// can be blind-played before final environment art exists.
/// </summary>
public static class GreyThreadSceneBuilder
{
    public const string Folder = "Assets/Scenes/Chapter01";

    public static string[] ScenePaths
    {
        get
        {
            var paths = new List<string>();
            foreach (var spec in GreyThreadSceneCatalog.Scenes)
                paths.Add(Path.Combine(Folder, spec.Name + ".unity").Replace('\\', '/'));
            return paths.ToArray();
        }
    }

    [MenuItem("Kessil/Story/Build VS2 Grey Thread Scenes")]
    public static void Build()
    {
        EnsureFolders();
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
            BuildScene(spec);

        SceneArchitectureBuilder.EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GreyThread] Built {GreyThreadSceneCatalog.Scenes.Count} additive Chapter 01 scenes.");
    }

    private static void BuildScene(GreyThreadSceneCatalog.SceneSpec spec)
    {
        var path = $"{Folder}/{spec.Name}.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject($"GreyThread_{spec.Name}");
        var context = root.AddComponent<SceneContext>();
        context.Configure(spec.SceneId, spec.Name == "Caldemar_Arrival" ? "spawn.council" : "spawn.entry");

        CreateSpawn(root.transform, "spawn.entry", new Vector3(0f, 1.1f, -7f), Quaternion.identity);
        if (spec.Name == "Estmere_Prison")
            CreateSpawn(root.transform, "spawn.route", new Vector3(-7f, 1.1f, -5f), Quaternion.Euler(0f, 90f, 0f));
        if (spec.Name == "Estmere_SeaCave")
            CreateSpawn(root.transform, "spawn.escape", new Vector3(0f, 1.1f, -6f), Quaternion.Euler(0f, 180f, 0f));
        if (spec.Name == "Caldemar_Arrival")
            CreateSpawn(root.transform, "spawn.council", new Vector3(0f, 1.1f, -6f), Quaternion.identity);

        var geometry = new GameObject("GreyGeometry").transform;
        geometry.SetParent(root.transform, false);
        var floor = CreateBlock(geometry, "RaisedStoneFloor", new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.5f, 22f), Stone(spec));
        floor.isStatic = true;
        BuildSteps(geometry, spec);
        BuildWalls(geometry, spec);
        BuildColumns(geometry, spec);
        BuildAccent(geometry, spec);
        CreateTitle(geometry, spec);

        var lightGo = new GameObject("GreyThread_Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = Color.Lerp(new Color(1f, 0.82f, 0.64f), spec.Accent, 0.18f);

        var ambient = new GameObject("GreyThread_Ambient");
        ambient.transform.SetParent(root.transform, false);
        var fill = ambient.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = 24f;
        fill.intensity = 2.2f;
        fill.color = Color.Lerp(spec.Accent, Color.white, 0.25f);
        fill.transform.position = new Vector3(0f, 5.5f, 2.5f);

        EditorSceneManager.SaveScene(scene, path);
    }

    private static void BuildWalls(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        CreateBlock(parent, "Wall_Back", new Vector3(0f, 3f, 10f), new Vector3(30f, 6f, 0.8f), stone);
        CreateBlock(parent, "Wall_Left", new Vector3(-14.6f, 3f, 0f), new Vector3(0.8f, 6f, 20f), stone);
        CreateBlock(parent, "Wall_Right", new Vector3(14.6f, 3f, 0f), new Vector3(0.8f, 6f, 20f), stone);
        CreateBlock(parent, "Gate_Pillar_L", new Vector3(-7.5f, 3f, -9.6f), new Vector3(1.4f, 6f, 1.4f), stone);
        CreateBlock(parent, "Gate_Pillar_R", new Vector3(7.5f, 3f, -9.6f), new Vector3(1.4f, 6f, 1.4f), stone);
        CreateBlock(parent, "Gate_Lintel", new Vector3(0f, 5.7f, -9.6f), new Vector3(16.4f, 1.4f, 1.4f), stone);
        CreateBlock(parent, "Back_Raised_Wall", new Vector3(0f, 6.3f, 8.2f), new Vector3(16f, 0.55f, 0.55f), Accent(spec));
    }

    private static void BuildColumns(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -11f : 11f;
            float z = i < 2 ? -1f : 6f;
            CreateBlock(parent, $"Column_{i}", new Vector3(x, 3.4f, z), new Vector3(1.3f, 6.8f, 1.3f), stone);
            CreateBlock(parent, $"ColumnCap_{i}", new Vector3(x, 6.9f, z), new Vector3(2.0f, 0.45f, 2.0f), Accent(spec));
        }
    }

    private static void BuildSteps(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        for (int i = 0; i < 4; i++)
        {
            float z = 1f + i * 1.15f;
            CreateBlock(parent, $"Step_{i}", new Vector3(0f, 0.18f + i * 0.36f, z), new Vector3(13f - i * 1.2f, 0.36f + i * 0.12f, 1.2f), stone);
        }
        CreateBlock(parent, "RaisedStage", new Vector3(0f, 1.8f, 6.3f), new Vector3(12f, 1.2f, 4f), stone);
    }

    private static void BuildAccent(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var accent = Accent(spec);
        var marker = CreateBlock(parent, "StoryMarker", new Vector3(0f, 3.0f, 5.5f), new Vector3(2.4f, 4.6f, 0.35f), accent);
        marker.transform.Rotate(0f, 45f, 0f);
        marker.GetComponent<Renderer>().sharedMaterial = accent;

        var brazier = CreateBlock(parent, "Brazier_Left", new Vector3(-5.5f, 1.6f, 5.2f), new Vector3(1.1f, 2.8f, 1.1f), accent);
        var brazierRight = Object.Instantiate(brazier, parent);
        brazierRight.name = "Brazier_Right";
        brazierRight.transform.localPosition = new Vector3(5.5f, 1.6f, 5.2f);
        var flame = brazier.AddComponent<Light>();
        flame.type = LightType.Point;
        flame.range = 8f;
        flame.intensity = 1.6f;
        flame.color = new Color(1f, 0.55f, 0.18f);
        var flameRight = brazierRight.AddComponent<Light>();
        flameRight.type = LightType.Point;
        flameRight.range = 8f;
        flameRight.intensity = 1.6f;
        flameRight.color = new Color(1f, 0.55f, 0.18f);
    }

    private static void CreateTitle(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var title = new GameObject("GreyThread_Title");
        title.transform.SetParent(parent, false);
        title.transform.position = new Vector3(0f, 7.5f, 9.2f);
        // TextMesh faces +Z by default; the capture/play camera approaches from -Z.
        // Keeping the authored orientation readable also makes the scene useful when
        // opened directly in the Unity editor.
        title.transform.rotation = Quaternion.identity;
        var text = title.AddComponent<TextMesh>();
        text.text = spec.Title.ToUpperInvariant();
        text.fontSize = 42;
        text.characterSize = 0.12f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.Lerp(new Color(0.94f, 0.82f, 0.58f), spec.Accent, 0.35f);
    }

    private static SceneSpawnPoint CreateSpawn(Transform parent, string id, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject("Spawn_" + id.Replace('.', '_'));
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var spawn = go.AddComponent<SceneSpawnPoint>();
        spawn.Configure(id);
        return spawn;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.layer = GameLayers.Structure;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static Material Stone(GreyThreadSceneCatalog.SceneSpec spec)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.name = spec.Name + "_Stone";
        material.color = Color.Lerp(new Color(0.12f, 0.14f, 0.17f), spec.Accent, 0.28f);
        return material;
    }

    private static Material Accent(GreyThreadSceneCatalog.SceneSpec spec)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.name = spec.Name + "_Accent";
        material.color = Color.Lerp(spec.Accent, new Color(0.9f, 0.68f, 0.35f), 0.3f);
        return material;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Chapter01");
    }
}
