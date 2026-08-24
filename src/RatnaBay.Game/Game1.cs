using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client;

public sealed class Game1 : Game
{
    private const int LogicalWidth = 1280;
    private const int LogicalHeight = 720;

    private enum GameScreen
    {
        MainMenu,
        WorldScene
    }

    private readonly GraphicsDeviceManager _graphics;
    private readonly Dictionary<string, Model> _models = new();
    private readonly Dictionary<string, float> _modelNormalizers = new();
    private readonly Dictionary<string, Vector3> _modelCenters = new();

    /// <summary>
    /// Bone transforms, resolved once at load. Nothing here is animated, so recomputing them
    /// into a freshly allocated array every model every frame was pure waste.
    /// </summary>
    private readonly Dictionary<string, Matrix[]> _modelBones = new();
    private readonly List<string> _assetErrors = new();
    private SpriteBatch _spriteBatch = null!;
    private FontSystem _fontSystem = null!;
    private FontSystem _headingFontSystem = null!;
    private Texture2D _white = null!;
    private BasicEffect _primitiveEffect = null!;
    private VertexPositionNormalTexture[] _cubeVertices = null!;
    private short[] _cubeIndices = null!;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    /// <summary>True while the pointer is captured for looking. Tab releases it.</summary>
    private bool _mouseLook;

    /// <summary>
    /// True when the pointer was freed to operate a panel rather than by the player asking
    /// for it. Closing that panel hands the camera back; pressing Tab does not.
    /// </summary>
    private bool _mouseFreedForPanel;
    private bool _showHelp;
    private bool _ignoreMouseDeltaThisFrame;

    /// <summary>Radians of rotation per pixel of mouse travel.</summary>
    private const float MouseSensitivity = 0.0032f;

    /// <summary>Radians per second while an arrow key is held.</summary>
    private const float KeyboardTurnSpeed = 2.2f;

    /// <summary>How far up or down the view can tip, short of straight up.</summary>
    private const float PitchLimit = 1.4f;

    /// <summary>Metres per second. Walking was 3.5, which read as wading.</summary>
    private const float WalkSpeed = 6f;

    private const float SprintSpeed = 11f;
    private const float PlayerCollisionRadius = 0.38f;
    private const float Gravity = 24f;
    private const float JumpSpeed = 8f;
    private const float CrouchDrop = 0.9f;
    private const float CrouchLerpSpeed = 12f;
    private GameScreen _screen = GameScreen.MainMenu;
    private int _menuSelection;
    private string _menuStatus = string.Empty;
    private Vector3 _cameraPosition = new(0f, 2.4f, 8.5f);
    private float _cameraYaw;
    private float _cameraPitch = -0.12f;
    private Matrix _view;
    private Matrix _projection;
    private Matrix _uiTransform = Matrix.Identity;
    private bool _borderlessFullscreen = true;
    private float _standingEyeY = 2.4f;
    private float _verticalOffset;
    private float _verticalVelocity;
    private bool _grounded = true;
    private bool _crouching;
    private bool _crouchToggled;
    private bool _forceCrouch;

    /// <summary>The live character. Null until a game is started or loaded.</summary>
    private GameSession? _session;

    /// <summary>The enemies in the scene and the fight with them.</summary>
    private Encounter? _encounter;
    private WorldRuntime? _world;
    private DialogueRuntime? _dialogue;
    private SpeakingActor? _conversationActor;
    private int _dialogueSelection;
    private string _dialogueResponse = string.Empty;
    private bool _dialogueOpen;
    private bool _showJournal;

    /// <summary>Which inventory row is selected on the character screen.</summary>
    private int _inventorySelection;
    private bool _showCharacter;
    private string _questObjectiveId = string.Empty;
    private WatcherRuntime? _watchers;
    private readonly Dictionary<string, PickpocketTarget> _pockets = new(StringComparer.Ordinal);
    private Shop? _shop;
    private bool _showShop;
    private int _shopSelection;
    private readonly List<WorldPickup> _pickups = new();
    private AmbientAudio? _ambientAudio;

    private BillboardRenderer _billboards = null!;

    /// <summary>The weapon in hand, and the swing it is part-way through.</summary>
    private readonly WeaponView _weaponView = new();

    /// <summary>Set by --screenshot: render a few frames, save a PNG, and quit.</summary>
    private string? _screenshotPath;

    /// <summary>Camera angles forced by --yaw / --pitch, for reproducible captures.</summary>
    private float? _startYaw;
    private float? _startPitch;

    /// <summary>Frames to render before --screenshot captures. Raise it to measure the rate.</summary>
    private int _warmupFrames = 4;

    /// <summary>
    /// Seconds into a swing to freeze at for --screenshot. Frames are uncapped during a
    /// capture, so the pose is driven directly rather than hoping to catch one mid-flight.
    /// </summary>
    private float? _captureSwing;
    private float? _captureCast;
    private bool _captureApplied;

    /// <summary>--stambha: frame the carved pillar as the trailer's opening shot.</summary>
    private bool _stambhaPreview;

    /// <summary>
    /// Bars that have just gone up, and for how much longer they say so.
    ///
    /// Tracked by watching the numbers rather than by listening for an event, so anything that
    /// restores health or prana — a potion, a stone, a heal, whatever is added later — reports
    /// itself without the domain needing to know that a HUD exists.
    /// </summary>
    private float _healthPulse;
    private float _pranaPulse;
    private float _lastHealth;
    private float _lastPrana;

    /// <summary>How long a restored bar stays lit.</summary>
    private const float PulseSeconds = 0.7f;

    /// <summary>What the player did this sitting, for reading back afterwards.</summary>
    private readonly PlayRecorder _recorder = PlayRecorder.Start();

    /// <summary>True once the camp panel has been shown for the current door.</summary>
    private bool _decisionRecorded;

    /// <summary>The descent in progress, when the loaded world is a mine.</summary>
    private RunRuntime? _run;

    /// <summary>The run that just ended, while its summary is on screen.</summary>
    private RunResult? _runSummary;

    /// <summary>What the last death cost, shown beside the run summary.</summary>
    private SuccessionResult? _succession;

    /// <summary>The shaft panel is open and a depth is being chosen.</summary>
    private bool _choosingDepth;
    private int _depthSelection = 1;

    /// <summary>Seconds until the next stance sample is written down.</summary>
    private float _stanceCountdown;

    /// <summary>How often the recorder notes where the player is standing.</summary>
    private const float StanceSampleSeconds = 1f;

    /// <summary>Northwatch's safe surface checkpoint between descents.</summary>
    private static readonly WorldPoint SurfaceCheckpoint = new(0f, 2.4f, 14.5f);

    /// <summary>--mine N: play a generated mine instead of the authored world.</summary>
    private int? _mineSeed;
    private int _mineRooms = 4;
    private int _mineDepth = 1;

    /// <summary>Screen to force open for --screenshot: inventory, journal, shop or help.</summary>
    private string? _captureScreen;

    /// <summary>
    /// Wall-clock frame rate. Deliberately not derived from GameTime: under a fixed
    /// timestep ElapsedGameTime is always 1/60 no matter how slowly the game is really
    /// running, which is exactly the failure this number exists to expose.
    /// </summary>
    private float _framesPerSecond;
    private int _fpsFrames;
    private readonly System.Diagnostics.Stopwatch _fpsClock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Real seconds the last frame took, for spotting a stall.</summary>
    private float _lastFrameMs;
    private int _framesDrawn;

    /// <summary>
    /// Logical-to-screen scale. Text is rasterized at this many device pixels per logical
    /// pixel so glyphs land 1:1 on the display instead of being resampled.
    /// </summary>
    private float _uiScale = 1f;
    private float _uiScalePreference = 1f;
    private bool _showSettings;
    private int _settingsSelection;

    /// <summary>Fonts rasterized per device-pixel size. Scaling a fixed atlas blurs text.</summary>
    private readonly Dictionary<int, DynamicSpriteFont> _bodyFonts = new();
    private readonly Dictionary<int, DynamicSpriteFont> _headingFonts = new();

    public Game1(string[] args)
    {
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Math.Max(displayMode.Width, LogicalWidth),
            PreferredBackBufferHeight = Math.Max(displayMode.Height, LogicalHeight),
            SynchronizeWithVerticalRetrace = true,
            IsFullScreen = true,
            HardwareModeSwitch = false
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        // MonoGame defaults to a fixed timestep, where ElapsedGameTime is always 1/60 no
        // matter how long the frame really took. At 43 fps that advanced game time at 72%
        // of real time, so walking was a quarter slower than its own speed constant said.
        // A variable timestep makes elapsed time mean elapsed time.
        IsFixedTimeStep = false;
        Window.Title = "Ratna Bay";
        Window.IsBorderless = true;

        _screen = ParseMode(args);
        _screenshotPath = ParseOption(args, "--screenshot");
        _forceCrouch = HasArgument(args, "--sneak");

        // Deterministic camera for screenshots, so a change to look or movement can be
        // compared frame against frame instead of described.
        if (int.TryParse(ParseOption(args, "--warmup"), out var warmup)) _warmupFrames = warmup;
        if (float.TryParse(ParseOption(args, "--swing"), out var swing)) _captureSwing = swing;
        if (float.TryParse(ParseOption(args, "--cast"), out var cast)) _captureCast = cast;

        // Opens a screen for a capture, so an interface change can be looked at rather than
        // described. Screenshot mode only.
        _captureScreen = ParseOption(args, "--show");
        _stambhaPreview = HasArgument(args, "--stambha");
        if (int.TryParse(ParseOption(args, "--mine"), out var mineSeed)) _mineSeed = mineSeed;
        if (int.TryParse(ParseOption(args, "--rooms"), out var mineRooms)) _mineRooms = mineRooms;
        if (int.TryParse(ParseOption(args, "--depth"), out var mineDepth)) _mineDepth = mineDepth;

        // Asking for a mine and being shown the title screen is a papercut; --mine means play it.
        if (_mineSeed is not null) _screen = GameScreen.WorldScene;
        if (float.TryParse(ParseOption(args, "--yaw"), out var yaw)) _startYaw = yaw;
        if (float.TryParse(ParseOption(args, "--pitch"), out var pitch)) _startPitch = pitch;
        if (_screenshotPath is not null)
        {
            // Deterministic capture: a fixed window, no vsync wait, and quit when done.
            _borderlessFullscreen = false;
            _graphics.PreferredBackBufferWidth = LogicalWidth;
            _graphics.PreferredBackBufferHeight = LogicalHeight;
            _graphics.IsFullScreen = false;
            _graphics.SynchronizeWithVerticalRetrace = false;
            Window.IsBorderless = false;
        }

        if (HasArgument(args, "--windowed"))
        {
            _borderlessFullscreen = false;
            _graphics.PreferredBackBufferWidth = LogicalWidth;
            _graphics.PreferredBackBufferHeight = LogicalHeight;
            _graphics.IsFullScreen = false;
            Window.IsBorderless = false;
        }
    }

    private static bool HasArgument(string[] args, string argument)
    {
        foreach (var value in args)
        {
            if (string.Equals(value, argument, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Reads `--name value` from the command line.</summary>
    private static string? ParseOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];

        return null;
    }

    private static GameScreen ParseMode(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!string.Equals(args[index], "--mode", StringComparison.OrdinalIgnoreCase))
                continue;

            return args[index + 1].ToLowerInvariant() switch
            {
                "menu" or "title" => GameScreen.MainMenu,
                "scene" or "game" or "world" => GameScreen.WorldScene,
                _ => GameScreen.MainMenu
            };
        }

        return GameScreen.MainMenu;
    }

    protected override void Initialize()
    {
        _projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(65f),
            GraphicsDevice.Viewport.AspectRatio,
            0.05f,
            200f);

        CreatePrimitiveCube();

        // Read before anything is loaded, so the first menu already knows whether there is a
        // descent waiting rather than a town.
        _suspendedDescentOnDisk = GameSession.PeekHasSuspendedDescent();

        // Launching straight into the scene (--mode scene, screenshots, playtests) needs a
        // character and a data-authored room, or the HUD has nothing to show.
        if (_screen == GameScreen.WorldScene)
        {
            LoadWorldManifest();
            ResetCamera();
            StartSession(GameSession.NewGame());
        }

