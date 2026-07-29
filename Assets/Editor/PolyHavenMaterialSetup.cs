using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Poly Haven photogrammetry models: URP materials + baked prefabs for runtime use.
/// </summary>
public static class PolyHavenMaterialSetup
{
    private const string Root = "Assets/ThirdParty/PolyHaven";
    private const string PrefabRoot = "Assets/Prefabs/Hero/PolyHaven";

    [MenuItem("Kessil/Assets/Setup Poly Haven Materials")]
    public static void SetupAll()
    {
        if (!AssetDatabase.IsValidFolder(Root))
        {
            Debug.LogWarning("[PolyHaven] No folder at " + Root);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Hero")) AssetDatabase.CreateFolder("Assets/Prefabs", "Hero");
        if (!AssetDatabase.IsValidFolder(PrefabRoot)) AssetDatabase.CreateFolder("Assets/Prefabs/Hero", "PolyHaven");

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[PolyHaven] URP Lit shader missing.");
            return;
        }

        int made = 0;
        foreach (var modelDir in AssetDatabase.GetSubFolders(Root))
        {
            var id = Path.GetFileName(modelDir);
            var matPath = $"{modelDir}/{id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = lit;
            var diff = FindTex(modelDir, id, "diff");
            var nor = FindTex(modelDir, id, "nor_gl") ?? FindTex(modelDir, id, "nor_dx");
            var rough = FindTex(modelDir, id, "rough");
            var metal = FindTex(modelDir, id, "metal");
            var ao = FindTex(modelDir, id, "ao");

            if (diff != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diff);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diff);
            }
            if (nor != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nor);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", rough != null ? 0.42f : 0.35f);
            if (metal != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metal);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metal != null ? 0.85f : 0f);
            }
            if (ao != null && mat.HasProperty("_OcclusionMap"))
                mat.SetTexture("_OcclusionMap", ao);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);

            var fbxGuid = AssetDatabase.FindAssets($"{id} t:Model", new[] { modelDir }).FirstOrDefault();
            if (string.IsNullOrEmpty(fbxGuid)) continue;
            var fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuid);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null) continue;

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(src);
            temp.name = id;
            ApplyMaterialRecursive(temp, mat);

            // Poly Haven FBX often imports in centimeters — normalize to ~meters for doors/props.
            float scale = id.Contains("modular_fort") ? 0.01f : id.Contains("gate") || id.Contains("door") ? 0.01f : 0.01f;
            temp.transform.localScale = Vector3.one * scale;

            var prefabPath = $"{PrefabRoot}/{id}.prefab";
            PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PolyHaven] Baked {made} high-detail prefabs → {PrefabRoot}");
    }

    private static void ApplyMaterialRecursive(GameObject go, Material mat)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterial = mat;
    }

    private static Texture2D FindTex(string folder, string prefix, string token)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (name.Contains(prefix.ToLowerInvariant()) && name.Contains(token))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }
}
