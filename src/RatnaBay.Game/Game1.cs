using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Client;

public sealed class Game1 : Game
{
    private const int LogicalWidth = 1280;
    private const int LogicalHeight = 720;

    private enum GameScreen
    {
        MainMenu,
        WorldScene,
        AssetGallery,
        PhotoScene,
        UiStress
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
    private bool _showHelp;

    /// <summary>Radians of rotation per pixel of mouse travel.</summary>
    private const float MouseSensitivity = 0.0032f;

    /// <summary>Radians per second while an arrow key is held.</summary>
    private const float KeyboardTurnSpeed = 2.2f;

    /// <summary>How far up or down the view can tip, short of straight up.</summary>
    private const float PitchLimit = 1.4f;

    /// <summary>Metres per second. Walking was 3.5, which read as wading.</summary>
    private const float WalkSpeed = 6f;

    private const float SprintSpeed = 11f;
    private GameScreen _screen = GameScreen.MainMenu;
    private int _menuSelection;
    private Vector3 _cameraPosition = new(0f, 2.4f, 8.5f);
    private float _cameraYaw;
    private float _cameraPitch = -0.12f;
    private Matrix _view;
    private Matrix _projection;
    private Matrix _uiTransform = Matrix.Identity;
    private bool _borderlessFullscreen = true;

    /// <summary>The live character. Null until a game is started or loaded.</summary>
    private GameSession? _session;

    /// <summary>The enemies in the scene and the fight with them.</summary>
    private Encounter? _encounter;

    private BillboardRenderer _billboards = null!;

    /// <summary>Set by --screenshot: render a few frames, save a PNG, and quit.</summary>
    private string? _screenshotPath;

    /// <summary>Camera angles forced by --yaw / --pitch, for reproducible captures.</summary>
    private float? _startYaw;
    private float? _startPitch;

    /// <summary>Frames to render before --screenshot captures. Raise it to measure the rate.</summary>
    private int _warmupFrames = 4;

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
        IsMouseVisible = true;

        // MonoGame defaults to a fixed timestep, where ElapsedGameTime is always 1/60 no
        // matter how long the frame really took. At 43 fps that advanced game time at 72%
        // of real time, so walking was a quarter slower than its own speed constant said.
        // A variable timestep makes elapsed time mean elapsed time.
        IsFixedTimeStep = false;
        Window.Title = "Ratna Bay - Development Shell";
        Window.IsBorderless = true;

        _screen = ParseMode(args);
        _screenshotPath = ParseOption(args, "--screenshot");

        // Deterministic camera for screenshots, so a change to look or movement can be
        // compared frame against frame instead of described.
        if (int.TryParse(ParseOption(args, "--warmup"), out var warmup)) _warmupFrames = warmup;
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
                "assets" or "asset" or "gallery" => GameScreen.AssetGallery,
                "photo" or "photorealism" => GameScreen.PhotoScene,
                "ui" or "stress" => GameScreen.UiStress,
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

        // Launching straight into the scene (--mode scene, screenshots, playtests) needs a
        // character, or the HUD has nothing to show.
        if (_screen == GameScreen.WorldScene) StartSession(GameSession.NewGame());

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

