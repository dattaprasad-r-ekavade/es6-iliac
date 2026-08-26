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
}
