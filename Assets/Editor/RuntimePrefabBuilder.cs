using UnityEditor;
using UnityEngine;

/// <summary>Creates the regenerable W-04 runtime roots consumed by generated scenes.</summary>
public static class RuntimePrefabBuilder
{
    public const string PlayerPath = "Assets/Resources/Prefabs/Runtime/Player.prefab";
    public const string SystemsPath = "Assets/Resources/Prefabs/Runtime/GameSystems.prefab";
    public const string NpcPath = "Assets/Resources/Prefabs/Runtime/Npc.prefab";
    public const string HudPath = "Assets/Resources/Prefabs/Runtime/Hud.prefab";
    public const string NpcDataFolder = "Assets/Resources/Data/Npcs";

    [MenuItem("Kessil/Architecture/Rebuild Runtime Prefabs")]
    public static void Install()
    {
        EnsureFolders();
        CreatePlayerPrefab();
        CreateNpcPrefab();
        CreateHudPrefab();
        CreateSystemsPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RuntimePrefabs] Player and GameSystems prefabs rebuilt.");
    }

    public static void EnsurePrefabs()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(NpcPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) == null)
            Install();
    }

    private static void CreatePlayerPrefab()
    {
        var player = new GameObject("Player");
        player.layer = GameLayers.Player;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.skinWidth = 0.08f;
        cc.minMoveDistance = 0f;
        cc.stepOffset = 0.35f;

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, 1.55f, 0f);
        var cameraGo = new GameObject("Main Camera");
        cameraGo.transform.SetParent(pivot, false);
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = WorldLayout.CameraFarPlane;
        camera.cullingMask &= ~(1 << GameLayers.Player);
        camera.enabled = false;
        cameraGo.AddComponent<AudioListener>();

        var controller = player.AddComponent<SimplePlayerController>();
        controller.SetCameraPivot(pivot);
        controller.enabled = false;
        player.AddComponent<PlayerStats>();
        player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerEquipment>();
        player.AddComponent<SpellCaster>();
        player.AddComponent<SkillSystem>();
        player.AddComponent<PlayerCombat>().enabled = false;
        player.AddComponent<PlayerInteract>();
        player.AddComponent<PlayerSafetyGuard>();

        PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        Object.DestroyImmediate(player);
    }

    private static void CreateSystemsPrefab()
    {
        var systems = new GameObject("GameSystems");
        systems.AddComponent<AudioSource>();
        systems.AddComponent<GameSfx>();
        systems.AddComponent<StoryDirector>();
        systems.AddComponent<TopicDialogueService>();
        systems.AddComponent<CinematicRunner>();
        systems.AddComponent<GreyThreadDirector>();
        var bootstrap = systems.AddComponent<GameSystemsBootstrap>();
        var npcAssets = CreateNpcAssets();
        CreateDialogueAssets();
        CreateQuestAssets();
        CreateCinematicAssets();
        var serializedBootstrap = new SerializedObject(bootstrap);
        var archetypes = serializedBootstrap.FindProperty("npcArchetypes");
        archetypes.arraySize = npcAssets.Length;
        for (int i = 0; i < npcAssets.Length; i++)
            archetypes.GetArrayElementAtIndex(i).objectReferenceValue = npcAssets[i];
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();
        systems.AddComponent<TimeWeatherSystem>();
        systems.AddComponent<DiscoveryTravelSystem>();
        systems.AddComponent<QuestSystem>();
        systems.AddComponent<GameHud>();
        systems.AddComponent<SaveLoadService>();
        PrefabUtility.SaveAsPrefabAsset(systems, SystemsPath);
        Object.DestroyImmediate(systems);
    }

    private static void CreateNpcPrefab()
    {
        var npc = new GameObject("Npc");
        npc.layer = GameLayers.Npc;
        var capsule = npc.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        npc.AddComponent<NpcInteractable>();
        PrefabUtility.SaveAsPrefabAsset(npc, NpcPath);
        Object.DestroyImmediate(npc);
    }

    private static void CreateHudPrefab()
    {
        var host = new GameObject("HudPrefabBuilder");
        var hud = host.AddComponent<GameHud>();
        hud.BuildPrefabVisuals();
        var visualRoot = host.transform.Find("GameHudCanvas");
        if (visualRoot == null)
            throw new MissingReferenceException("GameHud did not build its visual root.");
        PrefabUtility.SaveAsPrefabAsset(visualRoot.gameObject, HudPath);
        Object.DestroyImmediate(host);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Prefabs");
        EnsureFolder("Assets/Resources/Prefabs", "Runtime");
        EnsureFolder("Assets/Resources", "Data");
        EnsureFolder("Assets/Resources/Data", "Npcs");
        EnsureFolder("Assets/Resources/Data", "Dialogue");
        EnsureFolder("Assets/Resources/Data", "Quests");
        EnsureFolder("Assets/Resources/Data", "Cinematics");
    }

    private static void CreateQuestAssets()
    {
        CreateQuest("main_bay", "Winds of the Kessil",
            "Learn the lay of the bay. Discover Estmere or Qadris, then return to Caldemar's gate.",
            "Discover Estmere or Qadris", 0, null, "city_east");
        CreateQuest("bounty_bandits", "Kelrith Bounty",
            "Bandits prey on the southern road from Caldemar. Clear their camp.",
            "Slay bandits (0/3)", 3, "Bandit", null);
        CreateQuest("ruin_scout", "Coastal Ruin",
            "Scout the ruin south of Caldemar and survive whatever lurks there.",
            "Discover Coastal Ruin", 0, null, "coastal_ruin");
    }

    private static void CreateQuest(string id, string title, string description, string stage,
        int targetCount, string targetEnemy, string targetLocation)
    {
        string path = $"Assets/Resources/Data/Quests/{id}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
        if (asset == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<QuestDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("title").stringValue = title;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("initialStageText").stringValue = stage;
        serialized.FindProperty("targetCount").intValue = targetCount;
        serialized.FindProperty("targetEnemy").stringValue = targetEnemy ?? string.Empty;
        serialized.FindProperty("targetLocationId").stringValue = targetLocation ?? string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void CreateCinematicAssets()
    {
        const string path = "Assets/Resources/Data/Cinematics/ch01_title_crawl.asset";
        var asset = AssetDatabase.LoadAssetAtPath<CinematicSequence>(path);
        if (asset == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<CinematicSequence>();
            AssetDatabase.CreateAsset(asset, path);
        }
        asset.Configure("cin.title_crawl", 4f,
            new[]
            {
                new CinematicCue { AtSeconds = 0f, Action = "advance_beat", Key = "stage.escape", Value = "B640" }
            },
            new[] { new StoryFlag { Id = "flag.title_crawl_shown", Value = "true" } });
        EditorUtility.SetDirty(asset);
    }

    private static void CreateDialogueAssets()
    {
        CreateDialogue("topic_black_crystals", "black crystals",
            "The black crystals are not mined. Their manifests point to living prisoners.",
            null, null, new DialogueCondition { Key = "evidence_count", Operator = "min", Value = "3" });
        CreateDialogue("topic_black_crystals_rumor", "black crystals",
            "Black crystals? Dockside superstition. Ask the Arcanum if you enjoy locked doors.",
            null, null);
        CreateDialogue("topic_prince_trade", "the prince",
            "Merchant manifests put the prince's last transport beneath the east tower.",
            null, null, new DialogueCondition { Key = "route", Value = "route.trade" });
    }

    private static DialogueTopic CreateDialogue(string id, string keyword, string response,
        string actorId, string factionId, params DialogueCondition[] conditions)
    {
        string path = $"Assets/Resources/Data/Dialogue/{id}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<DialogueTopic>(path);
        if (asset == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<DialogueTopic>();
            AssetDatabase.CreateAsset(asset, path);
        }
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("keyword").stringValue = keyword;
        serialized.FindProperty("actorId").stringValue = actorId ?? string.Empty;
        serialized.FindProperty("factionId").stringValue = factionId ?? string.Empty;
        serialized.FindProperty("response").stringValue = response;
        var list = serialized.FindProperty("conditions");
        list.arraySize = conditions.Length;
        for (int i = 0; i < conditions.Length; i++)
        {
            var item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("Key").stringValue = conditions[i].Key;
            item.FindPropertyRelative("Operator").stringValue = conditions[i].Operator;
            item.FindPropertyRelative("Value").stringValue = conditions[i].Value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static NpcArchetype[] CreateNpcAssets()
    {
        return new[]
        {
            CreateNpc("mira_provisioner", "Mira the Provisioner", "character-female-b", "city_west",
                new Vector3(15f, 0f, 10f), new Color(0.35f, 0.45f, 0.7f),
                new[] { "Potions and rumors, traveler.", "Estmere lies far east across the hills." }, true, false),
            CreateNpc("ralen_gate_guard", "Gate Guard Ralen", "character-male-e", "city_west",
                new Vector3(-10f, 0f, 25f), new Color(0.4f, 0.4f, 0.45f),
                new[] { "Keep your blade sheathed in the city.", "Bandits haunt the southern road." }, false, false),
            CreateNpc("alid_captain", "Captain Alid", "character-male-c", "city_west",
                new Vector3(10f, 0f, -15f), new Color(0.55f, 0.4f, 0.25f),
                new[] { "Clear the Kelrith bandits.", "The bay remembers those who wander it." }, false, true),
            CreateNpc("estmere_dockhand", "Estmere Dockhand", "character-male-b", "city_east",
                new Vector3(-20f, 0f, 10f), new Color(0.3f, 0.5f, 0.4f),
                new[] { "Welcome to Estmere, jewel of the Esk." }, false, false),
            CreateNpc("qadris_scout", "Qadris Scout", "character-female-d", "city_south",
                new Vector3(20f, 0f, -10f), new Color(0.7f, 0.55f, 0.3f),
                new[] { "Hot wind and hotter steel — this is Qadris." }, false, false)
        };
    }

    private static NpcArchetype CreateNpc(string id, string displayName, string modelId,
        string anchorSiteId, Vector3 offset, Color tint, string[] lines, bool merchant, bool questGiver)
    {
        string path = $"{NpcDataFolder}/{id}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<NpcArchetype>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<NpcArchetype>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("modelId").stringValue = modelId;
        serialized.FindProperty("anchorSiteId").stringValue = anchorSiteId;
        serialized.FindProperty("offset").vector3Value = offset;
        serialized.FindProperty("tint").colorValue = tint;
        serialized.FindProperty("merchant").boolValue = merchant;
        serialized.FindProperty("questGiver").boolValue = questGiver;
        var dialogue = serialized.FindProperty("lines");
        dialogue.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
            dialogue.GetArrayElementAtIndex(i).stringValue = lines[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
