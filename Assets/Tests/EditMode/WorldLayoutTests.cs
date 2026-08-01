using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Authoring checks on the world description.
///
/// These exist because the prototype shipped with the bandit camp and the coastal
/// ruin placed in open water, and with the same coordinates copied into five files
/// that had quietly drifted apart. Both classes of mistake are cheap to catch here.
/// </summary>
public class WorldLayoutTests
{
    [Test]
    public void VersionedJsonIsTheRuntimeWorldSource()
    {
        var source = Resources.Load<TextAsset>(WorldLayoutData.ResourcePath);
        Assert.IsNotNull(source, "kessil.world.json is missing from Resources.");
        var document = JsonUtility.FromJson<WorldLayoutDocument>(source.text);
        Assert.AreEqual(WorldLayoutData.CurrentVersion, document.Version);
        Assert.AreEqual(document.Landmasses.Length, WorldLayout.Landmasses.Length);
        Assert.AreEqual(document.Sites.Length, WorldLayout.Sites.Length);
        Assert.AreEqual(document.Roads.Length, WorldLayout.Roads.Length);
        Assert.AreEqual(document.CaldemarSpawnPad, WorldLayout.CaldemarSpawnPad);
        Assert.AreEqual(document.Landmasses[0].TerrainSeed, WorldLayout.Landmasses[0].TerrainSeed);
        Assert.AreEqual(document.Sites[0].Id, WorldLayout.Sites[0].Id);
    }

