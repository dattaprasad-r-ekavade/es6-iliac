using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

/// <summary>One message on screen, with the time it has left.</summary>
public sealed class Toast
{
    public required string Message { get; init; }
    public float Remaining { get; set; }
}

/// <summary>
/// The bridge between the tested domain and the running game.
///
/// Everything the player *is* lives in <see cref="PlayerCharacter"/>; this owns the things
/// that only make sense with a window attached — where they are standing, which file the save
/// goes to, and what is currently on screen. Nothing here contains a game rule.
/// </summary>
public sealed class GameSession
{
    /// <summary>How long a message stays up.</summary>
    private const float ToastDuration = 3.5f;

    /// <summary>Keeps a burst of level-ups from filling the screen.</summary>
    private const int MaxToasts = 5;

    private readonly List<Toast> _toasts = new();

    private GameSession(PlayerCharacter player)
    {
        Player = player;
        Subscribe();
    }

    public PlayerCharacter Player { get; }

    /// <summary>Where the player is standing. The domain deliberately does not know.</summary>
    public WorldPoint Position { get; set; }

    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public IReadOnlyList<Toast> Toasts => _toasts;

    public static string SaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RatnaBay");

    public static string SaveFilePath => Path.Combine(SaveDirectory, "ratnabay_save.json");

    public static bool HasSaveFile => File.Exists(SaveFilePath);

    public static GameSession NewGame() => new(PlayerCharacter.NewGame());

    /// <summary>
    /// Advance the whole session. The domain owns what changes; this owns the clock and the
    /// message queue.
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        Player.Tick(deltaSeconds);
        TickToasts(deltaSeconds);
    }

    public void ShowToast(string message)
    {
        _toasts.Add(new Toast { Message = message, Remaining = ToastDuration });
        if (_toasts.Count > MaxToasts) _toasts.RemoveRange(0, _toasts.Count - MaxToasts);
    }

    /// <summary>
    /// Write the save. Returns a message fit to put on screen either way — a save that fails
    /// silently is how a player loses an evening.
    /// </summary>
    public string Save()
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            var data = SaveGame.Capture(Player, Position, Yaw, sceneId: "scene.northwatch");
            File.WriteAllText(SaveFilePath, SaveGame.Serialize(data));
            return "Saved.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {exception.Message}";
        }
    }

    /// <summary>Read the save into this session. The message is fit to put on screen.</summary>
    public string Load()
    {
        string json;
        try
        {
            if (!HasSaveFile) return "There is no save to load.";
            json = File.ReadAllText(SaveFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Could not read the save: {exception.Message}";
        }

        if (!SaveGame.TryRead(json, out var data, out var error)) return error;

        SaveGame.Restore(Player, data!);
        Position = new WorldPoint(data!.PlayerX, data.PlayerY, data.PlayerZ);
        Yaw = data.PlayerYaw;
        return "Loaded.";
    }

    private void Subscribe()
    {
        // The domain raises facts; turning them into words is a presentation decision, so it
        // happens here rather than inside the rules.
        Player.Skills.SkillRaised += (skillId, level) =>
            ShowToast($"{Skills.Label(skillId)} increased to {level}.");

        Player.Vitals.LevelGained += level => ShowToast($"Level up. You are now level {level}.");
        Player.Vitals.CrystalDrawn += () => ShowToast("You draw on a jiva stone.");
        Player.Vitals.Died += () => ShowToast("You were defeated.");
        Player.Quests.QuestCompleted += quest => ShowToast($"Quest complete: {quest.Title}");

        Player.Detection.AwarenessChanged += awareness => ShowToast(awareness switch
        {
            AwarenessLevel.Suspicious => "Someone thinks they saw something.",
            AwarenessLevel.Alerted => "You have been seen.",
            _ => "The alarm dies down."
        });
    }

    private void TickToasts(float deltaSeconds)
    {
        for (var index = _toasts.Count - 1; index >= 0; index--)
        {
            _toasts[index].Remaining -= deltaSeconds;
            if (_toasts[index].Remaining <= 0f) _toasts.RemoveAt(index);
        }
    }
}
