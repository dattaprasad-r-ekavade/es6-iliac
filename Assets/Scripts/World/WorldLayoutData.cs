using System;
using UnityEngine;

[Serializable]
public sealed class WorldLayoutDocument
{
    public int Version;
    public float WaterLevel, VoidCatcherY, OceanSize, CameraFarPlane;
    public float MapExtentPadding, MapMinX, MapMaxX, MapMinZ, MapMaxZ;
    public float CausewayDeckY, SafeZoneRadius, TerrainHalfExtent;
    public Vector3 CaldemarSpawnPad, BanditCamp, CoastalRuin, SafeZoneCenter;
    public WorldLandmassRecord[] Landmasses;
    public WorldSiteRecord[] Sites;
    public WorldRoadRecord[] Roads;
}

[Serializable]
public sealed class WorldLandmassRecord
{
    public string Name, Biome, CityId, CityName;
    public Vector3 Center, Size;
    public int PropCount, TerrainSeed;
}

[Serializable]
public sealed class WorldSiteRecord
{
    public string Id, DisplayName;
    public Vector3 WorldPosition, TravelPosition;
    public bool IsCity;
    public float DiscoverRadius;
}

[Serializable] public sealed class WorldRoadRecord { public Vector3[] Points; }

/// <summary>Strict loader for the versioned source consumed by WorldLayout and the future editor.</summary>
public static class WorldLayoutData
{
    public const int CurrentVersion = 1;
    public const string ResourcePath = "Data/World/kessil.world";

    public static WorldLayoutDocument LoadRequired()
    {
        var source = Resources.Load<TextAsset>(ResourcePath);
        if (source == null) throw new MissingReferenceException($"Missing Resources/{ResourcePath}.json");
        var document = JsonUtility.FromJson<WorldLayoutDocument>(source.text);
        if (document == null || document.Version != CurrentVersion)
            throw new InvalidOperationException($"World data version must be {CurrentVersion}.");
        if (document.Landmasses == null || document.Landmasses.Length == 0
            || document.Sites == null || document.Sites.Length == 0
            || document.Roads == null || document.Roads.Length == 0)
            throw new InvalidOperationException("World data requires landmasses, sites and roads.");
        return document;
    }

    public static WorldLayout.Landmass[] BuildLandmasses(WorldLayoutDocument document)
    {
        var values = new WorldLayout.Landmass[document.Landmasses.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var source = document.Landmasses[i];
            if (!Enum.TryParse(source.Biome, out WorldLayout.Biome biome))
                throw new InvalidOperationException($"Unknown biome '{source.Biome}' on '{source.Name}'.");
            values[i] = new WorldLayout.Landmass
            {
                Name = source.Name, Center = source.Center, Size = source.Size, Biome = biome,
                CityId = source.CityId, CityName = source.CityName,
                PropCount = source.PropCount, TerrainSeed = source.TerrainSeed
            };
        }
        return values;
    }

    public static WorldLayout.Site[] BuildSites(WorldLayoutDocument document)
    {
        var values = new WorldLayout.Site[document.Sites.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var source = document.Sites[i];
            values[i] = new WorldLayout.Site
            {
                Id = source.Id, DisplayName = source.DisplayName,
                WorldPosition = source.WorldPosition, TravelPosition = source.TravelPosition,
                IsCity = source.IsCity, DiscoverRadius = source.DiscoverRadius
            };
        }
        return values;
    }

    public static Vector3[][] BuildRoads(WorldLayoutDocument document)
    {
        var values = new Vector3[document.Roads.Length][];
        for (int i = 0; i < values.Length; i++)
            values[i] = document.Roads[i].Points ?? Array.Empty<Vector3>();
        return values;
    }
}
