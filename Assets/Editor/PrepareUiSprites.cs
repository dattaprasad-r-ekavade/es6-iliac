using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts Kenney RPG UI PNGs to Sprites and mirrors them into Resources/UI for builds.
/// </summary>
public static class PrepareUiSprites
{
    private const string Source = "Assets/ThirdParty/KenneyUI/UiPackRpg/PNG";
    private const string Dest = "Assets/Resources/UI";

    [MenuItem("Kessil/Presentation/Prepare UI Sprites")]
    public static void Prepare()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Dest))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Source });
        int n = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (importer.spritePixelsPerUnit != 100f)
            {
                importer.spritePixelsPerUnit = 100f;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            // 9-slice friendly borders for panels/buttons
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("panel") || name.StartsWith("button") || name.StartsWith("bar"))
            {
                var border = new Vector4(12, 12, 12, 12);
                if (importer.spriteBorder != border)
                {
                    importer.spriteBorder = border;
                    dirty = true;
                }
            }
            if (dirty)
            {
                importer.SaveAndReimport();
            }

            var destPath = $"{Dest}/{Path.GetFileName(path)}";
            // Existing Resources copies keep their GUIDs so rebuilding presentation does
            // not rewrite the scene and every UI reference. Newly added sprites are copied.
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(destPath) == null)
                AssetDatabase.CopyAsset(path, destPath);
            n++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[PrepareUiSprites] Prepared {n} UI sprites → {Dest}");
    }
}
