using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rebuildable module data for the first Arena Miniature environment slice.
///
/// This deliberately contains no UnityEditor code. The current scene builders materialise these
/// records as primitive geometry; W11 can later serialise the same records from the external map
/// editor without inventing a second vocabulary for facades and dungeon dressing.
/// </summary>
public static class ArenaMiniatureSliceLayout
{
    public const string StreetRootName = "ArenaMiniature_StreetSlice";
    public const string DungeonRootName = "ArenaMiniature_DungeonSlice";

    /// <summary>The dock-to-city street selected as the exterior art target.</summary>
    public static readonly Vector3 StreetOrigin = new(140f, CapitalRegion.GroundHeight, -610f);

    public const float StreetLength = 196f;
    public const float StreetClearHalfWidth = 8f;
    public const float StreetReservedHalfWidth = 46f;

    [Flags]
    public enum FacadeFeature
    {
        None = 0,
        Arcade = 1 << 0,
        Awning = 1 << 1,
        Balcony = 1 << 2,
        Pavilion = 1 << 3
    }

    [Serializable]
    public readonly struct FacadeSpec
    {
        public readonly string Id;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 Size;
        public readonly float FacingDegrees;
        public readonly FacadeFeature Features;

        public FacadeSpec(string id, Vector3 localPosition, Vector3 size,
            float facingDegrees, FacadeFeature features)
        {
            Id = id;
            LocalPosition = localPosition;
            Size = size;
            FacingDegrees = facingDegrees;
            Features = features;
        }
    }

    public enum StreetPropKind
    {
        MarketStall,
        Banner,
        ShadeTree,
        WaterBasin,
        CivicArch
    }

    [Serializable]
    public readonly struct StreetPropSpec
    {
        public readonly string Id;
        public readonly StreetPropKind Kind;
        public readonly Vector3 LocalPosition;
        public readonly float FacingDegrees;

        public StreetPropSpec(string id, StreetPropKind kind,
            Vector3 localPosition, float facingDegrees = 0f)
        {
            Id = id;
            Kind = kind;
            LocalPosition = localPosition;
            FacingDegrees = facingDegrees;
        }
    }

    // Local +Z is north toward the city. Facades face inward and leave a continuous 16 m lane.
    // The differing roofs and projections give the street a readable, non-random silhouette.
    private static readonly FacadeSpec[] AuthoredFacades =
    {
        new("facade.west.south", new Vector3(-31f, 0f, -72f), new Vector3(38f, 12f, 30f),
            90f, FacadeFeature.Arcade | FacadeFeature.Awning),
        new("facade.east.south", new Vector3(31f, 0f, -72f), new Vector3(38f, 16f, 30f),
            270f, FacadeFeature.Balcony),
        new("facade.west.market", new Vector3(-31f, 0f, -24f), new Vector3(38f, 17f, 30f),
            90f, FacadeFeature.Balcony | FacadeFeature.Pavilion),
        new("facade.east.market", new Vector3(31f, 0f, -24f), new Vector3(38f, 13f, 30f),
            270f, FacadeFeature.Arcade | FacadeFeature.Awning),
        new("facade.west.north", new Vector3(-31f, 0f, 24f), new Vector3(38f, 14f, 30f),
            90f, FacadeFeature.Arcade),
        new("facade.east.north", new Vector3(31f, 0f, 24f), new Vector3(38f, 18f, 30f),
            270f, FacadeFeature.Balcony | FacadeFeature.Pavilion),
        new("facade.west.gate", new Vector3(-31f, 0f, 72f), new Vector3(38f, 19f, 30f),
            90f, FacadeFeature.Balcony | FacadeFeature.Pavilion),
        new("facade.east.gate", new Vector3(31f, 0f, 72f), new Vector3(38f, 15f, 30f),
            270f, FacadeFeature.Arcade | FacadeFeature.Awning)
    };

