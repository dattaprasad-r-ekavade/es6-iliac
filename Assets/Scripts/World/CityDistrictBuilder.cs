using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural city districts with collision-safe streets, lots, walls, gates and docks.
/// Core structures use measured primitives; bounded ship prefabs are optional decoration.
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

    private const float MainRoadWidth = 10f;
    private const float SideRoadWidth = 4.5f;
    private const float GateClearWidth = 16f;
    private const float WallTargetLength = 24f;

    private static Mesh _pitchedRoofMesh;

    public static void Build(Transform parent, float surfaceY, Config cfg)
    {
        cfg.Rng ??= new System.Random(TerrainHeightSampler.GetStableSeed(cfg.CityName));

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
        // The terrain sampler already flattens the whole city footprint. A thin,
        // collider-free disc supplies the city surface treatment without leaving
        // square corners visible outside the circular wall.
        var plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "CityGround";
        plaza.transform.SetParent(city, false);
        plaza.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        plaza.transform.localScale = new Vector3(r * 2.08f, 0.035f, r * 2.08f);
        plaza.layer = GameLayers.Ground;
        RemoveCollider(plaza.GetComponent<Collider>());
        Apply(plaza, cfg.RoadMat ?? cfg.CityMat, cfg.Desert
            ? new Color(0.62f, 0.52f, 0.38f)
            : new Color(0.42f, 0.4f, 0.38f));
    }

    private static void BuildStreetGrid(Transform city, float r, float block, Config cfg)
    {
        var roads = new GameObject("Streets");
        roads.transform.SetParent(city, false);

        // Main avenues reach every cardinal gate. Lots are placed at cell centres,
        // so these corridors and their intersections remain unobstructed.
        MakeRoad(roads.transform, new Vector3(0f, 0.12f, 0f),
            new Vector3(r * 1.96f, 0.08f, MainRoadWidth), cfg);
        MakeRoad(roads.transform, new Vector3(0f, 0.12f, 0f),
            new Vector3(MainRoadWidth, 0.08f, r * 1.96f), cfg);

        int cells = Mathf.FloorToInt((r * 0.82f) / block);
        for (int i = -cells; i <= cells; i++)
        {
            if (i == 0) continue;
            float x = i * block;
            MakeRoad(roads.transform, new Vector3(x, 0.11f, 0f),
                new Vector3(SideRoadWidth, 0.06f, r * 1.72f), cfg);
            MakeRoad(roads.transform, new Vector3(0f, 0.11f, x),
                new Vector3(r * 1.72f, 0.06f, SideRoadWidth), cfg);
        }
    }

    private static void MakeRoad(Transform parent, Vector3 pos, Vector3 scale, Config cfg)
    {
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Road";
        road.transform.SetParent(parent, false);
        road.transform.localPosition = pos;
        road.transform.localScale = scale;
        road.layer = GameLayers.Ground;
        Apply(road, cfg.RoadMat ?? cfg.CityMat, new Color(0.28f, 0.27f, 0.26f));
    }

    private static void BuildOuterWall(Transform city, float r, Config cfg)
    {
        var walls = new GameObject("Walls");
        walls.transform.SetParent(city, false);
        float wallR = r * 0.98f;
        float wallHeight = cfg.Desert ? 7f : 9f;
        float halfGapAngle = Mathf.Asin(Mathf.Clamp(
            GateClearWidth * 0.5f / wallR, 0f, 0.45f));
        Color wallColor = cfg.Desert
            ? new Color(0.7f, 0.58f, 0.4f)
            : new Color(0.48f, 0.46f, 0.44f);
        int pieceIndex = 0;

        // Build each quadrant independently. The angular endpoints are calculated
        // from the requested opening width, rather than skipping arbitrary pieces.
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            float start = quadrant * Mathf.PI * 0.5f + halfGapAngle;
            float end = (quadrant + 1) * Mathf.PI * 0.5f - halfGapAngle;
            float arc = end - start;
            int segmentCount = Mathf.Max(1,
                Mathf.CeilToInt(wallR * arc / WallTargetLength));
            float step = arc / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float mid = start + (i + 0.5f) * step;
                Vector3 radial = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                float chord = 2f * wallR * Mathf.Sin(step * 0.5f);
                var piece = MakeCube(
                    walls.transform,
                    $"Wall_{pieceIndex:000}",
                    radial * wallR + Vector3.up * (wallHeight * 0.5f),
                    new Vector3(chord + 0.35f, wallHeight, 3.2f),
                    Quaternion.LookRotation(-radial),
                    cfg.CityMat,
                    wallColor,
                    GameLayers.Structure);
                piece.isStatic = true;
                pieceIndex++;
            }
        }

        PlaceGate(walls.transform, new Vector3(0f, 0f, -wallR), 180f,
            $"{cfg.CityName} South Gate", cfg, wallHeight);
        PlaceGate(walls.transform, new Vector3(0f, 0f, wallR), 0f,
            $"{cfg.CityName} North Gate", cfg, wallHeight);
        PlaceGate(walls.transform, new Vector3(wallR, 0f, 0f), 90f,
            $"{cfg.CityName} East Gate", cfg, wallHeight);
        PlaceGate(walls.transform, new Vector3(-wallR, 0f, 0f), -90f,
            $"{cfg.CityName} West Gate", cfg, wallHeight);

        // The city is placed on a flattened build pad. Bridge that pad to the
        // actual terrain at every gate so the wall openings are genuinely usable.
        Physics.SyncTransforms();
        BuildGateApproach(city, Vector3.back, r, cfg, "South");
        BuildGateApproach(city, Vector3.forward, r, cfg, "North");
        BuildGateApproach(city, Vector3.right, r, cfg, "East");
        BuildGateApproach(city, Vector3.left, r, cfg, "West");
    }

    private static void PlaceGate(
        Transform parent,
        Vector3 pos,
        float yaw,
        string label,
        Config cfg,
        float wallHeight)
    {
        var gateRoot = new GameObject(label.Replace(' ', '_'));
        gateRoot.transform.SetParent(parent, false);
        gateRoot.transform.localPosition = pos;
        gateRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        const float towerWidth = 6f;
        const float towerDepth = 6f;
        float towerHeight = wallHeight + 3f;
        float towerOffset = GateClearWidth * 0.5f + towerWidth * 0.5f;
        Color stone = cfg.Desert
            ? new Color(0.62f, 0.5f, 0.34f)
            : new Color(0.4f, 0.38f, 0.36f);

        for (int t = -1; t <= 1; t += 2)
        {
            var tower = MakeCube(
                gateRoot.transform,
                t < 0 ? "GateTower_L" : "GateTower_R",
                new Vector3(t * towerOffset, towerHeight * 0.5f, 0f),
                new Vector3(towerWidth, towerHeight, towerDepth),
                Quaternion.identity,
                cfg.CityMat,
                stone,
                GameLayers.Structure);
            tower.name = t < 0 ? "GateTower_L" : "GateTower_R";
            tower.isStatic = true;
        }

        float openingHeight = Mathf.Max(6.5f, wallHeight * 0.72f);
        var lintel = MakeCube(
            gateRoot.transform,
            "GateLintel",
            new Vector3(0f, openingHeight + 1f, 0f),
            new Vector3(GateClearWidth + towerWidth * 2f, 2f, 4f),
            Quaternion.identity,
            cfg.CityMat,
            stone * 0.9f,
            GameLayers.Structure);
        lintel.isStatic = true;
    }

    private static void BuildKeep(Transform city, Config cfg)
    {
        var keep = new GameObject("Keep");
        keep.transform.SetParent(city, false);
        keep.transform.localPosition = Vector3.zero;

        const float platformSize = 56f;
        const float platformHeight = 1.4f;
        const float rampLength = 16f;
        const float rampWidth = 10f;
        const float keepWidth = 18f;
        const float keepHeight = 20f;
        Color stone = cfg.Desert
            ? new Color(0.58f, 0.48f, 0.34f)
            : new Color(0.42f, 0.4f, 0.38f);

        var yard = MakeCube(
            keep.transform,
            "KeepPlatform",
            new Vector3(0f, platformHeight * 0.5f, 0f),
            new Vector3(platformSize, platformHeight, platformSize),
            Quaternion.identity,
            cfg.CityMat,
            stone,
            GameLayers.Ground);
        yard.isStatic = true;

        float inner = platformSize * 0.5f;
        float outer = inner + rampLength;
        float lowerY = 0.04f;
        float upperY = platformHeight - 0.17f;
        MakeSlopedBox(keep.transform, "KeepRamp_South",
            new Vector3(0f, lowerY, -outer), new Vector3(0f, upperY, -inner),
            rampWidth, 0.35f, cfg.RoadMat ?? cfg.CityMat,
            new Color(0.3f, 0.29f, 0.28f), GameLayers.Ground);
        MakeSlopedBox(keep.transform, "KeepRamp_North",
            new Vector3(0f, lowerY, outer), new Vector3(0f, upperY, inner),
            rampWidth, 0.35f, cfg.RoadMat ?? cfg.CityMat,
            new Color(0.3f, 0.29f, 0.28f), GameLayers.Ground);
        MakeSlopedBox(keep.transform, "KeepRamp_West",
            new Vector3(-outer, lowerY, 0f), new Vector3(-inner, upperY, 0f),
            rampWidth, 0.35f, cfg.RoadMat ?? cfg.CityMat,
            new Color(0.3f, 0.29f, 0.28f), GameLayers.Ground);
        MakeSlopedBox(keep.transform, "KeepRamp_East",
            new Vector3(outer, lowerY, 0f), new Vector3(inner, upperY, 0f),
            rampWidth, 0.35f, cfg.RoadMat ?? cfg.CityMat,
            new Color(0.3f, 0.29f, 0.28f), GameLayers.Ground);

        var citadel = MakeCube(
            keep.transform,
            "Citadel",
            new Vector3(0f, platformHeight + keepHeight * 0.5f, 0f),
            new Vector3(keepWidth, keepHeight, keepWidth),
            Quaternion.identity,
            cfg.CityMat,
            stone * 0.86f,
            GameLayers.Structure);
        citadel.isStatic = true;

        var crown = MakeCube(
            keep.transform,
            "CitadelCrown",
            new Vector3(0f, platformHeight + keepHeight + 1f, 0f),
            new Vector3(keepWidth + 2f, 2f, keepWidth + 2f),
            Quaternion.identity,
            cfg.CityMat,
            cfg.Desert ? new Color(0.42f, 0.31f, 0.2f) : new Color(0.3f, 0.25f, 0.23f),
            GameLayers.Structure,
            false);
        crown.isStatic = true;
    }

    private static void BuildDistricts(Transform city, float r, float block, Config cfg)
    {
        var districts = new GameObject("Districts");
        districts.transform.SetParent(city, false);

        // Lots belong at the centres of street cells, never on the street
        // coordinates themselves. Shuffle the candidates to avoid filling one
        // quadrant first when a city requests fewer buildings.
        var candidates = new List<Vector3>();
        int cells = Mathf.FloorToInt((r * 0.8f) / block);
        for (int ix = -cells; ix < cells; ix++)
        {
            for (int iz = -cells; iz < cells; iz++)
            {
                float x = (ix + 0.5f) * block;
                float z = (iz + 0.5f) * block;
                float radius = new Vector2(x, z).magnitude;
                if (radius < 58f || radius > r * 0.8f) continue;

                // Open market court south of the keep.
                bool marketCourt = Mathf.Abs(x) < block * 2.1f
                                   && z < -block * 1.9f
                                   && z > -block * 3.1f;
                if (marketCourt) continue;
                candidates.Add(new Vector3(x, 0f, z));
            }
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swap = cfg.Rng.Next(i + 1);
            Vector3 temp = candidates[i];
            candidates[i] = candidates[swap];
            candidates[swap] = temp;
        }

        int target = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Max(1, cfg.BuildingCount) * 0.42f), 36, 56);
        int placed = Mathf.Min(target, candidates.Count);
        float footprint = block * 0.48f;
        for (int i = 0; i < placed; i++)
            PlaceBuildingLot(districts.transform, candidates[i], footprint, cfg, i);

        int stalls = Mathf.Clamp(target / 7, 5, 8);
        for (int i = 0; i < stalls; i++)
        {
            int column = i % 4;
            int row = i / 4;
            float x = (column - 1.5f) * block;
            float z = -block * (2.42f + row * 0.16f);
            var p = new Vector3(x, 0f, z);
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
        float h = Mathf.Lerp(cfg.Desert ? 5f : 6f, cfg.Desert ? 11f : 14f, (float)cfg.Rng.NextDouble());
        float w = footprint * Mathf.Lerp(0.78f, 0.98f, (float)cfg.Rng.NextDouble());
        float d = footprint * Mathf.Lerp(0.74f, 0.96f, (float)cfg.Rng.NextDouble());
        float radialBand = new Vector2(pos.x, pos.z).magnitude / Mathf.Max(1f, footprint * 4f);
        float foundation = Mathf.Clamp(0.12f + Mathf.Floor(radialBand) * 0.08f, 0.12f, 0.55f);
        Color wallColor = cfg.Desert
            ? new Color(0.78f, 0.64f, 0.42f)
            : new Color(0.55f, 0.5f, 0.44f);
        Color foundationColor = cfg.Desert
            ? new Color(0.58f, 0.48f, 0.34f)
            : new Color(0.38f, 0.36f, 0.34f);
        Color roofColor = cfg.Desert
            ? new Color(0.45f, 0.28f, 0.18f)
            : new Color(0.35f, 0.22f, 0.18f);

        // Decorative plinth and roof have no colliders. The body owns the one
        // simple footprint collider for this non-enterable building.
        if (index % 3 == 0 || index >= 900)
        {
            MakeCube(
                lot.transform,
                "Foundation",
                new Vector3(0f, foundation * 0.5f, 0f),
                new Vector3(w + 0.5f, foundation, d + 0.5f),
                Quaternion.identity,
                cfg.CityMat,
                foundationColor,
                GameLayers.Structure,
                false);
        }

        var body = MakeCube(
            lot.transform,
            "Body",
            new Vector3(0f, (h + foundation) * 0.5f, 0f),
            new Vector3(w, h + foundation, d),
            Quaternion.identity,
            cfg.CityMat,
            wallColor,
            GameLayers.Structure);
        body.isStatic = true;

        if (cfg.Desert)
        {
            MakeCube(
                lot.transform,
                "FlatRoof",
                new Vector3(0f, h + foundation + 0.3f, 0f),
                new Vector3(w + 0.8f, 0.6f, d + 0.8f),
                Quaternion.identity,
                cfg.CityMat,
                roofColor,
                GameLayers.Structure,
                false);
        }
        else
        {
            var roof = new GameObject("PitchedRoof");
            roof.transform.SetParent(lot.transform, false);
            roof.transform.localPosition = new Vector3(0f, h + foundation, 0f);
            roof.transform.localScale = new Vector3(w + 0.9f, 2.3f, d + 0.9f);
            roof.layer = GameLayers.Structure;
            var filter = roof.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPitchedRoofMesh();
            var renderer = roof.AddComponent<MeshRenderer>();
            if (cfg.CityMat != null)
                renderer.sharedMaterial = cfg.CityMat;
            else
                Apply(roof, null, roofColor);
            roof.isStatic = true;
        }
    }

    private static void PlaceMarketStall(Transform parent, Vector3 pos, Config cfg, int i)
    {
        var stallRoot = new GameObject($"Stall_{i}");
        stallRoot.transform.SetParent(parent, false);
        stallRoot.transform.localPosition = pos;
        stallRoot.transform.localRotation = Quaternion.Euler(
            0f, i % 2 == 0 ? 90f : -90f, 0f);

        MakeCube(
            stallRoot.transform,
            "Counter",
            new Vector3(0f, 0.65f, 0f),
            new Vector3(3.6f, 1.3f, 2.2f),
            Quaternion.identity,
            cfg.SandMat ?? cfg.CityMat,
            new Color(0.55f, 0.4f, 0.25f),
            GameLayers.Structure);
        MakeCube(
            stallRoot.transform,
            "Canopy",
            new Vector3(0f, 2.7f, 0f),
            new Vector3(4.4f, 0.25f, 3.2f),
            Quaternion.Euler(0f, 0f, i % 3 == 0 ? 4f : -4f),
            cfg.CityMat,
            cfg.Desert ? new Color(0.72f, 0.34f, 0.18f) : new Color(0.28f, 0.18f, 0.16f),
            GameLayers.Prop,
            false);
    }

    private static void BuildHarbor(Transform city, float r, Config cfg)
    {
        var harbor = new GameObject("Harbor");
        harbor.transform.SetParent(city, false);
        float outward = cfg.Desert ? 1f : -1f;
        float harborDistance = r * 1.45f;
        if (WorldLayout.TryGetLandmassAt(city.position, out var landmass))
            harborDistance = WorldLayout.GetCoastRadii(landmass).y * 0.92f;

        // Waterfront geometry belongs at the waterline, not on the elevated city
        // pad. This also keeps piers from masquerading as extra roads outside a gate.
        float localWaterY = WorldLayout.WaterLevel - city.position.y;
        harbor.transform.localPosition = new Vector3(
            0f,
            localWaterY,
            outward * harborDistance);
        Color timber = new Color(0.34f, 0.24f, 0.15f);

        BuildHarborApproach(
            city,
            outward,
            r,
            harborDistance,
            localWaterY,
            cfg);

        // The decks are purpose-built primitives with one BoxCollider apiece.
        // Ships never double as piers, which keeps walkable collision predictable.
        var quay = MakeCube(
            harbor.transform,
            "Plaza_HarborQuay",
            new Vector3(0f, 0.16f, outward * 1.5f),
            new Vector3(86f, 0.3f, 10f),
            Quaternion.identity,
            cfg.SandMat ?? cfg.CityMat,
            timber,
            GameLayers.Ground);
        quay.isStatic = true;

        float[] pierX = { -32f, -12f, 12f, 32f };
        for (int i = 0; i < pierX.Length; i++)
        {
            var pier = MakeCube(
                harbor.transform,
                $"Pier_{i:00}",
                new Vector3(pierX[i], 0.16f, outward * 18f),
                new Vector3(5f, 0.3f, 28f),
                Quaternion.identity,
                cfg.SandMat ?? cfg.CityMat,
                timber,
                GameLayers.Ground);
            pier.isStatic = true;
        }

        float[] warehouseX = { -30f, -15f, 15f, 30f };
        for (int i = 0; i < warehouseX.Length; i++)
        {
            PlaceBuildingLot(
                harbor.transform,
                new Vector3(warehouseX[i], 0.31f, -outward * 2f),
                12f,
                cfg,
                900 + i);
        }

        // A maximum of two normalized, collider-free ships dress the offshore
        // water. Badly authored or boundless prefabs are discarded harmlessly.
        for (int i = 0; i < 2; i++)
        {
            if (!TryPrefab(cfg.DockPrefabs,
                    n => n.Contains("ship") || n.Contains("boat"),
                    cfg.Rng,
                    out var shipPrefab))
            {
                break;
            }

            var ship = Object.Instantiate(shipPrefab, harbor.transform);
            ship.name = $"Ship_Decorative_{i:00}";
            ship.transform.localPosition = new Vector3(
                i == 0 ? -23f : 24f,
                0f,
                outward * r * 0.52f);
            ship.transform.localRotation = Quaternion.Euler(
                0f, outward > 0f ? 180f : 0f, 0f);
            ship.transform.localScale = Vector3.one;
            RemoveCollidersRecursive(ship);
            SetLayerRecursive(ship, GameLayers.Prop);

            if (!NormalizeDecoration(ship, i == 0 ? 20f : 17f,
                    WorldLayout.WaterLevel - 0.35f))
            {
                DestroySafe(ship);
            }
        }
    }

    private static void BuildHarborApproach(
        Transform city,
        float outward,
        float cityRadius,
        float harborDistance,
        float localWaterY,
        Config cfg)
    {
        Vector3 direction = outward > 0f ? Vector3.forward : Vector3.back;
        float innerDistance = cityRadius * 1.12f;
        float outerDistance = Mathf.Max(innerDistance + 8f, harborDistance - 5f);
        Vector3 inner = direction * innerDistance + Vector3.up * 0.03f;
        Vector3 innerWorld = city.TransformPoint(direction * innerDistance);
        if (TrySampleTerrainSurface(innerWorld, out float terrainY))
        {
            inner.y = city.InverseTransformPoint(
                new Vector3(innerWorld.x, terrainY, innerWorld.z)).y + 0.03f;
        }

        Vector3 outer = direction * outerDistance
                        + Vector3.up * (localWaterY + 0.28f);
        var approach = MakeSlopedBox(
            city,
            "Road_HarborCauseway",
            inner,
            outer,
            MainRoadWidth + 2f,
            0.3f,
            cfg.RoadMat ?? cfg.CityMat,
            new Color(0.27f, 0.26f, 0.25f),
            GameLayers.Ground);
        approach.isStatic = true;
    }

    private static void BuildCitySigns(Transform city, float r, Config cfg)
    {
        // Roadside marker just outside south gate for travelers
        float z = cfg.Desert ? r * 1.15f : -r * 1.15f;
        var marker = new GameObject($"Sign_{cfg.CityName}");
        marker.transform.SetParent(city, false);
        marker.transform.localPosition = new Vector3(0f, 0f, z);
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.transform.SetParent(marker.transform, false);
        post.transform.localPosition = new Vector3(0f, 2f, 0f);
        post.transform.localScale = new Vector3(0.35f, 2f, 0.35f);
        post.layer = GameLayers.Prop;
        RemoveCollider(post.GetComponent<Collider>());
        Apply(post, cfg.CityMat, new Color(0.3f, 0.22f, 0.15f));
    }

    private static void BuildGateApproach(
        Transform city,
        Vector3 direction,
        float r,
        Config cfg,
        string cardinal)
    {
        float innerDistance = r * 0.88f;
        float outerDistance = r * 1.16f;
        Vector3 inner = direction.normalized * innerDistance + Vector3.up * 0.03f;
        Vector3 outerFlat = direction.normalized * outerDistance;
        Vector3 outerWorld = city.TransformPoint(outerFlat);
        float outerLocalY = 0.03f;
        if (TrySampleTerrainSurface(outerWorld, out float terrainY))
        {
            Vector3 sampledLocal = city.InverseTransformPoint(
                new Vector3(outerWorld.x, terrainY, outerWorld.z));
            float maxDrop = (outerDistance - innerDistance) * 0.3f;
            outerLocalY = Mathf.Clamp(sampledLocal.y + 0.03f, 0.03f - maxDrop, 3f);
        }

        var approach = MakeSlopedBox(
            city,
            $"Road_GateApproach_{cardinal}",
            inner,
            outerFlat + Vector3.up * outerLocalY,
            MainRoadWidth + 2f,
            0.25f,
            cfg.RoadMat ?? cfg.CityMat,
            new Color(0.27f, 0.26f, 0.25f),
            GameLayers.Ground);
        approach.isStatic = true;
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

    private static GameObject MakeCube(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material,
        Color fallback,
        int layer,
        bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        go.layer = layer;
        Apply(go, material, fallback);
        if (!solid)
            RemoveCollider(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject MakeSlopedBox(
        Transform parent,
        string name,
        Vector3 localStart,
        Vector3 localEnd,
        float width,
        float thickness,
        Material material,
        Color fallback,
        int layer)
    {
        Vector3 direction = localEnd - localStart;
        float length = direction.magnitude;
        if (length < 0.01f)
        {
            return MakeCube(
                parent,
                name,
                localStart,
                new Vector3(width, thickness, 0.1f),
                Quaternion.identity,
                material,
                fallback,
                layer);
        }

        return MakeCube(
            parent,
            name,
            (localStart + localEnd) * 0.5f,
            new Vector3(width, thickness, length * 1.02f),
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            material,
            fallback,
            layer);
    }

    private static bool TrySampleTerrainSurface(Vector3 worldPoint, out float surfaceY)
    {
        surfaceY = 0f;
        var origin = new Vector3(worldPoint.x, worldPoint.y + 500f, worldPoint.z);
        var hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            1000f,
            1 << GameLayers.Ground,
            QueryTriggerInteraction.Ignore);
        bool found = false;
        float best = float.NegativeInfinity;
        foreach (var hit in hits)
        {
            if (hit.collider == null
                || !hit.collider.gameObject.name.StartsWith("TerrainSurface"))
            {
                continue;
            }

            if (hit.point.y <= best) continue;
            best = hit.point.y;
            found = true;
        }

        if (found) surfaceY = best;
        return found;
    }

    private static Mesh GetPitchedRoofMesh()
    {
        if (_pitchedRoofMesh != null) return _pitchedRoofMesh;

        var mesh = new Mesh { name = "ProceduralPitchedRoof" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0f, 1f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0f, 1f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 3, 5, 0, 5, 2,
            1, 2, 5, 1, 5, 4,
            0, 2, 1,
            3, 4, 5,
            0, 1, 4, 0, 4, 3
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        _pitchedRoofMesh = mesh;
        return _pitchedRoofMesh;
    }

    private static bool NormalizeDecoration(
        GameObject go,
        float targetLongestHorizontal,
        float desiredBottomWorldY)
    {
        if (!TryGetRendererBounds(go, out var bounds)) return false;
        float longest = Mathf.Max(bounds.size.x, bounds.size.z);
        if (!IsUsableNumber(longest) || longest < 0.001f) return false;

        float factor = targetLongestHorizontal / longest;
        if (!IsUsableNumber(factor) || factor < 0.02f || factor > 50f)
            return false;

        go.transform.localScale *= factor;
        if (!TryGetRendererBounds(go, out bounds)) return false;
        go.transform.position += Vector3.up * (desiredBottomWorldY - bounds.min.y);
        return true;
    }

    private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        if (go == null) return false;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found
               && IsUsableNumber(bounds.size.x)
               && IsUsableNumber(bounds.size.y)
               && IsUsableNumber(bounds.size.z);
    }

    private static bool IsUsableNumber(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null) return;
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
    }

    private static void RemoveCollidersRecursive(GameObject root)
    {
        if (root == null) return;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            RemoveCollider(collider);
    }

    private static void RemoveCollider(Collider collider)
    {
        if (collider == null) return;
        collider.enabled = false;
        DestroySafe(collider);
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj);
        else Object.DestroyImmediate(obj);
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
