using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Save/load smoke coverage.
///
/// These exist because VS1 migrates the save schema from v3 to v4 and splits the
/// generated <c>Main</c> scene apart. Both are large refactors of working behaviour,
/// and until now nothing automated proved that behaviour worked at all.
///
/// Deliberately shallow: the question is "does a round trip preserve state", not
/// "is every field correct".
/// </summary>
public class SaveLoadSmokeTests : SmokeTestFixture
{
    private static readonly Vector3 SavedPosition = new(120f, 30f, -75f);

    [Test]
    public void Save_ThenLoad_RestoresStatsInventoryAndPosition()
    {
        var player = SpawnPlayer();
        var save = SpawnSaveService();
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;

        player.transform.position = SavedPosition;
        stats.Gold = 250;
        stats.Health = 42f;
        stats.RestoreProgress(4, 17);
        inventory.Add("tower_ledger", "Tower Ledger", 1, "loot");

        save.Save();
        Assert.IsTrue(SaveLoadService.HasSaveFile, "Save() did not write a file.");

        // Lose everything, the way a death or a bad fight would.
        stats.Gold = 0;
        stats.Health = 1f;
        stats.RestoreProgress(1, 0);
        inventory.Items.Clear();
        player.transform.position = Vector3.zero;

        save.Load();

        Assert.AreEqual(250, stats.Gold, "Gold did not survive the round trip.");
        Assert.AreEqual(42f, stats.Health, 0.01f, "Health did not survive the round trip.");
        Assert.AreEqual(4, stats.Level, "Level did not survive the round trip.");
        Assert.AreEqual(17, stats.Xp, "Xp did not survive the round trip.");
        Assert.IsNotNull(
            inventory.Items.Find(i => i.Id == "tower_ledger"),
            "Inventory contents were lost across the round trip.");
        Assert.Less(
            Vector3.Distance(player.transform.position, SavedPosition), 0.01f,
            "Player position was not restored. With no terrain present the height " +
            "reconciler should return the saved point unchanged.");
    }

    /// <summary>
    /// The rollback case: state written after a save must not survive loading it.
    /// This is the behaviour the plan calls "save → kill → load rollback".
    /// </summary>
    [Test]
    public void Load_DiscardsChangesMadeAfterTheSave()
    {
        SpawnPlayer();
        var save = SpawnSaveService();
        var stats = PlayerStats.Instance;

        stats.Gold = 100;
        save.Save();

        stats.Gold = 999;
        WorldState.MarkKilled("bandit_camp_0");

        save.Load();

        Assert.AreEqual(100, stats.Gold, "Post-save gold was not rolled back on load.");
        Assert.IsFalse(
            WorldState.IsKilled("bandit_camp_0"),
            "An enemy killed after the save was still marked dead after loading it.");
    }

    /// <summary>
    /// A save from an older schema must be refused outright. Silently applying one
    /// would load stats without the story state that v4 adds.
    /// </summary>
    [Test]
    public void Load_RejectsAnOlderSchema_WithoutTouchingCurrentState()
    {
        SpawnPlayer();
        var save = SpawnSaveService();
        var stats = PlayerStats.Instance;

        stats.Gold = 500;
        stats.Health = 88f;

        File.WriteAllText(
            SaveLoadService.SaveFilePath,
            "{\"Version\":2,\"Gold\":1,\"Health\":1.0,\"Level\":99,\"Xp\":99}");

        LogAssert.Expect(LogType.Warning, new Regex("Ignoring save from version 2"));
        save.Load();

        Assert.AreEqual(500, stats.Gold, "A v2 save was applied over current stats.");
        Assert.AreEqual(88f, stats.Health, 0.01f, "A v2 save overwrote current health.");
        Assert.AreEqual(1, stats.Level, "A v2 save overwrote the current level.");
    }

    /// <summary>
    /// Chapter 01's scenes were renamed to setting-neutral identities on 2026-08-12. A save
    /// stores the Unity scene *name*, so without a migration every mid-chapter save would come
    /// back pointing at a scene that no longer exists and silently fail to restore its location.
    ///
    /// The mapping is exactly known, which is why this one is migrated rather than orphaned the
    /// way the v2 and v3 bumps deliberately were.
    /// </summary>
    [Test]
    public void Load_MigratesSceneNamesRenamedByTheSettingCleanup()
    {
        File.WriteAllText(
            SaveLoadService.SaveFilePath,
            "{\"Version\":4,\"SceneId\":\"Estmere_Palace\",\"SpawnId\":\"spawn.entry\"}");

        Assert.IsTrue(SaveLoadService.TryReadSave(out var data, out var error),
            $"A v4 save with a pre-rename scene name failed to read: {error}");
        Assert.AreEqual("Palace", data.SceneId,
            "An old scene name survived the read, so the save points at a scene that is gone.");
    }

