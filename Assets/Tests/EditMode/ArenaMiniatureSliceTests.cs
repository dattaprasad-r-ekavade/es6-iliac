using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Locks the reusable geometry contract behind the first Arena Miniature art slice. Visual
/// screenshots remain the approval gate; these assertions catch the less visible regressions
/// that make a pretty capture unplayable or impossible to reproduce.
/// </summary>
public sealed class ArenaMiniatureSliceTests
{
    [Test]
    public void ExteriorModulesHaveStableUniqueIds()
    {
        var ids = ArenaMiniatureSliceLayout.StreetFacades.Select(f => f.Id)
            .Concat(ArenaMiniatureSliceLayout.StreetProps.Select(p => p.Id))
            .Concat(ArenaMiniatureSliceLayout.StreetFigures.Select(f => f.Id))
            .ToArray();

        Assert.IsTrue(ids.All(id => !string.IsNullOrWhiteSpace(id)));
        CollectionAssert.AllItemsAreUnique(ids,
            "Two street modules share an id, so an editor round-trip could overwrite one.");
    }

    [Test]
    public void FacadesLeaveTheAuthoredStreetLaneClear()
    {
        foreach (var facade in ArenaMiniatureSliceLayout.StreetFacades)
        {
            float inwardEdge = Mathf.Abs(facade.LocalPosition.x) - facade.Size.z * 0.5f;
            Assert.Greater(inwardEdge, ArenaMiniatureSliceLayout.StreetClearHalfWidth,
                $"{facade.Id} intrudes into the clear walking lane.");
            Assert.LessOrEqual(
                Mathf.Abs(facade.LocalPosition.z) + facade.Size.x * 0.5f,
                ArenaMiniatureSliceLayout.StreetLength * 0.5f,
                $"{facade.Id} extends beyond the painted street register.");
        }
    }

    [Test]
    public void EveryFacadeIsInsideTheProceduralBlockReservation()
    {
        foreach (var facade in ArenaMiniatureSliceLayout.StreetFacades)
        {
            Assert.IsTrue(
                ArenaMiniatureSliceLayout.OverlapsStreetReservation(
                    ArenaMiniatureSliceLayout.ToWorld(facade), 0f),
                $"{facade.Id} sits outside the reservation; a generated city block may overlap it.");
        }
    }

    [Test]
    public void StreetPropsDoNotBlockTheCriticalWalkingLane()
    {
        foreach (var prop in ArenaMiniatureSliceLayout.StreetProps)
        {
            // The civic arch's record is centred over the lane, but its only solid pieces are
            // two piers at x=±11.5 m; its lintel is overhead and collider-free.
            if (prop.Kind == ArenaMiniatureSliceLayout.StreetPropKind.CivicArch) continue;
            Assert.IsFalse(
                ArenaMiniatureSliceLayout.IsInsideStreetWalkingLane(
                    ArenaMiniatureSliceLayout.ToWorld(prop)),
                $"{prop.Id} sits in the 16 m critical walking lane.");
        }

        foreach (var figure in ArenaMiniatureSliceLayout.StreetFigures)
            Assert.IsFalse(
                ArenaMiniatureSliceLayout.IsInsideStreetWalkingLane(
                    ArenaMiniatureSliceLayout.ToWorld(figure)),
                $"{figure.Id} stands in the 16 m critical walking lane.");
    }

    [Test]
    public void DungeonModulesHaveStableUniqueIdsAndFitTheirRooms()
    {
        var ids = ArenaMiniatureSliceLayout.DungeonModules.Select(m => m.Id).ToArray();
        CollectionAssert.AllItemsAreUnique(ids);

        foreach (var module in ArenaMiniatureSliceLayout.DungeonModules)
        {
            Assert.That(module.RoomIndex, Is.InRange(0, 3),
                $"{module.Id} targets a room the prison slice does not have.");
            Assert.Less(Mathf.Abs(module.LocalOffset.z),
                ArenaMiniatureSliceLayout.DungeonRoomDepth * 0.5f,
                $"{module.Id} is beyond its chamber wall.");
        }
    }

    [Test]
    public void DeepestRoomPositionIsInsideTheSealedFarWall()
    {
        float deepest = ArenaMiniatureSliceLayout.DeepestDungeonRoomCentreZ(4);
        float farWall = deepest + ArenaMiniatureSliceLayout.DungeonRoomDepth * 0.5f;

        Assert.AreEqual(70f, deepest, 0.01f);
        Assert.AreEqual(82f, farWall, 0.01f);

        // A route mechanic sits six metres forward of the centre. The old formula put that
        // mechanic at z=100, eighteen metres beyond this wall.
        Assert.Less(deepest + 6f, farWall);
    }

    [Test]
    public void EditorMaterializerCreatesOneSimpleColliderPerFacadeBody()
    {
        var parent = new GameObject("ArenaSlice_TestRoot");
        try
        {
            ArenaMiniatureSliceBuilder.BuildCapitalStreet(parent.transform);
            var slice = parent.transform.Find(ArenaMiniatureSliceLayout.StreetRootName);
            Assert.IsNotNull(slice);

            foreach (var facade in ArenaMiniatureSliceLayout.StreetFacades)
            {
                var holder = slice.Find(facade.Id);
                Assert.IsNotNull(holder, facade.Id);
                // Solid colliders only. A facade may also carry trigger volumes — the two
                // street doorways are triggers — and a trigger cannot snag the player, which
                // is the thing this is actually guarding against.
                int solid = 0;
                foreach (var collider in holder.GetComponentsInChildren<Collider>(true))
                    if (!collider.isTrigger) solid++;
                Assert.AreEqual(1, solid,
                    $"{facade.Id} should have one body collider, not compound decorative collision.");
            }

            Assert.IsNull(slice.Find("Road_PaintedRegister").GetComponent<Collider>(),
                "The painted road duplicates the region ground collider and can snag the player.");
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }
}
