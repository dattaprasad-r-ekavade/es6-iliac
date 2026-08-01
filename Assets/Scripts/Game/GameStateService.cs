using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The mutually exclusive top-level modes that own input, cursor and time.</summary>
public enum GameState
{
    Menu,
    Cinematic,
    Gameplay,
    Dialogue,
    Loading,
    Death
}

/// <summary>
/// Single authority for the game's top-level mode, gameplay-input permission, cursor and
/// time scale. Temporary modes such as loading use Push/Pop so failure paths cannot leave
/// the application paused or input-locked.
/// </summary>
public sealed class GameStateService : MonoBehaviour
{
    private readonly struct StateFrame
    {
        public StateFrame(GameState state, bool pauseWorld)
        {
            State = state;
            PauseWorld = pauseWorld;
        }

        public GameState State { get; }
        public bool PauseWorld { get; }
    }

    [SerializeField] private GameState initialState = GameState.Menu;
    [SerializeField] private bool initialMenuPausesWorld;

    private readonly Stack<StateFrame> _history = new();
    private bool _pauseWorld;

    public static GameStateService Instance { get; private set; }
    public GameState CurrentState { get; private set; }
    public bool GameplayInputAllowed => CurrentState == GameState.Gameplay;
    public bool IsPaused => Mathf.Approximately(Time.timeScale, 0f);
    public CursorLockMode DesiredCursorLockMode { get; private set; }
    public bool DesiredCursorVisible { get; private set; }

    public event Action<GameState, GameState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CurrentState = initialState;
        _pauseWorld = initialMenuPausesWorld;
        ApplyPolicy();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        _history.Clear();
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Compatibility path for direct-Main editor/tests. Packaged play owns this service
    /// from Bootstrap; W-04 will remove this fallback when runtime systems become prefabs.
    /// </summary>
    public static GameStateService Ensure(GameObject owner = null)
    {
        if (Instance != null) return Instance;
        var existing = FindAnyObjectByType<GameStateService>();
        if (existing != null) return existing;
        if (owner != null) return owner.AddComponent<GameStateService>();
        return new GameObject("GameStateService_Legacy").AddComponent<GameStateService>();
    }

    /// <summary>Replace the current mode and discard temporary-state history.</summary>
    public void SetState(GameState state, bool pauseWorld = false)
    {
        _history.Clear();
        ChangeState(state, pauseWorld);
    }

    /// <summary>Enter a temporary mode that can restore the exact prior policy.</summary>
    public void PushState(GameState state, bool pauseWorld = false)
    {
        _history.Push(new StateFrame(CurrentState, _pauseWorld));
        ChangeState(state, pauseWorld);
    }

    /// <summary>
    /// Restore the mode beneath <paramref name="expectedCurrent"/>. Returns false rather
    /// than popping an unrelated owner, which protects against delayed callbacks.
    /// </summary>
    public bool PopState(GameState expectedCurrent)
    {
        if (CurrentState != expectedCurrent || _history.Count == 0) return false;
        var previous = _history.Pop();
        ChangeState(previous.State, previous.PauseWorld);
        return true;
    }

    private void ChangeState(GameState state, bool pauseWorld)
    {
        var previous = CurrentState;
        CurrentState = state;
        _pauseWorld = pauseWorld;
        ApplyPolicy();
        if (previous != state) StateChanged?.Invoke(previous, state);
    }

    private void ApplyPolicy()
    {
        bool paused = CurrentState == GameState.Dialogue
                      || CurrentState == GameState.Death
                      || (CurrentState == GameState.Menu && _pauseWorld);
        Time.timeScale = paused ? 0f : 1f;

        bool lockCursor = CurrentState == GameState.Gameplay;
        DesiredCursorLockMode = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        DesiredCursorVisible = !lockCursor;
        Cursor.lockState = DesiredCursorLockMode;
        Cursor.visible = DesiredCursorVisible;
    }
}
