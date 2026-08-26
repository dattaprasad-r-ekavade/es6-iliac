using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// Sockets, and the rules that keep stones the tactical layer rather than a second
/// progression track.
///
/// The one that matters most is that stones do not survive a descent. If they ever do, the two
/// progression layers collapse into one and every run becomes the same run with better numbers
/// — which is precisely what the short run length was chosen to avoid, and it would happen
/// silently.
/// </summary>
[TestFixture]
public sealed class StoneSlotTests
{
    private static PlayerCharacter NewPlayer() => PlayerCharacter.NewGame();

    private static Enemy Spawn() =>
        new(new EnemyArchetype { Id = "bandit", DisplayName = "Bandit", MaxHealth = 200f },
            "bandit.01");

    private static void Wield(PlayerCharacter player, string id)
    {
        player.Inventory.Add(id, EquipmentCatalog.GetWeapon(id).DisplayName, 1, "weapon");
        player.Equipment.Equip(id);
    }

    /// <summary>Find a stone and put it straight into a socket.</summary>
    private static void Socket(PlayerCharacter player, string stoneId)
    {
        player.Stones.Found(stoneId);
        Assert.That(player.Stones.Socket(stoneId), Is.True, $"could not socket {stoneId}");
    }

    // ------------------------------------------------------------------ the central rule

    [Test]
    public void StonesDoNotSurviveADescent()
    {
        var player = NewPlayer();
        Socket(player, StoneCatalog.CinderId);
        player.Stones.Found(StoneCatalog.RimeId);

        player.Stones.ClearForDescent();

        Assert.Multiple(() =>
        {
            Assert.That(player.Stones.Socketed, Is.Empty);
            Assert.That(player.Stones.Loose, Is.Empty);
            Assert.That(player.Stones.Has(StoneEffect.Cinder), Is.False);
        });
    }

    [Test]
    public void ClearingIsSafeToRepeat()
    {
        // Called on entering a descent, which can happen twice in a row if a run is abandoned
        // before it starts.
        var player = NewPlayer();
        player.Stones.ClearForDescent();
        player.Stones.ClearForDescent();

        Assert.That(player.Stones.Socketed, Is.Empty);
    }

    // ------------------------------------------------------------------ sockets

    [Test]
    public void AFreshCharacterHasSomewhereToPutAStone()
    {
        Assert.That(NewPlayer().Stones.Capacity, Is.GreaterThan(0));
    }

    [Test]
    public void ABetterWeaponHasMoreRoomInIt()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword");
        var tierOne = player.Stones.Capacity;

        Wield(player, "steel_sword");

