using UnityEngine;

/// <summary>
/// Single source of truth for the Kessil Bay layout.
///
/// These numbers used to be hand-copied into five different files
/// (world generator, discovery/fast-travel, map art, NPC spawns, safe zone),
/// so changing the world in one place silently desynced the others.
/// Everything that needs a world coordinate now reads it from here.
/// </summary>
public static class WorldLayout
{
    // ---- Global ------------------------------------------------------------

    /// <summary>Y of the ocean plane. Land below this is underwater.</summary>
    public static readonly float WaterLevel = 2f;

    /// <summary>Y of the invisible catcher slab under the whole map.</summary>
    public static readonly float VoidCatcherY = -25f;

    /// <summary>Extent of the ocean plane (square, centred on origin).</summary>
    public static readonly float OceanSize = 8000f;

    /// <summary>The world spans ~6.8 km, so cameras need a far plane to match.</summary>
    public static readonly float CameraFarPlane = 6000f;

    /// <summary>Bounds used to project world positions onto the map UI.</summary>
    public static readonly float MapExtentPadding = 100f;
    public static readonly float MapMinX = -3200f;
    public static readonly float MapMaxX = 3200f;
    public static readonly float MapMinZ = -3650f;
    public static readonly float MapMaxZ = 3750f;

    // ---- Landmasses --------------------------------------------------------

    public enum Biome
    {
        Halbrand,
        Sarrakh,
        IslandGreen,
        IslandRock
    }

    public struct Landmass
    {
        public string Name;
        public Vector3 Center;
        /// <summary>x width, y base height, z depth (metres).</summary>
        public Vector3 Size;
        public Biome Biome;
        /// <summary>
        /// Stable, setting-neutral key for the city on this patch; null when there is none.
        /// Code branches on this, never on <see cref="CityName"/>, so renaming the setting
        /// stays a display-only change.
        /// </summary>
        public string CityId;
        /// <summary>Display name of the city. Safe to rewrite at any time.</summary>
        public string CityName;
        public int PropCount;
        /// <summary>Authored seed; unlike string.GetHashCode(), this is stable across runtimes.</summary>
        public int TerrainSeed;

        public bool HasCity => !string.IsNullOrEmpty(CityId);
    }

    /// <summary>+Z = north, +X = east. Cities sit kilometres apart on purpose.</summary>
    public static readonly Landmass[] Landmasses =
    {
        new Landmass
        {
            Name = "Halbrand_KarnothHighlands",
            Center = new Vector3(200f, 0f, 3200f),
            Size = new Vector3(2200f, 55f, 900f),
            Biome = Biome.Halbrand,
            PropCount = 180,
            TerrainSeed = 1101
        },
        new Landmass
        {
            Name = "Halbrand_KelrithCoast",
            Center = new Vector3(-400f, 0f, 2200f),
            Size = new Vector3(2000f, 28f, 800f),
            Biome = Biome.Halbrand,
            PropCount = 160,
            TerrainSeed = 1102
        },
        new Landmass
        {
            Name = "Halbrand_CaldemarPeninsula",
            Center = new Vector3(-2000f, 0f, 1600f),
            Size = new Vector3(900f, 24f, 700f),
            Biome = Biome.Halbrand,
            CityId = "city_west",
            CityName = "Caldemar",
            PropCount = 90,
            TerrainSeed = 1103
        },
        new Landmass
        {
            Name = "Halbrand_EstmereShore",
            Center = new Vector3(2200f, 0f, 1800f),
            Size = new Vector3(850f, 22f, 650f),
            Biome = Biome.Halbrand,
            CityId = "city_east",
            CityName = "Estmere",
            PropCount = 80,
            TerrainSeed = 1104
        },
        new Landmass
        {
            Name = "Sarrakh_Waste",
            Center = new Vector3(300f, 0f, -3000f),
            Size = new Vector3(2600f, 16f, 1100f),
            Biome = Biome.Sarrakh,
            PropCount = 140,
            TerrainSeed = 2201
        },
        new Landmass
        {
            Name = "Sarrakh_QadrisCoast",
            Center = new Vector3(-1600f, 0f, -2200f),
            Size = new Vector3(900f, 18f, 700f),
            Biome = Biome.Sarrakh,
            CityId = "city_south",
            CityName = "Qadris",
            PropCount = 85,
            TerrainSeed = 2202
        },
        new Landmass
        {
            Name = "Sarrakh_KilnHills",
            Center = new Vector3(2400f, 0f, -2400f),
            Size = new Vector3(900f, 60f, 1000f),
            Biome = Biome.Sarrakh,
            PropCount = 100,
            TerrainSeed = 2203
        },
        new Landmass
        {
            Name = "Island_Tolm",
            Center = new Vector3(-2800f, 0f, 200f),
            Size = new Vector3(280f, 16f, 220f),
            Biome = Biome.IslandGreen,
            PropCount = 40,
            TerrainSeed = 3301
        },
        new Landmass
        {
            Name = "Island_Corrath",
            Center = new Vector3(150f, 0f, -100f),
            Size = new Vector3(240f, 28f, 200f),
            Biome = Biome.IslandRock,
            PropCount = 28,
            TerrainSeed = 4401
        },
        new Landmass
        {
            Name = "Island_Sarn",
            Center = new Vector3(-900f, 0f, -700f),
            Size = new Vector3(200f, 14f, 160f),
            Biome = Biome.IslandRock,
            PropCount = 22,
            TerrainSeed = 4402
        }
    };

