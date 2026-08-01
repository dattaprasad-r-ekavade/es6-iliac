using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Gameplay behaviour that does not need the generated scene: death recovery,
/// the dialogue pause contract, and fast travel.
///
/// All three were fixed by hand during the 2026-07-26 audit and nothing has
/// protected them since.
/// </summary>
public class GameplaySmokeTests : SmokeTestFixture
{
    [Test]
    public void Death_ReturnsThePlayerToSpawnAndRestoresHealth()
    {
        var player = SpawnPlayer();
        Track(new GameObject("SafetyGuard")).AddComponent<PlayerSafetyGuard>();
        var stats = PlayerStats.Instance;

        player.transform.position = new Vector3(500f, 12f, -400f);
        stats.Health = 5f;

        stats.Damage(999f);

        Assert.AreEqual(
            stats.MaxHealth, stats.Health, 0.01f,
            "Death did not restore health — the player would respawn already dead.");
        // Compared against the generator's own answer rather than a hardcoded point,
        // so moving the spawn does not break the test.
        var spawn = KessilWorldGenerator.GetPlayerSpawn();
        Assert.Less(
            Vector3.Distance(player.transform.position, spawn), 1.5f,
            "Death did not return the player to the spawn point.");
    }

    [Test]
    public void Dialogue_PausesGameplayAndClaimsTheCursor()
    {
        var player = SpawnPlayer();
        var hud = Track(new GameObject("HUD_Test")).AddComponent<GameHud>();
        hud.Build(player.transform);

        Assert.IsFalse(hud.AnyMenuOpen, "A menu was already open before dialogue started.");

        hud.ShowDialogue("Test Speaker", "A line of placeholder dialogue.");

        Assert.AreEqual(
            0f, Time.timeScale,
            "Dialogue did not pause gameplay. Before the audit fix, the world kept " +
            "simulating while the player read.");
        Assert.IsTrue(hud.AnyMenuOpen, "Dialogue did not register as an open menu.");
        Assert.IsTrue(Cursor.visible, "Dialogue did not release the cursor for the player.");
    }

    /// <summary>
    /// Combat blocking matters: fast travelling out of a fight was an escape hatch
    /// that also left enemy state inconsistent.
    /// </summary>
    [Test]
    public void FastTravel_IsRefusedWhenUndiscoveredOrInCombat()
    {
        var player = SpawnPlayer();
        var combat = player.AddComponent<PlayerCombat>();
        var travel = Track(new GameObject("Travel_Test")).AddComponent<DiscoveryTravelSystem>();
        travel.Configure(player.transform);
        travel.BootstrapDefaultLocations();

        var site = travel.Locations[0];

        Assert.IsFalse(
            travel.CanFastTravel(site.Id),
            "An undiscovered location offered fast travel.");

        travel.Discover(site.Id, silent: true);
        Assert.IsTrue(travel.CanFastTravel(site.Id), "A discovered location refused fast travel.");

        combat.EnterCombat();
        Assert.IsFalse(
            travel.CanFastTravel(site.Id),
            "Fast travel was allowed during combat.");
    }

    [UnityTest]
    public IEnumerator FastTravel_MovesThePlayerToTheDiscoveredSite()
    {
        var player = SpawnPlayer();
        var travel = Track(new GameObject("Travel_Test")).AddComponent<DiscoveryTravelSystem>();
        travel.Configure(player.transform);
        travel.BootstrapDefaultLocations();

        var site = travel.Locations[0];
        travel.Discover(site.Id, silent: true);
        player.transform.position = new Vector3(-900f, 20f, 900f);

        travel.FastTravel(site.Id);

        // The routine fades out, moves, then fades back in on realtime waits.
        float deadline = Time.realtimeSinceStartup + 5f;
        while (Vector3.Distance(player.transform.position, site.TravelPosition) > 2f
               && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.Less(
            Vector3.Distance(player.transform.position, site.TravelPosition), 2f,
            "Fast travel did not deliver the player to the site's travel position.");
    }

    [Test]
    public void DiscoveredLocations_SurviveASaveRoundTrip()
    {
        var player = SpawnPlayer();
        var save = SpawnSaveService();
        var travel = Track(new GameObject("Travel_Test")).AddComponent<DiscoveryTravelSystem>();
        travel.Configure(player.transform);
        travel.BootstrapDefaultLocations();

        var site = travel.Locations[0];
        travel.Discover(site.Id, silent: true);
        save.Save();

        travel.LoadDiscovered(new string[0]);
        Assert.IsFalse(travel.CanFastTravel(site.Id), "Test setup failed to clear discovery.");

        save.Load();

        Assert.IsTrue(
            travel.CanFastTravel(site.Id),
            "A discovered location was lost across a save round trip.");
    }
}
