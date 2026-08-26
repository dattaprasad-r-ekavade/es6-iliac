using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What the stall does after something is bought.
///
/// Both halves of this were wrong in the same way: every purchase marked the item sold out
/// forever. That made the stall useless after one visit, and — much worse — it made a death
/// unrecoverable, because dying takes half the pack and the shelf that could replace it was
/// empty for the rest of the save.
/// </summary>
[TestFixture]
public sealed class ShopRestockTests
{
    private static Shop NewShop() => new(new ShopDefinition
    {
        Id = "shop.test",
        DisplayName = "Test stall",
        Items = new[]
        {
            new ShopItemDefinition
                { Id = "health_potion", Name = "Health Potion", Kind = "potion", Price = 1, Count = 1 },
            new ShopItemDefinition
                { Id = "arrow", Name = "Arrows", Kind = "ammunition", Price = 1, Count = 20 },
            new ShopItemDefinition
                { Id = "steel_sword", Name = "Steel Sword", Kind = "weapon", Price = 1, Count = 1 }
        }
    });

    private static PlayerCharacter Rich()
    {
        var player = PlayerCharacter.NewGame();
        player.Vitals.AddGold(10_000);
        return player;
    }

    private static ShopPurchaseResult Buy(Shop shop, PlayerCharacter player, int index) =>
        shop.Buy(index, player.Vitals, player.Inventory, out _);

    [Test]
    public void PotionsCanBeBoughtTwice()
    {
        var shop = NewShop();
        var player = Rich();

        Assert.That(Buy(shop, player, 0), Is.EqualTo(ShopPurchaseResult.Bought));
        Assert.That(Buy(shop, player, 0), Is.EqualTo(ShopPurchaseResult.Bought));
    }

    [Test]
    public void ArrowsCanBeBoughtTwice()
    {
        // The one that matters most now that a bow spends them: a quiver you can refill once
        // is a bow you can use for one descent.
        var shop = NewShop();
        var player = Rich();

        Assert.That(Buy(shop, player, 1), Is.EqualTo(ShopPurchaseResult.Bought));
        Assert.That(Buy(shop, player, 1), Is.EqualTo(ShopPurchaseResult.Bought));
    }

    [Test]
    public void GearIsOneToAShelfWhileYouAreInTown()
    {
        var shop = NewShop();
        var player = Rich();

        Assert.That(Buy(shop, player, 2), Is.EqualTo(ShopPurchaseResult.Bought));
        Assert.That(Buy(shop, player, 2), Is.EqualTo(ShopPurchaseResult.SoldOut));
    }

    [Test]
    public void ADescentRestocksTheGear()
    {
        var shop = NewShop();
        var player = Rich();

        Buy(shop, player, 2);
        shop.Restock();

        Assert.That(Buy(shop, player, 2), Is.EqualTo(ShopPurchaseResult.Bought),
            "a player who lost their weapon could never replace it");
    }

    [Test]
    public void RestockingIsSafeWhenNothingWasBought()
    {
        var shop = NewShop();
        shop.Restock();

        Assert.That(shop.IsSoldOut("steel_sword"), Is.False);
    }

    [Test]
    public void EveryConsumableKindInTheRealStallRestocks()
    {
        // Guards the kind strings against a content edit: a new consumable typed as something
        // else would sell out permanently and nobody would notice until a playtest.
        foreach (var kind in new[] { "potion", "crystal", "ammunition", "misc" })
            Assert.That(
                Shop.IsConsumable(new ShopItemDefinition { Id = "x", Name = "x", Kind = kind }),
                Is.True, kind);

        foreach (var kind in new[] { "weapon", "armour", "shield" })
            Assert.That(
                Shop.IsConsumable(new ShopItemDefinition { Id = "x", Name = "x", Kind = kind }),
                Is.False, kind);
    }

    /// <summary>
    /// The sale is written to the save, so restocking has to unwrite it.
    ///
    /// This is the half the original tests missed, and missed for a reason worth remembering:
    /// they exercised Shop on its own, and the bug lived on the seam between Shop and the
    /// story state. Restock cleared the in-memory set and reported success; the game then
    /// rebuilt the shop from the save on the next descent and marked everything sold out
    /// again. Every check passed while the stall emptied permanently in the player's hands.
    ///
    /// So this test does what the game does: buy, persist the sale, restock, rebuild.
    /// </summary>
    [Test]
    public void RestockingClearsTheMarksASaveWouldRestore()
    {
        var shop = NewShop();
        var player = Rich();
        var story = player.Story;

        Assert.That(Buy(shop, player, 2), Is.EqualTo(ShopPurchaseResult.Bought),
            "the sword should sell the first time");

        // What the game does on a purchase: remember it in a place that survives a reload.
        story.MarkLooted("shop.shop.test.steel_sword");
        Assert.That(story.Capture().LootedObjects, Does.Contain("shop.shop.test.steel_sword"));

        // What the game does at the end of a descent.
        var cleared = shop.Restock();
        Assert.That(cleared, Does.Contain("steel_sword"),
            "restocking must report what it put back, or the caller cannot forget it");

        foreach (var itemId in cleared) story.ForgetLooted($"shop.shop.test.{itemId}");

        Assert.That(story.Capture().LootedObjects, Does.Not.Contain("shop.shop.test.steel_sword"),
            "a restocked shelf must not still be recorded as looted");

        // What the game does on the next descent: build the shop again from the save.
        var reopened = NewShop();
        foreach (var looted in story.Capture().LootedObjects)
            if (looted.StartsWith("shop.shop.test.", System.StringComparison.Ordinal))
                reopened.MarkSoldOut(looted["shop.shop.test.".Length..]);

        Assert.That(reopened.IsSoldOut("steel_sword"), Is.False,
            "the sword is back on the shelf");
        Assert.That(Buy(reopened, Rich(), 2), Is.EqualTo(ShopPurchaseResult.Bought),
            "and can be bought again after a death took the first one");
    }

    /// <summary>Nothing sold means nothing to report, and no needless save churn.</summary>
    [Test]
    public void RestockingAnUntouchedStallReportsNothing()
    {
        Assert.That(NewShop().Restock(), Is.Empty);
    }

    /// <summary>Forgetting something that was never taken is not an error, and changes nothing.</summary>
    [Test]
    public void ForgettingSomethingNeverLootedIsHarmless()
    {
        var story = PlayerCharacter.NewGame().Story;

        Assert.That(story.ForgetLooted("shop.shop.test.nothing"), Is.False);
        Assert.That(story.ForgetLooted(null), Is.False);
        Assert.That(story.ForgetLooted("  "), Is.False);
        Assert.That(story.Capture().LootedObjects, Is.Empty);
    }
}
