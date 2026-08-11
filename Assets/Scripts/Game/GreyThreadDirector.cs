using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Playable VS2 grey-thread driver. Every Chapter 01 beat is now represented by a real
/// placeholder milestone, while the later vertical slices replace the text, actors and
/// mechanics inside the same scene/route contract.
/// </summary>
public sealed class GreyThreadDirector : MonoBehaviour
{
    public static GreyThreadDirector Instance { get; private set; }

    public bool IsRunning { get; private set; }
    public string ActiveRoute { get; private set; }
    public string LastError { get; private set; }
    public int CheckpointCount { get; private set; }
    public event Action<string> RouteStarted;
    public event Action<string> RouteCompleted;
    public event Action<string> BeatVisited;

    private Coroutine _routeRoutine;
    private GreyThreadAssignmentPanel _assignmentPanel;
    private string _pendingRoute;
    private string _pendingName;
    private bool _interactiveStarted;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator Start()
    {
        // The director is on the persistent GameSystems prefab. Wait for the real HUD and
        // gameplay handoff before opening the audience panel.
        while (GameHud.Instance == null || !IsGameplay()) yield return null;
        if (!_interactiveStarted && !IsRunning)
        {
            _interactiveStarted = true;
            BeginInteractiveRoute();
        }
    }

    public void BeginInteractiveRoute()
    {
        if (IsRunning) return;
        _routeRoutine = StartCoroutine(RunRouteRoutine(null, true));
    }

    /// <summary>Compatibility/test entry point for a deterministic named route.</summary>
    public void BeginRoute(string routeId)
    {
        if (IsRunning) return;
        _routeRoutine = StartCoroutine(RunRouteRoutine(GreyThreadSceneCatalog.NormalizeRoute(routeId), false));
    }

    /// <summary>Runs one route without opening the UI; intended for PlayMode gates.</summary>
    public IEnumerator RunRoute(string routeId)
    {
        if (IsRunning) yield break;
        yield return RunRouteRoutine(GreyThreadSceneCatalog.NormalizeRoute(routeId), false);
    }

