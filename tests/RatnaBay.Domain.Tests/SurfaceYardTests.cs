using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The yard, as a thing that can be looked at rather than only walked through.
///
/// It was reported as having "weird and hollow places for the well and shops", and both halves
/// of that were true for reasons no existing test could see. Everything was drawn as coursed
/// blockwork tinted toward its authored colour, so timber, cloth and packed earth all came out
/// as sandy brick; and the mouth of the mine was covered by the ground it was supposed to be a
/// hole in, then covered again by the path laid over the top of that.
///
/// These check the facts a screenshot would show, so the camp cannot quietly go back to being
/// a set of brick lumps.
/// </summary>
[TestFixture]
public sealed class SurfaceYardTests
{
    private static WorldManifest Yard() => Surface.Build();

    /// <summary>Every solid that could be standing in the mouth of the shaft.</summary>
    private static WorldGeometry[] OverTheShaft(WorldManifest yard) => yard.Geometry
        .Where(g => g.Min.X < 1.5f && g.Max.X > -1.5f
            && g.Min.Z < -7.5f && g.Max.Z > -10.5f
            && g.Max.Y > -0.25f && g.Min.Y < 0.2f)
        .ToArray();

    [Test]
    public void TheShaftIsAHoleRatherThanAPatchOfFloor()
    {
        var covering = OverTheShaft(Yard()).Where(g => g.Visible).ToArray();

        Assert.That(covering, Is.Empty,
            "something visible is drawn across the mouth of the shaft: "
            + string.Join(", ", covering.Select(g => g.Id)));
    }

    [Test]
    public void ButYouStillCannotWalkIntoIt()
    {
        // Looking down a hole is the point; falling down it by leaning is not, because the
        // yard is the one place a run cannot be lost.
        var lid = OverTheShaft(Yard()).Where(g => g.Solid).ToArray();

        Assert.That(lid, Is.Not.Empty, "the shaft mouth must still be closed to walking");
        Assert.That(lid.All(g => !g.Visible), Is.True, "and whatever closes it must not be drawn");
    }

    [Test]
    public void ThePathStopsShortOfTheShaft()
    {
        var path = Yard().Geometry.Single(g => g.Id == "surface.path");

        Assert.That(path.Min.Z, Is.GreaterThan(-7.2f),
            "the path used to run the length of the yard and straight over the shaft");
    }

    [Test]
    public void TheShaftHasSidesGoingDown()
    {
        var yard = Yard();
        var lining = yard.Geometry.Where(g => g.Id.StartsWith("surface.shaft.line.")).ToArray();

        Assert.That(lining, Has.Length.EqualTo(4), "a shaft needs four sides");
        Assert.That(lining.Min(g => g.Min.Y), Is.LessThan(-6f),
            "and they have to go far enough down to read as depth rather than as a dark tile");
    }

    /// <summary>
    /// The fix that mattered most: colour could not say what a thing was made of, so the
    /// stall's counter, its awning and the ground were all drawn as the same brick.
    /// </summary>
    [Test]
    public void TheYardIsBuiltOfMoreThanOneMaterial()
    {
        var yard = Yard();

        var materials = yard.Geometry
            .Select(g => g.Material)
            .Distinct()
            .ToArray();

        Assert.That(materials, Does.Contain(WorldMaterials.Timber), "the stall and the windlass");
        Assert.That(materials, Does.Contain(WorldMaterials.Cloth), "the awning");
        Assert.That(materials, Does.Contain(WorldMaterials.Earth), "the ground");
    }

    [Test]
    public void TheStallIsTimberAndItsAwningIsCloth()
    {
        var yard = Yard();

        var counter = yard.Geometry.Where(g => g.Id.StartsWith("surface.stall.")
            && !g.Id.Contains("awning") && !g.Id.Contains("valance")).ToArray();
        var awning = yard.Geometry.Where(g => g.Id.Contains("awning")).ToArray();

        Assert.That(counter, Is.Not.Empty);
        Assert.That(counter.All(g => g.Material == WorldMaterials.Timber), Is.True,
            "a market stall is made of wood");

        Assert.That(awning, Is.Not.Empty);
        Assert.That(awning.All(g => g.Material == WorldMaterials.Cloth), Is.True,
            "and its awning is not");
    }

    /// <summary>Cloth and stock are there to be seen, not to be bumped into.</summary>
    [Test]
    public void TheAwningDoesNotBlockTheWay()
    {
        var awning = Yard().Geometry.Where(g => g.Id.Contains("awning") || g.Id.Contains("valance"));

        Assert.That(awning.All(g => !g.Solid), Is.True);
    }

    /// <summary>Anything without a material must still load and draw, as stone.</summary>
    [Test]
    public void GeometryDefaultsToStone()
    {
        Assert.That(new WorldGeometry().Material, Is.EqualTo(WorldMaterials.Stone));
    }
}
