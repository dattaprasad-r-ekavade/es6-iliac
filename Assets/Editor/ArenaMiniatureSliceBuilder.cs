using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Materialises <see cref="ArenaMiniatureSliceLayout"/> using the project's generated 64 px
/// surfaces. Geometry stays deliberately inexpensive; silhouette, contour, rhythm and a clear
/// traversable lane do the visual work.
/// </summary>
public static class ArenaMiniatureSliceBuilder
{
    [MenuItem("Kessil/Art Direction/Build + Capture Arena Miniature Slice")]
    public static void BuildAndCapture()
    {
        ProceduralSurfaceBaker.BakeAll();
        CapitalRegionBuilder.Build();
        GreyThreadSceneBuilder.Build();
        Capture();
        Debug.Log("[ArenaMiniature] Rebuilt and captured the Ratnapur street/prison slice.");
    }

    public static void BuildCapitalStreet(Transform parent)
    {
        var root = new GameObject(ArenaMiniatureSliceLayout.StreetRootName).transform;
        root.SetParent(parent, false);
        root.position = ArenaMiniatureSliceLayout.StreetOrigin;

        Material road = Mat(ProceduralSurface.Kind.Roof);
        Material stone = Mat(ProceduralSurface.Kind.Stone);
        Material plaster = Mat(ProceduralSurface.Kind.Plaster);
        Material timber = Mat(ProceduralSurface.Kind.Timber);
        Material foliage = Mat(ProceduralSurface.Kind.Foliage);
        Material water = Mat(ProceduralSurface.Kind.Water);

        // A paved apron under the whole corridor. The facades stand in four blocks with gaps
        // between them, and through those gaps you could see the region's temperate ground —
        // strips of grass running down the middle of a city, plainly visible in the capture.
        Block(root, "Street_Apron", new Vector3(0f, 0.01f, 0f),
            new Vector3(44f, 0.04f, ArenaMiniatureSliceLayout.StreetLength + 24f),
            stone, false, GameLayers.Ground);

        // The underlying region ground owns collision. These thin painted registers sit just
        // above it, so the street has no invisible curb or duplicate collision plane.
        Block(root, "Road_PaintedRegister", new Vector3(0f, 0.025f, 0f),
            new Vector3(ArenaMiniatureSliceLayout.StreetClearHalfWidth * 2f, 0.05f,
                ArenaMiniatureSliceLayout.StreetLength), road, false, GameLayers.Ground);
        Block(root, "Drain_Centre", new Vector3(0f, 0.055f, 0f),
            new Vector3(0.7f, 0.06f, ArenaMiniatureSliceLayout.StreetLength - 4f), stone,
            false, GameLayers.Prop);

        // Low walkable plinths define the street edge without making a wall of the pavement.
        foreach (float side in new[] { -1f, 1f })
        {
            Block(root, side < 0f ? "Walkway_West" : "Walkway_East",
                new Vector3(side * 4.9f, 0.11f, 0f),
                new Vector3(2.6f, 0.22f, ArenaMiniatureSliceLayout.StreetLength),
                stone, true, GameLayers.Ground);
        }

        foreach (var facade in ArenaMiniatureSliceLayout.StreetFacades)
            BuildFacade(root, facade, plaster, stone, road, timber);

        foreach (var prop in ArenaMiniatureSliceLayout.StreetProps)
            BuildStreetProp(root, prop, stone, road, timber, foliage, water);

        BuildStreetFigures(root);
        BuildStreetPickups(root, stone, road, water);
    }

    /// <summary>
    /// The street's inhabitants — and they talk.
    ///
    /// These stood here as silent billboards, which meant the one authored, dense, good-looking
    /// part of the world was still a place where nothing answered you. They now carry
    /// <see cref="SpeakingActor"/>, so every one of them can be asked about Ratnapur, the raja,
    /// the Stambha and jiva stones. That last topic is the one that states the chapter's whole
    /// moral premise, so a visitor who stops and talks to a spice vendor for thirty seconds
    /// hears what the game is actually about.
    ///
    /// No new writing was needed: those four topics are authored with no actor id, which the
    /// resolver treats as common knowledge anyone will answer.
    /// </summary>
    private static void BuildStreetFigures(Transform root)
    {
        var palette = ArtDirection.Active.Palette;
        var tints = new[] { palette.CityStone, palette.Road, palette.Sand, palette.Mountain };

        foreach (var spec in ArenaMiniatureSliceLayout.StreetFigures)
        {
            var actor = BillboardActor.Spawn(spec.Id, root.TransformPoint(spec.LocalPosition),
                tints[Mathf.Abs(spec.PaletteIndex) % tints.Length], spec.Height);
            var holder = actor.transform.root;
            holder.SetParent(root, true);

            holder.gameObject.AddComponent<SpeakingActor>().Configure(
                spec.Id,
                StreetFigureName(spec.Id),
                "faction.crown",
                "scene.capital_region",
                "ratnapur", "jiva stones", "the raja", "the stambha");
        }
    }

