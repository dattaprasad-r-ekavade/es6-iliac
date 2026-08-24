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
    private readonly string _saveFilePath;

    private GameSession(PlayerCharacter player, string saveFilePath)
    {
        Player = player;
        _saveFilePath = saveFilePath;
        Subscribe();
    }

    public PlayerCharacter Player { get; }

    /// <summary>Where the player is standing. The domain deliberately does not know.</summary>
    public WorldPoint Position { get; set; }

    public float Yaw { get; set; }
    public float Pitch { get; set; }

    /// <summary>
    /// A descent that was put down rather than finished, if there is one.
    ///
    /// Held on the session rather than the player because it is a fact about this save file,
    /// not about the person carrying the lamp.
    /// </summary>
    public SavedDescent? Descent { get; set; }

    /// <summary>True when Continue should walk back into a mine rather than into the town.</summary>
    public bool HasSuspendedDescent => Descent is { IsValid: true };

    public IReadOnlyList<Toast> Toasts => _toasts;

    public static string SaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RatnaBay");

    public static string SaveFilePath => Path.Combine(SaveDirectory, "ratnabay_save.json");

    public static bool HasSaveFile => File.Exists(SaveFilePath) || File.Exists(SaveFilePath + ".bak");

    /// <summary>
    /// Whether the save on disk holds a descent, without loading it.
    ///
    /// The menu has to label its first entry before anything has been read, and offering
    /// "Continue" to somebody who is actually halfway down a mine is how the whole entry flow
    /// became confusing in the first place.
    /// </summary>
    public static bool PeekHasSuspendedDescent(string? saveFilePath = null)
    {
        var target = string.IsNullOrWhiteSpace(saveFilePath) ? SaveFilePath : saveFilePath;

        foreach (var path in new[] { target, target + ".bak" })
        {
            try
            {
                if (!File.Exists(path)) continue;
                if (!SaveGame.TryRead(File.ReadAllText(path), out var data, out _)) continue;
                return data!.Descent is { IsValid: true };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // An unreadable save is a problem for the loader to report, not for a label.
            }
        }

        return false;
    }

    /// <summary>
    /// A custom path keeps automated checks completely separate from the player's real slot.
    /// Normal callers omit it and use the AppData save.
    /// </summary>
    public static GameSession NewGame(string? saveFilePath = null) =>
        new(PlayerCharacter.NewGame(), saveFilePath ?? SaveFilePath);

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
        var temporaryPath = _saveFilePath + ".tmp";
        var backupPath = _saveFilePath + ".bak";
        try
        {
            var directory = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var data = SaveGame.Capture(Player, Position, Yaw, sceneId: "scene.northwatch");
            data.Descent = Descent;
            var json = SaveGame.Serialize(data);
            if (!SaveGame.TryRead(json, out _, out var validationError))
                return $"Could not save: {validationError}";

            File.WriteAllText(temporaryPath, json);
            if (!SaveGame.TryRead(File.ReadAllText(temporaryPath), out _, out validationError))
                return $"Could not save: validation failed after writing ({validationError})";

            // Replace the live file only after the complete JSON is on disk. A crashed write
            // must leave the previous evening loadable rather than leaving a zero-byte slot.
            if (File.Exists(_saveFilePath))
                File.Replace(temporaryPath, _saveFilePath, backupPath,
                    ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, _saveFilePath);

            return "Saved.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {exception.Message}";
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// Read the save into this session. A damaged primary slot falls back to the last
    /// successfully replaced file, and callers can keep the current game running on failure.
    /// </summary>
    public bool TryLoad(out string message)
    {
        var primaryExists = File.Exists(_saveFilePath);
        if (TryReadSave(_saveFilePath, out var data, out var primaryError))
        {
            return TryRestore(data!, "Loaded.", out message);
        }

        var backupPath = _saveFilePath + ".bak";
        if (TryReadSave(backupPath, out data, out var backupError))
        {
            return TryRestore(data!, "Loaded the previous backup; the latest save was unreadable.",
                out message);
        }

        message = !primaryExists && !File.Exists(backupPath)
            ? "There is no save to load."
            : $"Could not load the save: {primaryError} Backup: {backupError}";
        return false;
    }

    /// <summary>Compatibility wrapper for the headless checks and simple callers.</summary>
    public string Load()
    {
        TryLoad(out var message);
        return message;
    }

    /// <summary>
    /// Apply the durable result of a descent and checkpoint it at the surface.
    ///
    /// The run ledger owns whether stones were carried out or lost. The session owns where
    /// durable inventory lives and where it is written. Keeping this transaction here prevents
    /// a summary screen from saying "banked" while the next world silently starts a new pack.
    /// </summary>
    public string CompleteRun(RunResult result, WorldPoint surfacePosition, float surfaceYaw = 0f)
    {
        if (result.Outcome == RunOutcome.InProgress) return "The run is still in progress.";

        if (result.StonesCarriedOut > 0)
            Player.Inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName,
                result.StonesCarriedOut, SoulCrystals.ItemKind);

        // A finished descent is not a suspended one. Leaving the block behind would offer
        // "resume" for a run that has already been banked or buried.
        Descent = null;

        // Both outcomes return to the surface. Health and stamina are restored there; prana
        // deliberately is not, because FullRestore preserves the stone economy.
        Player.Vitals.FullRestore();
        Player.Combat.ClearCombat();
        Position = surfacePosition;
        Yaw = surfaceYaw;
        Pitch = 0f;

        return Save();
    }

    /// <summary>
    /// Put a descent down and write it to disk.
    ///
    /// This is the only way a run in progress reaches a save file. A manual save mid-descent
    /// stays refused, because that is a reload button and the entire loop rests on not having
    /// one; putting the game down and picking it up again is a different thing entirely.
    /// </summary>
    public string Suspend(SavedDescent descent, WorldPoint position, float yaw, float pitch)
    {
        if (descent is null || !descent.IsValid) return "There is no descent to put down.";

        Descent = descent;
        Position = position;
        Yaw = yaw;
        Pitch = pitch;

        var message = Save();
        return message == "Saved." ? "Descent set aside. It will be here." : message;
    }

    /// <summary>
    /// Take the descent off the save the moment it is walked back into.
    ///
    /// Consuming it here is what keeps suspend from becoming a reload: once resumed, the file
    /// no longer holds a copy to go back to, so the only ways out of a mine remain camping and
    /// dying.
    /// </summary>
    public string ConsumeDescent()
    {
        if (Descent is null) return string.Empty;

        Descent = null;
        return Save();
    }

    private bool TryRestore(SaveData data, string successMessage, out string message)
    {
        try
        {
            SaveGame.Restore(Player, data);
        }
        catch (ArgumentException exception)
        {
            message = exception.Message;
            return false;
        }

        Position = new WorldPoint(data.PlayerX, data.PlayerY, data.PlayerZ);
        Yaw = data.PlayerYaw;
        Descent = data.Descent is { IsValid: true } descent ? descent : null;
        message = successMessage;
        return true;
    }

    private static bool TryReadSave(string path, out SaveData? data, out string error)
    {
        data = null;
        if (!File.Exists(path))
        {
            error = "File not found.";
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }

        return SaveGame.TryRead(json, out data, out error);
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
