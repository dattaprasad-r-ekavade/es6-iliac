using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Unity-side bridge for the external world-authoring workflow.
///
/// The external tool owns edits to the source JSON. This command imports and validates that
/// source, calls the one sanctioned presentation/world rebuild, then produces fixed proof views.
/// It intentionally does not contain a second world generator.
///
/// Headless use (graphics must remain enabled for the PNGs):
/// Unity.exe -batchmode -quit -projectPath .
///     -executeMethod WorldBuilderPreviewCommand.BuildValidateAndCapture
/// </summary>
public static class WorldBuilderPreviewCommand
{
    public const string WorldAssetPath = "Assets/Resources/Data/World/kessil.world.json";
    public const string MainScenePath = "Assets/Scenes/Main.unity";
    public const string OutputFolder = "Docs/Screenshots/WorldBuilder";
    public const string TopDownFileName = "world-top-down.png";
    public const string PlayerViewFileName = "world-player-perspective.png";

    private const int CaptureWidth = 1280;
    private const int CaptureHeight = 720;

    [MenuItem("Kessil/World Builder/Validate, Rebuild + Capture")]
    public static void BuildValidateAndCapture()
    {
        try
        {
            WorldLayoutDocument document = ImportAndValidateCurrentWorld();

            Debug.Log($"[WorldBuilderPreview] Preflight passed: v{document.Version}, "
                      + $"{document.Landmasses.Length} landmasses, {document.Sites.Length} sites, "
                      + $"{document.Roads.Length} roads. Rebuilding Main through SetupGamePresentation.");

            // This is the production presentation builder. Do not replace it with a local or
            // preview-only generator: the purpose of these images is to prove what will ship.
            SetupGamePresentation.SetupAll();

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            BuildProducts products = ValidateGeneratedMain(scene, document);
            CapturePreviews(document, products.PlayerCamera);

            string topDown = Path.GetFullPath(Path.Combine(OutputFolder, TopDownFileName));
            string player = Path.GetFullPath(Path.Combine(OutputFolder, PlayerViewFileName));
            Debug.Log($"[WorldBuilderPreview] SUCCESS: rebuilt {MainScenePath}; "
                      + $"wrote '{topDown}' and '{player}'.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[WorldBuilderPreview] FAILED: {exception.GetType().Name}: "
                           + exception.Message);
            Debug.LogException(exception);

            // A batch caller must be able to distinguish a rejected map from a usable preview.
            // Interactive callers retain the exception and stack trace in the Console instead.
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            throw;
        }
    }

    private static WorldLayoutDocument ImportAndValidateCurrentWorld()
    {
        string fullPath = Path.GetFullPath(WorldAssetPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                $"Required world source is missing at '{fullPath}'.", fullPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(
            WorldAssetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var imported = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldAssetPath);
        if (imported == null)
            throw new InvalidOperationException(
                $"Unity did not import '{WorldAssetPath}' as a TextAsset.");

        // LoadRequired proves the exact Resources path used in a player resolves, rather than
        // merely proving that an arbitrary disk file contains parseable JSON.
        WorldLayoutDocument document = WorldLayoutData.LoadRequired();
        WorldBuilderPreviewValidation.ValidateOrThrow(document);

        // The static runtime layout is normally initialised here, after the forced import. If it
        // was already initialised before an interactive edit, fail rather than quietly rebuilding
        // Main from stale data. A fresh headless Unity invocation cannot hit this condition.
        WorldBuilderPreviewValidation.ValidateRuntimeProjectionOrThrow(document);
        return document;
    }

    private readonly struct BuildProducts
    {
        public readonly Camera PlayerCamera;

        public BuildProducts(Camera playerCamera)
        {
            PlayerCamera = playerCamera;
        }
    }

    private static BuildProducts ValidateGeneratedMain(Scene scene, WorldLayoutDocument document)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException($"Generated Main scene did not load: {MainScenePath}");

        KessilWorldGenerator generator = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            generator = root.GetComponentInChildren<KessilWorldGenerator>(true);
            if (generator != null) break;
        }

        if (generator == null)
            throw new InvalidOperationException("Generated Main has no KessilWorldGenerator.");

        Transform generated = generator.transform.Find("Generated");
        if (generated == null)
            throw new InvalidOperationException(
                "The sanctioned builder returned without producing WorldRoot/Generated.");

