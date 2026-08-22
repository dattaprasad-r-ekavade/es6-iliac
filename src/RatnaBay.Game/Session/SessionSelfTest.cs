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
