using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fixes the classic "everything is pink" issue: URP package present but no pipeline asset assigned.
/// </summary>
public static class FixUrpPipeline
{
    private const string UrpAssetPath = "Assets/Settings/URP-HighFidelity.asset";
    private const string RendererPath = "Assets/Settings/URP-HighFidelity-Renderer.asset";

    [MenuItem("Kessil/Rendering/Fix Pink Materials (Setup URP)")]
    public static void Fix()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
        }

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
        if (urp == null)
        {
            urp = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(urp, UrpAssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = urp;
        QualitySettings.renderPipeline = urp;

        // Ensure our world materials use URP Lit (not missing/broken shaders).
        FixMaterial("Assets/Art/Materials/M_Ocean.mat", new Color(0.1f, 0.32f, 0.52f));
        FixMaterial("Assets/Art/Materials/M_Grass.mat", new Color(0.25f, 0.45f, 0.22f));
        FixMaterial("Assets/Art/Materials/M_Sand.mat", new Color(0.84f, 0.74f, 0.52f));
        FixMaterial("Assets/Art/Materials/M_CityStone.mat", new Color(0.55f, 0.52f, 0.48f));

        // Upgrade imported Kenney materials that still reference Built-in Standard.
        UpgradeFolderMaterials("Assets/ThirdParty/Kenney");

        AssetDatabase.SaveAssets();
        EditorUtility.RequestScriptReload();

        Debug.Log("[FixUrpPipeline] URP assigned and materials upgraded. Pink should be gone — refresh the Scene view if needed.");
    }

    private static void FixMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            return;
        }

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[FixUrpPipeline] URP Lit shader not found.");
            return;
        }

        mat.shader = lit;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else
        {
            mat.color = color;
        }

        EditorUtility.SetDirty(mat);
    }

    private static void UpgradeFolderMaterials(string folder)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
        int upgraded = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
            {
                continue;
            }

            var shaderName = mat.shader.name;
            if (shaderName == "Standard" || shaderName == "Autodesk Interactive" || shaderName.Contains("Error") || shaderName == "Hidden/InternalErrorShader")
            {
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                mat.shader = lit;
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }

                if (mainTex != null && mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", mainTex);
                }

                EditorUtility.SetDirty(mat);
                upgraded++;
            }
        }

        Debug.Log($"[FixUrpPipeline] Upgraded {upgraded} imported materials to URP Lit.");
    }
}