        foreach (WorldLandmassRecord landmass in document.Landmasses)
        {
            if (generated.Find(landmass.Name) == null)
                throw new InvalidOperationException(
                    $"Generated Main is missing declared landmass '{landmass.Name}'.");
        }

        SimplePlayerController controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<SimplePlayerController>(true);
            if (controller != null) break;
        }

        if (controller == null)
            throw new InvalidOperationException("Generated Main has no player controller.");

        Camera playerCamera = controller.GetComponentInChildren<Camera>(true);
        if (playerCamera == null)
            throw new InvalidOperationException("Generated player has no perspective camera.");

        return new BuildProducts(playerCamera);
    }

    private static void CapturePreviews(WorldLayoutDocument document, Camera playerCamera)
    {
        string output = Path.GetFullPath(OutputFolder);
        Directory.CreateDirectory(output);

        WorldBuilderPreviewValidation.TopDownFrame frame =
            WorldBuilderPreviewValidation.CalculateTopDownFrame(
                document, CaptureWidth / (float)CaptureHeight);

        bool previousFog = RenderSettings.fog;
        try
        {
            // A several-kilometre bird's-eye ray would otherwise be completely swallowed by
            // runtime fog. The perspective proof below restores and retains the authored fog.
            RenderSettings.fog = false;
            var topDownGo = new GameObject("~WorldBuilder_TopDownCapture")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                var topDown = topDownGo.AddComponent<Camera>();
                topDown.transform.SetPositionAndRotation(
                    frame.CameraPosition, Quaternion.Euler(90f, 0f, 0f));
                topDown.orthographic = true;
                topDown.orthographicSize = frame.OrthographicSize;
                topDown.nearClipPlane = 0.1f;
                topDown.farClipPlane = frame.CameraPosition.y + 1000f;
                topDown.clearFlags = CameraClearFlags.SolidColor;
                topDown.backgroundColor = ArtDirection.Active.Palette.Ocean;
                topDown.allowHDR = false;
                CaptureCamera(topDown, Path.Combine(output, TopDownFileName));
            }
            finally
            {
                Object.DestroyImmediate(topDownGo);
            }
        }
        finally
        {
            RenderSettings.fog = previousFog;
        }

        // Clone camera settings so capturing never changes the player's saved camera or target.
        var perspectiveGo = new GameObject("~WorldBuilder_PlayerCapture")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        try
        {
            var perspective = perspectiveGo.AddComponent<Camera>();
            perspective.CopyFrom(playerCamera);
            WorldSiteRecord focus = FindPerspectiveFocus(document);
            if (focus != null)
            {
                // Frame the newly-authored place from its approach instead of inheriting the
                // player's saved yaw. A deterministic preview should show the edit, not a wall
                // or whichever direction the prefab happened to face when it was last saved.
                Vector3 viewPosition = focus.TravelPosition + Vector3.up * 4f;
                Vector3 lookTarget = focus.WorldPosition + Vector3.up * 8f;
                perspective.transform.SetPositionAndRotation(viewPosition,
                    Quaternion.LookRotation(lookTarget - viewPosition, Vector3.up));
                perspective.fieldOfView = 58f;
            }
            else
            {
                perspective.transform.SetPositionAndRotation(
                    playerCamera.transform.position, playerCamera.transform.rotation);
            }
            perspective.orthographic = false;
            perspective.allowHDR = false;
            perspective.nearClipPlane = Mathf.Max(0.05f, playerCamera.nearClipPlane);
            perspective.farClipPlane = Mathf.Max(500f, playerCamera.farClipPlane);

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) perspective.cullingMask &= ~(1 << uiLayer);
            CaptureCamera(perspective, Path.Combine(output, PlayerViewFileName));
        }
        finally
        {
            Object.DestroyImmediate(perspectiveGo);
        }
    }

    private static WorldSiteRecord FindPerspectiveFocus(WorldLayoutDocument document)
    {
        if (document?.Sites == null) return null;
        WorldSiteRecord firstCity = null;
        foreach (WorldSiteRecord site in document.Sites)
        {
            if (site == null || !site.IsCity) continue;
            firstCity ??= site;
            if (site.Id == "city_north") return site;
        }
        return firstCity;
    }

    private static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            target = new RenderTexture(
                CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "WorldBuilderPreviewTarget",
                antiAliasing = 1
            };
            target.Create();
            if (!target.IsCreated())
                throw new InvalidOperationException(
                    "Could not create a render target. Do not use -nographics for preview capture.");

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
            image.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            ValidateImageIsUseful(image, Path.GetFileName(path));
            byte[] png = image.EncodeToPNG();
            if (png == null || png.Length < 1024)
                throw new InvalidOperationException(
                    $"Capture '{Path.GetFileName(path)}' did not encode a usable PNG.");

            File.WriteAllBytes(path, png);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (image != null) Object.DestroyImmediate(image);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }

    private static void ValidateImageIsUseful(Texture2D image, string fileName)
    {
        Color32[] pixels = image.GetPixels32();
        byte min = byte.MaxValue;
        byte max = byte.MinValue;

        // Sampling every 64th pixel is deterministic and sufficient to reject a blank graphics
        // device, an all-fog capture, or a camera that rendered only its clear colour.
        for (int i = 0; i < pixels.Length; i += 64)
        {
            Color32 pixel = pixels[i];
            byte luma = (byte)((pixel.r * 77 + pixel.g * 150 + pixel.b * 29) >> 8);
            if (luma < min) min = luma;
            if (luma > max) max = luma;
        }

        if (max - min < 8)
            throw new InvalidOperationException(
                $"Capture '{fileName}' is effectively blank (sampled luma range {min}-{max}). "
                + "Run Unity with graphics enabled and inspect the generated Main scene.");
    }
}

