using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns additive content-scene transitions while Bootstrap remains loaded.
/// Loading is transactional: a bad scene or spawn leaves the previous content scene active.
/// </summary>
public sealed class SceneTransitionService : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fadeSeconds = 0.2f;
    [SerializeField] private string persistentSceneName = "Bootstrap";
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private Transform playerOverride;

    private Scene _activeContentScene;

    public static SceneTransitionService Instance { get; private set; }
    public bool IsTransitioning { get; private set; }
    public string LastError { get; private set; }
    public string ActiveContentSceneName =>
        _activeContentScene.IsValid() && _activeContentScene.isLoaded ? _activeContentScene.name : string.Empty;
    public string ActiveSpawnId { get; private set; }

    public event Action<string> TransitionStarted;
    public event Action<string, string> TransitionCompleted;
    public event Action<string, string> TransitionFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureFadeCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Used by deterministic PlayMode tests and later by prefab wiring.</summary>
    public void Configure(
        float duration,
        Transform player = null,
        CanvasGroup overlay = null,
        string persistentScene = null)
    {
        fadeSeconds = Mathf.Max(0f, duration);
        playerOverride = player;
        if (overlay != null) fadeCanvas = overlay;
        if (!string.IsNullOrWhiteSpace(persistentScene)) persistentSceneName = persistentScene;
    }

    public IEnumerator TransitionTo(string sceneName, string spawnId = null, bool unloadPrevious = true)
    {
        if (IsTransitioning) yield break;

        IsTransitioning = true;
        LastError = null;
        TransitionStarted?.Invoke(sceneName);
        var gameState = GameStateService.Ensure(gameObject);
        gameState.PushState(GameState.Loading);

        yield return FadeTo(1f);

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Fail(sceneName, $"Scene '{sceneName}' is not available in build settings.");
            yield return FadeTo(0f);
            EndTransition(gameState);
            yield break;
        }

        var previous = ResolvePreviousContentScene();
        var destination = FindLoadedScene(sceneName);
        bool loadedForThisTransition = false;

        if (!destination.IsValid() || !destination.isLoaded)
        {
            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Fail(sceneName, $"Unity could not start loading scene '{sceneName}'.");
                yield return FadeTo(0f);
                EndTransition(gameState);
                yield break;
            }

            while (!load.isDone) yield return null;
            destination = FindLoadedScene(sceneName);
            loadedForThisTransition = true;
        }

        var context = SceneContext.FindInScene(destination);
        if (context == null)
        {
            if (loadedForThisTransition && destination.IsValid() && destination.isLoaded)
                yield return SceneManager.UnloadSceneAsync(destination);

            Fail(sceneName, $"Scene '{sceneName}' has no SceneContext.");
            RestorePrevious(previous);
            yield return FadeTo(0f);
            EndTransition(gameState);
            yield break;
        }

        SceneSpawnPoint spawn = null;
        if (!string.IsNullOrWhiteSpace(spawnId) && !context.TryGetSpawn(spawnId, out spawn))
        {
            Debug.LogWarning(
                $"[SceneTransition] Spawn '{spawnId}' was not found in '{sceneName}'. " +
                $"Using default '{context.DefaultSpawnId}'.");
        }

        if (spawn == null && !context.TryGetDefaultSpawn(out spawn))
        {
            if (loadedForThisTransition && destination.IsValid() && destination.isLoaded)
                yield return SceneManager.UnloadSceneAsync(destination);

            Fail(sceneName,
                $"Scene '{sceneName}' has neither requested spawn '{spawnId}' nor default " +
                $"spawn '{context.DefaultSpawnId}'.");
            RestorePrevious(previous);
            yield return FadeTo(0f);
            EndTransition(gameState);
            yield break;
        }

        PlacePlayer(spawn.transform);
        ActiveSpawnId = spawn.SpawnId;
        SceneManager.SetActiveScene(destination);
        _activeContentScene = destination;

        if (unloadPrevious && previous.IsValid() && previous.isLoaded
            && previous.handle != destination.handle && !IsPersistentScene(previous))
        {
            PreservePlayerAcrossUnload(previous);
            var unload = SceneManager.UnloadSceneAsync(previous);
            if (unload != null)
                while (!unload.isDone) yield return null;
        }

        TransitionCompleted?.Invoke(context.SceneId, spawn.SpawnId);
        yield return FadeTo(0f);
        EndTransition(gameState);
    }

    private void EndTransition(GameStateService gameState)
    {
        IsTransitioning = false;
        if (gameState != null) gameState.PopState(GameState.Loading);
    }

    private Scene ResolvePreviousContentScene()
    {
        if (_activeContentScene.IsValid() && _activeContentScene.isLoaded)
            return _activeContentScene;

        var active = SceneManager.GetActiveScene();
        return IsPersistentScene(active) ? default : active;
    }

    private void RestorePrevious(Scene previous)
    {
        if (previous.IsValid() && previous.isLoaded)
        {
            SceneManager.SetActiveScene(previous);
            _activeContentScene = previous;
        }
    }

    private bool IsPersistentScene(Scene scene)
    {
        return scene.IsValid()
               && string.Equals(scene.name, persistentSceneName, StringComparison.Ordinal);
    }

    private void Fail(string sceneName, string message)
    {
        LastError = message;
        Debug.LogWarning($"[SceneTransition] {message}");
        TransitionFailed?.Invoke(sceneName, message);
    }

    private Transform ResolvePlayer()
    {
        if (playerOverride != null) return playerOverride;
        return PlayerRef.Transform;
    }

    private void PlacePlayer(Transform spawn)
    {
        var player = ResolvePlayer();
        if (player == null || spawn == null) return;

        var controller = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;

        player.SetPositionAndRotation(spawn.position, spawn.rotation);

        if (controllerWasEnabled) controller.enabled = true;
        PlayerRef.Set(player);
    }

    /// <summary>
    /// Runtime-generated players are initially children of the content scene that built
    /// them. Moving from the first scene used to unload that parent and silently destroy
    /// the player, leaving the additive transition service with a stale PlayerRef. Keep
    /// the root player in Bootstrap before unloading its former content scene.
    /// </summary>
    private void PreservePlayerAcrossUnload(Scene previous)
    {
        var player = ResolvePlayer();
        if (player == null || player.gameObject.scene.handle != previous.handle) return;

        var persistent = SceneManager.GetSceneByName(persistentSceneName);
        if (!persistent.IsValid() || !persistent.isLoaded) return;

        player.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(player.gameObject, persistent);
        PlayerRef.Set(player);
    }

    private static Scene FindLoadedScene(string sceneNameOrPath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            if (string.Equals(scene.name, sceneNameOrPath, StringComparison.Ordinal)
                || string.Equals(scene.path, sceneNameOrPath, StringComparison.OrdinalIgnoreCase))
                return scene;
        }

        return default;
    }

    private IEnumerator FadeTo(float target)
    {
        if (fadeCanvas == null || fadeSeconds <= 0f)
        {
            if (fadeCanvas != null)
            {
                fadeCanvas.alpha = target;
                fadeCanvas.blocksRaycasts = target > 0.001f;
            }
            yield break;
        }

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.blocksRaycasts = true;
        float start = fadeCanvas.alpha;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeSeconds));
            yield return null;
        }

        fadeCanvas.alpha = target;
        fadeCanvas.blocksRaycasts = target > 0.001f;
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvas != null) return;

        var overlay = new GameObject(
            "TransitionOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup),
            typeof(Image));
        overlay.transform.SetParent(transform, false);

        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeCanvas = overlay.GetComponent<CanvasGroup>();
        fadeCanvas.alpha = 0f;
        fadeCanvas.blocksRaycasts = false;
    }
}
