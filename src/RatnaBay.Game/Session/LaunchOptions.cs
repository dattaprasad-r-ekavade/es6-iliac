using System;
using System.IO;
using System.Linq;

namespace RatnaBay.Client.Session;

/// <summary>
/// What the command line asked this sitting to open: a mine, the yard, a spike scene, a
/// capture pose, a script.
///
/// Capture window size, uncapped frames and <c>--perf</c> stay on <c>EngineHost</c>.
/// <c>--show</c> / <c>--swing</c> / <c>--cast</c> are this game's panels and weapon pose.
/// </summary>
internal sealed class LaunchOptions
{
    public GameScreen Screen { get; init; } = GameScreen.MainMenu;
    public string? FacesPath { get; init; }
    public string? FaceOnly { get; init; }
    public int FaceSheetScale { get; init; } = 2;
    public bool ForceCrouch { get; init; }
    public float? CaptureSwing { get; init; }
    public float? CaptureCast { get; init; }
    public string? CaptureScreen { get; init; }
    public bool StambhaPreview { get; init; }
    public string? ConsoleScript { get; init; }
    public string? ScriptMissing { get; init; }

    /// <summary>
    /// True when --script named a file, whether or not it could be read.
    ///
    /// Separate from ConsoleScript because a scripted run is a *kind of run* rather than a
    /// body of commands: it gates mouse capture, fullscreen and telemetry, and it has to do
    /// that even when the script turns out to be missing.
    /// </summary>
    public bool Scripted { get; init; }
    public bool StartOnTheSurface { get; init; }
    public bool Moodboard { get; init; }
    public bool AssetCase { get; init; }
    public int? MineSeed { get; init; }
    public int? MineRooms { get; init; }
    public int? MineDepth { get; init; }
    public float? StartYaw { get; init; }
    public float? StartPitch { get; init; }
    public bool CoverMode { get; init; }

    public static LaunchOptions Parse(string[] args, bool coverMode,
        Func<string[], string, string?> option,
        Func<string[], string, bool> flag)
    {
        var screen = GameScreen.MainMenu;
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--mode", StringComparison.OrdinalIgnoreCase))
                continue;
            screen = args[index + 1].ToLowerInvariant() switch
            {
                "menu" or "title" => GameScreen.MainMenu,
                "scene" or "game" or "world" => GameScreen.WorldScene,
                _ => GameScreen.MainMenu
            };
        }

        float? captureSwing = float.TryParse(option(args, "--swing"), out var swing) ? swing : null;
        float? captureCast = float.TryParse(option(args, "--cast"), out var cast) ? cast : null;
        var consoleScript = option(args, "--exec");
        string? scriptMissing = null;
        var scriptFile = option(args, "--script");
        if (scriptFile is not null)
        {
            if (!File.Exists(scriptFile))
                scriptMissing = scriptFile;
            else
            {
                var joined = string.Join(';', File.ReadAllLines(scriptFile)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith('#')));
                consoleScript = consoleScript is null ? joined : consoleScript + ";" + joined;
            }
        }

        var moodboard = flag(args, "--moodboard");
        var assetCase = flag(args, "--assets");
        if (assetCase) moodboard = true;

        int? mineSeed = int.TryParse(option(args, "--mine"), out var seed) ? seed : null;
        int? mineRooms = int.TryParse(option(args, "--rooms"), out var rooms) ? rooms : null;
        int? mineDepth = int.TryParse(option(args, "--depth"), out var depth) ? depth : null;
        if (mineSeed is not null) screen = GameScreen.WorldScene;

        var stambha = flag(args, "--stambha");
        var startOnSurface = flag(args, "--yard");
        if (consoleScript is not null && mineSeed is null && !moodboard && !stambha
            && screen == GameScreen.MainMenu)
            startOnSurface = true;

        float? yaw = float.TryParse(option(args, "--yaw"), out var parsedYaw) ? parsedYaw : null;
        float? pitch = float.TryParse(option(args, "--pitch"), out var parsedPitch) ? parsedPitch : null;

        var faceScale = 2;
        if (int.TryParse(option(args, "--face-scale"), out var parsedScale))
            faceScale = Math.Clamp(parsedScale, 1, 8);

        if (coverMode)
        {
            mineSeed ??= 20789;
            mineDepth = 4;
            screen = GameScreen.WorldScene;
            pitch ??= -0.06f;
        }

        return new LaunchOptions
        {
            Screen = screen,
            FacesPath = option(args, "--faces"),
            FaceOnly = option(args, "--face"),
            FaceSheetScale = faceScale,
            ForceCrouch = flag(args, "--sneak"),
            CaptureSwing = captureSwing,
            CaptureCast = captureCast,
            CaptureScreen = option(args, "--show"),
            StambhaPreview = stambha,
            ConsoleScript = consoleScript,
            ScriptMissing = scriptMissing,
            Scripted = !string.IsNullOrWhiteSpace(scriptFile),
            StartOnTheSurface = startOnSurface,
            Moodboard = moodboard,
            AssetCase = assetCase,
            MineSeed = mineSeed,
            MineRooms = mineRooms,
            MineDepth = mineDepth,
            StartYaw = yaw,
            StartPitch = pitch,
            CoverMode = coverMode
        };
    }
}
