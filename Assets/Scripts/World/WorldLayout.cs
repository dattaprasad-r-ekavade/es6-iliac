using UnityEngine;

/// <summary>
/// Single source of truth for the Iliac Bay layout.
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
    public const float WaterLevel = 2f;

    /// <summary>Y of the invisible catcher slab under the whole map.</summary>
    public const float VoidCatcherY = -25f;

    /// <summary>Extent of the ocean plane (square, centred on origin).</summary>
    public const float OceanSize = 8000f;

    /// <summary>The world spans ~6.8 km, so cameras need a far plane to match.</summary>
    public const float CameraFarPlane = 6000f;

    /// <summary>Bounds used to project world positions onto the map UI.</summary>
    public const float MapMinX = -3200f;
    public const float MapMaxX = 3200f;
    public const float MapMinZ = -3400f;
    public const float MapMaxZ = 3600f;

    // ---- Landmasses --------------------------------------------------------

    public enum Biome
    {
        HighRock,
        Hammerfell,
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
        public string CityName;   // null when the patch has no city
        public int PropCount;

        public bool HasCity => !string.IsNullOrEmpty(CityName);
    }

    /// <summary>+Z = north, +X = east. Cities sit kilometres apart on purpose.</summary>
    public static readonly Landmass[] Landmasses =
    {
        new Landmass
        {
            Name = "HighRock_WrothgarianFoothills",
            Center = new Vector3(200f, 0f, 3200f),
            Size = new Vector3(2200f, 55f, 900f),
            Biome = Biome.HighRock,
            PropCount = 180
        },
        new Landmass
        {
            Name = "HighRock_GlenumbraCoast",
            Center = new Vector3(-400f, 0f, 2200f),
            Size = new Vector3(2000f, 28f, 800f),
            Biome = Biome.HighRock,
            PropCount = 160
        },
        new Landmass
        {
            Name = "HighRock_DaggerfallPeninsula",
            Center = new Vector3(-2000f, 0f, 1600f),
            Size = new Vector3(900f, 24f, 700f),
            Biome = Biome.HighRock,
            CityName = "Daggerfall",
            PropCount = 90
        },
        new Landmass
        {
            Name = "HighRock_WayrestShore",
            Center = new Vector3(2200f, 0f, 1800f),
            Size = new Vector3(850f, 22f, 650f),
            Biome = Biome.HighRock,
            CityName = "Wayrest",
            PropCount = 80
        },
        new Landmass
        {
            Name = "Hammerfell_AlikrDesert",
            Center = new Vector3(300f, 0f, -3000f),
            Size = new Vector3(2600f, 16f, 1100f),
            Biome = Biome.Hammerfell,
            PropCount = 140
        },
        new Landmass
        {
            Name = "Hammerfell_SentinelCoast",
            Center = new Vector3(-1600f, 0f, -2200f),
            Size = new Vector3(900f, 18f, 700f),
            Biome = Biome.Hammerfell,
            CityName = "Sentinel",
            PropCount = 85
        },
        new Landmass
        {
            Name = "Hammerfell_DragontailFoothills",
            Center = new Vector3(2400f, 0f, -2400f),
            Size = new Vector3(900f, 60f, 1000f),
            Biome = Biome.Hammerfell,
            PropCount = 100
        },
        new Landmass
        {
            Name = "Island_Betony",
            Center = new Vector3(-2800f, 0f, 200f),
            Size = new Vector3(280f, 16f, 220f),
            Biome = Biome.IslandGreen,
            PropCount = 40
        },
        new Landmass
        {
            Name = "Island_Balfiera",
            Center = new Vector3(150f, 0f, -100f),
            Size = new Vector3(240f, 28f, 200f),
            Biome = Biome.IslandRock,
            PropCount = 28
        },
        new Landmass
        {
            Name = "Island_Cybiades",
            Center = new Vector3(-900f, 0f, -700f),
            Size = new Vector3(200f, 14f, 160f),
            Biome = Biome.IslandRock,
            PropCount = 22
        }
    };

    // ---- Named points of interest ------------------------------------------
    // Declared before Sites: static field initialisers run in textual order, so
    // anything Sites references must already be assigned.

    /// <summary>Central Daggerfall plaza — the player's start and respawn point.</summary>
    public static readonly Vector3 DaggerfallSpawnPad = new(-2000f, 24.2f, 1450f);

    // These two used to sit at (-1750, 850) and (-2200, 700) — both in open water,
    // several hundred metres off the southern edge of the Daggerfall peninsula. The
    // spawn bug hid it: everything that should have stood here was relocated to the
    // start plaza instead. They now sit on the Glenumbra coast, outside the safe zone.
    public static readonly Vector3 BanditCamp = new(-1150f, 30f, 1950f);
    public static readonly Vector3 CoastalRuin = new(-1000f, 30f, 2400f);

    /// <summary>Deck height for road sections that bridge open water.</summary>
    public const float CausewayDeckY = WaterLevel + 1.2f;

    /// <summary>Enemies do not aggro inside this radius of the start plaza.</summary>
    public static readonly Vector3 SafeZoneCenter = new(-2000f, 0f, 1450f);
    public const float SafeZoneRadius = 400f;

    // ---- Locations (map markers, fast travel, discovery) --------------------

    public struct Site
    {
        public string Id;
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
        new Site { Id = "daggerfall", DisplayName = "Daggerfall", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(-2000f, 24f, 1600f), TravelPosition = new Vector3(-2000f, 25.2f, 1450f) },
        new Site { Id = "wayrest", DisplayName = "Wayrest", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(2200f, 22f, 1800f), TravelPosition = new Vector3(2200f, 23.2f, 1550f) },
        new Site { Id = "sentinel", DisplayName = "Sentinel", IsCity = true, DiscoverRadius = 280f,
                   WorldPosition = new Vector3(-1600f, 18f, -2200f), TravelPosition = new Vector3(-1600f, 19.2f, -1950f) },
        new Site { Id = "betony", DisplayName = "Betony", IsCity = false, DiscoverRadius = 120f,
                   WorldPosition = new Vector3(-2800f, 16f, 200f), TravelPosition = new Vector3(-2800f, 17.2f, 200f) },
        new Site { Id = "balfiera", DisplayName = "Balfiera", IsCity = false, DiscoverRadius = 120f,
                   WorldPosition = new Vector3(150f, 28f, -100f), TravelPosition = new Vector3(150f, 29.2f, -100f) },
        new Site { Id = "cybiades", DisplayName = "Cybiades", IsCity = false, DiscoverRadius = 100f,
                   WorldPosition = new Vector3(-900f, 14f, -700f), TravelPosition = new Vector3(-900f, 15.2f, -700f) },
        new Site { Id = "bandit_camp", DisplayName = "Glenumbra Bandit Camp", IsCity = false, DiscoverRadius = 90f,
                   WorldPosition = BanditCamp, TravelPosition = BanditCamp },
        new Site { Id = "coastal_ruin", DisplayName = "Coastal Ruin", IsCity = false, DiscoverRadius = 90f,
                   WorldPosition = CoastalRuin, TravelPosition = CoastalRuin }
    };

    /// <summary>
    /// Road spines as polylines. Only XZ matters — height is sampled from the terrain
    /// at build time, and sections crossing open water become causeways at
    /// <see cref="CausewayDeckY"/>.
    ///
    /// The landmasses are separate islands (the Daggerfall peninsula ends at x≈-1559
    /// and the Glenumbra coast starts at x≈-1380), so these routes are what actually
    /// makes the bay walkable. The old single-cube roads bridged the same gaps at a
    /// fixed y≈24, which is why one of them was visibly flying through the sky.
    /// </summary>
    public static readonly Vector3[][] Roads =
    {
        // Daggerfall -> Wayrest, hopping the two water gaps via the Glenumbra coast.
        new[]
        {
            new Vector3(-2000f, 0f, 1450f),
            new Vector3(-1650f, 0f, 1750f),
            new Vector3(-1200f, 0f, 1900f),
            new Vector3(0f, 0f, 2000f),
            new Vector3(900f, 0f, 1900f),
            new Vector3(1850f, 0f, 1700f),
            new Vector3(2200f, 0f, 1600f)
        },
        // Daggerfall -> the bandit camp on the Glenumbra shore.
        new[]
        {
            new Vector3(-2000f, 0f, 1450f),
            new Vector3(-1700f, 0f, 1800f),
            new Vector3(-1400f, 0f, 1900f),
            BanditCamp
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
    public const float TerrainHalfExtent = 0.49f;

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
            if (Mathf.Abs(pos.x - land.Center.x) <= land.Size.x * TerrainHalfExtent &&
                Mathf.Abs(pos.z - land.Center.z) <= land.Size.z * TerrainHalfExtent)
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
