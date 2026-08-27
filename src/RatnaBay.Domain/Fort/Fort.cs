using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// One thing somebody in the fort will tell you, once.
///
/// Fired on a **conjunction** of rank and depth, never on either alone. That is the rule from
/// the story document and it is the one most easily broken by accident: with an OR, players
/// find whichever tap is cheaper and drain the entire story through it, and the other half of
/// the game stops mattering. Both taps have to stay live, which is the whole reason there are
/// two.
/// </summary>
public sealed record StoryFragment(
    string Id,
    Rank RequiredRank,

    /// <summary>Deepest room the order has ever reached.</summary>
    int RequiredDepth,

    string Text)
{
    public bool IsUnlocked(Rank rank, int deepestEver) =>
        Ranks.AtLeast(rank, RequiredRank) && deepestEver >= RequiredDepth;
}

/// <summary>
/// One room of the fort, and whoever is in it.
///
/// A room is the bounded authoring unit the whole design rests on — it is what keeps this from
/// becoming the open-city problem the pivot exists to escape. Ten of them, and the content
/// budget is ten rooms rather than a town.
/// </summary>
public sealed record FortRoom(
    string Id,
    string DisplayName,
    string Occupant,

    /// <summary>What the occupant does, in the empire's own words where there is one.</summary>
    string Office,

    Rank RequiredRank,

    /// <summary>What the room is like when you can finally get into it.</summary>
    string Description,

    /// <summary>Said every time, once the door is open. The room's standing line.</summary>
    string Greeting,

    IReadOnlyList<StoryFragment> Fragments)
{
    public bool IsOpen(Rank rank) => Ranks.AtLeast(rank, RequiredRank);

    /// <summary>What this occupant will say now, in order, given standing and depth.</summary>
    public IReadOnlyList<StoryFragment> AvailableTo(Rank rank, int deepestEver) =>
        Fragments.Where(f => f.IsUnlocked(rank, deepestEver)).ToList();
}

/// <summary>
/// The fort: ten rooms, opened by wins and gold, each carrying one turn of the story.
///
/// The occupants come from the offices research — a tally-keeper, an assayer, a mine registrar
/// who is an imperial officer rather than a local, a garrison captain who resents you. Story is
/// attached to rooms rather than to a questline, so a player who never opens room nine simply
/// never learns what is in it, and nothing is left half-finished in a journal.
/// </summary>
public static class FortRoster
{
    private static StoryFragment F(string id, Rank rank, int depth, string text) =>
        new(id, rank, depth, text);

