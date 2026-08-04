using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the character import state.
///
/// W-13 established that the current Kenney mini-characters cannot be Humanoid rigs — Unity
/// reports "Required human bone 'LeftLowerLeg' not found", because the models have no knee
/// joint. A Human rig without a valid avatar is strictly worse than Generic: it logs a rig
/// error on every import and still cannot play a single retargeted clip.
///
/// So the invariant is not "must be Generic" — a future mesh should be Humanoid. It is
/// "never Human-without-a-valid-avatar", which is the broken middle state.
/// </summary>
public class CharacterRigTests
{
    private const string CharacterFolder = "Assets/Resources/Characters";

    private static IEnumerable<string> HumanModelPaths()
    {
        if (!Directory.Exists(CharacterFolder)) yield break;

        foreach (var path in Directory.GetFiles(CharacterFolder, "*.fbx"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (CharacterLibrary.IsHumanModelName(name))
                yield return path.Replace('\\', '/');
        }
    }

    private static bool HasValidHumanAvatar(string assetPath)
    {
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (sub is Avatar avatar)
                return avatar.isValid && avatar.isHuman;

        return false;
    }

    [Test]
    public void HumanModels_AreNeverHumanoidWithoutAValidAvatar()
    {
        var broken = new List<string>();

        foreach (var path in HumanModelPaths())
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
            if (importer.animationType != ModelImporterAnimationType.Human) continue;
            if (HasValidHumanAvatar(path)) continue;

            broken.Add(Path.GetFileNameWithoutExtension(path));
        }

        Assert.IsEmpty(
            broken,
            "These models are set to Humanoid but produced no valid avatar, so they log a rig "
            + "error on import and cannot play retargeted animation. Run "
            + "Kessil → Characters → Revert To Generic Rig, or replace the mesh with one that "
            + "has the required humanoid bones: " + string.Join(", ", broken));
    }

    [Test]
    public void CharacterFolder_StillContainsHumanModels()
    {
        // Cheap guard: the rig tool reimports these, and a bad glob or a moved folder would
        // otherwise make the test above pass by finding nothing at all.
        CollectionAssert.IsNotEmpty(
            new List<string>(HumanModelPaths()),
            $"No human character models found under {CharacterFolder}.");
    }
}