    // ---- Named points of interest ------------------------------------------
    // Declared before Sites: static field initialisers run in textual order, so
    // anything Sites references must already be assigned.

    /// <summary>Central Caldemar plaza — the player's start and respawn point.</summary>
    public static readonly Vector3 CaldemarSpawnPad = new(-2000f, 24.2f, 1450f);

    // These two used to sit at (-1750, 850) and (-2200, 700) — both in open water,
    // several hundred metres off the southern edge of the Caldemar peninsula. The
    // spawn bug hid it: everything that should have stood here was relocated to the
    // start plaza instead. They now sit on the Kelrith coast, outside the safe zone.
    public static readonly Vector3 BanditCamp = new(-1150f, 30f, 1950f);
    public static readonly Vector3 CoastalRuin = new(-1000f, 30f, 2400f);

    /// <summary>Deck height for road sections that bridge open water.</summary>
    public static readonly float CausewayDeckY = WaterLevel + 1.2f;

    /// <summary>Enemies do not aggro inside this radius of the start plaza.</summary>
    public static readonly Vector3 SafeZoneCenter = new(-2000f, 0f, 1450f);
    public static readonly float SafeZoneRadius = 400f;

    // ---- Locations (map markers, fast travel, discovery) --------------------

    public struct Site
    {
        /// <summary>
        /// Stable key written into saves, quest targets and discovery state. Deliberately
        /// setting-neutral: renaming the world must never invalidate an existing save.
        /// </summary>
        public string Id;
        /// <summary>Player-facing name. Safe to rewrite at any time.</summary>
        public string DisplayName;
        /// <summary>Centre used for discovery proximity and the map marker.</summary>
        public Vector3 WorldPosition;
        /// <summary>Where fast travel drops the player (snapped to ground at runtime).</summary>
        public Vector3 TravelPosition;
        public bool IsCity;
        /// <summary>Discovery radius in metres.</summary>
        public float DiscoverRadius;
    }