        _white = new Texture2D(GraphicsDevice, 1, 1);
        _white.SetData(new[] { Color.White });

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
        CharacterSprites.Clear();
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
                Exit();
            }
            else if (_showHelp)
            {
                _showHelp = false;
            }
            else
            {
                SetMouseLook(false);
                _screen = GameScreen.MainMenu;
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
    private void SetMouseLook(bool enabled)
    {
        if (_screenshotPath is not null) enabled = false;

        _mouseLook = enabled;
        IsMouseVisible = !enabled;
        if (enabled) CentreMouse();
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
            case GameScreen.AssetGallery:
                DrawGallery();
                DrawModeHeader("3D ASSET IMPORT", "Kenney CC0 + Poly Haven CC0 | FBX -> MGCB -> Model");
                break;
            case GameScreen.PhotoScene:
                DrawPhotoScene(true);
                DrawModeHeader("PHOTOREALISM FEASIBILITY", "Textured FBX prop + authored lighting + fog + scene composition");
                break;
            case GameScreen.UiStress:
                DrawPhotoScene(false);
                DrawModeHeader("UI STRESS", "Layered RPG interface over the 3D scene");
                DrawComplexUi();
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
    private static string[] MenuItems => GameSession.HasSaveFile
        ? new[] { "Continue", "Start New Game", "Renderer Lab", "UI Stress Test", "Exit" }
        : new[] { "Start New Game", "Renderer Lab", "UI Stress Test", "Exit" };

    private void UpdateMenu(KeyboardState keyboard, MouseState mouse)
    {
        var menuItemCount = MenuItems.Length;
        _menuSelection = Math.Clamp(_menuSelection, 0, menuItemCount - 1);

        if (Pressed(keyboard, Keys.Up))
            _menuSelection = (_menuSelection + menuItemCount - 1) % menuItemCount;
        if (Pressed(keyboard, Keys.Down))
            _menuSelection = (_menuSelection + 1) % menuItemCount;

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

    /// <summary>
    /// One menu row. Drawing and hit testing both read this, so a clickable row is always
    /// exactly the row the player can see.
    /// </summary>
    private static Rectangle MenuItemBounds(int index) => new(120, 286 + index * 56, 368, 42);

    private void ActivateMenuItem()
    {
        switch (MenuItems[_menuSelection])
        {
            case "Continue":
                ResetCamera();
                LoadSession();
                _screen = GameScreen.WorldScene;
                SetMouseLook(true);
                break;
            case "Start New Game":
                ResetCamera();
                StartSession(GameSession.NewGame());
                _session!.ShowToast("You wake on the Northwatch road.");
                _screen = GameScreen.WorldScene;
                SetMouseLook(true);
                break;
            case "Renderer Lab":
                _screen = GameScreen.AssetGallery;
                break;
            case "UI Stress Test":
                _screen = GameScreen.UiStress;
                break;
            case "Exit":
                Exit();
                break;
        }
    }

    private void UpdateGameScreen(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        if (Pressed(keyboard, Keys.D1)) _screen = GameScreen.AssetGallery;
        if (Pressed(keyboard, Keys.D2)) _screen = GameScreen.PhotoScene;
        if (Pressed(keyboard, Keys.D3)) _screen = GameScreen.UiStress;
        if (Pressed(keyboard, Keys.M)) { SetMouseLook(false); _screen = GameScreen.MainMenu; }
        if (Pressed(keyboard, Keys.F1)) { _showHelp = !_showHelp; if (_showHelp) SetMouseLook(false); }
        if (Pressed(keyboard, Keys.Tab)) SetMouseLook(!_mouseLook);

        // Clicking the world takes the pointer back; Tab or Escape gives it up. Nothing
        // grabs the mouse without the player asking for it.
        if (!_mouseLook && !_showHelp && Clicked(mouse) && IsActive) SetMouseLook(true);

        UpdateCamera(gameTime, keyboard, mouse);

        if (_screen == GameScreen.WorldScene)
            UpdateSession(gameTime, keyboard);
    }

    /// <summary>
    /// Drive the domain from the running game: advance its clock, feed it the player's
    /// position, and honour the save keys.
    /// </summary>
    private void UpdateSession(GameTime gameTime, KeyboardState keyboard)
    {
        if (_session is null) return;

        // The camera is the player for now; a controller replaces this in iteration 7.
        _session.Position = new WorldPoint(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
        _session.Yaw = _cameraYaw;
        _session.Pitch = _cameraPitch;

        _session.Tick(StepSeconds(gameTime));

        // Sprinting is the only thing that spends stamina yet, so it is what proves the
        // vitals on screen are the domain's numbers rather than painted ones.
        if (keyboard.IsKeyDown(Keys.LeftShift) && IsMoving(keyboard))
            _session.Player.Vitals.SpendStamina(18f * StepSeconds(gameTime));

        if (Pressed(keyboard, Keys.F5)) _session.ShowToast(_session.Save());
        if (Pressed(keyboard, Keys.F9)) LoadSession();

        UpdateCombat(gameTime, keyboard);
    }

    /// <summary>
    /// The fight: enemies act, then the player does. Blocking is held rather than pressed,
    /// and attacking drops the guard, so the two cannot be used at once.
    /// </summary>
    private void UpdateCombat(GameTime gameTime, KeyboardState keyboard)
    {
        if (_session is null || _encounter is null) return;

        _encounter.Update(StepSeconds(gameTime), _cameraPosition, _cameraYaw);

        // Only while the pointer is captured, so a click that is reclaiming the mouse does
        // not also swing the sword.
        if (!_mouseLook || _showHelp) return;

        var mouse = Mouse.GetState();
        _session.Player.Combat.SetBlocking(mouse.RightButton == ButtonState.Pressed);

        if (Clicked(mouse)) ReportAttack(_encounter.PlayerAttack());
        if (Pressed(keyboard, Keys.Q)) ReportCast(_encounter.PlayerCast(_cameraPosition, _cameraYaw));

        // Number keys pick the bound spell.
        if (Pressed(keyboard, Keys.D4)) SelectSpell(SpellCatalog.FireId);
        if (Pressed(keyboard, Keys.D5)) SelectSpell(SpellCatalog.FrostId);
        if (Pressed(keyboard, Keys.D6)) SelectSpell(SpellCatalog.ShockId);
        if (Pressed(keyboard, Keys.D7)) SelectSpell(SpellCatalog.HealId);
        if (Pressed(keyboard, Keys.D8)) SelectSpell(SpellCatalog.LightId);
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

    private static bool IsMoving(KeyboardState keyboard) =>
        keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.A)
        || keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.D);

    /// <summary>
    /// Begin a session and populate the world around it. The camp is spawned here rather
    /// than by the session, because where a bandit stands is a scene fact, not a save fact —
    /// the save only remembers which ones are already dead.
    /// </summary>
    private void StartSession(GameSession session)
    {
        _session = session;
        _encounter = new Encounter(session);
        _encounter.SpawnDefaultCamp();

        session.Player.Vitals.Died += () =>
        {
            session.ShowToast("You were defeated — returned to safe ground.");
            session.Player.Vitals.FullRestore();
            session.Player.Combat.ClearCombat();
            ResetCamera();
        };
    }

    private void LoadSession()
    {
        if (_session is null) StartSession(GameSession.NewGame());

        var message = _session!.Load();
        _cameraPosition = new Vector3(_session.Position.X, _session.Position.Y, _session.Position.Z);
        _cameraYaw = _session.Yaw;

        _encounter = new Encounter(_session);
        _encounter.SpawnDefaultCamp();

        _session.ShowToast(message);
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

        var scale = MathF.Min(viewport.Width / (float)LogicalWidth, viewport.Height / (float)LogicalHeight);
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

            var (center, extent) = MeasureModel(model);
            _modelCenters[key] = center;
            _modelNormalizers[key] = 1f / extent;

            var bones = new Matrix[model.Bones.Count];
            if (bones.Length > 0) model.CopyAbsoluteBoneTransformsTo(bones);
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
        if (keyboard.IsKeyDown(Keys.Space)) movement += Vector3.Up;
        if (keyboard.IsKeyDown(Keys.LeftControl)) movement -= Vector3.Up;

        if (movement.LengthSquared() > 0.001f)
        {
            movement.Normalize();
            _cameraPosition += movement * speed * seconds;
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

    private void ResetCamera()
    {
        _cameraPosition = new Vector3(0f, 2.4f, 8.5f);
        _cameraYaw = 0f;
        _cameraPitch = -0.12f;
    }

    private void DrawMenu()
    {
        DrawPhotoScene(false);

        BeginUi();
        Fill(new Rectangle(0, 0, 1280, 720), new Color(3, 7, 12, 178));
        DrawPanel(new Rectangle(64, 62, 1152, 596), new Color(5, 11, 18, 232), new Color(91, 146, 159));

        Text("RATNA BAY", new Vector2(98, 96), 38, Color.White);
        Text("DEVELOPMENT BUILD", new Vector2(101, 153), 13, new Color(161, 211, 218));
        TextFit("A code-first fantasy RPG prototype", new Vector2(101, 181), 420f, 15, new Color(184, 197, 196));

        DrawPanel(new Rectangle(96, 222, 416, 322), new Color(8, 16, 24, 238), new Color(65, 105, 119));
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

        DrawPanel(new Rectangle(560, 222, 592, 322), new Color(8, 16, 24, 226), new Color(65, 105, 119));
        Text("NORTHWATCH OUTSKIRTS", new Vector2(592, 246), 14, new Color(151, 206, 210));
        Text("A small world to build on", new Vector2(592, 280), 24, Color.White);
        TextFit("The first scene establishes the basic loop:", new Vector2(592, 326), 500f, 15, new Color(190, 203, 200));
        TextFit("enter the world, move through a handcrafted space,", new Vector2(592, 350), 500f, 15, new Color(190, 203, 200));
        TextFit("and inspect the renderer as it grows.", new Vector2(592, 374), 500f, 15, new Color(190, 203, 200));
        Text("CURRENT FOUNDATION", new Vector2(592, 414), 12, new Color(214, 183, 108));
        Text("3D asset loading", new Vector2(592, 442), 14, new Color(190, 215, 208));
        Text("Code-drawn interface", new Vector2(592, 468), 14, new Color(190, 215, 208));
        Text("Keyboard-driven iteration", new Vector2(592, 494), 14, new Color(190, 215, 208));

        Text("Click or hover to choose      Up / Down select      Enter confirm      Esc exit",
            new Vector2(98, 610), 14, new Color(163, 191, 194));
        EndUi();
    }

    private void DrawWorldScene()
    {
        DrawPhotoScene(false);
        DrawEnemies();

        BeginUi();

        DrawDamageFlash();
        DrawCrosshair();
        DrawLocationBanner();
        DrawEnemyHealth();
        DrawObjective();
        DrawVitals();
        DrawToasts();
        DrawStatusStrip();
        if (_showHelp) DrawHelpOverlay();

        EndUi();
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
            var feet = new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z);
            var tint = _encounter.TintOf(enemy);

            // A chilled bandit is visibly cold, so frost reads as more than a slower walk.
            if (enemy.IsChilled) tint = new Color(tint.R / 2 + 90, tint.G / 2 + 110, tint.B);

            _billboards.Draw(texture, feet, Encounter.FigureHeight, _cameraYaw, tint);
        }

        // The billboard pass leaves its own render state behind; the UI expects the default.
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private float DistanceToCamera(Enemy enemy) =>
        Vector3.DistanceSquared(_cameraPosition,
            new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z));

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

        var status = enemy.IsStaggered ? "staggered"
            : enemy.IsBurning ? "burning"
            : enemy.IsChilled ? "chilled"
            : string.Empty;

        if (status.Length > 0)
            TextCentred(status, LogicalWidth / 2f, bar.Bottom + 6, 13, new Color(232, 194, 116));
    }

    /// <summary>Where a swing or a spell will go. Small, and always centred.</summary>
    private void DrawCrosshair()
    {
        const int cx = LogicalWidth / 2;
        const int cy = LogicalHeight / 2;
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
    private void DrawLocationBanner()
    {
        TextCentred("NORTHWATCH OUTSKIRTS", LogicalWidth / 2f, 24f, 15, new Color(196, 214, 214));
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
            vitals.Health, vitals.MaxHealth, new Color(198, 68, 74));

        DrawVitalBar(new Rectangle(barX, panel.Y + 58, barWidth, 26), "PRANA",
            vitals.Prana, vitals.MaxPrana, new Color(74, 134, 216));

        DrawVitalBar(new Rectangle(barX, panel.Y + 96, barWidth, 26), "STAMINA",
            vitals.Stamina, vitals.MaxStamina, new Color(98, 172, 106));
    }

    /// <summary>One labelled bar. The label and the value live inside it, vertically centred.</summary>
    private void DrawVitalBar(Rectangle bounds, string label, float value, float max, Color colour)
    {
        var fraction = max <= 0f ? 0f : MathHelper.Clamp(value / max, 0f, 1f);

        Fill(bounds, new Color(20, 27, 33));
        Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * fraction), bounds.Height), colour);
        Border(bounds, new Color(0, 0, 0, 110));

