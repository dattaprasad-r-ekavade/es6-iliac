using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuild world + ensure P0/P1 systems compile-ready in the Main scene.
/// </summary>
public static class SetupP0P1Systems
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Kessil/Systems/Install P0+P1 + Rebuild World")]
    public static void InstallAndRebuild()
    {
        AssetDatabase.Refresh();
        // Full presentation rebuild wires prefabs + UI + world
        SetupGamePresentation.SetupAll();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (Object.FindAnyObjectByType<GameSystemsBootstrap>() == null)
        {
            var go = new GameObject("GameSystemsBootstrap");
            go.AddComponent<GameSystemsBootstrap>();
        }

        // Ensure SFX object survived rebuild
        if (Object.FindAnyObjectByType<GameSfx>() == null)
        {
            var systems = GameObject.Find("GameSystems") ?? new GameObject("GameSystems");
            systems.AddComponent<GameSfx>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[P0P1] Installed. Play → START → skip cutscene. M/J/I/T for menus.");
    }
}
