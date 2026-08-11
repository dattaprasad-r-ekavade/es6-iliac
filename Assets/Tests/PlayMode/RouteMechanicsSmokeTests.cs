using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The Commerce/Thief toolkit: detection, locks and pickpocketing.
///
/// VS4's gate requires every mechanic to survive save/load and to be unable to strand the
/// player, and B410 requires being caught to be recoverable and never terminal. Those are
/// the properties these tests protect — not whether stealth is fun, which no test can say.
/// </summary>
public class RouteMechanicsSmokeTests : SmokeTestFixture
{
    private DetectionSystem SpawnDetection()
    {
        var go = Track(new GameObject("Detection_Test"));
        return go.AddComponent<DetectionSystem>();
    }

    private DetectionWatcher SpawnWatcher(Vector3 position, Vector3 facing)
    {
        var go = Track(new GameObject("Watcher_Test"));
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(facing);
        return go.AddComponent<DetectionWatcher>();
    }

    // --- detection -----------------------------------------------------------

    [Test]
    public void Crouching_MakesThePlayerHarderToSee()
    {
        var detection = SpawnDetection();
        SpawnPlayer();

        detection.SetCrouching(false);
        float standing = detection.Visibility;
        detection.SetCrouching(true);

        Assert.Less(detection.Visibility, standing, "Crouching did not reduce visibility.");
    }

    [Test]
    public void StealthSkill_ReducesVisibility_ButNeverToInvisible()
    {
        var player = SpawnPlayer();
        var skills = player.AddComponent<SkillSystem>();
        var detection = SpawnDetection();

        float novice = detection.Visibility;
        skills.GrantRouteSkills("route.trade", SkillSystem.MaxSkill);
        float master = detection.Visibility;

        Assert.Less(master, novice, "Stealth skill did not improve concealment.");
        Assert.Greater(master, 0f, "A master became literally invisible — there must be a floor.");
    }

    [Test]
    public void AWatcher_SeesThePlayerInFront_AndNotBehind()
    {
        SpawnDetection();
        var player = SpawnPlayer();
        player.transform.position = new Vector3(0f, 0f, 5f);

        var watcher = SpawnWatcher(Vector3.zero, Vector3.forward);
        Assert.IsTrue(watcher.CanSeePlayer(1f), "A watcher failed to see a player directly ahead.");

        player.transform.position = new Vector3(0f, 0f, -5f);
        Assert.IsFalse(watcher.CanSeePlayer(1f), "A watcher saw a player behind its own back.");
    }

    [UnityTest]
    public IEnumerator Suspicion_RisesWhenSeen_AndDecaysWhenNot()
    {
        var detection = SpawnDetection();
        var player = SpawnPlayer();
        player.transform.position = new Vector3(0f, 0f, 4f);
        var watcher = SpawnWatcher(Vector3.zero, Vector3.forward);
        detection.Register(watcher);

        for (int i = 0; i < 5; i++) yield return null;
        float seen = detection.Suspicion;
        Assert.Greater(seen, 0f, "Standing in plain sight raised no suspicion.");

        // Break line of sight — the escape must always exist.
        player.transform.position = new Vector3(0f, 0f, -40f);
        for (int i = 0; i < 5; i++) yield return null;

        Assert.Less(detection.Suspicion, seen, "Suspicion did not decay after breaking sight.");
    }

    /// <summary>Nothing may leave the player permanently detected.</summary>
    [UnityTest]
    public IEnumerator Alert_AlwaysDecaysBackToUnaware()
    {
        var detection = SpawnDetection();
        SpawnPlayer();
        detection.AddSuspicion(1f);
        Assert.AreEqual(AwarenessLevel.Alerted, detection.Awareness);

        float deadline = Time.realtimeSinceStartup + 10f;
        while (detection.Awareness != AwarenessLevel.Unaware && Time.realtimeSinceStartup < deadline)
            yield return null;

        Assert.AreEqual(
            AwarenessLevel.Unaware, detection.Awareness,
            "The player stayed detected forever. Being caught must be recoverable.");
    }

    // --- locks ---------------------------------------------------------------

    [Test]
    public void ALockBeyondYourSkill_FailsButStaysRetryable()
    {
        var player = SpawnPlayer();
        player.AddComponent<SkillSystem>();
        var door = Track(new GameObject("Door_Test")).AddComponent<DoorAndLock>();
        door.Configure(true, 60f);

        Assert.AreEqual(LockResult.Failed, door.TryPick(), "A hard lock did not refuse.");
        Assert.IsTrue(door.IsLocked, "A failed pick opened the door anyway.");
        Assert.AreEqual(LockResult.Failed, door.TryPick(), "A failed pick was not retryable.");
    }

