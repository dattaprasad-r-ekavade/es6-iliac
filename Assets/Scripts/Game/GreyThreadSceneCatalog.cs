using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable scene/route contract for the VS2 grey-thread slice. The editor builder consumes
/// this catalogue so regenerated scenes and runtime route logic cannot drift apart.
/// </summary>
public static class GreyThreadSceneCatalog
{
    public sealed class SceneSpec
    {
        public readonly string Name;
        public readonly string SceneId;
        public readonly string Title;
        public readonly Color Accent;

        public SceneSpec(string name, string sceneId, string title, Color accent)
        {
            Name = name;
            SceneId = sceneId;
            Title = title;
            Accent = accent;
        }
    }

    private static readonly SceneSpec[] Specs =
    {
        new("Prologue_Ship", "scene.prologue_ship", "The Wrecked Ship", new Color(0.30f, 0.42f, 0.52f)),
        new("Estmere_Docks", "scene.estmere_docks", "Estmere Docks", new Color(0.36f, 0.50f, 0.42f)),
        new("Estmere_Palace", "scene.estmere_palace", "Estmere Palace", new Color(0.53f, 0.40f, 0.24f)),
        new("Tutorial_Warrior", "scene.tutorial_warrior", "Guard Drill Yard", new Color(0.60f, 0.27f, 0.18f)),
        new("Estmere_Arcanum", "scene.estmere_arcanum", "The Arcanum", new Color(0.28f, 0.34f, 0.62f)),
        new("Estmere_Harbor", "scene.estmere_harbor", "Merchant Harbor", new Color(0.24f, 0.52f, 0.55f)),
        new("Estmere_SecuredTower", "scene.estmere_secured_tower", "Secured East Tower", new Color(0.48f, 0.32f, 0.24f)),
        new("Estmere_Prison", "scene.estmere_prison", "Estmere Prison", new Color(0.30f, 0.30f, 0.34f)),
        new("Estmere_SeaCave", "scene.estmere_sea_cave", "The Sea Cave", new Color(0.20f, 0.37f, 0.45f)),
        new("Estmere_Palace_Aftermath", "scene.estmere_palace_aftermath", "Palace Aftermath", new Color(0.46f, 0.25f, 0.20f)),
        new("Caldemar_Arrival", "scene.caldemar_arrival", "Caldemar Council Gate", new Color(0.55f, 0.43f, 0.25f))
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
