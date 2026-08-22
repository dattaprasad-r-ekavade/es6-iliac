using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class InventoryTests
{
    private Inventory _inventory = null!;

    [SetUp]
    public void Setup() => _inventory = new Inventory();

    [Test]
    public void ANewInventoryIsEmpty()
    {
        Assert.That(_inventory.Items, Is.Empty);
    }

    [Test]
    public void TheStartingKitArmsThePlayerAndStocksCrystals()
    {
        var kit = Inventory.CreateStartingKit();
        Assert.Multiple(() =>
        {
            Assert.That(kit.CountOf("iron_sword"), Is.EqualTo(1));
            Assert.That(kit.CountOf("health_potion"), Is.EqualTo(3));
            Assert.That(kit.CountOf(SoulCrystals.LesserId), Is.EqualTo(3));
        });
    }

    [Test]
    public void EveryStartingWeaponIsARealCatalogEntry()
    {
        var kit = Inventory.CreateStartingKit();
        foreach (var stack in kit.Items.Where(i => i.Kind == "weapon"))
            Assert.That(EquipmentCatalog.IsWeapon(stack.Id), Is.True,
                $"{stack.Id} is carried but has no stats");
    }

    [Test]
    public void AddingTheSameIdStacksRatherThanDuplicating()
    {
        _inventory.Add("health_potion", "Health Potion", 2, "potion");
        _inventory.Add("health_potion", "Health Potion", 3, "potion");

        Assert.Multiple(() =>
        {
            Assert.That(_inventory.Items, Has.Count.EqualTo(1));
            Assert.That(_inventory.CountOf("health_potion"), Is.EqualTo(5));
        });
    }

    [TestCase("", 1)]
    [TestCase(null, 1)]
    [TestCase("torch", 0)]
    [TestCase("torch", -1)]
    public void JunkAddsAreIgnored(string? id, int count)
    {
        _inventory.Add(id!, "Whatever", count, "misc");
        Assert.That(_inventory.Items, Is.Empty);
    }

    [Test]
    public void ConsumingRemovesTheStackWhenItEmpties()
    {
        _inventory.Add("torch", "Torch", 1, "misc");
        Assert.Multiple(() =>
        {
            Assert.That(_inventory.Consume("torch"), Is.True);
            Assert.That(_inventory.Items, Is.Empty);
            Assert.That(_inventory.CountOf("torch"), Is.Zero);
        });
    }

    [Test]
    public void ConsumingMoreThanHeldTakesNothingAtAll()
    {
        _inventory.Add("health_potion", "Health Potion", 2, "potion");

        Assert.Multiple(() =>
        {
            Assert.That(_inventory.Consume("health_potion", 3), Is.False);
            Assert.That(_inventory.CountOf("health_potion"), Is.EqualTo(2),
                "a failed consume must not partially drain the stack");
        });
    }

    [Test]
    public void ConsumingWhatIsNotHeldFails()
    {
        Assert.That(_inventory.Consume("health_potion"), Is.False);
    }

    [Test]
    public void ChangedFiresOnAddAndOnConsume()
    {
        var fired = 0;
        _inventory.Changed += () => fired++;

        _inventory.Add("torch", "Torch", 1, "misc");
        _inventory.Consume("torch");

        Assert.That(fired, Is.EqualTo(2));
    }

    [Test]
    public void ChangedDoesNotFireForARejectedChange()
    {
        var fired = 0;
        _inventory.Changed += () => fired++;

        _inventory.Consume("nothing_held");
        _inventory.Add("", "Nameless", 1, "misc");

        Assert.That(fired, Is.Zero);
    }

    [Test]
    public void ClearingAnEmptyInventoryIsNotAChange()
    {
        var fired = 0;
        _inventory.Changed += () => fired++;
        _inventory.Clear();
        Assert.That(fired, Is.Zero);
    }
}
