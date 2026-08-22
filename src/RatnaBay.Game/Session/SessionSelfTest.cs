using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

/// <summary>
/// A headless run of the session layer.
///
/// The domain has 245 tests, but none of them touch a file on disk or the wiring in
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
        var savePath = GameSession.SaveFilePath;
        var backup = BackUpExistingSave(savePath);

        try
        {
            var session = GameSession.NewGame();
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
            Check(failures, "prana does not regenerate",
                Math.Abs(player.Vitals.Prana - (player.Vitals.MaxPrana - 30f)) < 0.01f);

            var expected = Snapshot(session);

            var saveMessage = session.Save();
            Check(failures, $"save succeeds (said: {saveMessage})", saveMessage == "Saved.");
            Check(failures, "a save file exists on disk", File.Exists(savePath));

            // A brand-new session, exactly as a relaunch would build it.
            var reloaded = GameSession.NewGame();
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

            RunFightChecks(failures);
        }
        finally
        {
            RestoreSave(savePath, backup);
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

    /// <summary>
    /// A whole fight, with no window: the bandit closes, both sides trade blows, and the
    /// player wins. This is the iteration 6 gate — winnable, losable, and Blade rising only
    /// on landed hits — checked without anyone having to play it.
    /// </summary>
    private static void RunFightChecks(List<string> failures)
    {
        Console.WriteLine();

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

    private static void Check(ICollection<string> failures, string what, bool passed)
    {
        Console.WriteLine($"  [{(passed ? "ok" : "FAIL")}] {what}");
        if (!passed) failures.Add(what);
    }

    /// <summary>Never clobber a real save while testing.</summary>
    private static string? BackUpExistingSave(string savePath)
    {
        if (!File.Exists(savePath)) return null;
        var backup = savePath + ".selftest-backup";
        File.Copy(savePath, backup, overwrite: true);
        return backup;
    }

    private static void RestoreSave(string savePath, string? backup)
    {
        if (backup is null)
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            return;
        }

        File.Copy(backup, savePath, overwrite: true);
        File.Delete(backup);
    }
}
