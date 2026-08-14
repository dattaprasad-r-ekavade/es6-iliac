using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Morrowind-style shared keyword knowledge base, never a conversation tree.</summary>
public sealed class TopicDialogueService : MonoBehaviour
{
    [SerializeField] private DialogueTopic[] topics;
    private readonly HashSet<string> _knownTopics = new(StringComparer.OrdinalIgnoreCase);
    public static TopicDialogueService Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (topics == null || topics.Length == 0)
            topics = Resources.LoadAll<DialogueTopic>("Data/Dialogue");
        // Resources.LoadAll does not promise an order. Stable ordering also makes an
        // accidental equal-specificity resolver deterministic while authoring catches up.
        Array.Sort(topics, (a, b) => string.Compare(a?.Id, b?.Id, StringComparison.Ordinal));
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public IReadOnlyCollection<string> KnownTopics => _knownTopics;
    public bool KnowsTopic(string keyword) => !string.IsNullOrWhiteSpace(keyword) && _knownTopics.Contains(keyword);
    public void LearnTopic(string keyword)
    {
        if (!string.IsNullOrWhiteSpace(keyword)) _knownTopics.Add(keyword);
    }

    public List<string> CaptureKnownTopics()
    {
        var result = new List<string>(_knownTopics);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public void RestoreKnownTopics(IEnumerable<string> keywords)
    {
        _knownTopics.Clear();
        if (keywords == null) return;
        foreach (var keyword in keywords) LearnTopic(keyword);
    }

    public DialogueTopic Resolve(string keyword, DialogueContext context)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return null;
        DialogueTopic best = null;
        int bestSpecificity = -1;
        foreach (var topic in topics ?? Array.Empty<DialogueTopic>())
        {
            if (topic == null || !string.Equals(topic.Keyword, keyword, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(topic.ActorId) && topic.ActorId != context?.ActorId) continue;
            if (!string.IsNullOrEmpty(topic.FactionId) && topic.FactionId != context?.FactionId) continue;
            if (!ConditionsPass(topic.Conditions, context)) continue;
            int specificity = topic.Conditions.Count
                              + (string.IsNullOrEmpty(topic.ActorId) ? 0 : 2)
                              + (string.IsNullOrEmpty(topic.FactionId) ? 0 : 1);
            if (specificity > bestSpecificity
                || (specificity == bestSpecificity
                    && string.Compare(topic.Id, best?.Id, StringComparison.Ordinal) < 0))
            {
                best = topic;
                bestSpecificity = specificity;
            }
        }
        return best;
    }

    public string Respond(string keyword, DialogueContext context)
    {
        if (!KnowsTopic(keyword)) return null;
        var topic = Resolve(keyword, context);
        if (topic == null) return null;
        LearnTopic(topic.Keyword);
        StoryDirector.Instance?.RecordChoice($"topic.{topic.Id}", topic.Keyword);
        return topic.Response;
    }

    private static bool ConditionsPass(IReadOnlyList<DialogueCondition> conditions, DialogueContext context)
    {
        foreach (var condition in conditions)
        {
            string actual = ResolveValue(condition.Key, context);
            if (!Compare(actual, condition.Operator, condition.Value)) return false;
        }
        return true;
    }

    private static string ResolveValue(string key, DialogueContext context)
    {
        var story = StoryDirector.Instance?.State;
        if (key == "route") return story?.RouteId ?? string.Empty;
        if (key == "evidence_count") return (story?.Evidence.Count ?? 0).ToString();
        if (key == "disposition") return (context?.Disposition ?? 0).ToString();
        if (key == "faction") return context?.FactionId ?? string.Empty;
        if (key == "location") return context?.LocationId ?? string.Empty;
        if (key == "player.channeled") return (story?.PlayerChanneled ?? 0f).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (key != null && key.StartsWith("flag.", StringComparison.Ordinal))
            return story?.Flags.Find(f => f.Id == key)?.Value ?? string.Empty;
        return string.Empty;
    }

    private static bool Compare(string actual, string op, string expected)
    {
        if (op == "not_equals") return !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        if (op == "min" || op == "max")
        {
            if (!float.TryParse(actual, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var a)
                || !float.TryParse(expected, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var b)) return false;
            return op == "min" ? a >= b : a <= b;
        }
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
