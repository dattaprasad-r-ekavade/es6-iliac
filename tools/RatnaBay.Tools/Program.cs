using System;
using System.Collections.Generic;
using System.IO;

var command = args.Length == 0 ? "doctor" : args[0].ToLowerInvariant();
var root = FindRepositoryRoot(Directory.GetCurrentDirectory());

var checks = command switch
{
    "doctor" or "validate" => RunDoctor(root),
    "help" or "--help" or "-h" => PrintHelp(),
    _ => Unknown(command)
};

return checks;

static int RunDoctor(string root)
{
    var required = new[]
    {
        "RatnaBay.sln",
        "global.json",
        "build.ps1",
        "src/RatnaBay.Domain/RatnaBay.Domain.csproj",
        "src/RatnaBay.Game/RatnaBay.Game.csproj",
        "src/RatnaBay.Game/Content/Content.mgcb",
        "src/RatnaBay.Game/.config/dotnet-tools.json",
        "tests/RatnaBay.Domain.Tests/RatnaBay.Domain.Tests.csproj"
    };

    var failures = new List<string>();
    Console.WriteLine($"Ratna Bay tool doctor: {root}");
    foreach (var relative in required)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        var exists = File.Exists(path) || Directory.Exists(path);
        Console.WriteLine($"[{(exists ? "OK" : "FAIL")}] {relative}");
        if (!exists)
            failures.Add(relative);
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"{failures.Count} required path(s) are missing.");
        return 1;
    }

    Console.WriteLine("Toolchain baseline is valid.");
    return 0;
}

static int PrintHelp()
{
    Console.WriteLine("Ratna Bay tools");
    Console.WriteLine("  doctor     Check the repository/toolchain baseline (default)");
    Console.WriteLine("  validate   Alias for doctor; future source-data validation entry point");
    Console.WriteLine("  help       Show this help");
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'. Use 'help'.");
    return 2;
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "RatnaBay.sln")))
            return directory.FullName;
        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find RatnaBay.sln from the current directory.");
}
