using System;
using UnityEngine;

[Serializable]
public sealed class CinematicCue
{
    public float AtSeconds;
    public string Action;
    public string Key;
    public string Value;
}

[CreateAssetMenu(menuName = "Kessil/Cinematic Sequence", fileName = "CinematicSequence")]
public sealed class CinematicSequence : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private float duration;
    [SerializeField] private CinematicCue[] cues;
    [SerializeField] private StoryFlag[] endState;
    public string Id => id;
    public float Duration => duration;
    public CinematicCue[] Cues => cues ?? Array.Empty<CinematicCue>();
    public StoryFlag[] EndState => endState ?? Array.Empty<StoryFlag>();

    public void Configure(string sequenceId, float seconds, CinematicCue[] sequenceCues, StoryFlag[] finalState)
    {
        id = sequenceId; duration = Mathf.Max(0f, seconds); cues = sequenceCues; endState = finalState;
    }
}
