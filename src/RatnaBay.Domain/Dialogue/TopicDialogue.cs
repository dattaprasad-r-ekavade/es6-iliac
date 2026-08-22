using System.Globalization;

namespace RatnaBay.Domain;

public enum ConditionOperator
{
    Equals,
    NotEquals,

    /// <summary>Actual must be at least the expected number.</summary>
    Min,

    /// <summary>Actual must be at most the expected number.</summary>
    Max
}

public sealed class DialogueCondition
{
    public required string Key { get; init; }
    public ConditionOperator Operator { get; init; } = ConditionOperator.Equals;
    public string Value { get; init; } = string.Empty;
}

/// <summary>One answer, owned by the shared pool rather than by a conversation tree.</summary>
public sealed class DialogueTopic
{
    public required string Id { get; init; }
    public required string Keyword { get; init; }

    /// <summary>Empty means any actor can answer this.</summary>
    public string ActorId { get; init; } = string.Empty;

    /// <summary>Empty means any faction can answer this.</summary>
    public string FactionId { get; init; } = string.Empty;

    public required string Response { get; init; }

    public IReadOnlyList<DialogueCondition> Conditions { get; init; } = Array.Empty<DialogueCondition>();

    /// <summary>
    /// How specific this entry is. The most specific match wins, so a named character can
    /// override the generic line without the generic line having to know about them.
    /// </summary>
    public int Specificity =>
        Conditions.Count
        + (string.IsNullOrEmpty(ActorId) ? 0 : 2)
        + (string.IsNullOrEmpty(FactionId) ? 0 : 1);
}

/// <summary>Who is being asked, and where.</summary>
public sealed class DialogueContext
{
    public string ActorId { get; init; } = string.Empty;
    public string FactionId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public int Disposition { get; init; } = 50;
}

/// <summary>
/// Morrowind-style shared keyword knowledge base, never a conversation tree.
///
/// The player learns a keyword from one person and takes it to another; that is the core
/// verb rather than a feature. Topics offered are always filtered through the same resolver
/// the response uses, so a menu can never offer a keyword that produces silence.
/// </summary>
public sealed class TopicDialogueService
{
    private readonly List<DialogueTopic> _topics = new();
    private readonly HashSet<string> _knownTopics = new(StringComparer.OrdinalIgnoreCase);
    private readonly StoryDirector _story;

    public TopicDialogueService(StoryDirector story) => _story = story;

    public IReadOnlyCollection<string> KnownTopics => _knownTopics;

