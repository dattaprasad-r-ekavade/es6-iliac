using RatnaBay.Client;
using System;
using System.Linq;

// --selftest runs the session layer with no window: it proves the domain is wired to the
// save file on disk without a human pressing F5. This is the seed of the headless sim
// harness the production plan calls for.
if (args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
    return SessionSelfTest.Run();

using var game = new Game1(args);
game.Run();
return 0;
