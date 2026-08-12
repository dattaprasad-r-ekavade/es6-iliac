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
        Assert.IsNotNull(GreyThreadSceneCatalog.Find("Council_Arrival"));
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

    /// <summary>
    /// The naming policy, applied to the one place it was being broken.
    ///
    /// Scene ids and Unity scene names both used to embed the setting's place names
    /// (<c>scene.estmere_palace</c>, <c>Caldemar_Arrival</c>). A save persists the scene *name*,
    /// so a setting rename orphaned every mid-chapter save — the exact failure the policy exists
    /// to prevent, in the one category nothing was checking.
    ///
    /// Names a scene after the building, not the city. The list is explicit rather than clever
    /// because there is no way to infer "this word is a place name" from the string alone; when
    /// the setting gains a proper noun, add it here.
    /// </summary>
    [Test]
    public void SceneIdsAndNames_DoNotEmbedSettingPlaceNames()
    {
        string[] placeNames =
        {
            // Original setting
            "estmere", "caldemar", "kessil", "halbrand", "sarrakh", "corrath",
            // Indic variant
            "ratnapur", "sabhapur", "marukot", "shantipur", "ratna", "uttara", "maru"
        };

        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            foreach (var place in placeNames)
            {
                StringAssert.DoesNotContain(place, spec.SceneId.ToLowerInvariant(),
                    $"Scene id '{spec.SceneId}' embeds the place name '{place}'. Ids are "
                    + "persisted and must survive a rename of the setting.");
                StringAssert.DoesNotContain(place, spec.Name.ToLowerInvariant(),
                    $"Scene name '{spec.Name}' embeds the place name '{place}'. A save stores "
                    + "the scene name, so renaming the setting would orphan every save.");
            }
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
