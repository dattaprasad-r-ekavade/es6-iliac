using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

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

    public void LoadScript(string path, ref string? missing, ref string? exec)
    {
        if (!File.Exists(path))
        {
            missing = path;
            return;
        }

        var joined = string.Join(';', ConsoleRouter.ReadScript(File.ReadAllLines(path)));
        exec = exec is null ? joined : exec + ";" + joined;
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
