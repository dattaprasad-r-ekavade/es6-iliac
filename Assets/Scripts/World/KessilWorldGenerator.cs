using System;
using System.Collections.Generic;
using UnityEngine;

// The generator used to declare its own LandPatch struct and Biome enum, duplicating
// coordinates that the map art, fast travel and NPC spawners each re-declared as well.
using LandPatch = WorldLayout.Landmass;
using Biome = WorldLayout.Biome;

/// <summary>
/// Builds the walkable Kessil Bay region: Halbrand north (temperate), Sarrakh south
/// (arid), the bay between them, the islands Tolm / Corrath / Sarn, and the cities
/// Caldemar / Estmere / Qadris. All geometry is generated from <see cref="WorldLayout"/>.
/// </summary>
public class KessilWorldGenerator : MonoBehaviour
{
    [Header("World")]
    [SerializeField] private int propSeed = 4242;
    [SerializeField] private float waterSize = 8000f;
    [SerializeField] private Material oceanMaterial;
    [SerializeField] private Material halbrandMaterial;
    [SerializeField] private Material sarrakhMaterial;
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

    [ContextMenu("Generate Kessil Bay")]
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

        BuildLandmarkTower_Corrath();

        // Layers must be in place before anything raycasts for ground.
        WorldTagger.TagHierarchy(_root.gameObject);
        Physics.SyncTransforms();

        ScatterAllProps();
        BuildCaldemarSpawnPad();
        Physics.SyncTransforms();
        BuildRoadsAndPois();

        if (spawnPlayer)
        {
            SpawnPlayerAt(_playerSpawn);
        }

        Debug.Log("[Kessil] Map built: Halbrand (N), Sarrakh (S), Kessil Bay + Tolm/Corrath/Sarn. Spawn at Caldemar.");
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
        // Travel scale: cities are kilometres apart (walk, don't hop).
        // +Z = north, +X = east. Definitions are shared via WorldLayout.
        _patches.AddRange(WorldLayout.Landmasses);

