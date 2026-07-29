using UnityEngine;

/// <summary>
/// Runtime visual fixes: Kenney character atlas, water transparency.
/// </summary>
public static class WorldVisualFix
{
    private static Material _kenneyCharMat;

    public static Material KenneyCharacterMaterial
    {
        get
        {
            if (_kenneyCharMat != null) return _kenneyCharMat;
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _kenneyCharMat = new Material(lit) { name = "KenneyCharacter_Runtime" };
            _kenneyCharMat.enableInstancing = true;
            var tex = Resources.Load<Texture2D>("Characters/colormap");
            if (tex != null)
            {
                _kenneyCharMat.SetTexture("_BaseMap", tex);
                _kenneyCharMat.SetColor("_BaseColor", Color.white);
            }
            else
                _kenneyCharMat.SetColor("_BaseColor", new Color(0.82f, 0.68f, 0.55f));
            _kenneyCharMat.SetFloat("_Smoothness", 0.15f);
            return _kenneyCharMat;
        }
    }

    public static Material CreateWaterMaterial()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(lit) { name = "M_Ocean_Runtime" };
        m.enableInstancing = true;
        m.SetColor("_BaseColor", new Color(0.12f, 0.48f, 0.72f, 0.82f));
        m.SetFloat("_Smoothness", 0.94f);
        m.SetFloat("_Metallic", 0.08f);
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return m;
    }

    public static void FixCharacter(GameObject go)
    {
        if (go == null) return;
        var mat = KenneyCharacterMaterial;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            if (r != null) r.sharedMaterial = mat;
    }

    /// <summary>World-space UV scale: texture repeats every tileMeters.</summary>
    public static void SetWorldTiling(Material mat, float tileMeters)
    {
        if (mat == null || tileMeters <= 0.1f) return;
        float scale = 1f / tileMeters;
        if (mat.HasProperty("_BaseMap"))
            mat.SetTextureScale("_BaseMap", new Vector2(scale, scale));
        if (mat.HasProperty("_MainTex"))
            mat.SetTextureScale("_MainTex", new Vector2(scale, scale));
    }

    public static Material CreateTerrainMaterial(Material source, Color tint, float tileMeters)
    {
        if (source == null) return null;
        var m = new Material(source);
        m.SetColor("_BaseColor", tint);
        m.enableInstancing = true;
        // Terrain mesh UVs are already authored in world metres / tileMeters.
        return m;
    }
}
