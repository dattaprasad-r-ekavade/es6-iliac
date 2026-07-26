using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural city districts: streets, lots, walls, docks, named gates.
/// Uses Kenney Fantasy Town / Castle / Pirate pieces when available; falls back to solid blocks.
/// </summary>
public static class CityDistrictBuilder
{
    public struct Config
    {
        public string CityName;
        public bool Desert;
        public float Radius;      // half-extent of city footprint
        public float BlockSize;   // street grid cell
        public int BuildingCount;
        public System.Random Rng;
        public GameObject[] BuildingPrefabs;
        public GameObject[] TowerPrefabs;
        public GameObject[] PropPrefabs;
        public GameObject[] DockPrefabs;
        public Material CityMat;
        public Material RoadMat;
        public Material SandMat;
    }

    public static void Build(Transform parent, float surfaceY, Config cfg)
    {
        var city = new GameObject($"City_{cfg.CityName}");
        city.transform.SetParent(parent, false);
        city.transform.localPosition = new Vector3(0f, surfaceY, 0f);

        float r = Mathf.Max(80f, cfg.Radius);
        float block = Mathf.Clamp(cfg.BlockSize, 14f, 28f);

        BuildGround(city.transform, r, cfg);
        BuildStreetGrid(city.transform, r, block, cfg);
        BuildOuterWall(city.transform, r, cfg);
        BuildKeep(city.transform, cfg);
        BuildDistricts(city.transform, r, block, cfg);
        BuildHarbor(city.transform, r, cfg);
        BuildCitySigns(city.transform, r, cfg);
    }

    private static void BuildGround(Transform city, float r, Config cfg)
    {
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaza.name = "CityGround";
        plaza.transform.SetParent(city, false);
        plaza.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        plaza.transform.localScale = new Vector3(r * 2.1f, 0.12f, r * 2.1f);
        Apply(plaza, cfg.RoadMat ?? cfg.CityMat, cfg.Desert
            ? new Color(0.62f, 0.52f, 0.38f)
            : new Color(0.42f, 0.4f, 0.38f));
    }

    private static void BuildStreetGrid(Transform city, float r, float block, Config cfg)
    {
        var roads = new GameObject("Streets");
        roads.transform.SetParent(city, false);

        // Main avenues (cross)
        MakeRoad(roads.transform, new Vector3(0f, 0.12f, 0f), new Vector3(r * 1.9f, 0.08f, 10f), cfg);
        MakeRoad(roads.transform, new Vector3(0f, 0.12f, 0f), new Vector3(10f, 0.08f, r * 1.9f), cfg);

        int cells = Mathf.FloorToInt((r * 1.6f) / block);
        for (int i = -cells; i <= cells; i++)
        {
            if (i == 0) continue;
            float x = i * block;
            if (Mathf.Abs(x) > r * 0.92f) continue;
            MakeRoad(roads.transform, new Vector3(x, 0.11f, 0f), new Vector3(4.5f, 0.06f, r * 1.7f), cfg);
            MakeRoad(roads.transform, new Vector3(0f, 0.11f, x), new Vector3(r * 1.7f, 0.06f, 4.5f), cfg);
        }
    }

    private static void MakeRoad(Transform parent, Vector3 pos, Vector3 scale, Config cfg)
    {
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road";
        road.transform.SetParent(parent, false);
        road.transform.localPosition = pos;
        road.transform.localScale = scale;
        Apply(road, cfg.RoadMat ?? cfg.CityMat, new Color(0.28f, 0.27f, 0.26f));
    }