    public void Load(IEnumerable<DialogueTopic> topics)
    {
        _topics.Clear();
        _topics.AddRange(topics);
        // Stable ordering keeps an accidental equal-specificity tie deterministic.
        _topics.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    public bool KnowsTopic(string? keyword) =>
        !string.IsNullOrWhiteSpace(keyword) && _knownTopics.Contains(keyword);

    public void LearnTopic(string? keyword)
    {
        if (!string.IsNullOrWhiteSpace(keyword)) _knownTopics.Add(keyword);
    }

    /// <summary>The most specific topic this actor can answer for a keyword, or null.</summary>
    public DialogueTopic? Resolve(string? keyword, DialogueContext? context)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return null;

        DialogueTopic? best = null;
        var bestSpecificity = -1;

        foreach (var topic in _topics)
        {
            if (!string.Equals(topic.Keyword, keyword, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(topic.ActorId) && topic.ActorId != context?.ActorId) continue;
            if (!string.IsNullOrEmpty(topic.FactionId) && topic.FactionId != context?.FactionId) continue;
            if (!ConditionsPass(topic.Conditions, context)) continue;

            if (topic.Specificity <= bestSpecificity) continue;
            best = topic;
            bestSpecificity = topic.Specificity;
        }

        return best;
    }

    /// <summary>
    /// Ask about a keyword. Null when the player does not know it or this actor has nothing
    /// to say about it.
    /// </summary>
    public string? Respond(string? keyword, DialogueContext? context)
    {
        if (!KnowsTopic(keyword)) return null;

        var topic = Resolve(keyword, context);
        if (topic is null) return null;

        LearnTopic(topic.Keyword);
        _story.RecordChoice($"topic.{topic.Id}", topic.Keyword);
        return topic.Response;
    }

    /// <summary>Keywords this actor will actually answer right now.</summary>
    public IReadOnlyList<string> AvailableTopics(DialogueContext context)
    {
        var available = _knownTopics
            .Where(keyword => Resolve(keyword, context) is not null)
            .ToList();

        available.Sort(StringComparer.OrdinalIgnoreCase);
        return available;
    }

    public IReadOnlyList<string> Capture()
    {
        var result = new List<string>(_knownTopics);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public void Restore(IEnumerable<string>? keywords)
    {
        _knownTopics.Clear();
        if (keywords is null) return;
        foreach (var keyword in keywords) LearnTopic(keyword);
    }

    private bool ConditionsPass(IReadOnlyList<DialogueCondition> conditions, DialogueContext? context)
    {
        foreach (var condition in conditions)
            if (!Compare(ResolveValue(condition.Key, context), condition.Operator, condition.Value))
                return false;

        return true;
    }

    private string ResolveValue(string key, DialogueContext? context)
    {
        var state = _story.State;

        return key switch
        {
            "route" => state.RouteId,
            "evidence_count" => state.Evidence.Count.ToString(CultureInfo.InvariantCulture),
            "disposition" => (context?.Disposition ?? 0).ToString(CultureInfo.InvariantCulture),
            "faction" => context?.FactionId ?? string.Empty,
            "location" => context?.LocationId ?? string.Empty,
            "player.channeled" => state.PlayerChanneled.ToString(CultureInfo.InvariantCulture),
            _ => key.StartsWith("flag.", StringComparison.Ordinal) ? _story.FlagValue(key) : string.Empty
        };
    }

    private static bool Compare(string actual, ConditionOperator op, string expected)
    {
        switch (op)
        {
            case ConditionOperator.NotEquals:
                return !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

            case ConditionOperator.Min:
            case ConditionOperator.Max:
                if (!float.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    || !float.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                    return false;
                return op == ConditionOperator.Min ? a >= b : a <= b;

            default:
                return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// A named character you can talk to, using the topic knowledge base.
///
/// Topics offered are the intersection of what this actor can answer and what the player
/// knows to ask.
/// </summary>
public sealed class SpeakingActor
{
    private readonly TopicDialogueService _dialogue;

    public SpeakingActor(TopicDialogueService dialogue, string actorId, string displayName,
        string factionId = "", string locationId = "", params string[] opensWith)
    {
        _dialogue = dialogue;
        ActorId = actorId;
        DisplayName = displayName;
        FactionId = factionId;
        LocationId = locationId;
        OpensWith = opensWith.Length > 0 ? opensWith : new[] { "ratnapur" };
    }

    public string ActorId { get; }
    public string DisplayName { get; }
    public string FactionId { get; }
    public string LocationId { get; }

    /// <summary>Keywords this actor volunteers on a first meeting, teaching them to the player.</summary>
    public IReadOnlyList<string> OpensWith { get; }

    public DialogueContext Context => new()
    {
        ActorId = ActorId, FactionId = FactionId, LocationId = LocationId, Disposition = 50
    };

    /// <summary>Ask about a keyword. Null when this actor has nothing to say about it.</summary>
    public string? Ask(string? keyword) => _dialogue.Respond(keyword, Context);

    public IReadOnlyList<string> AvailableTopics() => _dialogue.AvailableTopics(Context);

    /// <summary>
    /// Open conversation. Teaches whatever this actor volunteers, then returns the topics
    /// they can actually answer. An empty list means they have nothing to say to you.
    /// </summary>
    public IReadOnlyList<string> Talk()
    {
        foreach (var keyword in OpensWith) _dialogue.LearnTopic(keyword);
        return AvailableTopics();
    }
}
