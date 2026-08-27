using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// Standing in the order, and therefore in the fort.
///
/// **These are the order's own rungs, not the empire's.** The state has exactly one word for a
/// Bhagiratha — *khanaka*, digger, on the tally roll — and no interest in a finer grain than that.
/// So the order ranks its own the only way that means anything underground: by which floor of
/// the world below you have been to and come back from.
///
/// The names are the seven **patalas**, the nether-realms of the Puranas, in their canonical
/// descending order, with Patala itself the deepest. Two things fall out of that and both are
/// worth having. *Tala* is a floor, a storey, a level — so the ladder is literally named in the
/// vocabulary of a mine. And the shared ending makes the seven read as one series on sight,
/// which the old civil-service titles never did: nothing about *sthanika* told a player it sat
/// below *pradeshika*.
///
/// It also stops two words meaning two things. The old top rung was *mahamatra*, which is the
/// inspector's actual office, and the old fourth was *adhyaksha*, which is half the registrar's
/// title. A rank ladder that collides with the cast is a ladder that teaches the player the
/// wrong thing twice.
/// </summary>
public enum Rank
{
    /// <summary>The first floor down. Where everybody starts.</summary>
    Atala,

    /// <summary>Second.</summary>
    Vitala,

    /// <summary>Third.</summary>
    Sutala,

    /// <summary>Fourth.</summary>
    Talatala,

    /// <summary>Fifth.</summary>
    Mahatala,

    /// <summary>Sixth.</summary>
    Rasatala,

    /// <summary>The bottom. Nothing in the province is offered past this.</summary>
    Patala
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
        new(Rank.Atala, "Atala", Descents: 0, Stones: 0),
        new(Rank.Vitala, "Vitala", Descents: 2, Stones: 20),
        new(Rank.Sutala, "Sutala", Descents: 5, Stones: 60),
        new(Rank.Talatala, "Talatala", Descents: 9, Stones: 140),
        new(Rank.Mahatala, "Mahatala", Descents: 14, Stones: 260),
        new(Rank.Rasatala, "Rasatala", Descents: 20, Stones: 440),
        new(Rank.Patala, "Patala", Descents: 28, Stones: 700)
    };

    /// <summary>
    /// Which rung this is, counting from one.
    ///
    /// Shown wherever a rank is, and the reason the ladder can afford opaque names at all. A
    /// shut door saying *Sutala* tells a player nothing about how far off it is; the same door
    /// saying *Sutala, 3rd of 7* tells them whether to keep going or come back in ten hours.
    /// The flavour survives and the navigation problem goes away.
    /// </summary>
    public static int RungOf(Rank rank) => (int)rank + 1;

    /// <summary>Rungs in the ladder.</summary>
    public static int Rungs => Ladder.Length;

    /// <summary>Title and position together, for anything the player reads.</summary>
    public static string LabelOf(Rank rank) =>
        $"{TitleOf(rank)} ({RungOf(rank)} of {Rungs})";

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
        var earned = Rank.Atala;

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
/// What the order has done, across every Bhagiratha who has held the lamp.
///
/// Lives on <see cref="Legacy"/> for the same reason amulets do: it has to survive death.
/// Rank is the order's standing, not one person's, and a successor who arrived to find
/// themselves demoted to atala would be a successor nobody wants to play.
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
