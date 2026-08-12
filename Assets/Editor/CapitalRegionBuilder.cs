using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the Estmere region scene from <see cref="CapitalRegion"/>.
///
/// The look is Arena's: flat-topped blocks, gridded streets, a rectangular curtain wall with
/// cardinal gates. It is procedural because hand-authoring a 2.4 km region is not available to
/// this project — the same reason Arena and Daggerfall were procedural.
///
/// **Deterministic.** Everything is driven from a fixed seed, so regenerating produces the
/// identical city. A city that reshuffles on every rebuild cannot be playtested, because no
/// two sessions would be discussing the same place.
///
/// Like `Main`, this scene is a build artifact. Do not hand-author into it.
/// </summary>
public static class CapitalRegionBuilder
{
    public const string ScenePath = "Assets/Scenes/Capital_Region.unity";
    private const int Seed = 20260804;

    private const float BlockSize = 60f;
    private const float StreetWidth = 14f;

    [MenuItem("Kessil/Architecture/Build Estmere Region")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("CapitalRegion").transform;
        var random = new System.Random(Seed);

        BuildGround(root);
        BuildSea(root);
        BuildCityWall(root);
        BuildDistricts(root, random);
        BuildAnchors(root);
        BuildCrowd(root, random);
        BuildSpawn(root);
        BuildLighting(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterInBuildSettings();
        Debug.Log($"[CapitalRegion] Built {CapitalRegion.RegionSize} m region at {ScenePath}.");
    }

    // --- terrain -------------------------------------------------------------

    private static void BuildGround(Transform root)
    {
        var ground = Block(root, "Ground",
            new Vector3(0f, CapitalRegion.GroundHeight - 1f, 0f),
            new Vector3(CapitalRegion.LandHalfExtent * 2f, 2f, CapitalRegion.LandHalfExtent * 2f),
            Palette(0));
        WorldTagger.SetLayerRecursive(ground, GameLayers.Ground);
    }

    /// <summary>
    /// The sea is the region's bound. It reads as endless under fog, and the player turns back
    /// from it rather than hitting an invisible wall in a field.
    /// </summary>
    private static void BuildSea(Transform root)
    {
        var sea = Block(root, "Sea",
            new Vector3(0f, CapitalRegion.WaterLevel - 0.5f, 0f),
            new Vector3(CapitalRegion.RegionSize * 3f, 1f, CapitalRegion.RegionSize * 3f),
            Palette(4));
        WorldTagger.SetLayerRecursive(sea, GameLayers.Ground);
    }

    // --- city ----------------------------------------------------------------

    private static void BuildCityWall(Transform root)
    {
        var walls = new GameObject("CityWall").transform;
        walls.SetParent(root, false);

        float half = CapitalRegion.CityHalf;
        float h = CapitalRegion.WallHeight;
        float t = CapitalRegion.WallThickness;
        var c = CapitalRegion.CityCenter;

        // Each side is split either side of its cardinal gate, so the opening is real geometry
        // rather than a gap the player discovers is solid.
        float segment = half - CapitalRegion.GateWidth * 0.5f;
        float offset = half - segment * 0.5f;

        foreach (int sign in new[] { -1, 1 })
        {
            WallSegment(walls, $"Wall_N_{sign}", new Vector3(c.x + sign * offset, c.y + h * 0.5f, c.z + half),
                new Vector3(segment, h, t));
            WallSegment(walls, $"Wall_S_{sign}", new Vector3(c.x + sign * offset, c.y + h * 0.5f, c.z - half),
                new Vector3(segment, h, t));
            WallSegment(walls, $"Wall_E_{sign}", new Vector3(c.x + half, c.y + h * 0.5f, c.z + sign * offset),
                new Vector3(t, h, segment));
            WallSegment(walls, $"Wall_W_{sign}", new Vector3(c.x - half, c.y + h * 0.5f, c.z + sign * offset),
                new Vector3(t, h, segment));
        }
    }

    private static void WallSegment(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var go = Block(parent, name, position, scale, Palette(1));
        WorldTagger.SetLayerRecursive(go, GameLayers.Structure);
    }

    /// <summary>
    /// City blocks on a grid, with streets between them. Heights vary by a seeded roll, which
    /// is what gives the skyline an Arena silhouette without any of it being authored.
    /// </summary>
    private static void BuildDistricts(Transform root, System.Random random)
    {
        var district = new GameObject("Districts").transform;
        district.SetParent(root, false);

        float half = CapitalRegion.CityHalf;
        float stride = BlockSize + StreetWidth;
        int count = Mathf.FloorToInt(half * 2f / stride);
        var reserved = ReservedFootprints();

        for (int ix = 0; ix < count; ix++)
        {
            for (int iz = 0; iz < count; iz++)
            {
                var centre = new Vector3(
                    CapitalRegion.CityCenter.x - half + stride * (ix + 0.5f),
                    CapitalRegion.GroundHeight,
                    CapitalRegion.CityCenter.z - half + stride * (iz + 0.5f));

                if (IsReserved(centre, reserved)) continue;
                if (IsOnGateApproach(centre)) continue;

                float height = 8f + (float)random.NextDouble() * 14f;
                var go = Block(district, $"Block_{ix}_{iz}",
                    centre + Vector3.up * (height * 0.5f),
                    new Vector3(BlockSize, height, BlockSize),
                    Palette(2 + (ix + iz) % 2));
                WorldTagger.SetLayerRecursive(go, GameLayers.Structure);
            }
        }
    }

    /// <summary>Keep the ground clear where story anchors and the player spawn will land.</summary>
    private static List<(Vector3 pos, float radius)> ReservedFootprints()
    {
        var reserved = new List<(Vector3, float)>();
        foreach (var anchor in CapitalRegion.Anchors)
            reserved.Add((anchor.Position, anchor.Footprint * 1.4f));
        reserved.Add((CapitalRegion.PlayerSpawn, 40f));
        return reserved;
    }

    private static bool IsReserved(Vector3 centre, List<(Vector3 pos, float radius)> reserved)
    {
        foreach (var (pos, radius) in reserved)
        {
            var flat = new Vector3(centre.x - pos.x, 0f, centre.z - pos.z);
            if (flat.magnitude < radius + BlockSize * 0.5f) return true;
        }
        return false;
    }

    /// <summary>A gate the player cannot walk through is worse than no gate at all.</summary>
    private static bool IsOnGateApproach(Vector3 centre)
    {
        foreach (var gate in CapitalRegion.Gates)
        {
            var world = CapitalRegion.CityCenter + new Vector3(gate.x, 0f, gate.z);
            bool alongX = Mathf.Abs(world.x) > Mathf.Abs(world.z);
            float lateral = alongX ? Mathf.Abs(centre.z - world.z) : Mathf.Abs(centre.x - world.x);
            if (lateral < CapitalRegion.GateWidth) return true;
        }
        return false;
    }

    // --- anchors -------------------------------------------------------------

    private static void BuildAnchors(Transform root)
    {
        var anchors = new GameObject("Anchors").transform;
        anchors.SetParent(root, false);

        foreach (var anchor in CapitalRegion.Anchors)
        {
            var holder = new GameObject(anchor.Id).transform;
            holder.SetParent(anchors, false);
            holder.position = anchor.Position;
            holder.rotation = Quaternion.Euler(0f, anchor.FacingDegrees, 0f);

            float size = anchor.Footprint;
            var shell = Block(holder, "Shell",
                Vector3.up * 9f, new Vector3(size, 18f, size), Palette(1));
            WorldTagger.SetLayerRecursive(shell, GameLayers.Structure);

            // The doorway is a trigger in front of the shell, not a hole in it — the interior
            // is a separate scene, so nothing needs to be modelled through the wall.
            var portal = new GameObject("Portal");
            portal.transform.SetParent(holder, false);
            portal.transform.localPosition = new Vector3(0f, 1.5f, size * 0.5f + 1.5f);
            var trigger = portal.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(6f, 3f, 3f);
            var link = portal.AddComponent<RegionPortal>();
            link.Configure(anchor.Id, anchor.DisplayName, anchor.SceneName, anchor.SpawnId);
        }
    }

    /// <summary>
    /// Street population. Arena-style billboards, seeded so the crowd stands in the same
    /// places every rebuild, and kept off the anchor footprints and the gate approaches.
    ///
    /// These are dressing, not actors — they make the city read as inhabited. Speaking roles
    /// are placed by the story, not here.
    /// </summary>
    private static void BuildCrowd(Transform root, System.Random random)
    {
        var crowd = new GameObject("Crowd").transform;
        crowd.SetParent(root, false);

        var palette = ArtDirection.Active.Palette;
        var tints = new[] { palette.CityStone, palette.Road, palette.Sand, palette.Mountain };
        var reserved = ReservedFootprints();
        float half = CapitalRegion.CityHalf;

        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < 90; attempt++)
        {
            var spot = new Vector3(
                CapitalRegion.CityCenter.x + ((float)random.NextDouble() * 2f - 1f) * half,
                CapitalRegion.GroundHeight,
                CapitalRegion.CityCenter.z + ((float)random.NextDouble() * 2f - 1f) * half);

            if (IsReserved(spot, reserved)) continue;

            var tint = tints[random.Next(tints.Length)];
            var actor = BillboardActor.Spawn($"Citizen_{placed}", spot, tint, 1.75f + (float)random.NextDouble() * 0.2f);
            actor.transform.root.SetParent(crowd, true);
            placed++;
        }
    }

