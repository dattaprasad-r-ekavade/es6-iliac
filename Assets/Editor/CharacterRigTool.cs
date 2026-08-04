using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts the character models in <c>Resources/Characters</c> from Unity's Generic rig to
/// the Humanoid rig, generating an avatar from each model.
///
/// This is W-13's blocker. Mixamo — and every other retargetable animation source — ships
/// Humanoid clips. The imported characters were <c>animationType: 2</c> (Generic) with
/// <c>avatarSetup: 0</c> (no avatar), so nothing could ever retarget onto them regardless of
/// which animations were downloaded. The mesh was never the problem; the import setting was.
///
/// Deterministic and idempotent: running it twice changes nothing. Kessil → Characters →
/// Convert To Humanoid Rig.
/// </summary>
public static class CharacterRigTool
{
    public const string CharacterFolder = "Assets/Resources/Characters";

    [MenuItem("Kessil/Characters/Convert To Humanoid Rig")]
    public static void ConvertAll()
    {
        var converted = new List<string>();
        var failed = new List<string>();

        foreach (var path in HumanModelPaths())
        {
            if (Convert(path)) converted.Add(Path.GetFileNameWithoutExtension(path));
            else failed.Add(Path.GetFileNameWithoutExtension(path));
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[CharacterRig] Humanoid: {converted.Count} converted, {failed.Count} without a valid avatar.");
        if (failed.Count > 0)
        {
            // Not an error. A model whose skeleton cannot satisfy Unity's required humanoid
            // bones simply cannot accept retargeted animation, and that is a finding rather
            // than a failure — it is exactly what the W-13 spike exists to establish.
            Debug.LogWarning($"[CharacterRig] No valid humanoid avatar for: {string.Join(", ", failed)}");
        }
    }

    /// <summary>Model paths that <see cref="CharacterLibrary"/> would treat as human.</summary>
    public static IEnumerable<string> HumanModelPaths()
    {
        if (!Directory.Exists(CharacterFolder)) yield break;

        foreach (var path in Directory.GetFiles(CharacterFolder, "*.fbx"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (CharacterLibrary.IsHumanModelName(name))
                yield return path.Replace('\\', '/');
        }
    }

    /// <summary>
    /// Attempts the Humanoid conversion and keeps it only if Unity produces a valid avatar.
    /// A model left as Human with an invalid avatar is strictly worse than Generic — it
    /// logs a rig error on every import and still cannot play anything — so a failed
    /// attempt reverts rather than leaving the asset broken.
    /// </summary>
    public static bool Convert(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer) return false;

        var originalType = importer.animationType;
        var originalSetup = importer.avatarSetup;

        if (originalType == ModelImporterAnimationType.Human
            && HasValidHumanAvatar(assetPath))
            return true; // already converted; idempotent

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();

        if (HasValidHumanAvatar(assetPath)) return true;

        importer.animationType = originalType == ModelImporterAnimationType.Human
            ? ModelImporterAnimationType.Generic
            : originalType;
        importer.avatarSetup = originalSetup == ModelImporterAvatarSetup.CreateFromThisModel
            ? ModelImporterAvatarSetup.NoAvatar
            : originalSetup;
        importer.SaveAndReimport();
        return false;
    }

    /// <summary>
    /// Force every human model back to Generic. Used to clean up after a failed conversion
    /// attempt, and to reset the folder before evaluating a replacement mesh.
    /// </summary>
    [MenuItem("Kessil/Characters/Revert To Generic Rig")]
    public static void RevertAll()
    {
        int n = 0;
        foreach (var path in HumanModelPaths())
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
            if (importer.animationType == ModelImporterAnimationType.Generic
                && importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar) continue;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.SaveAndReimport();
            n++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterRig] Reverted {n} model(s) to Generic.");
    }

    /// <summary>
    /// True when Unity produced a rig that retargeted humanoid animation can play on. This
    /// is the question W-13 was opened to answer, so it is asserted by a test rather than
    /// inspected by eye.
    /// </summary>
    public static bool HasValidHumanAvatar(string assetPath)
    {
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (sub is Avatar avatar)
                return avatar.isValid && avatar.isHuman;

        return false;
    }
}