    private static readonly FortRoom[] Rooms =
    {
        new("fort.gate", "The Gate", "Tissa", "Tally-keeper", Rank.Atala,
            "A table, a ledger, and a lamp that is never out.",
            "Bring it here before you spend it. I write down what the mountain gave you.",
            new[]
            {
                F("gate.1", Rank.Atala, 1,
                    "Everything that comes up goes in the book. The book goes to the capital."),
                F("gate.2", Rank.Sutala, 6,
                    "Your column is longer than most. That is not always a compliment here."),
                F("gate.3", Rank.Talatala, 12,
                    "I have kept this ledger eleven years. The totals never balance. The last "
                    + "person who said so out loud is behind a door at the bottom of a mine.")
            }),

        new("fort.hall", "The Order's Hall", "Revati", "Lamp-keeper", Rank.Atala,
            "Lamps in rows, and names cut into the wall beneath them.",
            "One lamp for each of us. I light the ones that have gone out.",
            new[]
            {
                F("hall.1", Rank.Atala, 1,
                    "Dipadhara, they call us. Lamp-bearers. On the tally roll we are "
                    + "akara-shantika — mine-pacifiers. The state prefers its own word."),
                F("hall.2", Rank.Sutala, 8,
                    "We are one year old. We are named for a woman who ran the mines and "
                    + "counted her people in and out, which nobody had thought to do before."),
                F("hall.3", Rank.Rasatala, 18,
                    "Her lamp is the first on the wall and her name is on no roll in this "
                    + "province. We remembered her and never once wrote her down.")
            }),

        new("fort.assay", "The Assay", "Nagadatta", "Assayer", Rank.Vitala,
            "Scales, a slate, and a drawer that is kept locked.",
            "Set them down. I weigh, I do not ask.",
            new[]
            {
                F("assay.1", Rank.Vitala, 4,
                    "Full and empty weigh the same. Only the light tells you which is which."),
                F("assay.2", Rank.Talatala, 12,
                    "Some of what comes across this table was never in a mine. I weigh that too.")
            }),

        new("fort.forge", "The Forge", "Lohasena", "Smith", Rank.Sutala,
            "Heat, and a floor worn into a hollow where he stands.",
            "Bring it back before it breaks, not after.",
            new[]
            {
                F("forge.1", Rank.Sutala, 5,
                    "Sockets are cut, not found. Better steel takes more of them, which is "
                    + "most of what you are paying for."),
                F("forge.2", Rank.Rasatala, 16,
                    "I have made the same six things for twenty years. The registrar decides "
                    + "what a Dipadhara is allowed to carry.")
            }),

        new("fort.physician", "The Physician", "Visakha", "Physician", Rank.Talatala,
            "Clean, quiet, and warmer than anywhere else in the fort.",
            "Sit. You have been breathing rock for a week.",
            new[]
            {
                F("phys.1", Rank.Talatala, 10,
                    "I buy prana. Do not ask me where it comes from; I have stopped asking."),
                F("phys.2", Rank.Talatala, 14,
                    "The lawful supply is what the almost-dead give up. It has never once been "
                    + "enough for a province this size."),
                F("phys.3", Rank.Rasatala, 20,
                    "There are people in the lower town who sleep eighteen hours and cannot "
                    + "say why. They are paid, if that helps.")
            }),

        new("fort.registrar", "The Registry", "Suvarnapala", "Akaradhyaksha", Rank.Talatala,
            "Palm-leaf in stacks, and a seal nobody else may touch.",
            "The mines are the crown's. You work them at the crown's pleasure.",
            new[]
            {
                F("reg.1", Rank.Talatala, 10,
                    "Mines fill the treasury. The treasury pays the army. That is the order "
                    + "of it, and it does not run the other way."),
                F("reg.2", Rank.Rasatala, 18,
                    "Ore taken is a fine of eight times its worth. A jiva stone taken is your "
                    + "life. I did not write that law and I do enforce it.")
            }),

        new("fort.shrine", "The Shrine", "Isidata", "Priest", Rank.Mahatala,
            "A carved pillar, and a lamp burning low in front of it.",
            "You are welcome here. What you do is not, but you are.",
            new[]
            {
                F("shrine.1", Rank.Mahatala, 9,
                    "You call them chhaya. Shadows. The word is preta, and it means something "
                    + "that cannot stop wanting. They are owed pity, not a sword."),
                F("shrine.2", Rank.Rasatala, 17,
                    "The verse on the pillar was cut by the state, in a mine the state opened, "
                    + "to take wealth out of it. Covet not — for whose is wealth? Nobody here "
                    + "finds that funny.")
            }),

        new("fort.barracks", "The Barracks", "Bhadrasena", "Garrison captain", Rank.Rasatala,
            "Twenty beds, eleven of them made.",
            "You are not soldiers. You are contractors with a lamp.",
            new[]
            {
                F("barracks.1", Rank.Rasatala, 15,
                    "We guard the convoys. You clear the holes. Nobody has ever asked us to "
                    + "go down there and I would refuse."),
                F("barracks.2", Rank.Rasatala, 22,
                    "An inspection comes every five years. The last one did not reach us. "
                    + "The books were sent ahead and came back approved.")
            }),

        new("fort.clerk", "The Clerk's Room", "Chandrashri", "Governor's clerk", Rank.Rasatala,
            "One window, and more paper than the registry.",
            "The governor is not available. I am, which is usually better.",
            new[]
            {
                F("clerk.1", Rank.Rasatala, 16,
                    "The province exports more prana than it lawfully collects. The difference "
                    + "has been growing for nine years."),
                F("clerk.2", Rank.Patala, 24,
                    "I have written the true figures three times. Each time the reply asks me "
                    + "to check my arithmetic.")
            }),

        new("fort.governor", "The Governor", "Vasumitra", "Governor", Rank.Patala,
            "Cold, and emptier than a governor's room should be.",
            "So you are the one who keeps coming back up.",
            new[]
            {
                F("gov.1", Rank.Patala, 22,
                    "I inherited an arrangement. I have kept it because the alternative is a "
                    + "province that starves in a season."),
                F("gov.2", Rank.Patala, 26,
                    "Uttara shut that door from the inside and it held five years. We did not "
                    + "lose it. We ordered it broken open, because the quota came."),
                F("gov.3", Rank.Patala, 30,
                    "You can expose it, take it, or end it. I have had thirty years to choose "
                    + "and I chose none of them.")
            })
    };

    public static IReadOnlyList<FortRoom> All => Rooms;

    public static FortRoom? Find(string? id) =>
        Rooms.FirstOrDefault(room => string.Equals(room.Id, id, StringComparison.Ordinal));

    /// <summary>The rooms a given standing can walk into.</summary>
    public static IReadOnlyList<FortRoom> OpenTo(Rank rank) =>
        Rooms.Where(room => room.IsOpen(rank)).ToList();

    /// <summary>Every fragment in the fort, for counting and for tests.</summary>
    public static IReadOnlyList<StoryFragment> AllFragments =>
        Rooms.SelectMany(room => room.Fragments).ToList();
}