    [Test]
    public void Load_LeavesUnrecognisedSceneNamesAlone()
    {
        File.WriteAllText(
            SaveLoadService.SaveFilePath,
            "{\"Version\":4,\"SceneId\":\"Main\",\"SpawnId\":\"spawn.entry\"}");

        Assert.IsTrue(SaveLoadService.TryReadSave(out var data, out _));
        Assert.AreEqual("Main", data.SceneId,
            "The migration rewrote a scene name it should not have touched.");
    }

    /// <summary>
    /// Continue is offered on file existence alone, so loading unreadable content has
    /// to fail safely rather than throw into gameplay.
    /// </summary>
    [Test]
    public void Load_HandlesACorruptSaveWithoutThrowing()
    {
        SpawnPlayer();
        var save = SpawnSaveService();
        var stats = PlayerStats.Instance;
        stats.Gold = 77;

        File.WriteAllText(SaveLoadService.SaveFilePath, "{ this is not json");

        // Load() catches the parse failure and logs it. The framework fails a test on
        // any unexpected LogError, so the expected one is declared rather than silenced.
        LogAssert.Expect(LogType.Error, new Regex("Could not read save file"));

        Assert.DoesNotThrow(() => save.Load(), "A corrupt save threw instead of failing safely.");
        Assert.AreEqual(77, stats.Gold, "A corrupt save modified player state.");
    }

    [Test]
    public void SaveV4_RoundTripsStoryProfileEvidenceCompanionAndMutations()
    {
        SpawnPlayer();
        var systems = Track(new GameObject("StorySystems_Test"));
        var story = systems.AddComponent<StoryDirector>();
        var dialogue = systems.AddComponent<TopicDialogueService>();
        var save = systems.AddComponent<SaveLoadService>();
        story.SetProfile(new CharacterProfile { Name = "Nara", AncestryId = "anc.isleborn", Pronouns = "she" });
        story.SelectRoute("route.trade");
        story.AdvanceTo("chapter.01", "stage.prison", "B630");
        story.AddEvidence(new EvidenceRecord
        {
            Id = "ev.tower_ledger", Title = "Tower Ledger",
            DocumentBody = "Shipment 14: source column amended from mine to prisoner.", Inspected = true
        });
        story.SetCompanion("role.prince", true, "Prison", "spawn.escape", 72f);
        story.MarkOpened("lock.prison_gate");
        story.MarkLooted("loot.warden_key");
        story.RecordChoice("choice.king", "imprison");
        story.AddChanneled(18f);
        dialogue.LearnTopic("black jiva");
        save.Save();

        story.Restore(new StorySnapshot());
        dialogue.RestoreKnownTopics(null);
        save.Load();

        Assert.AreEqual("Nara", story.State.Profile.Name);
        Assert.AreEqual("route.trade", story.State.RouteId);
        Assert.AreEqual("B630", story.State.BeatId);
        Assert.IsTrue(story.State.Evidence.Exists(e => e.Id == "ev.tower_ledger" && e.Inspected
            && e.DocumentBody.Contains("prisoner")));
        Assert.IsTrue(story.State.Companion.Following);
        Assert.Contains("lock.prison_gate", story.State.OpenedLocks);
        Assert.Contains("loot.warden_key", story.State.LootedObjects);
        Assert.AreEqual(18f, story.State.PlayerChanneled, 0.01f);
        Assert.IsTrue(dialogue.KnowsTopic("black jiva"),
            "Learned dialogue subjects were forgotten after loading the save.");
    }

    [Test]
    public void SaveV4_MigratesV3ExplorationWithoutInventingStoryProgress()
    {
        File.WriteAllText(SaveLoadService.SaveFilePath,
            "{\"Version\":3,\"Gold\":12,\"Discovered\":[\"city_west\"]}");
        Assert.IsTrue(SaveLoadService.TryReadSave(out var data, out var error), error);
        Assert.AreEqual(SaveLoadService.CurrentVersion, data.Version);
        Assert.AreEqual(12, data.Gold);
        Assert.Contains("city_west", data.Discovered);
        Assert.AreEqual("B010", data.Story.BeatId);
        Assert.IsFalse(data.Story.Profile.IsValid);
    }

    [Test]
    public void SaveV4_AtomicRewritePreservesPreviousSlotAsBackup()
    {
        SpawnPlayer();
        var save = SpawnSaveService();
        PlayerStats.Instance.Gold = 10;
        save.Save();
        PlayerStats.Instance.Gold = 20;
        save.Save();
        Assert.IsTrue(File.Exists(SaveLoadService.SaveFilePath + ".bak"));
        var backup = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveLoadService.SaveFilePath + ".bak"));
        Assert.AreEqual(10, backup.Gold);
    }
}
