using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The bug this class exists to prevent: a load that re-spawned enemies the player had
/// already cleared. Every test here is a shape of that bug.
/// </summary>
public class WorldStateTests
{
    private WorldState _state = null!;

    [SetUp]
    public void Setup() => _state = new WorldState();

    [Test]
    public void UnknownSpawnIsNotKilled()
    {
        Assert.That(_state.IsKilled("bandit.crossroads.01"), Is.False);
    }

    [Test]
    public void MarkedSpawnStaysKilled()
    {
        _state.MarkKilled("bandit.crossroads.01");
        Assert.That(_state.IsKilled("bandit.crossroads.01"), Is.True);
    }

    [Test]
    public void MarkingTwiceRecordsOneId()
    {
        _state.MarkKilled("bandit.crossroads.01");
        _state.MarkKilled("bandit.crossroads.01");
        Assert.That(_state.GetKilledIds(), Has.Count.EqualTo(1));
    }

    [TestCase("")]
    [TestCase(null)]
    public void EmptyIdsAreIgnored(string? id)
    {
        _state.MarkKilled(id);
        Assert.Multiple(() =>
        {
            Assert.That(_state.GetKilledIds(), Is.Empty);
            Assert.That(_state.IsKilled(id), Is.False);
        });
    }

    [Test]
    public void SpawnIdsAreCaseSensitive()
    {
        _state.MarkKilled("bandit.Crossroads.01");
        Assert.That(_state.IsKilled("bandit.crossroads.01"), Is.False);
    }

    [Test]
    public void SaveAndReloadPreservesKills()
    {
        _state.MarkKilled("bandit.crossroads.01");
        _state.MarkKilled("wolf.northwatch.03");

        var saved = _state.GetKilledIds();
        var reloaded = new WorldState();
        reloaded.LoadKilled(saved);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.IsKilled("bandit.crossroads.01"), Is.True);
            Assert.That(reloaded.IsKilled("wolf.northwatch.03"), Is.True);
        });
    }

    [Test]
    public void LoadReplacesRatherThanMerges()
    {
        _state.MarkKilled("bandit.crossroads.01");
        _state.LoadKilled(new[] { "wolf.northwatch.03" });

        Assert.Multiple(() =>
        {
            Assert.That(_state.IsKilled("bandit.crossroads.01"), Is.False,
                "loading a save must not keep kills from the previous session");
            Assert.That(_state.IsKilled("wolf.northwatch.03"), Is.True);
        });
    }

    [Test]
    public void LoadingNullClearsState()
    {
        _state.MarkKilled("bandit.crossroads.01");
        _state.LoadKilled(null);
        Assert.That(_state.GetKilledIds(), Is.Empty);
    }

    [Test]
    public void SavedIdsAreOrderedSoSaveFilesStayStable()
    {
        _state.MarkKilled("wolf.northwatch.03");
        _state.MarkKilled("bandit.crossroads.01");
        _state.MarkKilled("crab.shore.02");

        Assert.That(_state.GetKilledIds(), Is.EqualTo(new[]
        {
            "bandit.crossroads.01", "crab.shore.02", "wolf.northwatch.03"
        }));
    }

    [Test]
    public void ResetWipesTheWorldForANewGame()
    {
        _state.MarkKilled("bandit.crossroads.01");
        _state.Reset();
        Assert.That(_state.IsKilled("bandit.crossroads.01"), Is.False);
    }

    [Test]
    public void TwoStatesDoNotShareKills()
    {
        var other = new WorldState();
        _state.MarkKilled("bandit.crossroads.01");
        Assert.That(other.IsKilled("bandit.crossroads.01"), Is.False);
    }
}
