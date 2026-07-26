using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Caps import resolution for the high-res third-party texture packs.
///
/// The Poly Haven set ships 2K maps for props like crates and buckets, in a game whose
/// characters are Kenney minifigures. That pack alone accounted for ~45 MB of the ~60 MB
/// the scene pulls in, and most of the 206 MB player build.
///
/// Only the importer's max size is changed, so this is reversible and the source files
/// are untouched — raise the cap and reimport if a hero asset needs the detail.
/// </summary>
public static class TextureBudget
{
    /// <summary>Folders to cap, and the resolution to cap them to.</summary>
    private static readonly (string Path, int MaxSize)[] Budgets =
    {
        ("Assets/ThirdParty/PolyHaven", 1024),
        ("Assets/ThirdParty/Quaternius", 1024),
        ("Assets/ThirdParty/Kenney", 512)
    };

    [MenuItem("Elder Scrolls 6/Assets/Apply Texture Budget")]
    public static void Apply()
    {
        var changed = new List<string>();
        long before = 0, after = 0;

        foreach (var (folder, maxSize) in Budgets)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                before += importer.maxTextureSize;
                if (importer.maxTextureSize <= maxSize)
                {
                    after += importer.maxTextureSize;
                    continue;
                }

                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.streamingMipmaps = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                after += maxSize;
                changed.Add($"{path} -> {maxSize}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TextureBudget] Capped {changed.Count} textures. " +
                  $"Summed max-size {before} -> {after}.");
        foreach (var line in changed)
            Debug.Log($"[TextureBudget]   {line}");
    }
}
