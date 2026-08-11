using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The crystal-charge resource model.
///
/// Magic runs on crystals; crystals run on souls. A player who regenerates free mana for
/// forty hours contradicts the arc's central conflict at every second, so mana is charge
/// drawn from a consumable rather than a pool that refills itself.
///
/// These tests exist because "mana slowly creeps back up" is the single easiest thing to
/// reintroduce by accident — it is what every other RPG does.
/// </summary>
public class ResourceSmokeTests : SmokeTestFixture
{
    [UnityTest]
    public IEnumerator Mana_DoesNotRegenerateOnItsOwn()
    {
        var player = SpawnPlayer();
        var stats = PlayerStats.Instance;
        // Strip the reserve so a refill would be unmistakable.
        StripCrystals(PlayerInventory.Instance);
        stats.Mana = 10f;

        for (int i = 0; i < 10; i++) yield return null;

        Assert.AreEqual(
            10f, stats.Mana, 0.001f,
            "Mana regenerated. It is crystal charge, not a pool — see GAMEPLAY_DESIGN.md.");
        Assert.IsNotNull(player);
    }

    [UnityTest]
    public IEnumerator Stamina_RegeneratesInCombat_ButSlower()
    {
        var player = SpawnPlayer();
        var combat = player.AddComponent<PlayerCombat>();
        var stats = PlayerStats.Instance;
        stats.Stamina = 0f;

        combat.EnterCombat();
        yield return null;
        float inCombat = stats.Stamina;

        Assert.Greater(
            inCombat, 0f,
            "Stamina did not regenerate in combat. Zero regen gave 12 swings and then six " +
            "seconds of standing there unable to attack.");

        stats.Stamina = 0f;
        combat.ClearCombat();
        yield return null;
        float resting = stats.Stamina;

        Assert.Greater(resting, inCombat, "Resting regen should outpace combat regen.");
    }

    [Test]
    public void Casting_DrawsOnACrystal_WhenChargeIsShort()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;

        int crystalsBefore = inventory.CountOf(SoulCrystals.LesserId);
        Assert.Greater(crystalsBefore, 0, "Test setup: the player should start with crystals.");

        stats.Mana = 0f;
        bool spent = stats.SpendMana(16f);

        Assert.IsTrue(spent, "Casting failed despite a crystal being available to draw on.");
        Assert.AreEqual(
            crystalsBefore - 1, inventory.CountOf(SoulCrystals.LesserId),
            "Casting on an empty reserve did not consume a crystal.");
        Assert.AreEqual(
            SoulCrystals.LesserCharge - 16f, stats.Mana, 0.001f,
            "The crystal's charge was not applied before the cost was taken.");
        Assert.AreEqual(1, stats.Channeled, "Burning a crystal did not register as channeling.");
    }

    [Test]
    public void Casting_FailsWhenChargeAndCrystalsAreBothGone()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;

        StripCrystals(inventory);
        stats.Mana = 0f;

        Assert.IsFalse(stats.SpendMana(16f), "Casting succeeded with no charge and no crystals.");
        Assert.AreEqual(0, stats.Channeled, "A failed cast still counted as channeling.");
    }

    /// <summary>
    /// Dying is not a resupply route. Health and stamina come back; charge does not, because
    /// crystals cost gold and death would otherwise be the cheapest way to buy them.
    /// </summary>
    [Test]
    public void Recovery_RestoresHealthAndStamina_ButNotCharge()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        stats.Health = 5f;
        stats.Stamina = 5f;
        stats.Mana = 5f;

        stats.FullRestore();

        Assert.AreEqual(stats.MaxHealth, stats.Health, 0.001f, "Health was not restored.");
        Assert.AreEqual(stats.MaxStamina, stats.Stamina, 0.001f, "Stamina was not restored.");
        Assert.AreEqual(5f, stats.Mana, 0.001f, "Recovery refilled charge for free.");
    }

    [Test]
    public void LevellingUp_RaisesTheChargeCeiling_WithoutRefillingIt()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        stats.Mana = 3f;
        float ceilingBefore = stats.MaxMana;

        stats.AddXp(stats.XpToLevel);

        Assert.Greater(stats.MaxMana, ceilingBefore, "Levelling did not raise the charge ceiling.");
        Assert.AreEqual(3f, stats.Mana, 0.001f, "Levelling silently resupplied the player.");
    }

    /// <summary>
    /// Channeling is persisted in the story snapshot rather than beside the stats, because
    /// that is the copy topic dialogue reads through the `player.channeled` condition.
    /// Burning a crystal must reach it, or the counter rises and the world never notices.
    /// </summary>
    [Test]
    public void BurningACrystal_ReachesTheStoryStateDialogueReads()
    {
        SpawnPlayer();
        var story = Track(new GameObject("StoryDirector_Test")).AddComponent<StoryDirector>();
        var stats = PlayerStats.Instance;

        stats.Mana = 0f;
        stats.SpendMana(1f);

        Assert.AreEqual(1, stats.Channeled, "The runtime counter did not move.");
        Assert.AreEqual(
            1f, story.State.PlayerChanneled, 0.001f,
            "Burning a crystal never reached StoryDirector, so dialogue would never react.");
    }

    [Test]
    public void Channeled_SurvivesASaveRoundTrip()
    {
        SpawnPlayer();
        Track(new GameObject("StoryDirector_Test")).AddComponent<StoryDirector>();
        var save = SpawnSaveService();
        var stats = PlayerStats.Instance;

        stats.Mana = 0f;
        stats.SpendMana(1f);
        stats.SpendMana(SoulCrystals.LesserCharge);
        int channeled = stats.Channeled;
        Assert.Greater(channeled, 0, "Test setup: the player should have burned a crystal.");

        save.Save();
        stats.RestoreChanneled(0);
        save.Load();

        Assert.AreEqual(
            channeled, stats.Channeled,
            "Lifetime channeling was lost across a save. It is what the world reacts to.");
    }

    /// <summary>
    /// Crystals are the sink that gives gold a purpose, so the merchant must sell them once
    /// the player is running dry — and must still charge for them.
    /// </summary>
    [Test]
    public void Merchant_SellsCrystals_WhenThePlayerIsNearlyOut()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;
        var merchant = Track(new GameObject("NPC_TestMerchant")).AddComponent<NpcInteractable>();
        merchant.IsMerchant = true;
        merchant.Lines = new[] { "Wares." };

        StripCrystals(inventory);
        stats.Gold = SoulCrystals.LesserBasePrice;

        merchant.Interact();

        Assert.AreEqual(
            1, inventory.CountOf(SoulCrystals.LesserId),
            "The merchant did not restock a player who was out of crystals.");
        Assert.AreEqual(0, stats.Gold, "The crystal was not paid for.");
    }

    /// <summary>
    /// Empty the crystal stack. <see cref="PlayerInventory.Consume"/> refuses partial fills,
    /// so asking for more than the player holds is a no-op rather than a strip — correct
    /// behaviour, and it caught a bad first draft of these tests.
    /// </summary>
    private static void StripCrystals(PlayerInventory inventory)
    {
        int held = inventory.CountOf(SoulCrystals.LesserId);
        if (held > 0) inventory.Consume(SoulCrystals.LesserId, held);
    }
}
