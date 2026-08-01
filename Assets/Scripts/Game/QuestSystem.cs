using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : MonoBehaviour
{
    [SerializeField] private QuestDefinition[] definitions;
    public static QuestSystem Instance { get; private set; }
    public readonly List<QuestData> Quests = new();
    public event Action OnChanged;

    private void Awake() { Instance = this; if (Quests.Count == 0) Seed(); }

    private void Seed()
    {
        if (definitions == null || definitions.Length == 0)
            definitions = Resources.LoadAll<QuestDefinition>("Data/Quests");
        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
            Quests.Add(new QuestData
            {
                Id = definition.Id, Title = definition.Title, Description = definition.Description,
                StageText = definition.InitialStageText, Active = true,
                TargetCount = definition.TargetCount, TargetEnemy = definition.TargetEnemy,
                TargetLocationId = definition.TargetLocationId
            });
        }
    }

    public void NotifyEnemyKilled(string enemyName)
    {
        foreach (var q in Quests)
        {
            if (!q.Active || q.Completed || q.TargetCount <= 0) continue;
            if (string.IsNullOrEmpty(q.TargetEnemy) || enemyName.IndexOf(q.TargetEnemy, StringComparison.OrdinalIgnoreCase) < 0) continue;
            q.Progress++;
            q.StageText = $"Slay bandits ({q.Progress}/{q.TargetCount})";
            if (q.Progress >= q.TargetCount) Complete(q); else OnChanged?.Invoke();
        }
    }

    public void NotifyLocation(string locationId)
    {
        foreach (var q in Quests)
        {
            if (!q.Active || q.Completed) continue;
            if (q.TargetLocationId == locationId)
            {
                if (q.Id == "main_bay") q.StageText = "Return toward Caldemar (or explore freely)";
                Complete(q);
            }
            if (q.Id == "main_bay" && (locationId == "city_east" || locationId == "city_south")) Complete(q);
        }
    }

    private void Complete(QuestData q)
    {
        if (q.Completed) return;
        q.Completed = true;
        q.Active = false;
        q.StageText = "Completed";
        PlayerStats.Instance?.AddXp(50);
        if (PlayerStats.Instance != null) PlayerStats.Instance.Gold += 40;
        GameHud.Instance?.ShowToast($"Quest complete: {q.Title}");
        OnChanged?.Invoke();
    }

    public List<SavedQuest> CaptureState()
    {
        var list = new List<SavedQuest>(Quests.Count);
        foreach (var q in Quests)
            list.Add(new SavedQuest { Id = q.Id, Active = q.Active, Completed = q.Completed, Progress = q.Progress, StageText = q.StageText });
        return list;
    }

    public void RestoreState(List<SavedQuest> saved)
    {
        if (saved == null) return;
        foreach (var s in saved)
        {
            var q = Quests.Find(x => x.Id == s.Id);
            if (q == null) continue;
            q.Active = s.Active;
            q.Completed = s.Completed;
            q.Progress = s.Progress;
            if (!string.IsNullOrEmpty(s.StageText)) q.StageText = s.StageText;
        }
        OnChanged?.Invoke();
    }

    private void Start()
    {
        if (DiscoveryTravelSystem.Instance != null) DiscoveryTravelSystem.Instance.OnDiscovered += OnDisc;
    }

    private void OnDestroy()
    {
        if (DiscoveryTravelSystem.Instance != null) DiscoveryTravelSystem.Instance.OnDiscovered -= OnDisc;
        if (Instance == this) Instance = null;
    }

    private void OnDisc(DiscoveryTravelSystem.Location loc) => NotifyLocation(loc.Id);
}

[Serializable]
public class QuestData
{
    public string Id;
    public string Title;
    public string Description;
    public string StageText;
    public bool Active;
    public bool Completed;
    public int TargetCount;
    public int Progress;
    public string TargetEnemy;
    public string TargetLocationId;
}