        // Overridden by the spawn pad after the city is built.
        _playerSpawn = WorldLayout.CaldemarSpawnPad + Vector3.up * 1.8f;
    }

    private void CreateOcean()
    {
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Kessil_Ocean";
        ocean.transform.SetParent(_root, false);
        ocean.transform.position = new Vector3(0f, WorldLayout.WaterLevel, 0f);
        ocean.transform.localScale = Vector3.one * (waterSize / 10f);
        var oceanMat = oceanMaterial != null ? new Material(oceanMaterial) : WorldVisualFix.CreateWaterMaterial();
        if (oceanMaterial != null)
        {
            var oceanColor = ArtDirection.Active.Palette.Ocean;
            oceanMat.SetColor("_BaseColor", new Color(oceanColor.r, oceanColor.g, oceanColor.b, 0.82f));
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
            Biome.Sarrakh => sarrakhMaterial,
            Biome.IslandRock => mountainMaterial,
            _ => halbrandMaterial
        };
        // Surface colours come from the locked palette, never from literals here. The world
        // used to hardcode them in a dozen places, which meant the art direction could be
        // set on the materials and silently ignored by everything the generator built.
        var palette = ArtDirection.Active.Palette;
        var color = patch.Biome switch
        {
            Biome.Sarrakh => palette.Sarrakh,
            Biome.IslandRock => palette.Mountain,
            Biome.IslandGreen => palette.Halbrand,
            _ => palette.Halbrand
        };

        // Rounded landmasses — cylinders are distant silhouette only; walkable hills use TerrainSurface mesh.
        float height = Mathf.Max(4f, patch.Size.y);

        var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        skirt.name = "Beach";
        skirt.transform.SetParent(go.transform, false);
        skirt.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        skirt.transform.localScale = new Vector3(patch.Size.x * 1.12f, 0.7f, patch.Size.z * 1.12f);
        ApplyMat(skirt, sandMaterial, ArtDirection.Active.Palette.Sand);
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
        BuildCoastBand(go.transform, patch);

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
            float surfaceY = TerrainHeightSampler.Sample(patch.Center.x, patch.Center.z, patch) + 0.05f;
            BuildCity(go.transform, patch.CityName, surfaceY, patch.Biome == Biome.Sarrakh);
        }

        if (patch.CityId == "city_west")
        {
            var stage = CreateChild(go.transform, "Cutscene_TalkStage");
            // South of keep, inside walls — overlook toward the bay.
            stage.transform.position = new Vector3(patch.Center.x, height + 2f, patch.Center.z - 120f);
        }

        var top = CreateChild(go.transform, "Top");
        top.transform.localPosition = new Vector3(0f, height + 0.9f, 0f);
        top.transform.localScale = new Vector3(patch.Size.x * 0.85f, 1f, patch.Size.z * 0.85f);
    }

    private void BuildCaldemarSpawnPad()
    {
        // Central Caldemar plaza — south gate approach, on flattened city terrain.
        var padPos = WorldLayout.CaldemarSpawnPad;
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "SpawnPad_Caldemar";
        pad.transform.SetParent(_root, false);
        pad.transform.position = padPos;
        pad.transform.localScale = new Vector3(48f, 0.35f, 48f);
        pad.layer = GameLayers.Ground;
        ApplyMat(pad, sandMaterial, ArtDirection.Active.Palette.Sand);

        _playerSpawn = padPos + Vector3.up * 0.9f;
    }

    private void BuildTerrainSurface(Transform parent, LandPatch patch, Material mat, Color color)
    {
        const float targetSpacing = 32f;
        Vector2 radii = WorldLayout.GetCoastRadii(patch);
        int angularSegments = GetCoastSegmentCount(radii, targetSpacing);
        int radialRings = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(radii.x, radii.y) / targetSpacing),
            8,
            64);
        float tileMeters = patch.Biome == Biome.Sarrakh ? 48f : patch.Biome == Biome.IslandRock ? 40f : 56f;
        var verts = new Vector3[1 + radialRings * angularSegments];
        var uvs = new Vector2[verts.Length];
        var tris = new int[angularSegments * 3 + (radialRings - 1) * angularSegments * 6];

        float centerY = TerrainHeightSampler.Sample(
            patch.Center.x,
            patch.Center.z,
            patch);
        verts[0] = new Vector3(0f, centerY, 0f);
        uvs[0] = new Vector2(
            patch.Center.x / tileMeters,
            patch.Center.z / tileMeters);

        for (int ring = 1; ring <= radialRings; ring++)
        {
            float normalizedRadius = ring / (float)radialRings;
            int ringStart = 1 + (ring - 1) * angularSegments;
            for (int segment = 0; segment < angularSegments; segment++)
            {
                float angle = segment / (float)angularSegments * Mathf.PI * 2f;
                float lx = Mathf.Cos(angle) * radii.x * normalizedRadius;
                float lz = Mathf.Sin(angle) * radii.y * normalizedRadius;
                float wx = patch.Center.x + lx;
                float wz = patch.Center.z + lz;
                int vertex = ringStart + segment;
                verts[vertex] = new Vector3(
                    lx,
                    TerrainHeightSampler.Sample(wx, wz, patch),
                    lz);
                uvs[vertex] = new Vector2(wx / tileMeters, wz / tileMeters);
            }
        }

        int ti = 0;
        for (int segment = 0; segment < angularSegments; segment++)
        {
            int current = 1 + segment;
            int next = 1 + (segment + 1) % angularSegments;
            tris[ti++] = 0;
            tris[ti++] = next;
            tris[ti++] = current;
        }

        for (int ring = 1; ring < radialRings; ring++)
        {
            int innerStart = 1 + (ring - 1) * angularSegments;
            int outerStart = innerStart + angularSegments;
            for (int segment = 0; segment < angularSegments; segment++)
            {
                int next = (segment + 1) % angularSegments;
                int inner = innerStart + segment;
                int nextInner = innerStart + next;
                int outer = outerStart + segment;
                int nextOuter = outerStart + next;
                tris[ti++] = inner;
                tris[ti++] = nextInner;
                tris[ti++] = nextOuter;
                tris[ti++] = inner;
                tris[ti++] = nextOuter;
                tris[ti++] = outer;
            }
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
        terrainGo.isStatic = true;
    }

    /// <summary>
    /// A thin sand/stone shoulder makes the shared elliptical coast readable and hides
    /// the transition where the terrain mesh descends below the ocean.
    /// </summary>
    private void BuildCoastBand(Transform parent, LandPatch patch)
    {
        Vector2 radii = WorldLayout.GetCoastRadii(patch);
        int segments = GetCoastSegmentCount(radii, 32f);
        const int radialRings = 6;
        const float innerRadius = 0.84f;
        const float outerRadius = 0.985f;

        var verts = new Vector3[segments * radialRings];
        var uvs = new Vector2[verts.Length];
        var tris = new int[segments * (radialRings - 1) * 6];

        for (int ring = 0; ring < radialRings; ring++)
        {
            float radialT = ring / (float)(radialRings - 1);
            float normalizedRadius = Mathf.Lerp(innerRadius, outerRadius, radialT);
            for (int segment = 0; segment < segments; segment++)
            {
                float t = segment / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radii.x * normalizedRadius;
                float z = Mathf.Sin(angle) * radii.y * normalizedRadius;
                float sampledY = TerrainHeightSampler.Sample(
                    patch.Center.x + x,
                    patch.Center.z + z,
                    patch);
                float y = Mathf.Max(
                    WorldLayout.WaterLevel + 0.08f,
                    sampledY + 0.18f);
                int vertex = ring * segments + segment;
                verts[vertex] = new Vector3(x, y, z);
                uvs[vertex] = new Vector2(t * 12f, 1f - radialT);
            }
        }

        int ti = 0;
        for (int ring = 0; ring < radialRings - 1; ring++)
        {
            int innerStart = ring * segments;
            int outerStart = (ring + 1) * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int inner = innerStart + segment;
                int nextInner = innerStart + next;
                int outer = outerStart + segment;
                int nextOuter = outerStart + next;
                tris[ti++] = inner;
                tris[ti++] = nextInner;
                tris[ti++] = nextOuter;
                tris[ti++] = inner;
                tris[ti++] = nextOuter;
                tris[ti++] = outer;
            }
        }

        var mesh = new Mesh { name = $"{patch.Name}_CoastBand" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var band = new GameObject("CoastBand");
        band.transform.SetParent(parent, false);
        var filter = band.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = band.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = patch.Biome == Biome.IslandRock
            ? mountainMaterial
            : sandMaterial;
        band.isStatic = true;
    }

    private static int GetCoastSegmentCount(Vector2 radii, float targetSpacing)
    {
        float a = Mathf.Max(radii.x, radii.y);
        float b = Mathf.Min(radii.x, radii.y);
        float circumference = Mathf.PI * (
            3f * (a + b) - Mathf.Sqrt((3f * a + b) * (a + 3f * b)));
        return Mathf.Clamp(
            Mathf.CeilToInt(circumference / Mathf.Max(8f, targetSpacing)),
            48,
            256);
    }

    private void BuildCity(Transform parent, string cityName, float surfaceY, bool desertCity)
    {
        float radius = cityName switch
        {
            "Caldemar" => 220f,
            "Estmere" => 200f,
            "Qadris" => 210f,
            _ => 160f
        };
        int buildings = cityName switch
        {
            "Caldemar" => 105,
            "Estmere" => 90,
            "Qadris" => 95,
            _ => 70
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

    private void BuildLandmarkTower_Corrath()
    {
        var island = _root.Find("Island_Corrath");
        if (island == null)
        {
            return;
        }

        var top = island.Find("Top");
        var landmarkOrigin = top != null ? top.position : island.position;

        GameObject tower;
        if (towerPrefabs != null && towerPrefabs.Length > 0)
        {
            // "Top" is a broad land-sizing marker whose X/Z scale is hundreds of
            // metres. Parenting the tower to it multiplied the tower's width by
            // that scale. Keep the landmark under the unscaled island instead.
            tower = Instantiate(towerPrefabs[0], island);
            tower.transform.position = landmarkOrigin;
            tower.transform.localScale = Vector3.one;
            FitVisualToHeight(tower, 72f);
        }
        else
        {
            tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.transform.SetParent(island, false);
            tower.transform.position = landmarkOrigin + Vector3.up * 40f;
            tower.transform.localScale = new Vector3(8f, 40f, 8f);
            ApplyMat(tower, cityMaterial, ArtDirection.Active.Palette.CityStone);
        }

        tower.name = "EverspireTower";
        EnsureBoundsBoxCollider(tower);
        tower.isStatic = true;
    }

    private static void FitVisualToHeight(GameObject visual, float targetHeight)
    {
        if (visual == null || targetHeight <= 0.1f) return;
        float baseY = visual.transform.position.y;
        if (!TryGetWorldRendererBounds(visual, out var before) || before.size.y <= 0.001f) return;

        float scale = Mathf.Clamp(targetHeight / before.size.y, 0.01f, 5000f);
        visual.transform.localScale *= scale;
        if (!TryGetWorldRendererBounds(visual, out var after)) return;
        visual.transform.position += Vector3.up * (baseY + 0.1f - after.min.y);
    }

    private static void EnsureBoundsBoxCollider(GameObject root)
    {
        if (root == null || root.GetComponentInChildren<Collider>(true) != null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds localBounds = default;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            Bounds world = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                var worldPoint = world.center + Vector3.Scale(
                    world.extents,
                    new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                var localPoint = root.transform.InverseTransformPoint(worldPoint);
                if (!initialized)
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        if (!initialized) return;
        var box = root.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = new Vector3(
            Mathf.Max(0.5f, localBounds.size.x),
            Mathf.Max(1f, localBounds.size.y),
            Mathf.Max(0.5f, localBounds.size.z));
    }

    private static bool TryGetWorldRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        if (root == null) return false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return initialized;
    }

    private void ScatterAllProps()
    {
        foreach (Transform land in _root)
        {
            if (!land.name.StartsWith("Halbrand") && !land.name.StartsWith("Sarrakh") && !land.name.StartsWith("Island_"))
            {
                continue;
            }

            var patch = _patches.Find(p => p.Name == land.name);
            int count = patch.PropCount > 0 ? patch.PropCount : 20;
            // Cap for performance on large map
            count = Mathf.Min(count, patch.HasCity ? 70 : 120);
            float halfX = patch.Size.x * 0.42f;
            float halfZ = patch.Size.z * 0.42f;
            for (int i = 0; i < count; i++)
            {
                float x = ((float)_rng.NextDouble() - 0.5f) * 2f * halfX;
                float z = ((float)_rng.NextDouble() - 0.5f) * 2f * halfZ;
                if (patch.HasCity && new Vector2(x, z).magnitude < 280f) continue;

                // Prefer clusters away from exact center for Halbrand forests
                if (patch.Biome == Biome.Halbrand && _rng.NextDouble() < 0.35)
                {
                    x *= 0.7f;
                    z *= 0.7f;
                }

                var world = land.position + new Vector3(x, ProbeHeight, z);
                PlacePropWorld(world, patch.Biome);
            }
        }
    }

    private void PlacePropWorld(Vector3 rayOrigin, Biome biome)
    {
        // Terrain only: the old ~0 cast could land props on the void catcher slab,
        // and then filtered city surfaces back out by name.
        if (!Physics.Raycast(rayOrigin, Vector3.down, out var hit, ProbeDistance,
                1 << GameLayers.Ground, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // Keep scatter off built surfaces (roads, plazas, piers, spawn pads).
        if (!hit.collider.gameObject.name.StartsWith("TerrainSurface"))
        {
            return;
        }

        bool desert = biome == Biome.Sarrakh;
        bool rockyIsland = biome == Biome.IslandRock;
        bool greenIsland = biome == Biome.IslandGreen;

        GameObject[] pool;
        if (desert)
            pool = (desertPrefabs != null && desertPrefabs.Length > 0) ? desertPrefabs : rockPrefabs;
        else if (rockyIsland)
            pool = (rockPrefabs != null && rockPrefabs.Length > 0) ? rockPrefabs : treePrefabs;
        else if (greenIsland)
            pool = (treePrefabs != null && treePrefabs.Length > 0) ? treePrefabs : rockPrefabs;
        else
            pool = (treePrefabs != null && treePrefabs.Length > 0) ? treePrefabs : rockPrefabs;

        GameObject go;
        if (pool != null && pool.Length > 0)
        {
            go = Instantiate(pool[_rng.Next(pool.Length)], _root);
            float s = desert
                ? Mathf.Lerp(1.0f, 2.2f, (float)_rng.NextDouble())
                : rockyIsland
                    ? Mathf.Lerp(1.2f, 2.4f, (float)_rng.NextDouble())
                    : greenIsland
                        ? Mathf.Lerp(1.6f, 3.2f, (float)_rng.NextDouble())
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
            ApplyMat(go, desert ? sarrakhMaterial : halbrandMaterial,
                desert
                    ? ArtDirection.Active.Palette.Sarrakh
                    : Color.Lerp(ArtDirection.Active.Palette.Halbrand, Color.black, 0.25f));
        }

        go.name = desert ? "Prop_Desert" : rockyIsland ? "Prop_IslandRock" : "Prop_Tree";
        go.transform.position = hit.point;
        go.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
        WorldTagger.SetLayerRecursive(go, GameLayers.Prop);

        // Distance cull helper
        var cull = go.AddComponent<FoliageDistanceCull>();
        cull.maxDistance = desert ? 420f : rockyIsland ? 460f : 520f;
    }

    private void BuildRoadsAndPois()
    {
        var roads = WorldLayout.Roads;
        for (int i = 0; i < roads.Length; i++)
        {
            string roadName = i switch
            {
                0 => "Road_Caldemar_Estmere",
                1 => "Road_Caldemar_BanditCamp",
                2 => "Road_Kelrith_Karnoth",
                3 => "Road_Qadris_Waste",
                4 => "Road_Waste_Kiln",
                _ => $"Road_Regional_{i:00}"
            };
            BuildRoad(roads[i], roadName);
        }

        // Bandit camp / coastal ruin (Kenney Survival + Graveyard when wired; else block markers)
        BuildPrefabPoi(WorldLayout.BanditCamp, "POI_BanditCamp", campPrefabs, new Color(0.4f, 0.25f, 0.2f), tentStyle: true);
        BuildPrefabPoi(WorldLayout.CoastalRuin, "POI_CoastalRuin", ruinPrefabs, new Color(0.35f, 0.35f, 0.38f), tentStyle: false);
    }

    /// <summary>
    /// Lay a road along a polyline, projected onto the terrain.
    ///
    /// Roads used to be a single stretched cube per route — the Caldemar–Estmere
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
                BuildRoadSegment(root.transform, from, to, $"{name}_{index:000}");

                // Concentrate a modest amount of dressing along the playable routes
                // instead of spending the renderer budget uniformly across 6.8 km.
                if (index % 4 == 0)
                    PlaceRoadsideDressing(from, to, index);
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
        road.transform.localScale = new Vector3(10f, 0.25f, len * 1.04f);
        road.layer = GameLayers.Ground;
        road.isStatic = true;
        ApplyMat(road, roadMaterial != null ? roadMaterial : cityMaterial, ArtDirection.Active.Palette.Road);

        bool causeway = Mathf.Abs(a.y - WorldLayout.CausewayDeckY) < 0.2f
                        && Mathf.Abs(b.y - WorldLayout.CausewayDeckY) < 0.2f;
        if (causeway)
        {
            BuildCausewayRail(parent, road.transform, -5.1f, len, $"Wall_Causeway_{name}_L");
            BuildCausewayRail(parent, road.transform, 5.1f, len, $"Wall_Causeway_{name}_R");
        }
    }

    private void BuildCausewayRail(Transform parent, Transform road, float lateral, float length, string name)
    {
        var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = name;
        rail.transform.SetParent(parent, false);
        rail.transform.position = road.position + road.right * lateral + Vector3.up * 0.72f;
        rail.transform.rotation = road.rotation;
        rail.transform.localScale = new Vector3(0.32f, 1.2f, length * 1.04f);
        rail.layer = GameLayers.Structure;
        rail.isStatic = true;
        ApplyMat(rail, cityMaterial, ArtDirection.Active.Palette.CityStone);
    }

    private void PlaceRoadsideDressing(Vector3 from, Vector3 to, int index)
    {
        var flatDirection = to - from;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.1f) return;

        flatDirection.Normalize();
        var side = Vector3.Cross(Vector3.up, flatDirection);
        float sign = (index & 4) == 0 ? -1f : 1f;
        float distance = Mathf.Lerp(15f, 24f, (float)_rng.NextDouble());
        var point = (from + to) * 0.5f + side * (distance * sign);
        point.y = 0f;
        if (!WorldLayout.TryGetLandmassAt(point, out var patch)) return;

        point.y = ProbeHeight;
        PlacePropWorld(point, patch.Biome);
    }

    private void BuildPrefabPoi(Vector3 pos, string name, GameObject[] pool, Color color, bool tentStyle)
    {
        var root = CreateChild(_root, name);
        // Sit the camp/ruin on real terrain rather than an authored guess at the height.
        root.transform.position = PlaceOnLand(pos);

        if (pool != null && pool.Length > 0)
        {
            int count = tentStyle ? 12 : 10;
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
        PlacePropWorld(
            parent.TransformPoint(localPos + Vector3.up * 40f),
            desert ? Biome.Sarrakh : Biome.Halbrand);
    }

    private void SpawnPlayerAt(Vector3 worldPos)
    {
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

        // CharacterController positions are at the transform origin, not the
        // capsule's feet. Rest the capsule bottom just above the real surface.
        player.transform.position = SnapCharacterToGround(worldPos, cc);

        CharacterLibrary.AttachHumanVisual(player.transform, "character-male-a", 2.1f);

        var camPivot = CreateChild(player.transform, "CameraPivot");
        camPivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        var camGo = CreateChild(camPivot.transform, "Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.cullingMask &= ~(1 << GameLayers.Player);
        // The world spans ~6.8 km; the default 1000 m far plane popped whole cities.
        cam.farClipPlane = WorldLayout.CameraFarPlane;
        camGo.AddComponent<AudioListener>();

        var controller = player.AddComponent<SimplePlayerController>();
        controller.SetCameraPivot(camPivot.transform);
        // Game flow owns cursor lock / enable during menu → cutscene → play.
        controller.enabled = false;
        cam.enabled = false;

        // Face south toward the bay from Caldemar.
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        WorldTagger.SetLayerRecursive(player, GameLayers.Player);
    }

    /// <summary>
    /// Drop <paramref name="worldPos"/> onto the walkable surface directly below/above it.
    ///
    /// This used to be <c>SnapToWalkable</c>, which returned the Caldemar spawn pad
    /// whenever that pad existed in the scene — ignoring its argument entirely. Since the
    /// pad is baked into Main.unity it always existed, so every caller meaning "put this
    /// on the ground here" got "put this at Caldemar": all six NPCs and all five enemies
    /// spawned in one pile on the start plaza, and re-enabling the player controller
    /// teleported the player home.
    ///
    /// Returns the input unchanged when there is no ground at that XZ, so callers can
    /// detect failure by comparing against what they passed in.
    /// </summary>
    public static Vector3 SnapToGround(Vector3 worldPos, float clearance = 0.1f)
    {
        return TryGetGroundPoint(worldPos, out var groundPoint)
            ? groundPoint + Vector3.up * clearance
            : worldPos;
    }

    /// <summary>
    /// Places a CharacterController so the bottom of its capsule rests just above
    /// the surface. Unlike a fixed vertical offset, this remains correct if the
    /// controller's height or centre changes.
    /// </summary>
    public static Vector3 SnapCharacterToGround(
        Vector3 worldPos,
        CharacterController controller,
        float skinClearance = 0.02f)
    {
        if (!TryGetGroundPoint(worldPos, out var groundPoint))
            return worldPos;

        float bottomOffset = controller != null
            ? controller.center.y - controller.height * 0.5f
            : 0f;
        float clearance = Mathf.Max(0.01f, skinClearance);
        return groundPoint - Vector3.up * bottomOffset + Vector3.up * clearance;
    }

    private static bool TryGetGroundPoint(Vector3 worldPos, out Vector3 groundPoint)
    {
        var origin = new Vector3(worldPos.x, ProbeHeight, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, ProbeDistance,
                1 << GameLayers.Ground, QueryTriggerInteraction.Ignore)
            && hit.point.y > WorldLayout.WaterLevel + 0.05f)
        {
            groundPoint = hit.point;
            return true;
        }

        // Nothing on the ground layers — fall back to a broad probe so a world that
        // hasn't been tagged yet still places things sensibly.
        var hits = Physics.RaycastAll(origin, Vector3.down, ProbeDistance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit? best = null;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            int layer = h.collider.gameObject.layer;
            bool groundLike = layer == GameLayers.Ground
                              || (layer == GameLayers.Default
                                  && WorldTagger.Classify(h.collider.gameObject.name) == GameLayers.Ground);
            if (!groundLike || h.point.y <= WorldLayout.WaterLevel + 0.05f) continue;
            if (best == null || h.point.y > best.Value.point.y) best = h;
        }

        if (best.HasValue)
        {
            groundPoint = best.Value.point;
            return true;
        }

        groundPoint = default;
        return false;
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
        var hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            ProbeDistance,
            GameLayers.GroundMask,
            QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.point.y > WorldLayout.WaterLevel + 0.05f)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Where the player starts and respawns — the Caldemar plaza pad.
    /// This is the behaviour the old <c>SnapToWalkable</c> accidentally gave every caller;
    /// now only the callers that actually want it get it.
    /// </summary>
    public static Vector3 GetPlayerSpawn(CharacterController controller = null)
    {
        if (controller == null && PlayerRef.Transform != null)
            controller = PlayerRef.Transform.GetComponent<CharacterController>();

        Vector3 target;
        var pad = GameObject.Find("SpawnPad_Caldemar");
        if (pad != null)
        {
            target = pad.transform.position;
        }
        else
        {
            target = WorldLayout.CaldemarSpawnPad;
        }

        return SnapCharacterToGround(target, controller);
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