    public static readonly Site[] Sites =
    {
        new Site { Id = "city_west", DisplayName = "Caldemar", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(-2000f, 24f, 1600f), TravelPosition = new Vector3(-2000f, 25.2f, 1450f) },
        new Site { Id = "city_east", DisplayName = "Estmere", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(2200f, 22f, 1800f), TravelPosition = new Vector3(2200f, 23.2f, 1550f) },
        new Site { Id = "city_south", DisplayName = "Qadris", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(-1600f, 18f, -2200f), TravelPosition = new Vector3(-1600f, 19.2f, -1950f) },
        new Site { Id = "isle_west", DisplayName = "Tolm", IsCity = false, DiscoverRadius = 120f,
                   WorldPosition = new Vector3(-2800f, 16f, 200f), TravelPosition = new Vector3(-2800f, 17.2f, 200f) },
        new Site { Id = "isle_center", DisplayName = "Corrath", IsCity = false, DiscoverRadius = 120f,
                   WorldPosition = new Vector3(150f, 28f, -100f), TravelPosition = new Vector3(150f, 29.2f, -100f) },
        new Site { Id = "isle_south", DisplayName = "Sarn", IsCity = false, DiscoverRadius = 100f,
                   WorldPosition = new Vector3(-900f, 14f, -700f), TravelPosition = new Vector3(-900f, 15.2f, -700f) },
        new Site { Id = "bandit_camp", DisplayName = "Kelrith Bandit Camp", IsCity = false, DiscoverRadius = 90f,
                   WorldPosition = BanditCamp, TravelPosition = BanditCamp },
        new Site { Id = "coastal_ruin", DisplayName = "Coastal Ruin", IsCity = false, DiscoverRadius = 90f,
                   WorldPosition = CoastalRuin, TravelPosition = CoastalRuin }
    };

    /// <summary>
    /// Road spines as polylines. Only XZ matters — height is sampled from the terrain
    /// at build time, and sections crossing open water become causeways at
    /// <see cref="CausewayDeckY"/>.
    ///
    /// The landmasses are separate islands (the Caldemar peninsula ends at x≈-1559
    /// and the Kelrith coast starts at x≈-1380), so these routes are what actually
    /// makes the bay walkable. The old single-cube roads bridged the same gaps at a
    /// fixed y≈24, which is why one of them was visibly flying through the sky.
    /// </summary>
    public static readonly Vector3[][] Roads =
    {
        // Caldemar -> Estmere, hopping the two water gaps via the Kelrith coast.
        new[]
        {
            new Vector3(-2000f, 0f, 1450f),
            new Vector3(-1860f, 0f, 1510f),
            new Vector3(-1784f, 0f, 1600f), // Caldemar east gate
            new Vector3(-1400f, 0f, 1850f),
            new Vector3(-1200f, 0f, 1900f),
            new Vector3(0f, 0f, 2000f),
            new Vector3(900f, 0f, 1900f),
            new Vector3(1850f, 0f, 1700f),
            new Vector3(2004f, 0f, 1800f), // Estmere west gate
            new Vector3(2120f, 0f, 1680f),
            new Vector3(2200f, 0f, 1600f)
        },
        // Caldemar -> the bandit camp on the Kelrith shore.
        new[]
        {
            new Vector3(-2000f, 0f, 1450f),
            new Vector3(-1860f, 0f, 1510f),
            new Vector3(-1784f, 0f, 1600f), // Caldemar east gate
            new Vector3(-1400f, 0f, 1900f),
            BanditCamp
        },
        // Kelrith -> Karnoth, joining the two northern Halbrand regions.
        new[]
        {
            new Vector3(-200f, 0f, 2400f),
            new Vector3(0f, 0f, 2600f),
            new Vector3(100f, 0f, 2800f),
            new Vector3(200f, 0f, 3000f)
        },
        // Qadris -> Sarrakh Waste, giving the southern city an organic overland route.
        new[]
        {
            new Vector3(-1500f, 0f, -2100f),
            new Vector3(-1394f, 0f, -2200f), // Qadris east gate
            new Vector3(-1200f, 0f, -2500f),
            new Vector3(-1000f, 0f, -2700f),
            new Vector3(-850f, 0f, -2850f)
        },
        // Sarrakh Waste -> Kiln Hills.
        new[]
        {
            new Vector3(1350f, 0f, -2850f),
            new Vector3(1600f, 0f, -2750f),
            new Vector3(1850f, 0f, -2650f),
            new Vector3(2050f, 0f, -2600f)
        }
    };

    // ---- Helpers -----------------------------------------------------------

