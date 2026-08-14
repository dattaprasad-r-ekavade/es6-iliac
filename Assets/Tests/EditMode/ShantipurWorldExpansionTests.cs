using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Focused authoring checks for the fourth polity. These protect the stable-id link,
/// the dry highland arrival, the authored road connection, and the collision contract
/// inherited from CityDistrictBuilder.
/// </summary>
public class ShantipurWorldExpansionTests
{
    [Test]
    public void ShantipurUsesStableNorthIdAndFitsTheHighlandPlateau()
    {
        WorldLayout.Site? siteValue = WorldLayout.FindSite("city_north");
        Assert.IsTrue(siteValue.HasValue, "The fourth polity has no stable city_north site.");
        WorldLayout.Site site = siteValue.Value;
        Assert.AreEqual("Shantipur", site.DisplayName);
        Assert.IsTrue(site.IsCity);

        WorldLayout.Landmass north = FindCityLandmass("city_north");
        Assert.AreEqual("Shantipur", north.CityName);
        Assert.IsTrue(WorldLayout.IsInsideCoast(site.WorldPosition, north));
        Assert.IsTrue(WorldLayout.IsInsideCoast(site.TravelPosition, north));

        float centreHeight = TerrainHeightSampler.Sample(
            site.WorldPosition.x, site.WorldPosition.z, north);
        float arrivalHeight = TerrainHeightSampler.Sample(
            site.TravelPosition.x, site.TravelPosition.z, north);
        Assert.Greater(centreHeight, WorldLayout.WaterLevel + TerrainHeightSampler.DryInteriorClearance);
        Assert.Greater(arrivalHeight, WorldLayout.WaterLevel + TerrainHeightSampler.DryInteriorClearance);
        Assert.Greater(site.TravelPosition.y, arrivalHeight,
            "Fast travel must arrive above the authored highland ground, not inside it.");

        KessilWorldGenerator.GetCityLayout("city_north", out float radius, out int buildings);
        Assert.AreEqual(180f, radius);
        Assert.AreEqual(80, buildings);
        Vector2 coastRadii = WorldLayout.GetCoastRadii(north);
        Assert.Greater(Mathf.Min(coastRadii.x, coastRadii.y), radius + 24f,
            "The city wall and gate approaches do not fit inside the highland coast.");

        foreach (WorldLayout.Site other in WorldLayout.Sites)
        {
            if (other.Id == site.Id) continue;
            float separation = Vector2.Distance(
                new Vector2(other.WorldPosition.x, other.WorldPosition.z),
                new Vector2(site.WorldPosition.x, site.WorldPosition.z));
            Assert.Greater(separation, radius + 24f,
                $"Site '{other.Id}' overlaps Shantipur's generated footprint.");
        }
    }

    [Test]
    public void ShantipurRoadJoinsTheMainRouteAndEndsAtTheSouthGateArrival()
    {
        WorldLayout.Site site = WorldLayout.FindSite("city_north").Value;
        Vector3[] northRoad = null;
        foreach (Vector3[] road in WorldLayout.Roads)
        {
            if (road.Length > 1 && SameXZ(road[road.Length - 1], site.TravelPosition))
            {
                northRoad = road;
                break;
            }
        }

        Assert.IsNotNull(northRoad, "No authored road reaches the Shantipur travel marker.");
        Assert.IsTrue(WorldLayout.IsOverLand(northRoad[0]), "The route junction starts in water.");
        Assert.IsTrue(WorldLayout.IsOverLand(northRoad[northRoad.Length - 1]),
            "The Shantipur gate end is in water.");

        bool joinsMainRoad = false;
        foreach (Vector3 mainPoint in WorldLayout.Roads[0])
        {
            if (!SameXZ(mainPoint, northRoad[0])) continue;
            joinsMainRoad = true;
            break;
        }
        Assert.IsTrue(joinsMainRoad,
            "The north road must share a junction with the main east-west road.");

        WorldLayout.Landmass north = FindCityLandmass("city_north");
        float distanceFromCentre = Vector2.Distance(
            new Vector2(north.Center.x, north.Center.z),
            new Vector2(site.TravelPosition.x, site.TravelPosition.z));
        Assert.That(distanceFromCentre, Is.InRange(190f, 215f),
            "The arrival should sit outside the south wall but on its gate approach.");
    }

