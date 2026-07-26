using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a walkable multi-island fantasy world: water, islands, a coastal city, and random props.
/// Homage prototype — original layout, free CC0 Kenney props when available.
/// </summary>
public class IslandWorldGenerator : MonoBehaviour
{
    [Header("World")]
    [SerializeField] private int seed = 1337;
    [SerializeField] private int islandCount = 6;
    [SerializeField] private float worldRadius = 220f;
    [SerializeField] private float waterSize = 600f;
    [SerializeField] private Material waterMaterial;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material sandMaterial;
    [SerializeField] private Material cityMaterial;

    [Header("Scatter")]
    [SerializeField] private int treesPerIsland = 28;
    [SerializeField] private int rocksPerIsland = 12;
    [SerializeField] private int cityBuildings = 18;
    [SerializeField] private GameObject[] treePrefabs;
    [SerializeField] private GameObject[] rockPrefabs;
    [SerializeField] private GameObject[] buildingPrefabs;

    [Header("Player")]
    [SerializeField] private bool spawnPlayer = true;
    [SerializeField] private GameObject playerPrefab;

    private readonly List<Transform> _islands = new();
    private System.Random _rng;

    [ContextMenu("Generate World")]
    public void GenerateWorld()
    {
        ClearGenerated();
        _rng = new System.Random(seed);

        CreateWater();
        CreateIslands();
        BuildCityOnLargestIsland();
        ScatterNature();
        if (spawnPlayer)
        {
            SpawnPlayer();
        }

        Debug.Log($"[IslandWorldGenerator] World ready (seed={seed}, islands={_islands.Count}).");
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
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        _islands.Clear();
    }

    private Transform GeneratedRoot()
    {
        var root = transform.Find("Generated");
        if (root == null)
        {
            var go = new GameObject("Generated");
            go.transform.SetParent(transform, false);
            root = go.transform;
        }

        return root;
    }

