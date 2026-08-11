using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Spells and use-based skills.
///
/// The five anti-grind rules in Docs/GAMEPLAY_DESIGN.md are not optional — without them,
/// use-based progression becomes jumping in a corner for an hour. Most of them are enforced
/// here rather than by convention, because a rule nothing tests is a rule that rots.
/// </summary>
public class SpellAndSkillSmokeTests : SmokeTestFixture
{
    private SkillSystem SpawnSkills()
    {
        var player = SpawnPlayer();
        player.AddComponent<PlayerCombat>();
        return player.AddComponent<SkillSystem>();
    }

    private static EnemyBrain SpawnDummy(float health = 100f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "TestDummy";
        var brain = go.AddComponent<EnemyBrain>();
        brain.Setup("Test Dummy", health, "test_dummy");
        return brain;
    }

    // --- spells --------------------------------------------------------------

    [Test]
    public void EveryElement_DoesSomethingDifferent()
    {
        Assert.AreEqual(SpellEffect.Fire, SpellCatalog.Get(SpellCatalog.FireId).Effect);
        Assert.AreEqual(SpellEffect.Frost, SpellCatalog.Get(SpellCatalog.FrostId).Effect);
        Assert.AreEqual(SpellEffect.Shock, SpellCatalog.Get(SpellCatalog.ShockId).Effect);
        Assert.AreEqual(SpellSchool.Restoration, SpellCatalog.Get(SpellCatalog.HealId).School);
        Assert.AreEqual(SpellSchool.Restoration, SpellCatalog.Get(SpellCatalog.LightId).School);
    }

    [Test]
    public void Fire_KeepsDamagingAfterTheHit()
    {
        var enemy = SpawnDummy();
        Track(enemy.gameObject);

        enemy.ApplyBurn(10f, 3f);

        Assert.IsTrue(enemy.IsBurning, "Fire did not leave a burn — it is the reason it beats groups.");
    }

    [Test]
    public void Frost_SlowsTheTarget()
    {
        var enemy = SpawnDummy();
        Track(enemy.gameObject);
        float normal = enemy.CurrentMoveSpeed;

        enemy.ApplyChill(0.45f, 3f);

        Assert.IsTrue(enemy.IsChilled);
        Assert.Less(enemy.CurrentMoveSpeed, normal, "Frost did not slow the target.");
    }

    [Test]
    public void Shock_Staggers_WhichIsWhatMakesItControlRatherThanDamage()
    {
        var enemy = SpawnDummy();
        Track(enemy.gameObject);

        enemy.ApplyStagger(1f);

        Assert.IsTrue(enemy.IsStaggered, "Shock did not interrupt the target.");
    }

    [Test]
    public void Healing_CostsCharge_AndRestoresHealth()
    {
        var player = SpawnPlayer();
        var caster = player.AddComponent<SpellCaster>();
        var stats = PlayerStats.Instance;
        stats.Health = 20f;
        stats.Mana = stats.MaxMana;
        float chargeBefore = stats.Mana;

        Assert.IsTrue(caster.Cast(SpellCatalog.HealId), "Heal did not go off with charge available.");

        Assert.Greater(stats.Health, 20f, "Heal did not restore health.");
        Assert.Less(stats.Mana, chargeBefore, "Heal was free — every spell must cost charge.");
    }

    [Test]
    public void Light_IsCheapButNotFree()
    {
        var player = SpawnPlayer();
        var caster = player.AddComponent<SpellCaster>();
        var stats = PlayerStats.Instance;
        stats.Mana = stats.MaxMana;
        float before = stats.Mana;

        Assert.IsTrue(caster.Cast(SpellCatalog.LightId));

        Assert.IsTrue(caster.LightActive, "Light did not come on.");
        Assert.Less(stats.Mana, before,
            "Light was free. In a crystal-lit world, seeing costs the same resource.");
    }

    [Test]
    public void Casting_FailsAndAppliesNothing_WhenItCannotBePaidFor()
    {
        var player = SpawnPlayer();
        var caster = player.AddComponent<SpellCaster>();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;

        int held = inventory.CountOf(SoulCrystals.LesserId);
        if (held > 0) inventory.Consume(SoulCrystals.LesserId, held);
        stats.Mana = 0f;
        stats.Health = 10f;

        Assert.IsFalse(caster.Cast(SpellCatalog.HealId), "An unpayable spell still went off.");
        Assert.AreEqual(10f, stats.Health, 0.001f, "An unpaid heal still healed.");
    }

