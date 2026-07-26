using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Title menu → intro cutscene → gameplay.
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject subtitleRoot;
    [SerializeField] private Text speakerLabel;
    [SerializeField] private Text subtitleLabel;
    [SerializeField] private Text skipHintLabel;
    [SerializeField] private Button skipButton;
    [SerializeField] private CanvasGroup menuFade;
    [SerializeField] private CanvasGroup subtitleFade;

    [Header("World")]
    [SerializeField] private IliacBayWorldGenerator worldGenerator;
    [SerializeField] private Transform player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private IntroCutsceneDirector cutscene;

    [Header("Timing")]
    [SerializeField] private float menuFadeSeconds = 0.75f;

    /// <summary>True between START and the handoff to gameplay — i.e. while the
    /// cutscene is running and skip input is meaningful.</summary>
    private bool _inIntro;

    private bool _skipRequested;

    /// <summary>True once the player has control.</summary>
    public bool IsInGameplay { get; private set; }

    private void Awake()
    {
        Instance = this;
        ResolveRefs();
        if (menuRoot != null) menuRoot.SetActive(true);
        if (subtitleRoot != null) subtitleRoot.SetActive(false);
        if (cinematicCamera != null) cinematicCamera.gameObject.SetActive(false);
        SetPlayerActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(RequestSkip);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Only listen for skip input during the intro. This used to stay armed for the
        // whole session, so every Space (jump) and Esc (menu) during gameplay was also
        // recorded as a cutscene-skip request.
        if (!_inIntro) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;
        if (kb.spaceKey.wasPressedThisFrame
            || kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame
            || kb.escapeKey.wasPressedThisFrame
            || kb.tabKey.wasPressedThisFrame)
        {
            RequestSkip();
        }

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            RequestSkip();
        }
    }

    public void OnClickStart()
    {
        if (_inIntro || IsInGameplay) return;
        _inIntro = true;
        ShowSkipUi(true);
        StartCoroutine(StartRoutine());
    }

    public void RequestSkip()
    {
        if (!_inIntro) return;
        _skipRequested = true;
    }

    public bool ShouldSkipCutscene() => _skipRequested;

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        _inIntro = false;
        IsInGameplay = false;
        _skipRequested = false;

        // The scene reload below rebuilds everything; drop cached statics so nothing
        // holds a reference into the destroyed scene.
        PlayerRef.Clear();
        WorldState.Reset();
        ShowSkipUi(false);
        SetPlayerActive(false);
        if (cinematicCamera != null)
        {
            cinematicCamera.gameObject.SetActive(false);
            cinematicCamera.enabled = false;
        }
        if (subtitleRoot != null) subtitleRoot.SetActive(false);
        if (menuRoot != null) menuRoot.SetActive(true);
        if (menuFade != null) menuFade.alpha = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowSkipUi(bool visible)
    {
        if (skipButton != null) skipButton.gameObject.SetActive(visible);
        if (skipHintLabel != null)
        {
            skipHintLabel.gameObject.SetActive(visible);
            skipHintLabel.text = "SPACE / ENTER / SKIP — skip dialogue";
        }
    }

    private IEnumerator StartRoutine()
    {
        ResolveRefs();

        if (menuFade != null)
        {
            float t = 0f;
            while (t < menuFadeSeconds && !_skipRequested)
            {
                t += Time.deltaTime;
                menuFade.alpha = 1f - Mathf.Clamp01(t / menuFadeSeconds);
                yield return null;
            }
            menuFade.alpha = 0f;
        }

        if (menuRoot != null) menuRoot.SetActive(false);

        if (cutscene != null && !_skipRequested)
        {
            yield return cutscene.Play(this);
        }

        _skipRequested = false;
        _inIntro = false;
        BeginGameplay();
    }

    private void BeginGameplay()
    {
        ResolveRefs();
        IsInGameplay = true;
        ShowSkipUi(false);
        if (cinematicCamera != null)
        {
            cinematicCamera.gameObject.SetActive(false);
            cinematicCamera.enabled = false;
            var cl = cinematicCamera.GetComponent<AudioListener>();
            if (cl != null) cl.enabled = false;
        }

        if (subtitleRoot != null) subtitleRoot.SetActive(false);
        SetPlayerActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var bootstrap = FindFirstObjectByType<GameSystemsBootstrap>();
        if (bootstrap == null)
        {
            var go = new GameObject("GameSystemsBootstrap");
            bootstrap = go.AddComponent<GameSystemsBootstrap>();
        }
        if (player != null) bootstrap.StartGameplaySystems(player);
    }

    public void ShowSubtitle(string speaker, string line)
    {
        if (subtitleRoot != null) subtitleRoot.SetActive(true);
        if (speakerLabel != null) speakerLabel.text = speaker;
        if (subtitleLabel != null) subtitleLabel.text = line;
        if (subtitleFade != null) subtitleFade.alpha = 1f;
    }

    public void HideSubtitle()
    {
        if (subtitleRoot != null) subtitleRoot.SetActive(false);
    }

    public void SetPlayerActive(bool active)
    {
        ResolveRefs();

        if (player != null)
        {
            // Never disable CharacterController — that often breaks Move() after re-enable.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var controller = player.GetComponent<SimplePlayerController>();
            if (controller != null) controller.enabled = active;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = active;
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = active;
        }
    }

    public void EnableCinematicCamera(bool enabled)
    {
        ResolveRefs();

        if (cinematicCamera != null)
        {
            cinematicCamera.gameObject.SetActive(enabled);
            cinematicCamera.enabled = enabled;
            var listener = cinematicCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = enabled;
        }

        if (playerCamera != null && enabled)
        {
            playerCamera.enabled = false;
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private void ResolveRefs()
    {
        if (player == null)
        {
            player = PlayerRef.Transform;
        }

        if (playerCamera == null && player != null)
        {
            playerCamera = player.GetComponentInChildren<Camera>(true);
        }

        if (cinematicCamera == null)
        {
            var cine = GameObject.Find("CinematicCamera");
            if (cine != null) cinematicCamera = cine.GetComponent<Camera>();
            if (cinematicCamera == null)
            {
                // Inactive objects are invisible to Find — search all cameras.
                var cams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in cams)
                {
                    if (c != null && c.gameObject.name == "CinematicCamera")
                    {
                        cinematicCamera = c;
                        break;
                    }
                }
            }
        }

        if (cutscene == null)
        {
            cutscene = FindFirstObjectByType<IntroCutsceneDirector>();
        }
    }
}
