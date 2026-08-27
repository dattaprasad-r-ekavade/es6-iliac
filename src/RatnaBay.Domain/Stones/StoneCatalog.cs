using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// What a socketed stone changes.
///
/// Every one of these alters a **verb** — what a swing does, what a block does, what a kill
/// gives back. None of them alters a number. That is the rule the whole system stands on and
/// it comes straight out of the production plan: *"effects that change how a weapon or spell
/// behaves, not how large it is"*.
///
/// The reason is the run length. A stone that adds fifteen percent damage cannot be noticed
/// inside six minutes — the player has no baseline to compare against, no time to average out
/// the variance, and no way to tell it from a good roll on room layout. A stone that makes a
/// blade sweep is obvious on the first swing.
/// </summary>
public enum StoneEffect
{
    /// <summary>A one-handed weapon sweeps, hitting everything in its arc.</summary>
    Splitting,

    /// <summary>Melee sets what it hits alight.</summary>
    Cinder,

    /// <summary>Melee chills what it hits.</summary>
    Rime,

    /// <summary>Melee staggers, whatever the weapon.</summary>
    Thunder,

    /// <summary>A kill returns prana.</summary>
    Vessel,

    /// <summary>A blow taken on the guard staggers whoever threw it.</summary>
    Ward
}

public sealed record StoneDefinition(
    string Id,
    string DisplayName,
    StoneEffect Effect,

    /// <summary>One line, in the player's terms. Shown in the socketing screen.</summary>
    string Description,

    /// <summary>
    /// What the assayer and the miners call it.
    ///
    /// The English name is the one that has to work on sight — a player choosing between two
    /// stones mid-run needs *Cinder* and *Ward*, not a vocabulary test. The Indic name rides
    /// alongside it and is what the province actually says out loud, so the world has its own
    /// word for a thing the interface has already explained. Nothing reads this to make a
    /// decision; it exists so the trader and the assayer can be overheard using it.
    /// </summary>
    string Indic,

    /// <summary>How deep a mine has to be before this can be found in it.</summary>
    int MinimumDepth = 1)
{
    /// <summary>Both names, for anywhere with room for them.</summary>
    public string FullName => $"{DisplayName} — {Indic}";
}

/// <summary>
/// Every jiva stone that can be socketed.
///
/// Deliberately six. The design's own budget for in-run variety is small, and a longer list
/// makes each stone rarer without making any run more interesting — at three or four stones
/// per descent, twenty options means a player never sees the same combination twice and never
/// learns what any of them do.
/// </summary>
public static class StoneCatalog
{
    public const string SplittingId = "stone.splitting";
    public const string CinderId = "stone.cinder";
    public const string RimeId = "stone.rime";
    public const string ThunderId = "stone.thunder";
    public const string VesselId = "stone.vessel";
    public const string WardId = "stone.ward";

    /// <summary>Seconds of burn a Cinder stone applies, and what it does per second.</summary>
    public const float CinderDamagePerSecond = 4f;

    public const float CinderSeconds = 3f;

    /// <summary>How much a Rime stone slows, and for how long.</summary>
    public const float RimeSpeedFactor = 0.6f;

    public const float RimeSeconds = 2f;

    /// <summary>
    /// Thunder's stagger, deliberately shorter than a mace's.
    ///
    /// A stone that hands every weapon the mace's verb in full would make the mace pointless,
    /// which is the opposite of variety. This is a taste of it, and a mace with a Thunder stone
    /// is still the longest opening in the game.
    /// </summary>
    public const float ThunderSeconds = 0.35f;

    /// <summary>Prana a Vessel stone returns per kill.</summary>
    public const float VesselPrana = 9f;

    /// <summary>How long a Ward stone leaves an attacker reeling after it is blocked.</summary>
    public const float WardSeconds = 0.5f;

    private static readonly Dictionary<string, StoneDefinition> Stones =
        new(StringComparer.Ordinal)
        {
            [SplittingId] = new(SplittingId, "Splitting Stone", StoneEffect.Splitting,
                "Your blade sweeps. Every enemy in the arc is struck.", "Bhedaka"),

            [CinderId] = new(CinderId, "Cinder Stone", StoneEffect.Cinder,
                "What you strike catches fire.", "Angara"),

            [RimeId] = new(RimeId, "Rime Stone", StoneEffect.Rime,
                "What you strike slows.", "Tuhina"),

            [ThunderId] = new(ThunderId, "Thunder Stone", StoneEffect.Thunder,
                "Your blows stagger, whatever you are holding.", "Ashani", MinimumDepth: 2),

            [VesselId] = new(VesselId, "Vessel Stone", StoneEffect.Vessel,
                "A death gives its prana back to you.", "Kumbha", MinimumDepth: 2),

            [WardId] = new(WardId, "Ward Stone", StoneEffect.Ward,
                "A blow you catch on your guard leaves the striker reeling.", "Kavacha", MinimumDepth: 3)
        };

    public static IReadOnlyCollection<StoneDefinition> All => Stones.Values;

    public static StoneDefinition? Find(string? id) =>
        id is not null && Stones.TryGetValue(id, out var stone) ? stone : null;

    public static bool Exists(string? id) => Find(id) is not null;

    /// <summary>What a mine of this depth is allowed to give up.</summary>
    public static IReadOnlyList<StoneDefinition> AvailableAt(int depth) =>
        Stones.Values.Where(stone => stone.MinimumDepth <= Math.Max(1, depth)).ToList();
}
