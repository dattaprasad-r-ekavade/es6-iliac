using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Sailing.
///
/// The plan names this and stealth as the two largest unknowns in the slice, and it is the
/// Chapter 01 tutorial mechanic with the longest payoff — B400 teaches it and the region
/// ferries then run on it for the rest of the game.
///
/// VS4's gate is that a mechanic survives save/load and cannot strand the player. These
/// tests are about that property, not about whether sailing feels good.
/// </summary>
public class SailingSmokeTests : SmokeTestFixture
{
    private SailingController SpawnBoat(Vector3 position)
    {
        var go = Track(new GameObject("Boat_Test"));
        go.transform.position = position;
        return go.AddComponent<SailingController>();
    }

    [Test]
    public void Boarding_TakesControlOfThePlayer()
    {
        var boat = SpawnBoat(Vector3.zero);
        var player = SpawnPlayer();
        var controller = player.AddComponent<SimplePlayerController>();

        Assert.IsTrue(boat.Board(player.transform), "Boarding failed.");
        Assert.IsTrue(boat.IsBoarded);
        Assert.IsFalse(controller.enabled, "The player kept walking control while aboard.");
        Assert.AreEqual(boat.transform, player.transform.parent, "The boat does not carry the player.");
    }

    [Test]
    public void BoardingTwice_IsRefused()
    {
        var boat = SpawnBoat(Vector3.zero);
        var player = SpawnPlayer();

        Assert.IsTrue(boat.Board(player.transform));
        Assert.IsFalse(boat.Board(player.transform), "A second rider boarded an occupied boat.");
    }

    [Test]
    public void Throttle_MovesTheBoatForward()
    {
        var boat = SpawnBoat(Vector3.zero);
        var player = SpawnPlayer();
        boat.Board(player.transform);
        var start = boat.transform.position;

        for (int i = 0; i < 30; i++) boat.Steer(1f, 0f, 0.1f);

        Assert.Greater(
            Vector3.Distance(boat.transform.position, start), 1f,
            "Full throttle did not move the boat.");
        Assert.Greater(boat.Speed, 0f);
    }

    /// <summary>
    /// A boat with no way on cannot turn. Without this it spins on the spot like a turret,
    /// which reads as a bug even to players who have never sailed.
    /// </summary>
    [Test]
    public void AStationaryBoat_CannotTurnOnTheSpot()
    {
        var boat = SpawnBoat(Vector3.zero);
        var player = SpawnPlayer();
        boat.Board(player.transform);
        float before = boat.transform.eulerAngles.y;

        for (int i = 0; i < 20; i++) boat.Steer(0f, 1f, 0.1f);

        Assert.AreEqual(before, boat.transform.eulerAngles.y, 0.5f,
            "A boat with no speed turned on the spot.");
    }

    [Test]
    public void Steering_IsIgnoredWhenNobodyIsAboard()
    {
        var boat = SpawnBoat(Vector3.zero);
        var start = boat.transform.position;

        for (int i = 0; i < 20; i++) boat.Steer(1f, 1f, 0.1f);

        Assert.AreEqual(start, boat.transform.position, "An empty boat sailed itself.");
    }

    [Test]
    public void Disembarking_ReturnsWalkingControl_AndUnparentsThePlayer()
    {
        var boat = SpawnBoat(Vector3.zero);
        var player = SpawnPlayer();
        var controller = player.AddComponent<SimplePlayerController>();
        boat.Board(player.transform);

        Assert.IsTrue(boat.Disembark(), "Disembarking failed.");
        Assert.IsFalse(boat.IsBoarded);
        Assert.IsNull(player.transform.parent, "The player was still parented to the boat.");
        Assert.IsTrue(controller.enabled, "The player did not get walking control back.");
    }

    /// <summary>
    /// The stranding case. A boat driven far out with no shore in reach must still return the
    /// player to solid ground rather than dropping them in open water.
    /// </summary>
    [Test]
    public void DisembarkingWithNoShoreInReach_ReturnsToTheMooring()
    {
        var mooring = new Vector3(10f, 0f, 10f);
        var boat = SpawnBoat(mooring);
        boat.SetMooring(mooring, Quaternion.identity);
        var player = SpawnPlayer();
        boat.Board(player.transform);

        // Somewhere with no terrain under it at all.
        boat.transform.position = new Vector3(0f, 0f, 90000f);

        Assert.IsTrue(boat.Disembark(), "Disembarking in open water did nothing at all.");
        Assert.IsFalse(boat.IsBoarded, "The player was left aboard with no way off.");
        Assert.IsNull(player.transform.parent);
        Assert.Less(
            Vector3.Distance(boat.transform.position, mooring), 0.01f,
            "The boat did not return to its mooring.");
    }

    [Test]
    public void ResetToMooring_RecoversABoatAndItsRider()
    {
        var mooring = new Vector3(5f, 0f, -5f);
        var boat = SpawnBoat(mooring);
        boat.SetMooring(mooring, Quaternion.identity);
        var player = SpawnPlayer();
        boat.Board(player.transform);

        for (int i = 0; i < 50; i++) boat.Steer(1f, 0.2f, 0.1f);
        boat.ResetToMooring();

        Assert.Less(Vector3.Distance(boat.transform.position, mooring), 0.01f);
        Assert.AreEqual(0f, boat.Speed, 0.001f, "Speed was not shed on reset.");
        Assert.IsFalse(boat.IsBoarded);
        Assert.IsNull(player.transform.parent, "Reset left the player parented to the boat.");
    }

    [Test]
    public void ResettingAnEmptyBoat_IsHarmless()
    {
        var mooring = new Vector3(3f, 0f, 3f);
        var boat = SpawnBoat(mooring);
        boat.SetMooring(mooring, Quaternion.identity);
        boat.transform.position = new Vector3(200f, 0f, 200f);

        Assert.DoesNotThrow(() => boat.ResetToMooring());
        Assert.Less(Vector3.Distance(boat.transform.position, mooring), 0.01f);
    }
}
