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
    public void EverySite_HasUniqueId()
    {
        var seen = new HashSet<string>();
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsFalse(string.IsNullOrEmpty(site.Id), "A site has an empty id.");
            Assert.IsTrue(seen.Add(site.Id), $"Duplicate site id '{site.Id}'.");
        }
    }

    [Test]
    public void EverySite_TravelPositionIsOverLand()
    {
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsTrue(WorldLayout.IsOverLand(site.TravelPosition),
                $"Fast travel to '{site.Id}' would drop the player in open water at " +
                $"({site.TravelPosition.x:0}, {site.TravelPosition.z:0}).");
        }
    }

    [Test]
    public void EverySite_WorldPositionIsOverLand()
    {
        foreach (var site in WorldLayout.Sites)
        {
            Assert.IsTrue(WorldLayout.IsOverLand(site.WorldPosition),
                $"Site '{site.Id}' is centred on open water at " +
                $"({site.WorldPosition.x:0}, {site.WorldPosition.z:0}).");
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
                if (site.IsCity && site.DisplayName == land.CityName) { hasSite = true; break; }
            }

            Assert.IsTrue(hasSite,
                $"Landmass '{land.Name}' builds the city '{land.CityName}' but no map site exists for it.");
        }
    }

    [Test]
    public void HostilePois_AreOutsideTheSafeZone()
    {
        // Enemies do not aggro inside the safe zone, so a camp placed there is inert.
        foreach (var pos in new[] { WorldLayout.BanditCamp, WorldLayout.CoastalRuin })
        {
            Assert.IsFalse(WorldLayout.IsInSafeZone(pos),
                $"Hostile POI at ({pos.x:0}, {pos.z:0}) is inside the Daggerfall safe zone, " +
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
    public void SpawnPad_IsOnTheDaggerfallLandmass()
    {
        Assert.IsTrue(WorldLayout.TryGetLandmassAt(WorldLayout.DaggerfallSpawnPad, out var land),
            "The player spawn pad is not over any landmass.");
        Assert.AreEqual("Daggerfall", land.CityName,
            $"The spawn pad landed on '{land.Name}' instead of the Daggerfall peninsula.");
    }

    [Test]
    public void MapBounds_ContainEveryLandmass()
    {
        foreach (var land in WorldLayout.Landmasses)
        {
            var uv = WorldLayout.WorldToMapUV(land.Center);
            Assert.Greater(uv.x, 0f, $"'{land.Name}' is clamped to the west edge of the map.");
            Assert.Less(uv.x, 1f, $"'{land.Name}' is clamped to the east edge of the map.");
            Assert.Greater(uv.y, 0f, $"'{land.Name}' is clamped to the south edge of the map.");
            Assert.Less(uv.y, 1f, $"'{land.Name}' is clamped to the north edge of the map.");
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
