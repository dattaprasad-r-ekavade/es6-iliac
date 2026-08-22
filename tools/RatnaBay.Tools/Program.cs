using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpGLTF.Schema2;

var command = args.Length == 0 ? "doctor" : args[0].ToLowerInvariant();
var root = FindRepositoryRoot(Directory.GetCurrentDirectory());

var checks = command switch
{
    "doctor" => RunDoctor(root),
    "validate" => RunDoctor(root),
    "asset-info" => RunAssetInfo(root, args.Skip(1).ToArray()),
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
        "src/RatnaBay.Game/pipeline-references/MonoGame.Extended.Content.Pipeline.dll",
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

    var packageChecks = new[]
    {
        ("game", "MonoGame.Extended"),
        ("game", "Gum.MonoGame"),
        ("game", "ImGui.NET"),
        ("game", "FontStashSharp.MonoGame"),
        ("game", "BepuPhysics"),
        ("game", "DotRecast.Recast"),
        ("game", "Ink"),
        ("tools", "SharpGLTF.Core"),
        ("tools", "SharpGLTF.Toolkit")
    };

    foreach (var (project, package) in packageChecks)
    {
        var assetsPath = project == "game"
            ? Path.Combine(root, "src", "RatnaBay.Game", "obj", "project.assets.json")
            : Path.Combine(root, "tools", "RatnaBay.Tools", "obj", "project.assets.json");
        var present = File.Exists(assetsPath) &&
            File.ReadAllText(assetsPath).Contains($"\"{package}/", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[{(present ? "OK" : "FAIL")}] package {package}");
        if (!present)
            failures.Add($"package {package}");
    }

    if (failures.Count > 0)
    {
        Console.Error.WriteLine($"{failures.Count} required path or package check(s) failed.");
        return 1;
    }

    Console.WriteLine("Toolchain and community package baseline is valid.");
    return 0;
}

static int RunAssetInfo(string root, string[] arguments)
{
    if (arguments.Length == 0)
    {
        Console.Error.WriteLine("Usage: asset-info <path-to-gltf-or-glb>");
        return 2;
    }

    var path = Path.GetFullPath(Path.IsPathRooted(arguments[0])
        ? arguments[0]
        : Path.Combine(root, arguments[0]));
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Asset not found: {path}");
        return 1;
    }

    try
    {
        var model = ModelRoot.Load(path);
        Console.WriteLine($"Loaded glTF asset: {path}");
        Console.WriteLine($"  Scenes: {model.LogicalScenes.Count}");
        Console.WriteLine($"  Nodes: {model.LogicalNodes.Count}");
        Console.WriteLine($"  Meshes: {model.LogicalMeshes.Count}");
        Console.WriteLine($"  Materials: {model.LogicalMaterials.Count}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Could not read glTF asset: {exception.Message}");
        return 1;
    }
}

static int PrintHelp()
{
    Console.WriteLine("Ratna Bay tools");
    Console.WriteLine("  doctor     Check the repository/toolchain baseline (default)");
    Console.WriteLine("  validate   Alias for doctor; future source-data validation entry point");
    Console.WriteLine("  asset-info Inspect a .gltf or .glb asset using SharpGLTF");
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
