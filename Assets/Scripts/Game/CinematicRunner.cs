using System;
using System.Collections;
using UnityEngine;

/// <summary>Deterministic cue runner whose authored end state is identical on watch and skip.</summary>
public sealed class CinematicRunner : MonoBehaviour
{
    public static CinematicRunner Instance { get; private set; }
    public bool IsRunning { get; private set; }
    private bool _skipRequested;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void RequestSkip() => _skipRequested = true;

    public IEnumerator Play(CinematicSequence sequence)
    {
        if (sequence == null || IsRunning) yield break;
        var story = StoryDirector.Instance;
        string completionFlag = $"cinematic.{sequence.Id}.complete";
        if (story != null && story.HasFlag(completionFlag)) yield break;

        IsRunning = true;
        _skipRequested = false;
        int cueIndex = 0;
        float elapsed = 0f;
        var cues = (CinematicCue[])sequence.Cues.Clone();
        Array.Sort(cues, (a, b) => a.AtSeconds.CompareTo(b.AtSeconds));
        while (elapsed < sequence.Duration && !_skipRequested)
        {
            while (cueIndex < cues.Length && cues[cueIndex].AtSeconds <= elapsed)
                ApplyCue(cues[cueIndex++]);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Skipping executes every remaining state cue before the final contract.
        while (cueIndex < cues.Length) ApplyCue(cues[cueIndex++]);
        foreach (var flag in sequence.EndState)
            story?.SetFlag(flag.Id, flag.Value);
        story?.SetFlag(completionFlag);
        if (_skipRequested) story?.MarkCinematicSkipped(sequence.Id);
        IsRunning = false;
    }

    private static void ApplyCue(CinematicCue cue)
    {
        if (cue == null) return;
        var story = StoryDirector.Instance;
        switch (cue.Action)
        {
            case "set_flag": story?.SetFlag(cue.Key, cue.Value); break;
            case "advance_beat": story?.AdvanceTo(null, cue.Key, cue.Value); break;
            case "open_lock": story?.MarkOpened(cue.Key); break;
            case "loot": story?.MarkLooted(cue.Key); break;
            case "channeled":
                if (float.TryParse(cue.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var amount))
                    story?.AddChanneled(amount);
                break;
        }
    }
}
