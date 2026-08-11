using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The objective layer — written directions rather than a marker.
///
/// GAMEPLAY_DESIGN.md locks navigation as directions-first. The bearing line is generated from
/// the player's live position rather than authored per approach, which is precisely the thing
/// that stops Morrowind-style questing from needing a wiki: a direction that recomputes cannot
/// be wrong about where you are standing.
/// </summary>
public class ObjectiveSmokeTests : SmokeTestFixture
{
    private ObjectiveService SpawnObjectives()
    {
        var go = Track(new GameObject("Objectives_Test"));
        return go.AddComponent<ObjectiveService>();
    }

    [Test]
    public void SettingAnObjective_ExposesItsTargetPosition()
    {
        var objectives = SpawnObjectives();
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");

        Assert.IsTrue(objectives.HasObjective);
        Assert.IsNotNull(objectives.TargetPosition, "The objective names an anchor but resolves nowhere.");
        Assert.AreEqual(
            EstmereRegion.FindAnchor("anchor.palace").Value.Position,
            objectives.TargetPosition.Value);
    }

    [Test]
    public void AnObjectiveWithNoPlace_HasNoTarget()
    {
        var objectives = SpawnObjectives();
        objectives.Set("Speak to the guard", "He is standing right there.");

        Assert.IsTrue(objectives.HasObjective);
        Assert.IsNull(objectives.TargetPosition, "A placeless objective invented a position.");
        Assert.IsEmpty(objectives.BearingLine(), "A placeless objective produced a bearing.");
    }

    [Test]
    public void TheBearingNamesTheDirectionThePlayerMustActuallyGo()
    {
        var player = SpawnPlayer();
        var objectives = SpawnObjectives();
        var palace = EstmereRegion.FindAnchor("anchor.palace").Value;
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");

        // Stand due south of the palace; it must read as north.
        player.transform.position = palace.Position + new Vector3(0f, 0f, -400f);
        StringAssert.Contains("north", objectives.BearingLine());

        // And due west of it, reading as east.
        player.transform.position = palace.Position + new Vector3(-400f, 0f, 0f);
        StringAssert.Contains("east", objectives.BearingLine());
    }

    [Test]
    public void TheBearingReportsDistanceInPaces_NotMetres()
    {
        var player = SpawnPlayer();
        var objectives = SpawnObjectives();
        var palace = EstmereRegion.FindAnchor("anchor.palace").Value;
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");
        player.transform.position = palace.Position + new Vector3(0f, 0f, -300f);

        StringAssert.Contains("paces", objectives.BearingLine(),
            "Directions should be in the register a person would use, not engine units.");
    }

    [Test]
    public void StandingOnTheObjective_ReadsAsArrived()
    {
        var player = SpawnPlayer();
        var objectives = SpawnObjectives();
        var palace = EstmereRegion.FindAnchor("anchor.palace").Value;
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");

        player.transform.position = palace.Position + new Vector3(0f, 0f, -500f);
        Assert.IsFalse(objectives.PlayerHasArrived(), "Arrived while still half a kilometre away.");

        player.transform.position = palace.Position;
        Assert.IsTrue(objectives.PlayerHasArrived(), "Standing on the objective did not read as arrival.");
        StringAssert.Contains("here", objectives.BearingLine());
    }

    [Test]
    public void ClearingAnObjective_LeavesNothingBehind()
    {
        var objectives = SpawnObjectives();
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");
        objectives.Clear();

        Assert.IsFalse(objectives.HasObjective);
        Assert.IsNull(objectives.TargetPosition);
        Assert.IsEmpty(objectives.BearingLine());
    }

    /// <summary>
    /// Height must not count toward the bearing. A target one floor up is not "500 paces away"
    /// just because the player is standing on a wall.
    /// </summary>
    [Test]
    public void HeightIsIgnoredWhenMeasuringDistance()
    {
        var player = SpawnPlayer();
        var objectives = SpawnObjectives();
        var palace = EstmereRegion.FindAnchor("anchor.palace").Value;
        objectives.Set("Go to the Palace", "Inside the walls.", "anchor.palace");

        player.transform.position = palace.Position + Vector3.up * 200f;

        Assert.IsTrue(objectives.PlayerHasArrived(),
            "Standing directly above the objective did not count as arriving.");
    }
}
