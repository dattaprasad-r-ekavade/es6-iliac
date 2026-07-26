using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot setup: rebuild Iliac Bay (smooth land), menu UI, intro cutscene, better town assets, MSAA.
/// </summary>
public static class SetupGamePresentation
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string NaturePath = "Assets/ThirdParty/Kenney/NatureKit";
    private const string CastlePath = "Assets/ThirdParty/Kenney/CastleKit";
    private const string TownPath = "Assets/ThirdParty/Kenney/FantasyTown";
    private const string CharPath = "Assets/ThirdParty/Kenney/MiniCharacters";
    private const string PiratePath = "Assets/ThirdParty/Kenney/PirateKit";
    private const string CommercialPath = "Assets/ThirdParty/Kenney/CityKitCommercial";
    private const string SurvivalPath = "Assets/ThirdParty/Kenney/SurvivalKit";
    private const string GraveyardPath = "Assets/ThirdParty/Kenney/GraveyardKit";
    private const string FurniturePath = "Assets/ThirdParty/Kenney/FurnitureKit";
    private const string PolyHavenPrefabPath = "Assets/Prefabs/Hero/PolyHaven";
    private const string MedievalVillagePath = "Assets/ThirdParty/Quaternius/MedievalVillage";
    private const string MaterialsPath = "Assets/Art/Materials";

    [MenuItem("Elder Scrolls 6/Presentation/Setup Menu + Cutscene + Smooth Map")]
    public static void SetupAll()
    {
        AssetDatabase.Refresh();
        PrepareUiSprites.Prepare();
        PrepareCharacterResources();
        PolyHavenMaterialSetup.SetupAll();
        EnableMsaa();
        UpgradeKenneyMaterials();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        // Lighting
        var light = new GameObject("Directional Light");
        var l = light.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.96f, 0.88f);
        l.intensity = 1.35f;
        l.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        RenderSettings.sun = l;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.00055f;
        RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.72f);
        AddPostFxVolume();

        // World
        var world = new GameObject("WorldRoot");
        var gen = world.AddComponent<IliacBayWorldGenerator>();
        WireWorld(gen);
        gen.GenerateWorld();

        var player = GameObject.Find("Player");
        var playerCam = player != null ? player.GetComponentInChildren<Camera>() : null;

        // Cinematic camera
        var cineGo = new GameObject("CinematicCamera");
        var cineCam = cineGo.AddComponent<Camera>();
        cineCam.tag = "Untagged";
        cineGo.AddComponent<AudioListener>();
        cineGo.SetActive(false);

        // Actors
        var actors = BuildActors();
        var cutscene = world.AddComponent<IntroCutsceneDirector>();
        var cutSo = new SerializedObject(cutscene);
        cutSo.FindProperty("actorLeft").objectReferenceValue = actors.left;
        cutSo.FindProperty("actorRight").objectReferenceValue = actors.right;
        cutSo.FindProperty("cinematicCamera").objectReferenceValue = cineCam;
        cutSo.FindProperty("scenicDuration").floatValue = 22f;
        var linesProp = cutSo.FindProperty("lines");
        var defaults = IntroCutsceneDirector.DefaultLines();
        linesProp.arraySize = defaults.Length;
        for (int i = 0; i < defaults.Length; i++)
        {
            linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("speaker").stringValue = defaults[i].speaker;
            linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue = defaults[i].text;
            linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("holdSeconds").floatValue = defaults[i].holdSeconds;
        }
        cutSo.ApplyModifiedPropertiesWithoutUndo();

        // UI + flow
        var ui = BuildMenuUi();
        var flow = ui.canvas.AddComponent<GameFlowController>();
        var flowSo = new SerializedObject(flow);
        flowSo.FindProperty("menuRoot").objectReferenceValue = ui.menuRoot;
        flowSo.FindProperty("subtitleRoot").objectReferenceValue = ui.subtitleRoot;
        flowSo.FindProperty("speakerLabel").objectReferenceValue = ui.speaker;
        flowSo.FindProperty("subtitleLabel").objectReferenceValue = ui.subtitle;
        flowSo.FindProperty("skipHintLabel").objectReferenceValue = ui.skipHint;
        flowSo.FindProperty("skipButton").objectReferenceValue = ui.skipBtn;
        flowSo.FindProperty("menuFade").objectReferenceValue = ui.menuFade;
        flowSo.FindProperty("subtitleFade").objectReferenceValue = ui.subtitleFade;
        flowSo.FindProperty("worldGenerator").objectReferenceValue = gen;
        flowSo.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        flowSo.FindProperty("playerCamera").objectReferenceValue = playerCam;
        flowSo.FindProperty("cinematicCamera").objectReferenceValue = cineCam;
        flowSo.FindProperty("cutscene").objectReferenceValue = cutscene;
        flowSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire buttons (persistent via binder)
        var binder = ui.canvas.AddComponent<MenuButtonBinder>();
        var binderSo = new SerializedObject(binder);
        binderSo.FindProperty("startButton").objectReferenceValue = ui.startBtn;
        binderSo.FindProperty("quitButton").objectReferenceValue = ui.quitBtn;
        binderSo.FindProperty("flow").objectReferenceValue = flow;
        binderSo.ApplyModifiedPropertiesWithoutUndo();

        // Disable player until Start
        if (player != null)
        {
            var sp = player.GetComponent<SimplePlayerController>();
            if (sp != null) sp.enabled = false;
            if (playerCam != null) playerCam.enabled = false;
            var al = playerCam != null ? playerCam.GetComponent<AudioListener>() : null;
            if (al != null) al.enabled = false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Ensure build settings include Main first
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupGamePresentation] Menu + cutscene + smooth Iliac Bay ready. Press Play → Start.");
    }

    private static void PrepareCharacterResources()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Characters"))
            AssetDatabase.CreateFolder("Assets/Resources", "Characters");

        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharPath }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = System.IO.Path.GetFileName(path);
            var dest = $"Assets/Resources/Characters/{name}";
            AssetDatabase.DeleteAsset(dest);
            AssetDatabase.CopyAsset(path, dest);
        }

        var texSrc = $"{CharPath}/Textures/colormap.png";
        if (System.IO.File.Exists(texSrc))
        {
            AssetDatabase.DeleteAsset("Assets/Resources/Characters/colormap.png");
            AssetDatabase.CopyAsset(texSrc, "Assets/Resources/Characters/colormap.png");
        }
        AssetDatabase.Refresh();
    }

    private static void EnableMsaa()
    {
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP-HighFidelity.asset");
        if (urp == null) return;
        var so = new SerializedObject(urp);
        var msaa = so.FindProperty("m_MSAA");
        if (msaa != null)
        {
            msaa.intValue = 4; // 4x
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        GraphicsSettings.defaultRenderPipeline = urp;
        QualitySettings.renderPipeline = urp;
        QualitySettings.antiAliasing = 4;
    }

    private static void UpgradeKenneyMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        foreach (var folder in new[]
                 {
                     TownPath, CharPath, PiratePath, CommercialPath, CastlePath,
                     NaturePath, SurvivalPath, GraveyardPath, FurniturePath,
                     MedievalVillagePath,
                     "Assets/ThirdParty/PolyHaven"
                 })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                var n = mat.shader.name;
                if (n == "Standard" || n.Contains("Error") || n == "Hidden/InternalErrorShader")
                {
                    var c = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    var t = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    mat.shader = lit;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                    if (t != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
                    EditorUtility.SetDirty(mat);
                }
            }
        }
    }

    private static void WireWorld(IliacBayWorldGenerator gen)
    {
        EnsureMaterials();
        var ocean = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Ocean.mat");
        var highRock = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_HighRock.mat");
        var hammerfell = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Hammerfell.mat");
        var sand = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Sand.mat");
        var city = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_CityStone.mat");
        var mountain = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Mountain.mat");

        var trees = LoadModels(NaturePath, n => n.StartsWith("tree_") && (n.Contains("pine") || n.Contains("oak") || n.Contains("default") || n.Contains("tall")))
            .Concat(LoadModels(SurvivalPath, n => n.StartsWith("tree") && !n.Contains("log") && !n.Contains("trunk")))
            .Take(16).ToArray();
        var desert = LoadModels(NaturePath, n => n.StartsWith("cactus") || n.StartsWith("rock_") || n.StartsWith("plant_bush"))
            .Concat(LoadModels(SurvivalPath, n => n.StartsWith("rock-sand") || n.StartsWith("rock-")))
            .Take(16).ToArray();
        var rocks = LoadModels(NaturePath, n => n.StartsWith("rock_") || n.StartsWith("stone_large"))
            .Concat(LoadModels(SurvivalPath, n => n.StartsWith("rock-") || n.StartsWith("tree-log")))
            .Take(14).ToArray();

        // Quaternius modular medieval village (primary city art) + Poly Haven heroes + Kenney fill.
        var hero = LoadModels(PolyHavenPrefabPath, _ => true);
        var medieval = LoadModels(MedievalVillagePath, n =>
            n.StartsWith("Wall_") || n.StartsWith("Corner_") || n.StartsWith("Roof_") ||
            n.StartsWith("Door") || n.StartsWith("Stairs_") || n.StartsWith("Balcony_") ||
            n.StartsWith("Floor_") || n.Contains("Window"));
        var medievalProps = LoadModels(MedievalVillagePath, n => n.StartsWith("Prop_"));
        var buildings = medieval.Where(g => !g.name.StartsWith("Prop_"))
            .Concat(hero.Where(g => g.name.Contains("castle_door") || g.name.Contains("iron_gate") || g.name.Contains("modular_fort")))
            .Concat(LoadModels(TownPath, n =>
                n.Contains("watermill") || n.Contains("windmill") || n.StartsWith("stall") ||
                n.StartsWith("wall") || n.StartsWith("roof") || n.Contains("door") || n.Contains("window") ||
                n.Contains("chimney") || n.Contains("stairs")))
            .Concat(LoadModels(CastlePath, n => n.StartsWith("wall") || n.StartsWith("tower-") || n.StartsWith("gate") || n.StartsWith("bridge") || n.StartsWith("stairs")))
            .Concat(LoadModels(PirateKitSafe(), n => n.Contains("castle") || n.Contains("wall") || n.Contains("tower") || n.Contains("gate")))
            .Take(140).ToArray();
        var towers = medieval.Where(g => g.name.StartsWith("Roof_Tower") || g.name.Contains("RoundTiles_6x") || g.name.Contains("RoundTiles_8x"))
            .Concat(hero.Where(g => g.name.Contains("modular_fort") || g.name.Contains("iron_gate")))
            .Concat(LoadModels(TownPath, n => n.Contains("tower") || n.Contains("windmill")))
            .Concat(LoadModels(CastlePath, n => n.StartsWith("tower-")))
            .Concat(LoadModels(PirateKitSafe(), n => n.Contains("castle") || n.Contains("tower")))
            .Take(28).ToArray();
        var docks = LoadModels(PirateKitSafe(), n =>
                n.Contains("dock") || n.Contains("pier") || n.Contains("ship") || n.Contains("boat") ||
                n.Contains("plank") || n.Contains("platform") || n.Contains("bridge"))
            .Concat(LoadModels(CastlePath, n => n.StartsWith("bridge")))
            .Take(20).ToArray();
        var props = medievalProps
            .Concat(hero.Where(g => g.name.Contains("bucket") || g.name.Contains("crate") || g.name.Contains("fire_pit")))
            .Concat(LoadModels(TownPath, n => n.StartsWith("stall") || n.Contains("fountain") || n.Contains("barrel") || n.Contains("crate") || n.Contains("cart") || n.Contains("fence")))
            .Concat(LoadModels(PirateKitSafe(), n => n.Contains("barrel") || n.Contains("crate") || n.Contains("chest") || n.Contains("cannon")))
            .Concat(LoadModels(FurniturePath, n =>
                n == "table" || n == "tableCloth" || n == "chair" || n == "bench" ||
                n.StartsWith("bookcase") || n == "desk" || n == "books" || n.StartsWith("sideTable")))
            .Concat(LoadModels(SurvivalPath, n => n.Contains("barrel") || n.Contains("box") || n.Contains("crate") || n.Contains("bucket")))
            .Take(50).ToArray();

        var camp = medievalProps.Where(g => g.name.Contains("Crate") || g.name.Contains("Wagon") || g.name.Contains("Fence"))
            .Concat(hero.Where(g => g.name.Contains("fire_pit") || g.name.Contains("bucket") || g.name.Contains("crate")))
            .Concat(LoadModels(SurvivalPath, n =>
                n.StartsWith("tent") || n.StartsWith("campfire") || n.StartsWith("fence") ||
                n.Contains("bedroll") || n.Contains("barrel") || n.StartsWith("tool-axe")))
            .Take(28).ToArray();
        var ruins = hero.Where(g => g.name.Contains("castle_door") || g.name.Contains("iron_gate"))
            .Concat(LoadModels(GraveyardPath, n =>
                n.StartsWith("crypt") || n.StartsWith("stone-wall") || n.StartsWith("brick-wall") ||
                n.StartsWith("column") || n.StartsWith("gravestone") || n.StartsWith("altar") ||
                n.StartsWith("cross") || n.StartsWith("coffin")))
            .Take(28).ToArray();

        var road = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/M_Road.mat");

        var so = new SerializedObject(gen);
        so.FindProperty("propSeed").intValue = 4242;
        so.FindProperty("waterSize").floatValue = 8000f;
        so.FindProperty("spawnPlayer").boolValue = true;
        so.FindProperty("oceanMaterial").objectReferenceValue = ocean;
        so.FindProperty("highRockMaterial").objectReferenceValue = highRock;
        so.FindProperty("hammerfellMaterial").objectReferenceValue = hammerfell;
        so.FindProperty("sandMaterial").objectReferenceValue = sand;
        so.FindProperty("cityMaterial").objectReferenceValue = city;
        so.FindProperty("mountainMaterial").objectReferenceValue = mountain;
        so.FindProperty("roadMaterial").objectReferenceValue = road;
        Assign(so, "treePrefabs", trees);
        Assign(so, "desertPrefabs", desert);
        Assign(so, "rockPrefabs", rocks);
        Assign(so, "buildingPrefabs", buildings);
        Assign(so, "towerPrefabs", towers);
        Assign(so, "dockPrefabs", docks);
        Assign(so, "propPrefabs", props);
        Assign(so, "campPrefabs", camp);
        Assign(so, "ruinPrefabs", ruins);
        so.ApplyModifiedPropertiesWithoutUndo();

        WireSfx();
        Debug.Log($"[SetupGamePresentation] Wired: medieval={medieval.Length} heroPH={hero.Length} trees={trees.Length} buildings={buildings.Length} camp={camp.Length} ruins={ruins.Length} props={props.Length}");
    }

    private static void WireSfx()
    {
        var systems = GameObject.Find("GameSystems");
        if (systems == null) systems = new GameObject("GameSystems");
        var sfx = systems.GetComponent<GameSfx>() ?? systems.AddComponent<GameSfx>();

        AudioClip FindClip(params string[] names)
        {
            foreach (var name in names)
            {
                foreach (var guid in AssetDatabase.FindAssets($"{name} t:AudioClip", new[] { "Assets/ThirdParty/KenneyAudio" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(name, System.StringComparison.OrdinalIgnoreCase))
                        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
            return null;
        }

        sfx.Configure(
            FindClip("click_002", "click_001"),
            FindClip("confirmation_002", "confirmation_001"),
            FindClip("bookOpen", "doorOpen_1"),
            FindClip("error_002", "error_001"),
            FindClip("drawKnife1", "drawKnife2", "chop"),
            FindClip("knifeSlice", "impactMetal_medium_000", "impactPlate_light_000"),
            FindClip("glass_002", "impactGlass_medium_000", "bong_001"),
            FindClip("handleCoins", "drop_001"),
            FindClip("confirmation_004", "bong_001"));
        EditorUtility.SetDirty(sfx);
    }

    private static string PirateKitSafe()
    {
        return AssetDatabase.IsValidFolder(PiratePath) ? PiratePath : CastlePath;
    }

    private static void EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Art/Textures")) AssetDatabase.CreateFolder("Assets/Art", "Textures");

        var grass = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/grass_diff_1k.jpg");
        var sandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/sand_diff_1k.jpg");
        var rock = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/rock_diff_1k.jpg");

        CreateMat($"{MaterialsPath}/M_Ocean.mat", new Color(0.05f, 0.36f, 0.58f, 0.7f), 0.94f, null, 1f, metallic: 0.08f, transparent: true);
        CreateMat($"{MaterialsPath}/M_HighRock.mat", new Color(0.55f, 0.72f, 0.38f), 0.35f, grass, 56f);
        CreateMat($"{MaterialsPath}/M_Hammerfell.mat", new Color(0.92f, 0.8f, 0.55f), 0.4f, sandTex, 48f);
        CreateMat($"{MaterialsPath}/M_Sand.mat", new Color(0.95f, 0.88f, 0.7f), 0.55f, sandTex, 40f);
        CreateMat($"{MaterialsPath}/M_CityStone.mat", new Color(0.78f, 0.74f, 0.68f), 0.45f, rock, 24f);
        CreateMat($"{MaterialsPath}/M_Mountain.mat", new Color(0.7f, 0.7f, 0.68f), 0.3f, rock, 32f);
        CreateMat($"{MaterialsPath}/M_Road.mat", new Color(0.42f, 0.4f, 0.36f), 0.25f, rock, 16f);
    }

    private static void CreateMat(string path, Color c, float smooth, Texture2D tex, float tileMeters, float metallic = 0f, bool transparent = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (transparent && mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            float scale = tileMeters > 0.1f ? 1f / tileMeters : 1f;
            if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", new Vector2(scale, scale));
        }
        EditorUtility.SetDirty(mat);
    }

    private static void AddPostFxVolume()
    {
        var go = new GameObject("GlobalVolume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        AssetDatabase.CreateAsset(profile, "Assets/Art/IliacBayVolume.asset");

        var color = profile.Add<ColorAdjustments>(true);
        color.active = true;
        color.postExposure.overrideState = true;
        color.postExposure.value = 0.15f;
        color.contrast.overrideState = true;
        color.contrast.value = 12f;
        color.saturation.overrideState = true;
        color.saturation.value = 8f;

        var vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.28f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.45f;

        var bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.18f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.1f;

        EditorUtility.SetDirty(profile);
        vol.sharedProfile = profile;
    }

    private struct Actors { public Transform left, right; }

    private static Actors BuildActors()
    {
        var root = new GameObject("CutsceneActors");
        var chars = LoadModels(CharPath, _ => true);
        Transform MakeActor(string name, int index, Color fallback)
        {
            GameObject go;
            if (chars.Length > index)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(chars[index]);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                go.transform.localScale = Vector3.one * 2.2f;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                var r = go.GetComponent<Renderer>();
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fallback); else m.color = fallback;
                r.sharedMaterial = m;
            }

            // Name plate
            var label = new GameObject("Name");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            return go.transform;
        }

        var left = MakeActor("Liora", 0, new Color(0.55f, 0.35f, 0.75f));
        var right = MakeActor("Kael", Mathf.Min(1, chars.Length - 1), new Color(0.3f, 0.55f, 0.8f));
        left.gameObject.SetActive(false);
        right.gameObject.SetActive(false);
        return new Actors { left = left, right = right };
    }

    private struct UiBits
    {
        public GameObject canvas;
        public GameObject menuRoot;
        public GameObject subtitleRoot;
        public Text speaker;
        public Text subtitle;
        public Text skipHint;
        public Button skipBtn;
        public CanvasGroup menuFade;
        public CanvasGroup subtitleFade;
        public Button startBtn;
        public Button quitBtn;
    }

    private static UiBits BuildMenuUi()
    {
        // EventSystem (Input System package)
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Prefer Input System UI module when available.
            var inputSysType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSysType != null)
            {
                es.AddComponent(inputSysType);
            }
            else
            {
                es.AddComponent<StandaloneInputModule>();
            }
        }

        var canvasGo = new GameObject("UI_Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        UiTheme.EnsureLoaded();

        // Menu root
        var menu = CreatePanel(canvasGo.transform, "MenuRoot", new Color(0.04f, 0.05f, 0.07f, 0.88f), stretch: true);
        var menuFade = menu.AddComponent<CanvasGroup>();

        var menuCard = CreatePanel(menu.transform, "MenuCard", Color.white, stretch: false);
        var cardRt = menuCard.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.28f, 0.18f);
        cardRt.anchorMax = new Vector2(0.72f, 0.82f);
        cardRt.offsetMin = Vector2.zero;
        cardRt.offsetMax = Vector2.zero;
        var cardImg = menuCard.GetComponent<Image>();
        UiTheme.StylePanel(cardImg, UiTheme.PanelBrown, Color.white);

        var title = CreateText(menuCard.transform, "Title", "ILIAC BAY", 72, FontStyle.Bold, TextAnchor.MiddleCenter);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.08f, 0.72f);
        titleRt.anchorMax = new Vector2(0.92f, 0.9f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        title.color = new Color(0.96f, 0.9f, 0.7f);

        var sub = CreateText(menuCard.transform, "Subtitle", "A High Rock & Hammerfell Homage", 24, FontStyle.Italic, TextAnchor.MiddleCenter);
        var subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.08f, 0.62f);
        subRt.anchorMax = new Vector2(0.92f, 0.72f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        sub.color = new Color(0.85f, 0.8f, 0.7f);

        var startBtn = CreateButton(menuCard.transform, "StartButton", "START JOURNEY", new Vector2(0.5f, 0.42f), new Color(0.35f, 0.28f, 0.18f, 1f));
        var quitBtn = CreateButton(menuCard.transform, "QuitButton", "QUIT", new Vector2(0.5f, 0.28f), new Color(0.18f, 0.2f, 0.24f, 1f));
        UiTheme.StyleButton(startBtn, UiTheme.ButtonLong, UiTheme.ButtonLongPressed);
        UiTheme.StyleButton(quitBtn, UiTheme.ButtonLong, UiTheme.ButtonLongPressed);
        startBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 70);
        quitBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(360, 64);

        // Subtitles
        var subs = CreatePanel(canvasGo.transform, "SubtitleRoot", Color.white, stretch: false);
        var subsRt = subs.GetComponent<RectTransform>();
        subsRt.anchorMin = new Vector2(0.15f, 0.06f);
        subsRt.anchorMax = new Vector2(0.85f, 0.24f);
        subsRt.offsetMin = Vector2.zero;
        subsRt.offsetMax = Vector2.zero;
        UiTheme.StylePanel(subs.GetComponent<Image>(), UiTheme.PanelBrown, Color.white);
        var subFade = subs.AddComponent<CanvasGroup>();
        var speaker = CreateText(subs.transform, "Speaker", "Speaker", 26, FontStyle.Bold, TextAnchor.UpperLeft);
        var speakerRt = speaker.rectTransform;
        speakerRt.anchorMin = new Vector2(0.06f, 0.58f);
        speakerRt.anchorMax = new Vector2(0.94f, 0.92f);
        speakerRt.offsetMin = Vector2.zero;
        speakerRt.offsetMax = Vector2.zero;
        speaker.color = new Color(1f, 0.85f, 0.45f);
        var body = CreateText(subs.transform, "Body", "…", 28, FontStyle.Normal, TextAnchor.UpperLeft);
        var bodyRt = body.rectTransform;
        bodyRt.anchorMin = new Vector2(0.06f, 0.1f);
        bodyRt.anchorMax = new Vector2(0.94f, 0.56f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;
        body.color = new Color(0.96f, 0.93f, 0.85f);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        subs.SetActive(false);

        // Persistent skip control (top-right) during dialogue / flyover
        var skipBtn = CreateButton(canvasGo.transform, "SkipButton", "SKIP", new Vector2(0.9f, 0.92f), new Color(0.25f, 0.22f, 0.16f, 0.92f));
        var skipRt = skipBtn.GetComponent<RectTransform>();
        skipRt.sizeDelta = new Vector2(180, 52);
        UiTheme.StyleButton(skipBtn, UiTheme.ButtonLong, UiTheme.ButtonLongPressed);
        skipBtn.gameObject.SetActive(false);

        var skipHint = CreateText(canvasGo.transform, "SkipHint", "SPACE / ENTER — skip dialogue", 22, FontStyle.Italic, TextAnchor.MiddleRight);
        var hintRt = skipHint.rectTransform;
        hintRt.anchorMin = new Vector2(0.55f, 0.84f);
        hintRt.anchorMax = new Vector2(0.88f, 0.9f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        skipHint.color = new Color(0.9f, 0.85f, 0.7f, 0.95f);
        skipHint.gameObject.SetActive(false);

        return new UiBits
        {
            canvas = canvasGo,
            menuRoot = menu,
            subtitleRoot = subs,
            speaker = speaker,
            subtitle = body,
            skipHint = skipHint,
            skipBtn = skipBtn,
            menuFade = menuFade,
            subtitleFade = subFade,
            startBtn = startBtn,
            quitBtn = quitBtn
        };
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, bool stretch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
        {
            // Unity version fallbacks
            text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        }
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(280, 64);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = CreateText(go.transform, "Label", label, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        t.color = new Color(0.95f, 0.92f, 0.85f);
        return btn;
    }

    private static GameObject[] LoadModels(string folder, System.Func<string, bool> pred)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return System.Array.Empty<GameObject>();
        var list = new List<GameObject>();
        foreach (var filter in new[] { "t:Model", "t:Prefab" })
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!pred(name)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && !list.Contains(go)) list.Add(go);
            }
        }
        return list.OrderBy(g => g.name).ToArray();
    }

    private static void Assign(SerializedObject so, string name, GameObject[] values)
    {
        var p = so.FindProperty(name);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
