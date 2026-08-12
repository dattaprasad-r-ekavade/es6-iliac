using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Headless-friendly camera captures for the VS2 handoff record.</summary>
public static class GreyThreadScreenshotCommand
{
    public static void CaptureVs2()
    {
        string output = Path.GetFullPath("Docs/Screenshots");
        Directory.CreateDirectory(output);
        Capture("Palace", "vs2-estmere-palace.png", new Vector3(19f, 9f, -17f), new Vector3(0f, 3.2f, 4f));
        Capture("Council_Arrival", "vs2-caldemar-arrival.png", new Vector3(18f, 8f, -16f), new Vector3(0f, 3.1f, 4f));
        Debug.Log("[GreyThread] VS2 screenshots captured.");
    }

    private static void Capture(string sceneName, string fileName, Vector3 cameraPosition, Vector3 target)
    {
        var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Chapter01/{sceneName}.unity", OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var cameraGo = new GameObject("VS2_ScreenshotCamera");
        cameraGo.transform.position = cameraPosition;
        cameraGo.transform.LookAt(target);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;

        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        camera.targetTexture = texture;
        camera.Render();

        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine("Docs/Screenshots", fileName), image.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        texture.Release();
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(cameraGo);
    }
}
