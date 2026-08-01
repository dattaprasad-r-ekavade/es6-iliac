using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The boot path, driven through the real generated scene: New Game, Continue and
/// Return to Menu.
///
/// These are the most valuable tests in the suite and the most expensive — they load
/// <c>Main.unity</c> and run the actual title flow, so they catch "the game does not
/// start" rather than "a function returns the wrong number". Everything else here can
/// pass while the game is unlaunchable.
///
/// They are also the tests VS1 will have to rewrite first, since splitting the
/// generated scene into Bootstrap plus additive scenes changes exactly this path.
/// </summary>
public class GameFlowSmokeTests : SmokeTestFixture
{
    private const string GameplayScene = "Main";
    private const float BootTimeoutSeconds = 30f;

    /// <summary>
    /// Replace the generated scene with an empty one so later fixtures do not inherit
    /// its 2,000-odd objects and live singletons.
    /// </summary>
    [UnityTearDown]
    public IEnumerator UnloadGameplayScene()
    {
        var cleanup = SceneManager.CreateScene("SmokeCleanup_" + Time.frameCount);
        SceneManager.SetActiveScene(cleanup);

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.name == GameplayScene)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        PlayerRef.Clear();
        WorldState.Reset();
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator NewGame_ReachesGameplayWithSystemsOnline()
    {
        yield return LoadGameplayScene();

        var flow = GameFlowController.Instance;
        Assert.IsNotNull(flow, "The generated scene has no GameFlowController.");
        Assert.IsFalse(flow.IsInGameplay, "The scene started already in gameplay, bypassing the title.");

        yield return StartAndSkipIntro(flow);

        Assert.IsTrue(flow.IsInGameplay, $"New Game did not reach gameplay within {BootTimeoutSeconds}s.");
        Assert.IsNotNull(PlayerRef.Transform, "Gameplay started without a registered player.");
        Assert.IsNotNull(PlayerStats.Instance, "Player stats were never brought online.");
        Assert.IsNotNull(PlayerInventory.Instance, "Player inventory was never brought online.");
        Assert.IsNotNull(SaveLoadService.Instance, "Save service was never brought online.");
        Assert.AreEqual(1f, Time.timeScale, "Gameplay began with a non-running time scale.");
    }

    /// <summary>
    /// Continue is offered on file existence alone, so the path that actually applies
    /// a save on boot needs proving end to end rather than at the service level.
    /// </summary>
    [UnityTest]
    public IEnumerator Continue_AppliesTheSavedStateOnBoot()
    {
        WriteSaveWithGold(4321);

        yield return LoadGameplayScene();

        var flow = GameFlowController.Instance;
        Assert.IsNotNull(flow, "The generated scene has no GameFlowController.");

        flow.OnClickContinue();
        yield return WaitForGameplay(flow);

        Assert.IsTrue(flow.IsInGameplay, $"Continue did not reach gameplay within {BootTimeoutSeconds}s.");
        Assert.IsNotNull(PlayerStats.Instance, "Continue reached gameplay without player stats.");
        Assert.AreEqual(
            4321, PlayerStats.Instance.Gold,
            "Continue reached gameplay but never applied the save.");
    }

    [UnityTest]
    public IEnumerator ReturnToMainMenu_LeavesGameplayAndRestoresTimeScale()
    {
        yield return LoadGameplayScene();

        var flow = GameFlowController.Instance;
        yield return StartAndSkipIntro(flow);
        Assert.IsTrue(flow.IsInGameplay, "Could not reach gameplay to test the return path.");

        // Menus pause; returning to the title must not leave the game frozen.
        Time.timeScale = 0f;
        flow.ReturnToMainMenu();

        // ReturnToMainMenu reloads the scene, so the controller is rebuilt.
        float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
        while ((GameFlowController.Instance == null || GameFlowController.Instance == flow)
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        var reloaded = GameFlowController.Instance;
        Assert.IsNotNull(reloaded, "The scene did not come back after returning to the menu.");
        Assert.IsFalse(reloaded.IsInGameplay, "The reloaded scene came back already in gameplay.");
        Assert.AreEqual(1f, Time.timeScale, "Returning to the menu left the game paused.");
    }

    // --- helpers -------------------------------------------------------------

    private static IEnumerator LoadGameplayScene()
    {
        yield return SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Single);
        // One frame for Awake/Start across the loaded scene.
        yield return null;
    }

    private static IEnumerator StartAndSkipIntro(GameFlowController flow)
    {
        flow.OnClickStart();
        flow.RequestSkip();
        yield return WaitForGameplay(flow);
    }

    private static IEnumerator WaitForGameplay(GameFlowController flow)
    {
        float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
        while (!flow.IsInGameplay && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;
    }

    private static void WriteSaveWithGold(int gold)
    {
        var data = new SaveData
        {
            Version = SaveLoadService.CurrentVersion,
            Gold = gold,
            Level = 1,
            Xp = 0,
            Health = 100f, MaxHealth = 100f,
            Mana = 80f, MaxMana = 80f,
            Stamina = 100f, MaxStamina = 100f,
            TimeOfDay01 = 0.4f
        };

        var spawn = KessilWorldGenerator.GetPlayerSpawn();
        data.Px = spawn.x;
        data.Py = spawn.y;
        data.Pz = spawn.z;

        File.WriteAllText(SaveLoadService.SaveFilePath, JsonUtility.ToJson(data, true));
    }
}
