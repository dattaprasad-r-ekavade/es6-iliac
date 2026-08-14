using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterProfile
{
    public string Name;
    public string AncestryId;
    public string Pronouns;
    public string DeclaredInclination;
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(AncestryId);
}

[Serializable] public sealed class StoryFlag { public string Id; public string Value; }

[Serializable]
public sealed class EvidenceRecord
{
    public string Id;
    public string Title;
    [TextArea] public string DocumentBody;
    public bool Inspected;
}

[Serializable] public sealed class DialogueChoiceRecord { public string Id; public string Value; }

[Serializable]
public sealed class CompanionState
{
    public string ActorId;
    public bool Following;
    public string SceneId;
    public string SpawnId;
    public float Health = 100f;
}


[Serializable]
public sealed class StorySnapshot
{
    public CharacterProfile Profile = new();
    public string ChapterId = "chapter.01";
    public string StageId = "stage.prologue";
    public string BeatId = "B010";
    public string RouteId;
    public List<StoryFlag> Flags = new();
    public List<EvidenceRecord> Evidence = new();
    public List<DialogueChoiceRecord> DialogueChoices = new();
    public CompanionState Companion = new();
    public string KingOutcome;
    public string RulerId;
    public string GrantedTitle;
    public List<string> OpenedLocks = new();
    public List<string> LootedObjects = new();
    public List<string> SkippedCinematics = new();
    public List<string> KnownTopics = new();
    /// <summary>
    /// Lifetime crystals burned. Owned here rather than in <see cref="SaveData"/> because
    /// topic dialogue reads it through the `player.channeled` condition — this is the copy
    /// the world reacts to.
    /// </summary>
    public float PlayerChanneled;
}

/// <summary>Single authority for Chapter 01 beat, route, consequence and checkpoint state.</summary>
public sealed class StoryDirector : MonoBehaviour
{
    private static readonly HashSet<string> ValidRoutes = new(StringComparer.Ordinal)
    {
        "route.warrior", "route.mage", "route.trade", "route.refuse"
    };

    [SerializeField] private StorySnapshot state = new();
    public static StoryDirector Instance { get; private set; }
    public StorySnapshot State => state;
    public event Action Changed;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void SetProfile(CharacterProfile profile)
    {
        state.Profile = profile ?? new CharacterProfile();
        SetFlag("flag.profile_valid", state.Profile.IsValid ? "true" : "false");
    }

    public bool SelectRoute(string routeId)
    {
        if (!ValidRoutes.Contains(routeId)) routeId = "route.refuse";
        state.RouteId = routeId;
        SetFlag("flag.route", routeId);
        return ValidRoutes.Contains(routeId);
    }

    /// <summary>Commits the typed consequence state as well as its readable flags.</summary>
    public void SetOutcome(string kingOutcome, string rulerId, string grantedTitle)
    {
        if (!string.IsNullOrWhiteSpace(kingOutcome))
        {
            state.KingOutcome = kingOutcome;
            SetFlag("flag.king_outcome", kingOutcome);
        }
        if (!string.IsNullOrWhiteSpace(rulerId))
        {
            state.RulerId = rulerId;
            SetFlag("flag.ruler", rulerId);
        }
        if (!string.IsNullOrWhiteSpace(grantedTitle))
        {
            state.GrantedTitle = grantedTitle;
            SetFlag("flag.title_granted");
        }
        Changed?.Invoke();
    }

    public void AdvanceTo(string chapterId, string stageId, string beatId)
    {
        if (!string.IsNullOrWhiteSpace(chapterId)) state.ChapterId = chapterId;
        if (!string.IsNullOrWhiteSpace(stageId)) state.StageId = stageId;
        if (!string.IsNullOrWhiteSpace(beatId)) state.BeatId = beatId;
        Changed?.Invoke();
    }

    public void SetFlag(string id, string value = "true")
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        var flag = state.Flags.Find(f => f.Id == id);
        if (flag == null) state.Flags.Add(new StoryFlag { Id = id, Value = value });
        else flag.Value = value;
        Changed?.Invoke();
    }

    public bool HasFlag(string id, string value = null)
    {
        var flag = state.Flags.Find(f => f.Id == id);
        return flag != null && (value == null || flag.Value == value);
    }

    public void AddEvidence(EvidenceRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Id)) return;
        var existing = state.Evidence.Find(e => e.Id == record.Id);
        if (existing == null) state.Evidence.Add(CloneEvidence(record));
        else
        {
            existing.Title = record.Title;
            existing.DocumentBody = record.DocumentBody;
            existing.Inspected |= record.Inspected;
        }
        Changed?.Invoke();
    }

    public void RecordChoice(string id, string value)
    {
        var choice = state.DialogueChoices.Find(c => c.Id == id);
        if (choice == null) state.DialogueChoices.Add(new DialogueChoiceRecord { Id = id, Value = value });
        else choice.Value = value;
        Changed?.Invoke();
    }

    public void SetCompanion(string actorId, bool following, string sceneId, string spawnId, float health = 100f)
    {
        state.Companion = new CompanionState
        {
            ActorId = actorId, Following = following, SceneId = sceneId, SpawnId = spawnId, Health = health
        };
        SetFlag("flag.prince_following", following ? "true" : "false");
    }

    public void MarkOpened(string id) => AddUnique(state.OpenedLocks, id);
    public void MarkLooted(string id) => AddUnique(state.LootedObjects, id);
    public void MarkCinematicSkipped(string id) => AddUnique(state.SkippedCinematics, id);
    public void AddChanneled(float amount) { state.PlayerChanneled += Mathf.Max(0f, amount); Changed?.Invoke(); }

    public StorySnapshot Capture() => JsonUtility.FromJson<StorySnapshot>(JsonUtility.ToJson(state));

    public void Restore(StorySnapshot snapshot)
    {
        state = snapshot != null
            ? JsonUtility.FromJson<StorySnapshot>(JsonUtility.ToJson(snapshot))
            : new StorySnapshot();
        Normalize();
        Changed?.Invoke();
    }

    private void Normalize()
    {
        state.Profile ??= new CharacterProfile();
        state.Flags ??= new List<StoryFlag>();
        state.Evidence ??= new List<EvidenceRecord>();
        state.DialogueChoices ??= new List<DialogueChoiceRecord>();
        state.Companion ??= new CompanionState();
        state.OpenedLocks ??= new List<string>();
        state.LootedObjects ??= new List<string>();
        state.SkippedCinematics ??= new List<string>();
        state.KnownTopics ??= new List<string>();
    }

    private void AddUnique(List<string> values, string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && !values.Contains(id)) values.Add(id);
        Changed?.Invoke();
    }

    private static EvidenceRecord CloneEvidence(EvidenceRecord source) => new()
    {
        Id = source.Id, Title = source.Title, DocumentBody = source.DocumentBody, Inspected = source.Inspected
    };
}
