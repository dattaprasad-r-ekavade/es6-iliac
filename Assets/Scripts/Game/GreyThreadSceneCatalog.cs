using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable scene/route contract for the VS2 grey-thread slice. The editor builder consumes
/// this catalogue so regenerated scenes and runtime route logic cannot drift apart.
/// </summary>
public static class GreyThreadSceneCatalog
{
    /// <summary>
    /// The VS4 mechanic an interior exists to teach or use. This is what stops the mechanics
    /// from being systems nothing touches — each route's beat now has somewhere it happens.
    /// </summary>
    public enum Feature
    {
        None,
        /// <summary>A locked door worth picking. B420's tower infiltration.</summary>
        Lock,
        /// <summary>A pocket worth lifting, and a guard who might notice. B410 and B510.</summary>
        Pickpocket,
        /// <summary>A moored boat. B400's sailing lesson.</summary>
        Boat,
        /// <summary>A sparring dummy that fights back a little. B200's guard yard.</summary>
        CombatDummy,
        /// <summary>Crystals to spend and something to cast at. B300's spell instruction.</summary>
        SpellTarget
    }

    public sealed class SceneSpec
    {
        public readonly string Name;
        public readonly string SceneId;
        public readonly string Title;
        public readonly Color Accent;

        /// <summary>Chambers the interior is built from. One is a box; three is a place.</summary>
        public readonly int Rooms;

        public readonly Feature Mechanic;

        /// <summary>False for the region-adjacent spaces the story moves the player out of.</summary>
        public readonly bool HasExitDoor;

        public SceneSpec(string name, string sceneId, string title, Color accent,
            int rooms = 1, Feature mechanic = Feature.None, bool hasExitDoor = true)
        {
            Name = name;
            SceneId = sceneId;
            Title = title;
            Accent = accent;
            Rooms = Mathf.Max(1, rooms);
            Mechanic = mechanic;
            HasExitDoor = hasExitDoor;
        }
    }

    private static readonly SceneSpec[] Specs =
    {
        // The prologue is a directed sequence the player is carried through, so it has no
        // way back out to a city that has not been reached yet.
        new("Prologue_Ship", "scene.prologue_ship", "The Wrecked Ship", new Color(0.30f, 0.42f, 0.52f),
            rooms: 2, hasExitDoor: false),

        new("Docks", "scene.docks", "Estmere Docks", new Color(0.36f, 0.50f, 0.42f),
            rooms: 2),
        new("Palace", "scene.palace", "Estmere Palace", new Color(0.53f, 0.40f, 0.24f),
            rooms: 3),
        new("Tutorial_Warrior", "scene.tutorial_warrior", "Guard Drill Yard", new Color(0.60f, 0.27f, 0.18f),
            rooms: 2, mechanic: Feature.CombatDummy),
        new("Order_Hall", "scene.order_hall", "The Arcanum", new Color(0.28f, 0.34f, 0.62f),
            rooms: 3, mechanic: Feature.SpellTarget),
        new("Harbor", "scene.harbor", "Merchant Harbor", new Color(0.24f, 0.52f, 0.55f),
            rooms: 2, mechanic: Feature.Boat),
        new("Secured_Tower", "scene.secured_tower", "Secured East Tower", new Color(0.48f, 0.32f, 0.24f),
            rooms: 3, mechanic: Feature.Lock),
        new("Prison", "scene.prison", "Estmere Prison", new Color(0.30f, 0.30f, 0.34f),
            rooms: 4, mechanic: Feature.Pickpocket),

        // The cave and everything after it are one-way story spaces.
        new("Sea_Cave", "scene.sea_cave", "The Sea Cave", new Color(0.20f, 0.37f, 0.45f),
            rooms: 3, hasExitDoor: false),
        new("Palace_Aftermath", "scene.palace_aftermath", "Palace Aftermath", new Color(0.46f, 0.25f, 0.20f),
            rooms: 2, hasExitDoor: false),
        new("Council_Arrival", "scene.council_arrival", "Caldemar Council Gate", new Color(0.55f, 0.43f, 0.25f),
            rooms: 2, hasExitDoor: false)
    };

    public static IReadOnlyList<SceneSpec> Scenes => Specs;

    public static SceneSpec Find(string sceneName)
    {
        for (int i = 0; i < Specs.Length; i++)
            if (string.Equals(Specs[i].Name, sceneName, StringComparison.Ordinal)) return Specs[i];
        return null;
    }

    public static bool IsSupportedRoute(string routeId)
    {
        return routeId == "route.warrior" || routeId == "route.mage"
               || routeId == "route.trade" || routeId == "route.refuse";
    }

    public static string NormalizeRoute(string routeId)
    {
        return IsSupportedRoute(routeId) ? routeId : "route.refuse";
    }
}
