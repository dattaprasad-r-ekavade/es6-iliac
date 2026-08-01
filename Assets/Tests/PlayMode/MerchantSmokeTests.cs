using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Merchant economy smoke coverage.
///
/// The 2026-07-26 audit found merchants handing out potions without charging for
/// them. It was fixed by hand and nothing has protected the fix since. VS1 rewrites
/// the actors that own this behaviour, so it is exactly the kind of thing that comes
/// back silently.
/// </summary>
public class MerchantSmokeTests : SmokeTestFixture
{
    private const int PotionPrice = 10;

    [Test]
    public void Merchant_ChargesGold_BeforeGrantingAPotion()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;
        var merchant = SpawnMerchant();

        stats.Gold = PotionPrice;
        int potionsBefore = CountOf(inventory, "health_potion");

        merchant.Interact();

        Assert.AreEqual(0, stats.Gold, "The merchant did not charge for the potion.");
        Assert.AreEqual(
            potionsBefore + 1, CountOf(inventory, "health_potion"),
            "Payment was taken but no potion was granted.");
    }

    [Test]
    public void Merchant_WithoutEnoughGold_TakesNothingAndGivesNothing()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;
        var merchant = SpawnMerchant();

        stats.Gold = PotionPrice - 1;
        int potionsBefore = CountOf(inventory, "health_potion");

        merchant.Interact();

        Assert.AreEqual(
            PotionPrice - 1, stats.Gold,
            "Gold changed on a purchase the player could not afford.");
        Assert.AreEqual(
            potionsBefore, CountOf(inventory, "health_potion"),
            "A potion was granted without sufficient payment.");
    }

    /// <summary>
    /// Repeated interaction must keep charging. A one-shot flag guarding the price
    /// would pass the single-purchase test above and still give away free stock.
    /// </summary>
    [Test]
    public void Merchant_ChargesForEveryPurchase_UntilGoldRunsOut()
    {
        SpawnPlayer();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;
        var merchant = SpawnMerchant();

        stats.Gold = PotionPrice * 2;
        int potionsBefore = CountOf(inventory, "health_potion");

        merchant.Interact();
        merchant.Interact();
        merchant.Interact(); // third has no funds behind it

        Assert.AreEqual(0, stats.Gold, "Gold went negative or was not charged per purchase.");
        Assert.AreEqual(
            potionsBefore + 2, CountOf(inventory, "health_potion"),
            "Potions granted did not match the gold actually spent.");
    }

    private NpcInteractable SpawnMerchant()
    {
        var go = Track(new GameObject("NPC_TestMerchant"));
        var npc = go.AddComponent<NpcInteractable>();
        npc.NpcName = "Test Merchant";
        npc.Lines = new[] { "Wares for sale." };
        npc.IsMerchant = true;
        return npc;
    }
}
