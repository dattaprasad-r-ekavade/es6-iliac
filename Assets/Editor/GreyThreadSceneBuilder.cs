using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Regenerates the authored Chapter 01 grey-thread spaces. These are intentionally
/// simple, collision-backed modular rooms with elevation and silhouettes so the route
/// can be blind-played before final environment art exists.
/// </summary>
public static class GreyThreadSceneBuilder
{
    public const string Folder = "Assets/Scenes/Chapter01";

    public static string[] ScenePaths
    {
        get
        {
            var paths = new List<string>();
            foreach (var spec in GreyThreadSceneCatalog.Scenes)
                paths.Add(Path.Combine(Folder, spec.Name + ".unity").Replace('\\', '/'));
            return paths.ToArray();
        }
    }

    [MenuItem("Kessil/Story/Build VS2 Grey Thread Scenes")]
    public static void Build()
    {
        EnsureFolders();
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
            BuildScene(spec);

        SceneArchitectureBuilder.EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GreyThread] Built {GreyThreadSceneCatalog.Scenes.Count} additive Chapter 01 scenes.");
    }

    private static void BuildScene(GreyThreadSceneCatalog.SceneSpec spec)
    {
        var path = $"{Folder}/{spec.Name}.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject($"GreyThread_{spec.Name}");
        var context = root.AddComponent<SceneContext>();
        context.Configure(spec.SceneId, spec.Name == "Council_Arrival" ? "spawn.council" : "spawn.entry");

        CreateSpawn(root.transform, "spawn.entry", new Vector3(0f, 1.1f, -7f), Quaternion.identity);
        if (spec.Name == "Prison")
            CreateSpawn(root.transform, "spawn.route", new Vector3(-7f, 1.1f, -5f), Quaternion.Euler(0f, 90f, 0f));
        if (spec.Name == "Sea_Cave")
            CreateSpawn(root.transform, "spawn.escape", new Vector3(0f, 1.1f, -6f), Quaternion.Euler(0f, 180f, 0f));
        if (spec.Name == "Council_Arrival")
            CreateSpawn(root.transform, "spawn.council", new Vector3(0f, 1.1f, -6f), Quaternion.identity);

        var geometry = new GameObject("GreyGeometry").transform;
        geometry.SetParent(root.transform, false);

        // Room 0 is the entrance hall and keeps the authored silhouette the VS2 gate and the
        // screenshots were built against. Deeper chambers extend along +Z behind it.
        var floor = CreateBlock(geometry, "RaisedStoneFloor", new Vector3(0f, -0.25f, 0f), new Vector3(30f, 0.5f, 22f), Stone(spec));
        floor.isStatic = true;
        BuildSteps(geometry, spec);
        BuildWalls(geometry, spec);
        BuildColumns(geometry, spec);
        BuildAccent(geometry, spec);
        CreateTitle(geometry, spec);

        BuildDeeperRooms(geometry, spec);
        if (spec.HasExitDoor) BuildExit(root.transform);
        BuildMechanic(root.transform, spec);
        BuildCast(root.transform, spec);

        var lightGo = new GameObject("GreyThread_Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = Color.Lerp(new Color(1f, 0.82f, 0.64f), spec.Accent, 0.18f);

        var ambient = new GameObject("GreyThread_Ambient");
        ambient.transform.SetParent(root.transform, false);
        var fill = ambient.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.range = 24f;
        fill.intensity = 2.2f;
        fill.color = Color.Lerp(spec.Accent, Color.white, 0.25f);
        fill.transform.position = new Vector3(0f, 5.5f, 2.5f);

        EditorSceneManager.SaveScene(scene, path);
    }

    /// <summary>Depth of one chamber, and the width of the doorway joining two.</summary>
    private const float RoomDepth = 24f;
    private const float DoorWidth = 6f;

    /// <summary>
    /// Chambers beyond the entrance hall, each joined to the last by a real doorway.
    ///
    /// One box is a placeholder. Three connected rooms with doors between them is somewhere a
    /// player can be briefly lost, which is the difference a playtest can actually report on.
    /// </summary>
    private static void BuildDeeperRooms(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        if (spec.Rooms <= 1) return;
        var stone = Stone(spec);

        for (int room = 1; room < spec.Rooms; room++)
        {
            float z = 10f + RoomDepth * (room - 0.5f);
            var holder = new GameObject("Room_" + room).transform;
            holder.SetParent(parent, false);

            CreateBlock(holder, "Floor", new Vector3(0f, -0.25f, z), new Vector3(26f, 0.5f, RoomDepth), stone);
            CreateBlock(holder, "Ceiling", new Vector3(0f, 7f, z), new Vector3(26f, 0.5f, RoomDepth), stone);
            CreateBlock(holder, "Wall_Left", new Vector3(-13f, 3.5f, z), new Vector3(0.8f, 7f, RoomDepth), stone);
            CreateBlock(holder, "Wall_Right", new Vector3(13f, 3.5f, z), new Vector3(0.8f, 7f, RoomDepth), stone);

            float farZ = z + RoomDepth * 0.5f;
            bool last = room == spec.Rooms - 1;
            if (last)
            {
                CreateBlock(holder, "Wall_Far", new Vector3(0f, 3.5f, farZ), new Vector3(26f, 7f, 0.8f), stone);
            }
            else
            {
                float side = (26f - DoorWidth) * 0.5f;
                float offset = (DoorWidth + side) * 0.5f;
                CreateBlock(holder, "Wall_Far_L", new Vector3(-offset, 3.5f, farZ), new Vector3(side, 7f, 0.8f), stone);
                CreateBlock(holder, "Wall_Far_R", new Vector3(offset, 3.5f, farZ), new Vector3(side, 7f, 0.8f), stone);
                CreateBlock(holder, "Wall_Far_Top", new Vector3(0f, 6.2f, farZ), new Vector3(DoorWidth, 1.6f, 0.8f), stone);
            }

            var lamp = new GameObject("Room_" + room + "_Light");
            lamp.transform.SetParent(holder, false);
            lamp.transform.localPosition = new Vector3(0f, 5f, z);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 22f;
            light.intensity = 1.5f;
            light.color = Color.Lerp(spec.Accent, Color.white, 0.3f);
        }

        // Open the entrance hall back wall where the corridor now begins, or the deeper rooms
        // are unreachable and the interior is a box with wasted geometry behind it.
        float backSide = (30f - DoorWidth) * 0.5f;
        float backOffset = (DoorWidth + backSide) * 0.5f;
        var back = parent.Find("Wall_Back");
        if (back != null) Object.DestroyImmediate(back.gameObject);
        CreateBlock(parent, "Wall_Back_L", new Vector3(-backOffset, 3f, 10f), new Vector3(backSide, 6f, 0.8f), stone);
        CreateBlock(parent, "Wall_Back_R", new Vector3(backOffset, 3f, 10f), new Vector3(backSide, 6f, 0.8f), stone);
    }

    /// <summary>The way out, beside the entrance the player arrived through.</summary>
    private static void BuildExit(Transform root)
    {
        var exit = new GameObject("InteriorExit");
        exit.transform.SetParent(root, false);
        exit.transform.localPosition = new Vector3(0f, 1.5f, -9f);
        var trigger = exit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(10f, 3f, 3f);
        exit.AddComponent<InteriorExit>().Configure("Back to Estmere");
    }

    /// <summary>
    /// Put the route mechanic in the deepest chamber, so the player has to walk to it.
    /// Without this the VS4 systems are code that nothing in the game ever touches.
    /// </summary>
    private static void BuildMechanic(Transform root, GreyThreadSceneCatalog.SceneSpec spec)
    {
        if (spec.Mechanic == GreyThreadSceneCatalog.Feature.None) return;

        float deepZ = 10f + RoomDepth * (Mathf.Max(1, spec.Rooms) - 0.5f);
        var holder = new GameObject("Mechanic_" + spec.Mechanic).transform;
        holder.SetParent(root, false);
        holder.localPosition = new Vector3(0f, 0f, deepZ);

        switch (spec.Mechanic)
        {
            case GreyThreadSceneCatalog.Feature.Lock:
            {
                var door = CreateBlock(holder, "LockedDoor", new Vector3(0f, 2f, 6f), new Vector3(5f, 4f, 0.6f), Accent(spec));
                door.AddComponent<DoorAndLock>().Configure(true, 25f, "tower_key");
                AddWatcher(holder, new Vector3(6f, 0f, -2f), -120f);
                break;
            }

            case GreyThreadSceneCatalog.Feature.Pickpocket:
            {
                var mark = new GameObject("Mark");
                mark.transform.SetParent(holder, false);
                mark.transform.localPosition = new Vector3(-4f, 0f, 2f);
                mark.AddComponent<PickpocketTarget>().Configure(15f,
                    new PickpocketTarget.Holding { Id = "cell_key", Name = "Cell Key", Kind = "key" });
                AddWatcher(holder, new Vector3(5f, 0f, 3f), -140f);
                break;
            }

            case GreyThreadSceneCatalog.Feature.Boat:
            {
                var hull = CreateBlock(holder, "Boat", new Vector3(0f, 0.6f, 4f), new Vector3(3f, 1.2f, 7f), Accent(spec));
                hull.AddComponent<SailingController>();
                break;
            }

            case GreyThreadSceneCatalog.Feature.CombatDummy:
                AddTrainingActor(holder, "SparringDummy", "Sparring Dummy", 80f, "tutorial_dummy", 4f);
                break;

            case GreyThreadSceneCatalog.Feature.SpellTarget:
                AddTrainingActor(holder, "PracticeTarget", "Practice Effigy", 120f, "arcanum_effigy", 5f);
                break;
        }
    }

    private static void AddWatcher(Transform holder, Vector3 localPosition, float facingDegrees)
    {
        var watcher = new GameObject("Watcher");
        watcher.transform.SetParent(holder, false);
        watcher.transform.localPosition = localPosition;
        watcher.transform.localRotation = Quaternion.Euler(0f, facingDegrees, 0f);
        watcher.AddComponent<DetectionWatcher>();
    }

    private static void AddTrainingActor(Transform holder, string objectName, string displayName,
        float health, string spawnId, float z)
    {
        var actor = new GameObject(objectName);
        actor.transform.SetParent(holder, false);
        actor.transform.localPosition = new Vector3(0f, 0f, z);
        var capsule = actor.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.4f;
        capsule.center = Vector3.up * 0.9f;
        actor.AddComponent<EnemyBrain>().Setup(displayName, health, spawnId);
        WorldTagger.SetLayerRecursive(actor, GameLayers.Enemy);
    }

    /// <summary>
    /// Put the chapter's named cast where the beat sheet says they are.
    ///
    /// Until now the topic dialogue system was fully built, tested, and wired to nothing in
    /// any scene - there was nobody to talk to. These are billboard actors with role ids, so
    /// the same knowledge base answers differently depending on who is asked.
    /// </summary>
    private static void BuildCast(Transform root, GreyThreadSceneCatalog.SceneSpec spec)
    {
        switch (spec.Name)
        {
            case "Docks":
                Cast(root, "role.processing_guard", "The Registrar", "faction.crown",
                    spec, new Vector3(-4f, 0f, 3f), "the law", "the wreck", "ratnapur");
                break;

            case "Tutorial_Warrior":
                Cast(root, "role.instructor_warrior", "Senapati Karan", "faction.crown",
                    spec, new Vector3(4f, 0f, 3f), "the blade", "the transport", "the raja");
                break;

            case "Order_Hall":
                Cast(root, "role.instructor_mage", "Acharya Meera", "faction.order",
                    spec, new Vector3(-4f, 0f, 3f), "casting", "the order", "jiva stones");
                break;

            case "Harbor":
                Cast(root, "role.instructor_trade", "Harbourmaster Vasu", "faction.crown",
                    spec, new Vector3(4f, 0f, 3f), "sailing", "the tower", "ratnapur");
                break;

            case "Prison":
                Cast(root, "role.prisoner_a", "Hari", null,
                    spec, new Vector3(-5f, 0f, 2f), "the operation", "jiva stones");
                Cast(root, "role.prisoner_b", "Lekha", null,
                    spec, new Vector3(5f, 0f, 2f), "the count", "the operation");
                break;

            case "Palace":
                Cast(root, "role.king", "Raja Vikram", "faction.crown",
                    spec, new Vector3(0f, 0f, 6f), "the raja", "the stambha");
                break;

            case "Council_Arrival":
                Cast(root, "role.council_contact", "Mantri Devan", "faction.council",
                    spec, new Vector3(0f, 0f, 4f), "the stambha", "ratnapur");
                break;
        }
    }

    private static void Cast(Transform root, string actorId, string displayName, string factionId,
        GreyThreadSceneCatalog.SceneSpec spec, Vector3 position, params string[] opensWith)
    {
        var actor = BillboardActor.Spawn(displayName.Replace(' ', '_'), position,
            Color.Lerp(spec.Accent, Color.white, 0.35f), 1.85f);
        var root_go = actor.transform.root;
        root_go.SetParent(root, true);
        root_go.localPosition = position;

        root_go.gameObject.AddComponent<SpeakingActor>()
            .Configure(actorId, displayName, factionId, spec.SceneId, opensWith);
    }

    private static void BuildWalls(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        CreateBlock(parent, "Wall_Back", new Vector3(0f, 3f, 10f), new Vector3(30f, 6f, 0.8f), stone);
        CreateBlock(parent, "Wall_Left", new Vector3(-14.6f, 3f, 0f), new Vector3(0.8f, 6f, 20f), stone);
        CreateBlock(parent, "Wall_Right", new Vector3(14.6f, 3f, 0f), new Vector3(0.8f, 6f, 20f), stone);
        CreateBlock(parent, "Gate_Pillar_L", new Vector3(-7.5f, 3f, -9.6f), new Vector3(1.4f, 6f, 1.4f), stone);
        CreateBlock(parent, "Gate_Pillar_R", new Vector3(7.5f, 3f, -9.6f), new Vector3(1.4f, 6f, 1.4f), stone);
        CreateBlock(parent, "Gate_Lintel", new Vector3(0f, 5.7f, -9.6f), new Vector3(16.4f, 1.4f, 1.4f), stone);
        CreateBlock(parent, "Back_Raised_Wall", new Vector3(0f, 6.3f, 8.2f), new Vector3(16f, 0.55f, 0.55f), Accent(spec));
    }

    private static void BuildColumns(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -11f : 11f;
            float z = i < 2 ? -1f : 6f;
            CreateBlock(parent, $"Column_{i}", new Vector3(x, 3.4f, z), new Vector3(1.3f, 6.8f, 1.3f), stone);
            CreateBlock(parent, $"ColumnCap_{i}", new Vector3(x, 6.9f, z), new Vector3(2.0f, 0.45f, 2.0f), Accent(spec));
        }
    }

    private static void BuildSteps(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var stone = Stone(spec);
        for (int i = 0; i < 4; i++)
        {
            float z = 1f + i * 1.15f;
            CreateBlock(parent, $"Step_{i}", new Vector3(0f, 0.18f + i * 0.36f, z), new Vector3(13f - i * 1.2f, 0.36f + i * 0.12f, 1.2f), stone);
        }
        CreateBlock(parent, "RaisedStage", new Vector3(0f, 1.8f, 6.3f), new Vector3(12f, 1.2f, 4f), stone);
    }

    private static void BuildAccent(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var accent = Accent(spec);
        var marker = CreateBlock(parent, "StoryMarker", new Vector3(0f, 3.0f, 5.5f), new Vector3(2.4f, 4.6f, 0.35f), accent);
        marker.transform.Rotate(0f, 45f, 0f);
        marker.GetComponent<Renderer>().sharedMaterial = accent;

        var brazier = CreateBlock(parent, "Brazier_Left", new Vector3(-5.5f, 1.6f, 5.2f), new Vector3(1.1f, 2.8f, 1.1f), accent);
        var brazierRight = Object.Instantiate(brazier, parent);
        brazierRight.name = "Brazier_Right";
        brazierRight.transform.localPosition = new Vector3(5.5f, 1.6f, 5.2f);
        var flame = brazier.AddComponent<Light>();
        flame.type = LightType.Point;
        flame.range = 8f;
        flame.intensity = 1.6f;
        flame.color = new Color(1f, 0.55f, 0.18f);
        var flameRight = brazierRight.AddComponent<Light>();
        flameRight.type = LightType.Point;
        flameRight.range = 8f;
        flameRight.intensity = 1.6f;
        flameRight.color = new Color(1f, 0.55f, 0.18f);
    }

    private static void CreateTitle(Transform parent, GreyThreadSceneCatalog.SceneSpec spec)
    {
        var title = new GameObject("GreyThread_Title");
        title.transform.SetParent(parent, false);
        title.transform.position = new Vector3(0f, 7.5f, 9.2f);
        // TextMesh faces +Z by default; the capture/play camera approaches from -Z.
        // Keeping the authored orientation readable also makes the scene useful when
        // opened directly in the Unity editor.
        title.transform.rotation = Quaternion.identity;
        var text = title.AddComponent<TextMesh>();
        text.text = spec.Title.ToUpperInvariant();
        text.fontSize = 42;
        text.characterSize = 0.12f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.Lerp(new Color(0.94f, 0.82f, 0.58f), spec.Accent, 0.35f);
    }

    private static SceneSpawnPoint CreateSpawn(Transform parent, string id, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject("Spawn_" + id.Replace('.', '_'));
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var spawn = go.AddComponent<SceneSpawnPoint>();
        spawn.Configure(id);
        return spawn;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.layer = GameLayers.Structure;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            ProceduralSurface.ApplyTiling(renderer, scale);
        }
        return go;
    }

    /// <summary>
    /// The interior shell. Shared and asset-backed, so every room in the chapter batches into
    /// one material and carries the same drawn masonry as the region outside.
    ///
    /// This used to be a per-scene flat colour tinted toward <c>spec.Accent</c>, which is what
    /// made interiors read as coloured boxes. Scene identity now lives entirely in
    /// <see cref="Accent"/> — the story marker and braziers — where a strong colour is a
    /// landmark rather than a wall.
    /// </summary>
    private static Material Stone(GreyThreadSceneCatalog.SceneSpec spec) =>
        ProceduralSurfaceBaker.MaterialFor(ProceduralSurface.Kind.Stone);

    private static Material Accent(GreyThreadSceneCatalog.SceneSpec spec)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.name = spec.Name + "_Accent";
        material.color = Color.Lerp(spec.Accent, new Color(0.9f, 0.68f, 0.35f), 0.3f);
        return material;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenes", "Chapter01");
    }
}
