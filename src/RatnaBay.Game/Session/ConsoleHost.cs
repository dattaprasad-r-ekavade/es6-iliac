using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client.Session;

/// <summary>
/// Run a console line, pump a script one statement per frame, and refresh watches.
///
/// Typing stays on <see cref="ConsoleInput"/>. Game1 still implements
/// <see cref="IConsoleTarget"/> and still owns mouse-look when the console toggles.
/// This type must not take a <c>Game1</c> reference.
/// </summary>
internal sealed class ConsoleHost
{
    public readonly Queue<string> Queue = new();
    public readonly List<ConsoleLine> Output = new();
    public readonly List<string> Watches = new();
    public readonly List<string> WatchOutput = new();

    public ConsoleRouter? Router;
    public float WaitSeconds;
    public bool Failed;
    public bool QuitWhenDone;
    public int ScriptExitCode => Failed ? 1 : 0;

    public void Enqueue(string statements)
    {
        foreach (var statement in ConsoleRouter.SplitStatements(statements))
            Queue.Enqueue(statement);
    }

    /// <summary>
    /// Take a --script or --exec body and make it the queue, or fail the run trying.
    ///
    /// The whole script is checked before its first statement runs. A script naming a command
    /// nothing registered is a script written against a different build, and finding that out
    /// at statement forty means the thirty-nine asserts above it have already reported success
    /// on a run that was never going to finish — a green gate for a build that could not pass.
    ///
    /// <paramref name="missingPath"/> is the file the command line asked for and could not be
    /// read. It fails here rather than at parse time so a missing script exits 1 like a failed
    /// assert does, instead of quietly starting an ordinary game nobody asked for.
    /// </summary>
    public void LoadScript(string? missingPath, string? script, IConsoleTarget target)
    {
        if (missingPath is not null)
        {
            Fail($"No script file '{missingPath}'.");
            QuitWhenDone = true;
            return;
        }

        if (script is null) return;

        var statements = ConsoleRouter.SplitStatements(script);

        Router ??= GameConsole.Build(target);
        var unknown = Router.UnknownCommands(statements);
        if (unknown.Count > 0)
        {
            Fail($"Unknown command(s): {string.Join(", ", unknown)}. Try 'help'.");
            QuitWhenDone = true;
            return;
        }

        foreach (var statement in statements)
            Queue.Enqueue(statement);
    }

    public void Run(string line, IConsoleTarget target)
    {
        Router ??= GameConsole.Build(target);

        foreach (var output in Router.Execute(line))
        {
            if (output.Text == "\f")
            {
                Output.Clear();
                continue;
            }

            Output.Add(output);
        }

        while (Output.Count > 200) Output.RemoveAt(0);
    }

    /// <summary>True when the host asked the game to exit after draining the queue.</summary>
    public bool Pump(float simulatedSeconds, IConsoleTarget target, out bool exit)
    {
        exit = false;
        if (WaitSeconds > 0f)
        {
            WaitSeconds = MathF.Max(0f, WaitSeconds - simulatedSeconds);
            return false;
        }

        if (Queue.Count == 0)
        {
            if (QuitWhenDone)
            {
                QuitWhenDone = false;
                Console.WriteLine(Failed ? "SCRIPT FAILED" : "SCRIPT PASSED");
                exit = true;
            }

            return false;
        }

        var statement = Queue.Dequeue();
        var before = Output.Count;
        Run(statement, target);

        for (var index = before; index < Output.Count; index++)
        {
            var line = Output[index];
            Console.WriteLine((line.Tone == ConsoleTone.Error ? "[!] " : "    ") + line.Text);
        }

        return true;
    }

    public void RefreshWatches()
    {
        WatchOutput.Clear();
        if (Watches.Count == 0 || Router is null) return;

        foreach (var watch in Watches)
            foreach (var line in Router.RunQuiet(watch)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries))
                WatchOutput.Add(line.TrimEnd());
    }

    public void Fail(string why)
    {
        Failed = true;
        Console.WriteLine("[!] " + why);
    }
}