    /// <summary>
    /// Things on the street you can actually take.
    ///
    /// A jiva stone on the spice stall is doing real work in a demo: it is the object the whole
    /// setting runs on, it goes into a working inventory, and it is the reason the shared
    /// dialogue topic beside it is worth asking about. Until now the world contained no item a
    /// player could pick up by hand.
    /// </summary>
    private static void BuildStreetPickups(Transform root, Material stone, Material road, Material water)
    {
        Pickup(root, "pickup.jiva_stone", new Vector3(-5.0f, 1.06f, -47f),
            new Vector3(0.34f, 0.5f, 0.34f), water,
            SoulCrystals.LesserId, SoulCrystals.LesserName, "crystal");

        Pickup(root, "pickup.ledger", new Vector3(5.0f, 1.02f, 8f),
            new Vector3(0.5f, 0.14f, 0.66f), road,
            "tower_ledger", "Harbour Ledger", "quest");

        Pickup(root, "pickup.lamp", new Vector3(-5.2f, 0.9f, 5f),
            new Vector3(0.3f, 0.42f, 0.3f), stone,
            "brass_lamp", "Brass Lamp", "loot");
    }

    private static void Pickup(Transform root, string name, Vector3 localPosition, Vector3 size,
        Material material, string itemId, string label, string category)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;

