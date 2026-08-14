using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual RPG HUD: sprite panels/bars, compass, map, journal, inventory, wait, dialogue, toasts.
/// </summary>
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }

    public bool AnyMenuOpen =>
        (_pauseRoot != null && _pauseRoot.activeSelf) ||
        (_mapRoot != null && _mapRoot.activeSelf) ||
        (_journalRoot != null && _journalRoot.activeSelf) ||
        (_invRoot != null && _invRoot.activeSelf) ||
        (_waitRoot != null && _waitRoot.activeSelf) ||
        (_dialogueRoot != null && _dialogueRoot.activeSelf) ||
        (_fade != null && _fade.activeSelf);

    private bool InteractiveMenuOpen =>
        (_pauseRoot != null && _pauseRoot.activeSelf) ||
        (_mapRoot != null && _mapRoot.activeSelf) ||
        (_journalRoot != null && _journalRoot.activeSelf) ||
        (_invRoot != null && _invRoot.activeSelf) ||
        (_waitRoot != null && _waitRoot.activeSelf) ||
        (_dialogueRoot != null && _dialogueRoot.activeSelf);

    private Canvas _canvas;
    private Image _healthFill, _manaFill, _staminaFill, _damageFlash, _toastBg;
    private Text _compassText, _statusText, _toastText, _dialogueSpeaker, _dialogueBody;
    private Text _mapList, _journalText, _invText, _promptText, _combatText;
    private Text _healthLabel, _manaLabel, _staminaLabel;
    private GameObject _mapRoot, _journalRoot, _invRoot, _waitRoot, _pauseRoot, _fade, _dialogueRoot, _hudRoot;
    private RawImage _mapImage;
    private RectTransform _mapPlayerMarker;
    private RectTransform _mapMarkersRoot;
    private float _toastTimer;
    private float _healthDisp = 1f, _manaDisp = 1f, _staminaDisp = 1f;
    private int _mapSelected;
    private static Font _fontDisplay;
    private static Font _fontBody;
    private NpcInteractable _focusNpc;

    private GameObject _enemyBarRoot;
    private Image _enemyFill;
    private Text _enemyLabel;
    private float _enemyBarTimer;

    private Coroutine _dialogueRoutine;

    /// <summary>The player transform, resolved through the shared cache.</summary>
    private Transform _player => PlayerRef.Transform;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Build(Transform player)
    {
        GameStateService.Ensure(gameObject);
        PlayerRef.Set(player);
        EnsureEventSystem();
        UiTheme.EnsureLoaded();
        var prefab = Resources.Load<GameObject>("Prefabs/Runtime/Hud");
        if (prefab == null)
            throw new MissingReferenceException("The runtime HUD prefab has not been generated.");
        var visualRoot = Instantiate(prefab, transform);
        visualRoot.name = "GameHudCanvas";
        BindUiReferences(visualRoot.transform);
        WireButtons(visualRoot.transform);
        ShowFade(false);
        HideMenus();
    }

    /// <summary>Editor-builder entry point. Runtime code instantiates the resulting prefab.</summary>
    public void BuildPrefabVisuals()
    {
        UiTheme.EnsureLoaded();
        BuildUi();
        if (_mapImage != null) _mapImage.texture = null;
    }

    private void Update()
    {
        RefreshBars();
        RefreshEnemyBar();
        RefreshCompass();
        RefreshInteractPrompt();
        HandleInput();

        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.unscaledDeltaTime;
            if (_toastTimer <= 0f)
            {
                if (_toastText != null) _toastText.text = "";
                if (_toastBg != null) _toastBg.gameObject.SetActive(false);
            }
        }

        if (_damageFlash != null && _damageFlash.color.a > 0f)
        {
            var c = _damageFlash.color;
            c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 1.6f);
            _damageFlash.color = c;
        }

        if (_mapRoot != null && _mapRoot.activeSelf)
            RefreshMapVisual();
    }

    private void HandleInput()
    {
        var mode = GameStateService.Instance != null
            ? GameStateService.Instance.CurrentState
            : GameState.Gameplay;

        // Pause is read before any state gate, and deliberately so. It used to sit below this
        // return, which meant a game left in Cinematic, Loading or Death had no pause menu —
        // and since quitting lives *in* the pause menu, no way out of the program at all
        // except Alt+F4. Reported from playtest 2026-08-14.
        //
        // Whatever else is broken, the player must always be able to stop playing.
        if (GameInput.Cancel.WasPressedThisFrame()
            && mode != GameState.Gameplay
            && !AnyMenuOpen)
        {
            if (_pauseRoot != null && _pauseRoot.activeSelf) ClosePause();
            else OpenPause();
            return;
        }

        if (mode == GameState.Loading || mode == GameState.Cinematic || mode == GameState.Death)
            return;

        if (_fade != null && _fade.activeSelf) return;
        if (_dialogueRoot != null && _dialogueRoot.activeSelf)
        {
            // While a topic menu is open the number keys choose what to ask about. Closing
            // still works, so the player is never trapped in a conversation.
            if (_topicSpeaker != null && _topicKeywords.Count > 0 && TryPickTopic()) return;

            if (GameInput.Interact.WasPressedThisFrame()
                || GameInput.Submit.WasPressedThisFrame()
                || GameInput.Cancel.WasPressedThisFrame())
                CloseDialogue();
            return;
        }

        if (GameInput.ToggleMap.WasPressedThisFrame()) Toggle(_mapRoot);
        if (GameInput.ToggleJournal.WasPressedThisFrame()) { RefreshJournal(); Toggle(_journalRoot); }
        if (GameInput.ToggleInventory.WasPressedThisFrame()) { RefreshInventory(); Toggle(_invRoot); }
        if (GameInput.ToggleWait.WasPressedThisFrame()) Toggle(_waitRoot);
        if (GameInput.Cancel.WasPressedThisFrame())
        {
            if (_pauseRoot != null && _pauseRoot.activeSelf) ClosePause();
            else if (AnyMenuOpen) HideMenus();
            else OpenPause();
        }

        if (_mapRoot != null && _mapRoot.activeSelf)
        {
            if (GameInput.Navigate.WasPressedThisFrame())
            {
                float direction = GameInput.Navigate.ReadValue<float>();
                if (direction > 0f) _mapSelected--;
                else if (direction < 0f) _mapSelected++;
                RefreshMapList();
                GameSfx.Instance?.PlayUiClick();
            }
            if (GameInput.Submit.WasPressedThisFrame() || GameInput.Travel.WasPressedThisFrame())
                TryTravelSelected();
        }

        if (_waitRoot != null && _waitRoot.activeSelf)
        {
            if (GameInput.WaitOneHour.WasPressedThisFrame()) WaitHours(1f);
            if (GameInput.WaitEightHours.WasPressedThisFrame()) WaitHours(8f);
            if (GameInput.WaitDay.WasPressedThisFrame()) WaitHours(24f);
        }
    }

    private void Toggle(GameObject panel)
    {
        if (panel == null) return;
        bool open = !panel.activeSelf;
        HideMenus();
        panel.SetActive(open);
        GameStateService.Ensure().SetState(
            open ? GameState.Menu : GameState.Gameplay,
            pauseWorld: open);
        if (open)
        {
            GameSfx.Instance?.PlayUiOpen();
            if (panel == _mapRoot) RefreshMapList();
        }
        else GameSfx.Instance?.PlayUiClick();
    }

    private void HideMenus()
    {
        if (_mapRoot != null) _mapRoot.SetActive(false);
        if (_journalRoot != null) _journalRoot.SetActive(false);
        if (_invRoot != null) _invRoot.SetActive(false);
        if (_waitRoot != null) _waitRoot.SetActive(false);
        if (_pauseRoot != null) _pauseRoot.SetActive(false);
        GameStateService.Ensure().SetState(GameState.Gameplay);
    }

    private void OpenPause()
    {
        HideMenus();
        if (_pauseRoot == null) return;
        _pauseRoot.SetActive(true);
        GameStateService.Ensure().SetState(GameState.Menu, pauseWorld: true);
        GameSfx.Instance?.PlayUiOpen();
    }

    private void ClosePause()
    {
        if (_pauseRoot != null) _pauseRoot.SetActive(false);
        GameStateService.Ensure().SetState(GameState.Gameplay);
        GameSfx.Instance?.PlayUiClick();
    }

    private void QuitToDesktop()
    {
        GameStateService.Ensure().SetState(GameState.Menu);
        if (GameFlowController.Instance != null) GameFlowController.Instance.OnClickQuit();
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void QuitToMainMenu()
    {
        GameStateService.Ensure().SetState(GameState.Menu);
        if (GameFlowController.Instance != null) GameFlowController.Instance.ReturnToMainMenu();
        else UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void RefreshBars()
    {
        var s = PlayerStats.Instance;
        if (s == null) return;

        float ht = s.Health / Mathf.Max(1f, s.MaxHealth);
        float mt = s.Mana / Mathf.Max(1f, s.MaxMana);
        float st = s.Stamina / Mathf.Max(1f, s.MaxStamina);
        _healthDisp = Mathf.MoveTowards(_healthDisp, ht, Time.unscaledDeltaTime * 2.5f);
        _manaDisp = Mathf.MoveTowards(_manaDisp, mt, Time.unscaledDeltaTime * 2.5f);
        _staminaDisp = Mathf.MoveTowards(_staminaDisp, st, Time.unscaledDeltaTime * 2.5f);

        if (_healthFill != null) _healthFill.fillAmount = _healthDisp;
        if (_manaFill != null) _manaFill.fillAmount = _manaDisp;
        if (_staminaFill != null) _staminaFill.fillAmount = _staminaDisp;

        if (_healthLabel != null) _healthLabel.text = $"{Mathf.CeilToInt(s.Health)}/{Mathf.CeilToInt(s.MaxHealth)}";
        if (_manaLabel != null) _manaLabel.text = $"{Mathf.CeilToInt(s.Mana)}/{Mathf.CeilToInt(s.MaxMana)}";
        if (_staminaLabel != null) _staminaLabel.text = $"{Mathf.CeilToInt(s.Stamina)}/{Mathf.CeilToInt(s.MaxStamina)}";

        if (_statusText != null)
        {
            var w = TimeWeatherSystem.Instance;
            string weather = w != null ? w.CurrentWeather.ToString() : "?";
            string region = w != null ? w.CurrentRegion : "?";
            float hour = w != null ? w.Hour : 0f;
            int h = Mathf.FloorToInt(hour) % 24;
            int m = Mathf.FloorToInt((hour % 1f) * 60f);
            _statusText.text = $"{region}  ·  {weather}  ·  {h:00}:{m:00}  ·  {s.Gold} gold";
        }

        if (_combatText != null)
        {
            bool combat = PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat;
            _combatText.gameObject.SetActive(combat);
        }
    }

    private void RefreshCompass()
    {
        if (_compassText == null || _player == null) return;

        // An active objective takes the compass line. Directions, not a marker — the bearing
        // is generated live from the player's position so it cannot go stale.
        var objective = ObjectiveService.Instance;
        if (objective != null && objective.HasObjective)
        {
            string bearing = objective.BearingLine();
            _compassText.text = string.IsNullOrEmpty(bearing)
                ? objective.Title
                : $"{objective.Title}  ·  {bearing}";
            return;
        }

        if (DiscoveryTravelSystem.Instance == null) return;
        float yaw = _player.eulerAngles.y;
        string facing = yaw < 45 || yaw >= 315 ? "N" : yaw < 135 ? "E" : yaw < 225 ? "S" : "W";
        var sb = new StringBuilder();
        sb.Append(facing);
        string nearestName = null;
        float nearestDistance = float.MaxValue;
        foreach (var loc in DiscoveryTravelSystem.Instance.Locations)
        {
            if (!loc.Discovered) continue;
            Vector3 to = loc.WorldPosition - _player.position;
            if (to.sqrMagnitude < 0.01f) continue;
            float bearing = Quaternion.LookRotation(to).eulerAngles.y;
            float delta = Mathf.DeltaAngle(yaw, bearing);
            if (Mathf.Abs(delta) < 28f && to.sqrMagnitude < nearestDistance)
            {
                nearestName = loc.DisplayName;
                nearestDistance = to.sqrMagnitude;
            }
        }
        if (!string.IsNullOrEmpty(nearestName))
            sb.Append("   ·   ").Append(nearestName);
        _compassText.text = sb.ToString();
    }

    private void RefreshInteractPrompt()
    {
        if (_promptText == null || _player == null || AnyMenuOpen)
        {
            if (_promptText != null) _promptText.gameObject.SetActive(false);
            return;
        }

        var cam = _player.GetComponentInChildren<Camera>();
        var origin = cam != null ? cam.transform.position : _player.position + Vector3.up;
        var dir = cam != null ? cam.transform.forward : _player.forward;
        _focusNpc = null;
        if (Physics.SphereCast(origin, 0.4f, dir, out var hit, 3.2f,
                GameLayers.InteractMask, QueryTriggerInteraction.Ignore))
            _focusNpc = hit.collider.GetComponentInParent<NpcInteractable>();

        if (_focusNpc != null)
        {
            _promptText.gameObject.SetActive(true);
            string tag = _focusNpc.IsMerchant ? "Merchant" : _focusNpc.IsQuestGiver ? "Quest" : "Talk";
            _promptText.text = $"[E]  {_focusNpc.NpcName}  ·  {tag}";
        }
        else _promptText.gameObject.SetActive(false);
    }

    private void RefreshMapList()
    {
        if (_mapList == null || DiscoveryTravelSystem.Instance == null) return;
        var locs = DiscoveryTravelSystem.Instance.Locations;
        _mapSelected = Mathf.Clamp(_mapSelected, 0, Mathf.Max(0, locs.Count - 1));
        var sb = new StringBuilder();
        sb.AppendLine("↑ / ↓ select destination");
        sb.AppendLine("Enter / F travel   ·   M close");
        sb.AppendLine();
        for (int i = 0; i < locs.Count; i++)
        {
            var l = locs[i];
            if (!l.Discovered)
            {
                sb.AppendLine("     ·  ???");
                continue;
            }
            string mark = i == _mapSelected ? "▶" : " ";
            string city = l.IsCity ? "City" : "Landmark";
            float dist = _player != null ? Vector3.Distance(_player.position, l.WorldPosition) : 0f;
            sb.AppendLine($"{mark}  {l.DisplayName}   [{city}]   {dist:0} m");
        }
        if (PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat)
            sb.AppendLine("\nFast travel blocked while in combat.");
        _mapList.text = sb.ToString();
    }

    private void TryTravelSelected()
    {
        var locs = DiscoveryTravelSystem.Instance?.Locations;
        if (locs == null || locs.Count == 0) return;
        _mapSelected = Mathf.Clamp(_mapSelected, 0, locs.Count - 1);
        var loc = locs[_mapSelected];
        if (!loc.Discovered)
        {
            ShowToast("Not yet discovered");
            GameSfx.Instance?.PlayUiError();
            return;
        }
        if (!DiscoveryTravelSystem.Instance.CanFastTravel(loc.Id))
        {
            ShowToast("Cannot fast travel now");
            GameSfx.Instance?.PlayUiError();
            return;
        }
        HideMenus();
        GameSfx.Instance?.PlayUiConfirm();
        // The travel routine reports arrival (including elapsed time) when it finishes.
        DiscoveryTravelSystem.Instance.FastTravel(loc.Id);
    }

    private void RefreshJournal()
    {
        if (_journalText == null || QuestSystem.Instance == null) return;
        var sb = new StringBuilder();
        foreach (var q in QuestSystem.Instance.Quests)
        {
            string state = q.Completed ? "COMPLETE" : q.Active ? "ACTIVE" : "AVAILABLE";
            sb.AppendLine($"▸  {q.Title}   ({state})");
            sb.AppendLine($"     {q.Description}");
            sb.AppendLine($"     →  {q.StageText}");
            sb.AppendLine();
        }
        if (QuestSystem.Instance.Quests.Count == 0)
            sb.AppendLine("No quests yet. Speak with Captain Alid in Sabhapur.");
        _journalText.text = sb.ToString();
    }

    private void RefreshInventory()
    {
        if (_invText == null || PlayerInventory.Instance == null) return;
        var sb = new StringBuilder();
        sb.AppendLine("Q  drink potion     LMB / 1  melee     2  flare");
        sb.AppendLine();
        foreach (var item in PlayerInventory.Instance.Items)
            sb.AppendLine($"  ▸  {item.Name}   ×{item.Count}     ({item.Kind})");
        _invText.text = sb.ToString();
    }

    private void WaitHours(float h)
    {
        if (PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat)
        {
            ShowToast("Cannot wait in combat");
            GameSfx.Instance?.PlayUiError();
            return;
        }
        TimeWeatherSystem.Instance?.AdvanceHours(h);
        PlayerStats.Instance?.FullRestore();
        GameSfx.Instance?.PlayUiConfirm();
        ShowToast($"Rested {h:0} hours");
        HideMenus();
    }

    public void ShowToast(string msg)
    {
        if (_toastText == null) return;
        _toastText.text = msg;
        _toastTimer = 3.2f;
        if (_toastBg != null) _toastBg.gameObject.SetActive(true);
    }

    public void FlashDamage()
    {
        if (_damageFlash == null) return;
        var c = _damageFlash.color;
        c.a = 0.45f;
        _damageFlash.color = c;
    }

    public void ShowDialogue(string speaker, string line)
    {
        if (_dialogueRoot == null) return;
        HideMenus();
        _dialogueRoot.SetActive(true);
        GameStateService.Ensure().PushState(GameState.Dialogue);
        if (_dialogueSpeaker != null) _dialogueSpeaker.text = speaker;
        if (_dialogueBody != null)
            _dialogueBody.text = line + "\n\n<size=18><color=#9da09e>E / Enter / Esc  close</color></size>";

        // Track this one coroutine rather than StopAllCoroutines(), which would also
        // kill any unrelated HUD coroutine added later.
        if (_dialogueRoutine != null) StopCoroutine(_dialogueRoutine);
        _dialogueRoutine = StartCoroutine(HideDialogueSoon());
    }

    private SpeakingActor _topicSpeaker;
    private readonly List<string> _topicKeywords = new();
    private const int TopicsPerPage = 9;
    private int _topicPage;

    /// <summary>Whether a topic menu is currently open. Exposed for tests.</summary>
    public bool TopicMenuOpen => _topicSpeaker != null && _dialogueRoot != null && _dialogueRoot.activeSelf;

    /// <summary>The keywords currently on offer. Exposed for tests.</summary>
    public IReadOnlyList<string> OfferedTopics => _topicKeywords;

    /// <summary>
    /// Show the keywords an actor will answer. Morrowind's model — the player picks a subject
    /// rather than a line, so what they know to ask is the real inventory.
    ///
    /// The menu does not auto-hide the way a one-line barks does; a conversation ends when the
    /// player ends it.
    /// </summary>
    public void ShowTopicMenu(SpeakingActor speaker, IReadOnlyList<string> keywords)
    {
        if (_dialogueRoot == null || speaker == null) return;

        _topicSpeaker = speaker;
        _topicKeywords.Clear();
        _topicPage = 0;
        if (keywords != null)
            for (int i = 0; i < keywords.Count; i++) _topicKeywords.Add(keywords[i]);

        HideMenus();
        _dialogueRoot.SetActive(true);
        GameStateService.Ensure().PushState(GameState.Dialogue);
        if (_dialogueSpeaker != null) _dialogueSpeaker.text = speaker.DisplayName;

        if (_dialogueRoutine != null) { StopCoroutine(_dialogueRoutine); _dialogueRoutine = null; }
        RenderTopicList();
    }

    private void RenderTopicList()
    {
        if (_dialogueBody == null) return;

        var sb = new StringBuilder();
        sb.Append("What do you want to ask about?\n\n");
        int first = _topicPage * TopicsPerPage;
        int last = Mathf.Min(first + TopicsPerPage, _topicKeywords.Count);
        for (int i = first; i < last; i++)
            sb.Append(i - first + 1).Append(".  ").Append(_topicKeywords[i]).Append('\n');
        int pages = Mathf.Max(1, Mathf.CeilToInt(_topicKeywords.Count / (float)TopicsPerPage));
        if (pages > 1)
            sb.Append("\nPage ").Append(_topicPage + 1).Append('/').Append(pages)
              .Append("  ·  P/N previous/next");
        sb.Append("\n<size=18><color=#9da09e>1-9 ask  ·  E / Enter / Esc  leave</color></size>");
        _dialogueBody.text = sb.ToString();
    }

    /// <summary>Answer a topic, then hand the list back so the conversation continues.</summary>
    public bool AskTopic(int index)
    {
        if (_topicSpeaker == null || index < 0 || index >= _topicKeywords.Count) return false;

        string keyword = _topicKeywords[index];
        string response = _topicSpeaker.Ask(keyword);
        if (string.IsNullOrEmpty(response)) return false;

        if (_dialogueBody != null)
        {
            _dialogueBody.text = response +
                "\n\n<size=18><color=#9da09e>1-9 ask again  ·  E / Enter / Esc  leave</color></size>";
        }

        // Asking can teach new keywords, so the menu is rebuilt from what is now askable.
        _topicKeywords.Clear();
        foreach (var available in _topicSpeaker.AvailableTopics())
            _topicKeywords.Add(available);
        int pages = Mathf.Max(1, Mathf.CeilToInt(_topicKeywords.Count / (float)TopicsPerPage));
        _topicPage = Mathf.Clamp(_topicPage, 0, pages - 1);
        return true;
    }

    private bool TryPickTopic()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return false;

        int pages = Mathf.Max(1, Mathf.CeilToInt(_topicKeywords.Count / (float)TopicsPerPage));
        if ((keyboard.nKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame
             || keyboard.rightArrowKey.wasPressedThisFrame) && _topicPage + 1 < pages)
        {
            _topicPage++;
            RenderTopicList();
            return true;
        }
        if ((keyboard.pKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame
             || keyboard.leftArrowKey.wasPressedThisFrame) && _topicPage > 0)
        {
            _topicPage--;
            RenderTopicList();
            return true;
        }

        var digits = new[]
        {
            keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key,
            keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key,
            keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key
        };

        int first = _topicPage * TopicsPerPage;
        int visible = Mathf.Min(TopicsPerPage, _topicKeywords.Count - first);
        for (int i = 0; i < digits.Length && i < visible; i++)
            if (digits[i] != null && digits[i].wasPressedThisFrame)
                return AskTopic(first + i);

        return false;
    }

    private IEnumerator HideDialogueSoon()
    {
        yield return new WaitForSecondsRealtime(8f);
        CloseDialogue();
        _dialogueRoutine = null;
    }

    private void CloseDialogue()
    {
        if (_dialogueRoutine != null)
        {
            StopCoroutine(_dialogueRoutine);
            _dialogueRoutine = null;
        }
        _topicSpeaker = null;
        _topicKeywords.Clear();
        if (_dialogueRoot != null) _dialogueRoot.SetActive(false);
        var state = GameStateService.Ensure();
        if (!state.PopState(GameState.Dialogue))
            state.SetState(GameFlowController.Instance != null && GameFlowController.Instance.IsInGameplay
                ? GameState.Gameplay
                : GameState.Menu);
    }

    /// <summary>
    /// Show the health of the enemy that was just hit. This used to be a toast per
    /// swing ("Bandit hit (37 hp)"), which drowned out every other notification.
    /// </summary>
    public void ShowEnemyHealth(string enemyName, float health, float maxHealth)
    {
        if (_enemyBarRoot == null) return;
        _enemyBarRoot.SetActive(true);
        _enemyBarTimer = 4f;
        if (_enemyLabel != null) _enemyLabel.text = $"{enemyName}   {Mathf.CeilToInt(health)}/{Mathf.CeilToInt(maxHealth)}";
        if (_enemyFill != null) _enemyFill.fillAmount = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
    }

    private void RefreshEnemyBar()
    {
        if (_enemyBarRoot == null || !_enemyBarRoot.activeSelf) return;
        _enemyBarTimer -= Time.deltaTime;
        if (_enemyBarTimer <= 0f) _enemyBarRoot.SetActive(false);
    }

    public void ShowFade(bool on)
    {
        if (_fade != null) _fade.SetActive(on);
    }

    private void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) es.AddComponent(t);
        else es.AddComponent<StandaloneInputModule>();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("GameHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureFonts();

        _hudRoot = new GameObject("Hud", typeof(RectTransform));
        _hudRoot.transform.SetParent(canvasGo.transform, false);
        StretchFull(_hudRoot.GetComponent<RectTransform>());

        // Thin, low-contrast compass: readable without covering the view.
        var compassBg = MakeImage(_hudRoot.transform, "CompassBg", UiTheme.PanelBrown,
            new Vector2(0.33f, 0.952f), new Vector2(0.67f, 0.988f), UiTheme.PanelSoft);
        _compassText = MakeText(compassBg.transform, "Compass", "", 18, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), new Color(0.96f, 0.9f, 0.68f),
            wrap: false, display: true, outline: true);

        // Vitals rhythm: prana left, health centre, stamina right. The serialized stat keeps
        // its legacy field name so old saves remain compatible.
        var vitals = MakeImage(_hudRoot.transform, "Vitals", null,
            new Vector2(0.04f, 0.012f), new Vector2(0.96f, 0.072f), Color.clear);
        _manaFill = MakeSpriteBar(vitals.transform, "Prana", new Vector2(0.00f, 0.20f), new Vector2(0.25f, 0.56f), UiTheme.BarBlue, new Color(0.22f, 0.34f, 0.72f));
        _healthFill = MakeSpriteBar(vitals.transform, "Health", new Vector2(0.385f, 0.20f), new Vector2(0.615f, 0.56f), UiTheme.BarRed, new Color(0.62f, 0.12f, 0.10f));
        _staminaFill = MakeSpriteBar(vitals.transform, "Stamina", new Vector2(0.75f, 0.20f), new Vector2(1.00f, 0.56f), UiTheme.BarGreen, new Color(0.20f, 0.52f, 0.22f));
        _manaLabel = MakeText(vitals.transform, "MLbl", "", 12, TextAnchor.MiddleRight, new Vector2(0.12f, 0.57f), new Vector2(0.25f, 0.96f), UiTheme.Silver, false, false, true);
        _healthLabel = MakeText(vitals.transform, "HLbl", "", 12, TextAnchor.MiddleCenter, new Vector2(0.42f, 0.57f), new Vector2(0.58f, 0.96f), UiTheme.Silver, false, false, true);
        _staminaLabel = MakeText(vitals.transform, "SLbl", "", 12, TextAnchor.MiddleLeft, new Vector2(0.75f, 0.57f), new Vector2(0.88f, 0.96f), UiTheme.Silver, false, false, true);

        // Compact world/status readout in the upper-right.
        var statusBg = MakeImage(_hudRoot.transform, "StatusBg", null,
            new Vector2(0.64f, 0.905f), new Vector2(0.975f, 0.945f), Color.clear);
        _statusText = MakeText(statusBg.transform, "Status", "", 14, TextAnchor.MiddleRight,
            Vector2.zero, Vector2.one, UiTheme.MutedSilver,
            wrap: false, display: false, outline: true);

        // Minimal centre dot.
        var cross = MakeImage(_hudRoot.transform, "Crosshair", null,
            new Vector2(0.4985f, 0.4973f), new Vector2(0.5015f, 0.5027f), new Color(0.82f, 0.84f, 0.82f, 0.72f));
        if (cross != null) cross.type = Image.Type.Simple;

        _promptText = MakeText(_hudRoot.transform, "Prompt", "", 22, TextAnchor.MiddleCenter,
            new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.44f), new Color(0.98f, 0.92f, 0.7f),
            wrap: false, display: true, outline: true);
        _promptText.gameObject.SetActive(false);

        // Target health readout (replaces one toast per sword swing).
        _enemyBarRoot = MakeImage(_hudRoot.transform, "EnemyBar", UiTheme.PanelInset,
            new Vector2(0.38f, 0.858f), new Vector2(0.62f, 0.898f), UiTheme.Inset).gameObject;
        _enemyLabel = MakeText(_enemyBarRoot.transform, "EnemyName", "", 16, TextAnchor.UpperCenter,
            new Vector2(0.03f, 0.5f), new Vector2(0.97f, 1.02f), new Color(0.98f, 0.92f, 0.78f),
            wrap: false, display: false, outline: true);
        _enemyFill = MakeSpriteBar(_enemyBarRoot.transform, "EnemyHealth",
            new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.5f), UiTheme.BarRed, new Color(0.8f, 0.2f, 0.18f));
        _enemyBarRoot.SetActive(false);

        _combatText = MakeText(_hudRoot.transform, "Combat", "IN COMBAT", 20, TextAnchor.UpperRight,
            new Vector2(0.78f, 0.86f), new Vector2(0.98f, 0.91f), new Color(1f, 0.45f, 0.35f),
            wrap: false, display: true, outline: true);
        _combatText.gameObject.SetActive(false);

        // Toast
        _toastBg = MakeImage(_hudRoot.transform, "ToastBg", UiTheme.PanelBrown,
            new Vector2(0.30f, 0.755f), new Vector2(0.70f, 0.825f), UiTheme.PanelSoft);
        _toastBg.gameObject.SetActive(false);
        _toastText = MakeText(_toastBg.transform, "Toast", "", 26, TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f), new Color(1f, 0.95f, 0.8f),
            wrap: true, display: true, outline: true);

        _damageFlash = MakeImage(_hudRoot.transform, "DamageFlash", null,
            Vector2.zero, Vector2.one, new Color(0.7f, 0.05f, 0.02f, 0f));
        _damageFlash.raycastTarget = false;

        // Menus
        _mapRoot = BuildMapPanel(canvasGo.transform, out _mapList);
        _pauseRoot = BuildPausePanel(canvasGo.transform);
        _journalRoot = MakeMenuWindow(canvasGo.transform, "JournalPanel", "JOURNAL", out _journalText);
        _invRoot = MakeMenuWindow(canvasGo.transform, "InvPanel", "INVENTORY", out _invText);

        _waitRoot = MakeDimOverlay(canvasGo.transform, "WaitPanel");
        var waitCard = MakeImage(_waitRoot.transform, "WaitCard", UiTheme.PanelBrown,
            new Vector2(0.31f, 0.25f), new Vector2(0.69f, 0.75f), UiTheme.Panel);
        MakeText(waitCard.transform, "WaitTitle", "REST", 40, TextAnchor.UpperCenter,
            new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.95f), new Color(0.96f, 0.9f, 0.7f), false, true, true);
        MakeText(waitCard.transform, "WaitHint", "Recover health and stamina. Prana does not replenish by resting.", 20, TextAnchor.UpperCenter,
            new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.72f), new Color(0.9f, 0.85f, 0.75f), true, false, true);
        MakeWaitButton(waitCard.transform, "1 Hour", new Vector2(0.12f, 0.38f), new Vector2(0.88f, 0.52f), () => WaitHours(1f));
        MakeWaitButton(waitCard.transform, "8 Hours", new Vector2(0.12f, 0.22f), new Vector2(0.88f, 0.36f), () => WaitHours(8f));
        MakeWaitButton(waitCard.transform, "24 Hours", new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.2f), () => WaitHours(24f));

        _dialogueRoot = MakeDimOverlay(canvasGo.transform, "DialoguePanel", dimAlpha: 0.35f);
        var dlg = MakeImage(_dialogueRoot.transform, "DlgCard", UiTheme.PanelBrown,
            new Vector2(0.17f, 0.055f), new Vector2(0.83f, 0.255f), UiTheme.Panel);
        _dialogueSpeaker = MakeText(dlg.transform, "Speaker", "", 26, TextAnchor.UpperLeft,
            new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.92f), new Color(1f, 0.85f, 0.45f), false, true, true);
        _dialogueBody = MakeText(dlg.transform, "Body", "", 24, TextAnchor.UpperLeft,
            new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.6f), new Color(0.96f, 0.93f, 0.85f), true, false, true);
        _dialogueBody.supportRichText = true;

        _fade = MakeDimOverlay(canvasGo.transform, "Fade", dimAlpha: 1f);
        _fade.transform.SetAsLastSibling();
        var fadeImg = _fade.GetComponent<Image>();
        if (fadeImg != null) fadeImg.color = Color.black;
    }

    private void BindUiReferences(Transform root)
    {
        _canvas = root.GetComponent<Canvas>();
        _hudRoot = At(root, "Hud").gameObject;
        _compassText = At<Text>(root, "Hud/CompassBg/Compass");
        _statusText = At<Text>(root, "Hud/StatusBg/Status");
        _promptText = At<Text>(root, "Hud/Prompt");
        _combatText = At<Text>(root, "Hud/Combat");
        _healthFill = At<Image>(root, "Hud/Vitals/HealthBg/HealthFill");
        _manaFill = At<Image>(root, "Hud/Vitals/PranaBg/PranaFill");
        _staminaFill = At<Image>(root, "Hud/Vitals/StaminaBg/StaminaFill");
        _healthLabel = At<Text>(root, "Hud/Vitals/HLbl");
        _manaLabel = At<Text>(root, "Hud/Vitals/MLbl");
        _staminaLabel = At<Text>(root, "Hud/Vitals/SLbl");
        _enemyBarRoot = At(root, "Hud/EnemyBar").gameObject;
        _enemyLabel = At<Text>(root, "Hud/EnemyBar/EnemyName");
        _enemyFill = At<Image>(root, "Hud/EnemyBar/EnemyHealthBg/EnemyHealthFill");
        _toastBg = At<Image>(root, "Hud/ToastBg");
        _toastText = At<Text>(root, "Hud/ToastBg/Toast");
        _damageFlash = At<Image>(root, "Hud/DamageFlash");

        _mapRoot = At(root, "MapPanel").gameObject;
        _pauseRoot = At(root, "PausePanel").gameObject;
        _journalRoot = At(root, "JournalPanel").gameObject;
        _invRoot = At(root, "InvPanel").gameObject;
        _waitRoot = At(root, "WaitPanel").gameObject;
        _dialogueRoot = At(root, "DialoguePanel").gameObject;
        _fade = At(root, "Fade").gameObject;
        _mapList = At<Text>(root, "MapPanel/Card/ListInset/Body");
        _journalText = At<Text>(root, "JournalPanel/Card/Inset/Body");
        _invText = At<Text>(root, "InvPanel/Card/Inset/Body");
        _dialogueSpeaker = At<Text>(root, "DialoguePanel/DlgCard/Speaker");
        _dialogueBody = At<Text>(root, "DialoguePanel/DlgCard/Body");
        _mapImage = At<RawImage>(root, "MapPanel/Card/MapFrame/MapImage");
        _mapMarkersRoot = At(root, "MapPanel/Card/MapFrame/MapImage/Markers") as RectTransform;
        _mapPlayerMarker = At(root, "MapPanel/Card/MapFrame/MapImage/Markers/PlayerMarker") as RectTransform;
        _mapImage.texture = KessilMapArt.GetMapTexture();
    }

    private void WireButtons(Transform root)
    {
        Wire(root, "PausePanel/PauseCard/ResumeBtn", ClosePause);
        Wire(root, "PausePanel/PauseCard/MainMenuBtn", QuitToMainMenu);
        Wire(root, "PausePanel/PauseCard/QuitGameBtn", QuitToDesktop);
        Wire(root, "WaitPanel/WaitCard/1HourBtn", () => WaitHours(1f));
        Wire(root, "WaitPanel/WaitCard/8HoursBtn", () => WaitHours(8f));
        Wire(root, "WaitPanel/WaitCard/24HoursBtn", () => WaitHours(24f));
    }

    private static void Wire(Transform root, string path, UnityEngine.Events.UnityAction action)
    {
        var button = At<Button>(root, path);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static Transform At(Transform root, string path)
    {
        var result = root.Find(path);
        if (result == null) throw new MissingReferenceException($"HUD prefab is missing '{path}'.");
        return result;
    }

    private static T At<T>(Transform root, string path) where T : Component
    {
        var component = At(root, path).GetComponent<T>();
        if (component == null)
            throw new MissingComponentException($"HUD prefab path '{path}' needs {typeof(T).Name}.");
        return component;
    }

    private void RefreshMapVisual()
    {
        if (_mapPlayerMarker != null && _player != null)
        {
            var uv = KessilMapArt.WorldToMapUV(_player.position);
            _mapPlayerMarker.anchorMin = uv;
            _mapPlayerMarker.anchorMax = uv;
            _mapPlayerMarker.anchoredPosition = Vector2.zero;
        }

        if (_mapMarkersRoot == null || DiscoveryTravelSystem.Instance == null) return;
        // Child zero is always the red player marker. Location markers begin at one.
        int child = 1;
        foreach (var loc in DiscoveryTravelSystem.Instance.Locations)
        {
            if (!loc.Discovered) continue;
            RectTransform marker;
            if (child < _mapMarkersRoot.childCount)
                marker = _mapMarkersRoot.GetChild(child) as RectTransform;
            else
            {
                var go = new GameObject("LocMarker", typeof(RectTransform));
                go.transform.SetParent(_mapMarkersRoot, false);
                marker = go.GetComponent<RectTransform>();
                var img = go.AddComponent<Image>();
                img.color = loc.IsCity ? new Color(1f, 0.85f, 0.35f) : new Color(0.75f, 0.9f, 1f);
                img.raycastTarget = false;
            }
            var uv = KessilMapArt.WorldToMapUV(loc.WorldPosition);
            marker.anchorMin = uv;
            marker.anchorMax = uv;
            marker.sizeDelta = new Vector2(loc.IsCity ? 14f : 10f, loc.IsCity ? 14f : 10f);
            marker.anchoredPosition = Vector2.zero;
            child++;
        }
        for (int i = _mapMarkersRoot.childCount - 1; i >= child && i > 0; i--)
            Destroy(_mapMarkersRoot.GetChild(i).gameObject);
    }

    private GameObject BuildMapPanel(Transform parent, out Text listText)
    {
        var root = MakeDimOverlay(parent, "MapPanel");
        var card = MakeImage(root.transform, "Card", UiTheme.PanelBrown,
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), UiTheme.Panel);
        MakeText(card.transform, "Title", "WORLD MAP", 36, TextAnchor.UpperCenter,
            new Vector2(0.08f, 0.9f), new Vector2(0.92f, 0.98f), new Color(0.96f, 0.9f, 0.7f), false, true, true);

        var mapFrame = MakeImage(card.transform, "MapFrame", UiTheme.PanelInset,
            new Vector2(0.04f, 0.08f), new Vector2(0.58f, 0.88f), UiTheme.Inset);
        var mapGo = new GameObject("MapImage", typeof(RectTransform));
        mapGo.transform.SetParent(mapFrame.transform, false);
        var mapRt = mapGo.GetComponent<RectTransform>();
        StretchFull(mapRt);
        mapRt.offsetMin = new Vector2(6f, 6f);
        mapRt.offsetMax = new Vector2(-6f, -6f);
        _mapImage = mapGo.AddComponent<RawImage>();
        _mapImage.texture = KessilMapArt.GetMapTexture();
        _mapImage.color = Color.white;

        _mapMarkersRoot = new GameObject("Markers", typeof(RectTransform)).GetComponent<RectTransform>();
        _mapMarkersRoot.SetParent(mapGo.transform, false);
        StretchFull(_mapMarkersRoot);

        var playerGo = new GameObject("PlayerMarker", typeof(RectTransform));
        playerGo.transform.SetParent(_mapMarkersRoot, false);
        _mapPlayerMarker = playerGo.GetComponent<RectTransform>();
        _mapPlayerMarker.sizeDelta = new Vector2(16f, 16f);
        var playerImg = playerGo.AddComponent<Image>();
        playerImg.color = new Color(1f, 0.25f, 0.2f);
        playerImg.raycastTarget = false;

        var inset = MakeImage(card.transform, "ListInset", UiTheme.PanelInset,
            new Vector2(0.6f, 0.08f), new Vector2(0.96f, 0.88f), UiTheme.Inset);
        listText = MakeText(inset.transform, "Body", "", 20, TextAnchor.UpperLeft,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), UiTheme.Silver,
            wrap: true, display: false, outline: false);
        return root;
    }

    private GameObject BuildPausePanel(Transform parent)
    {
        var root = MakeDimOverlay(parent, "PausePanel");
        var card = MakeImage(root.transform, "PauseCard", UiTheme.PanelBrown,
            new Vector2(0.35f, 0.25f), new Vector2(0.65f, 0.75f), UiTheme.Panel);
        MakeText(card.transform, "PauseTitle", "PAUSED", 42, TextAnchor.UpperCenter,
            new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.95f), new Color(0.96f, 0.9f, 0.7f), false, true, true);
        MakeText(card.transform, "PauseHint", "Game paused", 18, TextAnchor.UpperCenter,
            new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.72f), new Color(0.9f, 0.85f, 0.75f), false, false, true);
        MakeWaitButton(card.transform, "Resume", new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.54f), ClosePause);
        MakeWaitButton(card.transform, "Main Menu", new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.4f), QuitToMainMenu);
        MakeWaitButton(card.transform, "Quit Game", new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.26f), QuitToDesktop);
        return root;
    }

    private GameObject MakeMenuWindow(Transform parent, string name, string title, out Text body)
    {
        var root = MakeDimOverlay(parent, name);
        var card = MakeImage(root.transform, "Card", UiTheme.PanelBrown,
            new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.90f), UiTheme.Panel);
        MakeText(card.transform, "Title", title, 36, TextAnchor.UpperCenter,
            new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.98f), new Color(0.96f, 0.9f, 0.7f), false, true, true);
        var inset = MakeImage(card.transform, "Inset", UiTheme.PanelInset,
            new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.86f), UiTheme.Inset);
        body = MakeText(inset.transform, "Body", "", 22, TextAnchor.UpperLeft,
            new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), UiTheme.Silver,
            wrap: true, display: false, outline: false);
        return root;
    }

    private void MakeWaitButton(Transform parent, string label, Vector2 amin, Vector2 amax, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label.Replace(" ", "") + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        UiTheme.StyleButton(btn, UiTheme.ButtonLong, UiTheme.ButtonLongPressed);
        btn.onClick.AddListener(action);
        MakeText(go.transform, "Label", label, 24, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, UiTheme.Silver, false, true, false);
    }

    private static GameObject MakeDimOverlay(Transform parent, string name, float dimAlpha = 0.72f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.02f, 0.02f, 0.03f, dimAlpha);
        go.SetActive(false);
        return go;
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 amin, Vector2 amax, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        UiTheme.StylePanel(img, sprite, tint);
        return img;
    }

    private static Image MakeSpriteBar(Transform parent, string name, Vector2 amin, Vector2 amax, Sprite fillSprite, Color fallback)
    {
        var bg = new GameObject(name + "Bg");
        bg.transform.SetParent(parent, false);
        var brt = bg.AddComponent<RectTransform>();
        brt.anchorMin = amin; brt.anchorMax = amax; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        if (UiTheme.BarBack != null)
        {
            bgImg.sprite = UiTheme.BarBack;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.055f, 0.06f, 0.065f, 0.9f);
        }
        else bgImg.color = new Color(0f, 0f, 0f, 0.55f);

        var fill = new GameObject(name + "Fill");
        fill.transform.SetParent(bg.transform, false);
        var frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.02f, 0.15f);
        frt.anchorMax = new Vector2(0.98f, 0.85f);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        var img = fill.AddComponent<Image>();
        if (fillSprite != null)
        {
            img.sprite = fillSprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = fallback;
        }
        else
        {
            img.color = fallback;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
        }
        img.fillAmount = 1f;
        return img;
    }

    private static void EnsureFonts()
    {
        if (_fontDisplay == null)
            _fontDisplay = Resources.Load<Font>("Fonts/CinzelDecorative-Regular")
                           ?? Resources.Load<Font>("Fonts/Cinzel-Regular");
        if (_fontBody == null)
            _fontBody = Resources.Load<Font>("Fonts/EBGaramond")
                        ?? Resources.Load<Font>("Fonts/Cinzel-Regular")
                        ?? _fontDisplay;
        if (_fontDisplay == null)
            _fontDisplay = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                           ?? Font.CreateDynamicFontFromOSFont("Georgia", 24);
        if (_fontBody == null)
            _fontBody = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Font.CreateDynamicFontFromOSFont("Georgia", 20);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
    }

    private static Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor,
        Vector2 amin, Vector2 amax, Color color, bool wrap = true, bool display = false, bool outline = false)
    {
        EnsureFonts();
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin;
        rt.anchorMax = amax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = display ? _fontDisplay : _fontBody;
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (outline)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.25f, -1.25f);
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.55f);
            s.effectDistance = new Vector2(2f, -2f);
        }

        return text;
    }
}
