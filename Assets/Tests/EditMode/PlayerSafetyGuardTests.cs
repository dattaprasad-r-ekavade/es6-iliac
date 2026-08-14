using NUnit.Framework;

/// <summary>
/// The safety guard exists to rescue a player who has drowned in the bay or walked off the
/// edge of the generated world. Both of those questions are answered in *world* terms: an
/// absolute Y compared against the bay's water level, and a raycast against generated terrain.
///
/// An authored interior has neither. Its floor sits at y≈0, which is below
/// <c>WaterLevel - 1.5</c>, and it contains no generated terrain — so from the guard's point of
/// view every interior in the game is a player who has drowned *and* fallen out of the world.
/// Walking into the docks teleported the player straight back out to the overworld spawn.
///
/// Found by playtest on 2026-08-14, not by a test, because nothing here had an opinion about
/// which scenes the guard understands.
/// </summary>
public class PlayerSafetyGuardTests
{
    [Test]
    public void TheGuardStandsDownInsideEveryAuthoredInterior()
    {
        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            Assert.IsFalse(PlayerSafetyGuard.PolicesScene(spec.Name),
                $"The guard is policing '{spec.Name}'. Its floor is at y=0, below the bay's "
                + "drown threshold, so it will teleport the player out of the interior.");
        }
    }

    [Test]
    public void TheGuardStillPolicesTheOpenWorld()
    {
        Assert.IsTrue(PlayerSafetyGuard.PolicesScene(GreyThreadDirector.RegionScene),
            "The guard stopped watching the region, where drowning and falling off the edge "
            + "are both reachable.");
        Assert.IsTrue(PlayerSafetyGuard.PolicesScene("Main"),
            "The guard stopped watching the legacy overworld.");
    }

    /// <summary>
    /// Before a transition has ever run there is no active content scene name. Defaulting to
    /// "do not police" would silently disable the guard for the whole first scene.
    /// </summary>
    [Test]
    public void AnUnknownSceneIsPoliced()
    {
        Assert.IsTrue(PlayerSafetyGuard.PolicesScene(null));
        Assert.IsTrue(PlayerSafetyGuard.PolicesScene(""));
    }
}
