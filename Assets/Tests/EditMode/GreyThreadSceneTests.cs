using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GreyThreadSceneTests
{
    [Test]
    public void ContinueProgressDoesNotRestartThePrologue()
    {
        Assert.IsFalse(GreyThreadDirector.HasRestoredProgress(new StorySnapshot()),
            "A genuinely new story should still start Chapter 01.");
        Assert.IsTrue(GreyThreadDirector.HasRestoredProgress(new StorySnapshot
        {
            BeatId = "B630",
            StageId = "stage.escape",
            RouteId = "route.trade"
        }), "A restored mid-story snapshot would be overwritten by B010.");
    }
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

    /// <summary>
    /// The shipping scene list is what actually goes into a player, and it is not the same
    /// thing as `EditorBuildSettings`.
    ///
    /// `BuildPlayerCommand` used to hand-maintain its own copy. `Capital_Region` was added to
    /// `EnsureBuildSettings` and not to that copy, so the region was present in the editor —
    /// every PlayMode test loaded it happily — and **absent from every shipped build**, which
    /// strands the player on New Game with nothing to walk into. No editor test could see it.
    ///
    /// Both now derive from `ShippingScenePaths`. These hold that it stays true.
    /// </summary>
    [Test]
    public void TheShippingSceneList_ContainsEverythingAPlayerNeeds()
    {
        var shipping = SceneArchitectureBuilder.ShippingScenePaths();

        CollectionAssert.Contains(shipping, CapitalRegionBuilder.ScenePath,
            "The region is missing from the shipping scene list. A build without it cannot "
            + "load the exterior, and New Game dead-ends.");
        CollectionAssert.Contains(shipping, SceneArchitectureBuilder.BootstrapPath);

        foreach (var spec in GreyThreadSceneCatalog.Scenes)
            CollectionAssert.Contains(shipping, $"Assets/Scenes/Chapter01/{spec.Name}.unity",
                $"{spec.Name} would not ship.");
    }

    [Test]
    public void BootstrapIsSceneZero()
    {
        Assert.AreEqual(SceneArchitectureBuilder.BootstrapPath,
            SceneArchitectureBuilder.ShippingScenePaths()[0],
            "Bootstrap is no longer scene zero, so the player boots into something else.");
    }

    [Test]
    public void TestFixturesNeverShip()
    {
        var shipping = SceneArchitectureBuilder.ShippingScenePaths();
        foreach (var fixturePath in SceneArchitectureBuilder.TestScenePaths)
            CollectionAssert.DoesNotContain(shipping, fixturePath,
                $"{fixturePath} is a test fixture and would ship to players.");
    }

    [Test]
    public void EveryShippingScene_ExistsOnDisk()
    {
        foreach (var path in SceneArchitectureBuilder.ShippingScenePaths())
            Assert.IsTrue(File.Exists(path),
                $"{path} is in the shipping list but not on disk; the build would silently drop it.");
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
