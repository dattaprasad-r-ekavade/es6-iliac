using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RatnaBay.Domain;

/// <summary>The kinds of thing worth writing down. Deliberately few.</summary>
public static class PlayEventKind
{
    public const string RunStarted = "run.started";
    public const string RoomEntered = "room.entered";
    public const string RoomCleared = "room.cleared";

    /// <summary>The camp panel appeared. The clock to the answer starts here.</summary>
    public const string DecisionOffered = "decision.offered";
    public const string Camped = "decision.camped";
    public const string PressedOn = "decision.pressed";

    public const string EnemyKilled = "enemy.killed";

    /// <summary>
    /// A swing of the equipped weapon, landed or missed.
    ///
    /// Added after a session was read back as "no melee happened" when the player had in fact
    /// fought the last room entirely with the sword. Nothing recorded melee, and silence was
    /// mistaken for absence — which is the most dangerous failure a log can have.
    /// </summary>
    public const string MeleeSwing = "melee.swing";

    /// <summary>A spell that would not go off, almost always for want of prana.</summary>
    public const string CastFailed = "spell.failed";

    /// <summary>A cast bolt arrived on something.</summary>
    public const string SpellHit = "spell.hit";

    /// <summary>A door was opened, which is a commitment as much as an action.</summary>
    public const string DoorOpened = "door.opened";

    /// <summary>
    /// A periodic sample of where the player is standing and what is near them.
    ///
    /// Everything else in this log is an action. This is the only thing that records the
    /// spaces between them, which is where habits live — whether a fight is taken in a room
    /// or through the doorway of the one before it cannot be inferred from a list of swings.
    /// </summary>
    public const string Stance = "stance";
    public const string PlayerHurt = "player.hurt";
    public const string SpellCast = "spell.cast";
    public const string ItemUsed = "item.used";
    public const string Died = "player.died";


    /// <summary>The successor found the last one's body and carried it out.</summary>
    public const string CacheRecovered = "cache.recovered";
    public const string RunEnded = "run.ended";
}

/// <summary>One thing the player did, and when.</summary>
public sealed class PlayEvent
{
    /// <summary>Seconds since the recording started.</summary>
    public float At { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>What it was about — an enemy name, a spell, an item.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The number that matters for this kind: damage, stones, a room index.</summary>
    public float Value { get; set; }

    /// <summary>
    /// The second number, where one kind needs two — a seed and its tier, a pot and what the
    /// next room pays. A field rather than text packed into <see cref="Detail"/>, because a
    /// summariser that has to parse its own log is a summariser that will one day misparse it.
    /// </summary>
    public float Extra { get; set; }

    /// <summary>Health at the moment, because most questions turn out to be about pressure.</summary>
    public float Health { get; set; }

    /// <summary>Prana at the moment. Running dry is what forces a change of weapon.</summary>
    public float Prana { get; set; }

    /// <summary>What the event was aimed at or about — usually an enemy.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Metres between the player and the subject.
    ///
    /// The single most informative number in the log. Whether a player fights at two metres
    /// or twelve decides what the rooms are for, and no amount of counting swings reveals it.
    /// </summary>
    public float Distance { get; set; }
}

/// <summary>
/// A recording of one sitting.
///
/// This exists because the most important open question about the game — does the decision at
/// the door make anyone hesitate — cannot be answered by asking. A player who hesitated for
/// four seconds and one who slapped the button without reading remember it the same way. The
/// clock does not.
/// </summary>
public sealed class PlayRecording
{
    public int Version { get; set; } = 1;
    public string StartedUtc { get; set; } = string.Empty;
    public string Build { get; set; } = string.Empty;
    public List<PlayEvent> Events { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Serialize(PlayRecording recording) =>
        JsonSerializer.Serialize(recording, JsonOptions);

    public static bool TryLoad(string path, out PlayRecording? recording, out string error)
    {
        recording = null;
        error = string.Empty;

        try
        {
            if (!File.Exists(path))
            {
                error = $"No recording at {path}";
                return false;
            }

            recording = JsonSerializer.Deserialize<PlayRecording>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            error = $"Could not read recording: {exception.Message}";
            return false;
        }

        if (recording is not null) return true;

        error = "The recording is empty.";
        return false;
    }
}

/// <summary>What one descent looked like from the outside.</summary>
public sealed record RunReview(
    int Seed,
    int Tier,
    float StartedAt,
    float EndedAt,
    int RoomsCleared,
    int StonesBanked,
    int StonesLost,
    bool Survived,
    float DamageTaken,
    int EnemiesKilled,
    int MeleeSwings,
    int MeleeLanded,
    int SpellsCast,
    int CastsRefused,

    /// <summary>What killed what, by the thing that did the most damage to it.</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> KillsByWeapon,

    /// <summary>Median distance at which swings and casts were made.</summary>
    float MedianMeleeRange,
    float MedianSpellRange,

    /// <summary>Share of sampled time spent within a body's length of a doorway.</summary>
    float ShareOfTimeInDoorways,

    /// <summary>
    /// Rooms that were already empty by the time the player set foot in them.
    ///
    /// High numbers mean the player is fighting each room from the previous doorway rather
    /// than in it — a legitimate chokepoint tactic, but one that makes the shape of a room
    /// irrelevant, and worth knowing before any effort goes into shaping rooms.
    /// </summary>
    int RoomsTakenFromTheDoorway,

    IReadOnlyList<DecisionReview> Decisions,
    IReadOnlyList<float> RoomSeconds)
{
    public float Seconds => MathF.Max(0f, EndedAt - StartedAt);

    /// <summary>Did the run end at all, or did the recording stop mid-descent?</summary>
    public bool Finished => Survived || StonesLost > 0 || RoomsCleared > 0;
}

/// <summary>
/// One moment at a door: what was on the table, how long it took, and what was chosen.
/// <see cref="Hesitation"/> is the number this whole feature exists to produce.
/// </summary>
public sealed record DecisionReview(
    int RoomsCleared,
    int Pending,
    int NextPays,
    float Health,
    float Hesitation,
    bool PressedOn)
{
    /// <summary>
    /// The mine had nothing deeper, so camping was the only thing left to do.
    ///
    /// This matters more than it looks. Counting a forced camp as a choice to bank makes a
    /// player who pressed on at every real door look balanced, which is the exact opposite of
    /// what they did — and it would hide the finding the recorder exists to surface.
    /// </summary>
    public bool Forced => NextPays <= 0;
}

/// <summary>
/// Turns a recording into the handful of numbers worth arguing about.
///
/// Engine-free and tested, because the conclusions drawn from it will decide what gets built
/// next, and a summariser that quietly miscounts is worse than no telemetry at all.
/// </summary>
public static class PlayReview
{
    /// <summary>Longer than this at a door and the player was genuinely weighing it.</summary>
    public const float DeliberateSeconds = 2f;

