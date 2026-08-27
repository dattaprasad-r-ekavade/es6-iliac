using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// What the console is allowed to do to the running game.
///
/// A narrow surface rather than handing the command table the whole of <c>Game1</c>: every verb
/// below is one the game already performs somewhere, and anything not listed here is something
/// the console deliberately cannot reach.
/// </summary>
internal interface IConsoleTarget
{
    GameSession? Session { get; }
    Encounter? Encounter { get; }
    RunRuntime? Run { get; }
    WorldRuntime? World { get; }

    Vector3 CameraPosition { get; }
    float CameraYaw { get; }
    float CameraPitch { get; }

    /// <summary>Put the player somewhere, camera and session together.</summary>
    void PlaceAt(Vector3 position);

    void LookAt(float yaw, float pitch);

    /// <summary>Open a descent at this tier, as the shaft panel would.</summary>
    void Descend(int tier, int? seed);

    /// <summary>Back to the yard, abandoning whatever is underway.</summary>
    void Surface();

    /// <summary>Free movement through walls, for looking at things from impossible angles.</summary>
    bool NoClip { get; set; }

    /// <summary>Damage is counted and reported but never applied.</summary>
    bool Invulnerable { get; set; }

    /// <summary>Hide every panel and marker, for a clean picture.</summary>
    bool HideInterface { get; set; }

    /// <summary>How fast simulated time runs. 1 is normal, 0 freezes, 0.2 is slow motion.</summary>
    float TimeScale { get; set; }

    /// <summary>Hold the rest of a script for this much simulated time.</summary>
    void WaitSeconds(float seconds);

    /// <summary>Record that an assertion failed, so the process can exit non-zero.</summary>
    void FailScript(string why);

    /// <summary>Ask to quit once the script runs out.</summary>
    void QuitWhenDone();

    /// <summary>Pin a command to the screen, re-run every frame. Empty clears them.</summary>
    void Watch(string? command);

    IReadOnlyList<string> Watches { get; }

    /// <summary>Queue more statements, for 'script' reading a file.</summary>
    void Queue(string statements);

    /// <summary>Take a picture now, from wherever the camera is.</summary>
    string Capture(string path);

    /// <summary>
    /// What is under the crosshair, or under one screen pixel when given.
    ///
    /// The pixel form is what makes a screenshot answerable: something odd in a captured
    /// frame can be named by its coordinates instead of by nudging the camera at it.
    /// </summary>
    string PickAt(int? screenX, int? screenY);

    void Say(string message);
}

/// <summary>
/// The command table.
///
/// Built for two readers. A player gets the thing every Bethesda game has — a key, a prompt,
/// and enough rope to put themselves anywhere. The more important reader is a script: with
/// <c>--exec</c> the same commands run before a capture, which is what makes the game
/// inspectable from outside. Several fixes to the yard and the mine had to be argued from the
/// contents of a JSON manifest because a screenshot could only ever be taken from the spawn
/// point and there was no way to walk anywhere. That is the gap this closes.
/// </summary>
internal static class GameConsole
{
    /// <summary>The yard's landmark names, as help should list them.</summary>
    private static string LandmarkNames => string.Join(", ", Surface.Landmarks.Keys);

