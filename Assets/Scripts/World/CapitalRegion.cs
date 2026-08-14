using System;
using UnityEngine;

/// <summary>
/// The Estmere region: one 2.4 km square plane bounded by open sea, holding a walled city and
/// the Chapter 01 story locations.
///
/// Dimensions are locked in plan.md § World architecture and derive from a 7–8 minute walk
/// across the city at 3.5 m/s. **Do not re-derive them.**
///
/// This is deliberately separate from <see cref="WorldLayout"/>, which describes the retired
/// 6.8 km continuous bay. The bay stays working — and stays under test — until the region
/// replaces it outright.
///
/// The look is Arena's: blocky, flat-topped, grid-streeted, and generated rather than
/// authored. What makes it read as a style rather than as greybox is the render layer, so
/// everything here is palette-locked through <see cref="ArtDirection"/>.
/// </summary>
public static class CapitalRegion
{
    // --- extents -------------------------------------------------------------

    /// <summary>Region is a 2.4 km square centred on the origin.</summary>
    public const float RegionSize = 2400f;
    public const float RegionHalf = RegionSize * 0.5f;

    /// <summary>Beyond this the plane is open water; the bound is a turn-back, not a wall.</summary>
    public const float LandHalfExtent = 1000f;

    /// <summary>
    /// City core is 1.6 km across, which is the locked 7–8 minute walk at 3.5 m/s
    /// (1600 / 3.5 / 60 = 7.6 min). An earlier draft said 1.2 km; that figure came from the
    /// 3 m/s row of the speed table and crosses in 5.7 minutes, missing the metric.
    /// </summary>
    public const float CitySize = 1600f;
    public const float CityHalf = CitySize * 0.5f;

    public const float WaterLevel = 2f;
    public const float GroundHeight = 8f;

    /// <summary>The city sits shoreward, so the harbour edge meets open water.</summary>
    public static readonly Vector3 CityCenter = new(0f, GroundHeight, -80f);

    // --- story anchors -------------------------------------------------------

    /// <summary>
    /// A place in the region the player can walk to. Interiors remain their own scenes — the
    /// Morrowind cell model — and <see cref="SceneName"/> is what the door there loads.
    /// </summary>
    [Serializable]
    public struct Anchor
    {
        public string Id;
        public string DisplayName;
        /// <summary>Interior scene this door leads to, or empty for an exterior-only landmark.</summary>
        public string SceneName;
        public string SpawnId;
        public Vector3 Position;
        /// <summary>Facing of the doorway, so the building is placed with its front outward.</summary>
        public float FacingDegrees;
        public float Footprint;

        /// <summary>
        /// Height of the exterior shell. Every anchor used to be an 18 m cube regardless of
        /// what it was, which made the docks a tower block on a quayside and gave the skyline
        /// nothing to navigate by. Silhouette is the only landmark cue this art direction has.
        /// </summary>
        public float Height;

        /// <summary>
        /// A working waterfront: a low deck with piers running out from it, rather than a
        /// building. Set for the docks and the harbour, which sit on the shore and should read
        /// as open ground the player walks onto.
        /// </summary>
        public bool IsQuay;
    }