    private static void BuildOuterWall(Transform city, float r, Config cfg)
    {
        var walls = new GameObject("Walls");
        walls.transform.SetParent(city, false);
        int segments = Mathf.Clamp(Mathf.RoundToInt(r / 8f), 24, 64);
        float wallR = r * 0.98f;

        for (int i = 0; i < segments; i++)
        {
            float a0 = (i / (float)segments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            // Gate openings on cardinal directions
            float mid = (a0 + a1) * 0.5f;
            bool gate =
                Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, 0f)) < 12f ||
                Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, 90f)) < 12f ||
                Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, 180f)) < 12f ||
                Mathf.Abs(Mathf.DeltaAngle(mid * Mathf.Rad2Deg, 270f)) < 12f;
            if (gate) continue;

            Vector3 p = new Vector3(Mathf.Cos(mid) * wallR, 0f, Mathf.Sin(mid) * wallR);
            GameObject piece;
            if (TryPrefab(cfg.BuildingPrefabs, n => n.Contains("wall") && !n.Contains("window") && !n.Contains("door"), cfg.Rng, out var prefab))
            {
                piece = Object.Instantiate(prefab, walls.transform);
                piece.transform.localPosition = p;
                piece.transform.localRotation = Quaternion.LookRotation(-p.normalized);
                piece.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, 3.2f, (float)cfg.Rng.NextDouble());
            }
            else
            {
                piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.transform.SetParent(walls.transform, false);
                float h = cfg.Desert ? 7f : 9f;
                piece.transform.localPosition = p + Vector3.up * (h * 0.5f);
                piece.transform.localRotation = Quaternion.LookRotation(-p.normalized);
                piece.transform.localScale = new Vector3(10f, h, 3.2f);
                Apply(piece, cfg.CityMat, cfg.Desert ? new Color(0.7f, 0.58f, 0.4f) : new Color(0.48f, 0.46f, 0.44f));
            }

            piece.name = $"Wall_{i}";
        }

        // Gate towers + named gates
        PlaceGate(walls.transform, new Vector3(0f, 0f, -wallR), 180f, $"{cfg.CityName} South Gate", cfg);
        PlaceGate(walls.transform, new Vector3(0f, 0f, wallR), 0f, $"{cfg.CityName} North Gate", cfg);
        PlaceGate(walls.transform, new Vector3(wallR, 0f, 0f), 90f, $"{cfg.CityName} East Gate", cfg);
        PlaceGate(walls.transform, new Vector3(-wallR, 0f, 0f), -90f, $"{cfg.CityName} West Gate", cfg);
    }

    private static void PlaceGate(Transform parent, Vector3 pos, float yaw, string label, Config cfg)
    {
        var gateRoot = new GameObject(label.Replace(' ', '_'));
        gateRoot.transform.SetParent(parent, false);
        gateRoot.transform.localPosition = pos;
        gateRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        for (int t = -1; t <= 1; t += 2)
        {
            GameObject tower;
            if (TryPrefab(cfg.TowerPrefabs, _ => true, cfg.Rng, out var prefab))
            {
                tower = Object.Instantiate(prefab, gateRoot.transform);
                tower.transform.localPosition = new Vector3(t * 7f, 0f, 0f);
                tower.transform.localScale = Vector3.one * 2.4f;
            }
            else
            {
                tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tower.transform.SetParent(gateRoot.transform, false);
                tower.transform.localPosition = new Vector3(t * 7f, 6f, 0f);
                tower.transform.localScale = new Vector3(5f, 12f, 5f);
                Apply(tower, cfg.CityMat, new Color(0.4f, 0.38f, 0.36f));
            }

            tower.name = t < 0 ? "GateTower_L" : "GateTower_R";
        }

        // Arch / lintel
        var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintel.name = "GateLintel";
        lintel.transform.SetParent(gateRoot.transform, false);
        lintel.transform.localPosition = new Vector3(0f, 8.5f, 0f);
        lintel.transform.localScale = new Vector3(16f, 2f, 4f);
        Apply(lintel, cfg.CityMat, new Color(0.35f, 0.33f, 0.31f));

        MakeWorldLabel(gateRoot.transform, label, new Vector3(0f, 12f, 0f), 2.4f);
    }

    private static void BuildKeep(Transform city, Config cfg)
    {
        var keep = new GameObject("Keep");
        keep.transform.SetParent(city, false);
        keep.transform.localPosition = Vector3.zero;

        var yard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        yard.name = "KeepYard";
        yard.transform.SetParent(keep.transform, false);
        yard.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        yard.transform.localScale = new Vector3(48f, 0.2f, 48f);
        Apply(yard, cfg.CityMat, new Color(0.45f, 0.43f, 0.4f));

        GameObject citadel;
        if (TryPrefab(cfg.TowerPrefabs, n => n.Contains("tower") || n.Contains("keep"), cfg.Rng, out var prefab))
        {
            citadel = Object.Instantiate(prefab, keep.transform);
            citadel.transform.localPosition = new Vector3(0f, 0f, 0f);
            citadel.transform.localScale = Vector3.one * 5.5f;
        }
        else
        {
            citadel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            citadel.transform.SetParent(keep.transform, false);
            citadel.transform.localPosition = new Vector3(0f, 14f, 0f);
            citadel.transform.localScale = new Vector3(18f, 28f, 18f);
            Apply(citadel, cfg.CityMat, new Color(0.38f, 0.36f, 0.34f));
        }

        citadel.name = "Citadel";
        MakeWorldLabel(keep.transform, cfg.CityName, new Vector3(0f, 34f, 0f), 4.5f);
    }

    private static void BuildDistricts(Transform city, float r, float block, Config cfg)
    {
        var districts = new GameObject("Districts");
        districts.transform.SetParent(city, false);

        int placed = 0;
        int target = Mathf.Max(40, cfg.BuildingCount);
        int cells = Mathf.FloorToInt((r * 0.85f) / block);

        for (int ix = -cells; ix <= cells && placed < target; ix++)
        {
            for (int iz = -cells; iz <= cells && placed < target; iz++)
            {
                if (ix == 0 || iz == 0) continue; // keep main avenues clear
                float x = ix * block;
                float z = iz * block;
                if (new Vector2(x, z).magnitude < 55f) continue; // keep yard
                if (new Vector2(x, z).magnitude > r * 0.88f) continue;
                // Skip some lots for courtyards / variety
                if (cfg.Rng.NextDouble() < 0.18) continue;

                PlaceBuildingLot(districts.transform, new Vector3(x, 0f, z), block * 0.72f, cfg, placed);
                placed++;
            }
        }

        // Market stalls near center-south
        int stalls = Mathf.Clamp(target / 8, 8, 24);
        for (int i = 0; i < stalls; i++)
        {
            float a = (float)cfg.Rng.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Lerp(30f, 70f, (float)cfg.Rng.NextDouble());
            var p = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d - 20f);
            PlaceMarketStall(districts.transform, p, cfg, i);
        }
    }

    private static void PlaceBuildingLot(Transform parent, Vector3 pos, float footprint, Config cfg, int index)
    {
        var lot = new GameObject($"Building_{index:000}");
        lot.transform.SetParent(parent, false);
        lot.transform.localPosition = pos;
        float yaw = (cfg.Rng.Next(0, 4)) * 90f;
        lot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Prefer whole-looking prefabs (houses/watermills/commercial buildings) over lone wall bits.
        if (TryPrefab(cfg.BuildingPrefabs, IsWholeBuildingName, cfg.Rng, out var whole))
        {
            var go = Object.Instantiate(whole, lot.transform);
            go.transform.localPosition = Vector3.zero;
            float s = footprint / 8f * Mathf.Lerp(0.9f, 1.35f, (float)cfg.Rng.NextDouble());
            go.transform.localScale = Vector3.one * Mathf.Clamp(s, 1.6f, 4.5f);
            EnsureCollider(go);
            return;
        }

        // Compose a simple house from modular walls + roof, or solid fallback.
        if (TryPrefab(cfg.BuildingPrefabs, n => n.Contains("wall") || n.StartsWith("Wall_"), cfg.Rng, out _) &&
            TryPrefab(cfg.BuildingPrefabs, n => n.Contains("roof") || n.StartsWith("Roof_"), cfg.Rng, out _))
        {
            ComposeModularHouse(lot.transform, footprint, cfg);
            return;
        }

        float h = Mathf.Lerp(cfg.Desert ? 5f : 6f, cfg.Desert ? 11f : 14f, (float)cfg.Rng.NextDouble());
        float w = footprint * Mathf.Lerp(0.7f, 1f, (float)cfg.Rng.NextDouble());
        float d = footprint * Mathf.Lerp(0.65f, 0.95f, (float)cfg.Rng.NextDouble());
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(lot.transform, false);
        body.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        body.transform.localScale = new Vector3(w, h, d);
        Apply(body, cfg.CityMat, cfg.Desert
            ? new Color(0.78f, 0.64f, 0.42f)
            : new Color(0.55f, 0.5f, 0.44f));

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(lot.transform, false);
        roof.transform.localPosition = new Vector3(0f, h + 1.2f, 0f);
        roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        roof.transform.localScale = new Vector3(w * 1.15f, 2.2f, d * 1.15f);
        Apply(roof, cfg.CityMat, cfg.Desert
            ? new Color(0.45f, 0.28f, 0.18f)
            : new Color(0.35f, 0.22f, 0.18f));
    }

    private static void ComposeModularHouse(Transform lot, float footprint, Config cfg)
    {
        float s = footprint / 6f;
        // Four walls
        Vector3[] offsets =
        {
            new Vector3(0f, 0f, -footprint * 0.35f),
            new Vector3(0f, 0f, footprint * 0.35f),
            new Vector3(-footprint * 0.35f, 0f, 0f),
            new Vector3(footprint * 0.35f, 0f, 0f)
        };
        float[] yaws = { 0f, 180f, 90f, -90f };
        for (int i = 0; i < 4; i++)
        {
            bool door = i == 0;
            if (!TryPrefab(cfg.BuildingPrefabs,
                    n => door ? n.Contains("door") || n.Contains("wall") : (n.Contains("window") || n.Contains("wall")),
                    cfg.Rng, out var wallPrefab))
            {
                continue;
            }

            var wall = Object.Instantiate(wallPrefab, lot);
            wall.transform.localPosition = offsets[i];
            wall.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
            wall.transform.localScale = Vector3.one * s;
            EnsureCollider(wall);
        }

        if (TryPrefab(cfg.BuildingPrefabs, n => n.Contains("roof"), cfg.Rng, out var roofPrefab))
        {
            var roof = Object.Instantiate(roofPrefab, lot);
            roof.transform.localPosition = new Vector3(0f, footprint * 0.45f, 0f);
            roof.transform.localScale = Vector3.one * (s * 1.1f);
            EnsureCollider(roof);
        }
    }

    private static void PlaceMarketStall(Transform parent, Vector3 pos, Config cfg, int i)
    {
        var stallRoot = new GameObject($"Stall_{i}");
        stallRoot.transform.SetParent(parent, false);
        stallRoot.transform.localPosition = pos;
        stallRoot.transform.localRotation = Quaternion.Euler(0f, (float)cfg.Rng.NextDouble() * 360f, 0f);

        if (TryPrefab(cfg.BuildingPrefabs, n => n.Contains("stall"), cfg.Rng, out var stall) ||
            TryPrefab(cfg.PropPrefabs, n => n.Contains("stall") || n.Contains("crate"), cfg.Rng, out stall))
        {
            var go = Object.Instantiate(stall, stallRoot.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 2.2f, (float)cfg.Rng.NextDouble());
            EnsureCollider(go);
            return;
        }

        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(stallRoot.transform, false);
        box.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        box.transform.localScale = new Vector3(3.2f, 1.4f, 2.2f);
        Apply(box, cfg.SandMat ?? cfg.CityMat, new Color(0.55f, 0.4f, 0.25f));
    }

    private static void BuildHarbor(Transform city, float r, Config cfg)
    {
        var harbor = new GameObject("Harbor");
        harbor.transform.SetParent(city, false);
        float dockZ = cfg.Desert ? r * 1.05f : -r * 1.05f;
        harbor.transform.localPosition = new Vector3(0f, 0f, dockZ);

        MakeWorldLabel(harbor.transform, $"{cfg.CityName} Docks", new Vector3(0f, 8f, 0f), 2.8f);

        for (int i = -3; i <= 3; i++)
        {
            if (TryPrefab(cfg.DockPrefabs, _ => true, cfg.Rng, out var dockPrefab))
            {
                var d = Object.Instantiate(dockPrefab, harbor.transform);
                d.transform.localPosition = new Vector3(i * 14f, -1.5f, cfg.Desert ? 12f : -12f);
                d.transform.localRotation = Quaternion.Euler(0f, cfg.Desert ? 0f : 180f, 0f);
                d.transform.localScale = Vector3.one * 2.2f;
                EnsureCollider(d);
            }
            else
            {
                var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pier.name = $"Pier_{i}";
                pier.transform.SetParent(harbor.transform, false);
                pier.transform.localPosition = new Vector3(i * 12f, -1.2f, cfg.Desert ? 18f : -18f);
                pier.transform.localScale = new Vector3(6f, 0.6f, 28f);
                Apply(pier, cfg.SandMat ?? cfg.CityMat, new Color(0.35f, 0.25f, 0.15f));
            }
        }

        // Warehouse row
        for (int i = -2; i <= 2; i++)
        {
            PlaceBuildingLot(harbor.transform, new Vector3(i * 18f, 0f, cfg.Desert ? -8f : 8f), 14f, cfg, 900 + i);
        }
    }

    private static void BuildCitySigns(Transform city, float r, Config cfg)
    {
        MakeWorldLabel(city, cfg.CityName.ToUpperInvariant(), new Vector3(0f, 42f, 0f), 6f);

        // Roadside marker just outside south gate for travelers
        float z = cfg.Desert ? r * 1.15f : -r * 1.15f;
        var marker = new GameObject($"Sign_{cfg.CityName}");
        marker.transform.SetParent(city, false);
        marker.transform.localPosition = new Vector3(0f, 0f, z);
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(marker.transform, false);
        post.transform.localPosition = new Vector3(0f, 2f, 0f);
        post.transform.localScale = new Vector3(0.35f, 2f, 0.35f);
        Apply(post, cfg.CityMat, new Color(0.3f, 0.22f, 0.15f));
        MakeWorldLabel(marker.transform, cfg.CityName, new Vector3(0f, 5.2f, 0f), 2.2f);
    }

    private static bool IsWholeBuildingName(string n)
    {
        if (n.Contains("skyscraper")) return false;
        return n.Contains("building") || n.Contains("house") || n.Contains("watermill") ||
               n.Contains("windmill") || n.Contains("shop") || n.Contains("tavern") ||
               n.Contains("inn") || n.Contains("tower-square-base") || n.Contains("tower-hexagon-base") ||
               n.Contains("apartment") || n.Contains("office") || n.Contains("hotel") ||
               n.Contains("restaurant") || n.Contains("detail-shop") || n.Contains("low-") ||
               n.StartsWith("Roof_RoundTiles_") || n.StartsWith("Roof_Tower_");
    }

    private static bool TryPrefab(GameObject[] pool, System.Func<string, bool> pred, System.Random rng, out GameObject prefab)
    {
        prefab = null;
        if (pool == null || pool.Length == 0) return false;
        var matches = new List<GameObject>();
        foreach (var p in pool)
        {
            if (p == null) continue;
            var n = p.name.ToLowerInvariant();
            if (pred(n)) matches.Add(p);
        }

        if (matches.Count == 0) return false;
        prefab = matches[rng.Next(matches.Count)];
        return true;
    }

    private static void EnsureCollider(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponentInChildren<Collider>() != null) return;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }
    }

    private static void MakeWorldLabel(Transform parent, string text, Vector3 localPos, float charSize)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = charSize * 0.08f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.95f, 0.9f, 0.75f);
        // Billboard-ish: face -Z by default; player will see from south approaches.
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private static void Apply(GameObject go, Material mat, Color fallback)
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
}