    private IEnumerator RunRouteRoutine(string requestedRoute, bool interactive)
    {
        ActiveRoute = requestedRoute;
        LastError = null;
        IsRunning = true;
        RouteStarted?.Invoke(requestedRoute);
        EnsureProfile(requestedRoute ?? "route.refuse");

        yield return Visit("Prologue_Ship", "spawn.entry", "B010", "stage.prologue");
        if (Failed()) yield break;
        AdvanceBeat("B020", "stage.prologue", "Warships hold the horizon.");
        AdvanceBeat("B030", "stage.prologue", "The Everspire pulse breaks across the water.");
        AdvanceBeat("B040", "stage.prologue", "The deck breaks. Water. Blackout.");
        AdvanceBeat("B050", "stage.prologue", "The King's ship pulls you aboard under blackout.");
        Story.SetFlag("flag.rescued");
        SaveCheckpoint();

        yield return Visit("Estmere_Docks", "spawn.entry", "B060", "stage.estmere");
        if (Failed()) yield break;
        AdvanceBeat("B070", "stage.estmere", "Guards process the survivors; every memory has the same gap.");
        Story.SetFlag("flag.profile_valid");
        SaveCheckpoint();

        yield return Visit("Estmere_Exterior", "spawn.caldemar", "B080", "stage.estmere");
        if (Failed()) yield break;

        yield return Visit("Estmere_Palace", "spawn.entry", "B090", "stage.assignment");
        if (Failed()) yield break;
        AdvanceBeat("B100", "stage.assignment", "Every soul must contribute.");
        AdvanceBeat("B110", "stage.assignment", "The King questions the missing prince and the pulse.");

        string route;
        if (interactive)
        {
            yield return WaitForAssignment();
            route = _pendingRoute;
            EnsureProfile(route, _pendingName);
        }
        else route = requestedRoute;

        route = GreyThreadSceneCatalog.NormalizeRoute(route);
        ActiveRoute = route;
        Story.SelectRoute(route);
        Story.RecordChoice("choice.audience_assignment", route);
        Story.RecordChoice("choice.profile_name", Story.State.Profile.Name);
        AdvanceBeat("B120", "stage.assignment", "Your name and inclination are recorded.");
        AdvanceBeat("B130", "stage.assignment", "The King assigns your route.");
        Story.SetFlag("flag.route", route);

        // Assignment grants the route's two skills. route.refuse grants none — the fastest
        // route gives the least, which is its continuing price.
        SkillSystem.Instance?.GrantRouteSkills(route);
        SaveCheckpoint();

        switch (route)
        {
            case "route.warrior":
                yield return Visit("Tutorial_Warrior", "spawn.entry", "B200", "stage.warrior");
                if (Failed()) yield break;
                IssueGear("iron_sword", "Iron Sword", "weapon");
                IssueGear("padded_jerkin", "Padded Jerkin", "armour");
                AdvanceBeat("B210", "stage.warrior", "The hunt and patrol resolve in a real encounter.");
                AddEvidence("ev.transport_order", "Transport Order", "A sealed order moves a prisoner beneath the city.");
                AdvanceBeat("B220", "stage.warrior", "The patrol uncovers a secret prisoner transport.");
                break;
            case "route.mage":
                yield return Visit("Estmere_Arcanum", "spawn.entry", "B300", "stage.mage");
                if (Failed()) yield break;
                // The Arcanum issues the charge its lesson consumes. B310's manifest is what
                // makes the player look at where that charge came from.
                IssueGear(SoulCrystals.LesserId, SoulCrystals.LesserName, SoulCrystals.ItemKind, 5);
                AddEvidence("ev.crystal_manifest", "Crystal Manifest", "The source column names prisoners transferred under royal seal.");
                AdvanceBeat("B310", "stage.mage", "The soul-crystal delivery exposes an impossible source column.");
                break;
            case "route.trade":
                yield return Visit("Estmere_Harbor", "spawn.entry", "B400", "stage.trade");
                if (Failed()) yield break;
                IssueGear("hunting_bow", "Hunting Bow", "weapon");
                AdvanceBeat("B410", "stage.trade", "Sailing, stealth, locks and pickpocketing are introduced.");
                yield return Visit("Estmere_SecuredTower", "spawn.entry", "B420", "stage.trade");
                if (Failed()) yield break;
                AddEvidence("ev.tower_ledger", "Tower Ledger", "A crown ledger ties the prisoner operation to the east tower.");
                break;
            default:
                yield return Visit("Estmere_Prison", "spawn.entry", "B500", "stage.refuse");
                if (Failed()) yield break;
                AddEvidence("ev.prisoner_testimony", "Prisoner Testimony", "A named prisoner confirms the living cargo below the palace.");
                AdvanceBeat("B510", "stage.refuse", "A prisoner reveals the soul-harvesting operation in motion.");
                AdvanceBeat("B520", "stage.refuse", "The route to solitary is the deliberate speed path.");
                break;
        }
        SaveCheckpoint();

        // Convergence contract clause 6: every route enters B600 unarmed, with gear stored
        // rather than destroyed. This is what lets B630's escape be authored once.
        PlayerEquipment.Instance?.StashGear();

        yield return Visit("Estmere_Prison", "spawn.route", "B320", "stage.convergence");
        if (Failed()) yield break;
        AdvanceBeat("B600", "stage.convergence", "The prince is located.");
        Story.SetFlag("flag.prince_located");
        AdvanceBeat("B610", "stage.convergence", "The prince explains the interception and his father's motive.");
        AddEvidence("ev.prince_testimony", "Prince's Testimony", "Terrin names the interception, the missing alternative and the King's motive.");
        AdvanceBeat("B615", "stage.convergence", "The Everspire and Ivory Concord are seeded for later chapters.");
        SaveCheckpoint();

        yield return Visit("Estmere_SeaCave", "spawn.escape", "B620", "stage.escape");
        if (Failed()) yield break;
        AddEvidence("ev.black_crystal", "Black Crystal", "A resonant shard remembers the voices of the prisoners.");
        // The evidence room holds what was taken on the way in.
        PlayerEquipment.Instance?.RestoreStashedGear();
        Story.SetCompanion("role.prince", true, "Estmere_SeaCave", "spawn.escape", 100f);
        AdvanceBeat("B630", "stage.escape", "The prince follows you into the sea cave.");
        Story.SetFlag("flag.prince_following");
        yield return PlayTitleCrawl();
        if (Failed()) yield break;
        SaveCheckpoint();

        yield return Visit("Estmere_Palace_Aftermath", "spawn.entry", "B700", "stage.aftermath");
        if (Failed()) yield break;
        AdvanceBeat("B710", "stage.aftermath", "Evidence is presented; the prince testifies.");
        AdvanceBeat("B720", "stage.aftermath", "The King gives the legitimate-supply defence.");
        string outcome = route == "route.refuse" ? "imprisoned" : "killed";
        Story.SetOutcome(outcome, string.Empty, string.Empty);
        AdvanceBeat("B730", "stage.aftermath", "The King's outcome is decided.");
        AdvanceBeat("B740", "stage.aftermath", "The prince is crowned.");
        Story.SetOutcome(string.Empty, "role.prince", string.Empty);
        Story.SetFlag("flag.ruler", "prince");
        AdvanceBeat("B750", "stage.aftermath", "Prisoner soul-binding is outlawed; prisoners are released.");
        Story.SetFlag("flag.ban_enacted");
        AdvanceBeat("B760", "stage.aftermath", "The player is granted the Crown Envoy title.");
        Story.SetOutcome(string.Empty, string.Empty, "title.crown_envoy");
        Story.SetFlag("flag.title_granted");
        AdvanceBeat("B800", "stage.aftermath", "The new king asks for Crown Council recognition.");
        Story.RecordChoice("choice.council_mission", "seek_recognition");
        SaveCheckpoint();

        yield return Visit("Estmere_Exterior", "spawn.caldemar", "B810", "stage.handoff");
        if (Failed()) yield break;
        yield return Visit("Caldemar_Arrival", "spawn.council", "B820", "stage.handoff");
        if (Failed()) yield break;
        AdvanceBeat("B830", "stage.handoff", "The opening chapter is complete; the Council awaits.");
        Story.SetFlag("flag.chapter_complete");
        SaveCheckpoint();

        IsRunning = false;
        _routeRoutine = null;
        RouteCompleted?.Invoke(ActiveRoute);
        GameHud.Instance?.ShowToast($"VS2 route complete · {ActiveRoute} · Council handoff ready");
    }

