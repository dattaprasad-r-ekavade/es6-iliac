using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// A headless run of the session layer.
///
/// The domain has 295 tests, but none of them touch a file on disk or the wiring in
/// <see cref="GameSession"/>. This closes that gap: it plays a few seconds of game, writes a
/// real save, reads it back into a fresh session, and checks the character came back whole.
///
/// Run with <c>--selftest</c>. Exit code 0 means every check passed.
/// </summary>
public static class SessionSelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        var testDirectory = Path.Combine(Path.GetTempPath(), $"ratnabay_selftest_{Guid.NewGuid():N}");
        var savePath = Path.Combine(testDirectory, "ratnabay_save.json");

        try
        {
            Directory.CreateDirectory(testDirectory);
            var session = GameSession.NewGame(savePath);
            var player = session.Player;

            // Play a little: take a hit, train a skill, spend some charge, move.
            player.Combat.TakeHit(24f);
            player.Skills.GrantRouteSkills(StoryDirector.RouteTrade);
            player.Story.SelectRoute(StoryDirector.RouteTrade);
            player.Story.AdvanceTo("chapter.01", "stage.road", "B120");
            player.Vitals.AddGold(37);
            player.Vitals.SpendPrana(30f);
            player.Vitals.SpendStamina(40f);
            player.Objective.Set("Reach the old watch road",
                "Follow the lantern markers beyond the camp.",
                "anchor.watchroad", new WorldPoint(0f, 0f, 120f));

            session.Position = new WorldPoint(14.5f, 2.4f, -8.25f);
            session.Yaw = 0.75f;

            // Two seconds of game time: stamina should recover, nothing else should drift.
            for (var frame = 0; frame < 120; frame++) session.Tick(1f / 60f);

            Check(failures, "stamina recovers over time",
                player.Vitals.Stamina > player.Vitals.MaxStamina - 40f);
            // The hit above put the player in a fight, and prana does not move during one.
            var pranaFloor = player.Vitals.MaxPrana - 30f;
            Check(failures, $"prana holds still during a fight ({player.Vitals.Prana:0.0})",
                Math.Abs(player.Vitals.Prana - pranaFloor) < 0.01f);

            // Once the fight goes quiet it creeps back — present, but nowhere near a refill.
            player.Combat.ClearCombat();
            for (var frame = 0; frame < 120; frame++) session.Tick(1f / 60f);

            Check(failures, $"prana creeps back out of combat ({player.Vitals.Prana:0.0})",
                player.Vitals.Prana > pranaFloor && player.Vitals.Prana < pranaFloor + 4f);

            var expected = Snapshot(session);

            var saveMessage = session.Save();
            Check(failures, $"save succeeds (said: {saveMessage})", saveMessage == "Saved.");
            Check(failures, "a save file exists on disk", File.Exists(savePath));

            // A brand-new session, exactly as a relaunch would build it.
            var reloaded = GameSession.NewGame(savePath);
            var loadMessage = reloaded.Load();
            Check(failures, $"load succeeds (said: {loadMessage})", loadMessage == "Loaded.");

            var actual = Snapshot(reloaded);
            foreach (var key in expected.Keys)
                Check(failures, $"{key} survives the round trip: {expected[key]}",
                    expected[key] == actual[key]);

            // The bearing must regenerate from the restored position rather than be stored.
            var bearing = reloaded.Player.Objective.BearingLine(reloaded.Position);
            Check(failures, $"objective bearing regenerates from the restored position: {bearing}",
                bearing.Contains("north") && bearing.Contains("paces"));

            // A second save creates a last-known-good backup. If the live file is damaged,
            // loading must recover that backup instead of entering a half-restored game.
            session.Player.Vitals.AddGold(1);
            Check(failures, "a replacement save succeeds", session.Save() == "Saved.");
            File.WriteAllText(savePath, "{ damaged save");
            var recovered = GameSession.NewGame(savePath);
            var recoveredOk = recovered.TryLoad(out var recoveryMessage);
            Check(failures, $"a corrupt latest save recovers its backup (said: {recoveryMessage})",
                recoveredOk && recoveryMessage.Contains("backup", StringComparison.OrdinalIgnoreCase)
                && recovered.Player.Vitals.Gold == reloaded.Player.Vitals.Gold);

            RunBetweenRunPersistenceChecks(failures,
                Path.Combine(testDirectory, "between_runs.json"));

            RunFightChecks(failures);
            RunWeaponChecks(failures);
            RunWorldChecks(failures);
            RunDialogueChecks(failures);
            RunQuestChecks(failures);
            RunStealthChecks(failures);
            RunShopChecks(failures);
            RunMineChecks(failures);
            RunSuccession(failures);
        }
        finally
        {
            DeleteTestFile(savePath + ".tmp");
            DeleteTestFile(savePath + ".bak");
            DeleteTestFile(savePath);
            DeleteTestFile(Path.Combine(testDirectory, "between_runs.json.tmp"));
            DeleteTestFile(Path.Combine(testDirectory, "between_runs.json.bak"));
            DeleteTestFile(Path.Combine(testDirectory, "between_runs.json"));
            try { if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Console.WriteLine();
        if (failures.Count == 0)
        {
            Console.WriteLine("Session self-test passed.");
            return 0;
        }

        Console.WriteLine($"Session self-test FAILED with {failures.Count} problem(s):");
        foreach (var failure in failures) Console.WriteLine($"  - {failure}");
        return 1;
    }

    /// <summary>A camp reward must still exist after the next process starts.</summary>
    private static void RunBetweenRunPersistenceChecks(List<string> failures, string savePath)
    {
        var session = GameSession.NewGame(savePath);
        var before = session.Player.Inventory.CountOf(SoulCrystals.LesserId);
        var run = RunState.Begin(seed: 4211, tier: 1, rooms: 8);
        for (var room = 0; room < 3; room++)
        {
            run.EnterRoom();
            run.ClearRoom();
        }

        var result = run.Camp();
        var message = session.CompleteRun(result, new WorldPoint(0f, 2.4f, 14.5f));
        Check(failures, $"camping checkpoints the run reward (said: {message})",
            message == "Saved."
            && session.Player.Inventory.CountOf(SoulCrystals.LesserId) == before + 6);

        var nextDescent = GameSession.NewGame(savePath);
        var loaded = nextDescent.TryLoad(out var loadMessage);
        Check(failures, $"banked stones survive into the next descent (said: {loadMessage})",
            loaded && nextDescent.Player.Inventory.CountOf(SoulCrystals.LesserId) == before + 6
            && nextDescent.Position == new WorldPoint(0f, 2.4f, 14.5f));
    }

    /// <summary>
    /// A whole fight, with no window: the bandit closes, both sides trade blows, and the
    /// player wins. This is the iteration 6 gate — winnable, losable, and Blade rising only
    /// on landed hits — checked without anyone having to play it.
    /// </summary>
    private static void RunFightChecks(List<string> failures)
    {
        Console.WriteLine();

        var stagedSession = GameSession.NewGame();
        var stagedEncounter = new Encounter(stagedSession);
        stagedEncounter.SpawnDefaultCamp();
        var stagedStarts = stagedEncounter.Enemies.Select(enemy => enemy.Position.Z).ToArray();
        var safeSpawn = new Vector3(0f, 2.4f, 14.5f);
        for (var frame = 0; frame < 180; frame++)
        {
            stagedSession.Tick(1f / 60f);
            stagedEncounter.Update(1f / 60f, safeSpawn, 0f);
        }

        Check(failures, "the authored encounter has two bandits at the far end of room three",
            stagedEncounter.Enemies.Count == 2
            && stagedEncounter.Enemies.All(enemy => enemy.Position.Z <= -40f));
        Check(failures, "the empty starting room does not trigger combat",
            stagedSession.Player.Vitals.Health == stagedSession.Player.Vitals.MaxHealth
            && stagedEncounter.Enemies.Select((enemy, index) =>
                Math.Abs(enemy.Position.Z - stagedStarts[index]) < 0.001f).All(still => still));

        var session = GameSession.NewGame();
        var player = session.Player;
        var encounter = new Encounter(session);

        var bandit = new EnemyArchetype
        {
            Id = "bandit", DisplayName = "Bandit", MaxHealth = 55f, MoveSpeed = 4.4f,
            AggroRange = 16f, AttackRange = 2.2f, AttackDamage = 7f,
            AttackCooldown = 1.4f, XpReward = 20
        };

        var playerPosition = new Vector3(0f, 0f, 0f);
        encounter.Spawn(bandit, new Vector3(0f, 0f, -12f), "bandit.test.01");
        Check(failures, "a bandit spawned", encounter.Enemies.Count == 1);

        var enemy = encounter.Enemies[0];
        var startDistance = enemy.Position.Z;

        // Facing it: yaw zero looks down -Z, which is where it is standing.
        const float facing = 0f;
        const float step = 1f / 60f;

        // It should close the distance on its own. The session is ticked too: cooldowns and
        // stamina live on the player, and a loop that only ticks the enemies deadlocks the
        // moment it waits on a swing that can never come off cooldown.
        for (var frame = 0; frame < 240; frame++)
        {
            session.Tick(step);
            encounter.Update(step, playerPosition, facing);
        }

        Check(failures, $"the bandit closed from {startDistance:0.0} m to {enemy.Position.Z:0.0} m",
            enemy.Position.Z > startDistance + 5f);
        Check(failures, "it is now in reach and focused by the crosshair",
            ReferenceEquals(encounter.Focused, enemy));
        Check(failures, "it fought back", player.Vitals.Health < player.Vitals.MaxHealth);

        // Animation must never move a hitbox. The drawn position is allowed to lunge and
        // recoil; the domain position it is targeted at is not.
        var domainPosition = enemy.Position;
        var drawn = encounter.DrawPositionOf(enemy);
        Check(failures, "animation offsets the drawn position without moving the enemy",
            Math.Abs(enemy.Position.X - domainPosition.X) < 0.0001f
            && Math.Abs(enemy.Position.Z - domainPosition.Z) < 0.0001f);

        Check(failures, $"the drawn position is animated away from it ({drawn.Y:0.00} m up)",
            drawn.Y >= domainPosition.Y);
        Check(failures, "being attacked started a fight", player.Combat.InCombat);

        // Swinging at nothing must not train the weapon.
        var facingAway = MathF.PI;
        player.Combat.Tick(2f);
        var missed = player.Combat.TryAttack(
            Targeting.Find(default, facingAway, player.Combat.ActiveWeapon.Range, encounter.Enemies));

        Check(failures, "swinging away from it misses", missed.Result == AttackResult.Missed);
        Check(failures, "a miss trains nothing", player.Skills.LevelOf(Skills.Blade) == 0f);

        // Now land blows until it drops.
        var swings = 0;
        var frames = 0;

        // Bounded rather than "until it dies": this runs inside the publish gate, so a
        // regression has to fail the build rather than hang it.
        while (enemy.IsAlive && frames++ < 20_000)
        {
            session.Tick(step);
            encounter.Update(step, playerPosition, facing);
            if (encounter.PlayerAttack().Result == AttackResult.Hit) swings++;

            // Keep the player standing so the fight can be seen through to the end.
            if (player.Vitals.Health < 30f) player.Vitals.Heal(60f);
        }

        Check(failures, "a struck enemy recoils and its height dips",
            encounter.DrawHeightOf(enemy) <= Encounter.FigureHeight * 1.001f);

        Check(failures, $"the bandit can be killed (took {swings} landed hits)", !enemy.IsAlive);
        Check(failures, "a dead enemy is no longer a valid target",
            Targeting.Find(default, facing, 3f, encounter.Enemies) is null);
        Check(failures, "landed hits trained Blade", player.Skills.LevelOf(Skills.Blade) > 0f);
        Check(failures, "the kill paid experience", player.Vitals.Xp > 0);
        Check(failures, "the kill dropped loot", player.Inventory.CountOf("bandit_loot") > 0);
        // Corpses clear on the following frame: removing an enemy from inside the loop that
        // is walking the list would mutate it mid-iteration.
        encounter.Update(step, playerPosition, facing);
        Check(failures, "the corpse clears on the next frame", encounter.Enemies.Count == 0);
        Check(failures, "the world remembers it stayed dead",
            player.World.IsKilled("bandit.test.01"));

        // A reload must not resurrect it.
        var saved = SaveGame.Capture(player, default);
        var reloaded = GameSession.NewGame();
        SaveGame.Restore(reloaded.Player, saved);
        var freshEncounter = new Encounter(reloaded);
        freshEncounter.Spawn(bandit, new Vector3(0f, 0f, -12f), "bandit.test.01");

        Check(failures, "a reload does not bring it back", freshEncounter.Enemies.Count == 0);

        // Losable: an unarmed, unhealed player against three of them.
        var doomed = GameSession.NewGame();
        doomed.Player.Equipment.UnequipWeapon();
        var ambush = new Encounter(doomed);
        ambush.Spawn(bandit, new Vector3(1f, 0f, -3f), "ambush.01");
        ambush.Spawn(bandit, new Vector3(-1f, 0f, -3f), "ambush.02");
        ambush.Spawn(bandit, new Vector3(0f, 0f, -4f), "ambush.03");

        for (var frame = 0; frame < 6000 && doomed.Player.Vitals.IsAlive; frame++)
        {
            doomed.Tick(step);
            ambush.Update(step, Vector3.Zero, facing);
        }

        Check(failures, "the fight is losable", !doomed.Player.Vitals.IsAlive);
    }

    /// <summary>The first authored room is loadable, blocks the closed door, and opens via Security.</summary>
    private static void RunWorldChecks(List<string> failures)
    {
        Console.WriteLine();

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "World", "northwatch.json");
        var loaded = WorldRuntime.TryLoad(path, out var world, out var error);
        Check(failures, $"the authored room loads (said: {error})", loaded && world is not null);
        if (world is null) return;

        Check(failures, "the authored world has three enterable thresholds",
            world.Manifest.Doors.Count == 3);
        Check(failures, "the starting and trader rooms contain no props, patrols or pickups",
            world.Manifest.Watchers.Count == 0
            && world.Manifest.Props.All(prop => prop.Position.Z < -44f)
            && world.Manifest.Pickups.All(pickup => pickup.Position.Z < -44f));
        Check(failures, "the later dungeon keeps one recoverable cache",
            world.Manifest.Pickups.Count == 1
            && world.Manifest.Pickups[0].ItemId == "health_potion");

        var reloadPath = Path.Combine(Path.GetTempPath(), $"ratnabay_world_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(reloadPath, WorldManifest.Serialize(world.Manifest));
            File.SetLastWriteTimeUtc(reloadPath, DateTime.UtcNow.AddSeconds(-2));
            Check(failures, "a hot-reload fixture loads",
                WorldRuntime.TryLoad(reloadPath, out var reloadWorld, out _)
                && reloadWorld is not null);

            if (WorldRuntime.TryLoad(reloadPath, out var hotWorld, out _) && hotWorld is not null)
            {
                var edited = new WorldManifest
                {
                    Version = world.Manifest.Version,
                    Id = "scene.northwatch.edited",
                    PlayerSpawn = world.Manifest.PlayerSpawn,
                    Geometry = world.Manifest.Geometry,
                    Props = world.Manifest.Props,
                    Lights = world.Manifest.Lights,
                    Doors = world.Manifest.Doors,
                    Watchers = world.Manifest.Watchers,
                    Pickups = world.Manifest.Pickups
                };
                File.WriteAllText(reloadPath, WorldManifest.Serialize(edited));
                File.SetLastWriteTimeUtc(reloadPath, DateTime.UtcNow.AddSeconds(2));
                Check(failures, "a valid world edit hot-reloads",
                    hotWorld.TryReloadIfChanged(out _)
                    && hotWorld.Manifest.Id == "scene.northwatch.edited");

                File.WriteAllText(reloadPath, "{ not valid json");
                File.SetLastWriteTimeUtc(reloadPath, DateTime.UtcNow.AddSeconds(4));
                Check(failures, "an invalid world edit leaves the room active",
                    !hotWorld.TryReloadIfChanged(out var reloadError)
                    && reloadError.Contains("Invalid world manifest JSON", StringComparison.Ordinal));
            }
        }
        finally
        {
            try { if (File.Exists(reloadPath)) File.Delete(reloadPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var outside = world.Manifest.PlayerSpawn.Position.ToWorldPoint();
        var blocked = world.Move(outside, new WorldPoint(0f, 0f, -30f), 0.38f);
        Check(failures, "the closed doorway blocks a long move",
            blocked.Z > -9.6f);

        var session = GameSession.NewGame();
        var atDoor = new WorldPoint(0f, 2.4f, -8f);
        var result = world.TryOpenDoor(atDoor, 0f, session.Player, out var door);
        Check(failures, "Security can open the authored door",
            result == LockResult.Opened && door is not null && door.Lock.IsOpen);

        var through = world.Move(atDoor, new WorldPoint(0f, 0f, -8f), 0.38f);
        Check(failures, "the opened doorway allows entry",
            through.Z < -15f);

        var atSupplyDoor = new WorldPoint(0f, 2.4f, -22f);
        var supplyResult = world.TryOpenDoor(atSupplyDoor, 0f, session.Player, out var supplyDoor);
        Check(failures, "the second authored door opens",
            supplyResult == LockResult.Opened && supplyDoor is not null && supplyDoor.Lock.IsOpen);
        var intoSupply = world.Move(atSupplyDoor, new WorldPoint(0f, 0f, -10f), 0.38f);
        Check(failures, "the second room is reachable through JSON-authored geometry",
            intoSupply.Z < -30f);

        var saved = SaveGame.Capture(session.Player, atDoor);
        var restoredPlayer = PlayerCharacter.NewGame();
        SaveGame.Restore(restoredPlayer, saved);
        var restoredWorldLoaded = WorldRuntime.TryLoad(path, out var restoredWorld, out _);
        if (restoredWorldLoaded && restoredWorld is not null)
            restoredWorld.RestoreOpenedDoors(restoredPlayer.Story.State.OpenedLocks);
        Check(failures, "an opened door is written to story save state",
            restoredPlayer.Story.State.OpenedLocks.Contains("northwatch.entry.door")
            && restoredPlayer.Story.State.OpenedLocks.Contains("northwatch.supply.door"));
        Check(failures, "a restored opened door remains passable",
            restoredWorld is not null
            && restoredWorld.FindDoor(atDoor, 0f) is null
            && restoredWorld.FindDoor(atSupplyDoor, 0f) is null
            && restoredWorld.Move(atSupplyDoor, new WorldPoint(0f, 0f, -10f), 0.38f).Z < -30f);
    }

    /// <summary>
    /// The weapon is a UI sprite whose origin is the player's grip. A swing must rotate the
    /// blade around that origin; translating the whole texture makes the hilt leave the hand.
    /// </summary>
    private static void RunWeaponChecks(List<string> failures)
    {
        var weapon = EquipmentCatalog.GetWeapon("iron_sword");
        var view = new WeaponView();
        var rest = view.Pose();

        view.Swing(weapon);
        view.Update(0.18f, moving: false, guarding: false);
        var swing = view.Pose();

        Check(failures, "the sword swing keeps its grip anchored",
            Math.Abs(swing.Position.X - rest.Position.X) < 0.001f
            && Math.Abs(swing.Position.Y - rest.Position.Y) < 0.001f);
        Check(failures, "the sword blade rotates around the grip",
            Math.Abs(swing.Rotation - rest.Rotation) > 0.01f);
        Check(failures, "the sword strike sweeps toward the target",
            swing.Rotation < rest.Rotation);

        var idle = new WeaponView();
        var idleBefore = idle.Pose();
        idle.Update(0.25f, moving: false, guarding: false);
        var idleAfter = idle.Pose();
        Check(failures, "standing still does not sway the weapon",
            idleBefore == idleAfter);

        idle.Update(0.25f, moving: true, guarding: false);
        Check(failures, "walking produces weapon sway",
            idle.Pose() != idleAfter);
    }

    /// <summary>The authored trader room: two merchants, learned keywords and conditioned answers.</summary>
    private static void RunDialogueChecks(List<string> failures)
    {
        Console.WriteLine();

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Dialogue", "northwatch.json");
        var session = GameSession.NewGame();
        var loaded = DialogueRuntime.TryLoad(path, session.Player.Dialogue, out var dialogue, out var error);

        Check(failures, $"the authored dialogue loads (said: {error})",
            loaded && dialogue is not null);
        if (dialogue is null) return;

        Check(failures, "the dialogue manifest has two traders in room two",
            dialogue.Actors.Count == 2
            && dialogue.Actors.All(actor => actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase))
            && dialogue.Actors.All(actor => actor.Position.Z < -10f && actor.Position.Z > -24f));
        // Counted rather than pinned: an exact total fails every time a line is authored,
        // which trains people to edit the test instead of reading it.
        Check(failures, $"the dialogue manifest carries a body of topics ({dialogue.Manifest.Topics.Count})",
            dialogue.Manifest.Topics.Count >= 14);

        Check(failures, "the watchpost key has somewhere to be learned about",
            dialogue.Manifest.Topics.Any(topic =>
                topic.Keyword.Equals("watchpost", StringComparison.OrdinalIgnoreCase)));

        var mara = dialogue.Actors.Single(actor => actor.ActorId == "actor.mara");
        var vesa = dialogue.Actors.Single(actor => actor.ActorId == "actor.vesa");
        var maraTopics = mara.Talk();
        Check(failures, "Mara teaches the road and two-bandit quest topics",
            maraTopics.Contains("northwatch") && maraTopics.Contains("road")
            && maraTopics.Contains("bandits") && !maraTopics.Contains("door"));
        Check(failures, "Mara gives an actor-specific Northwatch answer",
            mara.Ask("northwatch")?.Contains("lamp oil", StringComparison.OrdinalIgnoreCase) == true);

        session.Player.Story.SelectRoute(StoryDirector.RouteTrade);
        Check(failures, "a route condition changes Mara's road answer",
            mara.Ask("road")?.Contains("two bandits", StringComparison.OrdinalIgnoreCase) == true);

        session.Player.Story.AddChanneled(1f);
        vesa.Talk();
        Check(failures, "a story condition changes Vesa's jiva answer",
            vesa.Ask("jiva stones")?.Contains("drawn", StringComparison.OrdinalIgnoreCase) == true);

        session.Player.Story.SetFlag("flag.opened.northwatch.entry.door");
        mara.Talk();
        Check(failures, "an opened-door flag unlocks the authored follow-up topic",
            mara.AvailableTopics().Contains("door"));
    }

    /// <summary>Quest definitions stay dormant until the dialogue answer accepts one.</summary>
    private static void RunQuestChecks(List<string> failures)
    {
        Console.WriteLine();

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Quests", "northwatch.json");
        var session = GameSession.NewGame();
        var loaded = QuestManifest.TryLoad(path, out var manifest, out var error);
        Check(failures, $"the authored quest manifest loads (said: {error})",
            loaded && manifest is not null);
        if (manifest is null) return;

        session.Player.Quests.RegisterRange(manifest.ToDefinitions());
        var quest = session.Player.Quests.Find("quest.northwatch.bandits");
        Check(failures, "the bandit quest starts dormant before acceptance",
            quest is not null && !quest.IsActive);

        session.Player.Quests.Activate("quest.northwatch.bandits");
        for (var index = 0; index < 2; index++) session.Player.Quests.NotifyEnemyKilled("Bandit");
        Check(failures, "the accepted quest pays out after two bandits",
            quest is not null && quest.IsCompleted && session.Player.Vitals.Gold == 80);

        var dialoguePath = Path.Combine(AppContext.BaseDirectory, "Content", "Dialogue", "northwatch.json");
        var dialogueLoaded = DialogueRuntime.TryLoad(dialoguePath, session.Player.Dialogue,
            out var dialogue, out _);
        var mara = dialogue?.Actors.Single(actor => actor.ActorId == "actor.mara");
        mara?.Talk();
        Check(failures, "Mara acknowledges the completed quest instead of offering it again",
            dialogueLoaded && mara?.Ask("bandits")?.Contains("road is safe",
                StringComparison.OrdinalIgnoreCase) == true);

        var saved = SaveGame.Capture(session.Player, default);
        var restored = PlayerCharacter.NewGame();
        restored.Quests.RegisterRange(manifest.ToDefinitions());
        SaveGame.Restore(restored, saved);
        Check(failures, "quest completion survives save and reload",
            restored.Quests.Find("quest.northwatch.bandits")?.IsCompleted == true
            && restored.Story.HasFlag(PlayerCharacter.QuestCompletedFlag("quest.northwatch.bandits")));
    }

    /// <summary>View-cone sight, a blocker ray and recoverable pickpocketing.</summary>
    private static void RunStealthChecks(List<string> failures)
    {
        Console.WriteLine();

        var manifest = new WorldManifest
        {
            Version = 1,
            Id = "scene.stealth.test",
            Watchers = new List<WorldWatcher>
            {
                new()
                {
                    Id = "watcher.test",
                    Position = new WorldVector(0f, 0f, 4f),
                    Yaw = 0f,
                    Speed = 0f,
                    ViewRange = 10f,
                    ViewConeDegrees = 120f
                }
            }
        };
        var collision = new StaticCollisionIndex();
        collision.Rebuild(Array.Empty<CollisionBox>());
        var session = GameSession.NewGame();
        var runtime = new WatcherRuntime(manifest, collision, session.Player.Detection);
        var player = new WorldPoint(0f, 2.4f, 0f);
        runtime.Update(0.1f, player);
        session.Player.Detection.Tick(1f);
        Check(failures, "a guard's facing cone raises suspicion",
            session.Player.Detection.Suspicion > 0f);

        session.Player.Detection.Clear();
        collision.Rebuild(new[] { new CollisionBox("sight.wall", -1f, 0f, 1f, 1f, 3f, 3f) });
        runtime.Update(0.1f, player);
        session.Player.Detection.Tick(1f);
        Check(failures, "a solid between guard and player blocks sight",
            session.Player.Detection.Suspicion == 0f);

        runtime.Reload(manifest);
        Check(failures, "reloading patrol data does not duplicate watchers",
            runtime.Watchers.Count == 1);

        session.Player.Detection.Clear();
        session.Player.Detection.AddSuspicion(0.5f);
        var pocket = new PickpocketTarget(0f,
            new ItemStack { Id = "test.purse", Name = "Test purse", Kind = "loot", Count = 1 });
        var outcome = Pickpocketing.TryTake(pocket, session.Player.Skills,
            session.Player.Inventory, session.Player.Detection);
        Check(failures, "a witnessed pickpocket keeps the item but raises a caught result",
            outcome.Result == PickpocketResult.Caught
            && session.Player.Inventory.CountOf("test.purse") == 1);
    }

    /// <summary>The authored merchant stock spends gold and uses the canonical item ids.</summary>
    private static void RunShopChecks(List<string> failures)
    {
        Console.WriteLine();

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Shops", "northwatch.json");
        var loaded = ShopManifest.TryLoad(path, out var manifest, out var error);
        Check(failures, $"the authored shop manifest loads (said: {error})",
            loaded && manifest is not null);
        if (manifest is null) return;

        var definition = manifest.ToDefinitions().Single();
        var shop = new Shop(definition);
        var session = GameSession.NewGame();
        session.Player.Vitals.AddGold(100);
        var result = shop.Buy(2, session.Player.Vitals, session.Player.Inventory, out _);

        Check(failures, "the shop sells the canonical lesser jiva stone",
            result == ShopPurchaseResult.Bought
            && session.Player.Inventory.CountOf(SoulCrystals.LesserId) == 4
            && session.Player.Vitals.Gold == 75);
        Check(failures, "purchased stock becomes sold out",
            shop.IsSoldOut(SoulCrystals.LesserId));
    }

    /// <summary>Everything that must be identical after a save and a reload.</summary>
    private static Dictionary<string, string> Snapshot(GameSession session)
    {
        var player = session.Player;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["position"] = $"{session.Position.X:0.###},{session.Position.Y:0.###},{session.Position.Z:0.###}",
            ["yaw"] = session.Yaw.ToString("0.###"),
            ["health"] = player.Vitals.Health.ToString("0.###"),
            ["prana"] = player.Vitals.Prana.ToString("0.###"),
            ["stamina"] = player.Vitals.Stamina.ToString("0.###"),
            ["level"] = player.Vitals.Level.ToString(),
            ["gold"] = player.Vitals.Gold.ToString(),
            ["weapon"] = player.Equipment.WeaponId,
            ["security skill"] = player.Skills.LevelOf(Skills.Security).ToString("0.###"),
            ["stealth skill"] = player.Skills.LevelOf(Skills.Stealth).ToString("0.###"),
            ["potions"] = player.Inventory.CountOf("health_potion").ToString(),
            ["jiva stones"] = player.Inventory.CountOf(SoulCrystals.LesserId).ToString(),
            ["route"] = player.Story.State.RouteId,
            ["beat"] = player.Story.State.BeatId,
            ["objective"] = player.Objective.Title ?? "(none)"
        };
    }

    /// <summary>
    /// A generated mine, taken all the way through the game layer.
    ///
    /// The domain tests already prove the generator emits a valid manifest. What they cannot
    /// prove is the part that matters here: that the runtime loads one off disk like any other
    /// level and that its enemies actually arrive in the scene.
    /// </summary>
    private static void RunMineChecks(List<string> failures)
    {
        Console.WriteLine();

        const int seed = 4211;
        var manifest = MineGenerator.Generate(seed, rooms: 5, depth: 2);
        var path = Path.Combine(Path.GetTempPath(), $"ratnabay_mine_{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, WorldManifest.Serialize(manifest));

            var loaded = WorldRuntime.TryLoad(path, out var mine, out var error);
            Check(failures, $"a generated mine loads through the ordinary path (said: {error})",
                loaded && mine is not null);
            if (mine is null) return;

            var createdInMemory = WorldRuntime.TryCreate(manifest, out var memoryMine,
                out var memoryError);
            Check(failures, $"a generated mine loads in memory without an install file (said: {memoryError})",
                createdInMemory && memoryMine is not null && memoryMine.ManifestPath is null);

            Check(failures, "the same seed produces the same mine",
                WorldManifest.Serialize(MineGenerator.Generate(seed, rooms: 5, depth: 2))
                    == WorldManifest.Serialize(manifest));
            Check(failures, "a different seed produces a different mine",
                WorldManifest.Serialize(MineGenerator.Generate(seed + 1, rooms: 5, depth: 2))
                    != WorldManifest.Serialize(manifest));

            Check(failures, "the mine has five rooms joined by doors",
                mine.Manifest.Doors.Count == 5);

            var session = GameSession.NewGame();
            var encounter = new Encounter(session);
            var spawned = encounter.SpawnFrom(mine.Manifest);

            Check(failures, $"the mine spawns its own enemies ({spawned})",
                spawned > 0 && spawned == mine.Manifest.Spawns.Count);
            Check(failures, "and they arrive in the scene",
                encounter.Enemies.Count == spawned);
            Check(failures, "a depth-two enemy is tougher than the surface bandit",
                encounter.Enemies[0].MaxHealth
                    > EnemyCatalog.Find(EnemyCatalog.BanditId)!.MaxHealth);

            var spawn = mine.Manifest.PlayerSpawn.Position;
            var start = new WorldPoint(spawn.X, 1.7f, spawn.Z);
            var firstRoom = new WorldPoint(
                mine.Manifest.Lights[0].Position.X, 1.7f, mine.Manifest.Lights[0].Position.Z);

            // Straight at the far wall of the entrance room, which the player must not clip.
            var walked = mine.Move(start, new WorldPoint(0f, 0f, -60f), 0.45f);
            Check(failures, "the player spawns inside the mine and is stopped by its walls",
                walked.FlatDistanceTo(start) > 1f
                && walked.FlatDistanceTo(firstRoom) < 16f);

            CheckEnemiesRespectWalls(failures, mine);
            CheckTheArcherBreaksTheDoorway(failures, mine);

            // Last, because a descent opens doors and that changes what the walls do.
            RunOneDescent(failures, mine);
        }
        finally
        {
            DeleteTestFile(path);
        }
    }

    /// <summary>
    /// A whole descent, driven through the runtime rather than the domain.
    ///
    /// The ledger's arithmetic is already proven headlessly. What this covers is the wiring
    /// nobody else does: that standing in a room makes it the current room, that killing the
    /// last thing in it pays out exactly once, and that the door is what ends the argument.
    /// </summary>
    private static void RunOneDescent(List<string> failures, WorldRuntime mine)
    {
        var session = GameSession.NewGame();
        var encounter = new Encounter(session);
        encounter.SpawnFrom(mine.Manifest);

        var run = new RunRuntime(mine.Manifest, seed: 4211, tier: 2);
        var rooms = mine.Manifest.Rooms.OrderBy(room => room.Index).ToList();

        Check(failures, $"the run counts the payable rooms ({run.Run.Rooms})",
            run.Run.Rooms == rooms.Count - 1);

        Vector3 Stand(WorldRoom room) => new(room.Centre.X, 1.7f, room.Centre.Z);

        // Standing in the entrance pays nothing and offers nothing.
        run.Update(mine, Stand(rooms[0]), encounter);
        Check(failures, "the entrance room is worth nothing",
            run.Run.Pending == 0 && !run.Run.CanCamp);

        // Walk into the first fight room without killing anything.
        run.Update(mine, Stand(rooms[1]), encounter);
        Check(failures, "walking into an occupied room does not pay",
            run.Run.Pending == 0 && !run.Run.RoomIsClear);

        // Kill everything in it.
        foreach (var enemy in encounter.Enemies
            .Where(enemy => mine.Manifest.Spawns
                .Any(spawn => spawn.Id == enemy.SpawnId && spawn.RoomIndex == 1))
            .ToList())
        {
            enemy.TakeDamage(enemy.MaxHealth * 4f);
        }

        encounter.Update(0.016f, Stand(rooms[1]), 0f);
        run.Update(mine, Stand(rooms[1]), encounter);

        Check(failures, $"clearing the first room of a tier-two mine pays two ({run.Run.Pending})",
            run.Run.Pending == 2);
        // Ticking again must not pay a second time; the room is already clear.
        run.Update(mine, Stand(rooms[1]), encounter);
        Check(failures, "and it pays exactly once", run.Run.Pending == 2 && run.Run.RoomsCleared == 1);
        Check(failures, "the next room is worth more than the last",
            run.Run.NextRoomPays > 2);

        // The decision is only offered at the door, not in the middle of the room.
        Check(failures, "no decision in the middle of a cleared room", !run.AtDecision);

        var door = mine.Doors.First(candidate => !candidate.Lock.IsOpen);
        var atDoor = door.Definition.Centre();
        run.Update(mine, new Vector3(atDoor.X, 1.7f, atDoor.Z), encounter);

        Check(failures, "standing at the shut door asks the question", run.AtDecision);
        Check(failures, "and both answers are available",
            run.Run.CanCamp && run.Run.CanPressOn);

        // Press on: the door opens and the room behind it is committed to.
        Check(failures, "pressing on opens the way deeper",
            run.PressOn(mine, session.Player) && door.Lock.IsOpen);

        run.Update(mine, Stand(rooms[2]), encounter);
        Check(failures, "the room behind the door is not yet clear", !run.Run.RoomIsClear);
        Check(failures, "and there is no banking mid-fight", !run.Run.CanCamp);

        // Retreat into the cleared room behind, then come back. Neither may pay.
        //
        // Found in a recorded run: the player stepped back out of a room mid-fight, the
        // clearance test looked at the cleared room they were standing in, and paid for the
        // room they had left — then paid again when they walked back into it. They reached
        // room eight and were paid for nine.
        var bankedBefore = run.Run.Pending;
        var clearedBefore = run.Run.RoomsCleared;

        for (var step = 0; step < 8; step++)
        {
            run.Update(mine, Stand(rooms[1]), encounter);
            run.Update(mine, Stand(rooms[2]), encounter);
        }

        Check(failures, $"retreating and returning pays nothing ({run.Run.Pending})",
            run.Run.Pending == bankedBefore && run.Run.RoomsCleared == clearedBefore);
        Check(failures, "and the unfinished room is still unfinished", !run.Run.RoomIsClear);

        // Dying now forfeits everything held.
        var died = run.Die();
        Check(failures, $"dying forfeits the pot ({died.StonesLost})",
            !died.Survived && died.StonesLost == 2 && run.Run.Pending == 0);
    }

    /// <summary>
    /// Reported from play: enemies walked through walls.
    ///
    /// Their pursuit never consulted the world at all, so in a mine of small rooms a bandit
    /// would step out of solid rock. This puts one on the far side of a wall and asks it to
    /// come and get the player.
    /// </summary>
    private static void CheckEnemiesRespectWalls(List<string> failures, WorldRuntime mine)
    {
        var session = GameSession.NewGame();
        var encounter = new Encounter(session);
        encounter.UseCollision(mine.Collision);

        var rooms = mine.Manifest.Rooms.OrderBy(room => room.Index).ToList();
        var bandit = EnemyCatalog.Find(EnemyCatalog.BanditId)!;

        // One enemy in the entrance, the player two rooms away with shut doors between.
        var start = rooms[0].CentrePoint();
        var target = new Vector3(rooms[2].Centre.X, 1.7f, rooms[2].Centre.Z);

        encounter.Spawn(bandit, new Vector3(start.X, 0f, start.Z), "selftest.wallwalker");
        var walker = encounter.Enemies.Single(enemy => enemy.SpawnId == "selftest.wallwalker");
        walker.Home = new WorldPoint(target.X, 0f, target.Z);

        for (var step = 0; step < 600; step++) encounter.Update(0.05f, target, 0f);

        var stillInside = rooms[0].Contains(walker.Position.X, walker.Position.Z);
        Check(failures, "an enemy cannot walk out through a wall", stillInside);

        var reached = new WorldPoint(walker.Position.X, 0f, walker.Position.Z)
            .FlatDistanceTo(new WorldPoint(target.X, 0f, target.Z));
        Check(failures, $"and is stopped well short of the player ({reached:0.0} m)", reached > 8f);
    }

    /// <summary>
    /// The archer, which exists to break fighting every room from its doorway.
    ///
    /// A recorded run cleared seven of nine rooms without setting foot in them, because every
    /// enemy chased in a straight line and could be met one at a time in a corridor. This
    /// checks the two behaviours that change that: it hurts from across the room, and it gives
    /// ground rather than walking into reach.
    /// </summary>
    private static void CheckTheArcherBreaksTheDoorway(List<string> failures, WorldRuntime mine)
    {
        var rooms = mine.Manifest.Rooms.OrderBy(room => room.Index).ToList();
        var archer = EnemyCatalog.Find(EnemyCatalog.ArcherId)!;
        var centre = rooms[1].CentrePoint();

        // --- it shoots from far outside sword reach
        var session = GameSession.NewGame();
        var encounter = new Encounter(session);
        encounter.UseCollision(mine.Collision);
        encounter.Spawn(archer, new Vector3(centre.X, 0f, centre.Z), "selftest.archer");

        var shooter = encounter.Enemies.Single();

        // Inside the room. A room is sixteen metres across, so standing twelve from its centre
        // is standing in the rock — and the arrow correctly stopped at the wall, which read as
        // the archer being harmless.
        var standing = new Vector3(centre.X, 1.7f, centre.Z + 6.5f);
        var reach = session.Player.Combat.ActiveWeapon.Range;

        Check(failures, $"the archer outranges a sword ({archer.AttackRange:0} m vs {reach:0} m)",
            archer.AttackRange > reach * 3f);

        var before = session.Player.Vitals.Health;
        for (var step = 0; step < 200; step++) encounter.Update(0.05f, standing, 0f);

        Check(failures,
            $"and hurts from across the room ({before - session.Player.Vitals.Health:0} damage)",
            session.Player.Vitals.Health < before);

        // And its arrows are stopped by rock, so a doorway is cover rather than a firing slit.
        var walled = GameSession.NewGame();
        var walledEncounter = new Encounter(walled);
        walledEncounter.UseCollision(mine.Collision);
        walledEncounter.Spawn(archer, new Vector3(centre.X, 0f, centre.Z), "selftest.archer.wall");

        var throughRock = new Vector3(centre.X, 1.7f, centre.Z + 13f);
        for (var step = 0; step < 200; step++) walledEncounter.Update(0.05f, throughRock, 0f);

        Check(failures, "but not through a wall",
            walled.Player.Vitals.Health >= walled.Player.Vitals.MaxHealth);

        // --- it gives ground rather than closing
        var closeSession = GameSession.NewGame();
        var closeEncounter = new Encounter(closeSession);
        closeEncounter.UseCollision(mine.Collision);
        closeEncounter.Spawn(archer, new Vector3(centre.X, 0f, centre.Z), "selftest.archer.close");

        var cornered = closeEncounter.Enemies.Single();
        var crowding = new Vector3(centre.X, 1.7f, centre.Z + 2f);
        var startedAt = cornered.Position.FlatDistanceTo(
            new WorldPoint(crowding.X, 0f, crowding.Z));

        for (var step = 0; step < 40; step++) closeEncounter.Update(0.05f, crowding, 0f);

        var endedAt = cornered.Position.FlatDistanceTo(new WorldPoint(crowding.X, 0f, crowding.Z));
        Check(failures,
            $"and backs away when crowded ({startedAt:0.0} m -> {endedAt:0.0} m)",
            endedAt > startedAt);

        // A plain bandit must still do the opposite, or the fix has broken melee.
        var meleeSession = GameSession.NewGame();
        var meleeEncounter = new Encounter(meleeSession);
        meleeEncounter.UseCollision(mine.Collision);
        meleeEncounter.Spawn(EnemyCatalog.Find(EnemyCatalog.BanditId)!,
            new Vector3(centre.X, 0f, centre.Z), "selftest.melee");

        // From outside its reach, or it is already where it wants to be and has no reason
        // to move — which is a test of nothing.
        var brawler = meleeEncounter.Enemies.Single();
        var far = new Vector3(centre.X, 1.7f, centre.Z + 8f);
        var meleeStart = brawler.Position.FlatDistanceTo(new WorldPoint(far.X, 0f, far.Z));

        for (var step = 0; step < 40; step++) meleeEncounter.Update(0.05f, far, 0f);

        var meleeEnd = brawler.Position.FlatDistanceTo(new WorldPoint(far.X, 0f, far.Z));
        Check(failures, $"while a bandit still closes ({meleeStart:0.0} m -> {meleeEnd:0.0} m)",
            meleeEnd < meleeStart);
    }

    /// <summary>
    /// Die, be replaced, go back down, and lift your predecessor off the floor.
    ///
    /// The domain proves the ledger; this proves the loop closes in the world — that the cache
    /// is placed in the mine that killed them, in the room they died in, and that walking into
    /// it is an ordinary pickup rather than a special case.
    /// </summary>
    private static void RunSuccession(List<string> failures)
    {
        const int seed = 90210;

        var session = GameSession.NewGame();
        var player = session.Player;
        player.Vitals.AddXp(player.Vitals.XpToLevel + 15);

        var level = player.Vitals.Level;
        var buried = player.Legacy.CurrentName;
        var lost = new RunResult(RunOutcome.Died, 6, 0, 21, 1);

        Succession.Promote(player, lost, seed, roomIndex: 6);

        Check(failures, $"a death promotes a successor ({buried} -> {player.Legacy.CurrentName})",
            player.Legacy.CurrentName != buried && player.Legacy.Generation == 1);
        Check(failures, $"who keeps the rank but not the progress (level {player.Vitals.Level})",
            player.Vitals.Level == level && player.Vitals.Xp == 0);
        Check(failures, "and is sent down armed",
            player.Combat.ActiveWeapon.Damage > 0f);

        // The same mine, regenerated from the same seed, now holds the body.
        var manifest = MineGenerator.Generate(seed, 18, 1);
        var cache = player.Legacy.Fallen!;
        var room = manifest.Rooms.Single(candidate => candidate.Index == cache.RoomIndex);

        manifest.Pickups.Add(new WorldPickup
        {
            Id = "cache.fallen",
            ItemId = SoulCrystals.LesserId,
            Name = $"{cache.Name}'s Cache",
            Kind = SoulCrystals.ItemKind,
            Count = cache.Stones,
            Position = new WorldVector(room.Centre.X, 0.1f, room.Centre.Z)
        });

        Check(failures, "the mine with a body in it still validates",
            manifest.Validate().Count == 0);
        Check(failures, $"and the cache is in the room they died in (room {cache.RoomIndex})",
            room.Contains(manifest.Pickups[^1].Position.X, manifest.Pickups[^1].Position.Z));

        var before = player.Inventory.CountOf(SoulCrystals.LesserId);
        player.Inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, cache.Stones,
            SoulCrystals.ItemKind);
        player.Legacy.Recover();

        Check(failures, $"lifting it returns the stones ({before} -> "
            + $"{player.Inventory.CountOf(SoulCrystals.LesserId)})",
            player.Inventory.CountOf(SoulCrystals.LesserId) == before + 21);
        Check(failures, "and there is nothing left to fetch",
            player.Legacy.Fallen is null && !player.Legacy.CanRecoverIn(seed));

        // A save taken after all of that has to remember who is dead.
        var reloaded = PlayerCharacter.NewGame();
        SaveGame.Restore(reloaded, SaveGame.Capture(player, default));

        Check(failures, $"the bloodline survives a save ({reloaded.Legacy.CurrentName})",
            reloaded.Legacy.Generation == 1
            && reloaded.Legacy.CurrentName == player.Legacy.CurrentName);
    }

    private static void Check(ICollection<string> failures, string what, bool passed)
    {
        Console.WriteLine($"  [{(passed ? "ok" : "FAIL")}] {what}");
        if (!passed) failures.Add(what);
    }

    private static void DeleteTestFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
