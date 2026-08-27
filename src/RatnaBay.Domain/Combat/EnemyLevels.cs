using System;

namespace RatnaBay.Domain;

/// <summary>
/// What level a particular enemy is, given where it is standing.
///
/// Every enemy in a room used to share one number — <c>tier + room / 4</c> — so a room was a
/// squad of clones and depth moved the whole squad together. This gives each body its own
/// level, drawn from a band that rises with the tier of the cave and with how far in you have
/// walked, the way an encounter table works in a game with routes: the area sets the band, the
/// individual is rolled inside it.
///
/// Three properties are being bought here, and each one is worth more than the arithmetic.
///
/// **Bands overlap between tiers.** A deep room in a cheap cave reaches the same levels as a
/// shallow room in an expensive one. That is what keeps buying a deeper tier a decision rather
/// than a strict upgrade: paying eighty stones does not buy a harder game, it buys the harder
/// part sooner, and the player who cannot afford it can still get there by pressing on.
///
/// **A room is a group of individuals.** One body a level above its fellows changes how a room
/// is read on entry, and it costs nothing to generate.
///
/// **Levelling lengthens fights faster than it sharpens them.** Health rises 22% a level
/// against damage at 16%, so a level-6 bandit takes noticeably longer to kill without becoming
/// proportionally more lethal. That matters here specifically: the recordings show ordinary
/// fights lasting about two seconds, which is not long enough for blocking, staggering or a
/// chill to ever become visible. Depth is the lever that gives the tactical verbs room to
/// appear, so it should be pulled deliberately rather than left as a side effect.
/// </summary>
public static class EnemyLevels
{
    /// <summary>Levels gained per tier of cave bought at the shaft.</summary>
    public const int TierStep = 2;

    /// <summary>Rooms walked per level of depth.</summary>
    public const int RoomsPerLevel = 3;

    /// <summary>How far either side of the band's centre an ordinary body can roll.</summary>
    public const int BandHalfWidth = 1;

    /// <summary>
    /// One in this many is a standout, a further step above the band.
    ///
    /// Rare on purpose. The point of it is that a player remembers the one that was harder
    /// than it looked, and a memory like that needs the ordinary case to be ordinary.
    /// </summary>
    public const int StandoutOdds = 12;

    private const int StandoutBonus = 2;

    /// <summary>A vetala leads a room; it is not rank and file, and its level says so.</summary>
    public const int EliteBonus = 1;

    /// <summary>Nothing is below this, however shallow the cave.</summary>
    public const int MinLevel = 1;

    /// <summary>
    /// The middle of the band for a place, before any individual roll.
    ///
    /// Tier is worth two levels and three rooms are worth one, so pressing on out-earns buying
    /// depth over a long enough run — which is the shape the whole game wants, because pressing
    /// on is the decision it is built around and buying depth is only what sets the floor.
    /// </summary>
    public static int Centre(int tier, int roomIndex)
    {
        var fromTier = (Math.Max(1, tier) - 1) * TierStep;
        var fromDepth = Math.Max(0, roomIndex) / RoomsPerLevel;

        return MinLevel + fromTier + fromDepth;
    }

    /// <summary>The lowest and highest an ordinary body in this room can roll.</summary>
    public static (int Low, int High) Band(int tier, int roomIndex)
    {
        var centre = Centre(tier, roomIndex);

        return (Math.Max(MinLevel, centre - BandHalfWidth), centre + BandHalfWidth);
    }

    /// <summary>
    /// Roll one enemy's level.
    /// </summary>
    /// <param name="elite">
    /// Whether this is one of the archetypes that leads a room rather than fills it.
    /// </param>
    public static int Roll(int tier, int roomIndex, bool elite, Prng random)
    {
        var (low, high) = Band(tier, roomIndex);

        // Inclusive of both ends: a three-wide band with one end unreachable is a two-wide
        // band, and the whole point is that a room is not uniform.
        var level = low + random.Next(high - low + 1);

        if (elite) level += EliteBonus;
        if (random.Next(StandoutOdds) == 0) level += StandoutBonus;

        return Math.Max(MinLevel, level);
    }
}