        // A dark scrim behind the text keeps it legible over both the filled and empty halves.
        Text(label, new Vector2(bounds.X + 10, bounds.Y + 5), 14, Color.White);
        TextRight($"{value:0} / {max:0}", bounds.Right - 10, bounds.Y + 5, 14, Color.White);
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
        var readied = combat.IsBlocking ? "guarding"
            : combat.InCombat ? "in combat"
            : combat.ActiveWeapon.DisplayName;

        Text(readied, new Vector2(panel.X + 18, panel.Y + 38), 13,
            combat.IsBlocking ? new Color(232, 194, 116) : new Color(146, 174, 178));
        TextRight($"{_framesPerSecond:0} fps", panel.Right - 18, panel.Y + 38, 13,
            _framesPerSecond < 50f ? new Color(228, 128, 118) : new Color(146, 174, 178));
    }

    /// <summary>
    /// The control list, on demand. It used to be a permanent full-width bar across the
    /// bottom of the screen, which is developer scaffolding rather than a HUD.
    /// </summary>
    private void DrawHelpOverlay()
    {
        Fill(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 7, 12, 200));

        var panel = new Rectangle(320, 58, 640, 604);
        DrawPanel(panel, new Color(7, 14, 21, 244), new Color(91, 146, 159));
        TextCentred("CONTROLS", panel.X + panel.Width / 2f, panel.Y + 26, 24, Color.White);

        (string Key, string Action)[] rows =
        {
            ("W A S D", "move"),
            ("Mouse", "look"),
            ("Left click", "attack"),
            ("Right click", "guard — one-handed only"),
            ("Q", "cast the readied spell"),
            ("4 5 6 7 8", "flame, rime, arc, mend, emberlight"),
            ("Arrow keys", "look (keyboard)"),
            ("Shift", "sprint — spends stamina"),
            ("Space / Ctrl", "rise / descend"),
            ("F5 / F9", "save / load"),
            ("F1", "close this"),
            ("F11", "windowed / fullscreen"),
            ("Tab", "release the mouse"),
            ("M / Esc", "back to the menu")
        };

        var y = panel.Y + 76f;
        foreach (var (key, action) in rows)
        {
            Text(key, new Vector2(panel.X + 44, y), 17, new Color(232, 194, 116));
            Text(action, new Vector2(panel.X + 250, y), 17, new Color(214, 226, 222));
            y += 34f;
        }
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

    private static (Vector3 Center, float Extent) MeasureModel(Model model)
    {
        if (model.Meshes.Count == 0)
            return (Vector3.Zero, 1f);

        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        foreach (var mesh in model.Meshes)
        {
            var radius = new Vector3(mesh.BoundingSphere.Radius);
            minimum = Vector3.Min(minimum, mesh.BoundingSphere.Center - radius);
            maximum = Vector3.Max(maximum, mesh.BoundingSphere.Center + radius);
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

    private void EndUi() => _spriteBatch.End();

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