    /// <summary>
    /// Ids are setting-neutral and save-persisted, per the naming policy. Display names are
    /// display only; never branch on them.
    /// </summary>
    public static readonly Anchor[] Anchors =
    {
        new() { Id = "anchor.palace", DisplayName = "The Palace", SceneName = "Palace",
                SpawnId = "spawn.entry", Position = new Vector3(0f, GroundHeight, 300f),
                FacingDegrees = 180f, Footprint = 90f, Height = 30f },

        new() { Id = "anchor.prison", DisplayName = "The Prison", SceneName = "Prison",
                SpawnId = "spawn.entry", Position = new Vector3(-260f, GroundHeight, 220f),
                FacingDegrees = 135f, Footprint = 70f, Height = 12f },

        new() { Id = "anchor.tower", DisplayName = "The Secured Tower", SceneName = "Secured_Tower",
                SpawnId = "spawn.entry", Position = new Vector3(-380f, GroundHeight, 180f),
                FacingDegrees = 90f, Footprint = 40f, Height = 44f },

        new() { Id = "anchor.arcanum", DisplayName = "The Siddha Order", SceneName = "Order_Hall",
                SpawnId = "spawn.entry", Position = new Vector3(300f, GroundHeight, 160f),
                FacingDegrees = 225f, Footprint = 60f, Height = 22f },

        new() { Id = "anchor.guardyard", DisplayName = "The Guard Yard", SceneName = "Tutorial_Warrior",
                SpawnId = "spawn.entry", Position = new Vector3(-180f, GroundHeight, -180f),
                FacingDegrees = 45f, Footprint = 55f, Height = 7f },

        new() { Id = "anchor.docks", DisplayName = "The Survivor Docks", SceneName = "Docks",
                SpawnId = "spawn.entry", Position = new Vector3(140f, GroundHeight, -960f),
                FacingDegrees = 0f, Footprint = 60f, Height = 4f, IsQuay = true },

        new() { Id = "anchor.harbor", DisplayName = "The Harbour", SceneName = "Harbor",
                SpawnId = "spawn.entry", Position = new Vector3(-140f, GroundHeight, -960f),
                FacingDegrees = 0f, Footprint = 60f, Height = 4f, IsQuay = true },

        // Outside the walls, along the coast — the escape surfaces away from the city.
        new() { Id = "anchor.seacave", DisplayName = "The Sea Cave", SceneName = "Sea_Cave",
                SpawnId = "spawn.escape", Position = new Vector3(-900f, GroundHeight, -700f),
                FacingDegrees = 315f, Footprint = 45f, Height = 9f }
    };

    /// <summary>
    /// Where a new arrival starts: on the quay, because Chapter 01 begins with a sea rescue.
    /// Just inland of the docks, facing the south gate — so the first walk is up off the
    /// waterfront and into the city, which is the shape of the opening.
    /// </summary>
    public static readonly Vector3 PlayerSpawn = new(140f, GroundHeight, -915f);

    // --- city walls ----------------------------------------------------------

    /// <summary>Cardinal gate openings in the wall, as offsets from the city centre.</summary>
    public static readonly Vector3[] Gates =
    {
        new(0f, GroundHeight, CityHalf),    // north, toward the palace approach
        new(0f, GroundHeight, -CityHalf),   // south, toward the harbour
        new(CityHalf, GroundHeight, 0f),    // east
        new(-CityHalf, GroundHeight, 0f)    // west, toward the coast road
    };

    public const float GateWidth = 24f;
    public const float WallHeight = 14f;
    public const float WallThickness = 4f;

    // --- queries -------------------------------------------------------------

    public static bool IsInsideRegion(Vector3 position) =>
        Mathf.Abs(position.x) <= RegionHalf && Mathf.Abs(position.z) <= RegionHalf;

    /// <summary>Dry land, as opposed to the sea margin that bounds the plane.</summary>
    public static bool IsOverLand(Vector3 position) =>
        Mathf.Abs(position.x) <= LandHalfExtent && Mathf.Abs(position.z) <= LandHalfExtent;

    public static bool IsInsideCity(Vector3 position)
    {
        var local = position - CityCenter;
        return Mathf.Abs(local.x) <= CityHalf && Mathf.Abs(local.z) <= CityHalf;
    }

    public static Anchor? FindAnchor(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var anchor in Anchors)
            if (anchor.Id == id) return anchor;
        return null;
    }

    /// <summary>Ground height at a point. Flat inland, falling to the sea at the margin.</summary>
    public static float SampleHeight(Vector3 position)
    {
        float edge = Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.z));
        if (edge <= LandHalfExtent) return GroundHeight;

        // Beach slope out to the region bound, then open water.
        float t = Mathf.InverseLerp(LandHalfExtent, RegionHalf, edge);
        return Mathf.Lerp(GroundHeight, WaterLevel - 6f, t);
    }
}
