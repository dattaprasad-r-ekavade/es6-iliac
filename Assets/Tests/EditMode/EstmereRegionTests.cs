using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The Estmere region's data contract.
///
/// This is the geometry every later authored space sits on, so the assertions here are the
/// ones that would be expensive to discover late: story anchors that overlap, doors that lead
/// nowhere, ids that embed display names, and a city that does not fit inside its own region.
/// </summary>
public sealed class EstmereRegionTests
{
    [Test]
    public void RegionMatchesTheLockedDimensions()
    {
        // plan.md § World architecture: 2.4 km square, 1.6 km city, from a 7–8 min walk at
        // 3.5 m/s. If these change, the walk metric changed and that was a design decision.
        Assert.AreEqual(2400f, EstmereRegion.RegionSize, 0.01f);
        Assert.AreEqual(1600f, EstmereRegion.CitySize, 0.01f);
    }

    [Test]
    public void CityFitsInsideTheLandmass_WhichFitsInsideTheRegion()
    {
        Assert.Less(EstmereRegion.CityHalf, EstmereRegion.LandHalfExtent,
            "The city spills past the shoreline.");
        Assert.Less(EstmereRegion.LandHalfExtent, EstmereRegion.RegionHalf,
            "There is no sea margin — the plane has no bound to turn back from.");
    }

    [Test]
    public void CrossingTheCityTakesRoughlyTheLockedWalk()
    {
        const float walkSpeed = 3.5f;
        float minutes = EstmereRegion.CitySize / walkSpeed / 60f;

        // Deliberately tight. A looser range let a 5.7-minute city pass as "7-8".
        Assert.That(minutes, Is.InRange(7f, 8f),
            $"Crossing the city takes {minutes:0.0} min at {walkSpeed} m/s; the locked metric is 7–8.");
    }

    [Test]
    public void EveryAnchorHasAStableSettingNeutralId()
    {
        foreach (var anchor in EstmereRegion.Anchors)
        {
            Assert.IsNotEmpty(anchor.Id, "An anchor has no id.");
            StringAssert.StartsWith("anchor.", anchor.Id, $"'{anchor.Id}' does not follow the id convention.");

            // The naming policy: save-persisted ids never embed a display name.
            Assert.IsFalse(anchor.Id.ToLowerInvariant().Contains("estmere"),
                $"'{anchor.Id}' embeds a setting name and would not survive a rename.");
        }
    }

    [Test]
    public void AnchorIdsAreUnique()
    {
        var ids = EstmereRegion.Anchors.Select(a => a.Id).ToArray();
        CollectionAssert.AllItemsAreUnique(ids, "Two anchors share an id; one would shadow the other.");
    }

    [Test]
    public void EveryAnchorIsOnDryLand()
    {
        foreach (var anchor in EstmereRegion.Anchors)
            Assert.IsTrue(
                EstmereRegion.IsOverLand(anchor.Position),
                $"{anchor.Id} sits in open water at {anchor.Position}.");
    }

    /// <summary>
    /// Overlapping anchors would generate buildings inside one another. Cheap to assert now,
    /// miserable to diagnose once the generator is placing geometry.
    /// </summary>
    [Test]
    public void AnchorsDoNotOverlap()
    {
        var anchors = EstmereRegion.Anchors;
        for (int i = 0; i < anchors.Length; i++)
        {
            for (int j = i + 1; j < anchors.Length; j++)
            {
                float required = (anchors[i].Footprint + anchors[j].Footprint) * 0.5f;
                float actual = Vector3.Distance(anchors[i].Position, anchors[j].Position);
                Assert.Greater(actual, required,
                    $"{anchors[i].Id} and {anchors[j].Id} overlap: {actual:0} m apart, {required:0} m needed.");
            }
        }
    }

    /// <summary>
    /// Every door must lead to a scene that exists, or the player walks into a load failure.
    /// The catalog is the authority on which interiors are real.
    /// </summary>
    [Test]
    public void EveryAnchorSceneExistsInTheGreyThreadCatalog()
    {
        var known = new HashSet<string>(GreyThreadSceneCatalog.Scenes.Select(s => s.Name));

        foreach (var anchor in EstmereRegion.Anchors)
        {
            if (string.IsNullOrEmpty(anchor.SceneName)) continue;
            Assert.IsTrue(
                known.Contains(anchor.SceneName),
                $"{anchor.Id} opens onto '{anchor.SceneName}', which no generated scene provides.");
        }
    }

    [Test]
    public void EveryAnchorWithASceneNamesASpawn()
    {
        foreach (var anchor in EstmereRegion.Anchors)
        {
            if (string.IsNullOrEmpty(anchor.SceneName)) continue;
            Assert.IsNotEmpty(anchor.SpawnId,
                $"{anchor.Id} loads a scene but names no spawn, so the player would arrive at the origin.");
        }
    }

    [Test]
    public void ThePlayerSpawnsOnLand_OutsideAnyBuilding()
    {
        Assert.IsTrue(EstmereRegion.IsOverLand(EstmereRegion.PlayerSpawn), "The player spawns in the sea.");

        foreach (var anchor in EstmereRegion.Anchors)
            Assert.Greater(
                Vector3.Distance(EstmereRegion.PlayerSpawn, anchor.Position), anchor.Footprint * 0.5f,
                $"The player spawns inside {anchor.Id}.");
    }

    [Test]
    public void GatesSitOnTheCityWall()
    {
        foreach (var gate in EstmereRegion.Gates)
        {
            float x = Mathf.Abs(gate.x);
            float z = Mathf.Abs(gate.z);
            Assert.IsTrue(
                Mathf.Approximately(x, EstmereRegion.CityHalf) || Mathf.Approximately(z, EstmereRegion.CityHalf),
                $"Gate at {gate} is not on the wall line.");
        }
    }

    [Test]
    public void HeightFallsToTheSeaAtTheMargin()
    {
        Assert.AreEqual(
            EstmereRegion.GroundHeight, EstmereRegion.SampleHeight(Vector3.zero), 0.01f,
            "Inland ground is not at the authored height.");

        float atBound = EstmereRegion.SampleHeight(new Vector3(EstmereRegion.RegionHalf, 0f, 0f));
        Assert.Less(atBound, EstmereRegion.WaterLevel,
            "The region edge is above water, so the sea bound would read as a cliff.");
    }

    [Test]
    public void TheHarbourAndDocksReachTheWater()
    {
        // Both are waterfront by design — B060 arrives by ship and B400 teaches sailing.
        foreach (var id in new[] { "anchor.docks", "anchor.harbor" })
        {
            var anchor = EstmereRegion.FindAnchor(id);
            Assert.IsNotNull(anchor, $"{id} is missing.");
            Assert.Less(
                EstmereRegion.LandHalfExtent - Mathf.Abs(anchor.Value.Position.z), 350f,
                $"{id} is inland; boats cannot reach it.");
        }
    }

    [Test]
    public void TheSeaCaveIsOutsideTheCityWalls()
    {
        var cave = EstmereRegion.FindAnchor("anchor.seacave");
        Assert.IsNotNull(cave);
        Assert.IsFalse(
            EstmereRegion.IsInsideCity(cave.Value.Position),
            "The escape surfaces inside the city the player just escaped.");
    }
}
