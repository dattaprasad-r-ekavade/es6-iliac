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
    private int _sinceFlush;
    private bool _broken;

    private PlayRecorder(string path)
    {
        _path = path;
        _recording.StartedUtc = _started.ToString("O");
        _recording.Build = typeof(PlayRecorder).Assembly.GetName().Version?.ToString() ?? "dev";
    }

    public static string Directory =>
        System.IO.Path.Combine(GameSession.SaveDirectory, "recordings");

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

        if (++_sinceFlush < FlushEvery) return;
        Flush();
    }

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