    public static bool IsInSafeZone(Vector3 pos)
    {
        float dx = pos.x - SafeZoneCenter.x;
        float dz = pos.z - SafeZoneCenter.z;
        return dx * dx + dz * dz <= SafeZoneRadius * SafeZoneRadius;
    }

    public static Site? FindSite(string id)
    {
        foreach (var s in Sites)
            if (s.Id == id) return s;
        return null;
    }

    /// <summary>
    /// The terrain mesh of a patch covers Center ± Size * this. Kept here so authoring
    /// checks and the generator agree on where a landmass actually ends.
    /// </summary>
    public static readonly float TerrainHalfExtent = 0.49f;

    static WorldLayout()
    {
        var document = WorldLayoutData.LoadRequired();
        WaterLevel = document.WaterLevel;
        VoidCatcherY = document.VoidCatcherY;
        OceanSize = document.OceanSize;
        CameraFarPlane = document.CameraFarPlane;
        MapExtentPadding = document.MapExtentPadding;
        MapMinX = document.MapMinX;
        MapMaxX = document.MapMaxX;
        MapMinZ = document.MapMinZ;
        MapMaxZ = document.MapMaxZ;
        CausewayDeckY = document.CausewayDeckY;
        SafeZoneRadius = document.SafeZoneRadius;
        TerrainHalfExtent = document.TerrainHalfExtent;
        CaldemarSpawnPad = document.CaldemarSpawnPad;
        BanditCamp = document.BanditCamp;
        CoastalRuin = document.CoastalRuin;
        SafeZoneCenter = document.SafeZoneCenter;
        Landmasses = WorldLayoutData.BuildLandmasses(document);
        Sites = WorldLayoutData.BuildSites(document);
        Roads = WorldLayoutData.BuildRoads(document);
    }

    /// <summary>
    /// Semi-axes of the shared elliptical coast. Terrain, authoring checks and map art
    /// must all use these radii rather than independently interpreting <see cref="Landmass.Size"/>.
    /// </summary>
    public static Vector2 GetCoastRadii(Landmass landmass)
    {
        return new Vector2(
            landmass.Size.x * TerrainHalfExtent,
            landmass.Size.z * TerrainHalfExtent);
    }

    /// <summary>
    /// Elliptical distance on the XZ plane: zero at the centre, one on the coast and
    /// greater than one outside it. The Y coordinate is deliberately ignored.
    /// </summary>
    public static float GetNormalizedCoastDistance(Vector3 pos, Landmass landmass)
    {
        var radii = GetCoastRadii(landmass);
        if (radii.x <= Mathf.Epsilon || radii.y <= Mathf.Epsilon)
            return float.PositiveInfinity;

        float dx = (pos.x - landmass.Center.x) / radii.x;
        float dz = (pos.z - landmass.Center.z) / radii.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>True when an XZ position is on or inside a landmass's shared coast.</summary>
    public static bool IsInsideCoast(Vector3 pos, Landmass landmass)
    {
        return GetNormalizedCoastDistance(pos, landmass) <= 1f;
    }

    /// <summary>
    /// Which landmass (if any) covers this XZ position.
    ///
    /// Worth calling before authoring any fixed world position: the bandit camp and the
    /// coastal ruin were both placed in open water and nobody noticed, because a
    /// separate bug was relocating everything to the start plaza anyway.
    /// </summary>
    public static bool TryGetLandmassAt(Vector3 pos, out Landmass found)
    {
        foreach (var land in Landmasses)
        {
            if (IsInsideCoast(pos, land))
            {
                found = land;
                return true;
            }
        }

        found = default;
        return false;
    }

    public static bool IsOverLand(Vector3 pos) => TryGetLandmassAt(pos, out _);

    /// <summary>Normalised map-UI position for a world point.</summary>
    public static Vector2 WorldToMapUV(Vector3 world)
    {
        return new Vector2(
            Mathf.Clamp01(Mathf.InverseLerp(MapMinX, MapMaxX, world.x)),
            Mathf.Clamp01(Mathf.InverseLerp(MapMinZ, MapMaxZ, world.z)));
    }
}
