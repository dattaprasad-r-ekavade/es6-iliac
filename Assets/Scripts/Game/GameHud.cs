using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    private Image _healthFill, _magickaFill, _staminaFill, _damageFlash, _toastBg;
    private Text _compassText, _statusText, _toastText, _dialogueSpeaker, _dialogueBody;
    private Text _mapList, _journalText, _invText, _promptText, _combatText;
    private Text _healthLabel, _magickaLabel, _staminaLabel;
    private GameObject _mapRoot, _journalRoot, _invRoot, _waitRoot, _pauseRoot, _fade, _dialogueRoot, _hudRoot;
    private RawImage _mapImage;
    private RectTransform _mapPlayerMarker;
    private RectTransform _mapMarkersRoot;
    private float _toastTimer;
    private float _healthDisp = 1f, _magickaDisp = 1f, _staminaDisp = 1f;
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
        PlayerRef.Set(player);
        EnsureEventSystem();
        UiTheme.EnsureLoaded();
        BuildUi();
        ShowFade(false);
        HideMenus();
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

        if (InteractiveMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (_mapRoot != null && _mapRoot.activeSelf)
            RefreshMapVisual();
    }

    private void HandleInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (_fade != null && _fade.activeSelf) return;
        if (_dialogueRoot != null && _dialogueRoot.activeSelf)
        {
            if (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                CloseDialogue();
            return;
        }

        if (kb.mKey.wasPressedThisFrame) Toggle(_mapRoot);
        if (kb.jKey.wasPressedThisFrame) { RefreshJournal(); Toggle(_journalRoot); }
        if (kb.iKey.wasPressedThisFrame) { RefreshInventory(); Toggle(_invRoot); }
        if (kb.tKey.wasPressedThisFrame) Toggle(_waitRoot);
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (_pauseRoot != null && _pauseRoot.activeSelf) ClosePause();
            else if (AnyMenuOpen) HideMenus();
            else OpenPause();
        }

        if (_mapRoot != null && _mapRoot.activeSelf)
        {
            if (kb.upArrowKey.wasPressedThisFrame) { _mapSelected--; RefreshMapList(); GameSfx.Instance?.PlayUiClick(); }
            if (kb.downArrowKey.wasPressedThisFrame) { _mapSelected++; RefreshMapList(); GameSfx.Instance?.PlayUiClick(); }
            if (kb.enterKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame) TryTravelSelected();
        }

        if (_waitRoot != null && _waitRoot.activeSelf)
        {
            if (kb.digit1Key.wasPressedThisFrame) WaitHours(1f);
            if (kb.digit2Key.wasPressedThisFrame) WaitHours(8f);
            if (kb.digit3Key.wasPressedThisFrame) WaitHours(24f);
        }
    }

    private void Toggle(GameObject panel)
    {
        if (panel == null) return;
        bool open = !panel.activeSelf;
        HideMenus();
        panel.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
        if (open)
        {
            GameSfx.Instance?.PlayUiOpen();
            if (panel == _mapRoot) RefreshMapList();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else GameSfx.Instance?.PlayUiClick();

        if (!open && !AnyMenuOpen && GameFlowController.Instance != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void HideMenus()
    {
        if (_mapRoot != null) _mapRoot.SetActive(false);
        if (_journalRoot != null) _journalRoot.SetActive(false);
        if (_invRoot != null) _invRoot.SetActive(false);
        if (_waitRoot != null) _waitRoot.SetActive(false);
        if (_pauseRoot != null) _pauseRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OpenPause()
    {
        HideMenus();
        if (_pauseRoot == null) return;
        _pauseRoot.SetActive(true);
        Time.timeScale = 0f;
        GameSfx.Instance?.PlayUiOpen();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ClosePause()
    {
        if (_pauseRoot != null) _pauseRoot.SetActive(false);
        Time.timeScale = 1f;
        GameSfx.Instance?.PlayUiClick();
        if (!AnyMenuOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void QuitToDesktop()
    {
        Time.timeScale = 1f;
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
        Time.timeScale = 1f;
        if (GameFlowController.Instance != null) GameFlowController.Instance.ReturnToMainMenu();
        else UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void RefreshBars()
    {
        var s = PlayerStats.Instance;
        if (s == null) return;

        float ht = s.Health / Mathf.Max(1f, s.MaxHealth);
        float mt = s.Magicka / Mathf.Max(1f, s.MaxMagicka);
        float st = s.Stamina / Mathf.Max(1f, s.MaxStamina);
        _healthDisp = Mathf.MoveTowards(_healthDisp, ht, Time.unscaledDeltaTime * 2.5f);
        _magickaDisp = Mathf.MoveTowards(_magickaDisp, mt, Time.unscaledDeltaTime * 2.5f);
        _staminaDisp = Mathf.MoveTowards(_staminaDisp, st, Time.unscaledDeltaTime * 2.5f);

        if (_healthFill != null) _healthFill.fillAmount = _healthDisp;
        if (_magickaFill != null) _magickaFill.fillAmount = _magickaDisp;
        if (_staminaFill != null) _staminaFill.fillAmount = _staminaDisp;

        if (_healthLabel != null) _healthLabel.text = $"{Mathf.CeilToInt(s.Health)}/{Mathf.CeilToInt(s.MaxHealth)}";
        if (_magickaLabel != null) _magickaLabel.text = $"{Mathf.CeilToInt(s.Magicka)}/{Mathf.CeilToInt(s.MaxMagicka)}";
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
        if (_compassText == null || _player == null || DiscoveryTravelSystem.Instance == null) return;
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
            sb.AppendLine("No quests yet. Speak with Captain Alid in Daggerfall.");
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
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_dialogueSpeaker != null) _dialogueSpeaker.text = speaker;
        if (_dialogueBody != null)
            _dialogueBody.text = line + "\n\n<size=18><color=#9da09e>E / Enter / Esc  close</color></size>";

        // Track this one coroutine rather than StopAllCoroutines(), which would also
        // kill any unrelated HUD coroutine added later.
        if (_dialogueRoutine != null) StopCoroutine(_dialogueRoutine);
        _dialogueRoutine = StartCoroutine(HideDialogueSoon());
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
        if (_dialogueRoot != null) _dialogueRoot.SetActive(false);
        Time.timeScale = 1f;
        if (GameFlowController.Instance != null && GameFlowController.Instance.IsInGameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
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

        // Skyrim-like spatial rhythm: magicka left, health centre, stamina right.
        var vitals = MakeImage(_hudRoot.transform, "Vitals", null,
            new Vector2(0.04f, 0.012f), new Vector2(0.96f, 0.072f), Color.clear);
        _magickaFill = MakeSpriteBar(vitals.transform, "Magicka", new Vector2(0.00f, 0.20f), new Vector2(0.25f, 0.56f), UiTheme.BarBlue, new Color(0.22f, 0.34f, 0.72f));
        _healthFill = MakeSpriteBar(vitals.transform, "Health", new Vector2(0.385f, 0.20f), new Vector2(0.615f, 0.56f), UiTheme.BarRed, new Color(0.62f, 0.12f, 0.10f));
        _staminaFill = MakeSpriteBar(vitals.transform, "Stamina", new Vector2(0.75f, 0.20f), new Vector2(1.00f, 0.56f), UiTheme.BarGreen, new Color(0.20f, 0.52f, 0.22f));
        _magickaLabel = MakeText(vitals.transform, "MLbl", "", 12, TextAnchor.MiddleRight, new Vector2(0.12f, 0.57f), new Vector2(0.25f, 0.96f), UiTheme.Silver, false, false, true);
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
        MakeText(waitCard.transform, "WaitHint", "Recover health, magicka, and stamina.", 20, TextAnchor.UpperCenter,
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

    private void RefreshMapVisual()
    {
        if (_mapPlayerMarker != null && _player != null)
        {
            var uv = IliacBayMapArt.WorldToMapUV(_player.position);
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
            var uv = IliacBayMapArt.WorldToMapUV(loc.WorldPosition);
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
        _mapImage.texture = IliacBayMapArt.GetMapTexture();
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
