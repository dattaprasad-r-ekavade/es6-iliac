using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class ItemUseTests
{
    private PlayerCharacter _player = null!;

    [SetUp]
    public void Setup() => _player = PlayerCharacter.NewGame();

    [Test]
    public void DrinkingAPotionHealsAndSpendsIt()
    {
        _player.Vitals.TakeDamage(60f);
        var potions = _player.Inventory.CountOf("health_potion");
        var health = _player.Vitals.Health;

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use("health_potion", _player), Is.EqualTo(ItemUseResult.Used));
            Assert.That(_player.Vitals.Health, Is.EqualTo(health + ItemUse.PotionHeal).Within(0.01f));
            Assert.That(_player.Inventory.CountOf("health_potion"), Is.EqualTo(potions - 1));
        });
    }

    [Test]
    public void APotionIsNotWastedAtFullHealth()
    {
        var potions = _player.Inventory.CountOf("health_potion");

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use("health_potion", _player), Is.EqualTo(ItemUseResult.NoEffect));
            Assert.That(_player.Inventory.CountOf("health_potion"), Is.EqualTo(potions),
                "a misclick at full health should not cost a potion");
        });
    }

    [Test]
    public void HealingNeverOverfills()
    {
        _player.Vitals.TakeDamage(5f);
        ItemUse.Use("health_potion", _player);
        Assert.That(_player.Vitals.Health, Is.EqualTo(_player.Vitals.MaxHealth));
    }

    [Test]
    public void UsingAWeaponEquipsIt()
    {
        _player.Inventory.Add("iron_greatsword", "Iron Greatsword", 1, "weapon");

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use("iron_greatsword", _player), Is.EqualTo(ItemUseResult.Equipped));
            Assert.That(_player.Equipment.WeaponId, Is.EqualTo("iron_greatsword"));
        });
    }

    [Test]
    public void UsingArmourWearsIt()
    {
        _player.Inventory.Add("mail_hauberk", "Mail Hauberk", 1, "armour");

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use("mail_hauberk", _player), Is.EqualTo(ItemUseResult.Equipped));
            Assert.That(_player.Equipment.ArmourId, Is.EqualTo("mail_hauberk"));
        });
    }

    [Test]
    public void EquippingIsNotConsuming()
    {
        _player.Inventory.Add("iron_greatsword", "Iron Greatsword", 1, "weapon");
        ItemUse.Use("iron_greatsword", _player);
        Assert.That(_player.Inventory.CountOf("iron_greatsword"), Is.EqualTo(1));
    }

    [Test]
    public void DrawingAJivaStoneRestoresPrana()
    {
        _player.Vitals.SpendPrana(50f);
        var stones = _player.Inventory.CountOf(SoulCrystals.LesserId);
        var prana = _player.Vitals.Prana;

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use(SoulCrystals.LesserId, _player), Is.EqualTo(ItemUseResult.Used));
            Assert.That(_player.Vitals.Prana, Is.GreaterThan(prana));
            Assert.That(_player.Inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(stones - 1));
        });
    }

    [Test]
    public void AJivaStoneIsNotWastedAtFullPrana()
    {
        var stones = _player.Inventory.CountOf(SoulCrystals.LesserId);

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use(SoulCrystals.LesserId, _player), Is.EqualTo(ItemUseResult.NoEffect));
            Assert.That(_player.Inventory.CountOf(SoulCrystals.LesserId), Is.EqualTo(stones));
        });
    }

    [Test]
    public void LootAndKeysDoNothingWhenUsed()
    {
        _player.Inventory.Add("key.northwatch.dungeon", "Watchpost Key", 1, "key");
        _player.Inventory.Add("bandit_loot", "Bandit Satchel", 1, "loot");

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.Use("key.northwatch.dungeon", _player),
                Is.EqualTo(ItemUseResult.NotUsable));
            Assert.That(ItemUse.Use("bandit_loot", _player), Is.EqualTo(ItemUseResult.NotUsable));
            Assert.That(_player.Inventory.CountOf("key.northwatch.dungeon"), Is.EqualTo(1),
                "a key must survive being clicked on");
        });
    }

    [Test]
    public void UsingSomethingYouDoNotHoldFails()
    {
        Assert.That(ItemUse.Use("iron_greatsword", _player), Is.EqualTo(ItemUseResult.NotHeld));
    }

    [TestCase(null)]
    [TestCase("")]
    public void JunkIdsAreRefused(string? id)
    {
        Assert.That(ItemUse.Use(id, _player), Is.EqualTo(ItemUseResult.NotHeld));
    }

    [Test]
    public void EveryStartingItemAdvertisesWhatItDoes()
    {
        foreach (var item in _player.Inventory.Items)
        {
            Assert.That(ItemUse.Describe(item.Id, item.Kind), Is.Not.Empty);
            Assert.That(ItemUse.DescribeAction(item.Id, item.Kind), Is.Not.Empty);
        }
    }

    [Test]
    public void TheActionVerbMatchesWhatUsingItActuallyDoes()
    {
        _player.Inventory.Add("mail_hauberk", "Mail Hauberk", 1, "armour");
        _player.Vitals.TakeDamage(60f);
        _player.Vitals.SpendPrana(50f);

        Assert.Multiple(() =>
        {
            Assert.That(ItemUse.DescribeAction("iron_sword", "weapon"), Is.EqualTo("Equip"));
            Assert.That(ItemUse.DescribeAction("mail_hauberk", "armour"), Is.EqualTo("Wear"));
            Assert.That(ItemUse.DescribeAction("health_potion", "potion"), Is.EqualTo("Drink"));
            Assert.That(ItemUse.DescribeAction(SoulCrystals.LesserId, "crystal"), Is.EqualTo("Draw"));
            Assert.That(ItemUse.DescribeAction("bandit_loot", "loot"), Is.EqualTo("—"));
        });
    }
}