        Assert.That(player.Stones.Capacity, Is.GreaterThan(tierOne));
    }

    [Test]
    public void ASocketCannotBeFilledTwice()
    {
        var player = NewPlayer();
        while (player.Stones.HasRoom) Socket(player, StoneCatalog.CinderId);

        player.Stones.Found(StoneCatalog.RimeId);

        Assert.That(player.Stones.Socket(StoneCatalog.RimeId), Is.False);
        Assert.That(player.Stones.Loose, Does.Contain(StoneCatalog.RimeId));
    }

    [Test]
    public void AStoneCannotBeSocketedWithoutFindingItFirst()
    {
        var player = NewPlayer();

        Assert.That(player.Stones.Socket(StoneCatalog.CinderId), Is.False);
        Assert.That(player.Stones.Socketed, Is.Empty);
    }

    [Test]
    public void UnsocketingPutsItBackWithinReach()
    {
        var player = NewPlayer();
        Socket(player, StoneCatalog.CinderId);

        Assert.That(player.Stones.Unsocket(StoneCatalog.CinderId), Is.True);
        Assert.That(player.Stones.Loose, Does.Contain(StoneCatalog.CinderId));
        Assert.That(player.Stones.Has(StoneEffect.Cinder), Is.False);
    }

    [Test]
    public void DowngradingGearPushesAStoneOutRatherThanLosingIt()
    {
        var player = NewPlayer();
        Wield(player, "steel_sword");
        while (player.Stones.HasRoom) Socket(player, StoneCatalog.CinderId);

        var held = player.Stones.Socketed.Count;
        Wield(player, "iron_sword");

        Assert.Multiple(() =>
        {
            Assert.That(player.Stones.Socketed.Count, Is.LessThan(held));
            Assert.That(player.Stones.Socketed.Count + player.Stones.Loose.Count,
                Is.EqualTo(held), "a stone went missing when the weapon was swapped");
        });
    }

    // ------------------------------------------------------------------ the effects

    [Test]
    public void CinderSetsWhatYouHitAlight()
    {
        var player = NewPlayer();
        var enemy = Spawn();

        player.Combat.TryAttack(enemy);
        Assert.That(enemy.IsBurning, Is.False, "a bare blade should not ignite anything");

        Socket(player, StoneCatalog.CinderId);
        player.Combat.Tick(5f);
        player.Combat.TryAttack(enemy);

        Assert.That(enemy.IsBurning, Is.True);
    }

    [Test]
    public void RimeSlowsWhatYouHit()
    {
        var player = NewPlayer();
        var enemy = Spawn();
        Socket(player, StoneCatalog.RimeId);

        player.Combat.TryAttack(enemy);

        Assert.That(enemy.IsChilled, Is.True);
    }

    [Test]
    public void ThunderStaggersWhateverYouAreHolding()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword");
        var enemy = Spawn();

        player.Combat.TryAttack(enemy);
        Assert.That(enemy.IsStaggered, Is.False, "a blade staggers nothing on its own");

        Socket(player, StoneCatalog.ThunderId);
        player.Combat.Tick(5f);
        player.Combat.TryAttack(enemy);

        Assert.That(enemy.IsStaggered, Is.True);
    }

    [Test]
    public void ThunderDoesNotOutdoTheWeaponBuiltForIt()
    {
        // A stone that hands every weapon the mace's verb in full would make the mace
        // pointless, which is the opposite of variety.
        Assert.That(EquipmentCatalog.GetWeapon("iron_mace").StaggerSeconds,
            Is.GreaterThan(StoneCatalog.ThunderSeconds));
    }

    [Test]
    public void SplittingMakesABladeSweep()
    {
        var player = NewPlayer();
        Wield(player, "iron_sword");

        Assert.That(player.Combat.WeaponSweeps, Is.False);

        Socket(player, StoneCatalog.SplittingId);

        Assert.That(player.Combat.WeaponSweeps, Is.True);
    }

    [Test]
    public void VesselGivesPranaBackOnAKill()
    {
        var player = NewPlayer();
        Socket(player, StoneCatalog.VesselId);

        player.Vitals.SpendPrana(40f);
        var before = player.Vitals.Prana;

        player.NotifyEnemyKilled(Spawn());

        Assert.That(player.Vitals.Prana, Is.GreaterThan(before));
    }

    [Test]
    public void WithoutVesselAKillGivesNoPrana()
    {
        var player = NewPlayer();
        player.Vitals.SpendPrana(40f);
        var before = player.Vitals.Prana;

        player.NotifyEnemyKilled(Spawn());

        Assert.That(player.Vitals.Prana, Is.EqualTo(before));
    }

    // ------------------------------------------------------------------ the catalogue

    [Test]
    public void EveryStoneChangesAVerbRatherThanANumber()
    {
        // Not assertable directly, so it is asserted structurally: one effect per stone, and
        // every effect in the enum represented. A stone added as a damage bonus would have no
        // effect to name and would fail here.
        Assert.That(StoneCatalog.All.Select(stone => stone.Effect).Distinct().Count(),
            Is.EqualTo(StoneCatalog.All.Count));

        Assert.That(StoneCatalog.All.Select(stone => stone.Effect),
            Is.EquivalentTo(System.Enum.GetValues<StoneEffect>()));
    }

    [Test]
    public void EveryStoneSaysWhatItDoes()
    {
        foreach (var stone in StoneCatalog.All)
        {
            Assert.That(stone.DisplayName, Is.Not.Empty);
            Assert.That(stone.Description, Is.Not.Empty, stone.Id);
        }
    }

    [Test]
    public void TheShallowestMineStillGivesUpSomething()
    {
        Assert.That(StoneCatalog.AvailableAt(1), Is.Not.Empty);
    }

    [Test]
    public void DeeperMinesOfferMore()
    {
        Assert.That(StoneCatalog.AvailableAt(3).Count,
            Is.GreaterThan(StoneCatalog.AvailableAt(1).Count));
    }
}
