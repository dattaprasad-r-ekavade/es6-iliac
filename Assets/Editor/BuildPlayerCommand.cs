using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless Windows player build, so builds can run from CI or a terminal:
///
///   Unity.exe -batchmode -quit -projectPath . -executeMethod BuildPlayerCommand.BuildWindows
/// </summary>
public static class BuildPlayerCommand
{
    private const string OutputPath = "Builds/Windows/Kessil.exe";

    [MenuItem("Kessil/Build/Windows Player")]
    public static void BuildWindows()
    {
        // Derived, never hand-listed. This method used to maintain its own copy of the scene
        // list, which is how the region shipped missing from the player while being present in
        // EditorBuildSettings — so every editor test passed and the actual build stranded the
        // player on New Game.
        var allScenes = SceneArchitectureBuilder.ShippingScenePaths()
            .FindAll(File.Exists);

        if (allScenes.Count == 0)
        {
            Debug.LogError("[Build] No shipping scenes exist on disk. Regenerate them first.");
            EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var options = new BuildPlayerOptions
        {
            scenes = allScenes.ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Build] Succeeded: {OutputPath} " +
                      $"({summary.totalSize / 1024f / 1024f:0.0} MB, {summary.totalTime.TotalSeconds:0.0}s)");
        }
        else
        {
            Debug.LogError($"[Build] {summary.result}: {summary.totalErrors} error(s).");
            EditorApplication.Exit(1);
        }
    }
}