/// <summary>
/// Pure validation and framing helpers kept separate from the command so editor tests can exercise
/// rejected map states without rebuilding scenes or writing screenshots.
/// </summary>
public static class WorldBuilderPreviewValidation
{
    public readonly struct TopDownFrame
    {
        public readonly Vector3 CameraPosition;
        public readonly float OrthographicSize;

        public TopDownFrame(Vector3 cameraPosition, float orthographicSize)
        {
            CameraPosition = cameraPosition;
            OrthographicSize = orthographicSize;
        }
    }

    public static void ValidateOrThrow(WorldLayoutDocument document)
    {
        var errors = new List<string>();
        if (document == null)
            throw new InvalidOperationException("World JSON deserialised to null.");

        if (document.Version != WorldLayoutData.CurrentVersion)
            errors.Add($"version is {document.Version}; expected {WorldLayoutData.CurrentVersion}");

        RequireFinitePositive(document.OceanSize, nameof(document.OceanSize), errors);
        RequireFinitePositive(document.CameraFarPlane, nameof(document.CameraFarPlane), errors);
        RequireFinite(document.WaterLevel, nameof(document.WaterLevel), errors);
        RequireFinite(document.VoidCatcherY, nameof(document.VoidCatcherY), errors);
        RequireFinitePositive(document.SafeZoneRadius, nameof(document.SafeZoneRadius), errors);
        if (!IsFinite(document.SafeZoneCenter))
            errors.Add("SafeZoneCenter contains NaN or infinity");
        if (!IsFinite(document.BanditCamp))
            errors.Add("BanditCamp contains NaN or infinity");
        if (!IsFinite(document.CoastalRuin))
            errors.Add("CoastalRuin contains NaN or infinity");
        RequireFinitePositive(document.TerrainHalfExtent, nameof(document.TerrainHalfExtent), errors);
        if (document.TerrainHalfExtent > 0.5f)
            errors.Add("TerrainHalfExtent must be <= 0.5 so terrain stays inside its authored size");
        if (!IsFinite(document.MapExtentPadding) || document.MapExtentPadding < 0f)
            errors.Add("MapExtentPadding must be finite and non-negative");
        if (!IsFinite(document.MapMinX) || !IsFinite(document.MapMaxX)
            || !IsFinite(document.MapMinZ) || !IsFinite(document.MapMaxZ)
            || document.MapMinX >= document.MapMaxX || document.MapMinZ >= document.MapMaxZ)
            errors.Add("map bounds must be finite and ordered min < max");
        if (!IsFinite(document.CausewayDeckY) || document.CausewayDeckY <= document.WaterLevel)
            errors.Add("CausewayDeckY must be finite and above WaterLevel");
        if (!IsFinite(document.CaldemarSpawnPad))
            errors.Add("CaldemarSpawnPad contains NaN or infinity");
        float oceanHalf = document.OceanSize * 0.5f;
        float requiredOceanHalf = Mathf.Max(
            Mathf.Max(Mathf.Abs(document.MapMinX), Mathf.Abs(document.MapMaxX)),
            Mathf.Max(Mathf.Abs(document.MapMinZ), Mathf.Abs(document.MapMaxZ)));
        if (IsFinite(oceanHalf) && IsFinite(requiredOceanHalf) && oceanHalf < requiredOceanHalf)
            errors.Add("OceanSize does not cover the authored map bounds");

        if (document.Landmasses == null || document.Landmasses.Length == 0)
            errors.Add("at least one landmass is required");
        if (document.Sites == null || document.Sites.Length == 0)
            errors.Add("at least one site is required");
        if (document.Roads == null || document.Roads.Length == 0)
            errors.Add("at least one road is required");

        var landNames = new HashSet<string>(StringComparer.Ordinal);
        var terrainSeeds = new HashSet<int>();
        var cityIds = new HashSet<string>(StringComparer.Ordinal);
        if (document.Landmasses != null)
        {
            for (int i = 0; i < document.Landmasses.Length; i++)
            {
                WorldLandmassRecord land = document.Landmasses[i];
                string label = $"Landmasses[{i}]";
                if (land == null)
                {
                    errors.Add(label + " is null");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(land.Name)) errors.Add(label + " has no Name");
                else if (!landNames.Add(land.Name)) errors.Add($"duplicate landmass Name '{land.Name}'");
                if (!Enum.TryParse(land.Biome, false, out WorldLayout.Biome _))
                    errors.Add($"{label} has unknown Biome '{land.Biome}'");
                if (!IsFinite(land.Center) || !IsFinite(land.Size))
                    errors.Add(label + " contains NaN or infinity");
                if (land.Size.x <= 0f || land.Size.y <= 0f || land.Size.z <= 0f)
                    errors.Add(label + " Size components must all be positive");
                if (land.PropCount < 0) errors.Add(label + " PropCount cannot be negative");
                if (land.TerrainSeed == 0) errors.Add(label + " TerrainSeed cannot be zero");
                else if (!terrainSeeds.Add(land.TerrainSeed))
                    errors.Add($"duplicate TerrainSeed {land.TerrainSeed}");
                if (!string.IsNullOrEmpty(land.CityId) && !cityIds.Add(land.CityId))
                    errors.Add($"duplicate landmass CityId '{land.CityId}'");
                if (!string.IsNullOrEmpty(land.CityId) && string.IsNullOrWhiteSpace(land.CityName))
                    errors.Add($"{label} declares CityId '{land.CityId}' but no CityName");
                if (string.IsNullOrEmpty(land.CityId) && !string.IsNullOrEmpty(land.CityName))
                    errors.Add($"{label} declares CityName '{land.CityName}' but no stable CityId");

                if (IsFinite(land.Center) && IsFinite(land.Size)
                    && IsFinite(document.MapMinX) && IsFinite(document.MapMaxX)
                    && IsFinite(document.MapMinZ) && IsFinite(document.MapMaxZ))
                {
                    float west = land.Center.x - land.Size.x * 0.5f;
                    float east = land.Center.x + land.Size.x * 0.5f;
                    float south = land.Center.z - land.Size.z * 0.5f;
                    float north = land.Center.z + land.Size.z * 0.5f;
                    if (west - document.MapMinX < document.MapExtentPadding
                        || document.MapMaxX - east < document.MapExtentPadding
                        || south - document.MapMinZ < document.MapExtentPadding
                        || document.MapMaxZ - north < document.MapExtentPadding)
                        errors.Add($"landmass '{land.Name}' exceeds map bounds or required padding");
                }
            }
        }

        var siteIds = new HashSet<string>(StringComparer.Ordinal);
        var citySiteIds = new HashSet<string>(StringComparer.Ordinal);
        if (document.Sites != null)
        {
            for (int i = 0; i < document.Sites.Length; i++)
            {
                WorldSiteRecord site = document.Sites[i];
                string label = $"Sites[{i}]";
                if (site == null)
                {
                    errors.Add(label + " is null");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(site.Id)) errors.Add(label + " has no Id");
                else if (!siteIds.Add(site.Id)) errors.Add($"duplicate site Id '{site.Id}'");
                if (string.IsNullOrWhiteSpace(site.DisplayName))
                    errors.Add($"{label} has no DisplayName");
                if (!IsFinite(site.WorldPosition) || !IsFinite(site.TravelPosition))
                    errors.Add($"{label} contains NaN or infinity");
                if (!IsFinite(site.DiscoverRadius) || site.DiscoverRadius <= 0f)
                    errors.Add($"{label} DiscoverRadius must be finite and positive");
                if (site.IsCity && !string.IsNullOrWhiteSpace(site.Id)) citySiteIds.Add(site.Id);
                if (document.Landmasses != null && document.Landmasses.Length > 0)
                {
                    if (IsFinite(site.WorldPosition) && !IsOverLand(document, site.WorldPosition))
                        errors.Add($"site '{site.Id}' WorldPosition is over water");
                    if (IsFinite(site.TravelPosition) && !IsOverLand(document, site.TravelPosition))
                        errors.Add($"site '{site.Id}' TravelPosition is over water");
                }
            }
        }

        foreach (string cityId in cityIds)
            if (!citySiteIds.Contains(cityId))
                errors.Add($"landmass city '{cityId}' has no matching city site");
        foreach (string citySiteId in citySiteIds)
            if (!cityIds.Contains(citySiteId))
                errors.Add($"city site '{citySiteId}' has no matching city landmass");

        if (document.Roads != null)
        {
            for (int i = 0; i < document.Roads.Length; i++)
            {
                WorldRoadRecord road = document.Roads[i];
                if (road == null || road.Points == null || road.Points.Length < 2)
                {
                    errors.Add($"Roads[{i}] needs at least two points");
                    continue;
                }

                for (int point = 0; point < road.Points.Length; point++)
                    if (!IsFinite(road.Points[point]))
                        errors.Add($"Roads[{i}].Points[{point}] contains NaN or infinity");

                if (document.Landmasses != null && document.Landmasses.Length > 0
                    && IsFinite(road.Points[0])
                    && IsFinite(road.Points[road.Points.Length - 1]))
                {
                    if (!IsOverLand(document, road.Points[0]))
                        errors.Add($"Roads[{i}] starts over water");
                    if (!IsOverLand(document, road.Points[road.Points.Length - 1]))
                        errors.Add($"Roads[{i}] ends over water");
                }
            }
        }

        if (document.Landmasses != null && document.Landmasses.Length > 0
            && IsFinite(document.CaldemarSpawnPad)
            && !IsOverLand(document, document.CaldemarSpawnPad))
            errors.Add("CaldemarSpawnPad is over water");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "World validation rejected the source:\n - " + string.Join("\n - ", errors));

