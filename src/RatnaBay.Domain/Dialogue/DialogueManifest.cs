using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RatnaBay.Domain;

/// <summary>JSON-authored speakers and keyword answers for one location.</summary>
public sealed class DialogueManifest
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public List<DialogueActorDefinition> Actors { get; set; } = new();
    public List<DialogueTopicDefinition> Topics { get; set; } = new();

    public static bool TryLoad(string path, out DialogueManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                error = $"Dialogue manifest not found: {path}";
                return false;
            }

            return TryParse(File.ReadAllText(path), out manifest, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Could not read dialogue manifest: {exception.Message}";
            return false;
        }
    }

    public static bool TryParse(string json, out DialogueManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            manifest = JsonSerializer.Deserialize<DialogueManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Invalid dialogue manifest JSON: {exception.Message}";
            return false;
        }

        if (manifest is null)
        {
            error = "Dialogue manifest is empty.";
            return false;
        }

        var failures = manifest.Validate();
        if (failures.Count > 0)
        {
            error = string.Join(" ", failures);
            manifest = null;
            return false;
        }

        return true;
    }

    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Version != 1) failures.Add($"version must be 1, got {Version}.");
        if (string.IsNullOrWhiteSpace(Id)) failures.Add("id is required.");

        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in Actors ?? new List<DialogueActorDefinition>())
        {
            ValidateId(actor?.Id, "actor", actorIds, failures);
            if (actor is null || string.IsNullOrWhiteSpace(actor.DisplayName))
                failures.Add($"actor '{actor?.Id ?? "<null>"}' needs a displayName.");
            if (actor?.Position is null || !actor.Position.IsFinite())
                failures.Add($"actor '{actor?.Id ?? "<null>"}' has invalid position.");
            if (actor is not null && (!float.IsFinite(actor.Height) || actor.Height <= 0f))
                failures.Add($"actor '{actor.Id}' height must be positive.");

            if (actor?.Pocket is { } pocket)
            {
                if (!float.IsFinite(pocket.Difficulty) || pocket.Difficulty < 0f || pocket.Difficulty > 100f)
                    failures.Add($"actor '{actor.Id}' pocket difficulty must be between 0 and 100.");

                foreach (var item in pocket.Items ?? new List<DialoguePocketItemDefinition>())
                {
                    if (item is null || string.IsNullOrWhiteSpace(item.Id))
                        failures.Add($"actor '{actor.Id}' has a pocket item without an id.");
                    else if (item.Count <= 0)
                        failures.Add($"actor '{actor.Id}' pocket item '{item.Id}' needs a positive count.");
                }
            }

            var openTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyword in actor?.OpensWith ?? new List<string>())
                if (string.IsNullOrWhiteSpace(keyword) || !openTopics.Add(keyword))
                    failures.Add($"actor '{actor?.Id ?? "<null>"}' has a duplicate or empty opensWith keyword.");
        }

        var topicIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var topic in Topics ?? new List<DialogueTopicDefinition>())
        {
            ValidateId(topic?.Id, "topic", topicIds, failures);
            if (topic is null || string.IsNullOrWhiteSpace(topic.Keyword))
                failures.Add($"topic '{topic?.Id ?? "<null>"}' needs a keyword.");
            if (topic is null || string.IsNullOrWhiteSpace(topic.Response))
                failures.Add($"topic '{topic?.Id ?? "<null>"}' needs a response.");
            if (topic is not null && !string.IsNullOrWhiteSpace(topic.ActorId)
                && !actorIds.Contains(topic.ActorId))
                failures.Add($"topic '{topic.Id}' references unknown actor '{topic.ActorId}'.");

            foreach (var condition in topic?.Conditions ?? new List<DialogueConditionDefinition>())
            {
                if (condition is null || string.IsNullOrWhiteSpace(condition.Key))
                    failures.Add($"topic '{topic?.Id ?? "<null>"}' has a condition without a key.");
                if (condition is null || !DialogueConditionDefinition.IsKnownOperator(condition.Operator))
                    failures.Add($"topic '{topic?.Id ?? "<null>"}' has an unknown condition operator '{condition?.Operator}'.");
            }
        }

        var keywords = new HashSet<string>(
            (Topics ?? new List<DialogueTopicDefinition>()).Where(topic => topic is not null)
                .Select(topic => topic.Keyword), StringComparer.OrdinalIgnoreCase);
        foreach (var actor in Actors ?? new List<DialogueActorDefinition>())
        foreach (var keyword in actor?.OpensWith ?? new List<string>())
            if (!keywords.Contains(keyword))
                failures.Add($"actor '{actor?.Id ?? "<null>"}' opens with unknown keyword '{keyword}'.");

        return failures;
    }

    public IReadOnlyList<DialogueTopic> ToTopics() =>
        (Topics ?? new List<DialogueTopicDefinition>())
            .Select(topic => topic.ToDomain())
            .ToList();

    public static string Serialize(DialogueManifest manifest) =>
        JsonSerializer.Serialize(manifest, JsonOptions);

    private static void ValidateId(string? id, string kind, HashSet<string> ids,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            failures.Add($"{kind} id is required.");
            return;
        }

        if (!ids.Add(id)) failures.Add($"duplicate dialogue {kind} id '{id}'.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}

public sealed class DialogueActorDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public WorldVector Position { get; set; } = new();
    public float Height { get; set; } = 1.85f;
    public string Palette { get; set; } = "citizen";
    public List<string> OpensWith { get; set; } = new();

    /// <summary>What this actor is carrying loose. Null when there is nothing to lift.</summary>
    public DialoguePocketDefinition? Pocket { get; set; }
}

/// <summary>
/// A pocket worth picking, authored rather than hardcoded.
///
/// Difficulty is deliberately separate from the contents: a purse anyone can take and a key
/// only a trained thief can reach are the same shape with a different number.
/// </summary>
public sealed class DialoguePocketDefinition
{
    public float Difficulty { get; set; }
    public List<DialoguePocketItemDefinition> Items { get; set; } = new();
}

public sealed class DialoguePocketItemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "loot";
    public int Count { get; set; } = 1;
}

public sealed class DialogueTopicDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string FactionId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string QuestId { get; set; } = string.Empty;
    public List<DialogueConditionDefinition> Conditions { get; set; } = new();

    public DialogueTopic ToDomain() => new()
    {
        Id = Id,
        Keyword = Keyword,
        ActorId = ActorId,
        FactionId = FactionId,
        Response = Response,
        QuestId = QuestId,
        Conditions = (Conditions ?? new List<DialogueConditionDefinition>())
            .Select(condition => condition.ToDomain())
            .ToList()
    };
}

public sealed class DialogueConditionDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Operator { get; set; } = "equals";
    public string Value { get; set; } = string.Empty;

    public DialogueCondition ToDomain() => new()
    {
        Key = Key,
        Operator = ParseOperator(Operator),
        Value = Value
    };

    public static bool IsKnownOperator(string? value) =>
        value is not null && (value.Equals("equals", StringComparison.OrdinalIgnoreCase)
            || value.Equals("notEquals", StringComparison.OrdinalIgnoreCase)
            || value.Equals("min", StringComparison.OrdinalIgnoreCase)
            || value.Equals("max", StringComparison.OrdinalIgnoreCase));

    private static ConditionOperator ParseOperator(string? value) => value?.ToLowerInvariant() switch
    {
        "notequals" => ConditionOperator.NotEquals,
        "min" => ConditionOperator.Min,
        "max" => ConditionOperator.Max,
        _ => ConditionOperator.Equals
    };
}
