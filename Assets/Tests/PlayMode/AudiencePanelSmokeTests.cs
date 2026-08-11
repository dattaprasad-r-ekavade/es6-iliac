using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The King's audience panel — B110 through B130.
///
/// This is the one scene where the player chooses their route, and until now it was the one
/// scene nothing covered: the VS2 gate runs non-interactively, so it skipped the panel
/// entirely and drove the route directly.
///
/// These drive <see cref="GreyThreadAssignmentPanel.Submit"/>, which is exactly what the
/// panel's own buttons call. An end-to-end version that walked the whole chapter to reach the
/// audience was tried first and was too fragile to keep — it depended on six chained scene
/// transitions. A reliable test of the real logic beats a flaky test of more of it.
/// </summary>
public class AudiencePanelSmokeTests : SmokeTestFixture
{
    private GreyThreadAssignmentPanel SpawnPanel()
    {
        var go = Track(new GameObject("AssignmentPanel_Test"));
        return go.AddComponent<GreyThreadAssignmentPanel>();
    }

    [Test]
    public void AnsweringTheKing_ReportsTheNameAndRouteChosen()
    {
        var panel = SpawnPanel();
        string reportedName = null, reportedRoute = null;
        panel.Submitted += (name, route) => { reportedName = name; reportedRoute = route; };

        panel.Submit("Terrin's Castaway", "route.trade");

        Assert.AreEqual("Terrin's Castaway", reportedName, "The name the player gave was not reported.");
        Assert.AreEqual("route.trade", reportedRoute, "The route the player chose was not reported.");
    }

    /// <summary>
    /// B120 records what the guards wrote down. A survivor who gives no name still gets
    /// recorded as something, rather than the audience stalling on an empty field.
    /// </summary>
    [Test]
    public void GivingNoName_FallsBackRatherThanStalling()
    {
        var panel = SpawnPanel();
        string reportedName = null;
        panel.Submitted += (name, _) => reportedName = name;

        panel.Submit("   ", "route.warrior");

        Assert.IsNotEmpty(reportedName, "An unnamed survivor produced an empty profile name.");
    }

    /// <summary>
    /// B130's acceptance test: refusal and any invalid selection both resolve to
    /// <c>route.refuse</c>. A malformed route must never reach the story state.
    /// </summary>
    [Test]
    public void AnInvalidRoute_ResolvesToRefuse()
    {
        var panel = SpawnPanel();
        string reportedRoute = null;
        panel.Submitted += (_, route) => reportedRoute = route;

        panel.Submit("Someone", "route.that_does_not_exist");

        Assert.AreEqual("route.refuse", reportedRoute,
            "An unrecognised route was passed through instead of resolving to refusal.");
    }

    [Test]
    public void EverySupportedRoute_SurvivesTheAudienceUnchanged()
    {
        var panel = SpawnPanel();
        string reportedRoute = null;
        panel.Submitted += (_, route) => reportedRoute = route;

        foreach (var route in new[] { "route.warrior", "route.mage", "route.trade", "route.refuse" })
        {
            panel.Submit("Someone", route);
            Assert.AreEqual(route, reportedRoute, $"{route} was altered by the audience.");
        }
    }

    /// <summary>
    /// The director exposes the live panel so the audience can be answered without a click.
    /// Without this the interactive path stays permanently untestable.
    /// </summary>
    [Test]
    public void TheDirectorExposesItsPanelAndWaitingState()
    {
        var systems = Track(new GameObject("GameSystems_Audience"));
        systems.AddComponent<StoryDirector>();
        var director = systems.AddComponent<GreyThreadDirector>();

        Assert.IsFalse(director.AwaitingAssignment,
            "The director claims to be waiting on an audience it has not opened.");
        Assert.IsNull(director.AssignmentPanel,
            "A panel exists before the audience has begun.");
    }
}