        // Exercise the runtime conversion too. This catches an enum or conversion failure even
        // if a future field escapes the editor-only structural checks above.
        WorldLayoutData.BuildLandmasses(document);
        WorldLayoutData.BuildSites(document);
        WorldLayoutData.BuildRoads(document);
    }

    public static void ValidateRuntimeProjectionOrThrow(WorldLayoutDocument document)
    {
        var errors = new List<string>();
        if (WorldLayout.Landmasses.Length != document.Landmasses.Length)
            errors.Add("landmass count differs");
        if (WorldLayout.Sites.Length != document.Sites.Length)
            errors.Add("site count differs");
        if (WorldLayout.Roads.Length != document.Roads.Length)
            errors.Add("road count differs");

        int landCount = Mathf.Min(WorldLayout.Landmasses.Length, document.Landmasses.Length);
        for (int i = 0; i < landCount; i++)
        {
            WorldLayout.Landmass runtime = WorldLayout.Landmasses[i];
            WorldLandmassRecord source = document.Landmasses[i];
            if (runtime.Name != source.Name || runtime.Center != source.Center
                || runtime.Size != source.Size || runtime.TerrainSeed != source.TerrainSeed
                || runtime.PropCount != source.PropCount
                || runtime.CityId != source.CityId || runtime.CityName != source.CityName)
                errors.Add($"landmass {i} ('{source.Name}') differs");

            if (Enum.TryParse(source.Biome, false, out WorldLayout.Biome sourceBiome)
                && runtime.Biome != sourceBiome)
                errors.Add($"landmass {i} ('{source.Name}') biome differs");
        }

        int siteCount = Mathf.Min(WorldLayout.Sites.Length, document.Sites.Length);
        for (int i = 0; i < siteCount; i++)
        {
            WorldLayout.Site runtime = WorldLayout.Sites[i];
            WorldSiteRecord source = document.Sites[i];
            if (runtime.Id != source.Id || runtime.DisplayName != source.DisplayName
                || runtime.WorldPosition != source.WorldPosition
                || runtime.TravelPosition != source.TravelPosition
                || runtime.IsCity != source.IsCity
                || !Mathf.Approximately(runtime.DiscoverRadius, source.DiscoverRadius))
                errors.Add($"site {i} ('{source.Id}') differs");
        }

        int roadCount = Mathf.Min(WorldLayout.Roads.Length, document.Roads.Length);
        for (int i = 0; i < roadCount; i++)
        {
            Vector3[] runtime = WorldLayout.Roads[i];
            Vector3[] source = document.Roads[i].Points ?? Array.Empty<Vector3>();
            if (runtime.Length != source.Length)
            {
                errors.Add($"road {i} point count differs");
                continue;
            }

            for (int point = 0; point < runtime.Length; point++)
            {
                if (runtime[point] == source[point]) continue;
                errors.Add($"road {i} point {point} differs");
                break;
            }
        }

        if (!Mathf.Approximately(WorldLayout.WaterLevel, document.WaterLevel)
            || !Mathf.Approximately(WorldLayout.VoidCatcherY, document.VoidCatcherY)
            || !Mathf.Approximately(WorldLayout.OceanSize, document.OceanSize)
            || !Mathf.Approximately(WorldLayout.CameraFarPlane, document.CameraFarPlane)
            || !Mathf.Approximately(WorldLayout.MapExtentPadding, document.MapExtentPadding)
            || !Mathf.Approximately(WorldLayout.MapMinX, document.MapMinX)
            || !Mathf.Approximately(WorldLayout.MapMaxX, document.MapMaxX)
            || !Mathf.Approximately(WorldLayout.MapMinZ, document.MapMinZ)
            || !Mathf.Approximately(WorldLayout.MapMaxZ, document.MapMaxZ)
            || !Mathf.Approximately(WorldLayout.CausewayDeckY, document.CausewayDeckY)
            || !Mathf.Approximately(WorldLayout.SafeZoneRadius, document.SafeZoneRadius)
            || !Mathf.Approximately(WorldLayout.TerrainHalfExtent, document.TerrainHalfExtent)
            || WorldLayout.CaldemarSpawnPad != document.CaldemarSpawnPad
            || WorldLayout.BanditCamp != document.BanditCamp
            || WorldLayout.CoastalRuin != document.CoastalRuin
            || WorldLayout.SafeZoneCenter != document.SafeZoneCenter)
            errors.Add("global dimensions or spawn differ");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "WorldLayout is stale relative to the freshly imported JSON ("
                + string.Join(", ", errors) + "). Run this bridge in a fresh Unity process.");
    }

    public static TopDownFrame CalculateTopDownFrame(WorldLayoutDocument document, float aspect)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (!IsFinite(aspect) || aspect <= 0f)
            throw new ArgumentOutOfRangeException(nameof(aspect), "Aspect must be finite and positive.");

        float width = document.MapMaxX - document.MapMinX;
        float depth = document.MapMaxZ - document.MapMinZ;
        if (!IsFinite(width) || !IsFinite(depth) || width <= 0f || depth <= 0f)
            throw new InvalidOperationException("Cannot frame invalid map bounds.");

        const float margin = 1.04f;
        float orthographicSize = Mathf.Max(depth * 0.5f, width * 0.5f / aspect) * margin;
        float centreX = (document.MapMinX + document.MapMaxX) * 0.5f;
        float centreZ = (document.MapMinZ + document.MapMaxZ) * 0.5f;
        float cameraY = Mathf.Max(2000f, orthographicSize * 1.7f);
        return new TopDownFrame(new Vector3(centreX, cameraY, centreZ), orthographicSize);
    }

    private static void RequireFinite(float value, string name, List<string> errors)
    {
        if (!IsFinite(value)) errors.Add(name + " must be finite");
    }

    private static void RequireFinitePositive(float value, string name, List<string> errors)
    {
        if (!IsFinite(value) || value <= 0f) errors.Add(name + " must be finite and positive");
    }

    private static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsOverLand(WorldLayoutDocument document, Vector3 position)
    {
        if (document.Landmasses == null || !IsFinite(document.TerrainHalfExtent)
            || document.TerrainHalfExtent <= 0f)
            return false;

        foreach (WorldLandmassRecord land in document.Landmasses)
        {
            if (land == null || !IsFinite(land.Center) || !IsFinite(land.Size)) continue;
            float radiusX = land.Size.x * document.TerrainHalfExtent;
            float radiusZ = land.Size.z * document.TerrainHalfExtent;
            if (radiusX <= 0f || radiusZ <= 0f) continue;
            float nx = (position.x - land.Center.x) / radiusX;
            float nz = (position.z - land.Center.z) / radiusZ;
            if (nx * nx + nz * nz <= 1f) return true;
        }

        return false;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
