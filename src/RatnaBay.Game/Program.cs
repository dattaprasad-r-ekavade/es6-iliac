using RatnaBay.Client;
using System;
using System.Linq;

// --selftest runs the session layer with no window: it proves the domain is wired to the
// save file on disk without a human pressing F5. This is the seed of the headless sim
// harness the production plan calls for.
if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
    return SessionSelfTest.Run();

// --dump-sfx writes every synthesised sound to .wav and exits. Audio is the one part of this
// project that cannot be checked by looking at it: a sound that is silent, clipped, or three
// seconds of hiss builds fine and passes every test. Needs no window and no audio device,
// because the synthesis is pure arithmetic and only playback needs hardware.
var dumpIndex = Array.FindIndex(args,
    a => string.Equals(a, "--dump-sfx", StringComparison.OrdinalIgnoreCase));

if (dumpIndex >= 0 && dumpIndex + 1 < args.Length)
{
    var directory = args[dumpIndex + 1];
    var written = SoundBank.Dump(directory);

    Console.WriteLine($"Wrote {written} sounds to {System.IO.Path.GetFullPath(directory)}");
    return 0;
}

using var game = new Game1(args);
game.Run();
return 0;