        if (_startYaw is { } forcedYaw) _cameraYaw = forcedYaw;
        if (_startPitch is { } forcedPitch) _cameraPitch = forcedPitch;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _billboards = new BillboardRenderer(GraphicsDevice);
        var fontsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Feasibility",
            "Fonts");

        // Glyphs are rasterised at their real device size by SelectFont, so no resolution
        // multiplier is needed here — one would just double every atlas for no sharpness.
        _fontSystem = new FontSystem();
        _fontSystem.UseKernings = true;
        _fontSystem.AddFont(File.ReadAllBytes(Path.Combine(fontsDirectory, "NotoSans", "NotoSans-wght.ttf")));

        _headingFontSystem = new FontSystem();
        _headingFontSystem.UseKernings = true;
        _headingFontSystem.AddFont(File.ReadAllBytes(Path.Combine(fontsDirectory, "Cinzel", "Cinzel-wght.ttf")));

        // Devanagari for the carved verses. Absent, the pillar simply stands blank.
        StambhaCarving.Load(fontsDirectory);

        _white = new Texture2D(GraphicsDevice, 1, 1);
        _white.SetData(new[] { Color.White });

        if (!AmbientAudio.TryStart(out _ambientAudio, out var ambientError)
            && !string.IsNullOrWhiteSpace(ambientError))
            _assetErrors.Add(ambientError);

        _primitiveEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = false,
            TextureEnabled = false,
            LightingEnabled = true,
            PreferPerPixelLighting = true
        };

        // EnableDefaultLighting() overwrites the ambient colour and all three lights, so it
        // has to run before they are set. It was being called after, which made every
        // ambient value below it dead code and left the scene near-black.
        _primitiveEffect.EnableDefaultLighting();
        _primitiveEffect.AmbientLightColor = new Vector3(0.54f, 0.57f, 0.62f);
        _primitiveEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.25f));
        _primitiveEffect.DirectionalLight0.DiffuseColor = new Vector3(1f, 0.83f, 0.64f);
        _primitiveEffect.DirectionalLight0.SpecularColor = new Vector3(0.28f);

        LoadModel("bridge", "Feasibility/Models/Kenney/bridge_wood");
        LoadModel("campfire", "Feasibility/Models/Kenney/campfire_stones");
        LoadModel("ground", "Feasibility/Models/Kenney/ground_grass");
        LoadModel("bush", "Feasibility/Models/Kenney/plant_bushLarge");
        LoadModel("rock", "Feasibility/Models/Kenney/rock_largeA");
        LoadModel("tent", "Feasibility/Models/Kenney/tent_detailedOpen");
        LoadModel("tree", "Feasibility/Models/Kenney/tree_pineRoundA");
        LoadModel("cheeseBox", "Feasibility/Models/PolyHavenCheeseBox/CheeseBox_01_1k");
    }

    protected override void UnloadContent()
    {
        _fontSystem.Dispose();
        _headingFontSystem.Dispose();
        _white.Dispose();
        _primitiveEffect.Dispose();
        _billboards.Dispose();
        // A sitting that ends by closing the window is still a sitting worth reading back.
        _recorder.Flush();

        _ambientAudio?.Dispose();
        CharacterSprites.Clear();
        WeaponSprites.Clear();
        BoltSprites.Clear();
        StambhaCarving.Clear();
        base.UnloadContent();
    }

    /// <summary>
    /// Longest step any system is given. Without this a stall, a dragged window or a
    /// breakpoint resumes with one enormous frame and the player arrives somewhere else.
    /// </summary>
    private const float MaxFrameSeconds = 0.1f;

    private static float StepSeconds(GameTime gameTime) =>
        MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrameSeconds);

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (Pressed(keyboard, Keys.F11))
            SetBorderlessFullscreen(!_borderlessFullscreen);

        if (Pressed(keyboard, Keys.Escape))
        {
            if (_screen == GameScreen.MainMenu)
            {
                if (_showSettings) _showSettings = false;
            }
            else if (_paused)
            {
                ResumeFromPause();
            }
            else if (AnyPanelOpen)
            {
                ClosePanels();
            }
            else
            {
                // This used to drop straight to the main menu, which silently threw away a
                // descent in progress: the one key everybody presses to pause was the one key
                // that lost the run.
                Pause();
            }
        }

        if (_screen == GameScreen.MainMenu)
            UpdateMenu(keyboard, mouse);
        else
            UpdateGameScreen(gameTime, keyboard, mouse);

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    /// <summary>
    /// Capture or release the pointer.
    ///
    /// While captured the cursor is hidden and re-centred every frame, so looking around
    /// never runs out of desk and never lets a click land outside the window.
    /// </summary>
    private void SetMouseLook(bool enabled, bool forPanel = false)
    {
        if (_screenshotPath is not null) enabled = false;

        if (enabled) _mouseFreedForPanel = false;
        else if (forPanel) _mouseFreedForPanel = true;

        _mouseLook = enabled;
        // The system cursor stays hidden in every state; DrawPointer draws ours instead.
        IsMouseVisible = false;
        if (enabled)
        {
            // The MouseState passed through this Update was sampled before the cursor was
            // recentered. Ignore that stale position or clicking off-centre causes a camera
            // jump on the same frame that capture begins.
            _ignoreMouseDeltaThisFrame = true;
            CentreMouse();
        }
        else
        {
            _ignoreMouseDeltaThisFrame = false;
        }
    }

    private void CentreMouse()
    {
        var viewport = GraphicsDevice.Viewport;
        Mouse.SetPosition(viewport.Width / 2, viewport.Height / 2);
    }

    /// <summary>Mouse travel since the last frame, in pixels, while looking.</summary>
    private Vector2 ReadMouseDelta(MouseState mouse)
    {
        if (!_mouseLook || !IsActive) return Vector2.Zero;

        if (_ignoreMouseDeltaThisFrame)
        {
            _ignoreMouseDeltaThisFrame = false;
            CentreMouse();
            return Vector2.Zero;
        }

        var viewport = GraphicsDevice.Viewport;
        var centre = new Point(viewport.Width / 2, viewport.Height / 2);
        var delta = new Vector2(mouse.X - centre.X, mouse.Y - centre.Y);

        CentreMouse();
        return delta;
    }

    /// <summary>The pointer in 1280x720 logical space, so UI hit tests match what is drawn.</summary>
    private Vector2 LogicalMouse(MouseState mouse)
    {
        if (_uiScale <= 0f) return Vector2.Zero;

        var viewport = GraphicsDevice.Viewport;
        var offsetX = (viewport.Width - LogicalWidth * _uiScale) * 0.5f;
        var offsetY = (viewport.Height - LogicalHeight * _uiScale) * 0.5f;
        return new Vector2((mouse.X - offsetX) / _uiScale, (mouse.Y - offsetY) / _uiScale);
    }

    private bool Clicked(MouseState mouse) =>
        mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    protected override void Draw(GameTime gameTime)
    {
        ApplyCaptureScreen();

        _fpsFrames++;
        var elapsed = _fpsClock.Elapsed.TotalSeconds;
        if (elapsed >= 0.5)
        {
            _framesPerSecond = (float)(_fpsFrames / elapsed);
            _lastFrameMs = (float)(elapsed * 1000.0 / _fpsFrames);
            _fpsFrames = 0;
            _fpsClock.Restart();
        }

        GraphicsDevice.Clear(new Color(9, 15, 25));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        UpdateUiTransform();
        UpdateCameraMatrices();

        switch (_screen)
        {
            case GameScreen.MainMenu:
                DrawMenu();
                break;
            case GameScreen.WorldScene:
                DrawWorldScene();
                break;
        }

        base.Draw(gameTime);

        if (_screenshotPath is not null) CaptureAndExit();
    }

    /// <summary>
    /// Save the frame that was just drawn and quit.
    ///
    /// This exists so a change to the interface can be looked at rather than described. A few
    /// frames are skipped first because fonts rasterise and models settle on the first pass.
    /// </summary>
    private void CaptureAndExit()
    {
        if (++_framesDrawn <= _warmupFrames) return;

        var viewport = GraphicsDevice.Viewport;
        var pixels = new Color[viewport.Width * viewport.Height];
        GraphicsDevice.GetBackBufferData(pixels);

        using var texture = new Texture2D(GraphicsDevice, viewport.Width, viewport.Height);
        texture.SetData(pixels);

        var fullPath = Path.GetFullPath(_screenshotPath!);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        using (var stream = File.Create(fullPath))
            texture.SaveAsPng(stream, viewport.Width, viewport.Height);

        Console.WriteLine($"Saved {viewport.Width}x{viewport.Height} screenshot to {fullPath}");
        Exit();
    }

    /// <summary>
    /// Continue only appears when a save actually exists, so the menu never offers a door
    /// that opens onto nothing.
    /// </summary>
    private const string ResumeItem = "Resume Descent";

    /// <summary>
    /// Continue means "carry on where I was", and where the player was may be underground.
    ///
    /// Splitting that into two entries — one for the town, one for the mine — was the mess:
    /// three doors into the same save, and no way to tell which one kept your run.
    /// </summary>


    /// <summary>Read at startup so the menu can label itself before anything is loaded.</summary>
    private static bool _suspendedDescentOnDisk;

    /// <summary>The game is stopped and the pause screen owns the input.</summary>
    private bool _paused;
    private int _pauseSelection;

    /// <summary>
    /// One door per state.
    ///
    /// While a descent is set aside there is no "Descend into a Mine" entry at all: offering
    /// both would let a new mine quietly overwrite a run the player had carefully put down,
    /// which is the sort of loss that is never noticed until it has already happened.
    /// </summary>
    private static string[] MenuItems
    {
        get
        {
            if (!GameSession.HasSaveFile)
                return new[] { "Start New Game", "Settings", "Exit" };

            // No "Descend" here any more: a descent is bought at the shaft, standing in the
            // yard, with the stones in hand and the price in front of you. A menu entry that
            // skipped all of that was skipping the decision it exists to pose.
            //
            // No "Continue" beside Resume either: resuming *is* continuing, and a second
            // entry that walked to the surface instead would be the old confusion again.
            return _suspendedDescentOnDisk
                ? new[] { ResumeItem, "Start New Game", "Settings", "Exit" }
                : new[] { "Continue", "Start New Game", "Settings", "Exit" };
        }
    }

    private void UpdateMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (_showSettings)
        {
            UpdateSettings(keyboard);
            return;
        }

        var menuItemCount = MenuItems.Length;
        _menuSelection = Math.Clamp(_menuSelection, 0, menuItemCount - 1);

        if (Pressed(keyboard, Keys.Up))
        {
            _menuSelection = (_menuSelection + menuItemCount - 1) % menuItemCount;
            _menuStatus = string.Empty;
        }
        if (Pressed(keyboard, Keys.Down))
        {
            _menuSelection = (_menuSelection + 1) % menuItemCount;
            _menuStatus = string.Empty;
        }

        // Hovering moves the selection, so the keyboard and the mouse never disagree about
        // which item is about to be chosen.
        var pointer = LogicalMouse(mouse);
        var hovered = -1;
        for (var index = 0; index < menuItemCount; index++)
            if (MenuItemBounds(index).Contains((int)pointer.X, (int)pointer.Y))
                hovered = index;

        if (hovered >= 0)
        {
            _menuSelection = hovered;
            if (Clicked(mouse))
            {
                ActivateMenuItem();
                return;
            }
        }

        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
            ActivateMenuItem();
    }

    private void UpdateSettings(KeyboardState keyboard)
    {
        const int optionCount = 3;
        if (Pressed(keyboard, Keys.Up))
            _settingsSelection = (_settingsSelection + optionCount - 1) % optionCount;
        if (Pressed(keyboard, Keys.Down))
            _settingsSelection = (_settingsSelection + 1) % optionCount;

        if (_settingsSelection == 0 && Pressed(keyboard, Keys.Enter))
            SetBorderlessFullscreen(!_borderlessFullscreen);

        if (_settingsSelection == 1 && (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.Right)))
        {
            var direction = keyboard.IsKeyDown(Keys.Right) ? 0.1f : -0.1f;
            _uiScalePreference = MathHelper.Clamp(_uiScalePreference + direction, 0.8f, 1.2f);
            UpdateUiTransform();
        }
    }

    /// <summary>
    /// One menu row. Drawing and hit testing both read this, so a clickable row is always
    /// exactly the row the player can see.
    /// </summary>
    private static Rectangle MenuItemBounds(int index) => new(120, 286 + index * 56, 368, 42);

    /// <summary>
    /// Drop into a world, generated or authored.
    ///
    /// Both paths go through here because the world has to be discarded and rebuilt when the
    /// kind of world changes. Leaving the old one in place is how "Start New Game" after a
    /// descent used to hand back the mine you had just left.
    /// </summary>
    /// <summary>
    /// Walk back into the mine that was put down.
    ///
    /// The mine is rebuilt from its seed rather than stored, the ledger is adopted by the run
    /// that is already wired to the HUD and the recorder, and the descent is then struck off
    /// the save — so resuming is a way to stop playing and come back, never a way to reload a
    /// fight that went badly.
    /// </summary>
    private void ResumeSuspendedDescent()
    {
        _mineSeed = null;
        _world = null;
        _run = null;
        _runSummary = null;

        if (_session is null && !LoadSession()) return;
        if (_session is null) return;

        if (!_session.HasSuspendedDescent)
        {
            _menuStatus = "There is no descent to return to.";
            return;
        }

        var descent = _session.Descent!;
        _mineRooms = descent.Rooms;
        _mineDepth = descent.Depth;
        _mineSeed = descent.Seed;
        _world = null;

        StartSession(_session);

        // Where they were standing, not the mine's entrance.
        _cameraPosition = new Vector3(_session.Position.X, _session.Position.Y, _session.Position.Z);
        _cameraYaw = _session.Yaw;
        _cameraPitch = _session.Pitch;
        _standingEyeY = _session.Position.Y;

        _run?.Resume(descent);
        _session.ConsumeDescent();
        _suspendedDescentOnDisk = false;

        _session.ShowToast($"Back in the dark. {_run?.Run.Pending ?? 0} stones still at risk.");
        _menuStatus = string.Empty;
        _screen = GameScreen.WorldScene;
        SetMouseLook(true);
    }

    /// <summary>Go down, at a depth that has been paid for.</summary>
    private void EnterMine(int seed, int tier) => EnterWorld(seed, tier: tier);

    /// <summary>Come back up. The run is over either way by the time this is called.</summary>
    private void ReturnToTheSurface()
    {
        _runSummary = null;
        _succession = null;
        EnterWorld(null);
    }

    private void EnterWorld(int? mineSeed, bool newCharacter = false, int tier = 1)
    {
        _mineSeed = mineSeed;

        // Deeper than a run is meant to last. A mine you can clear out ends the run for you,
        // and then pressing on is never a risk — it is just the way forward until the game
        // stops you. The descent has to end because the player decided it did.
        _mineRooms = 18;
        _mineDepth = Math.Clamp(tier, MineEntry.MinTier, MineEntry.MaxTier);

        _world = null;
        _run = null;
        _runSummary = null;

        var session = newCharacter || _session is null ? GameSession.NewGame() : _session;
        StartSession(session);
        ResetCamera();

        _menuStatus = string.Empty;
        _screen = GameScreen.WorldScene;
        SetMouseLook(true);
    }

    private void ActivateMenuItem()
    {
        switch (MenuItems[_menuSelection])
        {
            case ResumeItem:
                ResumeSuspendedDescent();
                break;
            case "Continue":
                // A run summary returns to this same menu while its mine is still resident.
                // Continue means the persisted surface checkpoint, never that spent mine.
                _mineSeed = null;
                _world = null;
                _run = null;
                _runSummary = null;
                LoadWorldManifest();
                ResetCamera();
                if (LoadSession())
                {
                    _screen = GameScreen.WorldScene;
                    SetMouseLook(true);
                }
                break;
            case "Start New Game":
                EnterWorld(null, newCharacter: true);
                _session!.ShowToast("You wake on the Northwatch road.");
                break;
            case "Settings":
                _showSettings = true;
                _settingsSelection = 0;
                SetMouseLook(false);
                break;
            case "Exit":
                Exit();
                break;
        }
    }

    /// <summary>Use whatever is being stood at in the yard.</summary>
    private void UseFixture(SurfaceFixture fixture)
    {
        if (_session is null) return;

        switch (fixture)
        {
            case SurfaceFixture.Shaft:
                OpenTheShaft();
                break;

            case SurfaceFixture.Trader:
                if (_shop is null)
                {
                    _session.ShowToast("The stall is shut.");
                    break;
                }

                _showShop = true;
                _shopSelection = 0;
                SetMouseLook(false, forPanel: true);
                break;

            case SurfaceFixture.Stambha:
                _session.ShowToast("मा गृधः कस्य स्विद्धनम्  —  covet not; for whose is wealth?");
                break;
        }
    }

    /// <summary>
    /// The shaft: choose a depth, pay for it, and go down.
    ///
    /// The panel exists so the price and what it buys are in front of the player at the moment
    /// they commit. Banking stones meant nothing for five playtests because there was nothing
    /// to spend them on; this is where that stops being true.
    /// </summary>
    private void OpenTheShaft()
    {
        if (_session is null) return;

        _choosingDepth = true;
        _depthSelection = Math.Clamp(
            MineEntry.DeepestAffordable(_session.Player.Inventory), 1, MineEntry.MaxTier);
        SetMouseLook(false, forPanel: true);
    }

    private void UpdateDepthChoice(KeyboardState keyboard)
    {
        if (_session is null) { _choosingDepth = false; return; }

        if (Pressed(keyboard, Keys.Escape))
        {
            _choosingDepth = false;
            SetMouseLook(true);
            return;
        }

        if (Pressed(keyboard, Keys.Up))
            _depthSelection = Math.Max(MineEntry.MinTier, _depthSelection - 1);
        if (Pressed(keyboard, Keys.Down))
            _depthSelection = Math.Min(MineEntry.MaxTier, _depthSelection + 1);

        if (!Pressed(keyboard, Keys.Enter) && !Pressed(keyboard, Keys.Space)) return;

        var cost = MineEntry.CostOf(_depthSelection);
        if (!MineEntry.TryOpen(_session.Player.Inventory, _depthSelection))
        {
            _session.ShowToast($"That door wants {cost} stones. You have "
                + $"{_session.Player.Inventory.CountOf(SoulCrystals.LesserId)}.");
            return;
        }

        _choosingDepth = false;

        // Back to the mine that killed the last one, if there is a body in it. A fresh random
        // mine would put the cache somewhere unreachable by design, and a loss you are never
        // given the chance to answer is only a loss.
        var fallen = _session.Player.Legacy.Fallen;
        var returning = fallen is not null && fallen.Tier == _depthSelection;

        EnterMine(returning ? fallen!.MineSeed : Environment.TickCount, _depthSelection);

        _session.ShowToast(returning
            ? $"The same shaft. {fallen!.Name} is still down there, in room {fallen.RoomIndex}."
            : cost > 0
                ? $"{cost} stones, and the shaft opens. Tier {_depthSelection}."
                : "The shallow workings. They cost nothing and pay like it.");
    }

    /// <summary>What the pause screen offers, which depends on whether a run is underway.</summary>
    private string[] PauseItems => _run is { Run.IsActive: true }
        ? new[] { "Resume", "Settings", "Set the descent aside", "Give up the descent" }
        : new[] { "Resume", "Settings", "Save and quit to menu" };

    private void Pause()
    {
        if (_paused) return;

        ClosePanels();
        _paused = true;
        _pauseSelection = 0;
        SetMouseLook(false, forPanel: true);
    }

    private void ResumeFromPause()
    {
        _paused = false;
        _showSettings = false;
        if (_screen == GameScreen.WorldScene) SetMouseLook(true);
    }

    private void UpdatePause(KeyboardState keyboard)
    {
        if (_showSettings)
        {
            UpdateSettings(keyboard);
            return;
        }

        var items = PauseItems;
        if (Pressed(keyboard, Keys.Up))
            _pauseSelection = (_pauseSelection + items.Length - 1) % items.Length;
        if (Pressed(keyboard, Keys.Down))
            _pauseSelection = (_pauseSelection + 1) % items.Length;

        if (!Pressed(keyboard, Keys.Enter) && !Pressed(keyboard, Keys.Space)) return;

        switch (items[_pauseSelection])
        {
            case "Resume":
                ResumeFromPause();
                break;

            case "Settings":
                _showSettings = true;
                _settingsSelection = 0;
                break;

            case "Set the descent aside":
                SuspendDescent();
                break;

            case "Give up the descent":
                AbandonDescent();
                break;

            case "Save and quit to menu":
                _session?.ShowToast(_session.Save());
                LeaveToMenu();
                break;
        }
    }

    /// <summary>Put the run down mid-descent, to be walked back into later.</summary>
    private void SuspendDescent()
    {
        if (_session is null || _run is not { Run.IsActive: true } run || _mineSeed is not { } seed)
            return;

        var message = _session.Suspend(
            run.Capture(seed, _mineRooms, _mineDepth),
            new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z),
            _cameraYaw, _cameraPitch);

        _suspendedDescentOnDisk = _session.HasSuspendedDescent;
        _session.ShowToast(message);
        LeaveToMenu();
    }

    /// <summary>
    /// Give up on a descent, at the full price of one.
    ///
    /// It costs exactly what dying costs — the pot, half the pack, and progress toward the
    /// next level — because anything cheaper is a button that cancels a fight going badly.
    /// A run with an escape hatch is not a risk, and the risk is the entire loop.
    ///
    /// It exists at all so that "I want to stop playing this run" never requires losing the
    /// game on purpose, and so a set-aside descent can be cleared without being abandoned by
    /// accident somewhere else.
    /// </summary>
    private void AbandonDescent()
    {
        if (_session is null || _run is not { Run.IsActive: true } run) return;

        var result = run.Die();
        _recorder.Record(PlayEventKind.Died, "gave up", result.StonesLost, 0f,
            _session.Player.Vitals.Health, _session.Player.Vitals.Prana);

        _succession = Succession.Promote(_session.Player, result, _mineSeed ?? 0, run.DeepestRoom);

        _session.Descent = null;
        _suspendedDescentOnDisk = false;
        _paused = false;
        EndRun(result);
    }

    private void LeaveToMenu()
    {
        _paused = false;
        _showSettings = false;
        SetMouseLook(false);
        _screen = GameScreen.MainMenu;
        _menuSelection = 0;
    }

    private void UpdateGameScreen(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        // M was a second silent way out of a run. It opens the same pause screen now.
        if (Pressed(keyboard, Keys.M)) Pause();

        if (_paused)
        {
            UpdatePause(keyboard);
            return;
        }

        if (Pressed(keyboard, Keys.F1))
        {
            if (_showHelp) ClosePanels();
            else { _showHelp = true; SetMouseLook(false, forPanel: true); }
        }
        // The run summary owns the screen: nothing else is reachable until it is dismissed.
        if (_runSummary is not null)
        {
            if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Escape)
                || Pressed(keyboard, Keys.Space))
            {
                // Up into the yard rather than out to a menu. A loop that ends at a title
                // screen is not a loop; the whole point of the surface is having somewhere
                // to arrive with what you carried out.
                ReturnToTheSurface();
            }

            return;
        }

        if (_choosingDepth)
        {
            UpdateDepthChoice(keyboard);
            return;
        }

        if (_run is { AtDecision: true } decision && _session is not null)
        {
            // The clock on the answer starts the first frame the panel is up.
            if (!_decisionRecorded)
            {
                _decisionRecorded = true;
                _recorder.Record(PlayEventKind.DecisionOffered,
                    $"after {decision.Run.RoomsCleared} rooms",
                    decision.Run.Pending, decision.Run.NextRoomPays,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
            }

            if (Pressed(keyboard, Keys.C))
            {
                var result = decision.Camp();
                _recorder.Record(PlayEventKind.Camped, $"after {result.RoomsCleared} rooms",
                    result.StonesCarriedOut, 0f, _session.Player.Vitals.Health);
                EndRun(result);
                return;
            }

            if (Pressed(keyboard, Keys.E) && decision.Run.CanPressOn)
            {
                _recorder.Record(PlayEventKind.PressedOn,
                    $"into room {decision.Run.RoomsCleared + 1}",
                    decision.Run.Pending, decision.Run.NextRoomPays,
                    _session.Player.Vitals.Health);

                decision.PressOn(_world!, _session.Player);
                _session.ShowToast("The door swings in. No going back.");
                return;
            }
        }
        else if (_run is not null)
        {
            _decisionRecorded = false;
        }

        if (Pressed(keyboard, Keys.Tab)) SetMouseLook(!_mouseLook);
        if (Pressed(keyboard, Keys.J))
        {
            if (_showJournal) ClosePanels();
            else { _showJournal = true; _showCharacter = false; SetMouseLook(false, forPanel: true); }
        }
        if (Pressed(keyboard, Keys.I) || Pressed(keyboard, Keys.K))
        {
            if (_showCharacter) ClosePanels();
            else
            {
                _showCharacter = true;
                _showJournal = false;
                _inventorySelection = 0;
                SetMouseLook(false, forPanel: true);
            }
        }

        if (_showCharacter)
        {
            UpdateInventory(keyboard);
            return;
        }
        if (Pressed(keyboard, Keys.F2)) { _showSettings = !_showSettings; if (_showSettings) SetMouseLook(false, forPanel: true); }

        if (_showSettings)
        {
            UpdateSettings(keyboard);
            return;
        }

        if (_screen == GameScreen.WorldScene)
            UpdateCrouchToggle(keyboard);

        // A released pointer can click the active talk/shop/pickup prompt. A click anywhere
        // else returns to mouse-look; this prevents a UI click from becoming an attack.
        if (!_mouseLook && !_showHelp && !_dialogueOpen && !_showShop && !_showCharacter
            && Clicked(mouse) && IsActive
            && !TryActivateWorldPrompt(mouse))
            SetMouseLook(true);

        if (_screen == GameScreen.WorldScene && _world is not null)
        {
            if (_world.TryReloadIfChanged(out var reloadMessage))
            {
                LoadWatchers();
                LoadPickups();
                _session?.ShowToast(reloadMessage);
            }
            else if (!string.IsNullOrWhiteSpace(reloadMessage))
                _session?.ShowToast($"World reload failed: {reloadMessage}");
        }

        if (_screen == GameScreen.WorldScene && _dialogue is not null)
        {
            if (_dialogue.TryReloadIfChanged(out var reloadMessage))
            {
                LoadPockets();
                _session?.ShowToast(reloadMessage);
            }
            else if (!string.IsNullOrWhiteSpace(reloadMessage))
                _session?.ShowToast($"Dialogue reload failed: {reloadMessage}");
        }

        // Paused means paused: the camera stops too, or the world keeps moving behind a
        // screen that says it is stopped.
        if (!_paused && !_dialogueOpen && !_showJournal && !_showCharacter && !_showShop)
            UpdateCamera(gameTime, keyboard, mouse);

        if (_screen == GameScreen.WorldScene)
            UpdateSession(gameTime, keyboard, mouse);

        RestoreMouseLookAfterPanels();
    }

    /// <summary>True while any screen is holding the pointer.</summary>
    private bool AnyPanelOpen =>
        _dialogueOpen || _showShop || _showJournal || _showCharacter || _showHelp || _showSettings;

    /// <summary>
    /// Close everything and give the camera straight back.
    ///
    /// Every panel used to close itself in its own way, and dialogue closed itself in two
    /// different places, so whether the camera came back depended on which path you took out.
    /// One exit means one behaviour: the pointer goes, the camera moves, no click needed.
    /// </summary>
    private void ClosePanels()
    {
        _dialogueOpen = false;
        _conversationActor = null;
        _dialogueResponse = string.Empty;
        _showShop = false;
        _showJournal = false;
        _showCharacter = false;
        _showHelp = false;
        _showSettings = false;

        if (_screen == GameScreen.WorldScene) SetMouseLook(true);
    }

    /// <summary>
    /// Hand the camera back when the last panel closes.
    ///
    /// Closing a conversation used to leave the pointer free and the camera locked, so the
    /// player had to know to press Tab to carry on playing. Only pointer time that was taken
    /// *for a panel* is given back, so Tab still means what it says.
    /// </summary>
    private void RestoreMouseLookAfterPanels()
    {
        if (!_mouseFreedForPanel || _mouseLook) return;
        if (_screen != GameScreen.WorldScene) return;

        if (_dialogueOpen || _showShop || _showJournal || _showCharacter
            || _showHelp || _showSettings) return;

        SetMouseLook(true);
    }

    /// <summary>
    /// Drive the domain from the running game: advance its clock, feed it the player's
    /// position, and honour the save keys.
    /// </summary>
    private void UpdateSession(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        if (_session is null) return;

        // The camera is the player for now; a controller replaces this in iteration 7.
        _session.Position = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
        _session.Yaw = _cameraYaw;
        _session.Pitch = _cameraPitch;

        var step = StepSeconds(gameTime);
        _session.Player.Detection.SetCrouching(_crouching);
        _watchers?.Update(step, _session.Position);
        _session.Tick(step);

        if (_showJournal || _showCharacter)
        {
            return;
        }

        if (_showShop)
        {
            UpdateShopInput(keyboard, mouse);
            return;
        }

        if (_dialogueOpen)
        {
            UpdateDialogueInput(keyboard, mouse);
            return;
        }

        // Sprinting is the only thing that spends stamina yet, so it is what proves the
        // vitals on screen are the domain's numbers rather than painted ones.
        if (keyboard.IsKeyDown(Keys.LeftShift) && IsMoving(keyboard))
            _session.Player.Vitals.SpendStamina(18f * StepSeconds(gameTime));

        // A run cannot be saved out of. Being able to reload the moment a fight turns would
        // remove the only thing being risked, and the whole loop is the risk. Resuming an
        // interrupted descent is a separate feature and does not exist yet.
        if (Pressed(keyboard, Keys.F5))
            _session.ShowToast(_run is { Run.IsActive: true }
                ? "Not down here. Camp to bank what you are carrying."
                : _session.Save());

        if (Pressed(keyboard, Keys.F9) && _run is not { Run.IsActive: true }) LoadSession();

        if (Pressed(keyboard, Keys.P))
        {
            var actor = _dialogue?.FindActor(_session.Position, _cameraYaw);
            if (actor is not null) TryPickpocket(actor);
        }

        if (Pressed(keyboard, Keys.B))
        {
            var actor = _dialogue?.FindActor(_session.Position, _cameraYaw);
            if (actor is not null && actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                && _shop is not null)
            {
                _showShop = true;
                _shopSelection = 0;
                SetMouseLook(false);
            }
        }

        if (Pressed(keyboard, Keys.E))
        {
            var player = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            var actor = _dialogue?.FindActor(player, _cameraYaw);
            if (actor is not null)
            {
                OpenDialogue(actor);
            }
            else if (_world is not null)
            {
                var fixture = OnTheSurface ? Surface.FixtureAt(player) : SurfaceFixture.None;
                var pickup = FindPickup(player, _cameraYaw);

                if (fixture != SurfaceFixture.None)
                {
                    UseFixture(fixture);
                }
                else if (pickup is not null)
                {
                    TakePickup(pickup);
                }
                else if (_run is { BarsTheWay: true } && _world.FindDoor(player, _cameraYaw) is not null)
                {
                    _session.ShowToast("Not while something in here is still moving.");
                }
                else
                {
                    var result = _world.TryOpenDoor(player, _cameraYaw, _session.Player, out var door);
                    if (door is not null && door.Lock.IsOpen)
                        _recorder.Record(PlayEventKind.DoorOpened, door.Definition.Id, 0f, 0f,
                            _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
                    if (door is not null)
                    {
                        _session.ShowToast(result switch
                        {
                            LockResult.Opened => $"{door.Definition.Id} opened.",
                            LockResult.Unlocked => "The key turns. The door opens.",
                            LockResult.Failed => $"The lock resists. Security {door.Definition.Difficulty:0} required.",
                            _ => "The door is already open."
                        });
                    }
                }
            }
        }

        UpdateCombat(gameTime, keyboard);
    }

    /// <summary>
    /// The fight: enemies act, then the player does. Blocking is held rather than pressed,
    /// and attacking drops the guard, so the two cannot be used at once.
    /// </summary>
    private void UpdateCombat(GameTime gameTime, KeyboardState keyboard)
    {
        if (_session is null || _encounter is null) return;

        var step = StepSeconds(gameTime);
        TickVitalPulses(step);
        SampleStance(step);
        _encounter.Update(step, _cameraPosition, _cameraYaw);
        if (_world is not null) _run?.Update(_world, _cameraPosition, _encounter);
        _weaponView.Update(step, IsMoving(keyboard), _session.Player.Combat.IsBlocking);

        // Only while the pointer is captured, so a click that is reclaiming the mouse does
        // not also swing the sword.
        if (!_mouseLook || _showHelp) return;

        var mouse = Mouse.GetState();
        _session.Player.Combat.SetBlocking(mouse.RightButton == ButtonState.Pressed);

        if (Clicked(mouse))
        {
            var actor = _dialogue?.FindActor(
                new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z), _cameraYaw);
            if (actor is not null)
            {
                if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                    && _shop is not null)
                {
                    _showShop = true;
                    _shopSelection = 0;
                    SetMouseLook(false, forPanel: true);
                }
                else
                {
                    OpenDialogue(actor);
                }
                return;
            }

            var outcome = _encounter.PlayerAttack();

            // Melee was invisible to the recorder, so a session fought with the sword read
            // back as one where no melee happened at all.
            var struck = _encounter.Focused;
            _recorder.Record(PlayEventKind.MeleeSwing,
                _session.Player.Combat.ActiveWeapon.DisplayName,
                outcome.Damage, outcome.Result == AttackResult.Hit ? 1f : 0f,
                _session.Player.Vitals.Health, _session.Player.Vitals.Prana,
                struck?.Archetype.DisplayName ?? string.Empty,
                struck is null ? 0f : _encounter.PlayerPosition.FlatDistanceTo(struck.Position));

            // The arm moves whenever the swing actually happened — a hit and a miss look the
            // same from behind the weapon, which is what makes missing feel like missing
            // rather than like the button not working.
            if (outcome.Swung) _weaponView.Swing(_session.Player.Combat.ActiveWeapon);
            ReportAttack(outcome);
        }
        if (Pressed(keyboard, Keys.Q))
        {
            var cast = _encounter.PlayerCast(_cameraPosition, _cameraYaw, Forward);
            if (cast.WasCast)
            {
                _weaponView.Cast();
                var aimed = _encounter.Focused;
                _recorder.Record(PlayEventKind.SpellCast, cast.Spell?.DisplayName ?? "spell",
                    cast.Spell?.Power ?? 0f, 0f,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana,
                    aimed?.Archetype.DisplayName ?? string.Empty,
                    _encounter.NearestEnemyRange());
            }
            else
            {
                // A spell that would not go off is the moment the economy bites, and it is
                // usually the reason a mage picks the sword back up.
                _recorder.Record(PlayEventKind.CastFailed, cast.Spell?.DisplayName ?? "spell",
                    cast.Spell?.BaseCost ?? 0f, 0f,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
            }
            ReportCast(cast);
        }

        // Number keys pick the bound spell.
        if (Pressed(keyboard, Keys.D4)) SelectSpell(SpellCatalog.FireId);
        if (Pressed(keyboard, Keys.D5)) SelectSpell(SpellCatalog.FrostId);
        if (Pressed(keyboard, Keys.D6)) SelectSpell(SpellCatalog.ShockId);
        if (Pressed(keyboard, Keys.D7)) SelectSpell(SpellCatalog.HealId);
        if (Pressed(keyboard, Keys.D8)) SelectSpell(SpellCatalog.LightId);
    }

    private void OpenDialogue(SpeakingActor actor)
    {
        _conversationActor = actor;
        _dialogueOpen = true;
        _dialogueSelection = 0;
        var topics = actor.Talk();
        _dialogueResponse = topics.Count == 0
            ? $"{actor.DisplayName} has nothing to discuss."
            : $"{actor.DisplayName} looks your way. What do you want to know?";
        SetMouseLook(false);
    }

    /// <summary>
    /// Narrower and shorter than it was, with larger type. The old panel was 836x500 of
    /// mostly empty space set in 13-15 px text, which testers read as a wall.
    /// </summary>
    private static Rectangle DialoguePanelBounds => new(352, 150, 576, 420);

    /// <summary>Topic rows the panel shows before it stops listing.</summary>
    private const int DialogueRows = 6;

    private static Rectangle DialogueTopicBounds(int index) =>
        new(376, 300 + index * 34, 528, 30);

    private static Rectangle ShopItemBounds(int index) =>
        new(274, 232 + index * 52, 732, 42);

    private static Rectangle TalkPromptBounds() => new(302, 596, 224, 42);
    private static Rectangle SecondaryPromptBounds() => new(534, 596, 212, 42);
    private static Rectangle PickpocketPromptBounds() => new(754, 596, 224, 42);
    private static Rectangle SinglePromptBounds() => new(388, 596, 504, 42);

    /// <summary>Activate the prompt under a released mouse pointer instead of recapturing it.</summary>
    private bool TryActivateWorldPrompt(MouseState mouse)
    {
        if (_session is null || !Clicked(mouse)) return false;

        var pointer = LogicalMouse(mouse);
        var player = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
        var actor = _dialogue?.FindActor(player, _cameraYaw);
        if (actor is not null)
        {
            if (TalkPromptBounds().Contains((int)pointer.X, (int)pointer.Y))
            {
                OpenDialogue(actor);
                return true;
            }

            if (SecondaryPromptBounds().Contains((int)pointer.X, (int)pointer.Y))
            {
                if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                    && _shop is not null)
                {
                    _showShop = true;
                    _shopSelection = 0;
                    SetMouseLook(false);
                    return true;
                }

                if (HasPickablePocket(actor)
                    && PickpocketPromptBounds().Contains((int)pointer.X, (int)pointer.Y))
                {
                    TryPickpocket(actor);
                    return true;
                }
            }
        }

        if (!SinglePromptBounds().Contains((int)pointer.X, (int)pointer.Y)) return false;

        var pickup = FindPickup(player, _cameraYaw);
        if (pickup is not null)
        {
            TakePickup(pickup);
            return true;
        }

        if (_world is null) return false;
        var result = _world.TryOpenDoor(player, _cameraYaw, _session.Player, out var door);
        if (door is null) return false;

        _session.ShowToast(result switch
        {
            LockResult.Opened => $"{door.Definition.Id} opened.",
            LockResult.Unlocked => "The key turns. The door opens.",
            LockResult.Failed => $"The lock resists. Security {door.Definition.Difficulty:0} required.",
            _ => "The door is already open."
        });
        return true;
    }

    private void UpdateDialogueInput(KeyboardState keyboard, MouseState mouse)
    {
        if (_conversationActor is null)
        {
            _dialogueOpen = false;
            return;
        }

        var topics = _conversationActor.AvailableTopics();
        if (Pressed(keyboard, Keys.Escape))
        {
            ClosePanels();
            return;
        }

        if (topics.Count == 0) return;

        if (Pressed(keyboard, Keys.Up))
            _dialogueSelection = (_dialogueSelection + topics.Count - 1) % topics.Count;
        if (Pressed(keyboard, Keys.Down))
            _dialogueSelection = (_dialogueSelection + 1) % topics.Count;

        var numberKeys = new[]
        {
            Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5,
            Keys.D6, Keys.D7, Keys.D8, Keys.D9
        };
        for (var index = 0; index < numberKeys.Length && index < topics.Count; index++)
            if (Pressed(keyboard, numberKeys[index])) _dialogueSelection = index;

        var pointer = LogicalMouse(mouse);
        for (var index = 0; index < topics.Count && index < 9; index++)
        {
            var row = DialogueTopicBounds(index);
            if (!row.Contains((int)pointer.X, (int)pointer.Y)) continue;

            _dialogueSelection = index;
            if (Clicked(mouse)) AskDialogueTopic(topics[index]);
            return;
        }

        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
            AskDialogueTopic(topics[_dialogueSelection]);
    }

    private void AskDialogueTopic(string keyword)
    {
        if (_conversationActor is null) return;

        var topic = _session?.Player.Dialogue.Resolve(keyword, _conversationActor.Context);
        _dialogueResponse = _conversationActor.Ask(keyword)
            ?? "That topic does not lead anywhere here.";
        if (!string.IsNullOrWhiteSpace(topic?.QuestId)) AcceptQuest(topic.QuestId);
    }

    private void SelectSpell(string spellId)
    {
        if (_session is null) return;
        _session.Player.Spells.SelectSpell(spellId);
        _session.ShowToast($"{SpellCatalog.Get(spellId)!.DisplayName} readied.");
    }

    /// <summary>Only the outcomes the player cannot see for themselves are worth saying.</summary>
    private void ReportAttack(AttackOutcome outcome)
    {
        if (outcome.Result == AttackResult.Exhausted) _session?.ShowToast("Too exhausted.");
    }

    private void ReportCast(CastOutcome outcome)
    {
        if (_session is null) return;

        switch (outcome.Result)
        {
            case CastResult.NoCharge:
                _session.ShowToast("No prana, and no jiva stone to draw on.");
                break;
            case CastResult.Landed when outcome.Spell?.Effect == SpellEffect.Heal:
                _session.ShowToast($"{outcome.Spell.DisplayName} — restored.");
                break;
            case CastResult.Landed when outcome.Spell?.Effect == SpellEffect.Light:
                _session.ShowToast($"{outcome.Spell.DisplayName} — the dark pulls back.");
                break;
        }
    }

    /// <summary>
    /// Choosing and using an item.
    ///
    /// Testers found the inventory unintuitive because it was a list that did nothing: you
    /// could read your possessions but not drink, equip or wear any of them. Selection is
    /// driven from both the keyboard and the pointer so neither is the only way in.
    /// </summary>
    private void UpdateInventory(KeyboardState keyboard)
    {
        if (_session is null) return;

        var items = _session.Player.Inventory.Items;
        if (items.Count == 0)
        {
            _inventorySelection = 0;
            return;
        }

        _inventorySelection = Math.Clamp(_inventorySelection, 0, items.Count - 1);

        if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W))
            _inventorySelection = (_inventorySelection + items.Count - 1) % items.Count;
        if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S))
            _inventorySelection = (_inventorySelection + 1) % items.Count;

        var mouse = Mouse.GetState();
        var pointer = LogicalMouse(mouse);
        var hovered = -1;
        for (var index = 0; index < items.Count && index < InventoryRows; index++)
            if (InventoryRowBounds(index).Contains((int)pointer.X, (int)pointer.Y))
                hovered = index;

        if (hovered >= 0) _inventorySelection = hovered;

        var activate = Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space)
            || (hovered >= 0 && Clicked(mouse));
        if (!activate) return;

        var item = items[_inventorySelection];
        var name = item.Name;
        var result = ItemUse.Use(item.Id, _session.Player);
        _recorder.Record(PlayEventKind.ItemUsed, item.Name, 0f, 0f,
            _session.Player.Vitals.Health);

        _session.ShowToast(result switch
        {
            ItemUseResult.Used => $"Used {name}.",
            ItemUseResult.Equipped => $"Equipped {name}.",
            ItemUseResult.NoEffect => $"{name} would do nothing right now.",
            ItemUseResult.NotUsable => $"{name} is not something you can use.",
            _ => $"You are not carrying {name}."
        });

        // Consuming the last of a stack shortens the list under the selection.
        _inventorySelection = Math.Clamp(_inventorySelection, 0,
            Math.Max(0, _session.Player.Inventory.Items.Count - 1));
    }

    /// <summary>Rows the character screen can show before it stops listing.</summary>
    private const int InventoryRows = 10;

    /// <summary>
    /// One inventory row. Drawing and hit testing share it, so a clickable row is always
    /// exactly the row on screen.
    /// </summary>
    private static Rectangle InventoryRowBounds(int index) => new(480, 214 + index * 34, 356, 32);

    private static bool IsMoving(KeyboardState keyboard) =>
        keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.A)
        || keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.D);

    private void UpdateCrouchToggle(KeyboardState keyboard)
    {
        if (!_forceCrouch
            && (Pressed(keyboard, Keys.LeftControl) || Pressed(keyboard, Keys.RightControl)))
            _crouchToggled = !_crouchToggled;

        _crouching = _forceCrouch || _crouchToggled;
    }

    /// <summary>
    /// Begin a session and populate the world around it. The camp is spawned here rather
    /// than by the session, because where a bandit stands is a scene fact, not a save fact —
    /// the save only remembers which ones are already dead.
    /// </summary>
    private void StartSession(GameSession session)
    {
        // The session goes in first: a generated mine needs to know whose body is lying in
        // it before it is built.
        var changingCharacter = !ReferenceEquals(_session, session);
        _session = session;
        LoadWorldManifest();
        LoadQuestManifest();
        LoadDialogueManifest();
        LoadWatchers();
        LoadPockets();
        LoadPickups();
        LoadShop();
        if (changingCharacter) _session.Player.Quests.Changed += RefreshQuestObjective;
        _dialogueOpen = false;
        _showJournal = false;
        _showCharacter = false;
        _showShop = false;
        _questObjectiveId = string.Empty;
        _world?.RestoreOpenedDoors(session.Player.Story.State.OpenedLocks);
        _encounter = new Encounter(session);
        WatchForTheRecord(_encounter, session);
        SpawnEnemies();
        StartRun();

        if (!changingCharacter) return;

        session.Player.Vitals.Died += () =>
        {
            // Down in a mine, dying ends the run and forfeits the pot. Above ground it is
            // still the old forgiving reset, because there is nothing there to lose.
            if (_run is { Run.IsActive: true })
            {
                var lostRun = _run.Die();
                _recorder.Record(PlayEventKind.Died, $"after {lostRun.RoomsCleared} rooms",
                    lostRun.StonesLost, 0f, 0f);

                // Somebody else takes the lamp, and this one stays where they fell.
                _succession = Succession.Promote(session.Player, lostRun,
                    _mineSeed ?? 0, _run.DeepestRoom);

                EndRun(lostRun);
                return;
            }

            session.ShowToast("You were defeated — returned to safe ground.");
            session.Player.Vitals.FullRestore();
            session.Player.Combat.ClearCombat();
            ResetCamera();
        };
    }

    /// <summary>
    /// Fill the scene from the level file, falling back to the authored camp for the hand-made
    /// world, which predates spawns being part of the manifest.
    /// </summary>
    private void SpawnEnemies()
    {
        if (_encounter is null) return;

        // Enemies used to walk through walls: their pursuit never consulted the world.
        if (_world is not null) _encounter.UseCollision(_world.Collision);

        // A generated mine holds each room's occupants back until it is walked into.
        var byRoom = _world?.Manifest.Rooms.Count > 1;
        if (_world?.Manifest.Spawns is { Count: > 0 }
            && _encounter.SpawnFrom(_world.Manifest, byRoom) > 0)
            return;

        _encounter.SpawnDefaultCamp();
    }

    /// <summary>
    /// Note where the player is standing, once a second, while a descent is underway.
    ///
    /// Every other event in the log is something the player did. This is the only record of
    /// the spaces in between, and it is the only way to answer whether a fight was taken in a
    /// room or through the doorway of the one before it — which decides whether shaping rooms
    /// is worth any effort at all.
    /// </summary>
    private void SampleStance(float deltaSeconds)
    {
        if (_session is null || _encounter is null || _world is null) return;
        if (_run is not { Run.IsActive: true } run) return;

        _stanceCountdown -= deltaSeconds;
        if (_stanceCountdown > 0f) return;
        _stanceCountdown = StanceSampleSeconds;

        var (where, inDoorway) = run.Stance(_world, _cameraPosition);
        _recorder.Record(PlayEventKind.Stance, where,
            _encounter.NearestEnemyRange(), inDoorway ? 1f : 0f,
            _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
    }

    /// <summary>Light a bar that has just been restored, and fade the light back out.</summary>
    private void TickVitalPulses(float deltaSeconds)
    {
        if (_session is null) return;

        var vitals = _session.Player.Vitals;

        // A whole point, so the slow out-of-combat prana trickle does not keep the bar lit.
        if (vitals.Health > _lastHealth + 1f) _healthPulse = 1f;
        if (vitals.Prana > _lastPrana + 1f) _pranaPulse = 1f;

        _lastHealth = vitals.Health;
        _lastPrana = vitals.Prana;

        var fade = deltaSeconds / PulseSeconds;
        _healthPulse = MathF.Max(0f, _healthPulse - fade);
        _pranaPulse = MathF.Max(0f, _pranaPulse - fade);
    }

    /// <summary>
    /// Subscribe the recorder to the fight.
    ///
    /// Everything here is something that already happened for its own reasons; the recorder
    /// only listens. Nothing in the game asks whether it is recording, which is what keeps it
    /// impossible for telemetry to change how the game plays.
    /// </summary>
    private void WatchForTheRecord(Encounter encounter, GameSession session)
    {
        encounter.EnemyDefeated += enemy => _recorder.Record(PlayEventKind.EnemyKilled,
            // What did the most damage to it, not what struck last: a spell that softened
            // something and a sword that finished it would otherwise read as a sword kill.
            enemy.KilledBy, enemy.Archetype.Level, 0f,
            session.Player.Vitals.Health, session.Player.Vitals.Prana,
            enemy.Archetype.DisplayName,
            encounter.PlayerPosition.FlatDistanceTo(enemy.Position));

        encounter.SpellLanded += (spell, enemy, range) => _recorder.Record(PlayEventKind.SpellHit,
            spell.DisplayName, spell.Power, 0f,
            session.Player.Vitals.Health, session.Player.Vitals.Prana,
            enemy.Archetype.DisplayName, range);

        encounter.PlayerStruck += (damage, guarded) => _recorder.Record(PlayEventKind.PlayerHurt,
            guarded ? "guarded" : "clean", damage, 0f, session.Player.Vitals.Health);
    }

    /// <summary>Start the ledger for this descent, if the loaded world is a mine at all.</summary>
    private void StartRun()
    {
        _run = null;
        _runSummary = null;

        if (_world is null || _mineSeed is not { } seed) return;
        if (_world.Manifest.Rooms.Count < 2) return;

        _run = new RunRuntime(_world.Manifest, seed, _mineDepth);
        _decisionRecorded = false;

        _recorder.Record(PlayEventKind.RunStarted, _world.Manifest.Id, seed, _mineDepth,
            _session?.Player.Vitals.Health ?? 0f);

        _run.RoomEntered += room => _recorder.Record(PlayEventKind.RoomEntered,
            $"room {room}", room, 0f, _session?.Player.Vitals.Health ?? 0f,
            _session?.Player.Vitals.Prana ?? 0f);

        _run.RoomCleared += paid =>
        {
            _session?.ShowToast($"Room clear.  +{paid} stones held  ({_run.Run.Pending} at risk)");
            _recorder.Record(PlayEventKind.RoomCleared, $"room {_run.DeepestRoom}", paid,
                _run.Run.Pending, _session?.Player.Vitals.Health ?? 0f,
                _session?.Player.Vitals.Prana ?? 0f);
        };
    }

    /// <summary>
    /// Put the run away and show what it was worth.
    ///
    /// Camping pays out here rather than in the domain because this is where the inventory
    /// lives; the ledger's job ends at deciding the number.
    /// </summary>
    private void EndRun(RunResult result)
    {
        _runSummary = result;
        if (result.Survived) _succession = null;
        SetMouseLook(false, forPanel: true);

        _recorder.Record(PlayEventKind.RunEnded,
            result.Survived ? "camped" : "died", result.RoomsCleared, result.Tier,
            _session?.Player.Vitals.Health ?? 0f);
        _recorder.Flush();

        if (_session is null) return;

        var saveMessage = _session.CompleteRun(result, SurfaceCheckpoint);
        if (!string.Equals(saveMessage, "Saved.", StringComparison.Ordinal))
            _menuStatus = saveMessage;
    }

    private bool LoadSession()
    {
        if (_session is null) StartSession(GameSession.NewGame());

        if (!_session!.TryLoad(out var message))
        {
            if (_screen == GameScreen.MainMenu) _menuStatus = message;
            else _session.ShowToast(message);
            return false;
        }

        _cameraPosition = new Vector3(_session.Position.X, _session.Position.Y, _session.Position.Z);
        _cameraYaw = _session.Yaw;
        _world?.RestoreOpenedDoors(_session.Player.Story.State.OpenedLocks);
        LoadPockets();
        LoadPickups();
        LoadShop();
        RefreshQuestObjective();

        _encounter = new Encounter(_session);
        WatchForTheRecord(_encounter, _session);
        SpawnEnemies();
        StartRun();

        _session.ShowToast(message);
        _menuStatus = string.Empty;
        return true;
    }

    private void SetBorderlessFullscreen(bool enabled)
    {
        _borderlessFullscreen = enabled;

        if (enabled)
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = Math.Max(displayMode.Width, LogicalWidth);
            _graphics.PreferredBackBufferHeight = Math.Max(displayMode.Height, LogicalHeight);
            _graphics.IsFullScreen = true;
            _graphics.HardwareModeSwitch = false;
            Window.IsBorderless = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = LogicalWidth;
            _graphics.PreferredBackBufferHeight = LogicalHeight;
            _graphics.IsFullScreen = false;
            Window.IsBorderless = false;
        }

        _graphics.ApplyChanges();
        UpdateUiTransform();
    }

    private void UpdateUiTransform()
    {
        var viewport = GraphicsDevice.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        var scale = MathF.Min(viewport.Width / (float)LogicalWidth, viewport.Height / (float)LogicalHeight)
            * _uiScalePreference;
        var offsetX = (viewport.Width - LogicalWidth * scale) * 0.5f;
        var offsetY = (viewport.Height - LogicalHeight * scale) * 0.5f;
        _uiTransform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(offsetX, offsetY, 0f);

        // A changed scale invalidates every cached atlas: they are rasterized in device pixels.
        if (MathF.Abs(scale - _uiScale) < 0.001f) return;
        _uiScale = scale;
        _bodyFonts.Clear();
        _headingFonts.Clear();
    }

    private void LoadModel(string key, string contentPath)
    {
        try
        {
            var model = Content.Load<Model>(contentPath);
            _models[key] = model;

            var bones = new Matrix[model.Bones.Count];
            if (bones.Length > 0) model.CopyAbsoluteBoneTransformsTo(bones);
            var (center, extent) = MeasureModel(model, bones);
            _modelCenters[key] = center;
            _modelNormalizers[key] = 1f / extent;
            _modelBones[key] = bones;

            ConfigureModelLighting(model);
        }
        catch (Exception exception)
        {
            _assetErrors.Add($"{key}: {exception.GetType().Name}");
        }
    }

    /// <summary>
    /// Lighting, fog and material settings that never change. Applied once per loaded model
    /// rather than per mesh per frame.
    /// </summary>
    private static void ConfigureModelLighting(Model model)
    {
        foreach (var mesh in model.Meshes)
        foreach (var effect in mesh.Effects)
        {
            if (effect is not BasicEffect basic) continue;

            basic.EnableDefaultLighting();
            basic.PreferPerPixelLighting = true;
            basic.AmbientLightColor = new Vector3(0.54f, 0.57f, 0.62f);
            basic.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -1f, -0.2f));
            basic.DirectionalLight0.DiffuseColor = new Vector3(1f, 0.84f, 0.68f);
            basic.DirectionalLight0.SpecularColor = new Vector3(0.24f);
            basic.FogEnabled = true;
            basic.FogStart = 18f;
            basic.FogEnd = 45f;
            basic.FogColor = new Color(70, 88, 91).ToVector3();
        }
    }

    private void UpdateCamera(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        var seconds = StepSeconds(gameTime);
        var speed = keyboard.IsKeyDown(Keys.LeftShift) ? SprintSpeed : WalkSpeed;
        var yawInput = 0f;
        var pitchInput = 0f;

        if (keyboard.IsKeyDown(Keys.Left)) yawInput -= 1f;
        if (keyboard.IsKeyDown(Keys.Right)) yawInput += 1f;
        if (keyboard.IsKeyDown(Keys.Up)) pitchInput += 1f;
        if (keyboard.IsKeyDown(Keys.Down)) pitchInput -= 1f;

        _cameraYaw += yawInput * seconds * KeyboardTurnSpeed;
        _cameraPitch = MathHelper.Clamp(
            _cameraPitch + pitchInput * seconds * KeyboardTurnSpeed, -PitchLimit, PitchLimit);

        // Mouse look is framerate-independent by construction: it is pixels moved, not a
        // rate held over time, so it must not be multiplied by the frame duration.
        var lookDelta = ReadMouseDelta(mouse);
        if (lookDelta != Vector2.Zero)
        {
            _cameraYaw += lookDelta.X * MouseSensitivity;
            _cameraPitch = MathHelper.Clamp(
                _cameraPitch - lookDelta.Y * MouseSensitivity, -PitchLimit, PitchLimit);
        }

        var forward = Forward;
        var flatForward = new Vector3(forward.X, 0f, forward.Z);
        if (flatForward.LengthSquared() > 0.001f)
            flatForward.Normalize();

        var right = Vector3.Cross(flatForward, Vector3.Up);
        var movement = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) movement += flatForward;
        if (keyboard.IsKeyDown(Keys.S)) movement -= flatForward;
        if (keyboard.IsKeyDown(Keys.A)) movement -= right;
        if (keyboard.IsKeyDown(Keys.D)) movement += right;

        // Space is a jump, not a vertical free-flight throttle. Keeping a small explicit
        // vertical state also makes a held key produce one jump instead of levitation.
        if (Pressed(keyboard, Keys.Space) && _grounded)
        {
            _verticalVelocity = JumpSpeed;
            _grounded = false;
        }

        _verticalVelocity -= Gravity * seconds;
        _verticalOffset = MathF.Max(0f, _verticalOffset + _verticalVelocity * seconds);
        if (_verticalOffset <= 0.0001f)
        {
            _verticalOffset = 0f;
            _verticalVelocity = 0f;
            _grounded = true;
        }

        var targetEyeY = _standingEyeY - (_crouching ? CrouchDrop : 0f);
        var currentEyeY = _cameraPosition.Y - _verticalOffset;
        var crouchBlend = 1f - MathF.Exp(-CrouchLerpSpeed * seconds);
        var nextEyeY = MathHelper.Lerp(currentEyeY, targetEyeY, crouchBlend);

        if (movement.LengthSquared() > 0.001f)
        {
            movement.Normalize();
            var delta = movement * speed * seconds;
            if (_screen == GameScreen.WorldScene && _world is not null)
            {
                var current = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
                var resolved = _world.Move(current,
                    new WorldPoint(delta.X, 0f, delta.Z), PlayerCollisionRadius);
                _cameraPosition = new Vector3(resolved.X, nextEyeY + _verticalOffset, resolved.Z);
            }
            else
            {
                _cameraPosition = new Vector3(_cameraPosition.X + delta.X,
                    nextEyeY + _verticalOffset, _cameraPosition.Z + delta.Z);
            }
        }
        else
        {
            _cameraPosition.Y = nextEyeY + _verticalOffset;
        }
    }

    private void UpdateCameraMatrices()
    {
        _view = Matrix.CreateLookAt(_cameraPosition, _cameraPosition + Forward, Vector3.Up);
    }

    /// <summary>
    /// Where the camera is pointing.
    ///
    /// Yaw is negated because CreateRotationY turns anticlockwise: without this a rising
    /// yaw swung the view left while D strafed right, so both the mouse and the arrow keys
    /// were inverted horizontally. Yaw now increases clockwise (right) and pitch increases
    /// upward, matching the movement axes and every other first-person game.
    /// </summary>
    private Vector3 Forward => Vector3.Transform(
        Vector3.Forward,
        Matrix.CreateRotationX(_cameraPitch) * Matrix.CreateRotationY(-_cameraYaw));

    /// <summary>Where the player currently is, for the banner across the top of the HUD.</summary>
    private string LocationCaption() => _mineSeed is { } seed
        // The decimal seed, because that is what --mine takes: a mine worth replaying or
        // reporting can be asked for again exactly.
        ? $"MINE {seed}  ·  TIER {_mineDepth}"
        : "THE YARD  ·  RATNA BAY";

    private void ResetCamera()
    {
        var spawn = _world?.Manifest.PlayerSpawn;
        if (spawn is not null)
        {
            _standingEyeY = spawn.Position.Y;
            _cameraPosition = new Vector3(spawn.Position.X, spawn.Position.Y, spawn.Position.Z);
            _cameraYaw = spawn.Yaw;
        }
        else
        {
            _standingEyeY = 2.4f;
            _cameraPosition = new Vector3(0f, 2.4f, 8.5f);
            _cameraYaw = 0f;
        }
        _verticalOffset = 0f;
        _verticalVelocity = 0f;
        _grounded = true;
        _crouching = false;
        _crouchToggled = false;
        _cameraPitch = -0.12f;
    }

    private void LoadWorldManifest()
    {
        if (_world is not null) return;

        if (_mineSeed is { } seed)
        {
            var manifest = MineGenerator.Generate(seed, _mineRooms, _mineDepth);
            PlaceTheFallen(manifest, seed);

            if (!WorldRuntime.TryCreate(manifest, out var generated, out var generationError))
            {
                _assetErrors.Add(generationError);
                return;
            }

            _world = generated;
            return;
        }

        // Above ground is the yard. It is where a run starts, where it ends, and the only
        // place stones turn into anything — which is the half of the loop that did not exist.
        if (!WorldRuntime.TryCreate(Surface.Build(), out var yard, out var yardError))
        {
            _assetErrors.Add(yardError);
            return;
        }

        _world = yard;
    }

    /// <summary>True while the player is standing in the yard rather than down a mine.</summary>
    private bool OnTheSurface => _mineSeed is null;

    /// <summary>The one pickup that is not part of the level it appears in.</summary>
    private const string CachePickupId = "cache.fallen";

    /// <summary>
    /// Put the last Deepankar's cache into the mine that killed them.
    ///
    /// Added to the manifest rather than special-cased at runtime, so it is found, taken,
    /// saved and remembered by exactly the same machinery as everything else on the floor.
    /// </summary>
    private void PlaceTheFallen(WorldManifest manifest, int seed)
    {
        if (_session is null) return;

        var cache = _session.Player.Legacy.Fallen;
        if (cache is null || cache.MineSeed != seed) return;

        var room = manifest.Rooms.FirstOrDefault(candidate => candidate.Index == cache.RoomIndex)
            ?? manifest.Rooms.LastOrDefault();
        if (room is null) return;

        manifest.Pickups.Add(new WorldPickup
        {
            Id = CachePickupId,
            ItemId = SoulCrystals.LesserId,
            Name = string.IsNullOrWhiteSpace(cache.Name)
                ? "A Deepankar's Cache"
                : $"{cache.Name}'s Cache",
            Kind = SoulCrystals.ItemKind,
            Count = cache.Stones,
            Position = new WorldVector(room.Centre.X, 0.1f, room.Centre.Z),
            Model = "cheeseBox",
            Scale = 0.6f
        });
    }

    private void LoadDialogueManifest()
    {
        if (_session is null) return;

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Dialogue", "northwatch.json");
        if (!DialogueRuntime.TryLoad(path, _session.Player.Dialogue, out var dialogue, out var error))
        {
            _assetErrors.Add(error);
            _dialogue = null;
            return;
        }

        _dialogue = dialogue;
    }

    private void LoadWatchers()
    {
        if (_session is null || _world is null) return;
        if (_watchers is null)
            _watchers = new WatcherRuntime(_world.Manifest, _world.Collision,
                _session.Player.Detection);
        else
            _watchers.Reload(_world.Manifest);
    }

    private void LoadPockets()
    {
        _pockets.Clear();
        if (_session is null || _dialogue is null) return;

        foreach (var actor in _dialogue.Actors)
        {
            var pocket = _dialogue.PocketOf(actor.ActorId);
            var alreadyLifted = _session.Player.Story.State.LootedObjects.Contains(
                $"pickpocket.{actor.ActorId}", StringComparer.Ordinal);

            // Contents come from the manifest so a pocket can hold something that matters —
            // the watchpost key rather than a nameless purse.
            var contents = alreadyLifted || pocket is null
                ? Array.Empty<ItemStack>()
                : pocket.Items.Select(item => new ItemStack
                {
                    Id = item.Id, Name = item.Name, Kind = item.Kind, Count = item.Count
                }).ToArray();

            _pockets[actor.ActorId] = new PickpocketTarget(pocket?.Difficulty ?? 0f, contents);
        }
    }

    private void LoadPickups()
    {
        _pickups.Clear();
        if (_session is null || _world is null) return;

        foreach (var pickup in _world.Manifest.Pickups ?? new List<WorldPickup>())
        {
            if (_session.Player.Story.State.LootedObjects.Contains(
                    $"pickup.{pickup.Id}", StringComparer.Ordinal))
                continue;

            _pickups.Add(pickup);
        }
    }

    private void LoadShop()
    {
        if (_session is null) return;
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Shops", "northwatch.json");
        if (!ShopManifest.TryLoad(path, out var manifest, out var error))
        {
            _assetErrors.Add(error);
            _shop = null;
            return;
        }

        var definition = manifest!.ToDefinitions().FirstOrDefault();
        if (definition is null)
        {
            _shop = null;
            return;
        }

        _shop = new Shop(definition);
        foreach (var item in definition.Items)
            if (_session.Player.Story.State.LootedObjects.Contains(
                    $"shop.{definition.Id}.{item.Id}", StringComparer.Ordinal))
                _shop.MarkSoldOut(item.Id);
    }

    /// <summary>True when this actor is carrying something that has not been lifted yet.</summary>
    private bool HasPickablePocket(SpeakingActor actor) =>
        _pockets.TryGetValue(actor.ActorId, out var target) && target.RemainingItems > 0;

    private void TryPickpocket(SpeakingActor actor)
    {
        if (_session is null || !_pockets.TryGetValue(actor.ActorId, out var target)) return;

        var outcome = Pickpocketing.TryTake(target, _session.Player.Skills,
            _session.Player.Inventory, _session.Player.Detection);
        if (outcome.TookSomething)
            _session.Player.Story.MarkLooted($"pickpocket.{actor.ActorId}");

        _session.ShowToast(outcome.Result switch
        {
            PickpocketResult.Taken => $"You lifted {outcome.Item!.Name}.",
            PickpocketResult.Caught => "A watcher noticed your hand.",
            PickpocketResult.TooDifficult => "The pocket is beyond your Security skill.",
            _ => "There is nothing left to take."
        });
    }

    private WorldPickup? FindPickup(WorldPoint player, float yaw, float range = 3.2f)
    {
        var forward = Targeting.FlatForward(yaw);
        WorldPickup? best = null;
        var bestDistance = float.MaxValue;

        foreach (var pickup in _pickups)
        {
            var distance = player.FlatDistanceTo(pickup.Position.ToWorldPoint());
            if (distance > range || distance >= bestDistance) continue;

            var dx = pickup.Position.X - player.X;
            var dz = pickup.Position.Z - player.Z;
            if (distance > 0.001f && (dx * forward.X + dz * forward.Z) / distance < 0.35f)
                continue;

            best = pickup;
            bestDistance = distance;
        }

        return best;
    }

    private void TakePickup(WorldPickup pickup)
    {
        if (_session is null) return;

        _session.Player.Inventory.Add(pickup.ItemId, pickup.Name, pickup.Count, pickup.Kind);
        _session.Player.Story.MarkLooted($"pickup.{pickup.Id}");
        _pickups.Remove(pickup);

        if (string.Equals(pickup.Id, CachePickupId, StringComparison.Ordinal))
        {
            _session.Player.Legacy.Recover();
            _session.ShowToast($"You lift {pickup.Name}. {pickup.Count} stones come home.");
            _recorder.Record(PlayEventKind.CacheRecovered, pickup.Name, pickup.Count, 0f,
                _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
            return;
        }

        _session.ShowToast($"Taken: {pickup.Name} x{pickup.Count}.");
    }

    private void UpdateShopInput(KeyboardState keyboard, MouseState mouse)
    {
        if (_shop is null) return;

        var items = _shop.Definition.Items;
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.B))
        {
            _showShop = false;
            return;
        }
        if (items.Count == 0) return;

        if (Pressed(keyboard, Keys.Up))
            _shopSelection = (_shopSelection + items.Count - 1) % items.Count;
        if (Pressed(keyboard, Keys.Down))
            _shopSelection = (_shopSelection + 1) % items.Count;

        var pointer = LogicalMouse(mouse);
        for (var index = 0; index < items.Count; index++)
        {
            var row = ShopItemBounds(index);
            if (!row.Contains((int)pointer.X, (int)pointer.Y)) continue;

            _shopSelection = index;
            if (Clicked(mouse)) BuySelectedShopItem();
            return;
        }

        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
            BuySelectedShopItem();
    }

    private void BuySelectedShopItem()
    {
        if (_shop is null || _session is null) return;

        var result = _shop.Buy(_shopSelection, _session.Player.Vitals,
            _session.Player.Inventory, out var item);
        if (result == ShopPurchaseResult.Bought && item is not null)
        {
            _session.Player.Story.MarkLooted($"shop.{_shop.Definition.Id}.{item.Id}");
            _session.ShowToast($"Bought {item.Name}.");
        }
        else
        {
            _session.ShowToast(result switch
            {
                ShopPurchaseResult.TooExpensive => "Not enough gold.",
                ShopPurchaseResult.SoldOut => "That stock is gone.",
                _ => "That item is not available."
            });
        }
    }

    private void LoadQuestManifest()
    {
        if (_session is null) return;

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Quests", "northwatch.json");
        if (!QuestManifest.TryLoad(path, out var manifest, out var error))
        {
            _assetErrors.Add(error);
            return;
        }

        _session.Player.Quests.RegisterRange(manifest!.ToDefinitions());
    }

    private void AcceptQuest(string questId)
    {
        if (_session is null) return;

        var quest = _session.Player.Quests.Activate(questId);
        if (quest is null)
        {
            _session.ShowToast($"Unknown quest: {questId}.");
            return;
        }

        if (quest.IsCompleted)
        {
            _session.ShowToast("That work is already complete.");
            return;
        }

        _session.Player.Story.SetFlag($"flag.quest.{quest.Id}.accepted");
        _session.ShowToast($"Quest accepted: {quest.Title}.");
        RefreshQuestObjective();
    }

    private void RefreshQuestObjective()
    {
        if (_session is null) return;

        var quest = _session.Player.Quests.Active.FirstOrDefault();
        if (quest is null)
        {
            if (_questObjectiveId.Length > 0)
            {
                _session.Player.Objective.Clear();
                _questObjectiveId = string.Empty;
            }
            return;
        }

        var definition = quest.Definition;
        var progress = definition.TargetCount > 0
            ? $" ({quest.Progress}/{definition.TargetCount})"
            : string.Empty;
        var title = quest.Title + progress;
        var directions = string.IsNullOrWhiteSpace(definition.ObjectiveDirections)
            ? quest.StageText
            : definition.ObjectiveDirections;

        if (_questObjectiveId == quest.Id
            && _session.Player.Objective.Title == title
            && _session.Player.Objective.Directions == directions)
            return;

        _session.Player.Objective.Set(title, directions, definition.ObjectiveAnchorId,
            definition.ObjectivePosition);
        _questObjectiveId = quest.Id;
    }

    private void DrawMenu()
    {
        GraphicsDevice.Clear(new Color(40, 58, 68));

        BeginUi();
        Fill(new Rectangle(0, 0, 1280, 720), new Color(3, 7, 12, 178));
        DrawPanel(new Rectangle(64, 62, 1152, 596), new Color(5, 11, 18, 232), new Color(91, 146, 159));

        Text("RATNA BAY", new Vector2(98, 96), 38, Color.White);
        Text("NORTHWATCH SLICE", new Vector2(101, 153), 13, new Color(161, 211, 218));
        TextFit("Explore, talk, trade, and survive", new Vector2(101, 181), 420f, 15, new Color(184, 197, 196));

        DrawPanel(new Rectangle(96, 222, 416, 390), new Color(8, 16, 24, 238), new Color(65, 105, 119));
        Text("MAIN MENU", new Vector2(124, 246), 14, new Color(214, 183, 108));

        var menuItems = MenuItems;
        for (var index = 0; index < menuItems.Length; index++)
        {
            var itemBounds = MenuItemBounds(index);
            var selected = index == _menuSelection;
            DrawPanel(
                itemBounds,
                selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected ? new Color(224, 181, 88) : new Color(54, 82, 91));
            Text((index + 1).ToString("00"), new Vector2(itemBounds.X + 16, itemBounds.Y + 9), 13, selected ? new Color(245, 209, 124) : new Color(112, 148, 155));
            Text(menuItems[index], new Vector2(itemBounds.X + 62, itemBounds.Y + 7), 18, selected ? Color.White : new Color(192, 207, 205));
        }

        DrawPanel(new Rectangle(560, 222, 592, 390), new Color(8, 16, 24, 226), new Color(65, 105, 119));

        var descending = menuItems[_menuSelection] == ResumeItem;
        Text(descending ? "BELOW RATNA BAY" : "NORTHWATCH OUTSKIRTS",
            new Vector2(592, 246), 14, new Color(151, 206, 210));
        Text(descending ? "A DESCENT" : "A NORTHWATCH BEGINNING",
            new Vector2(592, 280), 24, Color.White);

        var blurb = descending
            ? new[]
            {
                "A mine that has never been walked before.",
                "Clear a room and it pays. Clear the next and it pays more.",
                "Camp at a door to bank it, or open the door and risk the lot."
            }
            : new[]
            {
                "Meet the people at the gate and find your footing.",
                "Talk, trade, explore the old road, and face the bandits.",
                "Your choices and discoveries persist in your save."
            };

        for (var line = 0; line < blurb.Length; line++)
            TextFit(blurb[line], new Vector2(592, 326 + line * 24), 500f, 15,
                new Color(190, 203, 200));

        Text("WHAT YOU CAN DO", new Vector2(592, 414), 12, new Color(214, 183, 108));

        var doing = descending
            ? new[] { "Fight through generated rooms", "Bank your stones, or press on", "Die and lose the lot" }
            : new[] { "Explore Northwatch", "Talk and trade with locals", "Fight, sneak, and save" };

        for (var line = 0; line < doing.Length; line++)
            Text(doing[line], new Vector2(592, 442 + line * 26), 14, new Color(190, 215, 208));

        Text("Click or hover to choose      Up / Down select      Enter confirm      Esc safe",
            new Vector2(98, 610), 14, new Color(163, 191, 194));
        if (!string.IsNullOrWhiteSpace(_menuStatus))
            TextFit(_menuStatus, new Vector2(592, 542), 520f, 14, new Color(228, 128, 118));
        if (_showSettings) DrawSettings();
        EndUi();
    }

    private void DrawSettings()
    {
        Fill(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 7, 12, 214));
        var panel = new Rectangle(260, 92, 760, 536);
        DrawPanel(panel, new Color(7, 14, 21, 248), new Color(91, 146, 159));
        Text("SETTINGS", new Vector2(panel.X + 32, panel.Y + 28), 28, Color.White);
        Text("Display, interface and current bindings", new Vector2(panel.X + 34, panel.Y + 70), 15,
            new Color(163, 191, 194));

        var options = new[]
        {
            $"Display mode     {(_borderlessFullscreen ? "Borderless fullscreen" : "Windowed 1280x720")}",
            $"UI scale          {_uiScalePreference:0.0}x",
            "Bindings          WASD move | E interact | J journal | I character"
        };
        for (var index = 0; index < options.Length; index++)
        {
            var selected = index == _settingsSelection;
            var row = new Rectangle(panel.X + 24, panel.Y + 122 + index * 56, panel.Width - 48, 42);
            DrawPanel(row, selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected ? new Color(224, 181, 88) : new Color(54, 82, 91));
            TextFit(options[index], new Vector2(row.X + 16, row.Y + 10), row.Width - 32, 16,
                selected ? Color.White : new Color(203, 216, 214));
        }

        Text("Up / Down select   Left / Right change UI scale   Enter toggle display   Esc close",
            new Vector2(panel.X + 32, panel.Bottom - 38), 13, new Color(163, 191, 194));
    }

    private void DrawWorldScene()
    {
        if (_stambhaPreview)
        {
            // Framed as the trailer's opening: close on the pillar, the stone low and left.
            _cameraPosition = new Vector3(0f, 0.75f, 2.1f);
            _cameraPitch = -0.02f;
            _cameraYaw = 0f;
            UpdateCameraMatrices();

            DrawStambhaPreview();
            return;
        }

        DrawAuthoredWorld();
        DrawSpeakingActors();
        DrawWatchers();
        DrawEnemies();
        DrawBolts();

        BeginUi();

        // A full-screen panel owns the screen. Leaving the combat HUD drawing underneath it
        // was most of why testers called the inventory cluttered.
        var panelOpen = _showHelp || _showJournal || _showCharacter || _showShop;

        if (!panelOpen)
        {
            DrawWeapon();
            DrawDamageFlash();
            DrawSneakOverlay();
            DrawThreatArrows();
            DrawFloatingNumbers();
            DrawCrosshair();
            DrawHitMarker();
            DrawDamageDirections();
            DrawSpellBar();
            DrawCastBanner();
            DrawCampDecision();
            DrawDoorPrompt();
            DrawRunLedger();
            DrawLocationBanner();
            DrawAwareness();
            DrawEnemyHealth();
            DrawObjective();
            DrawVitals();
            DrawStatusStrip();
        }

        DrawToasts();
        DrawContentErrors();

        if (_showHelp) DrawHelpOverlay();
        if (_dialogueOpen) DrawDialogue();
        if (_showJournal) DrawJournal();
        if (_showCharacter) DrawCharacterSheet();
        if (_showShop) DrawShop();
        if (_choosingDepth) DrawDepthChoice();
        if (_paused && _runSummary is null) DrawPause();
        if (_runSummary is { } summary) DrawRunSummary(summary);

        EndUi();
    }

    /// <summary>
    /// The whole game, in one panel.
    ///
    /// Both numbers are shown together on purpose: what is being staked, and what the next
    /// room pays. The escalating ratio between them is the pressure the loop runs on, and a
    /// player who has to work it out in their head is not feeling it.
    /// </summary>
    private void DrawCampDecision()
    {
        if (_run is not { AtDecision: true } decision) return;

        var run = decision.Run;
        var panel = new Rectangle(360, 386, 560, 232);
        DrawPanel(panel, new Color(6, 12, 19, 240), new Color(205, 157, 98));

        TextCentred("A CLEARED ROOM, AND A SHUT DOOR", panel.Center.X, panel.Y + 18f, 13,
            new Color(205, 157, 98));

        TextCentred($"{run.Pending}", panel.X + 148f, panel.Y + 52f, 44, new Color(151, 206, 210));

        // "Fifteen stones" is an abstraction; what it is worth is not. A pot the player cannot
        // price is a pot they cannot be afraid of losing, and a recorded run answered eight
        // doors in under a second each with forty-five stones on the table.
        TextCentred($"stones held  ·  {run.Pending * SoulCrystals.LesserBasePrice} gold",
            panel.X + 148f, panel.Y + 104f, 13, new Color(150, 162, 170));

        TextCentred(run.IsExhausted ? "—" : $"+{run.NextRoomPays}",
            panel.Right - 148f, panel.Y + 52f, 44, new Color(214, 186, 120));
        TextCentred(run.IsExhausted ? "the mine is spent" : "the next room pays",
            panel.Right - 148f, panel.Y + 104f, 13, new Color(150, 162, 170));

        TextCentred(run.IsExhausted
                ? $"{run.RoomsCleared} rooms cleared. There is nothing deeper."
                : $"{run.RoomsCleared} rooms cleared  ·  staking {run.RiskRatio:0.0} : 1",
            panel.Center.X, panel.Y + 128f, 15, new Color(206, 212, 218));

        if (!run.IsExhausted)
            TextCentred("Fall in there and you carry out nothing.",
                panel.Center.X, panel.Y + 150f, 13, new Color(196, 118, 96));

        var camp = new Rectangle(panel.X + 24, panel.Bottom - 62, 248, 40);
        DrawPanel(camp, new Color(17, 34, 28, 235), new Color(120, 178, 132));
        TextCentred($"C   Camp — bank {run.Pending}", camp.Center.X, camp.Y + 12f, 15,
            new Color(214, 240, 220));

        var press = new Rectangle(panel.Right - 272, panel.Bottom - 62, 248, 40);
        if (run.CanPressOn)
        {
            DrawPanel(press, new Color(40, 24, 20, 235), new Color(196, 118, 96));
            TextCentred("E   Open it", press.Center.X, press.Y + 12f, 15, new Color(244, 214, 200));
            return;
        }

        DrawPanel(press, new Color(18, 22, 26, 200), new Color(70, 78, 86));
        TextCentred("nothing deeper", press.Center.X, press.Y + 12f, 15, new Color(110, 120, 128));
    }

    /// <summary>First, second, third — for counting the dead.</summary>
    private static string Ordinal(int value) => (value % 100) switch
    {
        11 or 12 or 13 => $"{value}th",
        _ => (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        }
    };

    /// <summary>A quiet running total, so the pot is never a surprise at the door.</summary>
    private void DrawRunLedger()
    {
        if (_run is not { } active || !active.Run.IsActive || _runSummary is not null) return;

        var run = active.Run;
        var panel = new Rectangle(1016, 84, 240, 62);
        DrawPanel(panel, new Color(5, 11, 18, 214), new Color(65, 105, 119));

        Text("AT RISK", new Vector2(panel.X + 14, panel.Y + 10), 12, new Color(151, 206, 210));
        Text($"{run.Pending}", new Vector2(panel.Right - 44, panel.Y + 8), 18, Color.White);
        Text($"room {run.RoomsCleared}  ·  {run.Pending * SoulCrystals.LesserBasePrice} gold",
            new Vector2(panel.X + 14, panel.Y + 34), 12, new Color(150, 162, 170));
    }

    /// <summary>What the descent was worth, once it is over either way.</summary>
    /// <summary>
    /// The pause screen.
    ///
    /// It exists because Escape used to go straight to the main menu and take the descent with
    /// it. What is at stake is spelled out here rather than assumed: a player deciding whether
    /// to stop should be able to see what stopping costs.
    /// </summary>
    private void DrawPause()
    {
        DrawPanel(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 6, 10, 214),
            new Color(3, 6, 10, 0));

        var inRun = _run is { Run.IsActive: true };
        var panel = new Rectangle(400, 196, 480, inRun ? 332 : 268);
        DrawPanel(panel, new Color(6, 12, 19, 246), new Color(151, 206, 210));

        TextCentred("PAUSED", panel.Center.X, panel.Y + 26f, 24, new Color(214, 226, 226));

        var top = panel.Y + 78f;
        if (inRun && _run is not null)
        {
            var run = _run.Run;
            TextCentred($"{run.RoomsCleared} rooms cleared  ·  {run.Pending} stones at risk",
                panel.Center.X, panel.Y + 62f, 14, new Color(151, 206, 210));
            TextCentred("Setting it aside keeps all of it. Giving up keeps none.",
                panel.Center.X, panel.Y + 84f, 13, new Color(150, 162, 170));
            top = panel.Y + 118f;
        }

        var items = PauseItems;
        for (var index = 0; index < items.Length; index++)
        {
            var bounds = new Rectangle(panel.X + 40, (int)top + index * 46, panel.Width - 80, 38);
            var selected = index == _pauseSelection;
            var giveUp = items[index].StartsWith("Give up", StringComparison.Ordinal);

            DrawPanel(bounds,
                selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected
                    ? giveUp ? new Color(214, 118, 96) : new Color(224, 181, 88)
                    : new Color(54, 82, 91));

            TextCentred(items[index], bounds.Center.X, bounds.Y + 10f, 16,
                selected ? Color.White : new Color(192, 207, 205));
        }

        TextCentred("Up / Down select      Enter confirm      Esc resume",
            panel.Center.X, panel.Bottom - 30f, 13, new Color(140, 156, 164));
    }

    /// <summary>
    /// The price of every depth, and what each is worth, at the moment of committing.
    ///
    /// Both halves on screen together on purpose. Stones were an abstraction for five
    /// playtests because nothing ever asked for them; a door with a number on it is the first
    /// time carrying forty-five out of a mine has meant anything at all.
    /// </summary>
    private void DrawDepthChoice()
    {
        if (_session is null) return;

        var stones = _session.Player.Inventory.CountOf(SoulCrystals.LesserId);
        var panel = new Rectangle(320, 148, 640, 424);

        DrawPanel(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 6, 10, 214),
            new Color(3, 6, 10, 0));
        DrawPanel(panel, new Color(6, 12, 19, 246), new Color(205, 157, 98));

        TextCentred("HOW DEEP", panel.Center.X, panel.Y + 24f, 24, new Color(214, 226, 226));
        TextCentred($"{stones} jiva stones in hand", panel.Center.X, panel.Y + 58f, 14,
            new Color(151, 206, 210));

        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        {
            var cost = MineEntry.CostOf(tier);
            var affordable = stones >= cost;
            var selected = tier == _depthSelection;
            var row = new Rectangle(panel.X + 28, panel.Y + 88 + (tier - 1) * 56,
                panel.Width - 56, 48);

            DrawPanel(row,
                selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected ? new Color(224, 181, 88) : new Color(54, 82, 91));

            var ink = !affordable ? new Color(112, 100, 96)
                : selected ? Color.White
                : new Color(192, 207, 205);

            Text($"Tier {tier}", new Vector2(row.X + 18, row.Y + 6), 18, ink);
            TextRight(cost == 0 ? "free" : $"{cost} stones", row.Right - 18, row.Y + 8, 16,
                affordable ? new Color(214, 186, 120) : new Color(196, 118, 96));
            TextFit(MineEntry.DescriptionOf(tier), new Vector2(row.X + 18, row.Y + 28),
                row.Width - 150, 12, new Color(150, 162, 170));
        }

        var breakEven = MineEntry.RoomsToBreakEven(_depthSelection);
        TextCentred(breakEven == 0
                ? "Pays one stone a room. Nothing to make back."
                : $"Pays {_depthSelection} a room, rising. {breakEven} rooms before the door pays for itself.",
            panel.Center.X, panel.Bottom - 54f, 14, new Color(206, 212, 218));

        TextCentred("Up / Down choose      Enter descend      Esc step back",
            panel.Center.X, panel.Bottom - 28f, 13, new Color(140, 156, 164));
    }

    private void DrawRunSummary(RunResult summary)
    {
        DrawPanel(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 6, 10, 226),
            new Color(3, 6, 10, 0));

        var panel = new Rectangle(360, 220, 560, 280);
        var accent = summary.Survived ? new Color(120, 178, 132) : new Color(196, 96, 88);
        DrawPanel(panel, new Color(6, 12, 19, 246), accent);

        TextCentred(summary.Survived ? "YOU WALKED OUT" : "YOU DID NOT",
            panel.Center.X, panel.Y + 30f, 26, accent);

        TextCentred($"{summary.RoomsCleared} rooms cleared at tier {summary.Tier}",
            panel.Center.X, panel.Y + 78f, 15, new Color(206, 212, 218));

        if (summary.Survived)
        {
            TextCentred($"+{summary.StonesCarriedOut}", panel.Center.X, panel.Y + 124f, 52,
                new Color(151, 206, 210));
            TextCentred(
                $"jiva stones banked  ·  {summary.StonesCarriedOut * SoulCrystals.LesserBasePrice} gold",
                panel.Center.X, panel.Y + 186f, 14, new Color(150, 162, 170));
        }
        else
        {
            TextCentred($"−{summary.StonesLost}", panel.Center.X, panel.Y + 124f, 52,
                new Color(196, 96, 88));
            TextCentred(summary.StonesLost > 0
                    ? $"left where you fell  ·  {summary.StonesLost * SoulCrystals.LesserBasePrice} gold"
                    : "you had nothing to lose yet",
                panel.Center.X, panel.Y + 186f, 14, new Color(150, 162, 170));
        }

        // Who takes the lamp, and where the last one is lying. A death that only reports a
        // number is a reset; a death with a name and a place to go back to is a reason.
        if (!summary.Survived && _session is not null)
        {
            var legacy = _session.Player.Legacy;
            var successor = legacy.CurrentName;

            TextCentred($"{successor} takes the lamp  ·  Deepankar the {Ordinal(legacy.Generation + 1)}",
                panel.Center.X, panel.Bottom - 88f, 15, new Color(214, 200, 170));

            if (legacy.Fallen is { } cache)
                TextCentred(
                    $"{cache.Name} lies in room {cache.RoomIndex} with {cache.Stones} stones. Go and fetch them.",
                    panel.Center.X, panel.Bottom - 64f, 13, new Color(151, 206, 210));
            else if (_succession is { } cost && cost.ItemsLost > 0)
                TextCentred($"{cost.ItemsLost} items went into the ground.",
                    panel.Center.X, panel.Bottom - 64f, 13, new Color(150, 162, 170));
        }

        TextCentred("Enter  ·  back to the surface", panel.Center.X, panel.Bottom - 30f, 14,
            new Color(150, 162, 170));
    }

    private void DrawDoorPrompt()
    {
        if (_session is null) return;

        var player = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);

        if (OnTheSurface)
        {
            var fixture = Surface.FixtureAt(player);
            if (fixture == SurfaceFixture.None) return;

            var stones = _session.Player.Inventory.CountOf(SoulCrystals.LesserId);
            var line = fixture switch
            {
                SurfaceFixture.Shaft => $"E  Open a shaft   ({stones} stones)",
                SurfaceFixture.Trader => "E  Trade",
                _ => "E  Read the carving"
            };

            DrawPanel(SinglePromptBounds(), new Color(5, 11, 18, 225), new Color(205, 157, 98));
            Text(line, new Vector2(404, 608), 15, Color.White);
            return;
        }

        var actor = _dialogue?.FindActor(player, _cameraYaw);
        if (actor is not null)
        {
            var talk = TalkPromptBounds();
            var secondary = SecondaryPromptBounds();
            DrawPanel(talk, new Color(5, 11, 18, 225), new Color(151, 206, 210));
            TextFit($"Click / E  Talk to {actor.DisplayName}",
                new Vector2(talk.X + 16, talk.Y + 12), talk.Width - 32, 14, Color.White);

            if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                && _shop is not null)
            {
                DrawPanel(secondary, new Color(5, 11, 18, 225), new Color(205, 157, 98));
                Text("B  Shop", new Vector2(secondary.X + 16, secondary.Y + 12), 14, Color.White);
            }

            // A pocket worth picking was previously only advertised on guards, so the one
            // pocket in the slice that matters — the trader carrying the watchpost key —
            // had no prompt at all and testers never found it.
            if (HasPickablePocket(actor))
            {
                var pocket = PickpocketPromptBounds();
                DrawPanel(pocket, new Color(5, 11, 18, 225), new Color(190, 148, 196));
                Text("P  Pick pocket", new Vector2(pocket.X + 16, pocket.Y + 12), 14, Color.White);
            }

            return;
        }

        var pickup = FindPickup(player, _cameraYaw);
        if (pickup is not null)
        {
            DrawPanel(SinglePromptBounds(), new Color(5, 11, 18, 225),
                new Color(151, 206, 210));
            TextFit($"Click / E  Take {pickup.Name} x{pickup.Count}",
                new Vector2(404, 608), 472f, 15, Color.White);
            return;
        }

        // The camp decision is a bigger question about the same door; two prompts on one
        // doorway would just be noise.
        if (_world is null || _run is { AtDecision: true }) return;
        var door = _world.FindDoor(player, _cameraYaw);
        if (door is null) return;

        var hasKey = !string.IsNullOrEmpty(door.Definition.KeyItemId)
            && _session.Player.Inventory.Has(door.Definition.KeyItemId);

        if (_run is { BarsTheWay: true })
        {
            DrawPanel(SinglePromptBounds(), new Color(5, 11, 18, 225), new Color(150, 120, 110));
            Text("Barred  |  clear this room first", new Vector2(404, 608), 15,
                new Color(224, 196, 186));
            return;
        }

        var text = !door.Lock.IsLocked ? "Click / E  Open door"
            : hasKey ? "Click / E  Unlock with your key"
            : $"Locked  |  a key, or Security {door.Definition.Difficulty:0}";
        DrawPanel(SinglePromptBounds(), new Color(5, 11, 18, 225), new Color(205, 157, 98));
        Text(text, new Vector2(404, 608), 15, Color.White);
    }

    private void DrawSpeakingActors()
    {
        if (_dialogue is null || _dialogue.Actors.Count == 0) return;

        _billboards.Begin(_view, _projection);
        var sorted = new List<SpeakingActor>(_dialogue.Actors);
        sorted.Sort((a, b) => DistanceToCamera(b).CompareTo(DistanceToCamera(a)));

        foreach (var actor in sorted)
        {
            var texture = CharacterSprites.Get(GraphicsDevice, $"dialogue.{actor.ActorId}",
                PaletteFor(actor.Palette));
            var feet = new Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
            _billboards.Draw(texture, feet, actor.Height, _cameraYaw, Color.White);
        }

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private void DrawWatchers()
    {
        if (_watchers is null || _watchers.Watchers.Count == 0) return;

        _billboards.Begin(_view, _projection);
        foreach (var watcher in _watchers.Watchers)
        {
            var texture = CharacterSprites.Get(GraphicsDevice, $"watcher.{watcher.Definition.Id}",
                CharacterPalette.Guard);
            var feet = new Vector3(watcher.Position.X, watcher.Position.Y, watcher.Position.Z);
            var tint = watcher.LastSeen ? new Color(255, 168, 148) : Color.White;
            _billboards.Draw(texture, feet, 1.85f, _cameraYaw, tint);
        }

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private float DistanceToCamera(SpeakingActor actor) =>
        Vector3.DistanceSquared(_cameraPosition,
            new Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z));

    private static CharacterPalette PaletteFor(string? palette) => palette?.ToLowerInvariant() switch
    {
        "guard" => CharacterPalette.Guard,
        "merchant" => CharacterPalette.Merchant,
        "bandit" => CharacterPalette.Bandit,
        "wolf" => CharacterPalette.Wolf,
        _ => CharacterPalette.Citizen
    };

    private void DrawDialogue()
    {
        if (_conversationActor is null) return;

        var topics = _conversationActor.AvailableTopics();
        var panel = DialoguePanelBounds;
        DrawPanel(panel, new Color(5, 11, 18, 248), new Color(151, 206, 210));

        Text(_conversationActor.DisplayName, new Vector2(panel.X + 24, panel.Y + 20), 26, Color.White);
        TextWrapped(_dialogueResponse, new Vector2(panel.X + 24, panel.Y + 62),
            panel.Width - 48, 18, new Color(216, 228, 223), maxLines: 4);

        if (topics.Count == 0)
        {
            Text("Nothing you know to ask reaches them.",
                new Vector2(panel.X + 24, DialogueTopicBounds(0).Y + 6), 17,
                new Color(174, 188, 186));
        }
        else
        {
            for (var index = 0; index < topics.Count && index < DialogueRows; index++)
            {
                var selected = index == _dialogueSelection;
                var row = DialogueTopicBounds(index);
                Fill(row, selected ? new Color(74, 67, 43, 240) : new Color(17, 27, 35, 190));
                Text($"{index + 1}. {topics[index]}", new Vector2(row.X + 12, row.Y + 6), 17,
                    selected ? new Color(245, 209, 124) : new Color(206, 219, 217));
            }

            if (topics.Count > DialogueRows)
                Text($"+{topics.Count - DialogueRows} more",
                    new Vector2(panel.X + 24, DialogueTopicBounds(DialogueRows).Y + 4), 14,
                    new Color(142, 157, 157));
        }

        Text("Enter ask      Esc close", new Vector2(panel.X + 24, panel.Bottom - 30), 15,
            new Color(170, 197, 200));
    }

    private void DrawJournal()
    {
        if (_session is null) return;

        var panel = new Rectangle(200, 82, 880, 556);
        DrawPanel(panel, new Color(5, 11, 18, 246), new Color(182, 137, 71));
        Text("JOURNAL", new Vector2(panel.X + 30, panel.Y + 24), 13,
            new Color(214, 183, 108));
        Text("Current work", new Vector2(panel.X + 30, panel.Y + 56), 28, Color.White);

        var quests = _session.Player.Quests.Quests;
        if (quests.Count == 0)
        {
            Text("No quests have been recorded.", new Vector2(panel.X + 30, panel.Y + 112), 17,
                new Color(174, 188, 186));
        }
        else
        {
            var y = panel.Y + 108;
            foreach (var quest in quests)
            {
                var colour = quest.IsCompleted ? new Color(143, 180, 142)
                    : quest.IsActive ? Color.White : new Color(142, 157, 157);
                TextFit(quest.Title, new Vector2(panel.X + 30, y), 440f, 19, colour);
                var state = quest.IsCompleted ? "COMPLETE"
                    : quest.IsActive ? quest.StageText : "Not accepted";
                TextFit(state, new Vector2(panel.X + 54, y + 30), 760f, 15,
                    quest.IsCompleted ? new Color(143, 180, 142) : new Color(203, 216, 214));
                y += 76;
                if (y > panel.Bottom - 70) break;
            }
        }

        Text("J / Esc close", new Vector2(panel.X + 30, panel.Bottom - 34), 13,
            new Color(163, 191, 194));
    }

    private void DrawCharacterSheet()
    {
        if (_session is null) return;

        var player = _session.Player;
        var vitals = player.Vitals;
        var panel = new Rectangle(90, 70, 1100, 580);
        DrawPanel(panel, new Color(5, 11, 18, 248), new Color(117, 153, 166));
        Text("CHARACTER", new Vector2(panel.X + 30, panel.Y + 22), 13,
            new Color(214, 183, 108));

        var name = string.IsNullOrWhiteSpace(player.Story.State.Profile.Name)
            ? "Northwatch Wanderer"
            : player.Story.State.Profile.Name;
        TextFit(name, new Vector2(panel.X + 30, panel.Y + 52), 440f, 28, Color.White);
        TextRight($"{vitals.Gold} gold", panel.Right - 30, panel.Y + 60, 17,
            new Color(228, 197, 122));

        var leftX = panel.X + 30;
        var inventoryX = panel.X + 390;
        var skillsX = panel.X + 750;
        var top = panel.Y + 112;

        Text("VITALS & EQUIPMENT", new Vector2(leftX, top), 13, new Color(151, 206, 210));
        Text($"Level {vitals.Level}   XP {vitals.Xp} / {vitals.XpToLevel}",
            new Vector2(leftX, top + 34), 17, Color.White);
        Text($"Health     {vitals.Health:0} / {vitals.MaxHealth:0}",
            new Vector2(leftX, top + 70), 16, new Color(224, 116, 105));
        Text($"Prana       {vitals.Prana:0} / {vitals.MaxPrana:0}",
            new Vector2(leftX, top + 100), 16, new Color(112, 174, 225));
        Text($"Stamina   {vitals.Stamina:0} / {vitals.MaxStamina:0}",
            new Vector2(leftX, top + 130), 16, new Color(117, 194, 137));
        TextFit($"Weapon: {player.Equipment.Weapon.DisplayName}",
            new Vector2(leftX, top + 182), 320f, 16, new Color(203, 216, 214));
        TextFit($"Armour: {player.Equipment.Armour?.DisplayName ?? "None"}",
            new Vector2(leftX, top + 212), 320f, 16, new Color(203, 216, 214));
        Text($"Armour value: {player.Equipment.ArmourValue:0}",
            new Vector2(leftX, top + 242), 15, new Color(174, 188, 186));
        Text($"Jiva stones drawn: {vitals.Channeled}",
            new Vector2(leftX, top + 286), 15, new Color(174, 188, 186));

        Text("INVENTORY", new Vector2(inventoryX, top), 13, new Color(151, 206, 210));
        var items = player.Inventory.Items;

        if (items.Count == 0)
        {
            Text("Empty", new Vector2(inventoryX, top + 34), 16, new Color(142, 157, 157));
        }
        else
        {
            var selection = Math.Clamp(_inventorySelection, 0, items.Count - 1);

            for (var index = 0; index < items.Count && index < InventoryRows; index++)
            {
                var item = items[index];
                var row = InventoryRowBounds(index);
                var selected = index == selection;
                var equipped = string.Equals(item.Id, player.Equipment.WeaponId, StringComparison.Ordinal)
                    || string.Equals(item.Id, player.Equipment.ArmourId, StringComparison.Ordinal);

                if (selected)
                    DrawPanel(row, new Color(74, 67, 43, 235), new Color(224, 181, 88));

                TextFit(item.Name, new Vector2(row.X + 12, row.Y + 7), 214f, 16,
                    selected ? Color.White : new Color(203, 216, 214));

                if (equipped)
                    Text("worn", new Vector2(row.X + 236, row.Y + 8), 13, new Color(150, 200, 158));

                TextRight($"x{item.Count}", row.Right - 12, row.Y + 7, 15,
                    new Color(228, 197, 122));
            }

            if (items.Count > InventoryRows)
                Text($"+{items.Count - InventoryRows} more",
                    new Vector2(inventoryX, InventoryRowBounds(InventoryRows).Y + 6), 13,
                    new Color(142, 157, 157));

            // What the selected item is and what pressing Enter will do to it. Without this
            // the list is a set of names with no consequences attached.
            var chosen = items[selection];
            var detail = new Rectangle(480, 214 + InventoryRows * 34 + 16, 356, 74);
            DrawPanel(detail, new Color(8, 16, 24, 232), new Color(72, 104, 118));
            TextFit(ItemUse.Describe(chosen.Id, chosen.Kind),
                new Vector2(detail.X + 12, detail.Y + 12), 330f, 14, new Color(196, 212, 210));

            var verb = ItemUse.DescribeAction(chosen.Id, chosen.Kind);
            TextFit(verb == "—" ? "Nothing happens when you use this." : $"Enter or click to {verb.ToLowerInvariant()}",
                new Vector2(detail.X + 12, detail.Y + 44), 330f, 14,
                verb == "—" ? new Color(142, 157, 157) : new Color(232, 194, 116));
        }

        Text("SKILLS", new Vector2(skillsX, top), 13, new Color(151, 206, 210));
        var skillY = top + 34;
        foreach (var skillId in Skills.All)
        {
            TextFit(Skills.Label(skillId), new Vector2(skillsX, skillY), 205f, 16,
                new Color(203, 216, 214));
            TextRight(player.Skills.LevelOf(skillId).ToString("0.0"), panel.Right - 34,
                skillY, 16, Color.White);
            skillY += 37;
        }

        Text("Up / Down or hover to choose      Enter to use      I / K / Esc close",
            new Vector2(panel.X + 30, panel.Bottom - 34), 13, new Color(163, 191, 194));
    }

    private void DrawShop()
    {
        if (_session is null || _shop is null) return;

        var panel = new Rectangle(250, 112, 780, 468);
        DrawPanel(panel, new Color(5, 11, 18, 248), new Color(205, 157, 98));
        Text("SHOP", new Vector2(panel.X + 30, panel.Y + 26), 13,
            new Color(214, 183, 108));
        TextFit(_shop.Definition.DisplayName, new Vector2(panel.X + 30, panel.Y + 54), 520f, 27,
            Color.White);
        TextRight($"{_session.Player.Vitals.Gold} gold", panel.Right - 30, panel.Y + 62, 17,
            new Color(228, 197, 122));

        var items = _shop.Definition.Items;
        if (items.Count == 0)
            Text("No stock.", new Vector2(panel.X + 30, panel.Y + 126), 17, new Color(174, 188, 186));
        else
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var selected = index == _shopSelection;
                var row = ShopItemBounds(index);
                DrawPanel(row, selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                    selected ? new Color(224, 181, 88) : new Color(54, 82, 91));
                var state = _shop.IsSoldOut(item.Id) ? "SOLD OUT" : $"{item.Price} gold";
                TextFit($"{index + 1}. {item.Name} x{item.Count}", new Vector2(row.X + 14, row.Y + 10),
                    500f, 16, selected ? Color.White : new Color(203, 216, 214));
                TextRight(state, row.Right - 14, row.Y + 10, 15,
                    _shop.IsSoldOut(item.Id) ? new Color(142, 157, 157) : new Color(228, 197, 122));
            }
        }

        Text("Click an item to buy   Up / Down select   B / Esc close", new Vector2(panel.X + 30, panel.Bottom - 34),
            13, new Color(163, 191, 194));
    }

    /// <summary>
    /// The enemies, as camera-facing sprites.
    ///
    /// Drawn far to near so the alpha-tested cutouts never punch a hole in something behind
    /// them that has not been drawn yet.
    /// </summary>
    private void DrawEnemies()
    {
        if (_encounter is null || _encounter.Enemies.Count == 0) return;

        var texture = CharacterSprites.Get(GraphicsDevice, "bandit", CharacterPalette.Bandit);
        _billboards.Begin(_view, _projection);

        var sorted = new List<Enemy>(_encounter.Enemies);
        sorted.Sort((a, b) => DistanceToCamera(b).CompareTo(DistanceToCamera(a)));

        foreach (var enemy in sorted)
        {
            var feet = _encounter.DrawPositionOf(enemy);
            var tint = _encounter.TintOf(enemy);

            // A chilled bandit is visibly cold, so frost reads as more than a slower walk.
            if (enemy.IsChilled) tint = new Color(tint.R / 2 + 90, tint.G / 2 + 110, tint.B);

            _billboards.Draw(texture, feet, _encounter.DrawHeightOf(enemy), _cameraYaw, tint);
        }

        // The billboard pass leaves its own render state behind; the UI expects the default.
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private float DistanceToCamera(Enemy enemy) =>
        Vector3.DistanceSquared(_cameraPosition,
            new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z));

    /// <summary>
    /// The weapon in the player's hand.
    ///
    /// Drawn in the UI pass rather than the 3D one, which is how Daggerfall did it: the
    /// weapon is a sprite at the edge of the screen, not a modelled object in the world, so
    /// it never clips through a wall and never needs a rig.
    /// </summary>
    private void DrawWeapon()
    {
        if (_session is null) return;

        var weapon = _session.Player.Combat.ActiveWeapon;

        if (_captureSwing is { } progress)
        {
            _weaponView.Swing(weapon);
            _weaponView.Update(progress, moving: false, guarding: false);
        }

        if (_captureCast is { } castProgress)
        {
            _weaponView.Cast();
            _weaponView.Update(castProgress, moving: false, guarding: false);
        }

        var texture = WeaponSprites.Get(GraphicsDevice, weapon);
        var pose = _weaponView.Pose();

        // The grip, not the corner: rotating about the hand is what makes it swing rather
        // than spin.
        var origin = new Vector2(texture.Width / 2f, texture.Height);

        _spriteBatch.Draw(
            texture,
            pose.Position,
            null,
            Color.White,
            pose.Rotation,
            origin,
            pose.Scale,
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Spells in flight.
    ///
    /// Drawn as camera-facing glows in the element's colour, so what is crossing the room is
    /// legible at a glance: orange is fire, pale blue is frost, gold is shock.
    /// </summary>
    /// <summary>Arrows in flight. Small, pale and fast, so they read as shafts, not spells.</summary>
    private static readonly Color ArrowColour = new(226, 214, 186);

    private void DrawBolts()
    {
        if (_encounter is null) return;

        var shots = _encounter.Shots.ToList();
        if (_encounter.Bolts.Count == 0 && shots.Count == 0) return;

        _billboards.Begin(_view, _projection);

        foreach (var shot in shots)
        {
            _billboards.Draw(BoltSprites.Get(GraphicsDevice, ArrowColour),
                shot.Position, 0.2f, _cameraYaw, Color.White);
        }

        foreach (var bolt in _encounter.Bolts)
        {
            var texture = BoltSprites.Get(GraphicsDevice, bolt.Colour);

            // A pulse so a bolt reads as burning energy rather than a thrown pebble.
            var pulse = 0.52f + MathF.Sin(bolt.Spin) * 0.06f;
            _billboards.Draw(texture, bolt.Position - Vector3.Up * (pulse * 0.5f), pulse,
                _cameraYaw, Color.White);
        }

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    /// <summary>
    /// The trailer's opening shot, in engine.
    ///
    /// A dark cave, one jiva stone glowing, and its light raking across a carved Stambha. Flat
    /// pigment with a single hard light source is a look; flat pigment with even lighting is a
    /// placeholder, which is the whole reason the scene is lit this way.
    /// </summary>
    private void DrawStambhaPreview()
    {
        GraphicsDevice.Clear(new Color(14, 13, 16));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        // The stone is the light. BasicEffect has no point lights, so it is faked with a warm
        // directional raking up from where the stone sits, the key light killed, and the
        // ambient dropped hard. Flat pigment with one hard source is a look; flat pigment
        // under even light is a placeholder.
        var ambient = _primitiveEffect.AmbientLightColor;
        var keyDirection = _primitiveEffect.DirectionalLight0.Direction;
        var keyColour = _primitiveEffect.DirectionalLight0.DiffuseColor;
        var fillEnabled = _primitiveEffect.DirectionalLight1.Enabled;

        _primitiveEffect.AmbientLightColor = new Vector3(0.17f, 0.15f, 0.17f);
        _primitiveEffect.DirectionalLight0.Direction =
            Vector3.Normalize(new Vector3(0.62f, 0.66f, -0.42f));
        _primitiveEffect.DirectionalLight0.DiffuseColor = new Vector3(1.45f, 0.94f, 0.48f);
        _primitiveEffect.DirectionalLight1.Enabled = true;
        _primitiveEffect.DirectionalLight1.Direction =
            Vector3.Normalize(new Vector3(-0.3f, -0.5f, -1f));
        _primitiveEffect.DirectionalLight1.DiffuseColor = new Vector3(0.10f, 0.13f, 0.20f);

        // Cave floor and back wall, as low-poly and as flat as everything else.
        DrawCube(new Vector3(0f, -1.4f, 0f), new Vector3(14f, 0.4f, 14f), new Color(58, 52, 46), 0f);
        DrawCube(new Vector3(0f, 2.2f, -5.0f), new Vector3(11f, 7f, 0.4f), new Color(38, 35, 34), 0f);
        DrawCube(new Vector3(-4.2f, 2.2f, -2.2f), new Vector3(0.4f, 7f, 6f), new Color(33, 31, 30), 0f);
        DrawCube(new Vector3(4.2f, 2.2f, -2.2f), new Vector3(0.4f, 7f, 6f), new Color(33, 31, 30), 0f);

        // The pillar: a tapering column, a dozen faces at most.
        DrawCube(new Vector3(0f, -1.15f, -3.4f), new Vector3(3.3f, 0.4f, 1.7f), new Color(78, 72, 64), 0f);
        DrawCube(new Vector3(0f, 0.8f, -3.4f), new Vector3(2.7f, 3.7f, 1.2f), new Color(104, 97, 87), 0f);
        DrawCube(new Vector3(0f, 2.9f, -3.4f), new Vector3(3.0f, 0.34f, 1.45f), new Color(78, 72, 64), 0f);

        // The carved face, standing just proud of the shaft.
        var carving = StambhaCarving.Get(GraphicsDevice, StambhaCarving.SurfaceVerse);
        if (carving is not null)
        {
            _billboards.Begin(_view, _projection);
            _billboards.Draw(carving, new Vector3(0f, 0.5f, -2.78f), 1.14f, 0f,
                new Color(255, 226, 190));
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        // The jiva stone, low and to the left, so the light rakes up across the cuts.
        // The stone emits rather than reflects, so it is drawn unlit — otherwise the light
        // source is the darkest object in its own shot.
        _primitiveEffect.EmissiveColor = new Vector3(0.95f, 0.62f, 0.30f);
        DrawCube(new Vector3(-1.35f, -1.02f, -2.1f), new Vector3(0.34f, 0.34f, 0.34f),
            new Color(255, 206, 132), 0.6f);

        _primitiveEffect.EmissiveColor = new Vector3(1f, 0.94f, 0.82f);
        DrawCube(new Vector3(-1.35f, -1.0f, -2.1f), new Vector3(0.2f, 0.2f, 0.2f),
            new Color(255, 250, 236), 0.6f);

        _primitiveEffect.EmissiveColor = Vector3.Zero;

        _primitiveEffect.AmbientLightColor = ambient;
        _primitiveEffect.DirectionalLight0.Direction = keyDirection;
        _primitiveEffect.DirectionalLight0.DiffuseColor = keyColour;
        _primitiveEffect.DirectionalLight1.Enabled = fillEnabled;

        BeginUi();
        if (carving is null)
            TextCentred("Devanagari font not loaded", LogicalWidth / 2f, 300f, 20,
                new Color(228, 128, 118));

        TextCentred("\"Covet not \u2014 for whose is wealth?\"",
            LogicalWidth / 2f, 596f, 20, new Color(214, 206, 190));
        TextCentred("Isha Upanishad 1", LogicalWidth / 2f, 630f, 14, new Color(140, 132, 120));
        EndUi();
    }

    /// <summary>A red vignette while the player is being hurt.</summary>
    private void DrawDamageFlash()
    {
        if (_encounter is null || _encounter.DamageFlash <= 0f) return;

        var strength = _encounter.DamageFlash / Encounter.DamageFlashSeconds;
        var tint = new Color(150, 24, 28) * (strength * 0.45f);
        const int band = 90;

        Fill(new Rectangle(0, 0, LogicalWidth, band), tint);
        Fill(new Rectangle(0, LogicalHeight - band, LogicalWidth, band), tint);
        Fill(new Rectangle(0, 0, band, LogicalHeight), tint);
        Fill(new Rectangle(LogicalWidth - band, 0, band, LogicalHeight), tint);
    }

    /// <summary>
    /// Skyrim-style stealth feedback: the eye replaces the ordinary crosshair and a quiet
    /// vignette makes the stance readable without taking the player's eyes off the world.
    /// The awareness panel still supplies the exact state and suspicion amount.
    /// </summary>
    private void DrawSneakOverlay()
    {
        if (_session?.Player.Detection.IsCrouching != true) return;

        var awareness = _session.Player.Detection.Awareness;
        var edge = awareness switch
        {
            AwarenessLevel.Alerted => new Color(108, 30, 28),
            AwarenessLevel.Suspicious => new Color(104, 76, 31),
            _ => new Color(13, 25, 32)
        };

        // Two bands approximate a soft vignette using the existing 1x1 UI texture. The
        // centre remains clear so sneaking never hides the thing being tested.
        Fill(new Rectangle(0, 0, LogicalWidth, 30), edge * 0.66f);
        Fill(new Rectangle(0, LogicalHeight - 30, LogicalWidth, 30), edge * 0.66f);
        Fill(new Rectangle(0, 0, 42, LogicalHeight), edge * 0.54f);
        Fill(new Rectangle(LogicalWidth - 42, 0, 42, LogicalHeight), edge * 0.54f);
        Fill(new Rectangle(0, 30, 16, LogicalHeight - 60), edge * 0.28f);
        Fill(new Rectangle(LogicalWidth - 16, 30, 16, LogicalHeight - 60), edge * 0.28f);
    }

    /// <summary>
    /// The health of whatever the crosshair is over. Shown only while something is actually
    /// in reach, so it doubles as the answer to "will this swing connect?".
    /// </summary>
    private void DrawEnemyHealth()
    {
        if (_encounter?.Focused is not { } enemy) return;

        var bar = new Rectangle(LogicalWidth / 2 - 150, 96, 300, 24);
        var fraction = MathHelper.Clamp(enemy.Health / enemy.MaxHealth, 0f, 1f);

        Fill(bar, new Color(16, 20, 24, 226));
        Fill(new Rectangle(bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height),
            new Color(178, 62, 66));
        Border(bar, new Color(0, 0, 0, 140));

        Text(enemy.DisplayName, new Vector2(bar.X + 10, bar.Y + 4), 14, Color.White);
        TextRight($"{enemy.Health:0} / {enemy.MaxHealth:0}", bar.Right - 10, bar.Y + 4, 14,
            Color.White);

        if (_encounter.IsLunging(enemy))
            TextCentred("striking", LogicalWidth / 2f, bar.Y - 26f, 15, new Color(236, 148, 122));

        var status = enemy.IsStaggered ? "staggered"
            : enemy.IsBurning ? "burning"
            : enemy.IsChilled ? "chilled"
            : string.Empty;

        if (status.Length > 0)
            TextCentred(status, LogicalWidth / 2f, bar.Bottom + 6, 13, new Color(232, 194, 116));
    }

    /// <summary>Where a swing or a spell will go. Small, and always centred.</summary>
    /// <summary>
    /// Our own mouse pointer.
    ///
    /// Drawn whenever the pointer is free rather than driving the camera, in every screen,
    /// so it never blinks in and out with the system cursor as menus open and close.
    /// </summary>
    private void DrawPointer()
    {
        if (_mouseLook) return;

        var pointer = LogicalMouse(Mouse.GetState());
        if (pointer.X < -8f || pointer.Y < -8f
            || pointer.X > LogicalWidth + 8f || pointer.Y > LogicalHeight + 8f) return;

        var x = (int)pointer.X;
        var y = (int)pointer.Y;

        // An arrow drawn as a stack of rows, with a dark skirt so it survives any background.
        for (var row = 0; row < 15; row++)
        {
            var width = row < 11 ? row + 1 : 15 - row + 2;
            if (width <= 0) continue;

            Fill(new Rectangle(x - 1, y + row - 1, width + 2, 3), new Color(12, 14, 18, 220));
        }

        for (var row = 0; row < 15; row++)
        {
            var width = row < 11 ? row + 1 : 15 - row + 2;
            if (width <= 0) continue;

            Fill(new Rectangle(x, y + row, width, 1), Color.White);
        }
    }

    private void DrawCrosshair()
    {
        const int cx = LogicalWidth / 2;
        const int cy = LogicalHeight / 2;

        if (_session?.Player.Detection.IsCrouching == true)
        {
            var colour = _session.Player.Detection.Awareness switch
            {
                AwarenessLevel.Alerted => new Color(238, 91, 78, 240),
                AwarenessLevel.Suspicious => new Color(239, 190, 91, 240),
                _ => new Color(220, 235, 226, 240)
            };
            DrawSneakEye(cx, cy, colour);
            TextCentred("SNEAK", cx, cy + 22, 11, colour);
            return;
        }

        var shadow = new Color(0, 0, 0, 165);
        var ink = new Color(244, 248, 246, 225);

        // Drawn twice: a dark pass one pixel out, then the light pass. A single-colour
        // crosshair disappears whenever the scenery happens to match it.
        foreach (var (colour, grow) in new[] { (shadow, 1), (ink, 0) })
        {
            Fill(new Rectangle(cx - 10 - grow, cy - grow, 7 + grow * 2, 2 + grow * 2), colour);
            Fill(new Rectangle(cx + 3 - grow, cy - grow, 7 + grow * 2, 2 + grow * 2), colour);
            Fill(new Rectangle(cx - grow, cy - 10 - grow, 2 + grow * 2, 7 + grow * 2), colour);
            Fill(new Rectangle(cx - grow, cy + 3 - grow, 2 + grow * 2, 7 + grow * 2), colour);
        }
    }

    /// <summary>Where you are. Top-centre, out of the way of everything you look at.</summary>
    /// <summary>
    /// A tick on the crosshair the instant a blow lands.
    ///
    /// Playtesters could not tell a hit from a miss. This is the smallest possible answer:
    /// four strokes that only appear when the domain says something was struck.
    /// </summary>
    private void DrawHitMarker()
    {
        if (_encounter is null) return;

        var strength = MathF.Max(_encounter.Feedback.HitMarker, _encounter.Feedback.KillMarker);
        if (strength <= 0f) return;

        const int cx = LogicalWidth / 2;
        const int cy = LogicalHeight / 2;

        // A kill reads gold; an ordinary hit reads white.
        var colour = (_encounter.Feedback.KillMarker > 0f
            ? new Color(255, 214, 122)
            : new Color(255, 252, 246)) * strength;

        // Spread outward as it fades, so the marker feels like an impact.
        var spread = (int)(6f + (1f - strength) * 7f);
        var length = 7;

        for (var i = 0; i < 4; i++)
        {
            var dx = i < 2 ? (i == 0 ? -1 : 1) : 0;
            var dz = i < 2 ? 0 : (i == 2 ? -1 : 1);

            for (var step = 0; step < length; step++)
            {
                var x = cx + dx * (spread + step);
                var y = cy + dz * (spread + step);
                Fill(new Rectangle(x - 1, y - 1, 2, 2), colour);
            }
        }
    }

    /// <summary>Damage and status, floating up from where it happened.</summary>
    private void DrawFloatingNumbers()
    {
        if (_encounter is null) return;

        foreach (var number in _encounter.Feedback.Numbers)
        {
            var fade = 1f - number.Age;
            var rise = number.Age * 46f;
            Vector2 position;

            if (CombatFeedback.IsSelfInflicted(number))
            {
                // Damage taken belongs on the player, not out in the world.
                position = new Vector2(LogicalWidth / 2f, LogicalHeight / 2f + 78f - rise);
            }
            else
            {
                if (!TryProjectToScreen(
                        new Vector3(number.Origin.X, number.Origin.Y + 1.9f, number.Origin.Z),
                        out position))
                    continue;

                position.X += number.Drift;
                position.Y -= rise;
            }

            // Numbers were already being drawn for sword hits, but at melee range they sat
            // pale over a pale sprite and went unnoticed. A dark shadow behind them is what
            // makes them read against anything.
            var size = CombatFeedback.IsSelfInflicted(number) ? 19 : 24;
            TextCentred(number.Text, position.X + 2f, position.Y + 2f, size,
                new Color(0, 0, 0, 190) * fade);
            TextCentred(number.Text, position.X, position.Y, size, number.Colour * fade);
        }
    }

    /// <summary>
    /// An arc pointing at whatever just hit the player.
    ///
    /// Being hit from behind was previously indistinguishable from being hit from in front:
    /// the screen reddened and that was all.
    /// </summary>
    private void DrawDamageDirections()
    {
        if (_encounter is null) return;

        const float centreX = LogicalWidth / 2f;
        const float centreY = LogicalHeight / 2f;
        const float radius = 132f;

        foreach (var direction in _encounter.Feedback.Directions)
        {
            var fade = direction.Duration <= 0f ? 0f : direction.Remaining / direction.Duration;
            var colour = new Color(232, 96, 88) * (fade * 0.9f);

            // Screen space: bearing zero is up, positive is clockwise.
            for (var offset = -0.34f; offset <= 0.34f; offset += 0.02f)
            {
                var angle = direction.Bearing + offset;
                var thickness = 5f - MathF.Abs(offset) * 8f;
                var x = centreX + MathF.Sin(angle) * radius;
                var y = centreY - MathF.Cos(angle) * radius;

                Fill(new Rectangle((int)x - 2, (int)y - 2, (int)MathF.Max(2f, thickness),
                    (int)MathF.Max(2f, thickness)), colour);
            }
        }
    }

    /// <summary>
    /// Small markers around the crosshair for living enemies nearby.
    ///
    /// Testers lost track of bandits the moment they left the view. The marker fades with
    /// distance so it reads as "something is over there", not as a wallhack.
    /// </summary>
    private void DrawThreatArrows()
    {
        if (_encounter is null) return;

        const float centreX = LogicalWidth / 2f;
        const float centreY = LogicalHeight / 2f;
        const float radius = 172f;

        foreach (var (enemy, bearing, distance) in _encounter.NearbyThreats())
        {
            // Anything comfortably in front is already visible; do not clutter the view.
            if (MathF.Abs(bearing) < 0.42f) continue;

            var nearness = MathHelper.Clamp(1f - distance / 26f, 0.25f, 1f);
            var colour = new Color(226, 168, 96) * (0.5f + nearness * 0.45f);

            var x = centreX + MathF.Sin(bearing) * radius;
            var y = centreY - MathF.Cos(bearing) * radius;

            // A small triangle pointing outward along the bearing.
            for (var step = 0; step < 8; step++)
            {
                var width = 8 - step;
                var px = x + MathF.Sin(bearing) * step;
                var py = y - MathF.Cos(bearing) * step;
                Fill(new Rectangle((int)px - width / 2, (int)py - 1, Math.Max(1, width), 2), colour);
            }
        }
    }

    /// <summary>
    /// The readied spell, its cost, and whether it can be paid for.
    ///
    /// Spells were bound to keys but never shown, so testers reported them as unimplemented.
    /// </summary>
    private void DrawSpellBar()
    {
        if (_session is null) return;

        var caster = _session.Player.Spells;
        var spell = SpellCatalog.Get(caster.SelectedSpellId);
        if (spell is null) return;

        var panel = new Rectangle(LogicalWidth / 2 - 150, LogicalHeight - 96, 300, 60);
        DrawPanel(panel, new Color(6, 13, 20, 214), new Color(74, 106, 132));

        var cost = caster.CostOf(spell);
        var affordable = _session.Player.Vitals.Prana >= cost
            || _session.Player.Inventory.Has(SoulCrystals.LesserId);

        Text("READIED", new Vector2(panel.X + 14, panel.Y + 9), 12, new Color(146, 174, 178));
        TextFit(spell.DisplayName, new Vector2(panel.X + 14, panel.Y + 28), 176f, 19,
            affordable ? Color.White : new Color(198, 132, 126));

        TextRight($"{cost:0} prana", panel.Right - 14, panel.Y + 9, 13,
            affordable ? new Color(150, 190, 232) : new Color(216, 128, 120));
        TextRight(affordable ? "Q to cast" : "no charge", panel.Right - 14, panel.Y + 30, 13,
            new Color(146, 174, 178));

        if (caster.LightActive)
            TextCentred($"Emberlight {caster.LightRemaining:0}s",
                LogicalWidth / 2f, panel.Y - 24f, 13, new Color(232, 194, 116));
    }

    /// <summary>
    /// Open a screen for a capture, once, on the first drawn frame. Content manifests load
    /// after Initialize, so a dialogue capture done any earlier has nobody to talk to.
    /// </summary>
    private void ApplyCaptureScreen()
    {
        if (_captureScreen is null || _captureApplied) return;
        _captureApplied = true;

        switch (_captureScreen?.ToLowerInvariant())
        {
            case "inventory" or "character": _showCharacter = true; break;
            case "journal": _showJournal = true; break;
            case "help": _showHelp = true; break;
            case "dialogue":
                _conversationActor = _dialogue?.Actors.FirstOrDefault();
                _dialogueOpen = _conversationActor is not null;
                _dialogueResponse =
                    "Northwatch is a border camp built around an older stone watchpost. Keep "
                    + "your eyes open on the road north, and do not travel it after dark "
                    + "unless you have a reason worth the risk.";
                break;
        }

    }

    /// <summary>
    /// What was just cast, and what it did.
    ///
    /// A brief tint in the element's colour makes the cast itself unmissable, and the line
    /// underneath names the spell and its result, so a cast that found nothing is clearly a
    /// miss rather than a spell that silently failed to fire.
    /// </summary>
    private void DrawCastBanner()
    {
        if (_encounter is null) return;

        var strength = _encounter.Feedback.CastBanner;
        if (strength <= 0f) return;

        // The tint fades faster than the words, so it reads as the moment of casting.
        var tintStrength = MathF.Max(0f, strength - 0.55f) / 0.45f;
        if (tintStrength > 0f)
        {
            var tint = _encounter.Feedback.CastTint * (tintStrength * 0.2f);
            const int band = 72;
            Fill(new Rectangle(0, 0, LogicalWidth, band), tint);
            Fill(new Rectangle(0, LogicalHeight - band, LogicalWidth, band), tint);
            Fill(new Rectangle(0, 0, band, LogicalHeight), tint);
            Fill(new Rectangle(LogicalWidth - band, 0, band, LogicalHeight), tint);
        }

        var fade = MathHelper.Clamp(strength * 1.6f, 0f, 1f);
        TextCentred(_encounter.Feedback.CastLine, LogicalWidth / 2f, LogicalHeight / 2f + 118f,
            19, _encounter.Feedback.CastColour * fade);
    }

    /// <summary>
    /// World position to logical UI pixels. False when the point is behind the camera, which
    /// would otherwise project to a mirrored position in front of it.
    /// </summary>
    private bool TryProjectToScreen(Vector3 world, out Vector2 screen)
    {
        screen = Vector2.Zero;

        var viewport = GraphicsDevice.Viewport;
        var projected = viewport.Project(world, _projection, _view, Matrix.Identity);
        if (projected.Z is < 0f or > 1f) return false;

        if (_uiScale <= 0f) return false;
        var offsetX = (viewport.Width - LogicalWidth * _uiScale) * 0.5f;
        var offsetY = (viewport.Height - LogicalHeight * _uiScale) * 0.5f;

        screen = new Vector2((projected.X - offsetX) / _uiScale, (projected.Y - offsetY) / _uiScale);
        return true;
    }

    /// <summary>
    /// Content that failed to load, said out loud.
    ///
    /// These were only ever shown on the Renderer Lab screen, so a damaged install dropped
    /// the player into an empty void with a working HUD and no explanation. Saves already
    /// follow the rule that a half-load must fail loudly; content now does too.
    /// </summary>
    private void DrawContentErrors()
    {
        if (_assetErrors.Count == 0) return;

        var panel = new Rectangle(300, 84, 680, 44 + _assetErrors.Count * 22);
        DrawPanel(panel, new Color(38, 12, 12, 238), new Color(198, 96, 88));
        Text("CONTENT FAILED TO LOAD", new Vector2(panel.X + 16, panel.Y + 12), 14,
            new Color(255, 196, 186));

        var y = panel.Y + 36f;
        foreach (var error in _assetErrors)
        {
            TextFit(error, new Vector2(panel.X + 16, y), 648f, 13, new Color(240, 208, 202));
            y += 22f;
        }
    }

    private void DrawLocationBanner()
    {
        TextCentred(LocationCaption(), LogicalWidth / 2f, 24f, 15, new Color(196, 214, 214));
    }

    private void DrawAwareness()
    {
        if (_session is null) return;

        var detection = _session.Player.Detection;
        var panel = new Rectangle(LogicalWidth - 264, 24, 240, 48);
        var colour = detection.Awareness switch
        {
            AwarenessLevel.Alerted => new Color(188, 65, 68),
            AwarenessLevel.Suspicious => new Color(205, 157, 98),
            _ => new Color(76, 101, 116)
        };
        DrawPanel(panel, new Color(6, 13, 20, 226), colour);
        Text("AWARENESS", new Vector2(panel.X + 14, panel.Y + 8), 12, Color.White);
        TextRight(detection.Awareness.ToString().ToUpperInvariant(), panel.Right - 14,
            panel.Y + 8, 12, colour == new Color(76, 101, 116) ? new Color(180, 196, 194) : colour);
        Fill(new Rectangle(panel.X + 14, panel.Y + 29, panel.Width - 28, 7), new Color(20, 27, 33));
        Fill(new Rectangle(panel.X + 14, panel.Y + 29,
            (int)((panel.Width - 28) * MathHelper.Clamp(detection.Suspicion, 0f, 1f)), 7), colour);
    }

    /// <summary>
    /// The objective, with a live compass bearing generated from where the player actually
    /// is — which is what replaces a marker.
    /// </summary>
    private void DrawObjective()
    {
        if (_session?.Player.Objective is not { HasObjective: true } objective) return;

        var panel = new Rectangle(24, 24, 360, 116);
        DrawPanel(panel, new Color(7, 15, 22, 226), new Color(182, 137, 71));

        Text("OBJECTIVE", new Vector2(panel.X + 18, panel.Y + 14), 13, new Color(239, 196, 111));
        TextFit(objective.Title!, new Vector2(panel.X + 18, panel.Y + 36), 324f, 20, Color.White);
        TextFit(objective.Directions ?? string.Empty, new Vector2(panel.X + 18, panel.Y + 64),
            324f, 15, new Color(206, 220, 212));

        var bearing = objective.BearingLine(_session.Position);
        if (bearing.Length > 0)
            TextFit(bearing, new Vector2(panel.X + 18, panel.Y + 88), 324f, 15,
                new Color(232, 194, 116));
    }

    /// <summary>
    /// The player's real numbers, bottom-left where a HUD belongs.
    ///
    /// Labels sit *on* their bar rather than underneath it: the previous layout put an 11 px
    /// caption in the gap between two bars, which read as belonging to the wrong one.
    /// </summary>
    private void DrawVitals()
    {
        if (_session is null) return;

        var vitals = _session.Player.Vitals;
        var panel = new Rectangle(24, LogicalHeight - 164, 344, 140);
        DrawPanel(panel, new Color(6, 13, 20, 232), new Color(78, 128, 148));

        var barX = panel.X + 18;
        var barWidth = panel.Width - 36;

        DrawVitalBar(new Rectangle(barX, panel.Y + 20, barWidth, 26), "HEALTH",
            vitals.Health, vitals.MaxHealth, new Color(198, 68, 74), _healthPulse);

        DrawVitalBar(new Rectangle(barX, panel.Y + 58, barWidth, 26), "PRANA",
            vitals.Prana, vitals.MaxPrana, new Color(74, 134, 216), _pranaPulse);

        DrawVitalBar(new Rectangle(barX, panel.Y + 96, barWidth, 26), "STAMINA",
            vitals.Stamina, vitals.MaxStamina, new Color(98, 172, 106));
    }

    /// <summary>One labelled bar. The label and the value live inside it, vertically centred.</summary>
    private void DrawVitalBar(Rectangle bounds, string label, float value, float max, Color colour,
        float pulse = 0f)
    {
        var fraction = max <= 0f ? 0f : MathHelper.Clamp(value / max, 0f, 1f);

        Fill(bounds, new Color(20, 27, 33));
        Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * fraction), bounds.Height), colour);

        // A bar that just changed says so. Using an item used to alter a number in the corner
        // of the screen and nothing else, so it was easy to believe nothing had happened.
        if (pulse > 0f)
        {
            Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * fraction), bounds.Height),
                new Color(255, 255, 255) * (pulse * 0.42f));
            Border(bounds, new Color(226, 240, 255) * pulse);
            Border(new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4),
                new Color(226, 240, 255) * (pulse * 0.7f));
        }
        else
        {
            Border(bounds, new Color(0, 0, 0, 110));
        }

        // A dark scrim behind the text keeps it legible over both the filled and empty halves.
        Text(label, new Vector2(bounds.X + 10, bounds.Y + 5), 14, Color.White);

        var readout = pulse > 0f
            ? Color.Lerp(Color.White, new Color(198, 232, 255), pulse)
            : Color.White;
        TextRight($"{value:0} / {max:0}", bounds.Right - 10, bounds.Y + 5,
            pulse > 0f ? 16 : 14, readout);
    }

    /// <summary>Domain events, rendered above the vitals. Newest last, fading as they expire.</summary>
    private void DrawToasts()
    {
        if (_session is null || _session.Toasts.Count == 0) return;

        var y = LogicalHeight - 196f - _session.Toasts.Count * 28f;
        foreach (var toast in _session.Toasts)
        {
            var alpha = MathHelper.Clamp(toast.Remaining, 0f, 1f);
            TextCentred(toast.Message, LogicalWidth / 2f, y, 17, new Color(240, 230, 202) * alpha);
            y += 28f;
        }
    }

    /// <summary>Level, gold and the one key that opens the rest. Bottom-right, compact.</summary>
    private void DrawStatusStrip()
    {
        if (_session is null) return;

        var vitals = _session.Player.Vitals;
        var panel = new Rectangle(LogicalWidth - 264, LogicalHeight - 88, 240, 64);
        DrawPanel(panel, new Color(6, 13, 20, 226), new Color(76, 101, 116));

        Text($"LEVEL {vitals.Level}", new Vector2(panel.X + 18, panel.Y + 12), 16, Color.White);
        TextRight($"{vitals.Gold} gold", panel.Right - 18, panel.Y + 12, 16, new Color(228, 197, 122));
        var combat = _session.Player.Combat;
        Text(combat.ActiveWeapon.DisplayName, new Vector2(panel.X + 18, panel.Y + 38), 13,
            combat.IsBlocking ? new Color(232, 194, 116) : new Color(203, 216, 214));
        // Blank until the first averaging window closes: the counter used to show whatever
        // the opening, texture-generating window computed, so a build running at 700 fps
        // could report 4. A misleading diagnostic is worse than none.
        TextRight(_framesPerSecond > 0f ? $"{_framesPerSecond:0} fps" : "— fps",
            panel.Right - 18, panel.Y + 38, 13,
            _framesPerSecond is > 0f and < 50f
                ? new Color(228, 128, 118)
                : new Color(146, 174, 178));
    }

    /// <summary>
    /// The control list, on demand. It used to be a permanent full-width bar across the
    /// bottom of the screen, which is developer scaffolding rather than a HUD.
    /// </summary>
    private void DrawHelpOverlay()
    {
        Fill(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 7, 12, 200));

        var panel = new Rectangle(300, 30, 680, 660);
        DrawPanel(panel, new Color(7, 14, 21, 244), new Color(91, 146, 159));
        TextCentred("CONTROLS", panel.X + panel.Width / 2f, panel.Y + 26, 24, Color.White);

        (string Key, string Action)[] rows =
        {
            ("W A S D", "move"),
            ("Mouse", "look"),
            ("Left click", "attack / talk / shop"),
            ("Right click", "guard — one-handed only"),
            ("Q", "cast the readied spell"),
            ("I", "character and inventory — Enter to use an item"),
            ("E", "talk, open, take"),
            ("P", "pick a pocket"),
            ("B", "trade with a merchant"),
            ("4 5 6 7 8", "flame, rime, arc, mend, emberlight"),
            ("Arrow keys", "look (keyboard)"),
            ("Shift", "sprint — spends stamina"),
            ("Space", "jump"),
            ("Ctrl", "toggle crouch — reduces visibility"),
            ("E", "talk / open / interact"),
            ("P", "pickpocket a facing NPC"),
            ("J", "open the journal"),
            ("I / K", "open inventory, equipment and skills"),
            ("F5 / F9", "save / load"),
            ("F1", "close this"),
            ("F11", "windowed / fullscreen"),
            ("Esc", "close what is open, then back to the menu"),
            ("M", "back to the menu")
        };

        var y = panel.Y + 72f;
        foreach (var (key, action) in rows)
        {
            Text(key, new Vector2(panel.X + 44, y), 17, new Color(232, 194, 116));
            Text(action, new Vector2(panel.X + 250, y), 17, new Color(214, 226, 222));
            y += 31f;
        }

        // Anyone being recorded should be told so without having to be told so.
        TextCentred($"This session is being recorded to {PlayRecorder.Directory}",
            panel.X + panel.Width / 2f, panel.Bottom - 42f, 13, new Color(140, 156, 164));
    }

    private void DrawGallery()
    {
        DrawWorldBase(new Color(56, 82, 100), new Color(52, 64, 70));

        DrawModel("tree", new Vector3(-5f, 0f, -2f), 1.8f, 0.15f);
        DrawModel("rock", new Vector3(-2.3f, 0f, -1.8f), 1.4f, 0.4f);
        DrawModel("tent", new Vector3(1.2f, 0f, -2.2f), 1.7f, -0.3f);
        DrawModel("bridge", new Vector3(4.3f, 0f, -1.5f), 1.6f, 0.5f);
        DrawModel("campfire", new Vector3(-3.4f, 0f, 2.1f), 1.6f, 0f);
        DrawModel("bush", new Vector3(0.2f, 0f, 2.2f), 1.6f, 0.3f);
        DrawModel("cheeseBox", new Vector3(3.3f, 0.55f, 2.0f), 1.2f, -0.2f);

        BeginUi();
        DrawPanel(new Rectangle(24, 86, 374, 132), new Color(5, 10, 16, 220), new Color(78, 155, 185));
        Text("Imported assets", new Vector2(44, 104), 22, Color.White);
        Text("7 Kenney FBX models + 1 Poly Haven textured FBX", new Vector2(44, 139), 15, new Color(184, 214, 225));
        Text("WASD move | Arrow keys look | 1/2/3 switch tests", new Vector2(44, 171), 14, new Color(130, 169, 185));
        if (_assetErrors.Count > 0)
        {
            Text("Load issues: " + string.Join(", ", _assetErrors), new Vector2(44, 196), 12, Color.OrangeRed);
        }
        EndUi();
    }

    private void DrawPhotoScene(bool drawStudyOverlay)
    {
        DrawWorldBase(new Color(96, 121, 136), new Color(58, 70, 74));

        DrawCube(new Vector3(0f, -0.35f, 0f), new Vector3(24f, 0.4f, 24f), new Color(104, 112, 96), 0f);
        DrawCube(new Vector3(0f, 3.5f, -9f), new Vector3(22f, 7f, 0.3f), new Color(96, 110, 116), 0f);
        DrawCube(new Vector3(-9f, 2.8f, 0f), new Vector3(0.3f, 5.6f, 18f), new Color(82, 98, 96), 0f);

        DrawModel("ground", new Vector3(-1f, 0f, -1f), 2.8f, 0f);
        DrawModel("tree", new Vector3(-5.8f, 0f, -2.4f), 2.1f, 0.2f);
        DrawModel("tree", new Vector3(5.7f, 0f, -4.8f), 2.3f, -0.15f);
        DrawModel("rock", new Vector3(-4.2f, 0f, 2.7f), 1.4f, 0.1f);
        DrawModel("bush", new Vector3(4.4f, 0f, 1.8f), 1.4f, 0.4f);
        DrawModel("tent", new Vector3(1.5f, 0f, -3.9f), 1.7f, -0.2f);
        DrawModel("campfire", new Vector3(2.4f, 0f, 1.2f), 1.4f, 0f);
        DrawModel("cheeseBox", new Vector3(-0.2f, 0.45f, 2.3f), 1.3f, 0.3f);

        if (drawStudyOverlay)
        {
            BeginUi();
            DrawPanel(new Rectangle(24, 86, 438, 134), new Color(6, 12, 18, 205), new Color(205, 157, 98));
            Text("Photo-realism feasibility study", new Vector2(44, 104), 22, Color.White);
            Text("Current pass: textured prop + lit geometry", new Vector2(44, 139), 15, new Color(232, 205, 164));
            Text("This is a renderer test, not final PBR quality.", new Vector2(44, 171), 14, new Color(173, 188, 191));
            Text("WASD move | Arrow keys look | 3 opens UI stress", new Vector2(44, 196), 14, new Color(140, 165, 171));
            EndUi();
        }
    }

    private void DrawAuthoredWorld()
    {
        if (_world is null)
        {
            GraphicsDevice.Clear(new Color(40, 58, 68));
            return;
        }

        GraphicsDevice.Clear(new Color(96, 121, 136));
        foreach (var geometry in _world.Manifest.Geometry ?? new List<WorldGeometry>())
        {
            if (!geometry.Visible) continue;
            DrawWorldBox(geometry.Min, geometry.Max, ToXnaColor(geometry.Color));
        }

        foreach (var door in _world.Doors)
        {
            if (door.Lock.IsOpen) continue;
            DrawWorldBox(door.Definition.Min, door.Definition.Max,
                ToXnaColor(door.Definition.Color));
        }

        foreach (var prop in _world.Manifest.Props ?? new List<WorldProp>())
        {
            if (!prop.Visible) continue;
            var position = prop.Position.ToWorldPoint();
            DrawModel(prop.Model, new Vector3(position.X, position.Y, position.Z),
                prop.Scale, prop.Rotation);
        }

        foreach (var pickup in _pickups)
        {
            var position = pickup.Position.ToWorldPoint();
            DrawModel(pickup.Model, new Vector3(position.X, position.Y, position.Z),
                pickup.Scale, 0f);
        }
    }

    private void DrawSneakEye(int cx, int cy, Color colour)
    {
        var shadow = new Color(0, 0, 0, 190);

        // Block-built eye: it remains crisp at every supported UI scale and is legible over
        // both the bright ground and the dark dungeon walls.
        Fill(new Rectangle(cx - 17, cy - 9, 34, 3), shadow);
        Fill(new Rectangle(cx - 17, cy + 6, 34, 3), shadow);
        Fill(new Rectangle(cx - 13, cy - 6, 6, 3), shadow);
        Fill(new Rectangle(cx + 7, cy - 6, 6, 3), shadow);
        Fill(new Rectangle(cx - 13, cy + 3, 6, 3), shadow);
        Fill(new Rectangle(cx + 7, cy + 3, 6, 3), shadow);

        Fill(new Rectangle(cx - 14, cy - 7, 28, 2), colour);
        Fill(new Rectangle(cx - 14, cy + 5, 28, 2), colour);
        Fill(new Rectangle(cx - 10, cy - 5, 5, 2), colour);
        Fill(new Rectangle(cx + 5, cy - 5, 5, 2), colour);
        Fill(new Rectangle(cx - 10, cy + 3, 5, 2), colour);
        Fill(new Rectangle(cx + 5, cy + 3, 5, 2), colour);
        Fill(new Rectangle(cx - 4, cy - 4, 8, 8), colour);
        Fill(new Rectangle(cx - 1, cy - 1, 2, 2), new Color(20, 26, 27));
    }

    private void DrawWorldBox(WorldVector min, WorldVector max, Color color)
    {
        var centre = new Vector3(
            (min.X + max.X) * 0.5f,
            (min.Y + max.Y) * 0.5f,
            (min.Z + max.Z) * 0.5f);
        var scale = new Vector3(max.X - min.X, max.Y - min.Y, max.Z - min.Z);
        DrawCube(centre, scale, color, 0f);
    }

    private static Color ToXnaColor(WorldColor color) => new(
        (byte)Math.Clamp(color.R, 0, 255),
        (byte)Math.Clamp(color.G, 0, 255),
        (byte)Math.Clamp(color.B, 0, 255),
        (byte)Math.Clamp(color.A, 0, 255));

    private void DrawWorldBase(Color sky, Color horizon)
    {
        GraphicsDevice.Clear(sky);
        DrawCube(new Vector3(0f, -0.45f, 0f), new Vector3(22f, 0.3f, 22f), horizon, 0f);
    }

    private void DrawModel(string key, Vector3 position, float scale, float rotation)
    {
        if (!_models.TryGetValue(key, out var model))
            return;

        var normalizer = _modelNormalizers.TryGetValue(key, out var storedNormalizer) ? storedNormalizer : 1f;
        var center = _modelCenters.TryGetValue(key, out var storedCenter) ? storedCenter : Vector3.Zero;
        var world = Matrix.CreateTranslation(-center)
            * Matrix.CreateScale(scale * normalizer)
            * Matrix.CreateRotationY(rotation)
            * Matrix.CreateTranslation(position);

        var boneTransforms = _modelBones.TryGetValue(key, out var cachedBones)
            ? cachedBones
            : Array.Empty<Matrix>();

        foreach (var mesh in model.Meshes)
        {
            var meshTransform = boneTransforms.Length > mesh.ParentBone.Index
                ? boneTransforms[mesh.ParentBone.Index]
                : Matrix.Identity;

            foreach (var effect in mesh.Effects)
            {
                // Only what actually changes per frame. Lighting and fog are set once, at
                // load: EnableDefaultLighting rewrites every light and reselects a shader
                // permutation, and it was running for every mesh of every model every frame.
                if (effect is BasicEffect basic)
                {
                    basic.World = meshTransform * world;
                    basic.View = _view;
                    basic.Projection = _projection;
                }
            }

            mesh.Draw();
        }
    }

    private static (Vector3 Center, float Extent) MeasureModel(Model model, Matrix[] boneTransforms)
    {
        if (model.Meshes.Count == 0)
            return (Vector3.Zero, 1f);

        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        foreach (var mesh in model.Meshes)
        {
            var transform = boneTransforms.Length > mesh.ParentBone.Index
                ? boneTransforms[mesh.ParentBone.Index]
                : Matrix.Identity;
            var sphere = mesh.BoundingSphere;
            var centre = Vector3.Transform(sphere.Center, transform);
            var scale = MathF.Max(
                Vector3.TransformNormal(Vector3.Right, transform).Length(),
                MathF.Max(
                    Vector3.TransformNormal(Vector3.Up, transform).Length(),
                    Vector3.TransformNormal(Vector3.Forward, transform).Length()));
            var radius = new Vector3(sphere.Radius * scale);
            minimum = Vector3.Min(minimum, centre - radius);
            maximum = Vector3.Max(maximum, centre + radius);
        }

        var center = (minimum + maximum) * 0.5f;
        var halfSize = (maximum - minimum) * 0.5f;
        var extent = MathF.Max(halfSize.X, MathF.Max(halfSize.Y, halfSize.Z));
        return (center, MathF.Max(extent, 0.001f));
    }

    private void DrawCube(Vector3 position, Vector3 scale, Color color, float rotation)
    {
        _primitiveEffect.World = Matrix.CreateScale(scale)
            * Matrix.CreateRotationY(rotation)
            * Matrix.CreateTranslation(position);
        _primitiveEffect.View = _view;
        _primitiveEffect.Projection = _projection;
        _primitiveEffect.DiffuseColor = color.ToVector3();
        _primitiveEffect.Alpha = color.A / 255f;

        foreach (var pass in _primitiveEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _cubeVertices,
                0,
                _cubeVertices.Length,
                _cubeIndices,
                0,
                _cubeIndices.Length / 3);
        }
    }

    private void CreatePrimitiveCube()
    {
        _cubeVertices = new VertexPositionNormalTexture[24];
        _cubeIndices = new short[36];

        var positions = new[]
        {
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f)
        };
        var normals = new[]
        {
            Vector3.Forward, Vector3.Backward, Vector3.Left, Vector3.Right, Vector3.Up, Vector3.Down
        };

        for (var face = 0; face < 6; face++)
        {
            for (var vertex = 0; vertex < 4; vertex++)
            {
                _cubeVertices[face * 4 + vertex] = new VertexPositionNormalTexture(
                    positions[face * 4 + vertex],
                    normals[face],
                    new Vector2(vertex == 1 || vertex == 2 ? 1f : 0f, vertex >= 2 ? 0f : 1f));
            }

            var index = face * 6;
            var vertexIndex = face * 4;
            _cubeIndices[index] = (short)vertexIndex;
            _cubeIndices[index + 1] = (short)(vertexIndex + 1);
            _cubeIndices[index + 2] = (short)(vertexIndex + 2);
            _cubeIndices[index + 3] = (short)vertexIndex;
            _cubeIndices[index + 4] = (short)(vertexIndex + 2);
            _cubeIndices[index + 5] = (short)(vertexIndex + 3);
        }
    }

    private void DrawModeHeader(string title, string subtitle)
    {
        BeginUi();
        DrawPanel(new Rectangle(0, 0, 1280, 64), new Color(4, 8, 13, 236), new Color(75, 129, 150));
        Text("RATNA BAY / FEASIBILITY LAB", new Vector2(26, 12), 17, new Color(165, 212, 224));
        TextFit(title, new Vector2(350, 9), 500f, 20, Color.White);
        TextFit(subtitle, new Vector2(350, 37), 500f, 12, new Color(153, 174, 181));
        TextFit("[1] Assets   [2] Photo Study   [3] UI Stress   [Esc] Exit", new Vector2(902, 22), 350f, 12, new Color(194, 208, 207));
        EndUi();
    }

    private void DrawComplexUi()
    {
        BeginUi();

        DrawPanel(new Rectangle(24, 84, 270, 300), new Color(9, 16, 24, 236), new Color(79, 141, 164));
        Text("CHARACTER", new Vector2(44, 102), 13, new Color(145, 198, 210));
        TextFit("RATNA BAY EXPLORER", new Vector2(44, 123), 226f, 18, Color.White);
        DrawPortrait(new Rectangle(44, 160, 86, 104), new Color(72, 56, 45));
        TextFit("Level 4  |  Wayfarer", new Vector2(146, 168), 130f, 14, new Color(216, 225, 219));
        DrawBar(new Rectangle(146, 198, 120, 12), 0.72f, new Color(194, 66, 72), "HP  72 / 100");
        DrawBar(new Rectangle(146, 228, 120, 12), 0.48f, new Color(70, 130, 212), "MP  24 / 50");
        Text("STR  12     AGI  15", new Vector2(44, 285), 13, new Color(178, 192, 189));
        Text("WIL  11     LCK  09", new Vector2(44, 306), 13, new Color(178, 192, 189));
        Text("FATIGUE  34%", new Vector2(44, 339), 13, new Color(215, 176, 111));

        DrawPanel(new Rectangle(314, 84, 582, 146), new Color(10, 18, 23, 239), new Color(182, 137, 71));
        Text("ACTIVE QUEST", new Vector2(336, 102), 13, new Color(239, 196, 111));
        TextFit("The Lantern Under the Hill", new Vector2(336, 123), 528f, 21, Color.White);
        TextFit("Find the sealed stair beneath the old watch road.", new Vector2(336, 158), 528f, 14, new Color(211, 219, 210));
        DrawQuestStep(new Vector2(340, 190), "1", "Speak to gatekeeper", true, 178f);
        DrawQuestStep(new Vector2(530, 190), "2", "Inspect lantern", true, 178f);
        DrawQuestStep(new Vector2(720, 190), "3", "Open sealed stair", false, 166f);

        DrawPanel(new Rectangle(916, 84, 340, 300), new Color(7, 16, 22, 235), new Color(89, 167, 177));
        Text("REGION MAP", new Vector2(938, 102), 13, new Color(152, 213, 211));
        DrawMap(new Rectangle(938, 132, 296, 196));
        TextFit("Northwatch / 1.4 km", new Vector2(938, 341), 296f, 13, new Color(192, 207, 202));
        TextFit("Weather: clear  |  Visibility: good", new Vector2(938, 360), 296f, 12, new Color(136, 169, 170));

        DrawPanel(new Rectangle(314, 248, 582, 338), new Color(9, 15, 22, 242), new Color(87, 112, 128));
        Text("INVENTORY", new Vector2(336, 266), 13, new Color(163, 190, 197));
        Text("Field pack", new Vector2(336, 287), 20, Color.White);
        TextRight("12 / 24 slots", 856f, 294f, 13, new Color(178, 199, 198));
        DrawInventoryGrid(new Rectangle(336, 328, 520, 226));

        DrawPanel(new Rectangle(916, 402, 340, 184), new Color(8, 14, 21, 240), new Color(97, 114, 134));
        Text("EQUIPMENT", new Vector2(938, 420), 13, new Color(169, 190, 203));
        DrawEquipmentRow(new Vector2(938, 448), "MAIN HAND", "Ashwood sabre", new Color(180, 139, 83));
        DrawEquipmentRow(new Vector2(938, 486), "OFF HAND", "Traveler's lantern", new Color(212, 173, 90));
        DrawEquipmentRow(new Vector2(938, 524), "ARMOR", "Riveted leather", new Color(115, 129, 130));

        DrawPanel(new Rectangle(24, 404, 270, 182), new Color(9, 15, 22, 238), new Color(101, 135, 156));
        Text("NOTIFICATIONS", new Vector2(44, 422), 13, new Color(154, 194, 207));
        TextFit("A distant bell rings.", new Vector2(44, 454), 226f, 14, new Color(216, 226, 216));
        TextFit("Discovered: Northwatch", new Vector2(44, 482), 226f, 13, new Color(201, 176, 113));
        TextFit("Campfire warmth  +4 HP", new Vector2(44, 510), 226f, 13, new Color(166, 198, 175));
        TextFit("[I] Inventory  [J] Journal  [M] Map", new Vector2(44, 552), 226f, 12, new Color(131, 158, 165));

        DrawPanel(new Rectangle(24, 610, 1232, 78), new Color(4, 8, 13, 244), new Color(76, 101, 116));
        DrawQuickSlots(new Vector2(46, 626));
        Text("Frame: 60 fps", new Vector2(1048, 632), 12, new Color(120, 149, 155));
        Text("Mode 3 / UI stress", new Vector2(1048, 652), 12, new Color(120, 149, 155));

        EndUi();
    }

    private void DrawPortrait(Rectangle bounds, Color color)
    {
        Fill(bounds, color);
        Fill(new Rectangle(bounds.X + 18, bounds.Y + 16, bounds.Width - 36, 38), new Color(108, 80, 60));
        Fill(new Rectangle(bounds.X + 25, bounds.Y + 51, bounds.Width - 50, 42), new Color(45, 59, 75));
        Border(bounds, new Color(206, 166, 102));
    }

    private void DrawBar(Rectangle bounds, float amount, Color color, string label)
    {
        Fill(bounds, new Color(26, 34, 40));
        Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * amount), bounds.Height), color);
        TextFit(label, new Vector2(bounds.X, bounds.Y + 15), bounds.Width, 11, new Color(182, 197, 194));
    }

    private void DrawQuestStep(Vector2 position, string number, string label, bool complete, float maxWidth)
    {
        var color = complete ? new Color(101, 183, 135) : new Color(145, 153, 158);
        Fill(new Rectangle((int)position.X, (int)position.Y, 22, 22), color);
        Text(number, position + new Vector2(7, 2), 13, new Color(7, 16, 20));
        TextFit(label, position + new Vector2(30, 4), maxWidth - 30f, 12, complete ? new Color(197, 220, 204) : new Color(150, 161, 163));
    }

    private void DrawMap(Rectangle bounds)
    {
        Fill(bounds, new Color(18, 45, 48));
        for (var x = bounds.X + 20; x < bounds.Right; x += 35)
            Fill(new Rectangle(x, bounds.Y, 1, bounds.Height), new Color(47, 87, 83, 180));
        for (var y = bounds.Y + 20; y < bounds.Bottom; y += 32)
            Fill(new Rectangle(bounds.X, y, bounds.Width, 1), new Color(47, 87, 83, 180));
        Fill(new Rectangle(bounds.X + 24, bounds.Y + 138, 174, 5), new Color(97, 125, 104));
        Fill(new Rectangle(bounds.X + 156, bounds.Y + 44, 5, 126), new Color(97, 125, 104));
        Fill(new Rectangle(bounds.X + 214, bounds.Y + 76, 10, 10), new Color(222, 168, 75));
        Fill(new Rectangle(bounds.X + 91, bounds.Y + 117, 10, 10), new Color(198, 83, 86));
        Fill(new Rectangle(bounds.X + 156, bounds.Y + 43, 8, 8), new Color(102, 188, 205));
        Border(bounds, new Color(74, 124, 127));
    }

    private void DrawInventoryGrid(Rectangle bounds)
    {
        var columns = 6;
        var rows = 3;
        var gap = 8;
        var slotWidth = (bounds.Width - gap * (columns - 1)) / columns;
        var slotHeight = (bounds.Height - gap * (rows - 1)) / rows;
        var items = new[] { "SABRE", "LANTERN", "HERB", "ROPE", "KEY", "GEM", "BREAD", "BANDAGE", "MAP", "TORCH", "RING", "EMPTY" };

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                var x = bounds.X + column * (slotWidth + gap);
                var y = bounds.Y + row * (slotHeight + gap);
                var selected = index == 0;
                Fill(new Rectangle(x, y, slotWidth, slotHeight), selected ? new Color(69, 67, 47) : new Color(24, 31, 38));
                Border(new Rectangle(x, y, slotWidth, slotHeight), selected ? new Color(218, 176, 90) : new Color(67, 83, 94));
                if (index < items.Length - 1)
                {
                    Fill(new Rectangle(x + 28, y + 18, 34, 28), new Color(83 + index * 5, 83 + index * 3, 76));
                    TextFit(items[index], new Vector2(x + 8, y + slotHeight - 24), slotWidth - 16f, 11, new Color(181, 194, 192));
                }
                Text((index + 1).ToString(), new Vector2(x + 6, y + 6), 11, new Color(113, 137, 143));
            }
        }
    }

    private void DrawEquipmentRow(Vector2 position, string slot, string item, Color color)
    {
        Fill(new Rectangle((int)position.X, (int)position.Y, 28, 28), new Color(37, 46, 53));
        Border(new Rectangle((int)position.X, (int)position.Y, 28, 28), color);
        Text(slot, position + new Vector2(40, 0), 10, new Color(117, 143, 151));
        TextFit(item, position + new Vector2(40, 12), 250f, 13, Color.White);
    }

    private void DrawQuickSlots(Vector2 position)
    {
        for (var index = 0; index < 8; index++)
        {
            var slot = new Rectangle((int)position.X + index * 48, (int)position.Y, 38, 38);
            Fill(slot, index == 0 ? new Color(73, 67, 43) : new Color(26, 34, 41));
            Border(slot, index == 0 ? new Color(221, 177, 88) : new Color(75, 91, 99));
            Text((index + 1).ToString(), new Vector2(slot.X + 6, slot.Y + 4), 11, new Color(140, 165, 166));
            Fill(new Rectangle(slot.X + 13, slot.Y + 16, 14, 12), new Color(121 + index * 8, 94 + index * 4, 60));
        }
    }

    private void BeginUi()
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, _uiTransform);
    }

    /// <summary>
    /// Closes the UI batch, drawing our pointer last.
    ///
    /// Doing it here rather than at each screen is deliberate: the pointer was previously
    /// added to one screen by hand and silently missing from the main menu, which is exactly
    /// the failure a shared exit point prevents.
    /// </summary>
    private void EndUi()
    {
        DrawPointer();
        _spriteBatch.End();
    }

    private void DrawPanel(Rectangle bounds, Color fill, Color border)
    {
        Fill(bounds, fill);
        Border(bounds, border);
    }

    private void Fill(Rectangle bounds, Color color) => _spriteBatch.Draw(_white, bounds, color);

    private void Border(Rectangle bounds, Color color)
    {
        Fill(new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
        Fill(new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
        Fill(new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
        Fill(new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
    }

    private void Text(string value, Vector2 position, float scale, Color color)
    {
        var (font, drawScale) = SelectFont(scale);
        DrawString(font, value, position, drawScale, color);
    }

    private void TextFit(string value, Vector2 position, float maxWidth, float scale, Color color)
    {
        var (font, drawScale) = SelectFont(scale);
        var measuredWidth = font.MeasureString(value).X * drawScale;
        if (measuredWidth > maxWidth && measuredWidth > 0f)
            drawScale *= maxWidth / measuredWidth;

        DrawString(font, value, position, drawScale, color);
    }

    private void TextCentred(string value, float centreX, float y, float scale, Color color)
    {
        var (font, drawScale) = SelectFont(scale);
        var width = font.MeasureString(value).X * drawScale;
        DrawString(font, value, new Vector2(centreX - width * 0.5f, y), drawScale, color);
    }

    /// <summary>
    /// Text laid out over several lines at a fixed size.
    ///
    /// TextFit shrinks to fit one line, so a long dialogue answer was rendered microscopic
    /// rather than wrapped. Reading a paragraph is the whole point of a conversation, so it
    /// wraps at word boundaries and keeps the size it was asked for.
    /// </summary>
    /// <returns>The height used, so a caller can lay out beneath it.</returns>
    private float TextWrapped(string value, Vector2 position, float maxWidth, float scale,
        Color color, int maxLines = 6)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0f;

        var (font, drawScale) = SelectFont(scale);
        var lineHeight = scale * 1.34f;
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var line = string.Empty;
        var lines = 0;
        var y = position.Y;

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (font.MeasureString(candidate).X * drawScale <= maxWidth)
            {
                line = candidate;
                continue;
            }

            if (line.Length > 0)
            {
                DrawString(font, line, new Vector2(position.X, y), drawScale, color);
                y += lineHeight;
                if (++lines >= maxLines) return y - position.Y;
            }

            line = word;
        }

        if (line.Length > 0)
        {
            DrawString(font, line, new Vector2(position.X, y), drawScale, color);
            y += lineHeight;
        }

        return y - position.Y;
    }

    private void TextRight(string value, float right, float y, float scale, Color color)
    {
        var (font, drawScale) = SelectFont(scale);
        var width = font.MeasureString(value).X * drawScale;
        DrawString(font, value, new Vector2(right - width, y), drawScale, color);
    }

    /// <summary>
    /// Pick a font rasterized at the size it will actually occupy on the display.
    ///
    /// The previous version kept three fixed atlases (18/24/32 px) and scaled them to fit,
    /// so a 12 px label was an 18 px atlas squeezed to 0.67 and then stretched again by the
    /// canvas transform. Two resamples is why the HUD was soft and thin. Rasterizing at the
    /// device size and drawing at 1/scale lands every glyph 1:1 on the panel.
    /// </summary>
    private (SpriteFontBase Font, float Scale) SelectFont(float requestedSize)
    {
        var heading = requestedSize >= HeadingThreshold;
        var cache = heading ? _headingFonts : _bodyFonts;

        // Clamped so an extreme display cannot ask for a 4 px or a 900 px atlas.
        var devicePixels = Math.Clamp((int)MathF.Round(requestedSize * _uiScale), 8, 384);

        if (!cache.TryGetValue(devicePixels, out var font))
        {
            font = (heading ? _headingFontSystem : _fontSystem).GetFont(devicePixels);
            cache[devicePixels] = font;
        }

        return (font, requestedSize / devicePixels);
    }

    /// <summary>At and above this logical size, text is set in Cinzel rather than Noto Sans.</summary>
    private const float HeadingThreshold = 20f;

    private void DrawString(SpriteFontBase font, string value, Vector2 position, float scale, Color color)
    {
        var fontScale = new Vector2(scale);
        if (color.A > 20)
        {
            _spriteBatch.DrawString(
                font,
                value,
                position + new Vector2(1f, 1f),
                new Color(0, 0, 0, 150),
                0f,
                Vector2.Zero,
                fontScale,
                0f,
                0f,
                0f,
                TextStyle.None,
                FontSystemEffect.None,
                0);
        }

        _spriteBatch.DrawString(
            font,
            value,
            position,
            color,
            0f,
            Vector2.Zero,
            fontScale,
            0f,
            0f,
            0f,
            TextStyle.None,
            FontSystemEffect.None,
            0);
    }

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
