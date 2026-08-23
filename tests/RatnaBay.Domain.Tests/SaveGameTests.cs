using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// A save that half-loads is worse than one that refuses to load, so version handling is
/// asserted as hard as the round trip itself.
/// </summary>
public class SaveGameTests
{
    private PlayerCharacter _player = null!;

    private static readonly QuestDefinition BanditHunt = new()
    {
        Id = "quest.bandits", Title = "Clear the Crossroads",
        InitialStageText = "Slay bandits", TargetCount = 3, TargetEnemy = "Bandit"
    };

    [SetUp]
    public void Setup()
    {
        _player = PlayerCharacter.NewGame();
        _player.Quests.Add(BanditHunt);
    }

    /// <summary>A character with something interesting in every system.</summary>
    private PlayerCharacter Played()
    {
        _player.Story.SetProfile(new CharacterProfile
        {
            Name = "Aruna", AncestryId = "ancestry.coastal", Pronouns = "they/them"
        });
        _player.Story.SelectRoute(StoryDirector.RouteTrade);
        _player.Story.AdvanceTo("chapter.01", "stage.city", "B410");
        _player.Skills.GrantRouteSkills(StoryDirector.RouteTrade);

        _player.Inventory.Add("mail_hauberk", "Mail Hauberk", 1, "armour");
        _player.Equipment.Equip("mail_hauberk");

        _player.Vitals.AddXp(120);
        _player.Vitals.AddGold(64);
        _player.Vitals.TakeDamage(23f);

        _player.Quests.NotifyEnemyKilled("Bandit");
        _player.World.MarkKilled("bandit.crossroads.01");
        _player.Dialogue.LearnTopic("ratnapur");

        return _player;
    }

    private static PlayerCharacter Reload(SaveData data)
    {
        var loaded = PlayerCharacter.NewGame();
        loaded.Quests.Add(BanditHunt);
        SaveGame.Restore(loaded, data);
        return loaded;
    }