    public static ConsoleRouter Build(IConsoleTarget game)
    {
        var console = new ConsoleRouter();

        // ------------------------------------------------------------------ getting about

        console.Register("goto",
            "goto <landmark|x y z>",
            "Stand somewhere. Landmarks: " + LandmarkNames,
            args =>
            {
                if (args.Count == 0) return "goto where? " + LandmarkNames;

                if (Surface.TryLandmark(args.Text(0), out var landmark))
                {
                    // Stood beside it rather than inside it: a landmark is a thing to look at.
                    var offset = landmark.Z > 0f ? -3.2f : 3.2f;
                    game.PlaceAt(new Vector3(landmark.X, PlayerEye, landmark.Z + offset));
                    return $"At the {args.Text(0)} ({landmark.X:0.0}, {landmark.Z + offset:0.0}).";
                }

                if (!args.TryNumber(0, out var x) || !args.TryNumber(1, out var y))
                    return $"'{args.Text(0)}' is not a landmark or a coordinate.";

                // Two numbers means x and z at standing height, which is what is wanted almost
                // every time; three means the caller meant a height as well.
                var position = args.Count >= 3 && args.TryNumber(2, out var z)
                    ? new Vector3(x, y, z)
                    : new Vector3(x, PlayerEye, y);

                game.PlaceAt(position);
                return $"At {position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}.";
            },
            "tp", "teleport");

        console.Register("move",
            "move <forward> [right] [up]",
            "Step relative to where you are facing, in metres.",
            args =>
            {
                var forward = args.Number(0);
                var right = args.Number(1);
                var up = args.Number(2);

                var yaw = game.CameraYaw;
                var position = game.CameraPosition
                    + new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw)) * forward
                    + new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw)) * right
                    + new Vector3(0f, up, 0f);

                game.PlaceAt(position);
                return $"At {position.X:0.0}, {position.Y:0.0}, {position.Z:0.0}.";
            });

        console.Register("look",
            "look <yaw> [pitch]  |  look at <landmark>",
            "Face a direction in radians, or turn to face a landmark.",
            args =>
            {
                if (string.Equals(args.Text(0), "at", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Surface.TryLandmark(args.Text(1), out var landmark))
                        return $"No landmark '{args.Text(1)}'. Try: {LandmarkNames}.";

                    var here = game.CameraPosition;
                    var yaw = MathF.Atan2(-(landmark.X - here.X), -(landmark.Z - here.Z));
                    game.LookAt(yaw, args.Number(2, -0.05f));
                    return $"Facing the {args.Text(1)} (yaw {yaw:0.00}).";
                }

                game.LookAt(args.Number(0, game.CameraYaw), args.Number(1, game.CameraPitch));
                return $"Yaw {game.CameraYaw:0.00}, pitch {game.CameraPitch:0.00}.";
            });

        console.Register("where",
            "where",
            "Say where you are, what you are facing, and what room you are in.",
            _ =>
            {
                var here = game.CameraPosition;
                var room = game.Run?.Run is { IsActive: true } run
                    ? $"room {run.RoomsCleared}, {run.Pending} at risk"
                    : "the yard";

                return $"{here.X:0.0}, {here.Y:0.0}, {here.Z:0.0}  ·  "
                    + $"yaw {game.CameraYaw:0.00} pitch {game.CameraPitch:0.00}  ·  {room}";
            },
            "pos");

        // ------------------------------------------------------------------ the run

        console.Register("descend",
            "descend [tier] [seed]",
            "Open a mine and go down it, paying nothing.",
            args =>
            {
                var tier = Math.Clamp(args.Integer(0, 1), MineEntry.MinTier, MineEntry.MaxTier);
                int? seed = args.TryInteger(1, out var given) ? given : null;

                game.Descend(tier, seed);
                return $"Tier {tier}" + (seed is null ? "." : $", seed {seed}.");
            },
            "mine");

        console.Register("surface",
            "surface",
            "Abandon whatever is underway and return to the yard.",
            _ =>
            {
                game.Surface();
                return "Back in the yard.";
            },
            "up");

        // ------------------------------------------------------------------ the body

        console.Register("god",
            "god [on|off]",
            "Take no damage. Hits are still counted.",
            args =>
            {
                game.Invulnerable = args.Switch(0) ?? !game.Invulnerable;
                return game.Invulnerable ? "Nothing can hurt you." : "Mortal again.";
            });

        console.Register("noclip",
            "noclip [on|off]",
            "Walk through walls.",
            args =>
            {
                game.NoClip = args.Switch(0) ?? !game.NoClip;
                return game.NoClip ? "Walls are suggestions." : "Solid again.";
            },
            "ghost");

        console.Register("heal",
            "heal [amount]",
            "Restore health, prana and stamina. No amount means all of it.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";

                var vitals = session.Player.Vitals;
                if (args.TryNumber(0, out var amount))
                {
                    vitals.Heal(amount);
                    return $"Health {vitals.Health:0} / {vitals.MaxHealth:0}.";
                }

                vitals.Heal(vitals.MaxHealth);
                vitals.RestorePrana(vitals.MaxPrana);
                return $"Whole again: {vitals.Health:0} health, {vitals.Prana:0} prana.";
            });

        console.Register("gold",
            "gold <amount>",
            "Add gold. Negative takes it away.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";

                var amount = args.Integer(0, 100);
                session.Player.Vitals.AddGold(amount);
                return $"{session.Player.Vitals.Gold} gold.";
            });

        console.Register("give",
            "give <item id> [count]",
            "Put something in the pack. Try 'give health_potion 5'.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";
                if (args.Count == 0) return "give what?";

                var id = args.Text(0);
                var count = Math.Max(1, args.Integer(1, 1));

                session.Player.Inventory.Add(id, PrettyName(id), count, KindOf(id));
                return $"{count} x {PrettyName(id)}.";
            });

        console.Register("stone",
            "stone <name|list> ",
            "Find a stone, as a kill below would drop it. 'stone list' names them.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";

                var slots = session.Player.Stones;
                var wanted = args.Text(0).ToLowerInvariant();

                if (wanted is "" or "list")
                    return "Stones: " + string.Join(", ", StoneCatalog.All.Select(s => s.Id))
                        + $"\nSocketed {slots.Socketed.Count} / {slots.Capacity}, "
                        + $"{slots.Loose.Count} loose.";

                // Typed either way round: 'stone cinder' and 'stone stone.cinder' both work.
                var id = wanted.StartsWith("stone.", StringComparison.Ordinal)
                    ? wanted
                    : "stone." + wanted;

                if (StoneCatalog.Find(id) is null) return $"No stone '{id}'. Try 'stone list'.";

                slots.Found(id);
                return slots.Socketed.Contains(id)
                    ? $"{id} socketed ({slots.Socketed.Count} / {slots.Capacity})."
                    : $"{id} in the pack; no socket free.";
            });

        console.Register("stones",
            "stones <count>",
            "Add jiva stones to the pack.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";

                var count = Math.Max(1, args.Integer(0, 10));
                session.Player.Inventory.Add(SoulCrystals.LesserId, "Lesser Jiva Stone", count,
                    SoulCrystals.ItemKind);
                return $"{session.Player.Inventory.CountOf(SoulCrystals.LesserId)} stones.";
            });

        // ------------------------------------------------------------------ what is in the room

        console.Register("kill",
            "kill [all]",
            "Kill what the crosshair is on, or everything in the room.",
            args =>
            {
                if (game.Encounter is not { } encounter) return "Nothing is fighting.";

                var alive = encounter.Enemies.Where(enemy => enemy.IsAlive).ToList();
                if (alive.Count == 0) return "The room is already clear.";

                if (string.Equals(args.Text(0), "all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var enemy in alive) enemy.TakeDamage(enemy.Health + 1f, "console");
                    return $"{alive.Count} down.";
                }

                // Nearest to the crosshair, which is what "that one" means in practice.
                var here = new WorldPoint(game.CameraPosition.X, game.CameraPosition.Y,
                    game.CameraPosition.Z);
                var target = alive.OrderBy(enemy => enemy.Position.FlatDistanceTo(here)).First();

                target.TakeDamage(target.Health + 1f, "console");
                return $"{target.DisplayName} down.";
            });

        console.Register("enemies",
            "enemies",
            "List what is in the room and how far away it is.",
            _ =>
            {
                if (game.Encounter is not { } encounter) return "Nothing is fighting.";

                var here = new WorldPoint(game.CameraPosition.X, game.CameraPosition.Y,
                    game.CameraPosition.Z);
                var alive = encounter.Enemies.Where(enemy => enemy.IsAlive).ToList();
                if (alive.Count == 0) return "The room is clear.";

                return string.Join('\n', alive
                    .OrderBy(enemy => enemy.Position.FlatDistanceTo(here))
                    .Select(enemy =>
                        $"{enemy.DisplayName,-16} {enemy.Health:0} hp  "
                        + $"{enemy.Position.FlatDistanceTo(here):0.0} m  "
                        // Rousing is worth naming: a body still coming up out of the floor is
                        // drawn sunk into it, so one stuck part-way reads as a sliver of colour
                        // lying on the stone rather than as an enemy.
                        + (enemy.IsRousing ? $"rising {enemy.RousedFraction:0.00}  " : "")
                        + (enemy.IsStaggered ? "staggered  " : "")
                        + $"y {enemy.Position.Y:0.00}"));
            });

        console.Register("doors",
            "doors [open]",
            "List the doors in this world, or open all of them.",
            args =>
            {
                if (game.World is not { } world) return "No world.";
                if (world.Doors.Count == 0) return "No doors.";

                if (string.Equals(args.Text(0), "open", StringComparison.OrdinalIgnoreCase))
                {
                    var opened = 0;
                    foreach (var door in world.Doors.Where(door => !door.Lock.IsOpen))
                    {
                        door.Lock.ForceOpen();
                        opened++;
                    }

                    return $"{opened} opened.";
                }

                return string.Join('\n', world.Doors.Select(door =>
                    $"{(door.Lock.IsOpen ? "open  " : "shut  ")}{door.Definition.Id}"));
            });

        // ------------------------------------------------------------------ looking at it

        console.Register("geo",
            "geo [radius]",
            "List the geometry near you: id, box, material, and whether it is drawn.",
            args =>
            {
                if (game.World is not { } world) return "No world.";

                var radius = args.Number(0, 6f);
                var here = game.CameraPosition;

                var near = world.Manifest.Geometry
                    .Select(box => (Box: box, Gap: GapTo(box, here)))
                    .Where(pair => pair.Gap <= radius)
                    .OrderBy(pair => pair.Gap)
                    .Take(24)
                    .ToList();

                if (near.Count == 0) return $"Nothing within {radius:0.0} m.";

                return string.Join('\n', near.Select(pair =>
                    $"{pair.Gap,5:0.0} {pair.Box.Id,-34} "
                    + $"y {pair.Box.Min.Y,6:0.0}..{pair.Box.Max.Y,-6:0.0} "
                    + $"{pair.Box.Material}{(pair.Box.Visible ? "" : " (unseen)")}"
                    + $"{(pair.Box.Solid ? "" : " (open)")}"));
            });

        console.Register("hud",
            "hud [on|off]",
            "Hide the interface, for a clean picture.",
            args =>
            {
                game.HideInterface = args.Switch(0) is { } wanted ? !wanted : !game.HideInterface;
                return game.HideInterface ? "Interface hidden." : "Interface back.";
            });

        console.Register("echo",
            "echo <text>",
            "Say something back. Useful for marking a spot in a script's output.",
            args => args.Rest(0));

        // ------------------------------------------------------------------ scripting

        console.Register("wait",
            "wait [seconds]",
            "Hold the rest of the script while the game runs. Default 1 second.",
            args =>
            {
                // Seconds, not frames: capture mode runs uncapped, so a frame count buys an
                // unpredictable and usually tiny amount of game time.
                var seconds = Math.Clamp(args.Number(0, 1f), 0.01f, 120f);
                game.WaitSeconds(seconds);
                return $"Waiting {seconds:0.00}s.";
            });

        console.Register("assert",
            "assert <command> has <text>   |   assert <command> not <text>",
            "Run a command and check its answer. A failure makes the process exit non-zero.",
            args =>
            {
                // Split on the keyword rather than by position, so the command being checked
                // can itself take arguments.
                var words = Enumerable.Range(0, args.Count)
                    .Select(index => args.Text(index)).ToList();
                var at = words.FindIndex(word =>
                    string.Equals(word, "has", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(word, "not", StringComparison.OrdinalIgnoreCase));

                if (at <= 0 || at == words.Count - 1)
                    return "assert <command> has <text>   |   assert <command> not <text>";

                var wantPresent = string.Equals(words[at], "has", StringComparison.OrdinalIgnoreCase);
                var statement = string.Join(' ', words.Take(at));
                var needle = string.Join(' ', words.Skip(at + 1));

                var answer = console.RunQuiet(statement);
                var present = answer.Contains(needle, StringComparison.OrdinalIgnoreCase);

                if (present == wantPresent) return $"ok   {statement} {words[at]} {needle}";

                // The answer is included because "it did not contain X" is not enough to fix
                // anything; what it contained instead is the useful half.
                var why = $"FAIL {statement} {words[at]} {needle}"
                    + $"\n     got: {answer.Replace('\n', '/')}";

                game.FailScript(why);
                return why;
            });

        console.Register("script",
            "script <path>",
            "Read a file of commands and queue them. One statement per line; # is a comment.",
            args =>
            {
                var path = args.Rest(0);
                if (string.IsNullOrWhiteSpace(path)) return "script what?";
                if (!File.Exists(path)) return $"No file '{path}'.";

                var lines = ConsoleRouter.ReadScript(File.ReadAllLines(path));

                game.Queue(string.Join(';', lines));
                return $"{lines.Count} statements queued from {Path.GetFileName(path)}.";
            });

        console.Register("quit",
            "quit",
            "Leave once the script is done, exiting non-zero if any assert failed.",
            _ =>
            {
                game.QuitWhenDone();
                return "Quitting when the script runs out.";
            },
            "exit");

        console.Register("shot",
            "shot <path>",
            "Save a picture now, without ending the run.",
            args =>
            {
                var path = args.Rest(0);
                return string.IsNullOrWhiteSpace(path) ? "shot where?" : game.Capture(path);
            },
            "capture");

        // ------------------------------------------------------------------ live debugging

        console.Register("watch",
            "watch <command>  |  watch off",
            "Pin a command to the screen and re-run it every frame.",
            args =>
            {
                var what = args.Rest(0);

                if (what.Length == 0)
                    return game.Watches.Count == 0
                        ? "Nothing watched."
                        : "Watching: " + string.Join(" | ", game.Watches);

                if (string.Equals(what, "off", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(what, "clear", StringComparison.OrdinalIgnoreCase))
                {
                    game.Watch(null);
                    return "Watches cleared.";
                }

                game.Watch(what);
                return $"Watching '{what}'.";
            });

        console.Register("pick",
            "pick [x y]",
            "Say what the crosshair is on, or what is at a screen pixel of the last frame.",
            args => args.Count >= 2 && args.TryInteger(0, out var px) && args.TryInteger(1, out var py)
                ? game.PickAt(px, py)
                : game.PickAt(null, null));

        console.Register("time",
            "time [scale]",
            "Slow the simulation down. 1 is normal, 0.2 is slow motion, 0 freezes it.",
            args =>
            {
                if (args.Count == 0) return $"Time runs at {game.TimeScale:0.00}.";

                game.TimeScale = Math.Clamp(args.Number(0, 1f), 0f, 4f);
                return $"Time runs at {game.TimeScale:0.00}.";
            });

        console.Register("hurt",
            "hurt [amount]",
            "Take damage, to see what being hit looks like.",
            args =>
            {
                if (game.Session is not { } session) return "No session.";

                var amount = args.Number(0, 20f);
                var dealt = session.Player.Combat.TakeHit(amount);
                return $"Took {dealt:0}. {session.Player.Vitals.Health:0} health left.";
            });

        console.Register("clear",
            "clear",
            "Empty the console log.",
            _ =>
            {
                game.Say(string.Empty);
                return "\x0c";
            },
            "cls");

        console.Register("help",
            "help [command]",
            "This.",
            args => console.Help(args.Count > 0 ? args.Text(0) : null),
            "?", "commands");

        return console;
    }

    /// <summary>Distance from a point to the nearest face of a box, zero when inside it.</summary>
    private static float GapTo(WorldGeometry box, Vector3 point)
    {
        var dx = MathF.Max(0f, MathF.Max(box.Min.X - point.X, point.X - box.Max.X));
        var dy = MathF.Max(0f, MathF.Max(box.Min.Y - point.Y, point.Y - box.Max.Y));
        var dz = MathF.Max(0f, MathF.Max(box.Min.Z - point.Z, point.Z - box.Max.Z));

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Standing eye height, read from the authored spawn rather than repeated as a number.
    ///
    /// It was typed here as well, which meant a change to how tall the player is put the
    /// console's idea of standing somewhere underground or in the air.
    /// </summary>
    private static float PlayerEye => Surface.Spawn.Y;

    /// <summary>A readable name from an id, for things given by id rather than chosen.</summary>
    private static string PrettyName(string id)
    {
        var words = id.Replace('.', ' ').Replace('_', ' ').Split(' ',
            StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', words.Select(word =>
            char.ToUpperInvariant(word[0]) + word[1..]));
    }

    /// <summary>Enough of a guess at kind for the inventory to file it sensibly.</summary>
    private static string KindOf(string id) => id switch
    {
        _ when id.Contains("potion", StringComparison.OrdinalIgnoreCase) => "potion",
        _ when id.Contains("arrow", StringComparison.OrdinalIgnoreCase) => "ammunition",
        _ when id.Contains("sword", StringComparison.OrdinalIgnoreCase)
            || id.Contains("mace", StringComparison.OrdinalIgnoreCase)
            || id.Contains("bow", StringComparison.OrdinalIgnoreCase) => "weapon",
        _ => "misc"
    };
}
