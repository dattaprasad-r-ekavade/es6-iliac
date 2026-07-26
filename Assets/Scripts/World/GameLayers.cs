using UnityEngine;

/// <summary>
/// Named physics layers and the query masks built from them.
///
/// Gameplay used to decide "is this the ground / the ocean / a prop" by string
/// matching GameObject names (<c>name.Contains("Ocean")</c>), which meant any
/// rename silently broke falling, spawning and out-of-bounds detection. Raycasts
/// also used <c>~0</c>, so a sword swing could hit terrain or a friendly NPC.
///
/// Layer indices must match <c>ProjectSettings/TagManager.asset</c>.
/// <see cref="WorldTagger"/> is what actually assigns them at runtime.
/// </summary>
public static class GameLayers
{
    public const int Default = 0;
    public const int TransparentFx = 1;
    public const int IgnoreRaycast = 2;
    public const int Water = 4;
    public const int Ui = 5;

    /// <summary>Terrain, city plazas, spawn pads, roads — anything walkable.</summary>
    public const int Ground = 8;

    /// <summary>Buildings, walls, docks — solid but not "the ground".</summary>
    public const int Structure = 9;

    /// <summary>Trees, rocks, scatter. Not walkable, not a combat target.</summary>
    public const int Prop = 10;

    /// <summary>The player.</summary>
    public const int Player = 11;

    /// <summary>Hostiles.</summary>
    public const int Enemy = 12;

    /// <summary>Friendly, talkable characters.</summary>
    public const int Npc = 13;

    /// <summary>The invisible catcher slab under the map. Never counts as ground.</summary>
    public const int Void = 14;

    // ---- Query masks -------------------------------------------------------

    /// <summary>Surfaces the player can legitimately stand on.</summary>
    public static readonly int GroundMask = (1 << Ground) | (1 << Structure);

    /// <summary>Ground plus buildings — used to place props and probe the world.</summary>
    public static readonly int WorldMask = (1 << Ground) | (1 << Structure) | (1 << Default);

    /// <summary>Things a weapon swing may connect with.</summary>
    public static readonly int CombatMask = (1 << Enemy);

    /// <summary>Things "E" can interact with.</summary>
    public static readonly int InteractMask = (1 << Npc);

    /// <summary>Blocks line of sight for an enemy attack.</summary>
    public static readonly int SightBlockerMask = (1 << Ground) | (1 << Structure);

    /// <summary>Everything except triggers, characters and non-collidable dressing.</summary>
    public static readonly int VoidOrGroundMask =
        (1 << Ground) | (1 << Structure) | (1 << Void) | (1 << Water);
}
