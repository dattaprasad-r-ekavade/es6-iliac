using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RatnaBay.Client;

/// <summary>
/// The lines a player is shown once, the first time each thing happens to them.
///
/// Not a tutorial level and not a wall of text on a modal. A stranger downloading an alpha will
/// dismiss a wall of text unread and then not know what a jiva stone is, which produces a short
/// recording and no answer to any question worth asking. Instead each idea arrives at the exact
/// moment it becomes true — standing at the shaft, watching the first body come up out of the
/// floor, looking at the first shut door with something in the pot.
///
/// Every line is shown at most once, ever, and is remembered per installation rather than per
/// save. Somebody starting a second character has already learned what a stone is.
/// </summary>
public sealed class Coach
{
    private const string FileName = "learned.json";

    /// <summary>How long a line stays up. Long enough to read twice without hurrying.</summary>
    private const float ShownSeconds = 8f;

    /// <summary>Nothing new arrives until the last one has had this long on screen.</summary>
    private const float MinimumGap = 1.2f;

    private readonly HashSet<string> _learned;
    private readonly Queue<string> _waiting = new();
    private float _remaining;

    private Coach(HashSet<string> learned) => _learned = learned;

    /// <summary>The line on screen, or empty when there is nothing to say.</summary>
    public string Line { get; private set; } = string.Empty;

    /// <summary>Fades in and out at the ends rather than snapping.</summary>
    public float Opacity => _remaining <= 0f
        ? 0f
        : MathF.Min(1f, MathF.Min(_remaining, ShownSeconds - _remaining) * 3f + 0.15f);

    public static Coach Load()
    {
        try
        {
            var path = Path.Combine(GameSession.SaveDirectory, FileName);
            if (File.Exists(path))
            {
                var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
                if (ids is not null) return new Coach(new HashSet<string>(ids, StringComparer.Ordinal));
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            // Having forgotten what a player has been told is a small problem. Refusing to
            // start because of it would be a large one.
        }

        return new Coach(new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Say this, if it has never been said.
    ///
    /// Safe to call every frame from wherever the condition is naturally known — the guard is
    /// here rather than at each call site, so a trigger can be written where it is obvious
    /// rather than where it is convenient.
    /// </summary>
    public void Teach(string id, string line)
    {
        if (string.IsNullOrWhiteSpace(id) || !_learned.Add(id)) return;

        _waiting.Enqueue(line);
        Save();
    }

    /// <summary>Has this already been said? For triggers that need to fire only once.</summary>
    public bool HasLearned(string id) => _learned.Contains(id);

    public void Tick(float deltaSeconds)
    {
        if (_remaining > 0f)
        {
            _remaining = MathF.Max(0f, _remaining - deltaSeconds);

            // A queued line waits for the last one to be nearly gone. Two pieces of advice at
            // once is one piece of advice nobody reads.
            if (_remaining > MinimumGap) return;
        }

        if (_waiting.Count == 0)
        {
            if (_remaining <= 0f) Line = string.Empty;
            return;
        }

        Line = _waiting.Dequeue();
        _remaining = ShownSeconds;
    }

    /// <summary>Forget everything, so the next run teaches it all again.</summary>
    public void Reset()
    {
        _learned.Clear();
        _waiting.Clear();
        Line = string.Empty;
        _remaining = 0f;
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(GameSession.SaveDirectory);
            File.WriteAllText(Path.Combine(GameSession.SaveDirectory, FileName),
                JsonSerializer.Serialize(_learned));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Worst case the line is shown again next time, which is not worth interrupting
            // anybody over.
        }
    }
}

/// <summary>
/// Every line, in one place, so the whole of what a first-time player is told can be read at
/// once rather than assembled from a dozen call sites.
///
/// Written to be true rather than encouraging. A stranger has about ninety seconds of patience,
/// and the fastest way to spend it is to explain something they can already see.
/// </summary>
public static class Lessons
{
    public const string Yard = "yard.arrive";
    public const string Shaft = "yard.shaft";
    public const string Stall = "yard.stall";
    public const string FirstRoom = "mine.room";
    public const string Rising = "mine.rising";
    public const string FirstDoor = "mine.door";
    public const string Trader = "mine.trader";
    public const string Banked = "run.banked";
    public const string Died = "run.died";
    public const string Body = "run.body";

    public static string TextOf(string id) => id switch
    {
        Yard => "You clear the mines under Ratna Bay. Go down, kill what rises, "
            + "come back with more than you took.",

        Shaft => "The shallow shaft is free. Deeper ones cost jiva stones — "
            + "the same stones you go down to fetch.",

        Stall => "Gold buys gear here, and gear is the only thing a death cannot take from you.",

        FirstRoom => "The door shut behind you. A room pays when nothing in it is standing.",

        Rising => "They are still getting up. A blow now lands for double.",

        FirstDoor => "Bank what you are holding and the run ends here. Open the door and "
            + "the next room pays more — but dying loses every stone.",

        Trader => "Somebody will come down for the right money. It comes out of the pot, "
            + "and the next one costs more.",

        Banked => "Those stones are yours now. Spend them on a deeper shaft, "
            + "or on gear at the stall.",

        Died => "Someone else takes the lamp. They keep your rank and half your pack.",

        Body => "Your predecessor is still down there with everything they were carrying. "
            + "Go and get it.",

        _ => string.Empty
    };
}
