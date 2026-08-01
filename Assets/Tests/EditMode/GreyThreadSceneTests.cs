using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GreyThreadSceneTests
{
    [Test]
    public void CatalogContainsTheElevenAuthoredGreySpaces()
    {
        Assert.AreEqual(11, GreyThreadSceneCatalog.Scenes.Count);
        Assert.IsNotNull(GreyThreadSceneCatalog.Find("Prologue_Ship"));
        Assert.IsNotNull(GreyThreadSceneCatalog.Find("Caldemar_Arrival"));
    }

    [Test]
    public void GeneratedScenesHaveStableContextSpawnsAndCollisionGeometry()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            string path = $"Assets/Scenes/Chapter01/{spec.Name}.unity";
            Assert.IsTrue(File.Exists(path), $"Missing generated scene: {path}");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), path);

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var context = SceneContext.FindInScene(scene);
            Assert.IsNotNull(context, $"{spec.Name} has no SceneContext.");
            Assert.AreEqual(spec.SceneId, context.SceneId);
            Assert.IsTrue(context.TryGetDefaultSpawn(out var spawn), $"{spec.Name} has no default spawn.");
            Assert.IsNotNull(spawn);
            Assert.Greater(scene.GetRootGameObjects().Length, 0);

            int colliders = 0;
            foreach (var root in scene.GetRootGameObjects())
                colliders += root.GetComponentsInChildren<Collider>(true).Length;
            Assert.Greater(colliders, 8, $"{spec.Name} is visually grey but has no collision-backed walls.");
        }
    }

    [Test]
    public void GreyScenesAreEnabledInBuildSettings()
    {
        var enabled = new System.Collections.Generic.HashSet<string>();
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled) enabled.Add(scene.path);

        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            var path = $"Assets/Scenes/Chapter01/{spec.Name}.unity";
            Assert.IsTrue(enabled.Contains(path), $"{path} is not enabled in build settings.");
        }
    }
}
