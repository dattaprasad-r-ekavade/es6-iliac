using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// Standing in the order, and therefore in the fort.
///
/// The names are the empire's own, bottom to top, from the offices research. A player promoted
/// from yukta to sthanika has been promoted inside a real civil service, and the words do the
/// worldbuilding without a codex entry explaining them.
/// </summary>
public enum Rank
{
    /// <summary>Subordinate officer. Where everybody starts.</summary>
    Yukta,

    /// <summary>District officer.</summary>
    Sthanika,

    /// <summary>District head — revenue, and order.</summary>
    Pradeshika,

    /// <summary>Superintendent.</summary>
    Adhyaksha,

    /// <summary>High officer. The top of the ladder the province has to offer.</summary>
    Mahamatra
}

public sealed record RankRequirement(Rank Rank, string Title, int Descents, int Stones);

/// <summary>
/// What each rank costs, and what the order calls you when you have it.
///
/// **Opened by wins and gold**, per the iteration, and both are required rather than either.
/// A single requirement is a single grind: descents alone rewards repeating the shallowest
/// mine forever, and stones alone rewards one lucky run and then nothing. Together they ask
/// for a player who both keeps going down and comes back up with something.
/// </summary>
public static class Ranks
{
    private static readonly RankRequirement[] Ladder =
    {
        new(Rank.Yukta, "Yukta", Descents: 0, Stones: 0),
        new(Rank.Sthanika, "Sthanika", Descents: 3, Stones: 30),
        new(Rank.Pradeshika, "Pradeshika", Descents: 8, Stones: 120),
        new(Rank.Adhyaksha, "Adhyaksha", Descents: 16, Stones: 320),
        new(Rank.Mahamatra, "Mahamatra", Descents: 28, Stones: 700)
    };

    public static IReadOnlyList<RankRequirement> All => Ladder;

    public static RankRequirement Requirement(Rank rank) =>
        Ladder.First(entry => entry.Rank == rank);

    public static string TitleOf(Rank rank) => Requirement(rank).Title;

    /// <summary>
    /// The highest rank earned by a record of descents survived and stones banked.
    ///
    /// Read from the totals every time rather than stored as a number that gets incremented.
    /// A stored rank is a rank that can drift out of step with what earned it — and the one
    /// place it would drift is a save written by an older build, which is exactly where nobody
    /// would look.
    /// </summary>
    public static Rank Earned(int descentsSurvived, int stonesBanked)
    {
        var earned = Rank.Yukta;

        foreach (var entry in Ladder)
            if (descentsSurvived >= entry.Descents && stonesBanked >= entry.Stones)
                earned = entry.Rank;

        return earned;
    }

    /// <summary>The next rung, and what it wants. Null at the top.</summary>
    public static RankRequirement? Next(Rank current) =>
        Ladder.FirstOrDefault(entry => entry.Rank > current);

    /// <summary>True when the first is at least the second.</summary>
    public static bool AtLeast(Rank held, Rank required) => held >= required;
}

/// <summary>
/// What the order has done, across every Deepankar who has held the lamp.
///
/// Lives on <see cref="Legacy"/> for the same reason amulets do: it has to survive death.
/// Rank is the order's standing, not one person's, and a successor who arrived to find
/// themselves demoted to yukta would be a successor nobody wants to play.
/// </summary>
public sealed class ServiceRecord
{
    /// <summary>Descents walked out of alive. Dying does not count, and does not subtract.</summary>
    public int DescentsSurvived { get; private set; }

    /// <summary>Stones actually banked, over every run. Lost pots do not count.</summary>
    public int StonesBanked { get; private set; }

    public Rank Rank => Ranks.Earned(DescentsSurvived, StonesBanked);

    public string Title => Ranks.TitleOf(Rank);

    public event Action<Rank>? Promoted;

    /// <summary>Record a completed run. Returns true when it earned a promotion.</summary>
    public bool Record(RunResult run)
    {
        var before = Rank;

        // Only a banked run counts toward standing. The order is not impressed by how deep
        // somebody got if the stones stayed down there with them — and the ratchet that keeps
        // a lost run worth something is amulets, which is a separate promise deliberately.
        if (run.Survived)
        {
            DescentsSurvived++;
            StonesBanked += Math.Max(0, run.StonesCarriedOut);
        }

        var after = Rank;
        if (after == before) return false;

        Promoted?.Invoke(after);
        return true;
    }

    public void Restore(int descents, int stones)
    {
        DescentsSurvived = Math.Max(0, descents);
        StonesBanked = Math.Max(0, stones);
    }

    public void Reset()
    {
        DescentsSurvived = 0;
        StonesBanked = 0;
    }
}