    [Test]
    public void EverySite_HasUniqueId()
    {
        var seen = new HashSet<string>();
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsFalse(string.IsNullOrEmpty(site.Id), "A site has an empty id.");
            Assert.IsTrue(seen.Add(site.Id), $"Duplicate site id '{site.Id}'.");
        }
    }

    /// <summary>
    /// Ids are written into save files, so a place name must never become one: renaming
    /// the setting would then silently invalidate every existing save. Display names are
    /// what change; ids describe the world's shape instead.
    /// </summary>
    [Test]
    public void SiteAndCityIds_DoNotEmbedDisplayNames()
    {
        foreach (var site in WorldLayout.Sites)
        {
            Assert.AreNotEqual(site.DisplayName.ToLowerInvariant(), site.Id.ToLowerInvariant(),
                $"Site id '{site.Id}' is just its display name — ids must survive a rename.");
        }

        foreach (var land in WorldLayout.Landmasses)
        {
            if (!land.HasCity) continue;
            Assert.AreNotEqual(land.CityName.ToLowerInvariant(), land.CityId.ToLowerInvariant(),
                $"City id '{land.CityId}' is just its display name — ids must survive a rename.");
        }
    }

    [Test]
    public void EverySite_TravelPositionIsOverLand()
    {
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsTrue(WorldLayout.TryGetLandmassAt(site.TravelPosition, out var land),
                $"Fast travel to '{site.Id}' would drop the player in open water at " +
                $"({site.TravelPosition.x:0}, {site.TravelPosition.z:0}).");
            Assert.IsTrue(WorldLayout.IsInsideCoast(site.TravelPosition, land),
                $"Fast travel to '{site.Id}' disagrees with the shared coast geometry.");
        }
    }

    [Test]
    public void EverySite_WorldPositionIsOverLand()
    {
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsTrue(WorldLayout.TryGetLandmassAt(site.WorldPosition, out var land),
                $"Site '{site.Id}' is centred on open water at " +
                $"({site.WorldPosition.x:0}, {site.WorldPosition.z:0}).");
            Assert.IsTrue(WorldLayout.IsInsideCoast(site.WorldPosition, land),
                $"Site '{site.Id}' disagrees with the shared coast geometry.");
        }
    }

    [Test]
    public void CitySites_MatchLandmassesThatDeclareACity()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            if (!land.HasCity) continue;

            bool hasSite = false;
            foreach (var site in WorldLayout.Sites)
            {
                // Matched on the stable id, not the display name: renaming the setting
                // must not be able to break the landmass/site link.
                if (site.IsCity && site.Id == land.CityId) { hasSite = true; break; }
            }

            Assert.IsTrue(hasSite,
                $"Landmass '{land.Name}' builds city '{land.CityId}' but no map site has that id.");
        }
    }

    [Test]
    public void HostilePois_AreOutsideTheSafeZone()
    {
        // Enemies do not aggro inside the safe zone, so a camp placed there is inert.
        foreach (var pos in new[] { WorldLayout.BanditCamp, WorldLayout.CoastalRuin })
        {
            Assert.IsFalse(WorldLayout.IsInSafeZone(pos),
                $"Hostile POI at ({pos.x:0}, {pos.z:0}) is inside the Caldemar safe zone, " +
                "so nothing there will ever fight back.");
        }
    }

    [Test]
    public void Roads_StartAndEndOverLand()
    {
        // Middles may cross water — those sections become causeways — but the ends
        // have to actually meet the ground.
        foreach (var road in WorldLayout.Roads)
        {
            Assert.GreaterOrEqual(road.Length, 2, "A road needs at least two points.");
            Assert.IsTrue(WorldLayout.IsOverLand(road[0]),
                $"Road starts over water at ({road[0].x:0}, {road[0].z:0}).");
            Assert.IsTrue(WorldLayout.IsOverLand(road[road.Length - 1]),
                $"Road ends over water at ({road[road.Length - 1].x:0}, {road[road.Length - 1].z:0}).");
        }
    }

    [Test]
    public void MainlandConnections_AreExplicitAndKeepOriginalRoadsFirst()
    {
        Assert.AreEqual(5, WorldLayout.Roads.Length);
        Assert.AreEqual(new Vector3(-2000f, 0f, 1450f), WorldLayout.Roads[0][0]);
        Assert.AreEqual(WorldLayout.BanditCamp, WorldLayout.Roads[1][WorldLayout.Roads[1].Length - 1]);

        for (int i = 2; i < WorldLayout.Roads.Length; i++)
        {
            var road = WorldLayout.Roads[i];
            Assert.IsTrue(WorldLayout.TryGetLandmassAt(road[0], out _),
                $"Mainland connection {i} starts outside the shared coast.");
            Assert.IsTrue(WorldLayout.TryGetLandmassAt(road[road.Length - 1], out _),
                $"Mainland connection {i} ends outside the shared coast.");
        }
    }

    [Test]
    public void SpawnPad_IsOnTheCaldemarLandmass()
    {
        Assert.IsTrue(WorldLayout.TryGetLandmassAt(WorldLayout.CaldemarSpawnPad, out var land),
            "The player spawn pad is not over any landmass.");
        Assert.AreEqual("city_west", land.CityId,
            $"The spawn pad landed on '{land.Name}' instead of the start-city peninsula.");
    }

    [Test]
    public void MapBounds_ContainEveryLandmass()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            float west = land.Center.x - land.Size.x * 0.5f;
            float east = land.Center.x + land.Size.x * 0.5f;
            float south = land.Center.z - land.Size.z * 0.5f;
            float north = land.Center.z + land.Size.z * 0.5f;

            Assert.GreaterOrEqual(
                west - WorldLayout.MapMinX,
                WorldLayout.MapExtentPadding,
                $"'{land.Name}' lacks west map padding.");
            Assert.GreaterOrEqual(
                WorldLayout.MapMaxX - east,
                WorldLayout.MapExtentPadding,
                $"'{land.Name}' lacks east map padding.");
            Assert.GreaterOrEqual(
                south - WorldLayout.MapMinZ,
                WorldLayout.MapExtentPadding,
                $"'{land.Name}' lacks south map padding.");
            Assert.GreaterOrEqual(
                WorldLayout.MapMaxZ - north,
                WorldLayout.MapExtentPadding,
                $"'{land.Name}' lacks north map padding.");
        }
    }

    [Test]
    public void SharedCoast_IsElliptical()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            var radii = WorldLayout.GetCoastRadii(land);
            var eastCoast = land.Center + new Vector3(radii.x, 0f, 0f);
            var northCoast = land.Center + new Vector3(0f, 0f, radii.y);
            var outside = land.Center + new Vector3(radii.x * 1.01f, 0f, 0f);
            var rectangleCorner = land.Center + new Vector3(radii.x, 0f, radii.y);

            Assert.AreEqual(1f, WorldLayout.GetNormalizedCoastDistance(eastCoast, land), 0.0001f);
            Assert.AreEqual(1f, WorldLayout.GetNormalizedCoastDistance(northCoast, land), 0.0001f);
            Assert.IsTrue(WorldLayout.IsInsideCoast(eastCoast, land));
            Assert.IsFalse(WorldLayout.IsInsideCoast(outside, land));
            Assert.IsFalse(WorldLayout.IsInsideCoast(rectangleCorner, land),
                $"'{land.Name}' still treats a rectangular corner as land.");
        }
    }

    [Test]
    public void TerrainSeeds_AreExplicitAndUnique()
    {
        var seen = new HashSet<int>();
        foreach (var land in WorldLayout.Landmasses)
        {
            Assert.AreNotEqual(0, land.TerrainSeed, $"'{land.Name}' has no authored terrain seed.");
            Assert.IsTrue(seen.Add(land.TerrainSeed),
                $"'{land.Name}' reuses terrain seed {land.TerrainSeed}.");
        }

        Assert.AreEqual(
            TerrainHeightSampler.GetStableSeed("Kessil"),
            TerrainHeightSampler.GetStableSeed("Kessil"));
        Assert.AreNotEqual(
            TerrainHeightSampler.GetStableSeed("Kessil"),
            TerrainHeightSampler.GetStableSeed("Halbrand"));
    }

    [Test]
    public void TerrainSampler_IsDeterministic()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            var radii = WorldLayout.GetCoastRadii(land);
            var sample = land.Center + new Vector3(radii.x * 0.31f, 0f, radii.y * -0.27f);
            float first = TerrainHeightSampler.Sample(sample.x, sample.z, land);
            float second = TerrainHeightSampler.Sample(sample.x, sample.z, land);
            Assert.AreEqual(first, second, 0f, $"'{land.Name}' terrain is not deterministic.");
        }
    }

    [Test]
    public void TerrainSampler_KeepsRepresentativeInteriorsDry()
    {
        var normalisedSamples = new[]
        {
            Vector2.zero,
            new Vector2(0.35f, 0f),
            new Vector2(-0.3f, 0.25f),
            new Vector2(0.2f, -0.4f)
        };

        foreach (var land in WorldLayout.Landmasses)
        {
            var radii = WorldLayout.GetCoastRadii(land);
            foreach (var sample in normalisedSamples)
            {
                var world = land.Center + new Vector3(
                    radii.x * sample.x,
                    0f,
                    radii.y * sample.y);
                Assert.Less(
                    WorldLayout.GetNormalizedCoastDistance(world, land),
                    TerrainHeightSampler.ShoreBandStart);

                float height = TerrainHeightSampler.Sample(world.x, world.z, land);
                Assert.GreaterOrEqual(
                    height,
                    WorldLayout.WaterLevel + TerrainHeightSampler.DryInteriorClearance,
                    $"'{land.Name}' has a submerged interior sample at " +
                    $"({world.x:0}, {world.z:0}).");
            }
        }
    }

    [Test]
    public void TerrainSampler_SubmergesOnlyTheOuterCoast()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            var radii = WorldLayout.GetCoastRadii(land);
            var justInside = land.Center + new Vector3(radii.x * 0.999f, 0f, 0f);
            var outerCoast = land.Center + new Vector3(radii.x, 0f, 0f);

            Assert.Greater(
                TerrainHeightSampler.Sample(justInside.x, justInside.z, land),
                WorldLayout.WaterLevel,
                $"'{land.Name}' submerges a point inside its coast.");
            Assert.Less(
                TerrainHeightSampler.Sample(outerCoast.x, outerCoast.z, land),
                WorldLayout.WaterLevel,
                $"'{land.Name}' outer coast is coplanar with the ocean.");
        }
    }

    [Test]
    public void StaticInitialisers_RunInDependencyOrder()
    {
        // Sites reference BanditCamp/CoastalRuin. C# runs static field initialisers in
        // textual order, so if those move below Sites they silently become (0,0,0).
        Assert.AreNotEqual(Vector3.zero, WorldLayout.BanditCamp);
        var camp = WorldLayout.FindSite("bandit_camp");
        Assert.IsTrue(camp.HasValue);
        Assert.AreEqual(WorldLayout.BanditCamp, camp.Value.WorldPosition);
    }
}