    private static readonly StreetPropSpec[] AuthoredStreetProps =
    {
        new("prop.stall.spice", StreetPropKind.MarketStall, new Vector3(-13.5f, 0f, -47f), 90f),
        new("prop.stall.cloth", StreetPropKind.MarketStall, new Vector3(13.5f, 0f, 8f), 270f),
        new("prop.banner.west", StreetPropKind.Banner, new Vector3(-14.5f, 0f, 43f), 90f),
        new("prop.banner.east", StreetPropKind.Banner, new Vector3(14.5f, 0f, -22f), 270f),
        new("prop.tree.west", StreetPropKind.ShadeTree, new Vector3(-14f, 0f, 73f)),
        new("prop.tree.east", StreetPropKind.ShadeTree, new Vector3(14f, 0f, -72f)),
        new("prop.basin", StreetPropKind.WaterBasin, new Vector3(-14f, 0f, 5f)),
        new("prop.arch.north", StreetPropKind.CivicArch, new Vector3(0f, 0f, 96f))
    };

    public static IReadOnlyList<FacadeSpec> StreetFacades => AuthoredFacades;
    public static IReadOnlyList<StreetPropSpec> StreetProps => AuthoredStreetProps;

    [Serializable]
    public readonly struct StreetFigureSpec
    {
        public readonly string Id;
        public readonly Vector3 LocalPosition;
        public readonly int PaletteIndex;
        public readonly float Height;

        public StreetFigureSpec(string id, Vector3 localPosition, int paletteIndex, float height)
        {
            Id = id;
            LocalPosition = localPosition;
            PaletteIndex = paletteIndex;
            Height = height;
        }
    }

    // A reserved authored street must bring its own life; excluding the generic crowd and then
    // adding nobody here recreated the very “empty city” failure the slice exists to answer.
    private static readonly StreetFigureSpec[] AuthoredStreetFigures =
    {
        new("figure.spice_vendor", new Vector3(-10.5f, 0f, -51f), 1, 1.72f),
        new("figure.dock_runner", new Vector3(10.4f, 0f, -68f), 2, 1.78f),
        new("figure.cloth_vendor", new Vector3(10.5f, 0f, 12f), 3, 1.69f),
        new("figure.water_bearer", new Vector3(-10.2f, 0f, 3f), 0, 1.74f),
        new("figure.palace_clerk", new Vector3(-10.8f, 0f, 46f), 2, 1.82f),
        new("figure.watchman", new Vector3(10.8f, 0f, 71f), 1, 1.86f)
    };

    public static IReadOnlyList<StreetFigureSpec> StreetFigures => AuthoredStreetFigures;

    public static Vector3 ToWorld(in FacadeSpec spec) => StreetOrigin + spec.LocalPosition;
    public static Vector3 ToWorld(in StreetPropSpec spec) => StreetOrigin + spec.LocalPosition;
    public static Vector3 ToWorld(in StreetFigureSpec spec) => StreetOrigin + spec.LocalPosition;

    /// <summary>
    /// True when a generated block centred here would overlap the authored street reservation.
    /// The half-size makes this an AABB overlap rather than a point test, so background blocks do
    /// not poke through a facade after a rebuild.
    /// </summary>
    public static bool OverlapsStreetReservation(Vector3 worldCentre, float blockHalfSize)
    {
        Vector3 local = worldCentre - StreetOrigin;
        return Mathf.Abs(local.x) <= StreetReservedHalfWidth + blockHalfSize
               && Mathf.Abs(local.z) <= StreetLength * 0.5f + blockHalfSize;
    }

    /// <summary>Props are dressing, but none may narrow the critical walking lane.</summary>
    public static bool IsInsideStreetWalkingLane(Vector3 worldPoint)
    {
        Vector3 local = worldPoint - StreetOrigin;
        return Mathf.Abs(local.x) < StreetClearHalfWidth
               && Mathf.Abs(local.z) <= StreetLength * 0.5f;
    }

    public enum DungeonModuleKind
    {
        FloorRegister,
        WallPanel,
        CellFront,
        CeilingBeam,
        EndLandmark
    }

