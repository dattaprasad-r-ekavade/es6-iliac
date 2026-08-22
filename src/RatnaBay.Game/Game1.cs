using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

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
    private readonly List<string> _assetErrors = new();
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private SpriteFont _smallFont = null!;
    private SpriteFont _headingFont = null!;
    private Texture2D _white = null!;
    private BasicEffect _primitiveEffect = null!;
    private VertexPositionNormalTexture[] _cubeVertices = null!;
    private short[] _cubeIndices = null!;
    private KeyboardState _previousKeyboard;
    private GameScreen _screen = GameScreen.MainMenu;
    private int _menuSelection;
    private Vector3 _cameraPosition = new(0f, 2.4f, 8.5f);
    private float _cameraYaw;
    private float _cameraPitch = -0.12f;
    private Matrix _view;
    private Matrix _projection;
    private Matrix _uiTransform = Matrix.Identity;
    private bool _borderlessFullscreen = true;

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
        Window.Title = "Ratna Bay - Development Shell";
        Window.IsBorderless = true;

        _screen = ParseMode(args);
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
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Feasibility/Fonts/FeasibilityUI");
        _smallFont = Content.Load<SpriteFont>("Feasibility/Fonts/FeasibilityUISmall");
        _headingFont = Content.Load<SpriteFont>("Feasibility/Fonts/FeasibilityUIHeading");

        _white = new Texture2D(GraphicsDevice, 1, 1);
        _white.SetData(new[] { Color.White });

        _primitiveEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = false,
            TextureEnabled = false,
            LightingEnabled = true,
            PreferPerPixelLighting = true,
            AmbientLightColor = new Vector3(0.32f, 0.36f, 0.42f)
        };
        _primitiveEffect.EnableDefaultLighting();
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

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (Pressed(keyboard, Keys.F11))
            SetBorderlessFullscreen(!_borderlessFullscreen);

        if (Pressed(keyboard, Keys.Escape))
        {
            if (_screen == GameScreen.MainMenu)
                Exit();
            else
                _screen = GameScreen.MainMenu;
        }

        if (_screen == GameScreen.MainMenu)
            UpdateMenu(keyboard);
        else
            UpdateGameScreen(gameTime, keyboard);

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
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
    }

    private void UpdateMenu(KeyboardState keyboard)
    {
        const int menuItemCount = 4;

        if (Pressed(keyboard, Keys.Up))
            _menuSelection = (_menuSelection + menuItemCount - 1) % menuItemCount;
        if (Pressed(keyboard, Keys.Down))
            _menuSelection = (_menuSelection + 1) % menuItemCount;

        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
            ActivateMenuItem();
    }

    private void ActivateMenuItem()
    {
        switch (_menuSelection)
        {
            case 0:
                ResetCamera();
                _screen = GameScreen.WorldScene;
                break;
            case 1:
                _screen = GameScreen.AssetGallery;
                break;
            case 2:
                _screen = GameScreen.UiStress;
                break;
            case 3:
                Exit();
                break;
        }
    }

    private void UpdateGameScreen(GameTime gameTime, KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.D1)) _screen = GameScreen.AssetGallery;
        if (Pressed(keyboard, Keys.D2)) _screen = GameScreen.PhotoScene;
        if (Pressed(keyboard, Keys.D3)) _screen = GameScreen.UiStress;
        if (Pressed(keyboard, Keys.M)) _screen = GameScreen.MainMenu;

        UpdateCamera(gameTime, keyboard);
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
        }
        catch (Exception exception)
        {
            _assetErrors.Add($"{key}: {exception.GetType().Name}");
        }
    }

    private void UpdateCamera(GameTime gameTime, KeyboardState keyboard)
    {
        var seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var speed = keyboard.IsKeyDown(Keys.LeftShift) ? 7f : 3.5f;
        var yawInput = 0f;
        var pitchInput = 0f;

        if (keyboard.IsKeyDown(Keys.Left)) yawInput -= 1f;
        if (keyboard.IsKeyDown(Keys.Right)) yawInput += 1f;
        if (keyboard.IsKeyDown(Keys.Up)) pitchInput -= 1f;
        if (keyboard.IsKeyDown(Keys.Down)) pitchInput += 1f;

        _cameraYaw += yawInput * seconds * 1.5f;
        _cameraPitch = MathHelper.Clamp(_cameraPitch + pitchInput * seconds, -1.2f, 1.2f);

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

    private Vector3 Forward => Vector3.Transform(
        Vector3.Forward,
        Matrix.CreateRotationX(_cameraPitch) * Matrix.CreateRotationY(_cameraYaw));

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

        var menuItems = new[] { "Start New Game", "Renderer Lab", "UI Stress Test", "Exit" };
        for (var index = 0; index < menuItems.Length; index++)
        {
            var itemBounds = new Rectangle(120, 286 + index * 56, 368, 42);
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

        Text("Up / Down select     Enter / Space confirm     Esc exit", new Vector2(98, 614), 12, new Color(133, 164, 168));
        EndUi();
    }

    private void DrawWorldScene()
    {
        DrawPhotoScene(false);

        BeginUi();
        DrawPanel(new Rectangle(0, 0, 1280, 64), new Color(4, 8, 13, 232), new Color(75, 129, 150));
        Text("RATNA BAY", new Vector2(26, 12), 19, Color.White);
        Text("NORTHWATCH OUTSKIRTS", new Vector2(164, 18), 12, new Color(157, 202, 207));
        TextRight("DAY 1  /  CLEAR WEATHER", 1254f, 20f, 12, new Color(190, 208, 204));

        DrawPanel(new Rectangle(24, 88, 344, 142), new Color(7, 15, 22, 224), new Color(182, 137, 71));
        Text("CURRENT OBJECTIVE", new Vector2(46, 108), 12, new Color(239, 196, 111));
        TextFit("Reach the old watch road", new Vector2(46, 136), 290f, 19, Color.White);
        TextFit("Follow the lantern markers beyond the camp.", new Vector2(46, 174), 290f, 13, new Color(202, 216, 207));

        DrawPanel(new Rectangle(24, 608, 1232, 80), new Color(4, 8, 13, 232), new Color(76, 101, 116));
        Text("WASD", new Vector2(48, 628), 13, new Color(224, 184, 101));
        Text("move", new Vector2(104, 628), 12, new Color(181, 199, 198));
        Text("Arrow keys", new Vector2(158, 628), 13, new Color(224, 184, 101));
        Text("look", new Vector2(248, 628), 12, new Color(181, 199, 198));
        Text("Shift", new Vector2(300, 628), 13, new Color(224, 184, 101));
        Text("sprint", new Vector2(350, 628), 12, new Color(181, 199, 198));
        Text("M / Esc", new Vector2(420, 628), 13, new Color(224, 184, 101));
        Text("menu", new Vector2(492, 628), 12, new Color(181, 199, 198));
        TextRight("Scene foundation  /  60 fps", 1234f, 628f, 12, new Color(120, 158, 163));
        EndUi();
    }

    private void DrawGallery()
    {
        DrawWorldBase(new Color(32, 52, 67), new Color(18, 31, 38));

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
        DrawWorldBase(new Color(82, 105, 118), new Color(23, 31, 36));

        DrawCube(new Vector3(0f, -0.35f, 0f), new Vector3(24f, 0.4f, 24f), new Color(54, 61, 57), 0f);
        DrawCube(new Vector3(0f, 3.5f, -9f), new Vector3(22f, 7f, 0.3f), new Color(63, 75, 79), 0f);
        DrawCube(new Vector3(-9f, 2.8f, 0f), new Vector3(0.3f, 5.6f, 18f), new Color(45, 59, 58), 0f);

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

        var boneTransforms = new Matrix[model.Bones.Count];
        if (boneTransforms.Length > 0)
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

        foreach (var mesh in model.Meshes)
        {
            var meshTransform = boneTransforms.Length > mesh.ParentBone.Index
                ? boneTransforms[mesh.ParentBone.Index]
                : Matrix.Identity;

            foreach (var effect in mesh.Effects)
            {
                if (effect is BasicEffect basic)
                {
                    basic.World = meshTransform * world;
                    basic.View = _view;
                    basic.Projection = _projection;
                    basic.EnableDefaultLighting();
                    basic.PreferPerPixelLighting = true;
                    basic.AmbientLightColor = new Vector3(0.32f, 0.36f, 0.4f);
                    basic.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -1f, -0.2f));
                    basic.DirectionalLight0.DiffuseColor = new Vector3(1f, 0.84f, 0.68f);
                    basic.DirectionalLight0.SpecularColor = new Vector3(0.24f);
                    basic.FogEnabled = true;
                    basic.FogStart = 18f;
                    basic.FogEnd = 45f;
                    basic.FogColor = new Color(70, 88, 91).ToVector3();
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

    private void TextRight(string value, float right, float y, float scale, Color color)
    {
        var (font, drawScale) = SelectFont(scale);
        var width = font.MeasureString(value).X * drawScale;
        DrawString(font, value, new Vector2(right - width, y), drawScale, color);
    }

    private (SpriteFont Font, float Scale) SelectFont(float requestedSize)
    {
        if (requestedSize <= 12f)
            return (_smallFont, requestedSize / 18f);

        if (requestedSize >= 19f)
            return (_headingFont, requestedSize / 32f);

        return (_font, requestedSize / 24f);
    }

    private void DrawString(SpriteFont font, string value, Vector2 position, float scale, Color color)
    {
        if (color.A > 20)
        {
            _spriteBatch.DrawString(font, value, position + new Vector2(1f, 1f), new Color(0, 0, 0, 150), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        _spriteBatch.DrawString(font, value, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private bool Pressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}
