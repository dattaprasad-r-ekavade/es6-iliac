using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the persistent Bootstrap scene and deterministic additive-transition fixtures.
/// Main remains a generated artifact; this tool only adds a regenerable SceneContext to it.
/// </summary>
public static class SceneArchitectureBuilder
{
    public const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    public const string MainPath = "Assets/Scenes/Main.unity";
    public const string ExteriorPath = "Assets/Scenes/Capital_Exterior.unity";
    public const string TestSceneAPath = "Assets/Scenes/Tests/TransitionTest_A.unity";
    public const string TestSceneBPath = "Assets/Scenes/Tests/TransitionTest_B.unity";
    public const string TestSceneCPath = "Assets/Scenes/Tests/TransitionTest_C.unity";
    public const string InvalidTestScenePath = "Assets/Scenes/Tests/TransitionTest_Invalid.unity";

    [MenuItem("Kessil/Architecture/Install Bootstrap + Additive Scenes")]
    public static void Install()
    {
        EnsureFolders();
        EnsureMainContext();
        CreateExteriorSnapshot();
        CreateBootstrapScene();
        CreateTransitionTestScene(TestSceneAPath, "scene.test_a", new Vector3(10f, 1f, 0f));
        CreateTransitionTestScene(TestSceneBPath, "scene.test_b", new Vector3(20f, 2f, 0f));
        CreateTransitionTestScene(TestSceneCPath, "scene.test_c", new Vector3(30f, 3f, 0f));
        CreateInvalidTransitionTestScene();
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneArchitecture] Bootstrap, exterior snapshot and additive test scenes are ready.");
    }

    public static void EnsureMainContext()
    {
        var scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        EnsureMainContext(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MainPath);
    }

    public static void EnsureMainContext(Scene scene)
    {
        var context = SceneContext.FindInScene(scene);
        if (context == null)
        {
            var root = new GameObject("SceneContext_Main");
            SceneManager.MoveGameObjectToScene(root, scene);
            context = root.AddComponent<SceneContext>();
        }

        context.Configure("scene.legacy_main", "spawn.caldemar");
        var spawn = FindSpawn(context, "spawn.caldemar");
        if (spawn == null)
        {
            var spawnGo = new GameObject("Spawn_Caldemar");
            spawnGo.transform.SetParent(context.transform, false);
            spawn = spawnGo.AddComponent<SceneSpawnPoint>();
            spawn.Configure("spawn.caldemar");
        }

        spawn.transform.SetPositionAndRotation(KessilWorldGenerator.GetPlayerSpawn(), Quaternion.identity);
        EditorUtility.SetDirty(context);
        EditorUtility.SetDirty(spawn);
    }

    /// <summary>
    /// Every scene that ships inside a player, in load order. **Bootstrap must stay first** —
    /// it is scene zero and everything boots through it.
    ///
    /// <see cref="EnsureBuildSettings"/> and <c>BuildPlayerCommand</c> both derive from this.
    /// They used to keep separate hand-written lists, and that is how `Capital_Region` came to
    /// be present in `EditorBuildSettings` — so fine in the editor and in every PlayMode test —
    /// while being **absent from every shipped player**. A build that cannot load the region
    /// strands the player on New Game, and nothing in the editor would ever show it.
    ///
    /// One list, two consumers. Do not add a third.
    /// </summary>
    public static List<string> ShippingScenePaths()
    {
        var paths = new List<string> { BootstrapPath, MainPath, ExteriorPath };
        paths.AddRange(GreyThreadSceneBuilder.ScenePaths);

        // The walkable exterior the whole chapter returns to.
        paths.Add(CapitalRegionBuilder.ScenePath);
        return paths;
    }

    /// <summary>
    /// Transition fixtures. They belong in build settings so PlayMode tests can load them, and
    /// they must never reach a player, which is the one reason this list is separate.
    /// </summary>
    public static string[] TestScenePaths => new[]
    {
        TestSceneAPath, TestSceneBPath, TestSceneCPath, InvalidTestScenePath
    };

    public static void EnsureBuildSettings()
    {
        var paths = ShippingScenePaths();
        paths.AddRange(TestScenePaths);

        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void CreateBootstrapScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("BootstrapRoot");
        root.AddComponent<GameStateService>();
        root.AddComponent<SceneTransitionService>();
        var entry = root.AddComponent<BootstrapEntryPoint>();
        entry.Configure("Main", "spawn.caldemar");
        EditorSceneManager.SaveScene(scene, BootstrapPath);
    }

    private static void CreateExteriorSnapshot()
    {
        var main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        var exterior = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(exterior);

        foreach (var root in main.GetRootGameObjects())
        {
            if (root.name != "WorldRoot" && root.name != "Directional Light" && root.name != "GlobalVolume")
                continue;

            var clone = Object.Instantiate(root);
            clone.name = root.name;
            SceneManager.MoveGameObjectToScene(clone, exterior);

            if (clone.name == "WorldRoot")
            {
                foreach (var cutscene in clone.GetComponentsInChildren<IntroCutsceneDirector>(true))
                    Object.DestroyImmediate(cutscene);
            }
        }

        var contextRoot = new GameObject("SceneContext_EstmereExterior");
        SceneManager.MoveGameObjectToScene(contextRoot, exterior);
        var context = contextRoot.AddComponent<SceneContext>();
        context.Configure("scene.estmere_exterior", "spawn.caldemar");
        var spawnGo = new GameObject("Spawn_Caldemar");
        spawnGo.transform.SetParent(contextRoot.transform, false);
        spawnGo.transform.SetPositionAndRotation(KessilWorldGenerator.GetPlayerSpawn(), Quaternion.identity);
        spawnGo.AddComponent<SceneSpawnPoint>().Configure("spawn.caldemar");

        EditorSceneManager.SaveScene(exterior, ExteriorPath);
        EditorSceneManager.CloseScene(exterior, true);
        SceneManager.SetActiveScene(main);
    }

    private static void CreateTransitionTestScene(string path, string sceneId, Vector3 spawnPosition)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("SceneContext");
        var context = root.AddComponent<SceneContext>();
        context.Configure(sceneId, "spawn.entry");

        var spawnGo = new GameObject("Spawn_Entry");
        spawnGo.transform.SetParent(root.transform, false);
        spawnGo.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, spawnPosition.x, 0f));
        spawnGo.AddComponent<SceneSpawnPoint>().Configure("spawn.entry");

        EditorSceneManager.SaveScene(scene, path);
    }

    private static void CreateInvalidTransitionTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("IntentionallyMissingSceneContext");
        EditorSceneManager.SaveScene(scene, InvalidTestScenePath);
    }

    private static SceneSpawnPoint FindSpawn(SceneContext context, string id)
    {
        foreach (var spawn in context.GetComponentsInChildren<SceneSpawnPoint>(true))
        {
            if (spawn.SpawnId == id) return spawn;
        }

        return null;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Tests"))
            AssetDatabase.CreateFolder("Assets/Scenes", "Tests");
    }
}
