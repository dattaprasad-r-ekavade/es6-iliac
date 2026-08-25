using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The three tiers of risen dead, and the rules about where they appear.
///
/// The escalation is the point: depth already bites through enemy level, which is a number the
/// player never sees, and a tier is that same escalation made visible as a silhouette. If the
/// ladder ever stops climbing, or a tier turns up in the first room, the mine stops teaching
/// depth and these are the assertions that say so.
/// </summary>
[TestFixture]
public sealed class PretaTierTests
{
    [Test]
    public void TheThreeTiersExist()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EnemyCatalog.Find(EnemyCatalog.ChhayaId), Is.Not.Null);
            Assert.That(EnemyCatalog.Find(EnemyCatalog.VetalaId), Is.Not.Null);
            Assert.That(EnemyCatalog.Find(EnemyCatalog.KravyadaId), Is.Not.Null);
        });
    }

    [Test]
    public void EachTierIsHardierThanTheOneBelowIt()
    {
        var chhaya = EnemyCatalog.Find(EnemyCatalog.ChhayaId)!;
        var vetala = EnemyCatalog.Find(EnemyCatalog.VetalaId)!;
        var kravyada = EnemyCatalog.Find(EnemyCatalog.KravyadaId)!;

        Assert.Multiple(() =>
        {
            Assert.That(vetala.MaxHealth, Is.GreaterThan(chhaya.MaxHealth));
            Assert.That(kravyada.MaxHealth, Is.GreaterThan(vetala.MaxHealth));

            Assert.That(vetala.AttackDamage, Is.GreaterThan(chhaya.AttackDamage));
            Assert.That(kravyada.AttackDamage, Is.GreaterThan(vetala.AttackDamage));

            Assert.That(vetala.XpReward, Is.GreaterThan(chhaya.XpReward));
            Assert.That(kravyada.XpReward, Is.GreaterThan(vetala.XpReward));
        });
    }

    [Test]
    public void TheHigherTiersAreSlowerThanTheCommonOne()
    {
        // A chhaya is a pressure enemy and the higher tiers are not: they do not need to rush.
        // A player who has learned to kite chhaya has to learn something else, and that only
        // works if the ladder does not simply make everything faster as well as stronger.
        var chhaya = EnemyCatalog.Find(EnemyCatalog.ChhayaId)!;

        Assert.That(EnemyCatalog.Find(EnemyCatalog.VetalaId)!.MoveSpeed,
            Is.LessThan(chhaya.MoveSpeed));
        Assert.That(EnemyCatalog.Find(EnemyCatalog.KravyadaId)!.MoveSpeed,
            Is.LessThan(EnemyCatalog.Find(EnemyCatalog.VetalaId)!.MoveSpeed));
    }

    [Test]
    public void TheOldPretaIdStillResolves()
    {
        // Manifests and saves written before the rename carry "preta". A mine that fails to
        // spawn its enemies is a worse outcome than a legacy alias.
        Assert.That(EnemyCatalog.Find(EnemyCatalog.PretaId)?.Id, Is.EqualTo(EnemyCatalog.ChhayaId));
    }

    [Test]
    public void ShallowMinesHoldNothingAboveTheCommonTier()
    {
        // Four rooms is inside the first tier's range for every seed, so nothing above chhaya
        // may appear however the dice fall.
        for (var seed = 0; seed < 60; seed++)
        {
            var mine = MineGenerator.Generate(seed, rooms: 4, depth: 1);
            var ids = mine.Spawns.Select(spawn => spawn.ArchetypeId).ToArray();

            Assert.That(ids, Has.None.EqualTo(EnemyCatalog.VetalaId),
                $"seed {seed} put a vetala in a four-room mine");
            Assert.That(ids, Has.None.EqualTo(EnemyCatalog.KravyadaId),
                $"seed {seed} put a kravyada in a four-room mine");
        }
    }

    [Test]
    public void DeepMinesEventuallyProduceTheHigherTiers()
    {
        var sawVetala = false;
        var sawKravyada = false;

        for (var seed = 0; seed < 60 && !(sawVetala && sawKravyada); seed++)
        {
            var ids = MineGenerator.Generate(seed, rooms: 20, depth: 3)
                .Spawns.Select(spawn => spawn.ArchetypeId).ToArray();

            sawVetala |= ids.Contains(EnemyCatalog.VetalaId);
            sawKravyada |= ids.Contains(EnemyCatalog.KravyadaId);
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawVetala, Is.True, "no vetala in twenty-room mines across sixty seeds");
            Assert.That(sawKravyada, Is.True, "no kravyada in twenty-room mines across sixty seeds");
        });
    }

    [Test]
    public void ARoomNeverHoldsMoreThanOneOfTheHigherTiers()
    {
        // A room is allowed to be harder. It is not allowed to be a wall of bosses, which is
        // what an uncapped roll would produce at depth sooner or later.
        for (var seed = 0; seed < 40; seed++)
        {
            var mine = MineGenerator.Generate(seed, rooms: 24, depth: 3);

            foreach (var room in mine.Spawns.GroupBy(spawn => spawn.RoomIndex))
            {
                var elites = room.Count(spawn =>
                    spawn.ArchetypeId == EnemyCatalog.VetalaId ||
                    spawn.ArchetypeId == EnemyCatalog.KravyadaId);

                Assert.That(elites, Is.LessThanOrEqualTo(1),
                    $"seed {seed} room {room.Key} held {elites} elites");
            }
        }
    }

    [Test]
    public void EveryGeneratedArchetypeIsOneTheCatalogueKnows()
    {
        for (var seed = 0; seed < 40; seed++)
        foreach (var spawn in MineGenerator.Generate(seed, rooms: 18, depth: 2).Spawns)
            Assert.That(EnemyCatalog.Find(spawn.ArchetypeId), Is.Not.Null,
                $"seed {seed} placed unknown archetype '{spawn.ArchetypeId}'");
    }
}
