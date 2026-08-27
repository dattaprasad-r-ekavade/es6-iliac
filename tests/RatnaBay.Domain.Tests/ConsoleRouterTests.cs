using RatnaBay.Domain;
using System.Linq;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// The console's parsing and dispatch, which is the half that can be tested without a window.
///
/// It matters more than a debug tool usually would: the point of it is that a script can drive
/// the game from outside, so a swallowed argument or a mis-split statement produces a capture
/// of the wrong thing rather than an error anybody notices.
/// </summary>
[TestFixture]
public sealed class ConsoleRouterTests
{
    private static ConsoleRouter WithEcho(out System.Collections.Generic.List<string> seen)
    {
        var calls = new System.Collections.Generic.List<string>();
        seen = calls;

        var console = new ConsoleRouter();
        console.Register("goto", "goto <x> <z>", "Go somewhere.", args =>
        {
            calls.Add($"goto {args.Text(0)} {args.Text(1)}");
            return "moved";
        }, "tp");

        console.Register("say", "say <text>", "Say it.", args =>
        {
            calls.Add("say " + args.Rest(0));
            return args.Rest(0);
        });

        return console;
    }

    [Test]
    public void RunsACommandAndReportsWhatItSaid()
    {
        var console = WithEcho(out var seen);
        var output = console.Execute("goto 4 -9");

        Assert.That(seen, Is.EqualTo(new[] { "goto 4 -9" }));
        Assert.That(output.Any(line => line.Tone == ConsoleTone.Echo), Is.True, "the input is echoed");
        Assert.That(output.Any(line => line.Text == "moved"), Is.True);
    }

    [Test]
    public void AliasesReachTheSameCommand()
    {
        var console = WithEcho(out var seen);
        console.Execute("tp 1 2");

        Assert.That(seen, Is.EqualTo(new[] { "goto 1 2" }));
    }

    /// <summary>Semicolons are what make one --exec able to walk somewhere and then look at it.</summary>
    [Test]
    public void SemicolonsRunSeveralCommandsInOrder()
    {
        var console = WithEcho(out var seen);
        console.Execute("goto 1 2; say hello there; goto 3 4");

        Assert.That(seen, Is.EqualTo(new[] { "goto 1 2", "say hello there", "goto 3 4" }));
    }

    [Test]
    public void QuotesHoldSpacesAndSemicolonsTogether()
    {
        Assert.That(ConsoleRouter.Tokenise("say \"one two\" three"),
            Is.EqualTo(new[] { "say", "one two", "three" }));

        Assert.That(ConsoleRouter.SplitStatements("say \"a; b\"; goto 1"),
            Has.Count.EqualTo(2), "a semicolon inside quotes is text, not a separator");
    }

    [Test]
    public void AnUnknownCommandSuggestsTheNearestOne()
    {
        var console = WithEcho(out _);
        var output = console.Execute("got 1 2");

        Assert.That(output.Any(line => line.Tone == ConsoleTone.Error), Is.True);
        Assert.That(output.Any(line => line.Text.Contains("goto")), Is.True,
            "a typo is likelier than a wrong idea");
    }

    /// <summary>
    /// The console is most needed when something is already broken, so a command that throws
    /// has to be reported rather than allowed to take the game down with it.
    /// </summary>
    [Test]
    public void ACommandThatThrowsIsReportedRatherThanFatal()
    {
        var console = new ConsoleRouter();
        console.Register("boom", "boom", "Throws.", _ => throw new System.InvalidOperationException("no"));

        var output = console.Execute("boom");

        Assert.That(output.Any(line => line.Tone == ConsoleTone.Error && line.Text.Contains("no")),
            Is.True);
    }

    [Test]
    public void ArgumentsParseNumbersAndFallBackWhenTheyAreNotThere()
    {
        var args = new ConsoleArgs("test", new[] { "12", "-3.5", "banana" });

        Assert.That(args.Integer(0), Is.EqualTo(12));
        Assert.That(args.Number(1), Is.EqualTo(-3.5f).Within(0.001f));
        Assert.That(args.Number(2, 99f), Is.EqualTo(99f), "a word is not a number");
        Assert.That(args.Number(7, 42f), Is.EqualTo(42f), "and neither is a missing argument");
    }

    [Test]
    public void SwitchesReadOnOffAndAbsent()
    {
        Assert.That(new ConsoleArgs("t", new[] { "on" }).Switch(0), Is.True);
        Assert.That(new ConsoleArgs("t", new[] { "off" }).Switch(0), Is.False);
        Assert.That(new ConsoleArgs("t", new[] { "0" }).Switch(0), Is.False);
        Assert.That(new ConsoleArgs("t", System.Array.Empty<string>()).Switch(0), Is.Null,
            "absent means toggle");
    }

    [Test]
    public void CompletionOffersRealNamesOnly()
    {
        var console = WithEcho(out _);

        Assert.That(console.Complete("g"), Is.EqualTo(new[] { "goto" }));
        Assert.That(console.Complete("t"), Is.Empty,
            "'tp' is an alias; completing to it would teach the wrong word");
    }

    [Test]
    public void HistoryKeepsLinesButNotRepeats()
    {
        var console = WithEcho(out _);
        console.Execute("goto 1 2");
        console.Execute("goto 1 2");
        console.Execute("say hi");

        Assert.That(console.History, Is.EqualTo(new[] { "goto 1 2", "say hi" }));
    }

    [Test]
    public void BlankInputDoesNothingAtAll()
    {
        var console = WithEcho(out var seen);

        Assert.That(console.Execute("   "), Is.Empty);
        Assert.That(console.History, Is.Empty);
        Assert.That(seen, Is.Empty);
    }

    /// <summary>
    /// One reader for script files, used by both the command line and the 'script' command.
    /// When each had its own copy, a change to the format only ever landed in one of them.
    /// </summary>
    [Test]
    public void ReadingAScriptDropsBlankLinesAndComments()
    {
        var statements = ConsoleRouter.ReadScript(new[]
        {
            "# a comment",
            "",
            "  goto shaft  ",
            "   ",
            "assert pick has shaft",
            "# trailing note"
        });

        Assert.That(statements, Is.EqualTo(new[] { "goto shaft", "assert pick has shaft" }));
    }

    /// <summary>
    /// A scripted gate that half-ran is worse than one that refused: the asserts that never
    /// executed are silent rather than failed, so the run can still exit zero.
    /// </summary>
    [Test]
    public void UnknownCommandsInAScriptAreFoundBeforeItRuns()
    {
        var console = WithEcho(out var seen);

        var unknown = console.UnknownCommands(new[]
        {
            "goto 1 2",
            "teleport 3 4",
            "say hello",
            "descend 3"
        });

        Assert.That(unknown, Is.EqualTo(new[] { "teleport", "descend" }),
            "'tp' is the registered alias, and nothing here registers 'descend'");
        Assert.That(seen, Is.Empty, "checking a script must not run any of it");
    }

    [Test]
    public void AScriptOfKnownCommandsReportsNothingUnknown()
    {
        var console = WithEcho(out _);

        Assert.That(console.UnknownCommands(new[] { "goto 1 2", "tp 3 4", "say hi" }), Is.Empty,
            "aliases count as known");
    }

    [Test]
    public void EachUnknownCommandIsReportedOnce()
    {
        var console = WithEcho(out _);

        Assert.That(console.UnknownCommands(new[] { "nope a", "nope b", "NOPE c" }),
            Is.EqualTo(new[] { "nope" }),
            "a name repeated down a script is one thing to fix, not three");
    }
}
