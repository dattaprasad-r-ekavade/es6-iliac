using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures MCP for Unity auto-starts its local HTTP bridge for Cursor on every editor load,
/// and retries until the Unity bridge is connected to the MCP HTTP server.
/// </summary>
[InitializeOnLoad]
public static class McpBootstrap
{
    private const string UseHttpTransport = "MCPForUnity.UseHttpTransport";
    private const string AutoStartOnLoad = "MCPForUnity.AutoStartOnLoad";
    private const string HttpUrl = "MCPForUnity.HttpUrl";
    private const string SetupCompleted = "MCPForUnity.SetupCompleted";
    private const string SetupDismissed = "MCPForUnity.SetupDismissed";
    private const string UvxPath = "MCPForUnity.UvxPath";

    private static int _attempts;
    private const int MaxAttempts = 60;

    static McpBootstrap()
    {
        EditorPrefs.SetBool(UseHttpTransport, true);
        EditorPrefs.SetBool(AutoStartOnLoad, true);
        EditorPrefs.SetBool(SetupCompleted, true);
        EditorPrefs.SetBool(SetupDismissed, true);

        if (string.IsNullOrWhiteSpace(EditorPrefs.GetString(HttpUrl, string.Empty)))
        {
            EditorPrefs.SetString(HttpUrl, "http://127.0.0.1:8080");
        }

        if (string.IsNullOrWhiteSpace(EditorPrefs.GetString(UvxPath, string.Empty)))
        {
            var localUvx = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".local", "bin", "uvx.exe");
            if (System.IO.File.Exists(localUvx))
            {
                EditorPrefs.SetString(UvxPath, localUvx);
            }
        }

        Debug.Log("[McpBootstrap] MCP HTTP auto-start enabled → http://127.0.0.1:8080/mcp");
        EditorApplication.delayCall += ScheduleConnectRetry;
    }

    private static void ScheduleConnectRetry()
    {
        EditorApplication.update -= TryConnectTick;
        EditorApplication.update += TryConnectTick;
    }

    private static void TryConnectTick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        _attempts++;
        if (_attempts > MaxAttempts)
        {
            EditorApplication.update -= TryConnectTick;
            Debug.LogWarning("[McpBootstrap] Gave up waiting for MCP bridge after retries.");
            return;
        }

        try
        {
            var locatorType = FindType("MCPForUnity.Editor.Services.MCPServiceLocator");
            if (locatorType == null)
            {
                return;
            }

            var bridgeProp = locatorType.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
            var bridge = bridgeProp?.GetValue(null);
            if (bridge == null)
            {
                return;
            }

            var isRunningProp = bridge.GetType().GetProperty("IsRunning");
            if (isRunningProp != null && isRunningProp.GetValue(bridge) is true)
            {
                EditorApplication.update -= TryConnectTick;
                Debug.Log("[McpBootstrap] MCP bridge is running.");
                return;
            }

            var startMethod = bridge.GetType().GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
            if (startMethod == null)
            {
                return;
            }

            // Fire and forget; next ticks will re-check IsRunning.
            var task = startMethod.Invoke(bridge, null) as Task;
            if (task != null)
            {
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Debug.LogWarning($"[McpBootstrap] Bridge StartAsync fault: {t.Exception?.GetBaseException().Message}");
                    }
                }, TaskScheduler.Default);
            }

            if (_attempts == 1 || _attempts % 10 == 0)
            {
                Debug.Log($"[McpBootstrap] Requested MCP bridge start (attempt {_attempts}/{MaxAttempts}).");
            }
        }
        catch (Exception ex)
        {
            if (_attempts == 1 || _attempts % 10 == 0)
            {
                Debug.LogWarning($"[McpBootstrap] Connect attempt failed: {ex.Message}");
            }
        }
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    [MenuItem("Elder Scrolls 6/MCP/Force Bridge Connect")]
    public static void ForceBridgeConnect()
    {
        _attempts = 0;
        ScheduleConnectRetry();
        Debug.Log("[McpBootstrap] Forced MCP bridge connect retries.");
    }
}