    private IEnumerator Visit(string sceneName, string spawnId, string beatId, string stageId)
    {
        var transition = SceneTransitionService.Instance;
        if (transition == null)
        {
            LastError = "SceneTransitionService is missing.";
            IsRunning = false;
            yield break;
        }

        yield return transition.TransitionTo(sceneName, spawnId, unloadPrevious: true);
        if (!string.IsNullOrEmpty(transition.LastError))
        {
            LastError = transition.LastError;
            IsRunning = false;
            yield break;
        }

        AdvanceBeat(beatId, stageId, GreyThreadSceneCatalog.Find(sceneName)?.Title ?? sceneName);
        SaveCheckpoint();
        yield return null;
    }

    private IEnumerator PlayTitleCrawl()
    {
        var sequence = Resources.Load<CinematicSequence>("Data/Cinematics/ch01_title_crawl");
        if (sequence == null)
        {
            LastError = "The B640 title-crawl sequence is missing.";
            IsRunning = false;
            yield break;
        }

        var runner = CinematicRunner.Instance ?? GetComponent<CinematicRunner>();
        if (runner == null) runner = gameObject.AddComponent<CinematicRunner>();
        yield return runner.Play(sequence);
        if (!Story.HasFlag("flag.title_crawl_shown"))
        {
            LastError = "The B640 title-crawl sequence did not apply its end state.";
            IsRunning = false;
            yield break;
        }
        AdvanceBeat("B640", "stage.escape", "KESSIL BAY");
    }

    private IEnumerator WaitForAssignment()
    {
        _pendingRoute = null;
        _pendingName = null;
        if (_assignmentPanel == null)
        {
            _assignmentPanel = gameObject.AddComponent<GreyThreadAssignmentPanel>();
            _assignmentPanel.Submitted += OnAssignmentSubmitted;
        }

        _assignmentPanel.Show();
        GameStateService.Ensure().SetState(GameState.Menu, pauseWorld: true);
        while (string.IsNullOrWhiteSpace(_pendingRoute)) yield return null;
        _assignmentPanel.Hide();
        GameStateService.Ensure().SetState(GameState.Gameplay);
    }

    private void OnAssignmentSubmitted(string name, string route)
    {
        _pendingName = string.IsNullOrWhiteSpace(name) ? "The Castaway" : name;
        _pendingRoute = GreyThreadSceneCatalog.NormalizeRoute(route);
    }

    private void EnsureProfile(string routeId, string name = null)
    {
        var story = Story;
        if (story == null) throw new MissingReferenceException("GreyThreadDirector requires StoryDirector.");
        if (!story.State.Profile.IsValid
            || !string.IsNullOrWhiteSpace(name)
            || !string.Equals(story.State.Profile.DeclaredInclination, routeId, StringComparison.Ordinal))
        {
            story.SetProfile(new CharacterProfile
            {
                Name = string.IsNullOrWhiteSpace(name) ? "The Castaway" : name,
                AncestryId = "anc.isleborn",
                Pronouns = "they/them",
                DeclaredInclination = routeId
            });
        }
    }

    /// <summary>
    /// Hand the player route gear and equip it. Routes grant different kit, which is what
    /// makes the four openings mechanically distinct rather than differently captioned.
    /// </summary>
    private static void IssueGear(string itemId, string displayName, string kind, int count = 1)
    {
        PlayerInventory.Instance?.Add(itemId, displayName, count, kind);
        PlayerEquipment.Instance?.AutoEquipBest();
    }

    private void AdvanceBeat(string beatId, string stageId, string text)
    {
        Story.AdvanceTo("chapter.01", stageId, beatId);
        BeatVisited?.Invoke(beatId);
        GameHud.Instance?.ShowToast($"{beatId} · {text}");
    }

    private void AddEvidence(string id, string title, string body)
    {
        Story.AddEvidence(new EvidenceRecord { Id = id, Title = title, DocumentBody = body, Inspected = true });
    }

    private void SaveCheckpoint()
    {
        if (SaveLoadService.Instance == null) return;
        SaveLoadService.Instance.Save();
        CheckpointCount++;
    }

    private bool Failed() => !IsRunning || !string.IsNullOrEmpty(LastError);
    private bool IsGameplay() => GameStateService.Instance == null || GameStateService.Instance.CurrentState == GameState.Gameplay;
    private StoryDirector Story => StoryDirector.Instance;
}
