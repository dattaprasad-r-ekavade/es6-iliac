using System;
using System.Collections.Generic;
using UnityEngine;

// The generator used to declare its own LandPatch struct and Biome enum, duplicating
// coordinates that the map art, fast travel and NPC spawners each re-declared as well.
using LandPatch = WorldLayout.Landmass;
using Biome = WorldLayout.Biome;

/// <summary>
/// Walkable homage map of the Iliac Bay region (High Rock + Hammerfell).
/// Layout inspired by public Elder Scrolls lore geography (UESP / Daggerfall maps):
/// High Rock north (temperate), Hammerfell south (arid), Iliac Bay between them,
/// islands Betony / Balfiera / Cybiades, cities Daggerfall / Wayrest / Sentinel.
/// Original mesh layout — not a copy of Bethesda art.
/// </summary>
public class IliacBayWorldGenerator : MonoBehaviour
{
    [Header("World")]
    [SerializeField] private int propSeed = 4242;
    [SerializeField] private float waterSize = 8000f;
    [SerializeField] private Material oceanMaterial;
    [SerializeField] private Material highRockMaterial;
    [SerializeField] private Material hammerfellMaterial;
    [SerializeField] private Material sandMaterial;
    [SerializeField] private Material cityMaterial;
    [SerializeField] private Material mountainMaterial;