    /// <summary>Under this and they did not read the panel.</summary>
    public const float ReflexSeconds = 0.6f;

    /// <summary>Cleared this soon after walking in and it was cleared before walking in.</summary>
    public const float FromTheDoorwaySeconds = 0.75f;

    public static IReadOnlyList<RunReview> Runs(PlayRecording recording)
    {
        var runs = new List<RunReview>();
        var events = (recording.Events ?? new List<PlayEvent>())
            .OrderBy(item => item.At)
            .ToList();

        var index = 0;
        while (index < events.Count)
        {
            if (events[index].Kind != PlayEventKind.RunStarted)
            {
                index++;
                continue;
            }

            runs.Add(ReadRun(events, ref index));
        }

        return runs;
    }

    private static RunReview ReadRun(List<PlayEvent> events, ref int index)
    {
        var start = events[index];
        var seed = (int)start.Value;
        var tier = (int)start.Extra;

        var decisions = new List<DecisionReview>();
        var roomSeconds = new List<float>();

        var roomsCleared = 0;
        var banked = 0;
        var lost = 0;
        var survived = false;
        var damage = 0f;
        var killCount = 0;
        var swings = 0;
        var landed = 0;
        var casts = 0;
        var refused = 0;
        var fromDoorway = 0;
        var endedAt = start.At;

        var kills = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var meleeRanges = new List<float>();
        var spellRanges = new List<float>();
        var stances = 0;
        var inDoorways = 0;

        // A room's real cost is measured from the moment the player committed to it — the
        // door closing behind them — not from when they finally walked in. Timing it from
        // entry reports zero for every room fought from a doorway, which is most of them.
        var segmentStart = start.At;

        float? offeredAt = null;
        PlayEvent? offered = null;
        float? roomStartedAt = null;

        // The room count of the last decision actually answered. A second offer at the same
        // count is the same door being re-advertised, not a new question — the panel can stay
        // up for a frame after the answer, and counting that would restart the clock at zero
        // and report a long deliberation as an instant one.
        var lastAnswered = -1;

        for (index++; index < events.Count; index++)
        {
            var item = events[index];
            if (item.Kind == PlayEventKind.RunStarted) break;

            endedAt = item.At;

            switch (item.Kind)
            {
                case PlayEventKind.RoomEntered:
                    roomStartedAt = item.At;
                    break;

                case PlayEventKind.RoomCleared:
                    roomsCleared++;
                    roomSeconds.Add(item.At - segmentStart);

                    // Cleared the instant they walked in means it was cleared before they did.
                    if (roomStartedAt is { } began && item.At - began <= FromTheDoorwaySeconds)
                        fromDoorway++;

                    roomStartedAt = null;
                    segmentStart = item.At;
                    break;

                case PlayEventKind.DecisionOffered:
                    // Only the first offer counts. Walking away from the door and back is
                    // still one decision, and re-arming the clock would flatter the number.
                    if (offeredAt is null && roomsCleared != lastAnswered)
                    {
                        offeredAt = item.At;
                        offered = item;
                    }

                    break;

                case PlayEventKind.PressedOn when SetSegment(item.At):
                case PlayEventKind.Camped:
                    if (offered is not null && offeredAt is { } shown)
                    {
                        decisions.Add(new DecisionReview(
                            RoomsCleared: roomsCleared,
                            Pending: (int)offered.Value,
                            NextPays: (int)offered.Extra,
                            Health: offered.Health,
                            Hesitation: MathF.Max(0f, item.At - shown),
                            PressedOn: item.Kind == PlayEventKind.PressedOn));
                    }

                    offeredAt = null;
                    offered = null;
                    lastAnswered = roomsCleared;
                    if (item.Kind == PlayEventKind.Camped)
                    {
                        banked = (int)item.Value;
                        survived = true;
                    }

                    break;

                case PlayEventKind.EnemyKilled:
                    killCount++;

                    // Detail is whatever did the most damage to it, not the final blow: a
                    // spell that softened something and a sword that finished it would
                    // otherwise be reported as a sword kill, which is the opposite of the
                    // tactic actually used.
                    if (!string.IsNullOrEmpty(item.Target))
                    {
                        var byWeapon = kills.TryGetValue(item.Target, out var found)
                            ? found
                            : kills[item.Target] = new Dictionary<string, int>(StringComparer.Ordinal);

                        var weapon = string.IsNullOrEmpty(item.Detail) ? "unknown" : item.Detail;
                        byWeapon[weapon] = byWeapon.GetValueOrDefault(weapon) + 1;
                    }

                    break;

                case PlayEventKind.MeleeSwing:
                    swings++;
                    // Extra carries whether it connected; a miss is still a swing.
                    if (item.Extra > 0f) landed++;
                    if (item.Distance > 0f) meleeRanges.Add(item.Distance);
                    break;

                case PlayEventKind.SpellCast:
                    casts++;
                    if (item.Distance > 0f) spellRanges.Add(item.Distance);
                    break;

                case PlayEventKind.Stance:
                    stances++;
                    // Extra is one when the player is standing in or beside a doorway.
                    if (item.Extra > 0f) inDoorways++;
                    break;

                case PlayEventKind.CastFailed:
                    refused++;
                    break;

                case PlayEventKind.PlayerHurt:
                    damage += item.Value;
                    break;

                case PlayEventKind.Died:
                    lost = (int)item.Value;
                    survived = false;
                    break;

                case PlayEventKind.RunEnded:
                    index++;
                    return Build();
            }
        }

        return Build();

        RunReview Build() => new(seed, tier, start.At, endedAt, roomsCleared, banked, lost,
            survived, damage, killCount, swings, landed, casts, refused,
            kills.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyDictionary<string, int>)entry.Value,
                StringComparer.Ordinal),
            Median(meleeRanges), Median(spellRanges),
            stances == 0 ? 0f : inDoorways / (float)stances,
            fromDoorway, decisions, roomSeconds);

