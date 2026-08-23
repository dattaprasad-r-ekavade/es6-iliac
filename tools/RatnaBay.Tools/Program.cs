using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RatnaBay.Domain;
using SharpGLTF.Schema2;

var command = args.Length == 0 ? "doctor" : args[0].ToLowerInvariant();
var root = FindRepositoryRoot(Directory.GetCurrentDirectory());

var checks = command switch
{
    "doctor" => RunDoctor(root),
    "validate" => RunValidate(root, args.Skip(1).ToArray()),
    "sim" => RunSimulation(root),
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

static int RunSimulation(string root)
{
    var dialoguePath = Path.Combine(root, "src", "RatnaBay.Game", "Content", "Dialogue", "northwatch.json");
    var questPath = Path.Combine(root, "src", "RatnaBay.Game", "Content", "Quests", "northwatch.json");
    var failures = new List<string>();

    void Check(string label, bool passed)
    {
        Console.WriteLine($"[{(passed ? "OK" : "FAIL")}] {label}");
        if (!passed) failures.Add(label);
    }

    Check("dialogue manifest loads", DialogueManifest.TryLoad(dialoguePath, out var dialogue, out var dialogueError));
    if (dialogue is null)
    {
        Console.Error.WriteLine(dialogueError);
        return 1;
    }

    Check("quest manifest loads", QuestManifest.TryLoad(questPath, out var quests, out var questError));
    if (quests is null)
    {
        Console.Error.WriteLine(questError);
        return 1;
    }

    var player = PlayerCharacter.NewGame();
    player.Dialogue.Load(dialogue.ToTopics());
    player.Quests.RegisterRange(quests.ToDefinitions());
    var maraDefinition = dialogue.Actors.Single(actor => actor.Id == "actor.mara");
    var mara = new SpeakingActor(player.Dialogue, maraDefinition.Id,
        maraDefinition.DisplayName, maraDefinition.FactionId,
        maraDefinition.LocationId, maraDefinition.OpensWith.ToArray());
    var offered = mara.Talk();
    var questTopic = player.Dialogue.Resolve("bandits", mara.Context);
    Check("Mara offers the bandit topic", offered.Contains("bandits") && questTopic is not null);
    Check("the dialogue answer links the quest", questTopic?.QuestId == "quest.northwatch.bandits");
    mara.Ask("bandits");

    var quest = player.Quests.Activate(questTopic?.QuestId);
    Check("dialogue accepts the quest", quest?.IsActive == true);
    for (var index = 0; index < 2; index++) player.Quests.NotifyEnemyKilled("Bandit");
    Check("two bandits complete the quest", quest?.IsCompleted == true);
    Check("the reward pays gold", player.Vitals.Gold == 80);

    var saved = SaveGame.Capture(player, new WorldPoint(0f, 2.4f, -35f), yaw: 0f,
        sceneId: "scene.northwatch");
    var restored = PlayerCharacter.NewGame();
    restored.Quests.RegisterRange(quests.ToDefinitions());
    SaveGame.Restore(restored, saved);
    Check("quest completion survives the save loop",
        restored.Quests.Find("quest.northwatch.bandits")?.IsCompleted == true);
    Check("known dialogue survives the save loop", restored.Dialogue.KnowsTopic("bandits"));

    Console.WriteLine(failures.Count == 0
        ? "Simulation passed: dialogue -> quest -> combat events -> reward -> save -> reload."
        : $"Simulation failed with {failures.Count} problem(s).");
    return failures.Count == 0 ? 0 : 1;
}

static int RunValidate(string root, string[] arguments)
{
    var contentRoot = Path.Combine(root, "src", "RatnaBay.Game", "Content");
    var defaultPaths = new[]
    {
        Path.Combine(contentRoot, "World"),
        Path.Combine(contentRoot, "Dialogue"),
        Path.Combine(contentRoot, "Quests"),
        Path.Combine(contentRoot, "Shops")
    }
    .Where(Directory.Exists)
    .SelectMany(directory => Directory.GetFiles(directory, "*.json"));

    var paths = arguments.Length == 0
        ? defaultPaths.ToArray()
        : arguments.Select(path => Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(root, path))).ToArray();

    if (paths.Length == 0)
    {
        Console.Error.WriteLine("No world or dialogue manifests found.");
        return 1;
    }

    var failures = 0;
    foreach (var path in paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
    {
        var isDialogue = path.Contains(Path.Combine("Content", "Dialogue"),
            StringComparison.OrdinalIgnoreCase);
        var isQuest = path.Contains(Path.Combine("Content", "Quests"),
            StringComparison.OrdinalIgnoreCase);
        var isShop = path.Contains(Path.Combine("Content", "Shops"),
            StringComparison.OrdinalIgnoreCase);
        if (isDialogue)
        {
            if (!DialogueManifest.TryLoad(path, out var dialogue, out var error))
            {
                Console.WriteLine($"[FAIL] {Path.GetRelativePath(root, path)}: {error}");
                failures++;
                continue;
            }

            Console.WriteLine($"[OK] {Path.GetRelativePath(root, path)}: {dialogue!.Id} "
                + $"({dialogue.Actors.Count} actors, {dialogue.Topics.Count} topics)");
            continue;
        }

        if (isQuest)
        {
            if (!QuestManifest.TryLoad(path, out var quests, out var error))
            {
                Console.WriteLine($"[FAIL] {Path.GetRelativePath(root, path)}: {error}");
                failures++;
                continue;
            }

            Console.WriteLine($"[OK] {Path.GetRelativePath(root, path)}: {quests!.Id} "
                + $"({quests.Quests.Count} quests)");
            continue;
        }

        if (isShop)
        {
            if (!ShopManifest.TryLoad(path, out var shops, out var error))
            {
                Console.WriteLine($"[FAIL] {Path.GetRelativePath(root, path)}: {error}");
                failures++;
                continue;
            }

            Console.WriteLine($"[OK] {Path.GetRelativePath(root, path)}: {shops!.Id} "
                + $"({shops.Shops.Count} shops, {shops.Shops.Sum(shop => shop.Items.Count)} items)");
            continue;
        }

        if (!WorldManifest.TryLoad(path, out var manifest, out var worldError))
        {
            Console.WriteLine($"[FAIL] {Path.GetRelativePath(root, path)}: {worldError}");
            failures++;
            continue;
        }

        Console.WriteLine($"[OK] {Path.GetRelativePath(root, path)}: {manifest!.Id} "
            + $"({manifest.Geometry.Count} geometry, {manifest.Props.Count} props, "
            + $"{manifest.Doors.Count} doors, {manifest.Watchers.Count} watchers, "
            + $"{manifest.Pickups.Count} pickups)");
    }

    Console.WriteLine(failures == 0
        ? "World, dialogue, quest, shop and pickup content is valid."
        : $"{failures} content manifest(s) failed validation.");
    return failures == 0 ? 0 : 1;
}

static int PrintHelp()
{
    Console.WriteLine("Ratna Bay tools");
    Console.WriteLine("  doctor     Check the repository/toolchain baseline (default)");
    Console.WriteLine("  validate   Validate JSON world, dialogue, quest and shop manifests (optionally pass a path)");
    Console.WriteLine("  sim        Run the dialogue -> quest -> combat -> reward -> save regression");
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
