using NUnit.Framework;
using UnityEngine;

/// <summary>Locks the W-02 ownership contract for time, cursor and gameplay input.</summary>
public sealed class GameStateSmokeTests : SmokeTestFixture
{
    [Test]
    public void StatePolicies_ControlPauseCursorAndGameplayInput()
    {
        var state = SpawnStateService();

        state.SetState(GameState.Gameplay);
        Assert.IsTrue(state.GameplayInputAllowed);
        Assert.IsFalse(state.IsPaused);
        Assert.AreEqual(CursorLockMode.Locked, state.DesiredCursorLockMode);
        Assert.IsFalse(state.DesiredCursorVisible);

        state.SetState(GameState.Dialogue);
        Assert.IsFalse(state.GameplayInputAllowed);
        Assert.IsTrue(state.IsPaused);
        Assert.AreEqual(CursorLockMode.None, state.DesiredCursorLockMode);
        Assert.IsTrue(state.DesiredCursorVisible);

        state.SetState(GameState.Cinematic);
        Assert.IsFalse(state.GameplayInputAllowed);
        Assert.IsFalse(state.IsPaused);

        state.SetState(GameState.Menu, pauseWorld: true);
        Assert.IsTrue(state.IsPaused);

        state.SetState(GameState.Menu);
        Assert.IsFalse(state.IsPaused, "The title menu should not freeze its intro coroutine.");
    }

    [Test]
    public void TemporaryLoading_RestoresExactPreviousPolicy()
    {
        var state = SpawnStateService();
        state.SetState(GameState.Menu, pauseWorld: true);

        state.PushState(GameState.Loading);
        Assert.AreEqual(GameState.Loading, state.CurrentState);
        Assert.IsFalse(state.IsPaused, "Loading must advance even when entered from a paused menu.");

        Assert.IsTrue(state.PopState(GameState.Loading));
        Assert.AreEqual(GameState.Menu, state.CurrentState);
        Assert.IsTrue(state.IsPaused, "Loading did not restore the prior menu's pause policy.");
    }

    [Test]
    public void MismatchedPop_CannotReleaseAnotherStateOwner()
    {
        var state = SpawnStateService();
        state.SetState(GameState.Gameplay);
        state.PushState(GameState.Dialogue);

        Assert.IsFalse(state.PopState(GameState.Loading));
        Assert.AreEqual(GameState.Dialogue, state.CurrentState);
        Assert.IsTrue(state.IsPaused);
    }

    private GameStateService SpawnStateService()
    {
        Assert.IsNull(GameStateService.Instance, "A GameStateService leaked from another test.");
        return Track(new GameObject("GameStateService_Test")).AddComponent<GameStateService>();
    }
}