        // The clock on the next room starts when the door opens, not when it is walked through.
        bool SetSegment(float at)
        {
            segmentStart = at;
            return true;
        }
    }

    private static float Median(List<float> values)
    {
        if (values.Count == 0) return 0f;

        values.Sort();
        return values.Count % 2 == 1
            ? values[values.Count / 2]
            : (values[values.Count / 2 - 1] + values[values.Count / 2]) * 0.5f;
    }

    /// <summary>Every decision across every run, for the only question that matters yet.</summary>
    public static IReadOnlyList<DecisionReview> AllDecisions(PlayRecording recording) =>
        Runs(recording).SelectMany(run => run.Decisions).ToList();

    /// <summary>
    /// The verdict on the loop.
    ///
    /// A player who always presses on is being paid too well; one who always camps is being
    /// asked to risk too much; one who answers inside a reflex never read the panel at all.
    /// </summary>
    public static string Verdict(IReadOnlyList<DecisionReview> all)
    {
        // Only doors with something behind them. Camping because the mine ran out is not a
        // decision, and letting it count as one flatters the loop.
        var decisions = all.Where(decision => !decision.Forced).ToList();

        if (decisions.Count == 0)
            return all.Count == 0
                ? "No decisions were reached. Nobody got to a door."
                : "No real decisions: every door reached was the last one. The mine is too short.";

        var pressed = decisions.Count(decision => decision.PressedOn);
        var reflex = decisions.Count(decision => decision.Hesitation < ReflexSeconds);
        var deliberate = decisions.Count(decision => decision.Hesitation >= DeliberateSeconds);

        if (pressed == decisions.Count)
            return decisions.Count == 1
                ? "The one real door was pressed through. Not enough to judge the curve."
                : "Always pressed on. The payout is too generous, or the risk is not felt.";

        if (pressed == 0)
            return "Never pressed on. The pot is too precious to stake, or the fights are too costly.";

        if (reflex > decisions.Count / 2)
            return "Answered on reflex. The panel is not being read — it is a door, not a decision.";

        if (deliberate > decisions.Count / 2)
            return "Genuinely weighed. The decision is working; build on it.";

        return "Mixed, but quick. The choice is being made but not agonised over.";
    }
}
