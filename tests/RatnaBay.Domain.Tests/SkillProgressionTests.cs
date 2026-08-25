using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// These tests are the five anti-grind rules written as assertions. If a rebalance breaks
/// one, the design has changed and someone should have decided that on purpose.
/// </summary>
public class SkillProgressionTests
{
    /// <summary>Threat high enough to be worth training against at low skill.</summary>
    private const float RealThreat = 60f;

    private SkillProgression _skills = null!;

    [SetUp]
    public void Setup() => _skills = new SkillProgression();

    [Test]
    public void EverySkillStartsAtZero()
    {
        foreach (var id in Skills.All)
            Assert.That(_skills.LevelOf(id), Is.Zero);
    }

    [Test]
    public void AnUnknownSkillReadsAsZeroRatherThanThrowing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_skills.LevelOf("skill.nonexistent"), Is.Zero);
            Assert.That(_skills.LevelOf(null), Is.Zero);
        });
    }

    [Test]
    public void ALandedUseRaisesTheSkill()
    {
        _skills.ReportUse(Skills.Blade, magnitude: 20f, threat: RealThreat);
        Assert.That(_skills.LevelOf(Skills.Blade), Is.GreaterThan(0f));
    }

    [Test]
    public void Rule1_AMissTrainsNothing()
    {
        // Magnitude is damage actually dealt. Swinging at air reports zero.
        _skills.ReportUse(Skills.Blade, magnitude: 0f, threat: RealThreat);
        Assert.That(_skills.LevelOf(Skills.Blade), Is.Zero);
    }

    [Test]
    public void Rule2_ATrivialTargetTrainsNothing()
    {
        _skills.ReportUse(Skills.Blade, magnitude: 20f, threat: 0f);
        Assert.That(_skills.LevelOf(Skills.Blade), Is.Zero);
    }

    [Test]
    public void Rule2_TheSameTargetTrainsLessOnceYouAreGood()
    {
        var novice = new SkillProgression();
        novice.ReportUse(Skills.Blade, 20f, threat: 30f);
        var noviceGain = novice.LevelOf(Skills.Blade);

        var veteran = new SkillProgression();
        for (var i = 0; i < 40; i++)
        {
            veteran.ReportUse(Skills.Blade, 20f, threat: 300f);
            veteran.EndEncounter();
        }

        var before = veteran.LevelOf(Skills.Blade);
        veteran.ReportUse(Skills.Blade, 20f, threat: 30f);

        Assert.That(veteran.LevelOf(Skills.Blade) - before, Is.LessThan(noviceGain),
            "the fortieth identical bandit must be worth less than the first");
    }

    [Test]
    public void Rule3_OneEncounterCannotBeFarmedForever()
    {
        for (var i = 0; i < 500; i++)
            _skills.ReportUse(Skills.Blade, 20f, RealThreat);

        Assert.That(_skills.LevelOf(Skills.Blade), Is.LessThanOrEqualTo(6f),
            "the per-encounter cap is what stops one enemy being a training dummy");
    }

    [Test]
    public void Rule3_TheCapLiftsWhenTheFightEnds()
    {
        for (var i = 0; i < 500; i++) _skills.ReportUse(Skills.Blade, 20f, RealThreat);
        var capped = _skills.LevelOf(Skills.Blade);

        _skills.EndEncounter();
        _skills.ReportUse(Skills.Blade, 20f, RealThreat);

        Assert.That(_skills.LevelOf(Skills.Blade), Is.GreaterThan(capped));
    }

    [Test]
    public void SkillsNeverExceedTheCeiling()
    {
        TrainToMastery(Skills.Blade);
        Assert.That(_skills.LevelOf(Skills.Blade), Is.LessThanOrEqualTo(SkillProgression.MaxSkill));
    }

    [Test]
    public void TrainingOneSkillDoesNotTrainAnother()
    {
        _skills.ReportUse(Skills.Blade, 20f, RealThreat);
        Assert.That(_skills.LevelOf(Skills.Destruction), Is.Zero);
    }

    [Test]
    public void SkillRaisedFiresOnlyOnWholeNumberIncreases()
    {
        var raises = new List<int>();
        _skills.SkillRaised += (_, level) => raises.Add(level);

        // One report is a fractional gain, so nothing should be announced yet.
        _skills.ReportUse(Skills.Blade, 20f, RealThreat);
        Assert.That(raises, Is.Empty);

        for (var i = 0; i < 20; i++)
        {
            _skills.ReportUse(Skills.Blade, 20f, RealThreat);
            _skills.EndEncounter();
        }

        Assert.Multiple(() =>
        {
            Assert.That(raises, Is.Not.Empty);
            Assert.That(raises, Is.Ordered.Ascending);
            Assert.That(raises, Is.Unique);
        });
    }

    [Test]
    public void Rule4_CharacterLevelComesFromTotalSkillProgress()
    {
        var levels = 0;
        _skills.CharacterLevelGained += () => levels++;

        foreach (var id in Skills.All) TrainToMastery(id);

        Assert.That(levels, Is.EqualTo((int)(_skills.TotalPoints / 40f)));
    }

    [Test]
    public void Rule5_MasteryMakesCastingCheaperDownToTheFloor()
    {
        Assert.That(_skills.CostMultiplier(Skills.Destruction), Is.EqualTo(1f).Within(0.001f));

        TrainToMastery(Skills.Destruction);

        Assert.That(_skills.CostMultiplier(Skills.Destruction),
            Is.EqualTo(SoulCrystals.MinCostMultiplier).Within(0.01f));
    }

    [Test]
    public void CostMultiplierNeverFallsBelowTheFloor()
    {
        TrainToMastery(Skills.Destruction);
        Assert.That(_skills.CostMultiplier(Skills.Destruction),
            Is.GreaterThanOrEqualTo(SoulCrystals.MinCostMultiplier));
    }

    [Test]
    public void ARouteGrantsAHeadStartInItsTwoSkills()
    {
        _skills.GrantRouteSkills("route.mage");
        Assert.Multiple(() =>
        {
            Assert.That(_skills.LevelOf(Skills.Destruction), Is.EqualTo(SkillProgression.RouteGrant));
            Assert.That(_skills.LevelOf(Skills.Restoration), Is.EqualTo(SkillProgression.RouteGrant));
            Assert.That(_skills.LevelOf(Skills.Blade), Is.Zero);
        });
    }

    [Test]
    public void RefusingTheRouteGrantsNothing()
    {
        _skills.GrantRouteSkills("route.refuse");
        Assert.That(_skills.TotalPoints, Is.Zero);
    }

    [Test]
    public void ARouteGrantNeverDemotesATrainedSkill()
    {
        TrainToMastery(Skills.Destruction);
        var earned = _skills.LevelOf(Skills.Destruction);
        Assume.That(earned, Is.GreaterThan(SkillProgression.RouteGrant));

        _skills.GrantRouteSkills("route.mage");
        Assert.That(_skills.LevelOf(Skills.Destruction), Is.EqualTo(earned));
    }

    [Test]
    public void SaveAndReloadPreservesEverySkill()
    {
        _skills.GrantRouteSkills("route.trade");
        _skills.ReportUse(Skills.Blade, 20f, RealThreat);

        var restored = new SkillProgression();
        restored.Restore(_skills.Capture());

        foreach (var id in Skills.All)
            Assert.That(restored.LevelOf(id), Is.EqualTo(_skills.LevelOf(id)).Within(0.0001f));
    }

    [Test]
    public void ReloadingDoesNotHandOutAFreeCharacterLevel()
    {
        foreach (var id in Skills.All) TrainToMastery(id);
        var saved = _skills.Capture();
        Assume.That(_skills.TotalPoints, Is.GreaterThan(40f));

        var restored = new SkillProgression();
        var levelsAfterLoad = 0;
        restored.CharacterLevelGained += () => levelsAfterLoad++;
        restored.Restore(saved);

        // The classic save-scum bug: loading re-credits levels that were already granted.
        Assert.That(levelsAfterLoad, Is.Zero);
    }

    [Test]
    public void RestoreReplacesRatherThanMerges()
    {
        _skills.GrantRouteSkills("route.warrior");
        _skills.Restore(new[] { new SavedSkill { Id = Skills.Stealth, Value = 25f } });

        Assert.Multiple(() =>
        {
            Assert.That(_skills.LevelOf(Skills.Stealth), Is.EqualTo(25f));
            Assert.That(_skills.LevelOf(Skills.Blade), Is.Zero);
        });
    }

    [Test]
    public void ACorruptSaveIsClampedRatherThanTrusted()
    {
        _skills.Restore(new[]
        {
            new SavedSkill { Id = Skills.Blade, Value = 9999f },
            new SavedSkill { Id = Skills.Stealth, Value = -50f },
            new SavedSkill { Id = "skill.from_a_future_patch", Value = 40f }
        });

        Assert.Multiple(() =>
        {
            Assert.That(_skills.LevelOf(Skills.Blade), Is.EqualTo(SkillProgression.MaxSkill));
            Assert.That(_skills.LevelOf(Skills.Stealth), Is.Zero);
            Assert.That(_skills.LevelOf("skill.from_a_future_patch"), Is.Zero);
        });
    }

    [Test]
    public void ASaveCarriesAllEightSkills()
    {
        Assert.That(_skills.Capture(), Has.Count.EqualTo(Skills.All.Count));
    }

    /// <summary>Grinds one skill to its ceiling, respecting the per-encounter cap.</summary>
    private void TrainToMastery(string skillId)
    {
        for (var i = 0; i < 400; i++)
        {
            _skills.ReportUse(skillId, 50f, 500f);
            _skills.EndEncounter();
        }
    }

    [Test]
    public void HoldingAGuardTrainsBlock()
    {
        // Block existed in the list of skills and appeared nowhere else in the codebase. A
        // player could guard through a hundred fights and the number beside it never moved,
        // which is worse than not having the skill: it reads as progress that is not
        // happening.
        var player = PlayerCharacter.NewGame();
        var before = player.Skills.LevelOf(Skills.Block);

        player.Combat.SetBlocking(true);
        for (var hit = 0; hit < 12; hit++) player.Combat.TakeHit(14f);

        Assert.That(player.Skills.LevelOf(Skills.Block), Is.GreaterThan(before));
    }

    [Test]
    public void TakingAHitOnTheChinTrainsNothing()
    {
        var player = PlayerCharacter.NewGame();
        var before = player.Skills.LevelOf(Skills.Block);

        player.Combat.SetBlocking(false);
        for (var hit = 0; hit < 12; hit++) player.Combat.TakeHit(14f);

        Assert.That(player.Skills.LevelOf(Skills.Block), Is.EqualTo(before));
    }
}