    private static void BuildSpawn(Transform root)
    {
        // SceneTransitionService refuses to enter a scene with no SceneContext, so the region
        // needs one exactly like the generated interiors do — this is what makes it a place
        // the transition system can hand the player to rather than just a loaded scene.
        var context = new GameObject("SceneContext");
        context.transform.SetParent(root, false);
        context.AddComponent<SceneContext>().Configure("Capital_Region", "spawn.region");

        var spawn = new GameObject("spawn.region");
        spawn.transform.SetParent(context.transform, false);
        spawn.transform.position = CapitalRegion.PlayerSpawn;
        spawn.AddComponent<SceneSpawnPoint>().Configure("spawn.region");
    }

    private static void BuildLighting(Transform root)
    {
        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(root, false);
        sunGo.transform.rotation = Quaternion.Euler(42f, 150f, 0f);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.05f;
        sun.shadows = LightShadows.Soft;
    }

    // --- helpers -------------------------------------------------------------

    private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        ProceduralSurface.ApplyTiling(renderer, scale);
        return go;
    }

    /// <summary>
    /// Palette-locked surfaces, drawn by <see cref="ProceduralSurface"/> and baked to assets by
    /// <see cref="ProceduralSurfaceBaker"/>.
    ///
    /// These used to be flat untinted colours, which is what made the region read as coloured
    /// greybox rather than as a place: a 60 m building painted one uniform value has no texel
    /// grain and no drawn edges, so there is nothing for the eye to find. The textures carry
    /// both, and they are still palette-locked because every pixel in them is derived from
    /// `ArtDirection.Palette` rather than authored.
    /// </summary>
    private static Material Palette(int index) => ProceduralSurfaceBaker.MaterialFor((index % 5) switch
    {
        0 => ProceduralSurface.Kind.Ground,    // open land
        1 => ProceduralSurface.Kind.Stone,     // curtain wall and anchor shells
        2 => ProceduralSurface.Kind.Plaster,   // alternating city blocks
        3 => ProceduralSurface.Kind.Roof,
        _ => ProceduralSurface.Kind.Water
    });


    private static void RegisterInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
