using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client.World;

/// <summary>
/// The live, reloadable bridge from a location's dialogue JSON to the player's topic service.
/// The domain owns answers and knowledge; this class owns authored actor placement and target
/// selection for the current scene.
/// </summary>
public sealed class DialogueRuntime
{
    private readonly string _manifestPath;
    private readonly TopicDialogueService _dialogue;
    private DateTime _lastWriteUtc;
    private List<SpeakingActor> _actors = new();

    private DialogueRuntime(string manifestPath, DialogueManifest manifest,
        TopicDialogueService dialogue)
    {
        _manifestPath = manifestPath;
        _dialogue = dialogue;
        Manifest = manifest;
        ApplyManifest(manifest);
    }

    public DialogueManifest Manifest { get; private set; }
    public IReadOnlyList<SpeakingActor> Actors => _actors;
    public string ManifestPath => _manifestPath;

    public static bool TryLoad(string path, TopicDialogueService dialogue,
        out DialogueRuntime? runtime, out string error)
    {
        runtime = null;
        if (dialogue is null)
        {
            error = "Dialogue runtime requires a topic service.";
            return false;
        }

        if (!DialogueManifest.TryLoad(path, out var manifest, out error)) return false;

        runtime = new DialogueRuntime(Path.GetFullPath(path), manifest!, dialogue);
        return true;
    }

    /// <summary>Reload once after an edit; a malformed edit leaves the current actors active.</summary>
    public bool TryReloadIfChanged(out string message)
    {
        message = string.Empty;
        DateTime writeUtc;
        try { writeUtc = File.GetLastWriteTimeUtc(_manifestPath); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

        if (writeUtc == _lastWriteUtc) return false;
        _lastWriteUtc = writeUtc;

        if (!DialogueManifest.TryLoad(_manifestPath, out var manifest, out var error))
        {
            message = error;
            return false;
        }

        Manifest = manifest!;
        ApplyManifest(Manifest);
        message = $"Reloaded {Manifest.Id}.";
        return true;
    }

    /// <summary>Find the nearest actor inside the facing cone and interaction range.</summary>
    /// <summary>What an actor is carrying loose, as authored. Null when nothing is.</summary>
    public DialoguePocketDefinition? PocketOf(string actorId) =>
        Manifest.Actors.FirstOrDefault(a =>
            string.Equals(a.Id, actorId, StringComparison.Ordinal))?.Pocket;

    public SpeakingActor? FindActor(WorldPoint player, float yaw, float range = 3.2f)
    {
        var forward = Targeting.FlatForward(yaw);
        SpeakingActor? best = null;
        var bestDistance = float.MaxValue;

        foreach (var actor in _actors)
        {
            var distance = player.FlatDistanceTo(actor.Position);
            if (distance > range || distance >= bestDistance) continue;

            var dx = actor.Position.X - player.X;
            var dz = actor.Position.Z - player.Z;
            if (distance > 0.001f && (dx * forward.X + dz * forward.Z) / distance < 0.45f)
                continue;

            best = actor;
            bestDistance = distance;
        }

        return best;
    }

    private void ApplyManifest(DialogueManifest manifest)
    {
        _dialogue.Load(manifest.ToTopics());
        _actors = (manifest.Actors ?? new List<DialogueActorDefinition>())
            .Select(definition =>
            {
                var actor = new SpeakingActor(_dialogue, definition.Id, definition.DisplayName,
                    definition.FactionId, definition.LocationId,
                    (definition.OpensWith ?? new List<string>()).ToArray())
                {
                    Position = definition.Position.ToWorldPoint(),
                    Height = definition.Height,
                    Palette = definition.Palette
                };
                return actor;
            })
            .ToList();

        try { _lastWriteUtc = File.GetLastWriteTimeUtc(_manifestPath); }
        catch (IOException) { _lastWriteUtc = DateTime.MinValue; }
        catch (UnauthorizedAccessException) { _lastWriteUtc = DateTime.MinValue; }
    }
}