    [Test]
    public void EnoughSecurity_OpensTheLock_AndTrainsTheSkill()
    {
        var player = SpawnPlayer();
        var skills = player.AddComponent<SkillSystem>();
        skills.GrantRouteSkills("route.trade", 40f);
        var door = Track(new GameObject("Door_Test")).AddComponent<DoorAndLock>();
        door.Configure(true, 20f);
        float before = skills.LevelOf(Skills.Security);

        Assert.AreEqual(LockResult.Opened, door.TryPick());
        Assert.IsFalse(door.IsLocked);
        Assert.Greater(skills.LevelOf(Skills.Security), before, "Picking a lock did not train Security.");
    }

    [Test]
    public void AKeyOpensTheLock_WithoutAnySkill()
    {
        SpawnPlayer();
        var door = Track(new GameObject("Door_Test")).AddComponent<DoorAndLock>();
        door.Configure(true, 99f, "prison_key");
        PlayerInventory.Instance.Add("prison_key", "Prison Key", 1, "key");

        Assert.AreEqual(LockResult.Unlocked, door.TryOpen(), "A held key did not open the door.");
        Assert.IsFalse(door.IsLocked);
    }

    // --- pickpocketing -------------------------------------------------------

    [Test]
    public void PickpocketingBeyondYourSkill_TakesNothing_AndIsRetryable()
    {
        var player = SpawnPlayer();
        player.AddComponent<SkillSystem>();
        var target = Track(new GameObject("Mark_Test")).AddComponent<PickpocketTarget>();
        target.Configure(70f, new PickpocketTarget.Holding { Id = "ev.tower_ledger", Name = "Tower Ledger" });

        Assert.AreEqual(PickpocketResult.TooDifficult, PickpocketSystem.TryTake(target));
        Assert.AreEqual(1, target.RemainingItems, "A failed attempt still consumed the item.");
        Assert.AreEqual(0, PlayerInventory.Instance.CountOf("ev.tower_ledger"));
    }

    [Test]
    public void ASuccessfulLift_MovesTheItemAndTrainsSecurity()
    {
        var player = SpawnPlayer();
        var skills = player.AddComponent<SkillSystem>();
        skills.GrantRouteSkills("route.trade", 40f);
        var target = Track(new GameObject("Mark_Test")).AddComponent<PickpocketTarget>();
        target.Configure(10f, new PickpocketTarget.Holding { Id = "ev.tower_ledger", Name = "Tower Ledger" });
        float before = skills.LevelOf(Skills.Security);

        var result = PickpocketSystem.TryTake(target);

        Assert.AreNotEqual(PickpocketResult.TooDifficult, result);
        Assert.AreEqual(1, PlayerInventory.Instance.CountOf("ev.tower_ledger"), "The lifted item never arrived.");
        Assert.AreEqual(0, target.RemainingItems);
        Assert.Greater(skills.LevelOf(Skills.Security), before);
    }

    /// <summary>
    /// Being caught costs standing, not the goods. Confiscating the item would make the
    /// tutorial unwinnable for a player who is seen once.
    /// </summary>
    [Test]
    public void BeingCaught_CostsSuspicion_NotTheItem()
    {
        var player = SpawnPlayer();
        var skills = player.AddComponent<SkillSystem>();
        skills.GrantRouteSkills("route.trade", 40f);
        var detection = SpawnDetection();
        detection.AddSuspicion(0.4f);
        float suspicionBefore = detection.Suspicion;

        var target = Track(new GameObject("Mark_Test")).AddComponent<PickpocketTarget>();
        target.Configure(10f, new PickpocketTarget.Holding { Id = "coin_purse", Name = "Coin Purse" });

        var result = PickpocketSystem.TryTake(target);

        Assert.AreEqual(PickpocketResult.Caught, result, "A theft in plain view went unnoticed.");
        Assert.AreEqual(1, PlayerInventory.Instance.CountOf("coin_purse"), "Being caught confiscated the item.");
        Assert.Greater(detection.Suspicion, suspicionBefore, "Being caught cost no standing.");
    }

    [Test]
    public void AnUnwitnessedTheft_RaisesNoSuspicion()
    {
        var player = SpawnPlayer();
        var skills = player.AddComponent<SkillSystem>();
        skills.GrantRouteSkills("route.trade", 40f);
        var detection = SpawnDetection();

        var target = Track(new GameObject("Mark_Test")).AddComponent<PickpocketTarget>();
        target.Configure(10f, new PickpocketTarget.Holding { Id = "coin_purse", Name = "Coin Purse" });

        var result = PickpocketSystem.TryTake(target);

        Assert.AreEqual(PickpocketResult.Taken, result);
        Assert.AreEqual(0f, detection.Suspicion, 0.001f,
            "An unobserved theft raised suspicion. That is the whole point of stealth.");
    }
}
