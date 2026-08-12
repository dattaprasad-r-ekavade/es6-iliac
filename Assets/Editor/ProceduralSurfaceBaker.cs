using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Persists <see cref="ProceduralSurface"/>'s output as real project assets.
///
/// The generator draws at runtime, which is what the sprites want — a billboard rebuilds its
/// own 2,048 pixels on load. World surfaces are the opposite case: the same eight materials sit
/// on thousands of renderers in a *saved* scene, and Unity does not serialise runtime-created
/// textures into a scene file. Left unbaked, every generated building would come back with a
/// null material the moment the editor reopened the scene.
///
/// So the rule is: sprites regenerate, surfaces bake. Both come from the same code and the same
/// palette, so they cannot disagree.
///
/// Baked output is a build artifact like `Main.unity`. Delete it and rebuild; never hand-edit it.
/// </summary>
public static class ProceduralSurfaceBaker
{
    private const string TextureFolder = "Assets/Art/Generated/Textures";
    private const string MaterialFolder = "Assets/Art/Generated/Materials";

    [MenuItem("Kessil/Art Direction/Bake Procedural Textures")]
    public static void BakeAll()
    {
        EnsureFolders();
        ProceduralSurface.Invalidate();

        int count = 0;
        foreach (ProceduralSurface.Kind surface in System.Enum.GetValues(typeof(ProceduralSurface.Kind)))
        {
            Bake(surface);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProceduralSurface] Baked {count} surfaces at {ProceduralSurface.Size}px "
                  + $"for {ArtDirection.Active.Name}.");
    }

    /// <summary>The asset-backed material for a surface, baking it first if it is missing.</summary>
    public static Material MaterialFor(ProceduralSurface.Kind surface)
    {
        string path = MaterialPath(surface);
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolders();
        Bake(surface);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void Bake(ProceduralSurface.Kind surface)
    {
        string texturePath = TexturePath(surface);
        File.WriteAllBytes(texturePath, ProceduralSurface.Get(surface).EncodeToPNG());
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        // The import settings matter as much as the pixels. Compression would blur the texels
        // that the whole direction depends on, and mipmaps would average them back into the
        // muted blur that point filtering exists to avoid.
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = ArtDirection.Active.TextureFilter;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = Mathf.Max(32, ProceduralSurface.Size);
            importer.SaveAndReimport();
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        string materialPath = MaterialPath(surface);
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = $"M_{surface}" };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
        EditorUtility.SetDirty(material);
    }

    private static string TexturePath(ProceduralSurface.Kind surface) =>
        $"{TextureFolder}/T_{surface}.png";

    private static string MaterialPath(ProceduralSurface.Kind surface) =>
        $"{MaterialFolder}/M_{surface}.mat";

    private static void EnsureFolders()
    {
        foreach (var folder in new List<string> { TextureFolder, MaterialFolder })
        {
            if (AssetDatabase.IsValidFolder(folder)) continue;
            Directory.CreateDirectory(folder);
        }
        AssetDatabase.Refresh();
    }
}
