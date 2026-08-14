using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the game from where the player actually stands, and writes the frames to disk.
///
/// **Why this exists.** No agent working on this project has ever seen it running. Every
/// judgement about how it looked was inferred from code and coordinates, and every defect that
/// made it unplayable — drowning in interiors, a four-metre trench at the spawn, 250 m of
/// nothing between the spawn and the only authored street — was found by the developer pressing
/// Play. All of them are obvious in a single frame.
///
/// Deliberately an *editor* capture rather than a simulated play session. Driving the real
/// CharacterController headlessly is fragile; putting a camera at eye height where the player
/// would be standing is not, and it answers the same question: what does this look like from
/// there. The behavioural half — can the player stand there, get out again, and quit — belongs
/// in <c>PlayabilitySmokeTests</c>, which runs the real controller.
///
/// Headless (graphics must stay enabled for the PNGs):
/// Unity.exe -batchmode -quit -projectPath . -executeMethod PlaythroughCapture.CaptureAll
/// </summary>
public static class PlaythroughCapture
{
    public const string OutputFolder = "Docs/Screenshots/Playthrough";

    private const int Width = 1280;
    private const int Height = 720;

    /// <summary>Player eye height above the ground they stand on. Capsule is 1.8 m, origin at the feet.</summary>
    private const float EyeHeight = 1.6f;

    private readonly struct Shot
    {
        public readonly string Name;
        public readonly Vector3 Eye;
        public readonly float Yaw;

        public Shot(string name, Vector3 groundPosition, float yaw)
        {
            Name = name;
            Eye = groundPosition + Vector3.up * EyeHeight;
            Yaw = yaw;
        }
    }

    [MenuItem("Kessil/Playthrough/Capture The Route")]
    public static void CaptureAll()
    {
        Directory.CreateDirectory(OutputFolder);
        int written = 0;

        written += CaptureRegion();
        written += CaptureInteriors();

        AssetDatabase.Refresh();
        Debug.Log($"[Playthrough] Wrote {written} frames to {OutputFolder}");
    }

    /// <summary>
    /// The opening route, in the order a player walks it: what you see on spawn, the walk up
    /// the market street, the stall you can take something from, and the door you can enter.
    /// </summary>
    private static int CaptureRegion()
    {
        EditorSceneManager.OpenScene(CapitalRegionBuilder.ScenePath, OpenSceneMode.Single);

        var spawn = CapitalRegion.PlayerSpawn;
        var street = ArenaMiniatureSliceLayout.StreetOrigin;

        var shots = new List<Shot>
        {
            new("01-spawn-facing-street", spawn, 0f),
            new("02-spawn-looking-back", spawn, 180f),
            new("03-street-south", street + new Vector3(0f, 0f, -60f), 0f),
            new("04-street-market", street + new Vector3(0f, 0f, -20f), 0f),
            new("05-street-stall", street + new Vector3(-8f, 0f, -47f), 270f),
            new("06-street-north", street + new Vector3(0f, 0f, 40f), 0f),
            new("07-street-door", street + new Vector3(8f, 0f, 24f), 90f),
            new("08-city-beyond-street", street + new Vector3(0f, 0f, 150f), 0f),
            new("09-docks", CapitalRegion.FindAnchor("anchor.docks")?.Position ?? spawn, 180f)
        };

        int written = 0;
        foreach (var shot in shots) written += Capture(shot) ? 1 : 0;
        return written;
    }

    /// <summary>
    /// Every interior, from its own arrival spawn. One frame each is enough to see whether a
    /// room is a place or a grey box, and whether anybody is standing in it.
    /// </summary>
    private static int CaptureInteriors()
    {
        int written = 0;

        foreach (var spec in GreyThreadSceneCatalog.Scenes)
        {
            string path = $"Assets/Scenes/Chapter01/{spec.Name}.unity";
            if (!File.Exists(path)) continue;

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var context = SceneContext.FindInScene(scene);
            if (context == null || !context.TryGetDefaultSpawn(out var spawn) || spawn == null)
                continue;

            var ground = spawn.transform.position;
            written += Capture(new Shot($"interior-{spec.Name}", ground, spawn.transform.eulerAngles.y)) ? 1 : 0;
        }

        return written;
    }

    private static bool Capture(Shot shot)
    {
        var camGo = new GameObject("~PlaythroughCamera");
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D frame = null;

        try
        {
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = shot.Eye;
            cam.transform.rotation = Quaternion.Euler(0f, shot.Yaw, 0f);
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;

            cam.targetTexture = rt;
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            frame.Apply();
            RenderTexture.active = previous;

            var png = frame.EncodeToPNG();
            if (png == null || png.Length < 1024)
            {
                Debug.LogWarning($"[Playthrough] {shot.Name} produced no image.");
                return false;
            }

            File.WriteAllBytes($"{OutputFolder}/{shot.Name}.png", png);
            return true;
        }
        finally
        {
            camGo.GetComponent<Camera>().targetTexture = null;
            if (frame != null) Object.DestroyImmediate(frame);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }
}
