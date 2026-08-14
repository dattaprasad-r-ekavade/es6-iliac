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

    /// <summary>
    /// When true the player walks to each location themselves instead of the director
    /// transitioning for them. This is what turns a traversable beat list into a playable
    /// chapter; the automated gate runs with it off so it stays deterministic.
    /// </summary>
    private bool _playerDriven;

    /// <summary>The generated exterior the player returns to between story locations.</summary>
    public const string RegionScene = "Capital_Region";

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator Start()
    {
        // The director is on the persistent GameSystems prefab. Wait for the real HUD and
        // gameplay handoff before opening the audience panel.
        while (GameHud.Instance == null || !IsGameplay()
               || (GameFlowController.Instance != null && GameFlowController.Instance.IsContinuing)
               || (SaveLoadService.Instance != null && SaveLoadService.Instance.IsLoading))
            yield return null;

        // A Continue restores the exact scene, position and story snapshot. Starting this
        // routine again would overwrite all three with B010 and the prologue scene.
        if (HasRestoredProgress(Story?.State))
        {
            _interactiveStarted = true;
            yield break;
        }
        if (!_interactiveStarted && !IsRunning)
        {
            _interactiveStarted = true;
            BeginInteractiveRoute();
        }
    }

    public static bool HasRestoredProgress(StorySnapshot snapshot)
    {
        if (snapshot == null) return false;
        return !string.Equals(snapshot.BeatId, "B010", StringComparison.Ordinal)
               || !string.Equals(snapshot.StageId, "stage.prologue", StringComparison.Ordinal)
               || snapshot.Profile?.IsValid == true
               || !string.IsNullOrWhiteSpace(snapshot.RouteId)
               || snapshot.Flags?.Count > 0
               || snapshot.Evidence?.Count > 0;
    }

    public void BeginInteractiveRoute()
    {
        if (IsRunning) return;
        _playerDriven = true;
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
        AdvanceBeat("B020", "stage.prologue", "Dhruva Order warships hold the horizon.");
        AdvanceBeat("B030", "stage.prologue", "The Stambha pulse breaks across the water.");
        AdvanceBeat("B040", "stage.prologue", "The deck breaks. Water. Blackout.");
        AdvanceBeat("B050", "stage.prologue", "The Raja's search ship pulls you aboard under blackout.");
        Story.SetFlag("flag.rescued");
        SaveCheckpoint();

        yield return Visit("Docks", "spawn.entry", "B060", "stage.estmere");
        if (Failed()) yield break;
        AdvanceBeat("B070", "stage.estmere", "Guards process the survivors; every memory has the same gap.");
        Story.SetFlag("flag.profile_valid");
        SaveCheckpoint();

        // Character creation happens at the triage table, not in a different scene — B080 is
        // the guards recording who came out of the water.
        AdvanceBeat("B080", "stage.estmere", "The guards record your name, ancestry and origin.");

        yield return Visit("Palace", "spawn.entry", "B090", "stage.assignment");
        if (Failed()) yield break;
        AdvanceBeat("B100", "stage.assignment", "Every soul must contribute.");
        AdvanceBeat("B110", "stage.assignment", "Raja Vikram questions the missing Yuvraj and the pulse.");

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
        AdvanceBeat("B130", "stage.assignment", "Raja Vikram assigns your route.");
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
                yield return Visit("Order_Hall", "spawn.entry", "B300", "stage.mage");
                if (Failed()) yield break;
                // The Siddha Order issues the charge its lesson consumes. B310's manifest is what
                // makes the player look at where that charge came from.
                IssueGear(SoulCrystals.LesserId, SoulCrystals.LesserName, SoulCrystals.ItemKind, 5);
                AddEvidence("ev.crystal_manifest", "Jiva Manifest", "The source column names prisoners transferred under royal seal.");
                AdvanceBeat("B310", "stage.mage", "The jiva-stone delivery exposes an impossible source column.");
                break;
            case "route.trade":
                yield return Visit("Harbor", "spawn.entry", "B400", "stage.trade");
                if (Failed()) yield break;
                IssueGear("hunting_bow", "Hunting Bow", "weapon");
                AdvanceBeat("B410", "stage.trade", "Sailing, stealth, locks and pickpocketing are introduced.");
                yield return Visit("Secured_Tower", "spawn.entry", "B420", "stage.trade");
                if (Failed()) yield break;
                AddEvidence("ev.tower_ledger", "Tower Ledger", "A royal ledger ties the prisoner operation to the east tower.");
                break;
            default:
                yield return Visit("Prison", "spawn.entry", "B500", "stage.refuse");
                if (Failed()) yield break;
                AddEvidence("ev.prisoner_testimony", "Prisoner Testimony", "A named prisoner confirms the living cargo below the palace.");
                AdvanceBeat("B510", "stage.refuse", "A prisoner reveals the black-jiva operation in motion.");
                AdvanceBeat("B520", "stage.refuse", "The route to solitary is the deliberate speed path.");
                break;
        }
        SaveCheckpoint();

        // Convergence contract clause 6: every route enters B600 unarmed, with gear stored
        // rather than destroyed. This is what lets B630's escape be authored once.
        PlayerEquipment.Instance?.StashGear();

        yield return Visit("Prison", "spawn.route", "B320", "stage.convergence");
        if (Failed()) yield break;
        AdvanceBeat("B600", "stage.convergence", "Yuvraj Arun is located.");
        Story.SetFlag("flag.prince_located");
        AdvanceBeat("B610", "stage.convergence", "Arun explains the interception and Raja Vikram's motive.");
        AddEvidence("ev.prince_testimony", "Yuvraj's Testimony", "Arun names the interception, the missing alternative and Raja Vikram's motive.");
        AdvanceBeat("B615", "stage.convergence", "The Stambha and Dhruva Order are seeded for later chapters.");
        SaveCheckpoint();

        yield return Visit("Sea_Cave", "spawn.escape", "B620", "stage.escape");
        if (Failed()) yield break;
        AddEvidence("ev.black_crystal", "Black Jiva", "A resonant shard cages the voices and continuing selves of prisoners.");
        // The evidence room holds what was taken on the way in.
        PlayerEquipment.Instance?.RestoreStashedGear();
        Story.SetCompanion("role.prince", true, "Sea_Cave", "spawn.escape", 100f);
        AdvanceBeat("B630", "stage.escape", "Arun follows you into the sea cave.");
        Story.SetFlag("flag.prince_following");
        yield return PlayTitleCrawl();
        if (Failed()) yield break;
        SaveCheckpoint();

        yield return Visit("Palace_Aftermath", "spawn.entry", "B700", "stage.aftermath");
        if (Failed()) yield break;
        AdvanceBeat("B710", "stage.aftermath", "Evidence is presented; Yuvraj Arun testifies.");
        AdvanceBeat("B720", "stage.aftermath", "Raja Vikram invokes apad-dharma and the supply emergency.");
        string outcome = route == "route.refuse" ? "imprisoned" : "killed";
        Story.SetOutcome(outcome, string.Empty, string.Empty);
        AdvanceBeat("B730", "stage.aftermath", "Raja Vikram's outcome is decided.");
        AdvanceBeat("B740", "stage.aftermath", "Arun is crowned Raja.");
        Story.SetOutcome(string.Empty, "role.prince", string.Empty);
        Story.SetFlag("flag.ruler", "prince");
        AdvanceBeat("B750", "stage.aftermath", "Prisoner jiva-binding is outlawed; prisoners are released.");
        Story.SetFlag("flag.ban_enacted");
        AdvanceBeat("B760", "stage.aftermath", "The player is granted the Rajdoot title.");
        Story.SetOutcome(string.Empty, string.Empty, "title.crown_envoy");
        Story.SetFlag("flag.title_granted");
        AdvanceBeat("B800", "stage.aftermath", "Raja Arun asks for recognition through the Sabha.");
        Story.RecordChoice("choice.council_mission", "seek_recognition");
        SaveCheckpoint();

        yield return Visit("Capital_Exterior", "spawn.caldemar", "B810", "stage.handoff");
        if (Failed()) yield break;
        yield return Visit("Council_Arrival", "spawn.council", "B820", "stage.handoff");
        if (Failed()) yield break;
        AdvanceBeat("B830", "stage.handoff", "The opening chapter is complete; the Sabha awaits.");
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

        if (_playerDriven && TryFindAnchorFor(sceneName, out var anchor))
        {
            yield return WalkTo(anchor, transition);
            if (Failed()) yield break;
            AdvanceBeat(beatId, stageId, GreyThreadSceneCatalog.Find(sceneName)?.Title ?? sceneName);
            SaveCheckpoint();
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

    private static bool TryFindAnchorFor(string sceneName, out CapitalRegion.Anchor anchor)
    {
        foreach (var candidate in CapitalRegion.Anchors)
        {
            if (candidate.SceneName != sceneName) continue;
            anchor = candidate;
            return true;
        }
        anchor = default;
        return false;
    }

    /// <summary>
    /// Put the player outside, tell them where to go in words, and wait until they get there
    /// and open the door themselves.
    ///
    /// Directions rather than a marker, per GAMEPLAY_DESIGN.md. The bearing line is generated
    /// from the player's live position, so it cannot go stale.
    /// </summary>
    private IEnumerator WalkTo(CapitalRegion.Anchor anchor, SceneTransitionService transition)
    {
        if (transition.ActiveContentSceneName != RegionScene)
        {
            yield return transition.TransitionTo(RegionScene, "spawn.region", unloadPrevious: true);
            if (!string.IsNullOrEmpty(transition.LastError))
            {
                LastError = transition.LastError;
                IsRunning = false;
                yield break;
            }

            // Step back out of the door just used, rather than being flung to the docks.
            if (PlayerRef.TryGet(out var player))
            {
                var back = RegionReturn.ReturnPosition();
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.position = back;
                if (controller != null) controller.enabled = true;
            }
        }

        Objective?.Set($"Go to {anchor.DisplayName}", DirectionsTo(anchor), anchor.Id);

        while (transition.ActiveContentSceneName != anchor.SceneName)
        {
            if (!IsRunning) yield break;
            yield return null;
        }

        Objective?.Clear();
    }

    /// <summary>Written directions, in the register a person would actually use.</summary>
    private static string DirectionsTo(CapitalRegion.Anchor anchor)
    {
        bool inside = CapitalRegion.IsInsideCity(anchor.Position);
        string where = inside ? "inside the walls" : "beyond the walls, along the coast";
        return $"{anchor.DisplayName} lies {where}. Follow the streets and look for the door.";
    }

    private static ObjectiveService Objective => ObjectiveService.Instance;

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
        AdvanceBeat("B640", "stage.escape", "RATNA BAY");
    }

    /// <summary>
    /// The audience panel, once it is up. Exposed so the VS2 gate can answer the King the way
    /// a player does, rather than skipping the one scene where the route is chosen.
    /// </summary>
    public GreyThreadAssignmentPanel AssignmentPanel => _assignmentPanel;

    /// <summary>True while the director is waiting for the player to answer the King.</summary>
    public bool AwaitingAssignment { get; private set; }

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
        AwaitingAssignment = true;
        while (string.IsNullOrWhiteSpace(_pendingRoute)) yield return null;
        AwaitingAssignment = false;
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