    // --- skills --------------------------------------------------------------

    [Test]
    public void LandedUse_RaisesTheSkill()
    {
        var skills = SpawnSkills();
        float before = skills.LevelOf(Skills.Blade);

        skills.ReportUse(Skills.Blade, 18f, 100f);

        Assert.Greater(skills.LevelOf(Skills.Blade), before, "A landed hit did not train the skill.");
    }

    /// <summary>Rule 2: the fortieth identical bandit must be worth nothing.</summary>
    [Test]
    public void TrivialTargets_TrainNothing_OnceYouAreGood()
    {
        var skills = SpawnSkills();
        skills.GrantRouteSkills("route.warrior", 90f);
        float before = skills.LevelOf(Skills.Blade);

        // A threat that would matter at level 1 is beneath a near-master.
        skills.ReportUse(Skills.Blade, 18f, 1f);

        Assert.AreEqual(before, skills.LevelOf(Skills.Blade), 0.0001f,
            "A trivial target still trained a near-master. Rule 2 is not holding.");
    }

    /// <summary>Rule 3: one enemy cannot be farmed.</summary>
    [Test]
    public void GainsDiminish_WithinASingleEncounter()
    {
        var skills = SpawnSkills();

        skills.ReportUse(Skills.Blade, 18f, 100f);
        float firstGain = skills.LevelOf(Skills.Blade);

        for (int i = 0; i < 40; i++) skills.ReportUse(Skills.Blade, 18f, 100f);
        float total = skills.LevelOf(Skills.Blade);

        Assert.Less(total, firstGain * 40f,
            "Forty identical hits trained linearly. One enemy is farmable.");
    }

    [Test]
    public void MagicSkill_ReducesChargeCost()
    {
        var skills = SpawnSkills();
        var spell = SpellCatalog.Get(SpellCatalog.FireId);

        float novice = SpellCaster.CostOf(spell);
        skills.GrantRouteSkills("route.mage", SkillSystem.MaxSkill);
        float expert = SpellCaster.CostOf(spell);

        Assert.Less(expert, novice, "Skill did not improve crystal mileage.");
        Assert.AreEqual(
            spell.BaseCost * SoulCrystals.MinCostMultiplier, expert, 0.01f,
            "Mastery should land on the documented floor — roughly 3–4x casts per crystal.");
    }

    [Test]
    public void RouteAssignment_GrantsTwoSkills_AndRefuseGrantsNone()
    {
        var skills = SpawnSkills();

        skills.GrantRouteSkills("route.trade");
        Assert.Greater(skills.LevelOf(Skills.Stealth), 0f);
        Assert.Greater(skills.LevelOf(Skills.Security), 0f);

        skills.GrantRouteSkills("route.refuse");
        foreach (var id in Skills.All)
        {
            if (id == Skills.Stealth || id == Skills.Security) continue;
            Assert.AreEqual(0f, skills.LevelOf(id), 0.0001f,
                "Refuse granted a skill. The fastest route gives the least.");
        }
    }

    [Test]
    public void Skills_SurviveASaveRoundTrip()
    {
        var skills = SpawnSkills();
        var save = SpawnSaveService();
        skills.GrantRouteSkills("route.mage", 42f);
        float expected = skills.LevelOf(Skills.Destruction);

        save.Save();
        skills.Restore(null);
        Assert.AreEqual(0f, skills.LevelOf(Skills.Destruction), 0.0001f, "Test setup failed to clear skills.");

        save.Load();

        Assert.AreEqual(expected, skills.LevelOf(Skills.Destruction), 0.01f,
            "Skill progress was lost across a save.");
    }

    /// <summary>
    /// Character level derives from total skill progress. Reloading must not re-credit points
    /// that already paid for a level, or every load is a free level-up.
    /// </summary>
    [Test]
    public void Reloading_DoesNotHandOutFreeLevels()
    {
        var skills = SpawnSkills();
        var stats = PlayerStats.Instance;
        skills.GrantRouteSkills("route.warrior", SkillSystem.MaxSkill);

        var saved = skills.Capture();
        skills.Restore(saved);
        int levelAfterFirstLoad = stats.Level;

        skills.Restore(saved);
        skills.Restore(saved);

        Assert.AreEqual(levelAfterFirstLoad, stats.Level,
            "Repeated loads granted extra levels from the same skill points.");
    }
}