        // Interact raycasts against colliders, so the box stays — WorldPickup destroys the
        // whole object once taken.
        go.GetComponent<BoxCollider>().isTrigger = false;
        go.AddComponent<WorldPickup>().Configure(itemId, label, 1, category);
        WorldTagger.SetLayerRecursive(go, GameLayers.Structure);
    }

    /// <summary>A readable name for the nameplate. Trades, not characters — the named cast
    /// belong to the story and are placed by it.</summary>
    private static string StreetFigureName(string id) => id switch
    {
        "figure.spice_vendor" => "Spice Seller",
        "figure.dock_runner" => "Dock Runner",
        "figure.cloth_vendor" => "Cloth Seller",
        "figure.water_bearer" => "Water Bearer",
        "figure.palace_clerk" => "Palace Clerk",
        "figure.watchman" => "City Watchman",
        _ => "Citizen"
    };

    public static void BuildPrisonDungeon(Transform parent)
    {
        var root = new GameObject(ArenaMiniatureSliceLayout.DungeonRootName).transform;
        root.SetParent(parent, false);

        Material stone = Mat(ProceduralSurface.Kind.Stone);
        Material road = Mat(ProceduralSurface.Kind.Roof);
        Material timber = Mat(ProceduralSurface.Kind.Timber);
        Material plaster = Mat(ProceduralSurface.Kind.Plaster);

        foreach (var module in ArenaMiniatureSliceLayout.DungeonModules)
        {
            Vector3 position = ArenaMiniatureSliceLayout.DungeonPosition(module);
            var holder = new GameObject(module.Id).transform;
            holder.SetParent(root, false);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0f, module.FacingDegrees, 0f);

            switch (module.Kind)
            {
                case ArenaMiniatureSliceLayout.DungeonModuleKind.FloorRegister:
                    Block(holder, "Runner", new Vector3(0f, 0.02f, 0f),
                        new Vector3(6f, 0.04f, module.RoomIndex == 0 ? 19f : 22f),
                        road, false, GameLayers.Prop);
                    for (int stripe = -1; stripe <= 1; stripe += 2)
                        Block(holder, "RunnerEdge_" + stripe, new Vector3(stripe * 3.05f, 0.03f, 0f),
                            new Vector3(0.18f, 0.05f, module.RoomIndex == 0 ? 19f : 22f),
                            timber, false, GameLayers.Prop);
                    break;

                case ArenaMiniatureSliceLayout.DungeonModuleKind.WallPanel:
                    BuildWallPanel(holder, plaster, road);
                    break;

                case ArenaMiniatureSliceLayout.DungeonModuleKind.CellFront:
                    BuildCellFront(holder, timber, stone);
                    break;

                case ArenaMiniatureSliceLayout.DungeonModuleKind.CeilingBeam:
                    Block(holder, "CrossBeam", new Vector3(0f, 6.6f, 0f),
                        new Vector3(24.4f, 0.35f, 0.45f), timber, false, GameLayers.Prop);
                    Block(holder, "BeamBoss", new Vector3(0f, 6.25f, 0f),
                        new Vector3(1.2f, 0.7f, 0.8f), road, false, GameLayers.Prop);
                    break;

                case ArenaMiniatureSliceLayout.DungeonModuleKind.EndLandmark:
                    BuildEndLandmark(holder, plaster, road, timber);
                    break;
            }
        }
    }

    private static void BuildFacade(Transform parent, ArenaMiniatureSliceLayout.FacadeSpec spec,
        Material plaster, Material stone, Material roof, Material timber)
    {
        var holder = new GameObject(spec.Id).transform;
        holder.SetParent(parent, false);
        holder.localPosition = spec.LocalPosition;
        holder.localRotation = Quaternion.Euler(0f, spec.FacingDegrees, 0f);

        float width = spec.Size.x;
        float height = spec.Size.y;
        float depth = spec.Size.z;
        float front = depth * 0.5f;

        // One simple solid body is the building collider. Every decorative projection is either
        // collider-free or deliberately walkable, avoiding the compound-collider snags that the
        // older downloaded building prefabs suffered from.
        Block(holder, "Body", new Vector3(0f, height * 0.5f, 0f), spec.Size,
            plaster, true, GameLayers.Structure);
        Block(holder, "StonePlinth", new Vector3(0f, 0.35f, front + 0.10f),
            new Vector3(width, 0.7f, 0.2f), stone, false, GameLayers.Prop);
        Block(holder, "RoofBand", new Vector3(0f, height + 0.35f, 0f),
            new Vector3(width + 0.8f, 0.7f, depth + 0.8f), roof, false, GameLayers.Prop);

        BuildDoorAndWindows(holder, width, height, front, timber, stone);
        BuildFacadeDoorway(holder, spec.Id, front);

        if ((spec.Features & ArenaMiniatureSliceLayout.FacadeFeature.Arcade) != 0)
            BuildArcade(holder, width, front, stone);
        if ((spec.Features & ArenaMiniatureSliceLayout.FacadeFeature.Awning) != 0)
            BuildAwning(holder, width, front, roof, timber);
        if ((spec.Features & ArenaMiniatureSliceLayout.FacadeFeature.Balcony) != 0)
            BuildBalcony(holder, width, height, front, stone, timber);
        if ((spec.Features & ArenaMiniatureSliceLayout.FacadeFeature.Pavilion) != 0)
            BuildRoofPavilion(holder, width, height, roof, stone);
    }

    /// <summary>
    /// Makes two of the street's drawn doors actually open.
    ///
    /// The street had eight facades with painted-on doors and no way through any of them, and
    /// the nearest real entrance was 470 m away across generated blocks. So the one dense,
    /// authored space in the game was somewhere you could look at but not enter — which is most
    /// of what "there is nothing to demo" meant.
    /// </summary>
    private static void BuildFacadeDoorway(Transform holder, string facadeId, float front)
    {
        string scene = facadeId switch
        {
            "facade.east.north" => "Order_Hall",
            "facade.west.market" => "Harbor",
            _ => null
        };
        if (scene == null) return;

        string label = scene == "Order_Hall" ? "Hall of the Siddha Order" : "Merchant Harbour";

        var portal = new GameObject("Portal");
        portal.transform.SetParent(holder, false);
        portal.transform.localPosition = new Vector3(0f, 1.6f, front + 1.4f);
        var trigger = portal.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(4.5f, 3.2f, 2.8f);
        portal.AddComponent<RegionPortal>()
            .Configure($"anchor.street.{scene.ToLowerInvariant()}", label, scene, "spawn.entry");
    }

    private static void BuildDoorAndWindows(Transform holder, float width, float height,
        float front, Material timber, Material stone)
    {
        Block(holder, "Door", new Vector3(0f, 2.15f, front + 0.06f),
            new Vector3(3.1f, 4.3f, 0.12f), timber, false, GameLayers.Prop);
        Block(holder, "DoorLintel", new Vector3(0f, 4.5f, front + 0.10f),
            new Vector3(4.1f, 0.4f, 0.2f), stone, false, GameLayers.Prop);

        int windowCount = width >= 36f ? 4 : 3;
        float span = width * 0.72f;
        for (int i = 0; i < windowCount; i++)
        {
            float x = windowCount == 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (float)(windowCount - 1));
            if (Mathf.Abs(x) < 2.8f) x += x <= 0f ? -2.8f : 2.8f;
            float y = Mathf.Min(height - 2.2f, 6.4f + (i % 2) * 0.45f);
            Block(holder, $"Window_{i}", new Vector3(x, y, front + 0.065f),
                new Vector3(1.7f, 2.4f, 0.13f), timber, false, GameLayers.Prop);
            Block(holder, $"WindowSill_{i}", new Vector3(x, y - 1.35f, front + 0.10f),
                new Vector3(2.3f, 0.28f, 0.20f), stone, false, GameLayers.Prop);
        }
    }

    private static void BuildArcade(Transform holder, float width, float front, Material stone)
    {
        int bays = 3;
        float span = Mathf.Min(width - 5f, 27f);
        for (int i = 0; i <= bays; i++)
        {
            float x = Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (float)bays);
            Block(holder, $"ArcadePier_{i}", new Vector3(x, 2.6f, front + 0.6f),
                new Vector3(0.65f, 5.2f, 0.65f), stone, false, GameLayers.Prop);
        }
        Block(holder, "ArcadeLintel", new Vector3(0f, 5.15f, front + 0.6f),
            new Vector3(span + 0.7f, 0.65f, 0.65f), stone, false, GameLayers.Prop);
    }

    private static void BuildAwning(Transform holder, float width, float front,
        Material roof, Material timber)
    {
        Block(holder, "Awning", new Vector3(0f, 4.9f, front + 2.0f),
            new Vector3(Mathf.Min(width - 5f, 25f), 0.25f, 4f), roof,
            false, GameLayers.Prop, Quaternion.Euler(7f, 0f, 0f));
        foreach (float side in new[] { -1f, 1f })
            Block(holder, side < 0 ? "AwningPost_L" : "AwningPost_R",
                new Vector3(side * Mathf.Min(width * 0.33f, 9f), 2.4f, front + 3.5f),
                new Vector3(0.24f, 4.8f, 0.24f), timber, false, GameLayers.Prop);
    }

    private static void BuildBalcony(Transform holder, float width, float height, float front,
        Material stone, Material timber)
    {
        float y = Mathf.Min(height - 3.2f, 8.8f);
        float balconyWidth = Mathf.Min(width - 7f, 21f);
        Block(holder, "BalconyFloor", new Vector3(0f, y, front + 1.4f),
            new Vector3(balconyWidth, 0.35f, 2.8f), stone, false, GameLayers.Prop);
        Block(holder, "BalconyRail", new Vector3(0f, y + 1.15f, front + 2.7f),
            new Vector3(balconyWidth, 1.2f, 0.22f), timber, false, GameLayers.Prop);
        foreach (float side in new[] { -1f, 1f })
            Block(holder, side < 0 ? "BalconyBracket_L" : "BalconyBracket_R",
                new Vector3(side * balconyWidth * 0.38f, y - 0.85f, front + 0.8f),
                new Vector3(0.35f, 1.7f, 0.35f), timber, false, GameLayers.Prop);
    }

    private static void BuildRoofPavilion(Transform holder, float width, float height,
        Material roof, Material stone)
    {
        float pavilionWidth = Mathf.Min(8f, width * 0.24f);
        var pavilion = new GameObject("RoofPavilion").transform;
        pavilion.SetParent(holder, false);
        pavilion.localPosition = new Vector3(width * 0.24f, height + 0.7f, 0f);
        foreach (float x in new[] { -1f, 1f })
        foreach (float z in new[] { -1f, 1f })
            Block(pavilion, $"Post_{x}_{z}", new Vector3(x * pavilionWidth * 0.35f, 1.4f, z * 2f),
                new Vector3(0.35f, 2.8f, 0.35f), stone, false, GameLayers.Prop);
        Block(pavilion, "Canopy", new Vector3(0f, 3.0f, 0f),
            new Vector3(pavilionWidth, 0.55f, 5.4f), roof, false, GameLayers.Prop);
        Block(pavilion, "Finial", new Vector3(0f, 3.75f, 0f),
            new Vector3(0.55f, 1.0f, 0.55f), roof, false, GameLayers.Prop,
            Quaternion.Euler(0f, 45f, 0f));
    }

    private static void BuildStreetProp(Transform parent,
        ArenaMiniatureSliceLayout.StreetPropSpec spec, Material stone, Material roof,
        Material timber, Material foliage, Material water)
    {
        var holder = new GameObject(spec.Id).transform;
        holder.SetParent(parent, false);
        holder.localPosition = spec.LocalPosition;
        holder.localRotation = Quaternion.Euler(0f, spec.FacingDegrees, 0f);

        switch (spec.Kind)
        {
            case ArenaMiniatureSliceLayout.StreetPropKind.MarketStall:
                Block(holder, "Counter", new Vector3(0f, 0.75f, 0f),
                    new Vector3(4.2f, 1.5f, 2.4f), timber, true, GameLayers.Structure);
                Block(holder, "ClothCanopy", new Vector3(0f, 3.15f, 0f),
                    new Vector3(5.2f, 0.24f, 3.5f), roof, false, GameLayers.Prop);
                foreach (float x in new[] { -1f, 1f })
                    Block(holder, x < 0 ? "Post_L" : "Post_R", new Vector3(x * 2.1f, 1.7f, 0f),
                        new Vector3(0.22f, 3.4f, 0.22f), timber, false, GameLayers.Prop);
                break;

            case ArenaMiniatureSliceLayout.StreetPropKind.Banner:
                Block(holder, "Post", new Vector3(0f, 2.8f, 0f),
                    new Vector3(0.28f, 5.6f, 0.28f), timber, false, GameLayers.Prop);
                Block(holder, "PaintedBanner", new Vector3(0.9f, 4.2f, 0f),
                    new Vector3(1.8f, 2.3f, 0.12f), roof, false, GameLayers.Prop);
                break;

            case ArenaMiniatureSliceLayout.StreetPropKind.ShadeTree:
                Block(holder, "Trunk", new Vector3(0f, 2.4f, 0f),
                    new Vector3(0.7f, 4.8f, 0.7f), timber, true, GameLayers.Structure);
                Block(holder, "Crown_Low", new Vector3(0f, 5.3f, 0f),
                    new Vector3(5.8f, 2.1f, 4.8f), foliage, false, GameLayers.Prop,
                    Quaternion.Euler(0f, 25f, 0f));
                Block(holder, "Crown_High", new Vector3(0.8f, 6.6f, -0.4f),
                    new Vector3(4.1f, 2.0f, 3.7f), foliage, false, GameLayers.Prop,
                    Quaternion.Euler(0f, -18f, 0f));
                break;

            case ArenaMiniatureSliceLayout.StreetPropKind.WaterBasin:
                Block(holder, "BasinBase", new Vector3(0f, 0.45f, 0f),
                    new Vector3(4.4f, 0.9f, 3.2f), stone, true, GameLayers.Structure);
                Block(holder, "Water", new Vector3(0f, 0.92f, 0f),
                    new Vector3(3.5f, 0.05f, 2.3f), water, false, GameLayers.Prop);
                break;

            case ArenaMiniatureSliceLayout.StreetPropKind.CivicArch:
                foreach (float side in new[] { -1f, 1f })
                {
                    Block(holder, side < 0 ? "Pier_West" : "Pier_East",
                        new Vector3(side * 11.5f, 4.3f, 0f),
                        new Vector3(2.2f, 8.6f, 2.2f), stone, true, GameLayers.Structure);
                    Block(holder, side < 0 ? "Roof_West" : "Roof_East",
                        new Vector3(side * 11.5f, 9.0f, 0f),
                        new Vector3(4.1f, 0.8f, 4.1f), roof, false, GameLayers.Prop);
                }
                Block(holder, "Lintel", new Vector3(0f, 8.2f, 0f),
                    new Vector3(21f, 1.1f, 1.5f), stone, false, GameLayers.Prop);
                Block(holder, "PaintedRegister", new Vector3(0f, 8.2f, -0.8f),
                    new Vector3(7f, 0.7f, 0.15f), roof, false, GameLayers.Prop);
                break;
        }
    }

    private static void BuildWallPanel(Transform holder, Material plaster, Material roof)
    {
        Block(holder, "Panel", new Vector3(0f, 3.2f, 0f),
            new Vector3(5.5f, 4.7f, 0.12f), plaster, false, GameLayers.Prop);
        Block(holder, "PanelInset", new Vector3(0f, 3.2f, -0.075f),
            new Vector3(3.8f, 3.2f, 0.08f), roof, false, GameLayers.Prop);
    }

    private static void BuildCellFront(Transform holder, Material timber, Material stone)
    {
        Block(holder, "CellLintel", new Vector3(0f, 5.0f, 0f),
            new Vector3(7f, 0.5f, 0.5f), stone, false, GameLayers.Prop);
        Block(holder, "CellSill", new Vector3(0f, 0.3f, 0f),
            new Vector3(7f, 0.5f, 0.5f), stone, false, GameLayers.Prop);
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-2.8f, 2.8f, i / 5f);
            Block(holder, $"Bar_{i}", new Vector3(x, 2.65f, 0f),
                new Vector3(0.18f, 4.5f, 0.18f), timber, false, GameLayers.Prop);
        }
    }

    private static void BuildEndLandmark(Transform holder, Material plaster,
        Material roof, Material timber)
    {
        Block(holder, "EvidencePanel", new Vector3(0f, 3.4f, 0f),
            new Vector3(10f, 5.8f, 0.18f), plaster, false, GameLayers.Prop);
        for (int i = -1; i <= 1; i++)
            Block(holder, "PaintedSeal_" + i, new Vector3(i * 2.5f, 3.5f, -0.12f),
                new Vector3(1.4f, 2.8f, 0.10f), i == 0 ? roof : timber,
                false, GameLayers.Prop);
    }

    private static Material Mat(ProceduralSurface.Kind kind) =>
        ProceduralSurfaceBaker.MaterialFor(kind);

    private static GameObject Block(Transform parent, string name, Vector3 localPosition,
        Vector3 localScale, Material material, bool solid, int layer,
        Quaternion? localRotation = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = localScale;
        go.layer = layer;
        go.isStatic = true;

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        ProceduralSurface.ApplyTiling(renderer, localScale);

        if (!solid)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // --- headless-friendly proof captures ---------------------------------

    public static void Capture()
    {
        string output = Path.GetFullPath("Docs/Screenshots");
        Directory.CreateDirectory(output);

        CaptureScene(CapitalRegionBuilder.ScenePath, "arena-miniature-ratnapur-street.png",
            new Vector3(140f, CapitalRegion.GroundHeight + 3.4f, -704f),
            new Vector3(140f, CapitalRegion.GroundHeight + 5.2f, -548f), 58f, 420f);
        CaptureScene("Assets/Scenes/Chapter01/Prison.unity", "arena-miniature-prison.png",
            new Vector3(0f, 2.7f, 14f), new Vector3(0f, 2.7f, 70f), 68f, 120f);

        AssetDatabase.Refresh();
        Debug.Log("[ArenaMiniature] Captured street and dungeon proof images in Docs/Screenshots.");
    }

    private static void CaptureScene(string scenePath, string fileName,
        Vector3 cameraPosition, Vector3 target, float fieldOfView, float farClip)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var cameraGo = new GameObject("ArenaMiniature_ScreenshotCamera");
        cameraGo.transform.position = cameraPosition;
        cameraGo.transform.LookAt(target);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.Lerp(ArtDirection.Active.Palette.Sand,
            ArtDirection.Active.Palette.Ocean, 0.18f);
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = farClip;
        camera.allowHDR = false;

        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        camera.targetTexture = texture;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine("Docs/Screenshots", fileName), image.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        texture.Release();
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(cameraGo);
    }
}
