using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Playable VS2 grey-thread driver. It turns the authored Chapter 01 beat contract into
/// deterministic additive scene travel while leaving all final art, dialogue and combat
/// implementation for the later vertical slice.
/// </summary>
public sealed class GreyThreadDirector : MonoBehaviour
{
    public static GreyThreadDirector Instance { get; private set; }

    public bool IsRunning { get; private set; }
    public string ActiveRoute { get; private set; }
    public string LastError { get; private set; }
    public event Action<string> RouteStarted;
    public event Action<string> RouteCompleted;

    private Coroutine _routeRoutine;
    private bool _hintShown;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator Start()
    {
        // The director lives on the persistent GameSystems prefab. Delay the hint until
        // the HUD and player have completed the intro handoff.
        yield return null;
        yield return new WaitForSecondsRealtime(1f);
        ShowRouteHint();
    }

    private void Update()
    {
        if (IsRunning || !Application.isPlaying) return;
        if (GameStateService.Instance != null
            && GameStateService.Instance.CurrentState != GameState.Gameplay)
            return;

        if (Pressed(GameInput.RouteWarrior)) BeginRoute("route.warrior");
        else if (Pressed(GameInput.RouteMage)) BeginRoute("route.mage");
        else if (Pressed(GameInput.RouteTrade)) BeginRoute("route.trade");
        else if (Pressed(GameInput.RouteRefuse)) BeginRoute("route.refuse");
    }

    public void BeginRoute(string routeId)
    {
        if (IsRunning) return;
        ActiveRoute = GreyThreadSceneCatalog.NormalizeRoute(routeId);
        _routeRoutine = StartCoroutine(RunRoute(ActiveRoute));
    }

    public IEnumerator RunRoute(string routeId)
    {
        if (IsRunning) yield break;

        ActiveRoute = GreyThreadSceneCatalog.NormalizeRoute(routeId);
        LastError = null;
        IsRunning = true;
        RouteStarted?.Invoke(ActiveRoute);
        EnsureStoryState(ActiveRoute);

        yield return Visit("Prologue_Ship", "spawn.entry", "B010", "stage.prologue");
        if (Failed()) yield break;
        Story.SetFlag("flag.rescued");

        yield return Visit("Estmere_Docks", "spawn.entry", "B060", "stage.estmere");
        if (Failed()) yield break;
        Story.SetFlag("flag.profile_valid");

        // The extracted exterior remains part of the VS2 scene contract. It is a real
        // additive hop even though the first pass uses the existing generated landscape.
        yield return Visit("Estmere_Exterior", "spawn.caldemar", "B080", "stage.estmere");
        if (Failed()) yield break;

        yield return Visit("Estmere_Palace", "spawn.entry", "B090", "stage.assignment");
        if (Failed()) yield break;
        Story.AdvanceTo("chapter.01", "stage.assignment", "B130");
        Story.SetFlag("flag.route", ActiveRoute);

        switch (ActiveRoute)
        {
            case "route.warrior":
                yield return Visit("Tutorial_Warrior", "spawn.entry", "B200", "stage.warrior");
                if (Failed()) yield break;
                AddEvidence("ev.transport_order", "Transport Order", "A sealed order moves a prisoner beneath the city.");
                Story.AdvanceTo("chapter.01", "stage.warrior", "B220");
                break;
            case "route.mage":
                yield return Visit("Estmere_Arcanum", "spawn.entry", "B300", "stage.mage");
                if (Failed()) yield break;
                AddEvidence("ev.crystal_manifest", "Crystal Manifest", "The source column names prisoners transferred under royal seal.");
                Story.AdvanceTo("chapter.01", "stage.mage", "B310");
                break;
            case "route.trade":
                yield return Visit("Estmere_Harbor", "spawn.entry", "B400", "stage.trade");
                if (Failed()) yield break;
                yield return Visit("Estmere_SecuredTower", "spawn.entry", "B420", "stage.trade");
                if (Failed()) yield break;
                AddEvidence("ev.tower_ledger", "Tower Ledger", "A crown ledger ties the prisoner operation to the east tower.");
                break;
            default:
                yield return Visit("Estmere_Prison", "spawn.entry", "B500", "stage.refuse");
                if (Failed()) yield break;
                AddEvidence("ev.prisoner_testimony", "Prisoner Testimony", "A named prisoner confirms the living cargo below the palace.");
                Story.AdvanceTo("chapter.01", "stage.refuse", "B510");
                break;
        }

        yield return Visit("Estmere_Prison", "spawn.route", "B600", "stage.convergence");
        if (Failed()) yield break;
        Story.SetFlag("flag.prince_located");

        yield return Visit("Estmere_SeaCave", "spawn.escape", "B620", "stage.escape");
        if (Failed()) yield break;
        AddEvidence("ev.black_crystal", "Black Crystal", "A resonant shard remembers the voices of the prisoners.");
        Story.SetCompanion("role.prince", true, "Estmere_SeaCave", "spawn.escape", 100f);
        Story.AdvanceTo("chapter.01", "stage.escape", "B630");
        Story.SetFlag("flag.prince_following");
        Story.SetFlag("flag.title_crawl_shown");

        yield return Visit("Estmere_Palace_Aftermath", "spawn.entry", "B700", "stage.aftermath");
        if (Failed()) yield break;
        Story.SetFlag("flag.king_outcome", ActiveRoute == "route.refuse" ? "imprisoned" : "killed");
        Story.AdvanceTo("chapter.01", "stage.aftermath", "B730");
        Story.SetFlag("flag.ruler", "prince");
        Story.SetFlag("flag.ban_enacted");
        Story.SetFlag("flag.title_granted");
        Story.AdvanceTo("chapter.01", "stage.aftermath", "B760");

        yield return Visit("Caldemar_Arrival", "spawn.council", "B820", "stage.handoff");
        if (Failed()) yield break;
        Story.SetFlag("flag.chapter_complete");
        Story.AdvanceTo("chapter.01", "stage.handoff", "B830");

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

        Story.AdvanceTo("chapter.01", stageId, beatId);
        var spec = GreyThreadSceneCatalog.Find(sceneName);
        GameHud.Instance?.ShowToast($"{spec?.Title ?? sceneName} · {beatId}");
        yield return null;
    }

    private void EnsureStoryState(string routeId)
    {
        var story = Story;
        if (story == null) throw new MissingReferenceException("GreyThreadDirector requires StoryDirector.");
        if (!story.State.Profile.IsValid)
        {
            story.SetProfile(new CharacterProfile
            {
                Name = "The Castaway",
                AncestryId = "anc.isleborn",
                Pronouns = "they/them",
                DeclaredInclination = routeId
            });
        }
        story.SelectRoute(routeId);
    }

    private void AddEvidence(string id, string title, string body)
    {
        Story.AddEvidence(new EvidenceRecord { Id = id, Title = title, DocumentBody = body, Inspected = true });
    }

    private bool Failed() => !IsRunning || !string.IsNullOrEmpty(LastError);

    private StoryDirector Story => StoryDirector.Instance;

    private void ShowRouteHint()
    {
        if (_hintShown || IsRunning) return;
        _hintShown = true;
        GameHud.Instance?.ShowToast("VS2 Grey Thread · F1 Warrior · F2 Mage · F3 Trade · F4 Refuse");
    }

    private static bool Pressed(UnityEngine.InputSystem.InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }
}
