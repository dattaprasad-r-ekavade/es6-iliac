using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

/// <summary>
/// Writes down what the player did, so it can be read back afterwards.
///
/// The point is not analytics. It is that the open question about this game — whether the
/// decision at the door makes anybody hesitate — cannot be answered by asking, because a
/// player who agonised for four seconds and one who slapped the button remember it the same
/// way. The clock does not.
///
/// It never throws and never blocks the game: a recorder that can cost a frame or crash a run
/// would be worse than having none, and it would be switched off within a week.
/// </summary>
public sealed class PlayRecorder
{
    /// <summary>Written after this many new events, so a crash still leaves most of it.</summary>
    private const int FlushEvery = 25;

    private readonly PlayRecording _recording = new();
    private readonly string _path;
    private DateTime _started = DateTime.UtcNow;
    private DateTime _lastProgress = DateTime.UtcNow;
    private int _sinceFlush;
    private bool _broken;

    private PlayRecorder(string path)
    {
        _path = path;
        _recording.StartedUtc = _started.ToString("O");
        _recording.Build = Telemetry.Version;
    }

    public static string Directory =>
        System.IO.Path.Combine(GameSession.SaveDirectory, "recordings");

    /// <summary>
    /// The recordings folder as it should be shown to a player, rather than as it is on disk.
    ///
    /// The expanded path names whoever is running the build, and the screen it appears on is
    /// the help screen — which is exactly the screen that ends up in a store screenshot, next
    /// to a page promising no account, no email and no name.
    ///
    /// <c>%APPDATA%</c> is also the form that pastes straight into an Explorer address bar,
    /// and the form the install instructions already give, so the string that keeps the
    /// account name off the glass is the more useful one anyway.
    /// </summary>
    public static string DisplayDirectory
    {
        get
        {
            var full = Directory;
            if (!OperatingSystem.IsWindows()) return full;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return !string.IsNullOrEmpty(appData)
                   && full.StartsWith(appData, StringComparison.OrdinalIgnoreCase)
                ? "%APPDATA%" + full[appData.Length..]
                : full;
        }
    }

    /// <summary>
    /// The file this sitting is writing to, so the uploader can leave it alone.
    ///
    /// The sweep used to identify the in-progress recording by asking whether it had been
    /// touched in the last two minutes, which is a guess standing in for a fact the process
    /// already holds. It also made sending on the way out impossible: the recording had just
    /// been written, so it always looked like the live one and was always skipped.
    /// </summary>
    public string FilePath => _path;
    public int Count => _recording.Events.Count;

    /// <summary>One file per sitting, named so the newest sorts last.</summary>
    public static PlayRecorder Start()
    {
        var name = $"play_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        return new PlayRecorder(System.IO.Path.Combine(Directory, name));
    }

    public void Record(string kind, string detail = "", float value = 0f, float extra = 0f,
        float health = 0f, float prana = 0f, string target = "", float distance = 0f)
    {
        if (_broken) return;

        _recording.Events.Add(new PlayEvent
        {
            At = (float)(DateTime.UtcNow - _started).TotalSeconds,
            Kind = kind,
            Detail = detail ?? string.Empty,
            Value = value,
            Extra = extra,
            Health = health,
            Prana = prana,
            Target = target ?? string.Empty,
            Distance = distance
        });

        if (IsProgress(kind)) _lastProgress = DateTime.UtcNow;

        if (++_sinceFlush < FlushEvery) return;
        Flush();
    }

    /// <summary>
    /// How long since the player last got anywhere.
    ///
    /// Measured here rather than in Game1 because the recorder already sees every event, so
    /// the definition of progress cannot drift away from the list of things that are recorded
    /// as progress.
    /// </summary>
    public TimeSpan SinceProgress => DateTime.UtcNow - _lastProgress;

    /// <summary>
    /// What counts as getting somewhere.
    ///
    /// Swinging is not on this list, and that is the point: the one player who was stuck swung
    /// sixty times without ever hitting anything. Activity is not progress, and a definition
    /// that counted it would have called that session busy.
    /// </summary>
    private static bool IsProgress(string kind) =>
        kind is PlayEventKind.RunStarted
             or PlayEventKind.RoomEntered
             or PlayEventKind.RoomCleared
             or PlayEventKind.EnemyKilled
             or PlayEventKind.DoorOpened
             or PlayEventKind.DecisionOffered
             or PlayEventKind.Camped
             or PlayEventKind.PressedOn;

    /// <summary>
    /// Put it on disk. Failure disables the recorder rather than reporting itself: a player
    /// mid-descent has no use for a message about telemetry.
    /// </summary>
    public void Flush()
    {
        if (_broken || _recording.Events.Count == 0) return;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(_path, PlayRecording.Serialize(_recording));
            _sinceFlush = 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _broken = true;
        }
    }

    /// <summary>The newest recording on disk, for the review tool.</summary>
    public static string? Newest()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return null;

            var files = new List<string>(System.IO.Directory.GetFiles(Directory, "play_*.json"));
            files.Sort(StringComparer.Ordinal);
            return files.Count == 0 ? null : files[^1];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
