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

        BakeSky();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProceduralSurface] Baked {count} surfaces at {ProceduralSurface.Size}px "
                  + $"for {ArtDirection.Active.Name}.");
    }

    private const string SkyTexturePath = "Assets/Art/Generated/Textures/T_Sky.png";
    private const string SkyMaterialPath = "Assets/Art/Generated/Materials/M_Sky.mat";

    /// <summary>
    /// Bakes the painted sky and returns the skybox material.
    ///
    /// A skybox rather than a dome because fog is linear and ends at 340 m — any geometry
    /// standing in for sky renders as solid fog colour. Baked to an asset because a
    /// runtime-created material does not survive a scene save, which is the trap that ate the
    /// billboards once already.
    /// </summary>
    public static Material BakeSky()
    {
        EnsureFolders();

        File.WriteAllBytes(SkyTexturePath, ProceduralSky.BuildTexture(ArtDirection.Active).EncodeToPNG());
        AssetDatabase.ImportAsset(SkyTexturePath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(SkyTexturePath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogWarning("[ProceduralSky] Skybox/Panoramic is unavailable; sky not baked.");
                return null;
            }
            material = new Material(shader) { name = "M_Sky" };
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
        }

        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath));
        if (material.HasProperty("_Mapping")) material.SetFloat("_Mapping", 1f);      // lat-long
        if (material.HasProperty("_ImageType")) material.SetFloat("_ImageType", 0f);  // 360 degrees
        if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
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
        byte[] encoded = ProceduralSurface.Get(surface).EncodeToPNG();
        // A second deterministic bake should not touch an identical imported PNG. Apart from
        // needless churn, Windows can keep a just-imported texture memory-mapped long enough
        // for an immediate overwrite to fail with ERROR_USER_MAPPED_FILE (1224).
        bool textureChanged = !File.Exists(texturePath)
                              || !BytesEqual(File.ReadAllBytes(texturePath), encoded);
        if (textureChanged)
        {
            File.WriteAllBytes(texturePath, encoded);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        }

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
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Texture")
                         ?? Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = $"M_{surface}" };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        // Existing baked materials may predate the unlit pigment contract. Reassign the
        // shader on every deterministic bake so a rebuild actually fixes their lighting.
        var unlit = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Texture")
                    ?? Shader.Find("Universal Render Pipeline/Lit");
        if (unlit != null && material.shader != unlit) material.shader = unlit;

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_SpecularHighlights")) material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
        EditorUtility.SetDirty(material);
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i]) return false;
        return true;
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
