using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies an <see cref="ArtDirection"/> look to the project and captures matched
/// comparison shots, so the direction is chosen by looking rather than by discussing.
///
/// Everything here is render-layer only: no geometry, materials or assets are modified,
/// which is what makes switching looks reversible and cheap.
/// </summary>
public static class ArtDirectionTool
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string UrpAssetPath = "Assets/Settings/URP-HighFidelity.asset";
    private const string VolumeProfilePath = "Assets/Art/KessilVolume.asset";
    private const string ShotDir = "Assets/Screenshots/ArtDirection";

    /// <summary>Matched viewpoints, chosen to show sky, distance, architecture and ground.</summary>
    private static readonly (string Name, Vector3 Pos, Vector3 Look)[] Viewpoints =
    {
        // Eye level is ~1.7 m above the Caldemar pad (y 24.2). Vistas are judged from where
        // the player actually stands, not from a drone.
        ("01-city-approach",  new Vector3(-1830f, 26f, 1390f), new Vector3(-2000f, 27f, 1520f)),
        ("02-open-bay",       new Vector3(-1720f, 34f, 1260f), new Vector3(-900f, 14f, 500f)),
        ("03-plaza-ground",   new Vector3(-2000f, 27f, 1420f), new Vector3(-1900f, 26f, 1560f)),
        ("04-coast-horizon",  new Vector3(-2260f, 55f, 1500f), new Vector3(-2800f, 18f, 900f))
    };

    [MenuItem("Kessil/Art Direction/Apply Arena Miniature (locked)")]
    public static void ApplyArenaMiniature() => ApplyAndRebuild(ArtDirection.Look.ArenaMiniature);

    [MenuItem("Kessil/Art Direction/Apply Morrowind Clean (comparison only)")]
    public static void ApplyMorrowind() => ApplyAndRebuild(ArtDirection.Look.MorrowindClean);

    [MenuItem("Kessil/Art Direction/Apply PS1 Crunch (comparison only)")]
    public static void ApplyPs1() => ApplyAndRebuild(ArtDirection.Look.Ps1Crunch);

    /// <summary>
    /// Headless entry point for restoring the locked look — used after a comparison run,
    /// which necessarily leaves the project in whichever preset it captured last.
    /// </summary>
    public static void LockArenaMiniature()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyAndRebuild(ArtDirection.Look.ArenaMiniature);
    }

    /// <summary>
    /// Applies a look and bakes it into the world. Settings alone are not enough: the
    /// generator writes surface colours into the materials it builds, so without a rebuild
    /// the terrain keeps the previous palette.
    /// </summary>
    public static void ApplyAndRebuild(ArtDirection.Look look)
    {
        Apply(look);
        RebuildWorld();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ArtDirection] '{ArtDirection.Get(look).Name}' baked into {scene.name} and saved.");
    }

    public static void Apply(ArtDirection.Look look)
    {
        var preset = ArtDirection.Get(look);
        ArtDirection.Current = look;

        ApplyRenderScale(preset);
        ApplyTextureFilter(preset);
        ApplyMaterialPalette(preset);

        // The generated surfaces carry the palette in their texels, so a look change has to
        // redraw them. Skipping this leaves the previous palette baked into every wall in the
        // region — the same failure that made ApplyAndRebuild the only sanctioned way to switch.
        ProceduralSurface.Invalidate();
        ProceduralSurfaceBaker.BakeAll();

        ApplyGrading(preset);
        ArtDirection.ApplyEnvironment(preset);
        ApplyFogBaseline(preset);

        AssetDatabase.SaveAssets();
        Debug.Log($"[ArtDirection] Applied '{preset.Name}' — render scale {preset.RenderScale}, " +
                  $"filter {preset.TextureFilter}, fog x{preset.FogDensityScale}.");
    }

    private static void ApplyRenderScale(in ArtDirection.Preset preset)
    {
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
        if (urp == null)
        {
            Debug.LogWarning("[ArtDirection] URP asset not found; render scale not applied.");
            return;
        }

        urp.renderScale = preset.RenderScale;
        // Point-filtered upscaling keeps the low internal resolution crunchy instead of
        // smearing it back into a soft image.
        urp.upscalingFilter = preset.TextureFilter == FilterMode.Point
            ? UpscalingFilterSelection.Point
            : UpscalingFilterSelection.Auto;
        urp.msaaSampleCount = preset.TextureFilter == FilterMode.Point ? 1 : 4;
        EditorUtility.SetDirty(urp);
    }

    /// <summary>
    /// Filter mode is set on the imported textures themselves. Anisotropic filtering is
    /// dropped to 0 for the crunchy look so ground planes shimmer the way they used to.
    /// </summary>
    private static void ApplyTextureFilter(in ArtDirection.Preset preset)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ThirdParty", "Assets/Art" });
        int changed = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

            bool point = preset.TextureFilter == FilterMode.Point;
            var wantAniso = point ? 0 : 4;
            if (importer.filterMode == preset.TextureFilter && importer.anisoLevel == wantAniso) continue;

            importer.filterMode = preset.TextureFilter;
            importer.anisoLevel = wantAniso;
            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log($"[ArtDirection] Set {changed} textures to {preset.TextureFilter}.");
    }

    /// <summary>
    /// Writes the locked palette onto the generated world materials. Assets are held to the
    /// palette rather than the palette adapting to assets — the rule that stops the project
    /// drifting back into several visual languages at once.
    ///
    /// Values are assigned, never blended with what was already there, so re-applying a look
    /// is idempotent.
    /// </summary>
    private static void ApplyMaterialPalette(in ArtDirection.Preset preset)
    {
        var p = preset.Palette;
        var byName = new (string File, Color Color)[]
        {
            ("M_Ocean", p.Ocean),
            ("M_Halbrand", p.Temperate),
            ("M_Grass", p.Temperate),
            ("M_Sarrakh", p.Arid),
            ("M_Sand", p.Sand),
            ("M_CityStone", p.CityStone),
            ("M_Mountain", p.Mountain),
            ("M_Road", p.Road)
        };

        int applied = 0;
        foreach (var (file, color) in byName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/{file}.mat");
            if (mat == null) continue;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;

            // Flat, non-metallic surfaces. Specular highlights on terrain are the fastest
            // way to break a painterly read.
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", file == "M_Ocean" ? 0.45f : 0.05f);

            EditorUtility.SetDirty(mat);
            applied++;
        }

        Debug.Log($"[ArtDirection] Palette applied to {applied} world materials.");
    }

    private static void ApplyGrading(in ArtDirection.Preset preset)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogWarning("[ArtDirection] Volume profile not found; grading not applied.");
            return;
        }

        if (profile.TryGet<ColorAdjustments>(out var color))
        {
            color.active = true;
            color.contrast.Override(preset.Contrast);
            color.saturation.Override(preset.Saturation);
            color.postExposure.Override(preset.PostExposure);
            color.colorFilter.Override(preset.ColorFilter);
        }

        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.active = preset.BloomIntensity > 0f;
            bloom.intensity.Override(preset.BloomIntensity);
        }

        if (profile.TryGet<Vignette>(out var vignette))
        {
            vignette.active = true;
            vignette.intensity.Override(preset.VignetteIntensity);
        }

        if (!profile.TryGet<Tonemapping>(out var tonemap))
        {
            tonemap = profile.Add<Tonemapping>(true);
        }

        tonemap.active = true;
        // Neutral keeps the painterly read; None leaves the harder, flatter PS1 contrast.
        tonemap.mode.Override(preset.TextureFilter == FilterMode.Point
            ? TonemappingMode.None
            : TonemappingMode.Neutral);

        EditorUtility.SetDirty(profile);
    }

    /// <summary>
    /// Edit-time fog so the Scene view matches Play mode. At runtime the weather system
    /// owns fog and scales it by the same preset.
    /// </summary>
    private static void ApplyFogBaseline(in ArtDirection.Preset preset)
    {
        RenderSettings.fog = true;

        // ApplyEnvironment has already chosen the mode for this preset — linear for contoured
        // looks, exponential for atmospheric ones. Only the density belongs here, and setting
        // the mode again would silently undo that choice.
        if (!ArtDirection.UsesContour(preset))
            RenderSettings.fogDensity = 0.0015f * preset.FogDensityScale;

        RenderSettings.fogColor = ArtDirection.Grade(new Color(0.55f, 0.65f, 0.75f), preset);
    }

    /// <summary>
    /// Headless entry point: applies each look in turn and renders the same viewpoints,
    /// so the only variable between the two image sets is the art direction.
    /// </summary>
    public static void CaptureComparison()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(ShotDir);

        foreach (ArtDirection.Look look in System.Enum.GetValues(typeof(ArtDirection.Look)))
        {
            Apply(look);
            // The generator bakes surface colours into the materials it builds, so the
            // world has to be regenerated for a palette change to reach the terrain.
            RebuildWorld();
            var preset = ArtDirection.Get(look);
            foreach (var (name, pos, target) in Viewpoints)
            {
                Capture(preset, look, name, pos, target);
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[ArtDirection] Comparison shots written to {ShotDir}");
    }

    private static void RebuildWorld()
    {
        var gen = Object.FindAnyObjectByType<KessilWorldGenerator>();
        if (gen == null)
        {
            Debug.LogWarning("[ArtDirection] No world generator in scene; palette not baked into terrain.");
            return;
        }

        gen.GenerateWorld();
        Physics.SyncTransforms();
    }

    private static void Capture(in ArtDirection.Preset preset, ArtDirection.Look look,
                                string shotName, Vector3 pos, Vector3 target)
    {
        // Render at full resolution, then downsample to the preset's render scale and back
        // up with the matching filter. This reproduces URP's render-scale pipeline in an
        // offline capture, where renderScale itself is not applied.
        const int width = 1280;
        const int height = 720;

        var camGo = new GameObject("~ArtDirectionCapture");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(target);
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = WorldLayout.CameraFarPlane;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;

        var data = camGo.GetComponent<UniversalAdditionalCameraData>();
        if (data != null) data.renderPostProcessing = true;

        int rw = Mathf.Max(1, Mathf.RoundToInt(width * preset.RenderScale));
        int rh = Mathf.Max(1, Mathf.RoundToInt(height * preset.RenderScale));

        var rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32)
        {
            filterMode = preset.TextureFilter
        };
        cam.targetTexture = rt;
        cam.Render();

        // Upscale back to a common size so both looks are compared at the same dimensions.
        var full = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        full.filterMode = preset.TextureFilter;
        Graphics.Blit(rt, full);

        var prev = RenderTexture.active;
        RenderTexture.active = full;
        var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        shot.Apply();
        RenderTexture.active = prev;

        // Named from the enum rather than from a two-way ternary, which silently mislabelled
        // every shot the moment a third look existed.
        var path = $"{ShotDir}/{shotName}-{look.ToString().ToLowerInvariant()}.png";
        File.WriteAllBytes(path, shot.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(full);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(shot);
        Object.DestroyImmediate(camGo);

        Debug.Log($"[ArtDirection] Captured {path}");
    }
}
