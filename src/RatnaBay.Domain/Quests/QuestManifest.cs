using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RatnaBay.Domain;

/// <summary>JSON-authored quest definitions. Live progress remains in QuestSystem.</summary>
public sealed class QuestManifest
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public List<QuestDefinitionData> Quests { get; set; } = new();

    public static bool TryLoad(string path, out QuestManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                error = $"Quest manifest not found: {path}";
                return false;
            }

            return TryParse(File.ReadAllText(path), out manifest, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Could not read quest manifest: {exception.Message}";
            return false;
        }
    }

    public static bool TryParse(string json, out QuestManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            manifest = JsonSerializer.Deserialize<QuestManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Invalid quest manifest JSON: {exception.Message}";
            return false;
        }

        if (manifest is null)
        {
            error = "Quest manifest is empty.";
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

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quest in Quests ?? new List<QuestDefinitionData>())
        {
            if (quest is null || string.IsNullOrWhiteSpace(quest.Id))
                failures.Add("quest id is required.");
            else if (!ids.Add(quest.Id))
                failures.Add($"duplicate quest id '{quest.Id}'.");

            if (quest is null || string.IsNullOrWhiteSpace(quest.Title))
                failures.Add($"quest '{quest?.Id ?? "<null>"}' needs a title.");
            if (quest is null || string.IsNullOrWhiteSpace(quest.InitialStageText))
                failures.Add($"quest '{quest?.Id ?? "<null>"}' needs initialStageText.");
            if (quest is not null && quest.TargetCount < 0)
                failures.Add($"quest '{quest.Id}' targetCount cannot be negative.");
            if (quest is not null && quest.TargetCount > 0 && string.IsNullOrWhiteSpace(quest.TargetEnemy))
                failures.Add($"quest '{quest.Id}' needs targetEnemy for a kill count.");
            if (quest?.ObjectivePosition is not null && !quest.ObjectivePosition.IsFinite())
                failures.Add($"quest '{quest.Id}' has an invalid objectivePosition.");
        }

        return failures;
    }

    public IReadOnlyList<QuestDefinition> ToDefinitions() =>
        (Quests ?? new List<QuestDefinitionData>()).Select(quest => quest.ToDomain()).ToList();

    public static string Serialize(QuestManifest manifest) =>
        JsonSerializer.Serialize(manifest, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}

public sealed class QuestDefinitionData
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InitialStageText { get; set; } = string.Empty;
    public string ObjectiveDirections { get; set; } = string.Empty;
    public string ObjectiveAnchorId { get; set; } = string.Empty;
    public WorldVector? ObjectivePosition { get; set; }
    public int TargetCount { get; set; }
    public string TargetEnemy { get; set; } = string.Empty;
    public List<string> TargetLocationIds { get; set; } = new();
    public int XpReward { get; set; } = 50;
    public int GoldReward { get; set; } = 40;

    public QuestDefinition ToDomain() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
        InitialStageText = InitialStageText,
        ObjectiveDirections = ObjectiveDirections,
        ObjectiveAnchorId = ObjectiveAnchorId,
        ObjectivePosition = ObjectivePosition?.ToWorldPoint(),
        TargetCount = TargetCount,
        TargetEnemy = TargetEnemy,
        TargetLocationIds = (TargetLocationIds ?? new List<string>()).ToArray(),
        XpReward = XpReward,
        GoldReward = GoldReward
    };
}
