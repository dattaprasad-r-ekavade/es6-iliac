using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// One cave's character: what it looks like, and which element it shrugs off.
///
/// The design's rule for these is one line and it decides the whole shape: **depth decides
/// reward, theme decides tactics.** A cave never pays more for being hostile to what you are
/// carrying. Give a hard theme a payout bonus and players stop choosing the cave they can
/// handle and start choosing the one that pays best, which is the opposite of a decision.
/// </summary>
public sealed record CaveTheme(
    string Id,
    string DisplayName,

    /// <summary>What the stone is called, for the line shown before the player pays.</summary>
    string Rock,

    /// <summary>
    /// What the miners call this level. See the note on <c>StoneDefinition.Indic</c>.
    ///
    /// Each is simply the material: *shila* rock, *tapta* heated, *jala* water, *lavana* salt,
    /// *asthi* bone. Miners do not name a seam poetically — they name it after what is in it,
    /// and the English name above is already carrying the atmosphere.
    /// </summary>
    string Indic,

    /// <summary>Takes reduced damage from this. Never zero — see <see cref="ResistedFactor"/>.</summary>
    SpellEffect Resists,

    /// <summary>Takes extra damage from this.</summary>
    SpellEffect Fears,

    /// <summary>Base, mortar and accent for the stone this cave is cut from.</summary>
    ThemeColour Base,
    ThemeColour Mortar,
    ThemeColour Accent)
{
    public string Summary => $"{Rock}. Shrugs off {Name(Resists)}, fears {Name(Fears)}.";

    public static string Name(SpellEffect effect) => effect switch
    {
        SpellEffect.Fire => "Flame",
        SpellEffect.Frost => "Rime",
        SpellEffect.Shock => "Arc",
        _ => effect.ToString()
    };
}

/// <summary>A colour, without dragging a rendering type into the domain.</summary>
public readonly record struct ThemeColour(byte R, byte G, byte B);

/// <summary>
/// Five caves, and the rules about how much a theme is allowed to matter.
/// </summary>
public static class CaveThemeCatalog
{
    /// <summary>
    /// What a resisted spell is multiplied by.
    ///
    /// **Resistance, never immunity**, and this number is why the rule is stated as its own
    /// constant rather than per theme. A player whose only offence is Flame must still be able
    /// to finish a lava cave — badly, slowly, and wishing they had brought something else, but
    /// able. Immunity turns "which cave can I handle" into "which cave am I locked out of",
    /// and a roguelite that locks a build out of content has stopped being one.
    /// </summary>
    public const float ResistedFactor = 0.45f;

    /// <summary>And what a feared one is multiplied by.</summary>
    public const float FearedFactor = 1.55f;

    private static readonly CaveTheme[] Themes =
    {
        new("cave.granite", "The Old Workings", "Cold grey granite", "Shila",
            SpellEffect.Shock, SpellEffect.Frost,
            new ThemeColour(86, 86, 92), new ThemeColour(34, 33, 36), new ThemeColour(120, 118, 122)),

        new("cave.lava", "The Burnt Seam", "Scorched red rock, still warm", "Tapta",
            SpellEffect.Fire, SpellEffect.Frost,
            new ThemeColour(104, 62, 52), new ThemeColour(48, 24, 20), new ThemeColour(148, 92, 70)),

        new("cave.water", "The Drowned Level", "Wet black stone, running with water", "Jala",
            SpellEffect.Frost, SpellEffect.Shock,
            new ThemeColour(58, 74, 88), new ThemeColour(24, 32, 40), new ThemeColour(92, 116, 132)),

        new("cave.salt", "The Salt Reach", "Pale salt, crusted and brittle", "Lavana",
            SpellEffect.Frost, SpellEffect.Fire,
            new ThemeColour(132, 128, 116), new ThemeColour(78, 74, 66), new ThemeColour(172, 168, 154)),

        new("cave.bone", "The Ossuary", "Grey rock threaded with old bone", "Asthi",
            SpellEffect.Shock, SpellEffect.Fire,
            new ThemeColour(96, 90, 78), new ThemeColour(44, 40, 34), new ThemeColour(136, 128, 112))
    };

    public static IReadOnlyList<CaveTheme> All => Themes;

    public static CaveTheme? Find(string? id) =>
        Themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Which cave a given mine is.
    ///
    /// Derived from the seed rather than stored, so the answer is the same everywhere without
    /// anything having to pass it around: the shaft screen can name the cave before the player
    /// pays, the generator can theme its rooms, and the renderer can pick a palette, and none
    /// of the three can disagree with the other two.
    ///
    /// Tier one is always the granite workings. A first descent is a tutorial whether or not
    /// it is labelled one, and teaching the loop against a resistance the player has no way to
    /// answer yet is teaching them the wrong lesson about their own competence.
    /// </summary>
    public static CaveTheme For(int seed, int tier)
    {
        if (tier <= RunState.MinTier) return Themes[0];

        // Mixed so that neighbouring seeds do not give neighbouring themes, and unsigned so a
        // negative seed does not index backwards off the array.
        var mixed = unchecked((uint)(seed * 2654435761u + (uint)tier * 40503u));
        return Themes[(int)(mixed % (uint)Themes.Length)];
    }

    /// <summary>
    /// What a spell of this element is multiplied by in this cave.
    ///
    /// One place, so the shaft screen's promise and the damage actually dealt cannot drift
    /// apart. A theme that says it fears Arc and does not is worse than no theme at all.
    /// </summary>
    public static float DamageFactor(CaveTheme? theme, SpellEffect effect)
    {
        if (theme is null) return 1f;
        if (theme.Resists == effect) return ResistedFactor;
        if (theme.Fears == effect) return FearedFactor;
        return 1f;
    }
}
