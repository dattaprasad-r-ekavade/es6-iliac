namespace RatnaBay.Domain;

public sealed class CharacterProfile
{
    public string Name { get; set; } = string.Empty;
    public string AncestryId { get; set; } = string.Empty;
    public string Pronouns { get; set; } = string.Empty;
    public string DeclaredInclination { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(AncestryId);

    public CharacterProfile Clone() => new()
    {
        Name = Name, AncestryId = AncestryId,
        Pronouns = Pronouns, DeclaredInclination = DeclaredInclination
    };
}

public sealed class EvidenceRecord
{
    public required string Id { get; init; }
    public string Title { get; set; } = string.Empty;
    public string DocumentBody { get; set; } = string.Empty;
    public bool Inspected { get; set; }

    public EvidenceRecord Clone() => new()
    {
        Id = Id, Title = Title, DocumentBody = DocumentBody, Inspected = Inspected
    };
}

public sealed class CompanionState
{
    public string ActorId { get; set; } = string.Empty;
    public bool Following { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public string SpawnId { get; set; } = string.Empty;
    public float Health { get; set; } = 100f;

    public CompanionState Clone() => new()
    {
        ActorId = ActorId, Following = Following,
        SceneId = SceneId, SpawnId = SpawnId, Health = Health
    };
}

/// <summary>The whole story state, as written to a save.</summary>
public sealed class StorySnapshot
{
    public CharacterProfile Profile { get; set; } = new();
    public string ChapterId { get; set; } = "chapter.01";
    public string StageId { get; set; } = "stage.prologue";
    public string BeatId { get; set; } = "B010";
    public string RouteId { get; set; } = string.Empty;

    public Dictionary<string, string> Flags { get; set; } = new(StringComparer.Ordinal);
    public List<EvidenceRecord> Evidence { get; set; } = new();
    public Dictionary<string, string> DialogueChoices { get; set; } = new(StringComparer.Ordinal);
    public CompanionState Companion { get; set; } = new();

    public string KingOutcome { get; set; } = string.Empty;
    public string RulerId { get; set; } = string.Empty;
    public string GrantedTitle { get; set; } = string.Empty;

    public List<string> OpenedLocks { get; set; } = new();
    public List<string> LootedObjects { get; set; } = new();
    public List<string> SkippedCinematics { get; set; } = new();
    public List<string> KnownTopics { get; set; } = new();

    /// <summary>
    /// Lifetime crystals burned. Owned here rather than on the player because topic dialogue
    /// reads it through the `player.channeled` condition — this is the copy the world reacts to.
    /// </summary>
    public float PlayerChanneled { get; set; }

    public StorySnapshot Clone() => new()
    {
        Profile = Profile.Clone(),
        ChapterId = ChapterId, StageId = StageId, BeatId = BeatId, RouteId = RouteId,
        Flags = new Dictionary<string, string>(Flags, StringComparer.Ordinal),
        Evidence = Evidence.Select(e => e.Clone()).ToList(),
        DialogueChoices = new Dictionary<string, string>(DialogueChoices, StringComparer.Ordinal),
        Companion = Companion.Clone(),
        KingOutcome = KingOutcome, RulerId = RulerId, GrantedTitle = GrantedTitle,
        OpenedLocks = new List<string>(OpenedLocks),
        LootedObjects = new List<string>(LootedObjects),
        SkippedCinematics = new List<string>(SkippedCinematics),
        KnownTopics = new List<string>(KnownTopics),
        PlayerChanneled = PlayerChanneled
    };
}

/// <summary>Single authority for chapter, beat, route, consequence and checkpoint state.</summary>
public sealed class StoryDirector
{
    public const string RouteWarrior = "route.warrior";
    public const string RouteMage = "route.mage";
    public const string RouteTrade = "route.trade";

    /// <summary>The fastest route, which grants the least. That is its continuing price.</summary>
    public const string RouteRefuse = "route.refuse";

    private static readonly HashSet<string> ValidRoutes = new(StringComparer.Ordinal)
    {
        RouteWarrior, RouteMage, RouteTrade, RouteRefuse
    };

    private StorySnapshot _state = new();