    [Test]
    public void ASaveRoundTripsThroughJson()
    {
        var data = SaveGame.Capture(Played(), new WorldPoint(12f, 0f, -30f), yaw: 1.2f,
            sceneId: "scene.ratnapur", spawnId: "spawn.gate");

        var json = SaveGame.Serialize(data);
        Assert.That(SaveGame.TryRead(json, out var read, out var error), Is.True, error);

        var loaded = Reload(read!);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Vitals.Level, Is.EqualTo(_player.Vitals.Level));
            Assert.That(loaded.Vitals.Gold, Is.EqualTo(_player.Vitals.Gold));
            Assert.That(loaded.Vitals.Health, Is.EqualTo(_player.Vitals.Health).Within(0.01f));
            Assert.That(loaded.Equipment.WeaponId, Is.EqualTo(_player.Equipment.WeaponId));
            Assert.That(loaded.Equipment.ArmourId, Is.EqualTo("mail_hauberk"));
            Assert.That(loaded.Skills.LevelOf(Skills.Security),
                Is.EqualTo(_player.Skills.LevelOf(Skills.Security)).Within(0.001f));
            Assert.That(loaded.Inventory.CountOf("iron_sword"), Is.EqualTo(1));
            Assert.That(loaded.Quests.Find("quest.bandits")!.Progress, Is.EqualTo(1));
            Assert.That(loaded.World.IsKilled("bandit.crossroads.01"), Is.True);
            Assert.That(loaded.Dialogue.KnowsTopic("ratnapur"), Is.True);
            Assert.That(loaded.Story.State.RouteId, Is.EqualTo(StoryDirector.RouteTrade));
            Assert.That(loaded.Story.State.BeatId, Is.EqualTo("B410"));
            Assert.That(loaded.Story.State.Profile.Name, Is.EqualTo("Aruna"));
        });
    }

    [Test]
    public void AnOpenedLockSurvivesSaveAndReload()
    {
        _player.Story.MarkOpened("northwatch.entry.door");

        var loaded = Reload(SaveGame.Capture(_player, default));

        Assert.That(loaded.Story.State.OpenedLocks,
            Is.EqualTo(new[] { "northwatch.entry.door" }));
    }

    [Test]
    public void TheCurrentObjectiveSurvivesButItsBearingIsRegenerated()
    {
        _player.Objective.Set("Reach the old watch road", "Follow the lanterns.",
            "anchor.watchroad", new WorldPoint(0f, 0f, 120f));

        var loaded = Reload(SaveGame.Capture(_player, new WorldPoint(0f, 0f, 0f)));

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Objective.Title, Is.EqualTo("Reach the old watch road"));
            Assert.That(loaded.Objective.TargetAnchorId, Is.EqualTo("anchor.watchroad"));
            Assert.That(loaded.Objective.BearingLine(new WorldPoint(0f, 0f, 0f)),
                Does.StartWith("north"));
            Assert.That(loaded.Objective.BearingLine(new WorldPoint(0f, 0f, 300f)),
                Does.StartWith("south"),
                "the bearing is generated from where the player is, not stored");
        });
    }

    [Test]
    public void ASaveWithNoObjectiveClearsWhateverWasShowing()
    {
        var data = SaveGame.Capture(_player, default);

        var loaded = PlayerCharacter.NewGame();
        loaded.Objective.Set("Stale objective", "From before the load.");
        SaveGame.Restore(loaded, data);

        Assert.That(loaded.Objective.HasObjective, Is.False);
    }

    [Test]
    public void ThePlayerPositionSurvives()
    {
        var data = SaveGame.Capture(Played(), new WorldPoint(12f, 3f, -30f), yaw: 1.2f);
        var json = SaveGame.Serialize(data);
        SaveGame.TryRead(json, out var read, out _);

        Assert.Multiple(() =>
        {
            Assert.That(read!.PlayerX, Is.EqualTo(12f));
            Assert.That(read.PlayerZ, Is.EqualTo(-30f));
            Assert.That(read.PlayerYaw, Is.EqualTo(1.2f).Within(0.001f));
        });
    }

    [Test]
    public void ReloadingDoesNotDuplicateTheStartingKit()
    {
        var data = SaveGame.Capture(Played(), default);
        var loaded = Reload(data);

        Assert.That(loaded.Inventory.CountOf("health_potion"), Is.EqualTo(3),
            "restore must replace the inventory, not merge into a fresh kit");
    }

    [Test]
    public void ReloadingDoesNotHandOutAFreeCharacterLevel()
    {
        foreach (var id in Skills.All)
            for (var i = 0; i < 400; i++)
            {
                _player.Skills.ReportUse(id, 50f, 500f);
                _player.Skills.EndEncounter();
            }

        var data = SaveGame.Capture(_player, default);

        var loaded = PlayerCharacter.NewGame();
        var levels = 0;
        loaded.Vitals.LevelGained += _ => levels++;
        SaveGame.Restore(loaded, data);

        Assert.That(levels, Is.Zero);
    }

    [Test]
    public void AnEmptyFileIsRefusedWithAReason()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead("", out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void CorruptJsonIsRefusedRatherThanThrowing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead("{ this is not json", out var data, out var error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void ReloadingACompletedQuestRepairsItsDialogueFlag()
    {
        for (var i = 0; i < 3; i++) _player.Quests.NotifyEnemyKilled("Bandit");
        var data = SaveGame.Capture(_player, default);
        data.Story.Flags.Remove(PlayerCharacter.QuestCompletedFlag("quest.bandits"));

        var loaded = Reload(data);

        Assert.That(loaded.Story.HasFlag(
            PlayerCharacter.QuestCompletedFlag("quest.bandits")), Is.True);
    }

    [Test]
    public void ASaveMissingItsVersionIsRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead("{}", out var data, out var error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void ASaveWithNullRequiredDataIsRejectedRatherThanCrashingRestore()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead("{\"Version\":4,\"Vitals\":null}", out var data,
                out var error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error, Is.Not.Empty);
        });
    }

    [Test]
    public void ASaveFromAnOlderBuildIsRejectedLoudly()
    {
        var json = SaveGame.Serialize(new SaveData { Version = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead(json, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("2"),
                "the bump is what makes a hand-copied file fail loudly instead of quietly");
        });
    }

    [Test]
    public void ASaveFromANewerBuildIsRejectedRatherThanGuessed()
    {
        var json = SaveGame.Serialize(new SaveData { Version = SaveGame.CurrentVersion + 1 });
        Assert.That(SaveGame.TryRead(json, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("newer"));
    }

    [Test]
    public void AMigratableSaveIsBroughtForward()
    {
        var json = SaveGame.Serialize(new SaveData
        {
            Version = SaveGame.MinimumMigratableVersion,
            Discovered = { "a_place_that_no_longer_resolves" }
        });

        Assert.Multiple(() =>
        {
            Assert.That(SaveGame.TryRead(json, out var data, out var error), Is.True, error);
            Assert.That(data!.Version, Is.EqualTo(SaveGame.CurrentVersion));
            Assert.That(data.Discovered, Is.Empty, "v3 location ids no longer resolve");
        });
    }

    [Test]
    public void ASaveNamingGearFromAFuturePatchDegradesToUnarmed()
    {
        var data = SaveGame.Capture(Played(), default);
        data.WeaponId = "sword_of_a_later_build";
        data.ArmourId = "plate_of_a_later_build";

        var loaded = Reload(data);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Equipment.WeaponId, Is.EqualTo(EquipmentCatalog.UnarmedId));
            Assert.That(loaded.Equipment.ArmourId, Is.Empty);
        });
    }

    [Test]
    public void TheSaveRecordsWhenItWasWritten()
    {
        var data = SaveGame.Capture(Played(), default);
        Assert.That(DateTimeOffset.Parse(data.SavedUtc),
            Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void StashedGearIsNotSilentlyLostAcrossASave()
    {
        _player.Equipment.StashGear();
        var data = SaveGame.Capture(_player, default);
        var loaded = Reload(data);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Equipment.WeaponId, Is.EqualTo(EquipmentCatalog.UnarmedId));
            Assert.That(loaded.Inventory.CountOf("iron_sword"), Is.EqualTo(1),
                "gear is stored, never destroyed — the items stay in the pack");
        });
    }
}
