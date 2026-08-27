using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// What an amulet changes, permanently.
///
/// These are the opposite layer from stones. A stone answers *this cave* and is gone when you
/// leave it; an amulet is kept forever and passes to your successor. So a stone can afford to
/// be loud — a blade that suddenly sweeps — and an amulet cannot, because it will be true for
/// every run the player ever makes and has to still be interesting on the hundredth.
///
/// They grant *options* rather than numbers, for the reason the design's own reference list
/// gives for Dead Cells: a permanent +5% is invisible after the run it arrives in, and a
/// permanent extra socket changes what the player does with every stone they find afterwards.
/// </summary>
public enum AmuletEffect
{
    /// <summary>One more socket, in whatever is being held.</summary>
    Socket,

    /// <summary>The first blow landed in each room counts as an opening.</summary>
    FirstBlood,

    /// <summary>Death takes less of the pack.</summary>
    LongMemory,

    /// <summary>Each descent begins with an extra jiva stone.</summary>
    Bearer,

    /// <summary>Stamina returns faster out of combat.</summary>
    SecondBreath
}

public sealed record AmuletDefinition(
    string Id,
    string DisplayName,
    AmuletEffect Effect,
    string Description);

/// <summary>
/// Every amulet, and the depths that earn them.
///
/// **Earned by going deeper than you ever have, whether you live or not.** That is the whole
/// mechanism, and it is chosen to answer the question this iteration exists to retire: *does a
/// losing run still pull you back in?* An amulet awarded for surviving would make a bad run
/// worth nothing, which is exactly the failure. Awarded for depth reached, a run that ends in
/// a corpse two rooms further down than last time still ratchets.
///
/// It also cannot be farmed. Shallow runs repeated forever award nothing, because the
/// threshold is a personal best rather than a per-run roll.
/// </summary>
public static class AmuletCatalog
{
    public const string SocketId = "amulet.socket";
    public const string FirstBloodId = "amulet.firstblood";
    public const string LongMemoryId = "amulet.longmemory";
    public const string BearerId = "amulet.bearer";
    public const string SecondBreathId = "amulet.secondbreath";

    /// <summary>What a Long Memory amulet leaves in the pack that death would have taken.</summary>
    public const float LongMemoryPackKept = 0.25f;

    /// <summary>How much faster stamina returns out of combat with Second Breath.</summary>
    public const float SecondBreathFactor = 1.5f;

    /// <summary>
    /// Deepest room ever reached, and what reaching it grants.
    ///
    /// The gaps widen deliberately. Early amulets arrive close together so a new player feels
    /// the ratchet inside their first few runs; later ones are far enough apart that they stay
    /// a goal rather than a drip.
    /// </summary>
    private static readonly (int Depth, string AmuletId)[] Milestones =
    {
        (3, SocketId),
        (6, FirstBloodId),
        (10, LongMemoryId),
        (15, BearerId),
        (21, SecondBreathId)
    };

    private static readonly Dictionary<string, AmuletDefinition> Amulets =
        new(StringComparer.Ordinal)
        {
            [SocketId] = new(SocketId, "Dipadhara's Lamp", AmuletEffect.Socket,
                "One more socket, in whatever you carry."),

            [FirstBloodId] = new(FirstBloodId, "Steady Hand", AmuletEffect.FirstBlood,
                "The first blow you land in a room strikes an opening."),

            [LongMemoryId] = new(LongMemoryId, "Long Memory", AmuletEffect.LongMemory,
                "Your successor inherits more of what you were carrying."),

            [BearerId] = new(BearerId, "Bearer's Mark", AmuletEffect.Bearer,
                "You go down with one more jiva stone than you bought."),

            [SecondBreathId] = new(SecondBreathId, "Second Breath", AmuletEffect.SecondBreath,
                "Your wind comes back faster when nothing is fighting you.")
        };

    public static IReadOnlyCollection<AmuletDefinition> All => Amulets.Values;

    public static AmuletDefinition? Find(string? id) =>
        id is not null && Amulets.TryGetValue(id, out var amulet) ? amulet : null;

    /// <summary>The deepest room any amulet is awarded for. Past this, the ladder is done.</summary>
    public static int DeepestMilestone => Milestones.Max(m => m.Depth);

    /// <summary>
    /// Which amulets a new personal best has just earned.
    ///
    /// Takes both the old best and the new one so a single run that jumps several milestones
    /// awards all of them. Returning only the deepest would quietly swallow the others, and a
    /// player who had a very good run would be paid for part of it.
    /// </summary>
    public static IReadOnlyList<string> EarnedBetween(int previousBest, int newBest)
    {
        if (newBest <= previousBest) return Array.Empty<string>();

        return Milestones
            .Where(m => m.Depth > previousBest && m.Depth <= newBest)
            .Select(m => m.AmuletId)
            .ToList();
    }

    /// <summary>The next one to aim at, or null once they have all been earned.</summary>
    public static (int Depth, AmuletDefinition Amulet)? NextAfter(int best)
    {
        foreach (var (depth, id) in Milestones.OrderBy(m => m.Depth))
            if (depth > best && Find(id) is { } amulet)
                return (depth, amulet);

        return null;
    }
}
