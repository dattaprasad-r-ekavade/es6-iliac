using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class PickpocketingTests
{
    private SkillProgression _skills = null!;
    private Inventory _inventory = null!;
    private Detection _detection = null!;
    private FakeWatcher _watcher = null!;

    [SetUp]
    public void Setup()
    {
        _skills = new SkillProgression();
        _inventory = new Inventory();
        _detection = new Detection(_skills);
        _watcher = new FakeWatcher();
        _detection.Register(_watcher);
    }

    private static PickpocketTarget Purse(float difficulty = 0f, int items = 1)
    {
        var holdings = new ItemStack[items];
        for (var i = 0; i < items; i++)
            holdings[i] = new ItemStack { Id = $"coin_purse_{i}", Name = "Coin Purse", Kind = "loot", Count = 1 };
        return new PickpocketTarget(difficulty, holdings);
    }

    [Test]
    public void ASkilledThiefTakesTheItem()
    {
        var outcome = Pickpocketing.TryTake(Purse(), _skills, _inventory, _detection);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.Taken));
            Assert.That(outcome.Item, Is.Not.Null);
            Assert.That(_inventory.CountOf("coin_purse_0"), Is.EqualTo(1));
        });
    }

    [Test]
    public void SuccessIsDeterministicRatherThanARoll()
    {
        // A hidden roll that fails is indistinguishable from a broken mechanic, so the same
        // thief against the same pocket must always get the same answer.
        for (var i = 0; i < 20; i++)
        {
            var attempt = Pickpocketing.TryTake(
                Purse(difficulty: 15f), new SkillProgression(), new Inventory(), null);
            Assert.That(attempt.Result, Is.EqualTo(PickpocketResult.TooDifficult));
        }
    }

    [Test]
    public void TooDifficultTakesNothingAndIsRetryable()
    {
        var target = Purse(difficulty: 40f);
        var outcome = Pickpocketing.TryTake(target, _skills, _inventory, _detection);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.TooDifficult));
            Assert.That(outcome.Item, Is.Null);
            Assert.That(target.RemainingItems, Is.EqualTo(1), "a failed attempt must not consume the pocket");
            Assert.That(_inventory.Items, Is.Empty);
        });
    }

    [Test]
    public void SecuritySkillIsWhatOpensAHarderPocket()
    {
        var target = Purse(difficulty: 8f);
        Assume.That(Pickpocketing.TryTake(target, _skills, _inventory, _detection).Result,
            Is.EqualTo(PickpocketResult.TooDifficult));

        _skills.GrantRouteSkills("route.trade");

        Assert.That(Pickpocketing.TryTake(target, _skills, _inventory, _detection).Result,
            Is.EqualTo(PickpocketResult.Taken));
    }

    [Test]
    public void AnEmptyPocketReportsNothingToTake()
    {
        var outcome = Pickpocketing.TryTake(Purse(items: 0), _skills, _inventory, _detection);
        Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.NothingToTake));
    }

    [Test]
    public void ANullTargetIsNotACrash()
    {
        Assert.That(Pickpocketing.TryTake(null, _skills, _inventory, _detection).Result,
            Is.EqualTo(PickpocketResult.NothingToTake));
    }

    [Test]
    public void PocketsEmptyOneItemAtATime()
    {
        var target = Purse(items: 3);

        for (var expected = 2; expected >= 0; expected--)
        {
            Assume.That(Pickpocketing.TryTake(target, _skills, _inventory, _detection).TookSomething);
            Assert.That(target.RemainingItems, Is.EqualTo(expected));
        }

        Assert.That(Pickpocketing.TryTake(target, _skills, _inventory, _detection).Result,
            Is.EqualTo(PickpocketResult.NothingToTake));
    }

    [Test]
    public void GettingCaughtStillKeepsTheItem()
    {
        _watcher.Sees = true;
        _detection.Tick(1f);
        Assume.That(_detection.Awareness, Is.Not.EqualTo(AwarenessLevel.Unaware));

        var outcome = Pickpocketing.TryTake(Purse(), _skills, _inventory, _detection);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.Caught));
            Assert.That(outcome.TookSomething, Is.True);
            Assert.That(_inventory.CountOf("coin_purse_0"), Is.EqualTo(1),
                "being caught is a consequence, not a confiscation");
        });
    }

    [Test]
    public void GettingCaughtCostsSuspicion()
    {
        _watcher.Sees = true;
        _detection.Tick(1f);
        var before = _detection.Suspicion;

        Pickpocketing.TryTake(Purse(), _skills, _inventory, _detection);

        Assert.That(_detection.Suspicion, Is.GreaterThan(before));
    }

    [Test]
    public void GettingCaughtIsRecoverable()
    {
        _watcher.Sees = true;
        _detection.Tick(1f);
        Pickpocketing.TryTake(Purse(), _skills, _inventory, _detection);

        _watcher.Sees = false;
        _detection.Tick(30f);

        Assert.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Unaware));
    }

    [Test]
    public void AnUnwitnessedLiftCostsNothing()
    {
        _watcher.Sees = false;
        var outcome = Pickpocketing.TryTake(Purse(), _skills, _inventory, _detection);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.Taken));
            Assert.That(_detection.Suspicion, Is.Zero);
        });
    }

    [Test]
    public void ASuccessfulLiftTrainsSecurity()
    {
        _skills.GrantRouteSkills("route.trade");
        var before = _skills.LevelOf(Skills.Security);

        Pickpocketing.TryTake(Purse(difficulty: 8f), _skills, _inventory, _detection);

        Assert.That(_skills.LevelOf(Skills.Security), Is.GreaterThan(before));
    }

    [Test]
    public void AFailedAttemptTrainsNothing()
    {
        Pickpocketing.TryTake(Purse(difficulty: 40f), _skills, _inventory, _detection);
        Assert.That(_skills.LevelOf(Skills.Security), Is.Zero,
            "gains come from effect, not from attempts");
    }

    [Test]
    public void ATrivialPocketTrainsNothing()
    {
        Pickpocketing.TryTake(Purse(difficulty: 0f), _skills, _inventory, _detection);
        Assert.That(_skills.LevelOf(Skills.Security), Is.Zero);
    }

    [Test]
    public void DifficultyIsClampedToTheSkillRange()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new PickpocketTarget(9999f).Difficulty, Is.EqualTo(100f));
            Assert.That(new PickpocketTarget(-50f).Difficulty, Is.Zero);
        });
    }

    [Test]
    public void PickpocketingWorksWithoutAnyWatchersInTheScene()
    {
        var outcome = Pickpocketing.TryTake(Purse(), _skills, _inventory, detection: null);
        Assert.That(outcome.Result, Is.EqualTo(PickpocketResult.Taken));
    }
}