    [Serializable]
    public readonly struct DungeonModuleSpec
    {
        public readonly string Id;
        public readonly DungeonModuleKind Kind;
        public readonly int RoomIndex;
        public readonly Vector3 LocalOffset;
        public readonly float FacingDegrees;

        public DungeonModuleSpec(string id, DungeonModuleKind kind, int roomIndex,
            Vector3 localOffset, float facingDegrees = 0f)
        {
            Id = id;
            Kind = kind;
            RoomIndex = roomIndex;
            LocalOffset = localOffset;
            FacingDegrees = facingDegrees;
        }
    }

    public const float DungeonRoomDepth = 24f;

    private static readonly DungeonModuleSpec[] AuthoredDungeonModules =
    {
        new("dungeon.floor.entry", DungeonModuleKind.FloorRegister, 0, Vector3.zero),
        new("dungeon.panel.entry.west", DungeonModuleKind.WallPanel, 0, new Vector3(-14.15f, 0f, 3f), 90f),
        new("dungeon.panel.entry.east", DungeonModuleKind.WallPanel, 0, new Vector3(14.15f, 0f, 3f), 270f),
        new("dungeon.beam.entry", DungeonModuleKind.CeilingBeam, 0, new Vector3(0f, 0f, 5f)),

        new("dungeon.floor.one", DungeonModuleKind.FloorRegister, 1, Vector3.zero),
        new("dungeon.cell.one.west", DungeonModuleKind.CellFront, 1, new Vector3(-12.45f, 0f, -4f), 90f),
        new("dungeon.cell.one.east", DungeonModuleKind.CellFront, 1, new Vector3(12.45f, 0f, 5f), 270f),
        new("dungeon.beam.one", DungeonModuleKind.CeilingBeam, 1, Vector3.zero),

        new("dungeon.floor.two", DungeonModuleKind.FloorRegister, 2, Vector3.zero),
        new("dungeon.cell.two.west", DungeonModuleKind.CellFront, 2, new Vector3(-12.45f, 0f, 5f), 90f),
        new("dungeon.cell.two.east", DungeonModuleKind.CellFront, 2, new Vector3(12.45f, 0f, -4f), 270f),
        new("dungeon.beam.two", DungeonModuleKind.CeilingBeam, 2, Vector3.zero),

        new("dungeon.floor.three", DungeonModuleKind.FloorRegister, 3, Vector3.zero),
        new("dungeon.cell.three.west", DungeonModuleKind.CellFront, 3, new Vector3(-12.45f, 0f, -4f), 90f),
        new("dungeon.cell.three.east", DungeonModuleKind.CellFront, 3, new Vector3(12.45f, 0f, 5f), 270f),
        new("dungeon.beam.three", DungeonModuleKind.CeilingBeam, 3, Vector3.zero),
        new("dungeon.landmark.end", DungeonModuleKind.EndLandmark, 3, new Vector3(0f, 0f, 11.45f))
    };

    public static IReadOnlyList<DungeonModuleSpec> DungeonModules => AuthoredDungeonModules;

    /// <summary>Centre of a generated chamber in GreyThreadSceneBuilder coordinates.</summary>
    public static float DungeonRoomCentreZ(int roomIndex) =>
        roomIndex <= 0 ? 0f : 10f + DungeonRoomDepth * (roomIndex - 0.5f);

    public static Vector3 DungeonPosition(in DungeonModuleSpec spec) =>
        spec.LocalOffset + Vector3.forward * DungeonRoomCentreZ(spec.RoomIndex);

    /// <summary>
    /// Centre of the deepest real chamber. The previous route-mechanic formula returned one
    /// chamber beyond this point, on the far side of the sealed wall.
    /// </summary>
    public static float DeepestDungeonRoomCentreZ(int roomCount) =>
        DungeonRoomCentreZ(Mathf.Max(1, roomCount) - 1);
}