    private void CreateWater()
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Ocean";
        water.transform.SetParent(GeneratedRoot(), false);
        water.transform.position = new Vector3(0f, -0.2f, 0f);
        water.transform.localScale = Vector3.one * (waterSize / 10f);
        ApplyMat(water, waterMaterial, new Color(0.12f, 0.35f, 0.55f, 1f));
        var col = water.GetComponent<Collider>();
        if (col != null)
        {
            DestroyCollider(col);
        }
    }

    private void CreateIslands()
    {
        // Island 0 is always near origin (starter / city island).
        CreateIsland(0, Vector3.zero, 55f, 8f, true);

        for (int i = 1; i < islandCount; i++)
        {
            float angle = (float)(_rng.NextDouble() * Mathf.PI * 2f);
            float dist = Mathf.Lerp(70f, worldRadius, (float)_rng.NextDouble());
            var pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            float radius = Mathf.Lerp(18f, 42f, (float)_rng.NextDouble());
            float height = Mathf.Lerp(3f, 14f, (float)_rng.NextDouble());
            CreateIsland(i, pos, radius, height, false);
        }
    }

    private void CreateIsland(int index, Vector3 center, float radius, float height, bool isCity)
    {
        var island = new GameObject(isCity ? "Island_CityHarbor" : $"Island_{index}");
        island.transform.SetParent(GeneratedRoot(), false);
        island.transform.position = center;

        // Layered discs: sand ring + grassy top.
        var sand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sand.name = "Sand";
        sand.transform.SetParent(island.transform, false);
        sand.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        sand.transform.localScale = new Vector3(radius * 2.2f, 0.35f, radius * 2.2f);
        ApplyMat(sand, sandMaterial, new Color(0.84f, 0.74f, 0.52f));

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Ground";
        ground.transform.SetParent(island.transform, false);
        ground.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        ground.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
        ApplyMat(ground, isCity ? cityMaterial : groundMaterial,
            isCity ? new Color(0.45f, 0.5f, 0.42f) : new Color(0.28f, 0.48f, 0.26f));

        // Walkable top marker for scatter / spawn.
        var top = new GameObject("Top");
        top.transform.SetParent(island.transform, false);
        top.transform.localPosition = new Vector3(0f, height + 0.2f, 0f);
        _islands.Add(top.transform);
    }

    private void BuildCityOnLargestIsland()
    {
        if (_islands.Count == 0)
        {
            return;
        }

        var cityRoot = new GameObject("City_HarborTown");
        cityRoot.transform.SetParent(_islands[0], false);

        // Plaza
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaza.name = "Plaza";
        plaza.transform.SetParent(cityRoot.transform, false);
        plaza.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        plaza.transform.localScale = new Vector3(28f, 0.2f, 28f);
        ApplyMat(plaza, cityMaterial, new Color(0.55f, 0.52f, 0.48f));

        // Road ring
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = $"Road_{i}";
            road.transform.SetParent(cityRoot.transform, false);
            road.transform.localPosition = new Vector3(Mathf.Cos(a) * 18f, 0.08f, Mathf.Sin(a) * 18f);
            road.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            road.transform.localScale = new Vector3(8f, 0.15f, 4f);
            ApplyMat(road, cityMaterial, new Color(0.4f, 0.38f, 0.35f));
        }

        // Buildings around plaza
        for (int i = 0; i < cityBuildings; i++)
        {
            float a = (i / (float)cityBuildings) * Mathf.PI * 2f + (float)_rng.NextDouble() * 0.2f;
            float dist = Mathf.Lerp(12f, 24f, (float)_rng.NextDouble());
            var pos = new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);
            PlaceBuilding(cityRoot.transform, pos, a * Mathf.Rad2Deg + 180f);
        }

        // Simple dock toward -Z water
        var dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dock.name = "Dock";
        dock.transform.SetParent(cityRoot.transform, false);
        dock.transform.localPosition = new Vector3(0f, -1.5f, -34f);
        dock.transform.localScale = new Vector3(6f, 0.4f, 18f);
        ApplyMat(dock, sandMaterial, new Color(0.45f, 0.3f, 0.18f));
    }

    private void PlaceBuilding(Transform parent, Vector3 localPos, float yaw)
    {
        GameObject building;
        if (buildingPrefabs != null && buildingPrefabs.Length > 0)
        {
            var prefab = buildingPrefabs[_rng.Next(buildingPrefabs.Length)];
            building = Instantiate(prefab, parent);
            building.transform.localPosition = localPos;
            building.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float s = Mathf.Lerp(1.2f, 2.2f, (float)_rng.NextDouble());
            building.transform.localScale = Vector3.one * s;
        }
        else
        {
            building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.SetParent(parent, false);
            float w = Mathf.Lerp(3f, 6f, (float)_rng.NextDouble());
            float h = Mathf.Lerp(4f, 10f, (float)_rng.NextDouble());
            float d = Mathf.Lerp(3f, 6f, (float)_rng.NextDouble());
            building.transform.localPosition = localPos + Vector3.up * (h * 0.5f);
            building.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            building.transform.localScale = new Vector3(w, h, d);
            ApplyMat(building, cityMaterial, Color.Lerp(new Color(0.55f, 0.45f, 0.35f), new Color(0.7f, 0.65f, 0.55f), (float)_rng.NextDouble()));

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(building.transform, false);
            roof.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            roof.transform.localScale = new Vector3(1.1f, 0.2f, 1.1f);
            ApplyMat(roof, cityMaterial, new Color(0.45f, 0.2f, 0.15f));
        }

        building.name = "Building";
    }

    private void ScatterNature()
    {
        for (int i = 0; i < _islands.Count; i++)
        {
            var island = _islands[i];
            bool skipCenterCity = i == 0;
            int trees = skipCenterCity ? treesPerIsland / 3 : treesPerIsland;
            int rocks = rocksPerIsland;

            for (int t = 0; t < trees; t++)
            {
                PlaceProp(island, treePrefabs, trees > 0, PrimitiveType.Capsule, new Color(0.15f, 0.4f, 0.15f), 8f, 30f, 1.5f, 4f);
            }

            for (int r = 0; r < rocks; r++)
            {
                PlaceProp(island, rockPrefabs, true, PrimitiveType.Sphere, new Color(0.4f, 0.4f, 0.42f), 6f, 32f, 0.6f, 1.8f);
            }
        }
    }

    private void PlaceProp(Transform islandTop, GameObject[] prefabs, bool allowPrefab, PrimitiveType fallback, Color color, float minR, float maxR, float minScale, float maxScale)
    {
        float a = (float)(_rng.NextDouble() * Mathf.PI * 2f);
        float dist = Mathf.Lerp(minR, maxR, (float)_rng.NextDouble());
        var local = new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);

        GameObject go;
        if (allowPrefab && prefabs != null && prefabs.Length > 0)
        {
            go = Instantiate(prefabs[_rng.Next(prefabs.Length)], islandTop);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            float s = Mathf.Lerp(minScale, maxScale, (float)_rng.NextDouble());
            go.transform.localScale = Vector3.one * s;
        }
        else
        {
            go = GameObject.CreatePrimitive(fallback);
            go.transform.SetParent(islandTop, false);
            float s = Mathf.Lerp(minScale, maxScale, (float)_rng.NextDouble());
            go.transform.localPosition = local + Vector3.up * (s * 0.5f);
            go.transform.localScale = Vector3.one * s;
            ApplyMat(go, groundMaterial, color);
        }
    }

    private void SpawnPlayer()
    {
        Vector3 spawn = _islands[0].position + new Vector3(0f, 2f, -8f);
        GameObject player;
        if (playerPrefab != null)
        {
            player = Instantiate(playerPrefab, spawn, Quaternion.identity);
        }
        else
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = spawn;
            Object.DestroyImmediate(player.GetComponent<Collider>());
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.up;
            var camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(player.transform, false);
            camPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camPivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var controller = player.AddComponent<SimplePlayerController>();
            controller.SetCameraPivot(camPivot.transform);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        player.name = "Player";
        player.transform.SetParent(GeneratedRoot(), true);
    }

    private static void ApplyMat(GameObject go, Material mat, Color fallbackColor)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        if (mat != null)
        {
            renderer.sharedMaterial = mat;
        }
        else
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.color = fallbackColor;
            renderer.sharedMaterial = m;
        }
    }

    private static void DestroyCollider(Collider col)
    {
        if (Application.isPlaying)
        {
            Object.Destroy(col);
        }
        else
        {
            Object.DestroyImmediate(col);
        }
    }
}