    public StorySnapshot State => _state;

    public event Action? Changed;

    public void SetProfile(CharacterProfile? profile)
    {
        _state.Profile = profile ?? new CharacterProfile();
        SetFlag("flag.profile_valid", _state.Profile.IsValid ? "true" : "false");
    }

    /// <summary>
    /// Commit the player's route. An unrecognised route falls back to refusal rather than
    /// leaving the story in a state no beat is authored for.
    /// </summary>
    public bool SelectRoute(string? routeId)
    {
        var accepted = routeId is not null && ValidRoutes.Contains(routeId);
        _state.RouteId = accepted ? routeId! : RouteRefuse;
        SetFlag("flag.route", _state.RouteId);
        return accepted;
    }

    /// <summary>Commits the typed consequence state as well as its readable flags.</summary>
    public void SetOutcome(string? kingOutcome, string? rulerId, string? grantedTitle)
    {
        if (!string.IsNullOrWhiteSpace(kingOutcome))
        {
            _state.KingOutcome = kingOutcome;
            SetFlag("flag.king_outcome", kingOutcome);
        }

        if (!string.IsNullOrWhiteSpace(rulerId))
        {
            _state.RulerId = rulerId;
            SetFlag("flag.ruler", rulerId);
        }

        if (!string.IsNullOrWhiteSpace(grantedTitle))
        {
            _state.GrantedTitle = grantedTitle;
            SetFlag("flag.title_granted");
        }

        Changed?.Invoke();
    }

    public void AdvanceTo(string? chapterId, string? stageId, string? beatId)
    {
        if (!string.IsNullOrWhiteSpace(chapterId)) _state.ChapterId = chapterId;
        if (!string.IsNullOrWhiteSpace(stageId)) _state.StageId = stageId;
        if (!string.IsNullOrWhiteSpace(beatId)) _state.BeatId = beatId;
        Changed?.Invoke();
    }

    public void SetFlag(string? id, string value = "true")
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _state.Flags[id] = value;
        Changed?.Invoke();
    }

    public bool HasFlag(string? id, string? value = null)
    {
        if (id is null || !_state.Flags.TryGetValue(id, out var stored)) return false;
        return value is null || string.Equals(stored, value, StringComparison.Ordinal);
    }

    public string FlagValue(string? id) =>
        id is not null && _state.Flags.TryGetValue(id, out var value) ? value : string.Empty;

    public void AddEvidence(EvidenceRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.Id)) return;

        var existing = _state.Evidence.Find(e => string.Equals(e.Id, record.Id, StringComparison.Ordinal));
        if (existing is null) _state.Evidence.Add(record.Clone());
        else
        {
            existing.Title = record.Title;
            existing.DocumentBody = record.DocumentBody;
            existing.Inspected |= record.Inspected;
        }

        Changed?.Invoke();
    }

    public void RecordChoice(string? id, string value)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        _state.DialogueChoices[id] = value;
        Changed?.Invoke();
    }

    public void SetCompanion(string actorId, bool following, string sceneId, string spawnId,
        float health = 100f)
    {
        _state.Companion = new CompanionState
        {
            ActorId = actorId, Following = following,
            SceneId = sceneId, SpawnId = spawnId, Health = health
        };

        SetFlag("flag.prince_following", following ? "true" : "false");
    }

    public void MarkOpened(string? id) => AddUnique(_state.OpenedLocks, id);
    public void MarkLooted(string? id) => AddUnique(_state.LootedObjects, id);
    public void MarkCinematicSkipped(string? id) => AddUnique(_state.SkippedCinematics, id);

    public void AddChanneled(float amount)
    {
        _state.PlayerChanneled += MathF.Max(0f, amount);
        Changed?.Invoke();
    }

    public StorySnapshot Capture() => _state.Clone();

    public void Restore(StorySnapshot? snapshot)
    {
        _state = snapshot?.Clone() ?? new StorySnapshot();
        Changed?.Invoke();
    }

    private void AddUnique(List<string> values, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || values.Contains(id, StringComparer.Ordinal)) return;
        values.Add(id);
        Changed?.Invoke();
    }
}