    [Header("Props (Kenney)")]
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] desertPrefabs;
    [SerializeField] private GameObject[] rockPrefabs;
    [SerializeField] private GameObject[] buildingPrefabs;
    [SerializeField] private GameObject[] towerPrefabs;
    [SerializeField] private GameObject[] dockPrefabs;
    [SerializeField] private GameObject[] propPrefabs;
    [SerializeField] private GameObject[] campPrefabs;
    [SerializeField] private GameObject[] ruinPrefabs;
    [SerializeField] private Material roadMaterial;

    [Header("Player")]
    [SerializeField] private bool spawnPlayer = true;

    private System.Random _rng;
    private Transform _root;
    private readonly List<LandPatch> _patches = new();
    private Vector3 _playerSpawn = new(-160f, 12f, 95f);

    // Landmass shape/biome definitions live in WorldLayout (see the using aliases at
    // the top of this file), shared with the map art, fast travel and the spawners.

    [ContextMenu("Generate Iliac Bay")]
    public void GenerateWorld()
    {
        ClearGenerated();
        _rng = new System.Random(propSeed);
        _root = CreateChild(transform, "Generated").transform;
        _patches.Clear();

        CreateOcean();
        CreateFallCatcher();
        DefineLandmasses();
        foreach (var patch in _patches)
        {
            BuildLandmass(patch);
        }

        BuildLandmarkTower_Balfiera();

        // Layers must be in place before anything raycasts for ground.
        WorldTagger.TagHierarchy(_root.gameObject);
        Physics.SyncTransforms();

        ScatterAllProps();
        BuildDaggerfallSpawnPad();
        Physics.SyncTransforms();
        BuildRoadsAndPois();

        if (spawnPlayer)
        {
            SpawnPlayerAt(_playerSpawn);
        }

        Debug.Log("[IliacBay] Map built: High Rock (N), Hammerfell (S), Iliac Bay + Betony/Balfiera/Cybiades. Spawn at Daggerfall.");
    }

    private void Start()
    {
        if (transform.Find("Generated") == null)
        {
            GenerateWorld();
        }
    }

    public void ClearGenerated()
    {
        var existing = transform.Find("Generated");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    private void DefineLandmasses()
    {
        // Large homage scale: cities are kilometres apart (walk, don't hop).
        // +Z = north, +X = east. Definitions are shared via WorldLayout.
        _patches.AddRange(WorldLayout.Landmasses);

        // Overridden by the spawn pad after the city is built.
        _playerSpawn = WorldLayout.DaggerfallSpawnPad + Vector3.up * 1.8f;
    }

    private void CreateOcean()
    {
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "IliacBay_Ocean";
        ocean.transform.SetParent(_root, false);
        ocean.transform.position = new Vector3(0f, 2f, 0f);
        ocean.transform.localScale = Vector3.one * (waterSize / 10f);
        var oceanMat = oceanMaterial != null ? new Material(oceanMaterial) : WorldVisualFix.CreateWaterMaterial();
        if (oceanMaterial != null)
        {
            oceanMat.SetColor("_BaseColor", new Color(0.12f, 0.48f, 0.72f, 0.82f));
            if (oceanMat.HasProperty("_Surface"))
            {
                oceanMat.SetFloat("_Surface", 1f);
                oceanMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                oceanMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                oceanMat.SetInt("_ZWrite", 0);
                oceanMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                oceanMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
        var r = ocean.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = oceanMat;
        ocean.layer = GameLayers.Water;
        DestroyColliderSafe(ocean.GetComponent<Collider>());
    }

    private void CreateFallCatcher()
    {
        // Invisible safety floor under the whole map.
        var catcher = GameObject.CreatePrimitive(PrimitiveType.Cube);
        catcher.name = "FallCatcher";
        catcher.transform.SetParent(_root, false);
        catcher.transform.position = new Vector3(0f, WorldLayout.VoidCatcherY, 0f);
        catcher.transform.localScale = new Vector3(waterSize * 1.2f, 2f, waterSize * 1.2f);
        catcher.layer = GameLayers.Void;
        var r = catcher.GetComponent<Renderer>();
        if (r != null) r.enabled = false;
    }

    /// <summary>
    /// Unity Cylinder primitives use CapsuleColliders. Non-uniform XZ scale turns those into
    /// giant rounded blobs — players spawn on the curve and slide off the world.
    /// Visual mesh stays; collision is an unscaled BoxCollider child (size in metres).
    /// </summary>
    private static void ReplaceWithBoxCollider(GameObject visual, Vector3 localCenter, Vector3 worldSize)
    {
        DestroyColliderSafe(visual.GetComponent<Collider>());
        var colGo = new GameObject($"{visual.name}_Collider");
        colGo.transform.SetParent(visual.transform.parent, false);
        colGo.transform.localPosition = localCenter;
        colGo.transform.localRotation = Quaternion.identity;
        colGo.transform.localScale = Vector3.one;
        var solid = colGo.AddComponent<BoxCollider>();
        solid.size = worldSize;
        solid.center = Vector3.zero;
    }

    private void BuildLandmass(LandPatch patch)
    {
        var go = CreateChild(_root, patch.Name);
        go.transform.position = patch.Center;

        var mat = patch.Biome switch
        {
            Biome.Hammerfell => hammerfellMaterial,
            Biome.IslandRock => mountainMaterial,
            _ => highRockMaterial
        };
        var color = patch.Biome switch
        {
            Biome.Hammerfell => new Color(0.72f, 0.55f, 0.32f),
            Biome.IslandRock => new Color(0.45f, 0.45f, 0.48f),
            Biome.IslandGreen => new Color(0.3f, 0.5f, 0.28f),
            _ => new Color(0.27f, 0.48f, 0.24f)
        };

        // Rounded landmasses — cylinders are distant silhouette only; walkable hills use TerrainSurface mesh.
        float height = Mathf.Max(4f, patch.Size.y);

        var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        skirt.name = "Beach";
        skirt.transform.SetParent(go.transform, false);
        skirt.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        skirt.transform.localScale = new Vector3(patch.Size.x * 1.12f, 0.7f, patch.Size.z * 1.12f);
        ApplyMat(skirt, sandMaterial, new Color(0.82f, 0.72f, 0.5f));
        DestroyColliderSafe(skirt.GetComponent<Collider>());
        var skirtR = skirt.GetComponent<Renderer>();
        if (skirtR != null) skirtR.enabled = false;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Land";
        ground.transform.SetParent(go.transform, false);
        ground.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        ground.transform.localScale = new Vector3(patch.Size.x, height * 0.5f, patch.Size.z);
        ApplyMat(ground, mat, color);
        DestroyColliderSafe(ground.GetComponent<Collider>());
        var groundR = ground.GetComponent<Renderer>();
        if (groundR != null) groundR.enabled = false;

        BuildTerrainSurface(go.transform, patch, mat, color);

        // Soft rim plateau — visual only (no collider) so it can't strand the player.
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Rim";
        rim.transform.SetParent(go.transform, false);
        rim.transform.localPosition = new Vector3(0f, height + 0.35f, 0f);
        rim.transform.localScale = new Vector3(patch.Size.x * 0.92f, 0.4f, patch.Size.z * 0.92f);
        ApplyMat(rim, mat, Color.Lerp(color, Color.black, 0.12f));
        DestroyColliderSafe(rim.GetComponent<Collider>());
        var rimR = rim.GetComponent<Renderer>();
        if (rimR != null) rimR.enabled = false;

        if (patch.HasCity)
        {
            float surfaceY = SampleTerrainHeight(patch.Center.x, patch.Center.z, patch) + 0.05f;
            BuildCity(go.transform, patch.CityName, surfaceY, patch.Biome == Biome.Hammerfell);
        }

        if (patch.CityName == "Daggerfall")
        {
            var stage = CreateChild(go.transform, "Cutscene_TalkStage");
            // South of keep, inside walls — overlook toward the bay.
            stage.transform.position = new Vector3(patch.Center.x, height + 2f, patch.Center.z - 120f);
        }

        var top = CreateChild(go.transform, "Top");
        top.transform.localPosition = new Vector3(0f, height + 0.9f, 0f);
        top.transform.localScale = new Vector3(patch.Size.x * 0.85f, 1f, patch.Size.z * 0.85f);
    }

    private void BuildDaggerfallSpawnPad()
    {
        // Central Daggerfall plaza — south gate approach, on flattened city terrain.
        var padPos = WorldLayout.DaggerfallSpawnPad;
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "SpawnPad_Daggerfall";
        pad.transform.SetParent(_root, false);
        pad.transform.position = padPos;
        pad.transform.localScale = new Vector3(48f, 0.35f, 48f);
        pad.layer = GameLayers.Ground;
        ApplyMat(pad, sandMaterial, new Color(0.55f, 0.5f, 0.42f));

        _playerSpawn = padPos + Vector3.up * 0.9f;
    }

    private void BuildTerrainSurface(Transform parent, LandPatch patch, Material mat, Color color)
    {
        int res = Mathf.Clamp((int)(Mathf.Max(patch.Size.x, patch.Size.z) / 45f), 32, 80);
        float halfX = patch.Size.x * 0.49f;
        float halfZ = patch.Size.z * 0.49f;
        float tileMeters = patch.Biome == Biome.Hammerfell ? 48f : patch.Biome == Biome.IslandRock ? 40f : 56f;
        var verts = new Vector3[res * res];
        var uvs = new Vector2[res * res];
        var tris = new int[(res - 1) * (res - 1) * 6];

        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float u = x / (float)(res - 1);
            float v = z / (float)(res - 1);
            float lx = Mathf.Lerp(-halfX, halfX, u);
            float lz = Mathf.Lerp(-halfZ, halfZ, v);
            float wx = patch.Center.x + lx;
            float wz = patch.Center.z + lz;
            float h = SampleTerrainHeight(wx, wz, patch);
            verts[z * res + x] = new Vector3(lx, h, lz);
            uvs[z * res + x] = new Vector2(lx / tileMeters, lz / tileMeters);
        }

        int ti = 0;
        for (int z = 0; z < res - 1; z++)
        for (int x = 0; x < res - 1; x++)
        {
            int i = z * res + x;
            tris[ti++] = i;
            tris[ti++] = i + res;
            tris[ti++] = i + res + 1;
            tris[ti++] = i;
            tris[ti++] = i + res + 1;
            tris[ti++] = i + 1;
        }

        var mesh = new Mesh { name = "TerrainMesh" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var terrainGo = new GameObject("TerrainSurface");
        terrainGo.transform.SetParent(parent, false);
        terrainGo.transform.localPosition = Vector3.zero;
        var mf = terrainGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = terrainGo.AddComponent<MeshRenderer>();
        var terrainMat = WorldVisualFix.CreateTerrainMaterial(mat, color, tileMeters);
        if (terrainMat != null) mr.sharedMaterial = terrainMat;
        else ApplyMat(terrainGo, mat, color);
        var mc = terrainGo.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        terrainGo.layer = GameLayers.Ground;
    }

    private static float SampleTerrainHeight(float worldX, float worldZ, LandPatch patch)
    {
        float baseH = Mathf.Max(4f, patch.Size.y);
        float amp = patch.Biome switch
        {
            Biome.Hammerfell => 28f,
            Biome.IslandRock => 35f,
            Biome.IslandGreen => 22f,
            _ => patch.Size.y > 40f ? 75f : 42f
        };

        float lx = worldX - patch.Center.x;
        float lz = worldZ - patch.Center.z;
        float halfX = patch.Size.x * 0.49f;
        float halfZ = patch.Size.z * 0.49f;

        float edgeDistX = halfX - Mathf.Abs(lx);
        float edgeDistZ = halfZ - Mathf.Abs(lz);
        float edgeDist = Mathf.Min(edgeDistX, edgeDistZ);
        float cliff = Mathf.Clamp01(edgeDist / 120f);

        float cityFlat = 1f;
        if (patch.HasCity)
        {
            float dist = new Vector2(lx, lz).magnitude;
            cityFlat = Mathf.SmoothStep(0f, 1f, (dist - 140f) / 100f);
        }

        float seed = patch.Name.GetHashCode() * 0.001f;
        float n1 = Mathf.PerlinNoise(worldX * 0.0011f + seed, worldZ * 0.0011f + seed);
        float n2 = Mathf.PerlinNoise(worldX * 0.0035f - seed, worldZ * 0.0035f + seed);
        float n3 = Mathf.PerlinNoise(worldX * 0.009f, worldZ * 0.009f);
        float n4 = Mathf.PerlinNoise(worldX * 0.00035f + 40f, worldZ * 0.00035f - 20f);
        float hills = (n1 * 2f - 1f) * 0.45f + (n2 * 2f - 1f) * 0.3f + (n3 * 2f - 1f) * 0.15f;
        float ridges = (n4 * 2f - 1f) * 0.35f;
        float relief = (hills + ridges) * amp * cityFlat;

        float height = baseH + relief * cliff;
        if (cliff < 0.35f)
            height = Mathf.Lerp(2f, height, cliff / 0.35f);
        return height;
    }

    private void BuildCity(Transform parent, string cityName, float surfaceY, bool desertCity)
    {
        float radius = cityName switch
        {
            "Daggerfall" => 220f,
            "Wayrest" => 200f,
            "Sentinel" => 210f,
            _ => 160f
        };
        int buildings = cityName switch
        {
            "Daggerfall" => 140,
            "Wayrest" => 120,
            "Sentinel" => 130,
            _ => 80
        };

        CityDistrictBuilder.Build(parent, surfaceY, new CityDistrictBuilder.Config
        {
            CityName = cityName,
            Desert = desertCity,
            Radius = radius,
            BlockSize = 22f,
            BuildingCount = buildings,
            Rng = _rng,
            BuildingPrefabs = buildingPrefabs,
            TowerPrefabs = towerPrefabs,
            PropPrefabs = propPrefabs,
            DockPrefabs = dockPrefabs,
            CityMat = cityMaterial,
            RoadMat = roadMaterial != null ? roadMaterial : cityMaterial,
            SandMat = sandMaterial
        });
    }

    private void BuildLandmarkTower_Balfiera()
    {
        var island = _root.Find("Island_Balfiera");
        if (island == null)
        {
            return;
        }

        var top = island.Find("Top");
        var parent = top != null ? top : island;

        GameObject tower;
        if (towerPrefabs != null && towerPrefabs.Length > 0)
        {
            tower = Instantiate(towerPrefabs[0], parent);
            tower.transform.localPosition = Vector3.zero;
            tower.transform.localScale = Vector3.one * 6f;
        }
        else
        {
            tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = new Vector3(0f, 40f, 0f);
            tower.transform.localScale = new Vector3(8f, 40f, 8f);
            ApplyMat(tower, cityMaterial, new Color(0.55f, 0.55f, 0.6f));
        }

        tower.name = "AdamantineTower_Homage";
    }

    private void ScatterAllProps()
    {
        foreach (Transform land in _root)
        {
            if (!land.name.StartsWith("HighRock") && !land.name.StartsWith("Hammerfell") && !land.name.StartsWith("Island_"))
            {
                continue;
            }

            var patch = _patches.Find(p => p.Name == land.name);
            int count = patch.PropCount > 0 ? patch.PropCount : 20;
            // Cap for performance on large map
            count = Mathf.Min(count, patch.HasCity ? 70 : 120);
            bool desert = patch.Biome == Biome.Hammerfell;
            bool island = patch.Biome == Biome.IslandGreen || patch.Biome == Biome.IslandRock;
            float halfX = patch.Size.x * 0.42f;
            float halfZ = patch.Size.z * 0.42f;
            float landTopY = Mathf.Max(4f, patch.Size.y);

            for (int i = 0; i < count; i++)
            {
                float x = ((float)_rng.NextDouble() - 0.5f) * 2f * halfX;
                float z = ((float)_rng.NextDouble() - 0.5f) * 2f * halfZ;
                if (patch.HasCity && new Vector2(x, z).magnitude < 280f) continue;

                // Prefer clusters away from exact center for High Rock forests
                if (!desert && !island && _rng.NextDouble() < 0.35)
                {
                    x *= 0.7f;
                    z *= 0.7f;
                }

                var world = land.position + new Vector3(x, landTopY + 40f, z);
                PlacePropWorld(world, desert, island);
            }
        }
    }

    private void PlacePropWorld(Vector3 rayOrigin, bool desert, bool island)
    {
        // Terrain only: the old ~0 cast could land props on the void catcher slab,
        // and then filtered city surfaces back out by name.
        if (!Physics.Raycast(rayOrigin, Vector3.down, out var hit, 120f,
                1 << GameLayers.Ground, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // Keep scatter off built surfaces (roads, plazas, piers, spawn pads).
        if (!hit.collider.gameObject.name.StartsWith("TerrainSurface"))
        {
            return;
        }

        GameObject[] pool;
        if (desert)
            pool = (desertPrefabs != null && desertPrefabs.Length > 0) ? desertPrefabs : rockPrefabs;
        else if (island)
            pool = (rockPrefabs != null && rockPrefabs.Length > 0) ? rockPrefabs : treePrefabs;
        else
            pool = (treePrefabs != null && treePrefabs.Length > 0) ? treePrefabs : rockPrefabs;

        GameObject go;
        if (pool != null && pool.Length > 0 && _rng.NextDouble() > 0.12)
        {
            go = Instantiate(pool[_rng.Next(pool.Length)], _root);
            float s = desert
                ? Mathf.Lerp(1.0f, 2.2f, (float)_rng.NextDouble())
                : island
                    ? Mathf.Lerp(1.2f, 2.4f, (float)_rng.NextDouble())
                    : Mathf.Lerp(2.2f, 4.5f, (float)_rng.NextDouble());
            go.transform.localScale = Vector3.one * s;
        }
        else
        {
            go = GameObject.CreatePrimitive(desert ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            go.transform.SetParent(_root, false);
            float fs = Mathf.Lerp(1.2f, 3.5f, (float)_rng.NextDouble());
            go.transform.localScale = desert
                ? new Vector3(fs, fs * 0.6f, fs)
                : new Vector3(fs * 0.35f, fs, fs * 0.35f);
            ApplyMat(go, desert ? hammerfellMaterial : highRockMaterial,
                desert ? new Color(0.65f, 0.5f, 0.3f) : new Color(0.12f, 0.38f, 0.14f));
        }

        go.name = desert ? "Prop_Desert" : island ? "Prop_Island" : "Prop_Tree";
        go.transform.position = hit.point;
        go.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
        WorldTagger.SetLayerRecursive(go, GameLayers.Prop);

        // Distance cull helper
        var cull = go.AddComponent<FoliageDistanceCull>();
        cull.maxDistance = desert ? 420f : 520f;
    }

    private void BuildRoadsAndPois()
    {
        var roads = WorldLayout.Roads;
        BuildRoad(roads[0], "Road_Daggerfall_Wayrest");
        BuildRoad(roads[1], "Road_Daggerfall_BanditCamp");

        // Bandit camp / coastal ruin (Kenney Survival + Graveyard when wired; else block markers)
        BuildPrefabPoi(WorldLayout.BanditCamp, "POI_BanditCamp", campPrefabs, new Color(0.4f, 0.25f, 0.2f), tentStyle: true);
        BuildPrefabPoi(WorldLayout.CoastalRuin, "POI_CoastalRuin", ruinPrefabs, new Color(0.35f, 0.35f, 0.38f), tentStyle: false);
    }

    /// <summary>
    /// Lay a road along a polyline, projected onto the terrain.
    ///
    /// Roads used to be a single stretched cube per route — the Daggerfall–Wayrest
    /// road was one 4.2 km box at a fixed Y, which sailed through the air over every
    /// dip and buried itself in every rise.
    /// </summary>
    private void BuildRoad(Vector3[] spine, string name)
    {
        if (spine == null || spine.Length < 2) return;

        var root = CreateChild(_root, name);
        root.layer = GameLayers.Ground;

        const float segmentLength = 40f;
        int index = 0;

        for (int i = 0; i < spine.Length - 1; i++)
        {
            var a = spine[i];
            var b = spine[i + 1];
            float span = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(span / segmentLength));

            for (int s = 0; s < steps; s++)
            {
                var from = GroundPoint(Vector3.Lerp(a, b, s / (float)steps));
                var to = GroundPoint(Vector3.Lerp(a, b, (s + 1) / (float)steps));
                BuildRoadSegment(root.transform, from, to, $"Road_{name}_{index:000}");
                index++;
            }
        }
    }

    /// <summary>
    /// Project an XZ position onto the terrain, or onto the causeway deck where the
    /// route crosses open water.
    /// </summary>
    private static Vector3 GroundPoint(Vector3 p)
    {
        var snapped = SnapToGround(p, 0.12f);
        bool foundGround = snapped != p && snapped.y > WorldLayout.WaterLevel;
        return foundGround
            ? snapped
            : new Vector3(p.x, WorldLayout.CausewayDeckY, p.z);
    }

    private void BuildRoadSegment(Transform parent, Vector3 a, Vector3 b, string name)
    {
        var dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f) return;

        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = name;
        road.transform.SetParent(parent, false);
        road.transform.position = (a + b) * 0.5f;
        road.transform.rotation = Quaternion.LookRotation(dir.normalized);
        // Slight overlap (1.04) so adjacent segments don't show seams on slopes.
        road.transform.localScale = new Vector3(8f, 0.25f, len * 1.04f);
        road.layer = GameLayers.Ground;
        ApplyMat(road, roadMaterial != null ? roadMaterial : cityMaterial, new Color(0.25f, 0.24f, 0.22f));
    }

    private void BuildPrefabPoi(Vector3 pos, string name, GameObject[] pool, Color color, bool tentStyle)
    {
        var root = CreateChild(_root, name);
        // Sit the camp/ruin on real terrain rather than an authored guess at the height.
        root.transform.position = PlaceOnLand(pos);

        if (pool != null && pool.Length > 0)
        {
            int count = tentStyle ? 7 : 6;
            for (int i = 0; i < count; i++)
            {
                var prefab = pool[_rng.Next(pool.Length)];
                var go = Instantiate(prefab, root.transform);
                float ang = i * (360f / count) * Mathf.Deg2Rad;
                float r = tentStyle ? 6f + (i % 3) * 2.5f : 5f + (i % 2) * 4f;
                go.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                go.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                float s = tentStyle ? Mathf.Lerp(1.4f, 2.2f, (float)_rng.NextDouble()) : Mathf.Lerp(1.8f, 3.2f, (float)_rng.NextDouble());
                go.transform.localScale = Vector3.one * s;
            }
        }
        else
        {
            BuildFortMarkerFallback(root.transform, color);
        }

        var label = new GameObject("Label");
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 14f, 0f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = name.Replace("POI_", "").Replace("_", " ");
        tm.characterSize = 0.35f;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.95f, 0.85f, 0.6f);
    }

    private void BuildFortMarkerFallback(Transform root, Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root, false);
            wall.transform.localPosition = new Vector3(Mathf.Cos(a) * 12f, 3f, Mathf.Sin(a) * 12f);
            wall.transform.localScale = new Vector3(14f, 6f, 2.5f);
            wall.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            ApplyMat(wall, cityMaterial, color);
        }

        var keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keep.transform.SetParent(root, false);
        keep.transform.localPosition = new Vector3(0f, 5f, 0f);
        keep.transform.localScale = new Vector3(8f, 10f, 8f);
        ApplyMat(keep, cityMaterial, Color.Lerp(color, Color.black, 0.2f));
    }

    private void PlaceProp(Transform parent, Vector3 localPos, bool desert)
    {
        // Legacy path unused — kept for compile safety if referenced.
        PlacePropWorld(parent.TransformPoint(localPos + Vector3.up * 40f), desert, false);
    }

    private void SpawnPlayerAt(Vector3 worldPos)
    {
        worldPos = SnapToGround(worldPos, 1.0f);

        var player = new GameObject("Player");
        player.transform.SetParent(_root, false);
        player.transform.position = worldPos;
        player.layer = GameLayers.Player;
        PlayerRef.Set(player.transform);

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.skinWidth = 0.08f;
        cc.minMoveDistance = 0f;
        cc.stepOffset = 0.35f;

        CharacterLibrary.AttachHumanVisual(player.transform, "character-male-a", 2.1f);

        var camPivot = CreateChild(player.transform, "CameraPivot");
        camPivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        var camGo = CreateChild(camPivot.transform, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        // The world spans ~6.8 km; the default 1000 m far plane popped whole cities.
        cam.farClipPlane = WorldLayout.CameraFarPlane;
        camGo.AddComponent<AudioListener>();

        var controller = player.AddComponent<SimplePlayerController>();
        controller.SetCameraPivot(camPivot.transform);
        // Game flow owns cursor lock / enable during menu → cutscene → play.
        controller.enabled = false;
        cam.enabled = false;

        // Face south toward the bay from Daggerfall.
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>
    /// Drop <paramref name="worldPos"/> onto the walkable surface directly below/above it.
    ///
    /// This used to be <c>SnapToWalkable</c>, which returned the Daggerfall spawn pad
    /// whenever that pad existed in the scene — ignoring its argument entirely. Since the
    /// pad is baked into Main.unity it always existed, so every caller meaning "put this
    /// on the ground here" got "put this at Daggerfall": all six NPCs and all five enemies
    /// spawned in one pile on the start plaza, and re-enabling the player controller
    /// teleported the player home.
    ///
    /// Returns the input unchanged when there is no ground at that XZ, so callers can
    /// detect failure by comparing against what they passed in.
    /// </summary>
    public static Vector3 SnapToGround(Vector3 worldPos, float clearance = 0.1f)
    {
        var origin = new Vector3(worldPos.x, ProbeHeight, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, ProbeDistance,
                GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * clearance;
        }

        // Nothing on the ground layers — fall back to a broad probe so a world that
        // hasn't been tagged yet still places things sensibly.
        var hits = Physics.RaycastAll(origin, Vector3.down, ProbeDistance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? best = null;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            int layer = h.collider.gameObject.layer;
            if (layer == GameLayers.Void || layer == GameLayers.Water || layer == GameLayers.Prop) continue;
            if (h.point.y < WorldLayout.WaterLevel) continue;
            if (best == null || h.point.y > best.Value.point.y) best = h;
        }

        return best.HasValue ? best.Value.point + Vector3.up * clearance : worldPos;
    }

    /// <summary>
    /// Place something on dry land at or near <paramref name="around"/>.
    ///
    /// Terrain height comes from layered Perlin noise, so an authored coordinate can
    /// land in a dip below sea level even when it is well inside a landmass. This
    /// spirals outwards until it finds a spot that is genuinely above water, which
    /// keeps spawned NPCs, enemies and camps from ending up underwater.
    /// </summary>
    public static Vector3 PlaceOnLand(Vector3 around, float searchRadius = 140f, float clearance = 0.1f)
    {
        var direct = SnapToGround(around, clearance);
        if (direct != around && direct.y > WorldLayout.WaterLevel + 0.5f) return direct;

        // Golden-angle spiral: even coverage without needing a grid.
        const int samples = 32;
        const float goldenAngle = 2.39996323f;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float radius = Mathf.Sqrt(t) * searchRadius;
            float angle = i * goldenAngle;
            var probe = around + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            var candidate = SnapToGround(probe, clearance);
            if (candidate != probe && candidate.y > WorldLayout.WaterLevel + 0.5f)
                return candidate;
        }

        return direct;
    }

    /// <summary>True when there is walkable ground at this XZ.</summary>
    public static bool HasGroundAt(Vector3 worldPos)
    {
        var origin = new Vector3(worldPos.x, ProbeHeight, worldPos.z);
        return Physics.Raycast(origin, Vector3.down, ProbeDistance,
            GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Where the player starts and respawns — the Daggerfall plaza pad.
    /// This is the behaviour the old <c>SnapToWalkable</c> accidentally gave every caller;
    /// now only the callers that actually want it get it.
    /// </summary>
    public static Vector3 GetPlayerSpawn()
    {
        var pad = GameObject.Find("SpawnPad_Daggerfall");
        if (pad != null)
        {
            var p = pad.transform.position;
            return new Vector3(p.x, p.y + 1.0f, p.z);
        }

        return SnapToGround(WorldLayout.DaggerfallSpawnPad, 1.0f);
    }

    private const float ProbeHeight = 500f;
    private const float ProbeDistance = 900f;

    private static GameObject CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void ApplyMat(GameObject go, Material mat, Color fallback)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        if (mat != null)
        {
            r.sharedMaterial = mat;
            return;
        }

        var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", fallback);
        else m.color = fallback;
        r.sharedMaterial = m;
    }

    private static void DestroyColliderSafe(Collider col)
    {
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
