namespace RatnaBay.Domain;

/// <summary>
/// Authored quest data. Immutable — the mutable half is <see cref="Quest"/>.
///
/// A quest completes on one of two conditions, declared here rather than branched on by id.
/// The Unity version special-cased `main_bay` inside the completion check, which is exactly
/// the drift the declarative model exists to prevent.
/// </summary>
public sealed class QuestDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = string.Empty;
    public string InitialStageText { get; init; } = string.Empty;
    public string ObjectiveDirections { get; init; } = string.Empty;
    public string ObjectiveAnchorId { get; init; } = string.Empty;
    public WorldPoint? ObjectivePosition { get; init; }

    /// <summary>How many kills are needed. Zero means this is not a kill quest.</summary>
    public int TargetCount { get; init; }

    /// <summary>Matched against an enemy's display name, case-insensitively.</summary>
    public string TargetEnemy { get; init; } = string.Empty;

    /// <summary>Any of these locations completes the quest on discovery.</summary>
    public IReadOnlyList<string> TargetLocationIds { get; init; } = Array.Empty<string>();

    public int XpReward { get; init; } = 50;
    public int GoldReward { get; init; } = 40;

    public bool IsKillQuest => TargetCount > 0 && !string.IsNullOrEmpty(TargetEnemy);
    public bool IsLocationQuest => TargetLocationIds.Count > 0;
}

/// <summary>One quest's live state.</summary>
public sealed class Quest
{
    public Quest(QuestDefinition definition, bool active = true)
    {
        Definition = definition;
        StageText = definition.InitialStageText;
        IsActive = active;
    }

    public QuestDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Title => Definition.Title;

    public string StageText { get; internal set; }
    public bool IsActive { get; internal set; } = true;
    public bool IsCompleted { get; internal set; }
    public int Progress { get; internal set; }

    public bool CanProgress => IsActive && !IsCompleted;
}

/// <summary>One quest's state as written to a save.</summary>
public sealed class SavedQuest
{
    public required string Id { get; init; }
    public bool IsActive { get; init; }
    public bool IsCompleted { get; init; }
    public int Progress { get; init; }
    public string StageText { get; init; } = string.Empty;
}

/// <summary>
/// The quest log.
///
/// Progression is stage-bound and event-bound; nothing here expires because in-game hours
/// passed. That exclusion is what keeps quest state, saves and testing tractable.
/// </summary>
public sealed class QuestSystem
{
    private readonly List<Quest> _quests = new();
    private readonly PlayerVitals _vitals;

    public QuestSystem(PlayerVitals vitals) => _vitals = vitals;

    public IReadOnlyList<Quest> Quests => _quests;
    public IEnumerable<Quest> Active => _quests.Where(q => q.CanProgress);

    public event Action? Changed;
    public event Action<Quest>? QuestCompleted;

    public void Add(QuestDefinition definition)
    {
        if (Find(definition.Id) is not null) return;
        _quests.Add(new Quest(definition));
        Changed?.Invoke();
    }

    /// <summary>Register authored data without offering the quest until dialogue accepts it.</summary>
    public void Register(QuestDefinition definition)
    {
        if (Find(definition.Id) is not null) return;
        _quests.Add(new Quest(definition, active: false));
        Changed?.Invoke();
    }

    public void RegisterRange(IEnumerable<QuestDefinition> definitions)
    {
        foreach (var definition in definitions) Register(definition);
    }

    public void AddRange(IEnumerable<QuestDefinition> definitions)
    {
        foreach (var definition in definitions) Add(definition);
    }

    public Quest? Find(string? id) =>
        string.IsNullOrEmpty(id) ? null : _quests.Find(q => string.Equals(q.Id, id, StringComparison.Ordinal));

    public Quest? Activate(string? id)
    {
        var quest = Find(id);
        if (quest is null || quest.IsCompleted) return quest;
        if (!quest.IsActive)
        {
            quest.IsActive = true;
            Changed?.Invoke();
        }

        return quest;
    }

    /// <summary>An enemy died. Advances every kill quest that named it.</summary>
    public void NotifyEnemyKilled(string? enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return;

        foreach (var quest in _quests)
        {
            var definition = quest.Definition;
            if (!quest.CanProgress || !definition.IsKillQuest) continue;
            if (enemyName.IndexOf(definition.TargetEnemy, StringComparison.OrdinalIgnoreCase) < 0) continue;

            quest.Progress++;
            quest.StageText = $"{definition.InitialStageText} ({quest.Progress}/{definition.TargetCount})";

            if (quest.Progress >= definition.TargetCount) Complete(quest);
            else Changed?.Invoke();
        }
    }

    /// <summary>A location was discovered. Completes every quest that named it.</summary>
    public void NotifyLocation(string? locationId)
    {
        if (string.IsNullOrEmpty(locationId)) return;

        foreach (var quest in _quests.ToList())
        {
            if (!quest.CanProgress || !quest.Definition.IsLocationQuest) continue;
            if (!quest.Definition.TargetLocationIds.Contains(locationId, StringComparer.Ordinal)) continue;
            Complete(quest);
        }
    }

    public void Complete(Quest? quest)
    {
        if (quest is null || quest.IsCompleted) return;

        quest.IsCompleted = true;
        quest.IsActive = false;
        quest.StageText = "Completed";

        _vitals.AddXp(quest.Definition.XpReward);
        _vitals.AddGold(quest.Definition.GoldReward);

        QuestCompleted?.Invoke(quest);
        Changed?.Invoke();
    }

    public IReadOnlyList<SavedQuest> Capture() => _quests
        .Select(q => new SavedQuest
        {
            Id = q.Id, IsActive = q.IsActive, IsCompleted = q.IsCompleted,
            Progress = q.Progress, StageText = q.StageText
        })
        .ToList();

    /// <summary>
    /// Restore from a save. Unknown ids are skipped rather than trusted, so a save from a
    /// build with a quest this one does not have will not throw.
    /// </summary>
    public void Restore(IEnumerable<SavedQuest>? saved)
    {
        if (saved is null) return;

        foreach (var entry in saved)
        {
            var quest = Find(entry.Id);
            if (quest is null) continue;

            quest.IsActive = entry.IsActive;
            quest.IsCompleted = entry.IsCompleted;
            quest.Progress = entry.Progress;
            if (!string.IsNullOrEmpty(entry.StageText)) quest.StageText = entry.StageText;
        }

        Changed?.Invoke();
    }
}