    [Test]
    public void ShantipurDistrictBuildsSolidWallsAndNonOverlappingBuildingColliders()
    {
        WorldLayout.Landmass north = FindCityLandmass("city_north");
        KessilWorldGenerator.GetCityLayout("city_north", out float radius, out int buildings);
        var root = new GameObject("ShantipurBuilderTest");
        Material material = null;

        try
        {
            root.transform.position = north.Center;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader, "No test-safe lit shader is available.");
            material = new Material(shader);

            float surfaceY = TerrainHeightSampler.Sample(
                north.Center.x, north.Center.z, north) + 0.05f;
            CityDistrictBuilder.Build(root.transform, surfaceY, new CityDistrictBuilder.Config
            {
                CityName = "Shantipur",
                Radius = radius,
                BlockSize = 22f,
                BuildingCount = buildings,
                Rng = new System.Random(north.TerrainSeed),
                CityMat = material,
                RoadMat = material,
                SandMat = material
            });
            Physics.SyncTransforms();

            Transform city = root.transform.Find("City_Shantipur");
            Assert.IsNotNull(city, "The stable north layout did not flow into CityDistrictBuilder.");
            Transform walls = city.Find("Walls");
            Transform streets = city.Find("Streets");
            Transform districts = city.Find("Districts");
            Assert.IsNotNull(walls);
            Assert.IsNotNull(streets);
            Assert.IsNotNull(districts);

            int wallPieces = 0;
            foreach (Transform child in walls)
            {
                if (!child.name.StartsWith("Wall_")) continue;
                wallPieces++;
                Assert.IsNotNull(child.GetComponent<BoxCollider>(),
                    $"{child.name} is visible wall geometry without collision.");
            }
            Assert.Greater(wallPieces, 20, "The highland city did not receive a complete outer wall.");

            BoxCollider[] wallColliders = walls.GetComponentsInChildren<BoxCollider>(true);
            BoxCollider[] roadColliders = streets.GetComponentsInChildren<BoxCollider>(true);
            Assert.Greater(roadColliders.Length, 4, "The generated street grid has no solid surface.");
            var buildingBodies = new List<BoxCollider>();
            foreach (Transform child in districts)
            {
                if (!child.name.StartsWith("Building_")) continue;
                Collider[] lotColliders = child.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(1, lotColliders.Length,
                    $"{child.name} should have one simple body collider.");
                var body = child.Find("Body");
                Assert.IsNotNull(body, $"{child.name} has no body geometry.");
                var bodyCollider = body.GetComponent<BoxCollider>();
                Assert.IsNotNull(bodyCollider, $"{child.name} has no building collision.");
                buildingBodies.Add(bodyCollider);

                foreach (BoxCollider road in roadColliders)
                {
                    Assert.IsFalse(OverlapsXZ(bodyCollider.bounds, road.bounds),
                        $"{child.name} overlaps generated street '{road.name}'.");
                }
                foreach (BoxCollider wall in wallColliders)
                {
                    Assert.IsFalse(OverlapsXZ(bodyCollider.bounds, wall.bounds),
                        $"{child.name} overlaps city wall '{wall.name}'.");
                }
            }
            Assert.GreaterOrEqual(buildingBodies.Count, 36,
                "The thinly populated polity still needs a readable district, not an empty wall.");

            for (int i = 0; i < buildingBodies.Count; i++)
            {
                for (int j = i + 1; j < buildingBodies.Count; j++)
                {
                    Assert.IsFalse(OverlapsXZ(buildingBodies[i].bounds, buildingBodies[j].bounds),
                        $"Generated buildings {i} and {j} overlap.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (material != null) Object.DestroyImmediate(material);
        }
    }

    private static WorldLayout.Landmass FindCityLandmass(string cityId)
    {
        foreach (WorldLayout.Landmass landmass in WorldLayout.Landmasses)
            if (landmass.CityId == cityId) return landmass;
        Assert.Fail($"No landmass declares city '{cityId}'.");
        return default;
    }

    private static bool SameXZ(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.z - b.z) < 0.01f;
    }

    private static bool OverlapsXZ(Bounds a, Bounds b)
    {
        const float tolerance = 0.01f;
        return a.min.x < b.max.x - tolerance
               && a.max.x > b.min.x + tolerance
               && a.min.z < b.max.z - tolerance
               && a.max.z > b.min.z + tolerance;
    }
}
