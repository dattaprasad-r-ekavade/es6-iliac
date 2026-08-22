using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>A watcher whose sight is set by the test rather than by geometry.</summary>
internal sealed class FakeWatcher : IWatcher
{
    public bool Sees { get; set; }
    public float LastVisibility { get; private set; } = -1f;
    public int ResetCount { get; private set; }

    public bool CanSeePlayer(float visibility)
    {
        LastVisibility = visibility;
        return Sees;
    }

    public void ResetView() => ResetCount++;
}

/// <summary>
/// The governing rule for every test here: being caught is a setback, never a dead end.
/// There must be no state detection cannot fall back out of.
/// </summary>
public class DetectionTests
{
    private SkillProgression _skills = null!;
    private Detection _detection = null!;
    private FakeWatcher _watcher = null!;

    [SetUp]
    public void Setup()
    {
        _skills = new SkillProgression();
        _detection = new Detection(_skills);
        _watcher = new FakeWatcher();
        _detection.Register(_watcher);
    }

    [Test]
    public void ThePlayerStartsUnseen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_detection.Suspicion, Is.Zero);
            Assert.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Unaware));
        });
    }

    [Test]
    public void BeingWatchedBuildsSuspicion()
    {
        _watcher.Sees = true;
        _detection.Tick(0.5f);
        Assert.That(_detection.Suspicion, Is.GreaterThan(0f));
    }

    [Test]
    public void StayingUnseenBuildsNothing()
    {
        _watcher.Sees = false;
        _detection.Tick(5f);
        Assert.That(_detection.Suspicion, Is.Zero);
    }

    [Test]
    public void EnoughExposureRaisesTheAlarm()
    {
        _watcher.Sees = true;
        _detection.Tick(10f);
        Assert.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Alerted));
    }

    [Test]
    public void BreakingLineOfSightIsAlwaysTheAnswer()
    {
        _watcher.Sees = true;
        _detection.Tick(10f);
        Assume.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Alerted));

        _watcher.Sees = false;
        _detection.Tick(10f);

        Assert.Multiple(() =>
        {
            Assert.That(_detection.Suspicion, Is.Zero);
            Assert.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Unaware),
                "no detection state may be terminal");
        });
    }

    [Test]
    public void EscapingTakesLongerThanBeingSpotted()
    {
        // Decay is deliberately slower than build, so escapes take commitment.
        _watcher.Sees = true;
        _detection.Tick(1f);
        var built = _detection.Suspicion;

        _watcher.Sees = false;
        _detection.Tick(1f);
        var shed = built - _detection.Suspicion;

        Assert.That(shed, Is.LessThan(built));
    }

    [Test]
    public void SuspicionIsBoundedAtBothEnds()
    {
        _watcher.Sees = true;
        _detection.Tick(1000f);
        Assert.That(_detection.Suspicion, Is.EqualTo(1f).Within(0.0001f));

        _watcher.Sees = false;
        _detection.Tick(1000f);
        Assert.That(_detection.Suspicion, Is.Zero);
    }

    [Test]
    public void AwarenessChangedFiresOnceForEachCrossing()
    {
        var changes = new List<AwarenessLevel>();
        _detection.AwarenessChanged += changes.Add;

        // Two seconds of exposure at 0.6/s crosses both thresholds, in order, once each.
        _watcher.Sees = true;
        for (var i = 0; i < 20; i++) _detection.Tick(0.1f);

        Assert.That(changes, Is.EqualTo(new[] { AwarenessLevel.Suspicious, AwarenessLevel.Alerted }));
    }

    [Test]
    public void AwarenessChangedDoesNotFireWithoutACrossing()
    {
        var fired = 0;
        _detection.AwarenessChanged += _ => fired++;

        _watcher.Sees = true;
        _detection.Tick(0.1f);

        Assert.Multiple(() =>
        {
            Assert.That(_detection.Suspicion, Is.GreaterThan(0f));
            Assert.That(fired, Is.Zero, "suspicion rose but the level did not change");
        });
    }

    [Test]
    public void CrouchingMakesThePlayerHarderToSee()
    {
        var standing = _detection.Visibility;
        _detection.SetCrouching(true);
        Assert.That(_detection.Visibility, Is.LessThan(standing));
    }

    [Test]
    public void StealthSkillMakesThePlayerHarderToSee()
    {
        var untrained = _detection.Visibility;

        for (var i = 0; i < 400; i++)
        {
            _skills.ReportUse(Skills.Stealth, 50f, 500f);
            _skills.EndEncounter();
        }

        Assert.That(_detection.Visibility, Is.LessThan(untrained));
    }

    [Test]
    public void EvenAMasterIsNeverInvisible()
    {
        _detection.SetCrouching(true);
        for (var i = 0; i < 400; i++)
        {
            _skills.ReportUse(Skills.Stealth, 50f, 500f);
            _skills.EndEncounter();
        }

        Assert.That(_detection.Visibility, Is.GreaterThan(0f),
            "a hidden roll the player cannot beat is indistinguishable from a bug");
    }

    [Test]
    public void WatchersAreToldTheCurrentVisibility()
    {
        _detection.SetCrouching(true);
        _detection.Tick(0.1f);
        Assert.That(_watcher.LastVisibility, Is.EqualTo(_detection.Visibility).Within(0.0001f));
    }

    [Test]
    public void OneWatcherSeeingIsEnough()
    {
        var blind = new FakeWatcher { Sees = false };
        _detection.Register(blind);
        _watcher.Sees = true;

        _detection.Tick(1f);
        Assert.That(_detection.Suspicion, Is.GreaterThan(0f));
    }

    [Test]
    public void AnUnregisteredWatcherStopsCounting()
    {
        _watcher.Sees = true;
        _detection.Unregister(_watcher);
        _detection.Tick(5f);
        Assert.That(_detection.Suspicion, Is.Zero);
    }

    [Test]
    public void RegisteringTwiceDoesNotDoubleCount()
    {
        _detection.Register(_watcher);
        _watcher.Sees = true;
        _detection.Tick(0.1f);

        var single = new Detection(_skills);
        var other = new FakeWatcher { Sees = true };
        single.Register(other);
        single.Tick(0.1f);

        Assert.That(_detection.Suspicion, Is.EqualTo(single.Suspicion).Within(0.0001f));
    }

    [Test]
    public void ClearingWipesSuspicionAndResetsWatchers()
    {
        _watcher.Sees = true;
        _detection.Tick(10f);

        _detection.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_detection.Suspicion, Is.Zero);
            Assert.That(_watcher.ResetCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void APausedGameDoesNotAccumulateSuspicion()
    {
        _watcher.Sees = true;
        _detection.Tick(0f);
        Assert.That(_detection.Suspicion, Is.Zero);
    }

    [Test]
    public void AnUnseenCrimeCostsNothing()
    {
        Assume.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Unaware));

        Assert.Multiple(() =>
        {
            Assert.That(CrimeWitness.ReportIfSeen(_detection), Is.False);
            Assert.That(_detection.Suspicion, Is.Zero, "the unwitnessed theft is the point of stealth");
        });
    }

    [Test]
    public void ACrimeCommittedInViewAddsSuspicion()
    {
        _watcher.Sees = true;
        _detection.Tick(1f);
        Assume.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Suspicious));
        var before = _detection.Suspicion;

        Assert.Multiple(() =>
        {
            Assert.That(CrimeWitness.ReportIfSeen(_detection), Is.True);
            Assert.That(_detection.Suspicion,
                Is.EqualTo(Math.Min(Detection.AlertedAt, before + CrimeWitness.SeenPenalty)).Within(0.0001f));
        });
    }

    [Test]
    public void BeingCaughtCommittingACrimeIsStillRecoverable()
    {
        _watcher.Sees = true;
        _detection.Tick(1f);
        CrimeWitness.ReportIfSeen(_detection);

        _watcher.Sees = false;
        _detection.Tick(30f);

        Assert.That(_detection.Awareness, Is.EqualTo(AwarenessLevel.Unaware));
    }
}
