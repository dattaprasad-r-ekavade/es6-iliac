using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// Ratna Bay's lifecycle coordinator: screens, the session, draw order.
///
/// Devices, fonts and the canvas live on <c>EngineHost</c>. Overlay-stack selection lives
/// in <c>OverlayInput</c>; world-panel selection in <c>WorldPanelInput</c>; console typing
/// in <c>ConsoleInput</c>. Look and walk live in <c>FirstPersonView</c>. Game rules live in
/// <c>RatnaBay.Domain</c>. A second game subclasses <c>EngineHost</c>, not this type — see
/// <c>Docs/ENGINE.md</c>.
/// </summary>
public sealed class Game1 : EngineHost, IConsoleTarget, ISessionHooks
{
    private readonly ModelCache _modelCache = new();

    /// <summary>Boxes, the crystal and the carved quad, with the two shaders.</summary>
    private SceneRenderer _scene = null!;

    private readonly List<string> _assetErrors = new();

    /// <summary>
    /// Shared by SceneRenderer. Begin takes it each frame so the spike scenes can retune it
    /// for a shot and restore it afterwards.
    /// </summary>
    private BasicEffect _primitiveEffect = null!;

    /// <summary>
    /// The lights affecting the current draw, nearest first.
    ///
    /// The shader takes four. A room with more torches than that is not a lighting problem,
    /// it is a level design problem, and clamping quietly is the right response either way.
    /// </summary>
    private readonly List<PointLight> _lights = new();

    /// <summary>
    /// Consent, title menu, pause and settings: selection, hover and confirm.
    /// Side effects of a confirmed row stay in this class.
    /// </summary>
    private readonly OverlayInput _overlay = new();

    /// <summary>Which world panels are open, and which one owns the frame.</summary>
    private readonly ScreenStack _stack = new();

    /// <summary>The live descent, the yard, and the character walking them.</summary>
    private readonly PlayState _play = new();
    private SessionDirector _director = null!;
    private ContentLoader _content = null!;
    private readonly CombatFeel _feel = new();
    private readonly CombatDirector _combat;

    /// <summary>
    /// Inventory, shop, dialogue, shaft, camp trader, fort and the run-summary button.
    /// Side effects of a confirmed row stay in this class.
    /// </summary>
    private readonly WorldPanelInput _panels = new();

    /// <summary>Typing, history and toggle. Running a line stays in this class.</summary>
    private readonly ConsoleInput _consoleKeys = new();

    /// <summary>
    /// Look, walk, jump and crouch. Collision is a callback so this type does not know about
    /// mines. Ratna Bay's spawn and speeds are set on it; a different game would construct
    /// its own.
    /// </summary>
    private readonly FirstPersonView _camera = new()
    {
        Position = new Vector3(0f, 2.4f, 8.5f),
        Pitch = -0.12f,
        StandingEyeY = 2.4f
    };

    /// <summary>Walks a Ratna Bay manifest onto the engine primitives.</summary>
    private readonly WorldPresenter _worldView = new();

    /// <summary>Speakers, watchers, enemies and bolts onto <see cref="BillboardRenderer"/>.</summary>
    private readonly FigurePresenter _figures = new();

    /// <summary>Moodboard, stambha trailer shot and generated-asset case.</summary>
    private readonly SpikeScenes _spikes = new();

    /// <summary>True while the pointer is captured for looking. Tab releases it.</summary>
    private bool _mouseLook;

    /// <summary>
    /// True when the pointer was freed to operate a panel rather than by the player asking
    /// for it. Closing that panel hands the camera back; pressing Tab does not.
    /// </summary>
    private bool _mouseFreedForPanel;
    private bool _ignoreMouseDeltaThisFrame;

    private GameScreen _screen = GameScreen.MainMenu;
    private string _menuStatus = string.Empty;

    /// <summary>
    /// Metres walked since the last footstep.
    ///
    /// Paced by distance rather than by a timer, which is the only version that stays honest:
    /// a timer keeps stepping when the player walks into a wall, and goes out of step the
    /// moment they sprint. Distance is also free — the collision resolver already reports how
    /// far the body actually moved, which is not the same as how far it tried to.
    /// </summary>
    private float _stride
    {
        get => _feel.Stride;
        set => _feel.Stride = value;
    }
    private bool _crouchToggled;
    private bool _forceCrouch;

    /// <summary>The live character. Null until a game is started or loaded.</summary>
    private GameSession? _session
    {
        get => _play.Session;
        set => _play.Session = value;
    }

    /// <summary>
    /// The developer console, and what it has said.
    ///
    /// Typing is <see cref="ConsoleInput"/>. Running a line, pumping a script and watches
    /// live on <see cref="ConsoleHost"/>. Everything a script can reach is still
    /// <see cref="IConsoleTarget"/>, implemented below.
    /// </summary>
    private readonly ConsoleHost _scripts = new();

    /// <summary>--exec / --script: commands to run once the world exists.</summary>
    private string? _consoleScript;

    private string? _scriptMissing;
    public int ScriptExitCode => _scripts.ScriptExitCode;

    /// <summary>How fast simulated time runs. Set by 'time'; 1 is normal.</summary>
    private float _timeScale = 1f;

    private bool _invulnerable;
    private bool _hideInterface;

    /// <summary>The enemies in the scene and the fight with them.</summary>
    private Encounter? _encounter
    {
        get => _play.Encounter;
        set => _play.Encounter = value;
    }
    private WorldRuntime? _world
    {
        get => _play.World;
        set => _play.World = value;
    }
    private DialogueRuntime? _dialogue
    {
        get => _play.Dialogue;
        set => _play.Dialogue = value;
    }
    private SpeakingActor? _conversationActor;
    private string _dialogueResponse = string.Empty;
    private string _questObjectiveId = string.Empty;
    private WatcherRuntime? _watchers
    {
        get => _play.Watchers;
        set => _play.Watchers = value;
    }
    private Dictionary<string, PickpocketTarget> _pockets => _play.Pockets;
    private Shop? _shop
    {
        get => _play.Shop;
        set => _play.Shop = value;
    }
    private List<WorldPickup> _pickups => _play.Pickups;
    private AmbientAudio? _ambientAudio;

    /// <summary>Every sound effect in the game, synthesised at startup.</summary>
    private SoundBank? _sfx;

    /// <summary>
    /// Seconds of frozen simulation still owed to a landed blow.
    ///
    /// The cheapest and most effective trick in melee combat: on impact, stop the world for
    /// four or five frames. It reads as the blow having mass — the swing meets resistance
    /// instead of passing through — and it does more for how a hit feels than any amount of
    /// particles or numbers. Held to a few frames because past about 120ms it stops reading as
    /// impact and starts reading as the game stuttering.
    /// </summary>
    private float _hitstop
    {
        get => _feel.Hitstop;
        set => _feel.Hitstop = value;
    }

    /// <summary>How far the camera is still owed a shake, and how hard.</summary>
    private float _shake
    {
        get => _feel.Shake;
        set => _feel.Shake = value;
    }
    private float _shakeStrength
    {
        get => _feel.ShakeStrength;
        set => _feel.ShakeStrength = value;
    }

    private BillboardRenderer _billboards = null!;

    /// <summary>The weapon in hand, and the swing it is part-way through.</summary>
    private readonly WeaponView _weaponView = new();
    private UiScreens _screens = null!;

    /// <summary>Set by --faces: write the portrait contact sheet and quit without a frame.</summary>
    private string? _facesPath;

    /// <summary>--face-scale: how far to blow the sheet up. Four is where a brow is arguable.</summary>
    private int _faceSheetScale = 2;

    /// <summary>--face: restrict the sheet to occupants whose room id contains this.</summary>
    private string? _faceOnly;


    /// <summary>Camera angles forced by --yaw / --pitch, for reproducible captures.</summary>
    private float? _startYaw;
    private float? _startPitch;

    /// <summary>
    /// Seconds into a swing to freeze at for --screenshot. Frames are uncapped during a
    /// capture, so the pose is driven directly rather than hoping to catch one mid-flight.
    /// </summary>
    private float? _captureSwing;
    private float? _captureCast;
    private bool _captureApplied;

    /// <summary>--stambha: frame the carved pillar as the trailer's opening shot.</summary>
    private bool _stambhaPreview;

    /// <summary>--yard: start in the surface camp instead of at the menu.</summary>
    private bool _startOnTheSurface;

    /// <summary>
    /// --moodboard: one room built to the target fidelity, to be argued with.
    ///
    /// Deliberately a separate scene rather than a change to the mine. It is a claim about what
    /// the renderer can reach, and a claim should be cheap to reject.
    /// </summary>
    private bool _moodboard;

    /// <summary>--assets: the generated item and creature sprites, over the moodboard room.</summary>
    private bool _assetCase;

    /// <summary>
    /// Bars that have just gone up, and for how much longer they say so.
    ///
    /// Tracked by watching the numbers rather than by listening for an event, so anything that
    /// restores health or prana — a potion, a stone, a heal, whatever is added later — reports
    /// itself without the domain needing to know that a HUD exists.
    /// </summary>
    private float _healthPulse
    {
        get => _feel.HealthPulse;
        set => _feel.HealthPulse = value;
    }
    private float _pranaPulse
    {
        get => _feel.PranaPulse;
        set => _feel.PranaPulse = value;
    }
    private float _lastHealth
    {
        get => _feel.LastHealth;
        set => _feel.LastHealth = value;
    }
    private float _lastPrana
    {
        get => _feel.LastPrana;
        set => _feel.LastPrana = value;
    }

    /// <summary>How long a restored bar stays lit.</summary>
    private const float PulseSeconds = 0.7f;

    /// <summary>What the player did this sitting, for reading back afterwards.</summary>
    private readonly PlayRecorder _recorder = PlayRecorder.Start();

    /// <summary>What a first-time player has been told, and what is still to say.</summary>
    private readonly Coach _coach = Coach.Load();

    /// <summary>Whether this player has agreed to send recordings, and their anonymous name.</summary>
    private readonly TelemetryConsent _consent = TelemetryConsent.Load();

    private TelemetryUploader? _uploader;

    /// <summary>True while the question is on screen, before anything has been sent.</summary>
    private bool _askingConsent;

    /// <summary>True once the camp panel has been shown for the current door.</summary>
    private bool _decisionRecorded
    {
        get => _play.DecisionRecorded;
        set => _play.DecisionRecorded = value;
    }

    /// <summary>The descent in progress, when the loaded world is a mine.</summary>
    private RunRuntime? _run
    {
        get => _play.Run;
        set => _play.Run = value;
    }

    /// <summary>The run that just ended, while its summary is on screen.</summary>
    private RunResult? _runSummary
    {
        get => _play.Summary;
        set => _play.Summary = value;
    }

    /// <summary>What the last death cost, shown beside the run summary.</summary>
    private SuccessionResult? _succession
    {
        get => _play.Succession;
        set => _play.Succession = value;
    }

    /// <summary>
    /// The mine each tier is offering right now, one seed per tier.
    ///
    /// Rolled when the shaft is opened rather than when a tier is confirmed, because the
    /// design requires the cave's element to be shown *before* the player pays: entry already
    /// costs stones, so the information belongs at the point of payment. A seed picked after
    /// the money changed hands cannot be previewed, and a preview of a different mine than the
    /// one you get is worse than none.
    /// </summary>
    private readonly int[] _shaftSeeds = new int[MineEntry.MaxTier + 1];

    /// <summary>The cave currently being descended, or null on the surface.</summary>
    private CaveTheme? _cave
    {
        get => _play.Cave;
        set => _play.Cave = value;
    }

    /// <summary>
    /// Tell the caster which cave it is standing in.
    ///
    /// Called after the session exists rather than alongside the derivation, because starting
    /// a new character replaces the session — and the assignment made before that replacement
    /// went to the object that was about to be thrown away.
    /// </summary>
    private void ApplyCave() => _director.ApplyCave();

    /// <summary>
    /// True while walking back into a descent that was set aside.
    ///
    /// The one case where a mine's dead must stay dead: the rooms already cleared before the
    /// game was put down are still cleared when it is picked up.
    /// </summary>
    private bool _resumingDescent
    {
        get => _play.ResumingDescent;
        set => _play.ResumingDescent = value;
    }

    /// <summary>
    /// Rooms built per segment of mine.
    ///
    /// Small enough that the first one arrives quickly and the work of building the next lands
    /// while the player is busy, large enough that a join is rare.
    /// </summary>
    private const int RoomsPerSegment = 8;

    /// <summary>Seconds left on a click that arrived before the last swing had finished.</summary>
    private float _swingBuffered
    {
        get => _feel.SwingBuffered;
        set => _feel.SwingBuffered = value;
    }

    /// <summary>
    /// How long a click is remembered while the weapon is still swinging.
    ///
    /// It was a flat 0.22s, which matches no weapon: a sword's cooldown is 0.45 and a mace's
    /// is 0.72, so the window reached less than half of one and less than a third of the
    /// other. The recordings show what that cost -- 660 refused clicks across 76 sessions, all
    /// of them cooldown and not one of them stamina, and of the ones the buffer failed to
    /// catch, 80% arrived too early for a 0.22s window to reach the swing. The median lost
    /// click came 0.19s after the last one: a player pressing at about two and a half a
    /// second against a weapon that allows two and a bit.
    ///
    /// Held for the whole of whatever the weapon's own cooldown is, so a click during a swing
    /// is never dropped. Still one click, not a queue -- mashing five times buys one swing,
    /// which is the part that should stay true.
    /// </summary>
    private float SwingBufferSeconds =>
        _session?.Player.Combat.ActiveWeapon.Cooldown ?? 0.45f;

    /// <summary>Whether a panel was open on the previous frame.</summary>
    private bool _panelWasOpen;

    /// <summary>Seconds until the next stance sample is written down.</summary>
    private float _stanceCountdown;

    /// <summary>How often the recorder notes where the player is standing.</summary>
    private const float StanceSampleSeconds = 1f;

    /// <summary>Northwatch's safe surface checkpoint between descents.</summary>
    private static WorldPoint SurfaceCheckpoint => SessionDirector.SurfaceCheckpoint;

    /// <summary>--mine N: play a generated mine instead of the authored world.</summary>
    private int? _mineSeed
    {
        get => _play.MineSeed;
        set => _play.MineSeed = value;
    }
    private int _mineRooms
    {
        get => _play.MineRooms;
        set => _play.MineRooms = value;
    }
    private int _mineDepth
    {
        get => _play.MineDepth;
        set => _play.MineDepth = value;
    }

    /// <summary>Screen to force open for --screenshot: inventory, journal, shop or help.</summary>
    private string? _captureScreen;

    /// <summary>
    /// Seconds since the game started, for anything that moves on its own.
    ///
    /// Deliberately not <c>gameTime.TotalGameTime</c>: a screenshot run advances a fixed number
    /// of frames rather than real time, and a capture has to be reproducible. Accumulating the
    /// same step the rest of the simulation uses keeps <c>--screenshot</c> deterministic.
    /// </summary>
    private float _clock;

    public Game1(string[] args) : base(args, UiLayout.Width, UiLayout.Height, "Ratna Bay")
    {
        var launch = LaunchOptions.Parse(args, _capture.CoverMode, ParseOption, HasArgument);
        _screen = launch.Screen;
        _facesPath = launch.FacesPath;
        _faceOnly = launch.FaceOnly;
        _faceSheetScale = launch.FaceSheetScale;
        _forceCrouch = launch.ForceCrouch;
        _captureSwing = launch.CaptureSwing;
        _captureCast = launch.CaptureCast;
        _captureScreen = launch.CaptureScreen;
        _stambhaPreview = launch.StambhaPreview;
        _consoleScript = launch.ConsoleScript;
        _scriptMissing = launch.ScriptMissing;
        _startOnTheSurface = launch.StartOnTheSurface;
        _moodboard = launch.Moodboard;
        _assetCase = launch.AssetCase;
        if (launch.MineSeed is { } seed) _mineSeed = seed;
        if (launch.MineRooms is { } rooms) _mineRooms = rooms;
        if (launch.MineDepth is { } depth) _mineDepth = depth;
        _startYaw = launch.StartYaw;
        _startPitch = launch.StartPitch;

        _director = new SessionDirector(_play, this);
        _content = new ContentLoader(_play, _assetErrors);
        _combat = new CombatDirector(_feel);
    }

    protected override void Initialize()
    {
        _camera.SetProjection(GraphicsDevice.Viewport.AspectRatio);

        _scene = new SceneRenderer(GraphicsDevice);

        // Read before anything is loaded, so the first menu already knows whether there is a
        // descent waiting rather than a town.
        _suspendedDescentOnDisk = GameSession.PeekHasSuspendedDescent();

        // Asked once, before a single byte leaves the machine. A game that uploads by default
        // and mentions it in a settings menu is disclosed in the technical sense and not in
        // any other, and the tester who finds out afterwards is right to be annoyed.
        _uploader = new TelemetryUploader(_consent);

        // Never asked during a capture.
        //
        // The question owns the screen until it is answered, so on any machine that has not
        // answered it yet — a fresh clone, a build agent, a contributor's first run — every
        // --screenshot and every --cover came out as a picture of the consent dialog. The
        // store assets the board is waiting on were being generated wrong and looked fine
        // until somebody opened the file.
        //
        // Suppressing it is also correct rather than merely convenient: a capture run renders
        // a few frames and exits, nobody is playing, and there is no session to consent to
        // sending. Nothing is uploaded either way, because SendPending is not reached.
        // --cover already funnels into CaptureHost.OutputPath, so this covers both.
        var capturing = _capture.IsCapturing;

        _askingConsent = !capturing
            && !_consent.Asked
            && !string.IsNullOrWhiteSpace(Telemetry.Endpoint);

        if (!_askingConsent && !capturing) _uploader.SendPending();

        // Launching straight into the scene (--mode scene, screenshots, playtests) needs a
        // character and a data-authored room, or the HUD has nothing to show.
        if (_screen == GameScreen.WorldScene)
        {
            // The launch flags set _mineSeed and _mineDepth directly rather than going through
            // EnterWorld, so the cave has to be derived here too. Without it every --mine
            // capture came out in the default granite whatever seed was asked for, which is
            // exactly the kind of thing a screenshot is supposed to prove.
            _cave = _mineSeed is { } launchSeed
                ? CaveThemeCatalog.For(launchSeed, _mineDepth)
                : null;

            LoadWorldManifest();
            ResetCamera();
            StartSession(GameSession.NewGame());
            ApplyCave();
        }

        if (_startYaw is { } forcedYaw) _camera.Yaw = forcedYaw;
        if (_startPitch is { } forcedPitch) _camera.Pitch = forcedPitch;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // Before anything else loads: this needs a graphics device and nothing more, and the
        // point of it is to be runnable on a machine where the rest of the game will not start.
        if (_facesPath is not null)
        {
            FaceSheet.Write(GraphicsDevice, _facesPath, _faceOnly, _faceSheetScale);
            Exit();
            return;
        }

        var fontsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Feasibility",
            "Fonts");

        _billboards = new BillboardRenderer(GraphicsDevice);
        AttachCanvas(
            Path.Combine(fontsDirectory, "NotoSans", "NotoSans-wght.ttf"),
            Path.Combine(fontsDirectory, "Cinzel", "Cinzel-wght.ttf"));
        _screens = new UiScreens(_ui, GraphicsDevice);

        // Devanagari for the carved verses. Absent, the pillar simply stands blank.
        StambhaCarving.Load(fontsDirectory);

        if (_scene.LoadCaveShader(Content, "Effects/CaveLighting") is { } shaderFault)
            _assetErrors.Add($"cave lighting: {shaderFault}");

        if (!AmbientAudio.TryStart(out _ambientAudio, out var ambientError)
            && !string.IsNullOrWhiteSpace(ambientError))
            _assetErrors.Add(ambientError);

        _sfx = SoundBank.Create(out var sfxError);
        if (!string.IsNullOrWhiteSpace(sfxError)) _assetErrors.Add(sfxError);

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

        _modelCache.Load(Content, "bridge", "Feasibility/Models/Kenney/bridge_wood");
        _modelCache.Load(Content, "campfire", "Feasibility/Models/Kenney/campfire_stones");
        _modelCache.Load(Content, "ground", "Feasibility/Models/Kenney/ground_grass");
        _modelCache.Load(Content, "bush", "Feasibility/Models/Kenney/plant_bushLarge");
        _modelCache.Load(Content, "rock", "Feasibility/Models/Kenney/rock_largeA");
        _modelCache.Load(Content, "tent", "Feasibility/Models/Kenney/tent_detailedOpen");
        _modelCache.Load(Content, "tree", "Feasibility/Models/Kenney/tree_pineRoundA");
        _modelCache.Load(Content, "cheeseBox", "Feasibility/Models/PolyHavenCheeseBox/CheeseBox_01_1k");

        // Done here rather than at parse time: the surface needs the device, the fonts and
        // the models, and none of those exist when the command line is read.
        if (_startOnTheSurface) EnterWorld(null, newCharacter: true);

        // --exec runs against a world that now exists, and before the first frame is drawn,
        // so a capture taken after it sees where the commands put the player.
        if (_scriptMissing is not null)
        {
            FailScript($"No script file '{_scriptMissing}'.");
            _scripts.QuitWhenDone = true;
            return;
        }

        if (_consoleScript is null) return;

        var statements = ConsoleRouter.SplitStatements(_consoleScript);

        // Checked as a whole before the first one runs. A script that names a command nothing
        // registered is a script that was written against a different build, and finding that
        // out at statement forty means the thirty-nine asserts before it already reported
        // success on a run that was never going to finish.
        _scripts.Router ??= GameConsole.Build(this);
        var unknown = _scripts.Router.UnknownCommands(statements);
        if (unknown.Count > 0)
        {
            FailScript($"Unknown command(s): {string.Join(", ", unknown)}. Try 'help'.");
            _scripts.QuitWhenDone = true;
            return;
        }

        foreach (var statement in statements)
            _scripts.Queue.Enqueue(statement);
    }

    protected override void UnloadContent()
    {
        // --faces returns out of LoadContent before any of this exists, so there is nothing
        // here to release and every line below would throw on a null.
        DisposeHost();
        if (_facesPath is not null) return;

        _primitiveEffect.Dispose();
        _billboards.Dispose();
        StoneTextures.Clear();
        PropTextures.Clear();
        ItemSprites.Clear();
        PortraitForge.Clear();
        // A sitting that ends by closing the window is still a sitting worth reading back.
        _recorder.Flush();

        _ambientAudio?.Dispose();
        _sfx?.Dispose();
        CharacterSprites.Clear();
        WeaponSprites.Clear();
        BoltSprites.Clear();
        StambhaCarving.Clear();
        base.UnloadContent();
    }

    /// <summary>
    /// Simulated seconds this frame: zero while a hit is landing, scaled by the console's 'time'.
    ///
    /// Everything that moves reads this rather than the clock, so hitstop freezes the world
    /// without any of it knowing why. The input layer deliberately does not: a player who
    /// presses a key during those four frames should still be heard, or the freeze reads as
    /// dropped input rather than as impact.
    ///
    /// The time scale is what makes a fight watchable — at 0.2 a swing can be followed through,
    /// and at 0 the frame holds still while the camera is moved. Real time is untouched, so
    /// fire keeps flickering and the console stays responsive.
    /// </summary>
    private float StepSeconds(GameTime gameTime) =>
        _hitstop > 0f ? 0f : RealSeconds(gameTime) * _timeScale;

    protected override void Update(GameTime gameTime)
    {
        _input.Sample();
        var keyboard = _input.CurrentKeyboard;
        var mouse = _input.CurrentMouse;

        // On real time, not simulation time: the fire has to keep moving during a hitstop or
        // the freeze looks like the game locked up rather than like a blow landing.
        var real = RealSeconds(gameTime);
        _clock += real;
        _feel.TickRealTime(real);

        PumpScript(RealSeconds(gameTime) * _timeScale);
        UpdateWatches();

        // First, and it swallows the frame when it is open: a console you cannot type an S
        // into without walking backwards is not a console.
        UpdateConsole(keyboard);
        if (_consoleKeys.Open)
        {
            _input.Commit();
            return;
        }

        if (Pressed(keyboard, Keys.F11))
            SetBorderlessFullscreen(!_borderlessFullscreen);

        if (Pressed(keyboard, Keys.Escape))
        {
            if (_screen == GameScreen.MainMenu)
            {
                if (_overlay.ShowSettings) _overlay.ShowSettings = false;
            }
            else if (_stack.Paused)
            {
                ResumeFromPause();
            }
            else if (_stack.Shaft)
            {
                _stack.Shaft = false;
                SetMouseLook(true);
            }
            else if (_runSummary is not null)
            {
                ReturnToTheSurface();
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

        // Before the screen dispatch, not inside it. An open inventory returns out of
        // UpdateGameScreen long before the session is ticked, so a panel tracker living down
        // there would never see the panel that was open — and the correction it exists to
        // make would quietly be worth nothing.
        TrackPanelTime();

        // Nothing else is reachable until the question is answered. It is two keys and it is
        // asked exactly once.
        if (_askingConsent)
        {
            UpdateConsent(keyboard, mouse);
            _input.CommitMouse();
            return;
        }

        if (_screen == GameScreen.MainMenu)
            UpdateMenu(keyboard, mouse);
        else
            UpdateGameScreen(gameTime, keyboard, mouse);

        _input.Commit();
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
        if (_capture.IsCapturing) enabled = false;

        if (enabled) _mouseFreedForPanel = false;
        else if (forPanel) _mouseFreedForPanel = true;

        _mouseLook = enabled;
        // The system cursor stays hidden in every state; OverlayRenderer.DrawPointer draws ours.
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

    /// <summary>
    /// The console owns the keyboard while it is open.
    ///
    /// Typing is read from key transitions rather than a text-input event, because MonoGame's
    /// TextInput is not wired here and the rest of the game samples keys through InputRouter.
    /// </summary>
    private void UpdateConsole(KeyboardState keyboard)
    {
        switch (_consoleKeys.Step(_input, keyboard))
        {
            case ConsoleAction.Toggle:
                SetMouseLook(!_consoleKeys.Open, forPanel: true);
                break;
            case ConsoleAction.Close:
                SetMouseLook(true);
                break;
            case ConsoleAction.Submit:
                RunConsole(_consoleKeys.Buffer);
                _consoleKeys.Clear();
                break;
            case ConsoleAction.Complete:
                // Completing the command word only. Arguments differ per command and guessing
                // at them would be worse than not offering.
                var candidates = _scripts.Router?.Complete(_consoleKeys.Buffer) ?? new List<string>();
                if (candidates.Count == 1) _consoleKeys.Buffer = candidates[0] + " ";
                else if (candidates.Count > 1)
                    _scripts.Output.Add(new ConsoleLine(string.Join("  ", candidates), ConsoleTone.Info));
                break;
            case ConsoleAction.HistoryUp:
                _consoleKeys.WalkHistory(_scripts.Router?.History, -1);
                break;
            case ConsoleAction.HistoryDown:
                _consoleKeys.WalkHistory(_scripts.Router?.History, 1);
                break;
        }
    }

    /// <summary>
    /// Run one queued statement per frame, so the game keeps simulating between them.
    ///
    /// One per frame rather than all at once is the whole point: a script that descends and
    /// then asks what is in the room has to let the room notice it has been walked into.
    /// </summary>
    private void PumpScript(float simulatedSeconds)
    {
        _scripts.Pump(simulatedSeconds, this, out var exit);
        if (exit) Exit();
    }

    private void UpdateWatches() => _scripts.RefreshWatches();

    private void DrawWatches() => _screens.Console.DrawWatches(_scripts.WatchOutput);

    private void RunConsole(string line) => _scripts.Run(line, this);

    private bool Clicked(MouseState mouse) => _input.Clicked(mouse);

    protected override void Draw(GameTime gameTime)
    {
        ApplyCaptureScreen();
        BeginHostFrame();

        GraphicsDevice.Clear(new Color(9, 15, 25));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _ui.Resize(GraphicsDevice.Viewport, _uiScalePreference);
        UpdateCameraMatrices();

        // The one point per frame where the camera is settled and nothing has drawn yet. The
        // scene renderer takes its whole per-frame context here rather than as six arguments
        // on each of forty draw calls, which is how one of them ends up passing a stale view.
        _scene.Begin(_primitiveEffect, _camera.View, _camera.Projection, _camera.Position,
            _camera.Yaw, _worldView.Stone, _lights);

        // The question owns the screen until it is answered.
        FramePresenter.Present(_askingConsent, _screen == GameScreen.WorldScene,
            drawConsent: () => { _ui.Begin(); DrawConsent(); EndUi(); },
            drawMenu: DrawMenu,
            drawWorld: DrawWorldScene);

        base.Draw(gameTime);

        EndHostFrame(
            hold: _scripts.Queue.Count > 0 || _scripts.WaitSeconds > 0f,
            exit: Exit);
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

    private void UpdateConsent(KeyboardState keyboard, MouseState mouse)
    {
        var chosen = _overlay.StepConsent(_input, keyboard, mouse, LogicalMouse(mouse));
        if (chosen < 0) return;

        _consent.Asked = true;
        _consent.Allowed = chosen == 0;
        _consent.Save();

        _askingConsent = false;
        if (_consent.Allowed) _uploader?.SendPending();
    }

    /// <summary>
    /// The question, in the words it deserves.
    ///
    /// Short enough to be read rather than dismissed, specific about what is sent, and honest
    /// that the answer changes nothing about the game. "Yes" is not the default and is not
    /// styled to look like the safe one.
    /// </summary>
    private void DrawConsent() => _screens.Consent.Draw(_overlay.ConsentSelection);

    private void UpdateMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (_overlay.ShowSettings)
        {
            ApplySettings(_overlay.StepSettings(_input, keyboard, LogicalMouse(mouse)));
            return;
        }

        var activated = _overlay.StepMenu(_input, keyboard, mouse, LogicalMouse(mouse),
            MenuItems.Length, out var moved);
        if (moved) _menuStatus = string.Empty;
        if (activated) ActivateMenuItem();
    }

    /// <summary>What the telemetry row says, including what is still waiting to go.</summary>
    private string SettingsTelemetryLine()
    {
        if (string.IsNullOrWhiteSpace(Telemetry.Endpoint))
            return "Send recordings   not available in this build";

        var waiting = _uploader?.PendingCount() ?? 0;
        var queued = _consent.Allowed && waiting > 0 ? $"  ({waiting} waiting)" : string.Empty;

        return $"Send recordings   {(_consent.Allowed ? "On" : "Off")}{queued}";
    }

    /// <summary>Apply what the settings panel asked for. Display, scale and volume live here.</summary>
    private void ApplySettings(SettingsCommand command)
    {
        switch (command.Action)
        {
            case SettingsAction.ToggleDisplay:
                SetBorderlessFullscreen(!_borderlessFullscreen);
                break;

            case SettingsAction.NudgeScale:
                _uiScalePreference = MathHelper.Clamp(
                    _uiScalePreference + command.Nudge * 0.1f, 0.8f, 1.2f);
                _ui.Resize(GraphicsDevice.Viewport, _uiScalePreference);
                break;

            case SettingsAction.NudgeVolume:
                if (_sfx is null) return;
                _sfx.Volume = MathHelper.Clamp(_sfx.Volume + command.Nudge * 0.1f, 0f, 1f);

                // Play the thing being adjusted, so the number is not the only feedback. A volume
                // slider that makes no sound is guesswork.
                _sfx.Play(Sfx.Coin, 0.5f);
                break;
        }
    }

    /// <summary>
    /// Walk back into the mine that was put down.
    ///
    /// The mine is rebuilt from its seed rather than stored, the ledger is adopted by the run
    /// that is already wired to the HUD and the recorder, and the descent is then struck off
    /// the save — so resuming is a way to stop playing and come back, never a way to reload a
    /// fight that went badly.
    /// </summary>
    private void ResumeSuspendedDescent() => _director.ResumeSuspendedDescent();

    /// <summary>Go down, at a depth that has been paid for.</summary>
    private void EnterMine(int seed, int tier) => _director.EnterMine(seed, tier);

    /// <summary>Come back up. The run is over either way by the time this is called.</summary>
    private void ReturnToTheSurface() => _director.ReturnToTheSurface();

    /// <summary>
    /// Drop into a world, generated or authored.
    ///
    /// Both paths go through here because the world has to be discarded and rebuilt when the
    /// kind of world changes. Leaving the old one in place is how "Start New Game" after a
    /// descent used to hand back the mine you had just left.
    /// </summary>
    private void EnterWorld(int? mineSeed, bool newCharacter = false, int tier = 1) =>
        _director.EnterWorld(mineSeed, newCharacter, tier);

    private void ActivateMenuItem()
    {
        switch (MenuItems[_overlay.MenuSelection])
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
                _overlay.OpenSettings();
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

                _stack.Shop = true;
                _panels.ShopSelection = 0;
                SetMouseLook(false, forPanel: true);
                break;

            case SurfaceFixture.Stambha:
                _session.ShowToast("मा गृधः कस्य स्विद्धनम्  —  covet not; for whose is wealth?");
                break;
        }
    }

    /// <summary>
    /// The trader at a cleared room's exit.
    ///
    /// Everything here is priced in the pot, because down a mine the pot is the purse: what is
    /// spent was never carried out, and what is sold is at risk exactly like the rest. That is
    /// what stops a camp being a way to bank early.
    /// </summary>
    private void UpdateCampTrader(KeyboardState keyboard, MouseState mouse)
    {
        var cmd = _panels.StepCampCommand(_input, keyboard, mouse, LogicalMouse(mouse),
            CampRowCount(), _session is not null && _run is not null);
        switch (cmd.Action)
        {
            case CampAction.Dismiss:
                _stack.CampTrader = false;
                SetMouseLook(true);
                return;
            case CampAction.SellLoot:
                ApplyCampSell();
                return;
            case CampAction.BuyStock:
                ApplyCampBuy(cmd.StockIndex);
                return;
        }
    }

    private void ApplyCampSell()
    {
        if (_session is null || _run is null) return;

        var run = _run.Run;
        var paid = CampTrader.SellLoot(_session.Player.Inventory, run);
        _session.ShowToast(paid > 0
            ? $"They take the lot. +{paid} stones, and the pot is {run.Pending}."
            : "Nothing in your pack they want.");

        if (paid > 0)
            _recorder.Record(PlayEventKind.LootSold, "loot", paid, run.Pending,
                _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
    }

    private void ApplyCampBuy(int stockIndex)
    {
        if (_session is null || _run is null) return;
        if (stockIndex < 0 || stockIndex >= CampTrader.Stock.Count) return;

        var run = _run.Run;
        var good = CampTrader.Stock[stockIndex];
        if (!run.TrySpend(good.Stones))
        {
            _session.ShowToast($"{good.Name} wants {good.Stones} stones. The pot holds {run.Pending}.");
            return;
        }

        _session.Player.Inventory.Add(good.ItemId, good.Name, good.Count, good.Kind);
        _recorder.Record(PlayEventKind.ItemBought, good.Name, good.Stones, run.Pending,
            _session.Player.Vitals.Health, _session.Player.Vitals.Prana, "camp");

        _session.ShowToast($"{good.Name}. {run.Pending} stones left in the pot.");
    }

    private void ApplyCampOut(RunRuntime decision)
    {
        if (_session is null) return;

        var result = decision.Camp();
        _recorder.Record(PlayEventKind.Camped, $"after {result.RoomsCleared} rooms",
            result.StonesCarriedOut, 0f, _session.Player.Vitals.Health);
        EndRun(result);
    }

    private void ApplyCallTrader(RunRuntime decision)
    {
        if (_session is null) return;

        var fare = decision.Run.TraderCallCost;
        if (!decision.Run.TrySpend(fare)) return;

        decision.Run.NoteTraderCalled();
        _stack.CampTrader = true;
        _panels.CampSelection = 0;
        SetMouseLook(false, forPanel: true);

        _recorder.Record(PlayEventKind.TraderCalled,
            $"call {decision.Run.TradersCalled}", fare, decision.Run.Pending,
            _session.Player.Vitals.Health, _session.Player.Vitals.Prana);

        _session.ShowToast($"{fare} stones, and somebody comes down the ladder.");
    }

    private void ApplyPressOn(RunRuntime decision)
    {
        if (_session is null) return;

        _recorder.Record(PlayEventKind.PressedOn,
            $"into room {decision.Run.RoomsCleared + 1}",
            decision.Run.Pending, decision.Run.NextRoomPays,
            _session.Player.Vitals.Health);

        decision.PressOn(_world!, _session.Player);
        _session.ShowToast("The door swings in. No going back.");
    }

    /// <summary>Sell everything, then one row for each thing on offer.</summary>
    private static int CampRowCount() => 1 + CampTrader.Stock.Count;

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

        _stack.Shaft = true;
        _panels.DepthSelection = Math.Clamp(
            MineEntry.DeepestAffordable(_session.Player.Inventory), 1, MineEntry.MaxTier);

        // A fresh set of offers each time the shaft is opened. Backing out and looking again
        // re-rolls them, which is deliberate: it is free, it costs no stones, and a player who
        // does not like any of today's caves should be able to come back tomorrow.
        var roll = new Random(Environment.TickCount);
        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
            _shaftSeeds[tier] = roll.Next(int.MinValue, int.MaxValue);

        SetMouseLook(false, forPanel: true);
    }

    private void UpdateDepthChoice(KeyboardState keyboard, MouseState mouse)
    {
        switch (_panels.StepShaftCommand(_input, keyboard, mouse, LogicalMouse(mouse),
            _session is not null))
        {
            case ShaftAction.Dismiss:
                _stack.Shaft = false;
                SetMouseLook(true);
                return;
            case ShaftAction.Commit:
                ApplyShaftCommit();
                return;
        }
    }

    private void ApplyShaftCommit()
    {
        if (_session is null) return;

        var cost = MineEntry.CostOf(_panels.DepthSelection);
        if (!MineEntry.TryOpen(_session.Player.Inventory, _panels.DepthSelection))
        {
            _session.ShowToast($"That door wants {cost} stones. You have "
                + $"{_session.Player.Inventory.CountOf(SoulCrystals.LesserId)}.");
            return;
        }

        _stack.Shaft = false;

        // Back to the mine that killed the last one, if there is a body in it. A fresh random
        // mine would put the cache somewhere unreachable by design, and a loss you are never
        // given the chance to answer is only a loss.
        var fallen = _session.Player.Legacy.Fallen;
        var returning = fallen is not null && fallen.Tier == _panels.DepthSelection;

        EnterMine(returning ? fallen!.MineSeed : _shaftSeeds[_panels.DepthSelection], _panels.DepthSelection);

        _session.ShowToast(returning
            ? $"The same shaft. {fallen!.Name} is still down there, in room {fallen.RoomIndex}."
            : cost > 0
                ? $"{cost} stones, and the shaft opens. Tier {_panels.DepthSelection}."
                : "The picked-over workings. They cost nothing and pay like it.");
    }

    /// <summary>What the pause screen offers, which depends on whether a run is underway.</summary>
    private string[] PauseItems => _run is { Run.IsActive: true }
        ? new[] { "Resume", "Settings", "Set the descent aside", "Give up the descent" }
        : new[] { "Resume", "Settings", "Save and quit to menu" };

    private void Pause()
    {
        if (_stack.Paused) return;

        ClosePanels();
        _stack.Paused = true;
        _overlay.PauseSelection = 0;
        SetMouseLook(false, forPanel: true);
    }

    private void ResumeFromPause()
    {
        _stack.Paused = false;
        _overlay.ShowSettings = false;
        if (_screen == GameScreen.WorldScene) SetMouseLook(true);
    }

    private void UpdatePause(KeyboardState keyboard, MouseState mouse)
    {
        if (_overlay.ShowSettings)
        {
            ApplySettings(_overlay.StepSettings(_input, keyboard, LogicalMouse(mouse)));
            return;
        }

        var items = PauseItems;
        var inRun = _run is { Run.IsActive: true };
        switch (_overlay.StepPause(_input, keyboard, mouse, LogicalMouse(mouse), items, inRun))
        {
            case PauseAction.Resume:
                ResumeFromPause();
                break;

            case PauseAction.Settings:
                _overlay.OpenSettings();
                break;

            case PauseAction.Suspend:
                SuspendDescent();
                break;

            case PauseAction.Abandon:
                AbandonDescent();
                break;

            case PauseAction.Quit:
                _session?.ShowToast(_session.Save());
                LeaveToMenu();
                break;
        }
    }

    /// <summary>Put the run down mid-descent, to be walked back into later.</summary>
    private void SuspendDescent() => _director.SuspendDescent();

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
    private void AbandonDescent() => _director.AbandonDescent();

    private void LeaveToMenu()
    {
        _stack.Paused = false;
        _overlay.ShowSettings = false;
        SetMouseLook(false);
        _screen = GameScreen.MainMenu;
        _overlay.MenuSelection = 0;
    }

    /// <summary>
    /// Open what a key asked for, or close it if it is already open.
    ///
    /// The seven bindings each used to repeat the same three steps — close if open, otherwise
    /// open and give the pointer to the panel — and a binding that forgot the third step left
    /// the player turning the camera with a menu in front of them. Once, here, so a new panel
    /// cannot be added with that step missing.
    /// </summary>
    private void OpenPanel(PanelRequest request)
    {
        if (request == PanelRequest.None) return;

        if (request == PanelRequest.ToggleMouseLook)
        {
            SetMouseLook(!_mouseLook);
            return;
        }

        if (request == PanelRequest.Settings)
        {
            _overlay.ShowSettings = !_overlay.ShowSettings;
            if (_overlay.ShowSettings) SetMouseLook(false, forPanel: true);
            return;
        }

        var alreadyOpen = request switch
        {
            PanelRequest.Help => _stack.Help,
            PanelRequest.Fort => _stack.Fort,
            PanelRequest.Journal => _stack.Journal,
            PanelRequest.Character => _stack.Character,
            _ => false
        };

        if (alreadyOpen)
        {
            ClosePanels();
            return;
        }

        switch (request)
        {
            case PanelRequest.Help: _stack.Help = true; break;
            case PanelRequest.Fort: _stack.OpenFort(); _panels.FortSelection = 0; break;
            case PanelRequest.Journal: _stack.OpenJournal(); break;
            case PanelRequest.Character: _stack.OpenCharacter(); _panels.InventorySelection = 0; break;
        }

        SetMouseLook(false, forPanel: true);
    }

    private void UpdateGameScreen(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        // M was a second silent way out of a run. It opens the same pause screen now.
        if (Pressed(keyboard, Keys.M)) Pause();

        // Pause first, then F1, then the rest of EarlyHold. F1 used to fire on the frame
        // between pause returning and the summary taking the screen; keep that order.
        if (_stack.EarlyHold(_runSummary is not null) == WorldHold.Pause)
        {
            UpdatePause(keyboard, mouse);
            return;
        }

        // Help first, before the early holds, for the reason above.
        OpenPanel(PanelKeys.ReadHelp(_input, keyboard));

        // A screen with no way out but a function key is a screen some players will be stuck
        // on. Anywhere on the controls overlay closes it.
        if (_stack.ClickClosesHelp(_input, mouse)) ClosePanels();

        switch (_stack.EarlyHold(_runSummary is not null))
        {
            case WorldHold.Summary:
                if (_panels.StepSummary(_input, keyboard, mouse, LogicalMouse(mouse))
                    || Pressed(keyboard, Keys.Escape))
                {
                    // Up into the yard rather than out to a menu. A loop that ends at a title
                    // screen is not a loop; the whole point of the surface is having somewhere
                    // to arrive with what you carried out.
                    ReturnToTheSurface();
                }

                return;
            case WorldHold.CampTrader:
                UpdateCampTrader(keyboard, mouse);
                return;
            case WorldHold.Shaft:
                UpdateDepthChoice(keyboard, mouse);
                return;
        }

        if (_run is { AtDecision: true } decision && _session is not null)
        {
            // The one decision the whole game is built on, explained the first time it is
            // actually in front of somebody with stones in the pot.
            _coach.Teach(Lessons.FirstDoor, Lessons.TextOf(Lessons.FirstDoor));
            if (decision.Run.CanCallTrader)
                _coach.Teach(Lessons.Trader, Lessons.TextOf(Lessons.Trader));

            // The clock on the answer starts the first frame the panel is up.
            if (!_decisionRecorded)
            {
                _decisionRecorded = true;
                _recorder.Record(PlayEventKind.DecisionOffered,
                    $"after {decision.Run.RoomsCleared} rooms",
                    decision.Run.Pending, decision.Run.NextRoomPays,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
            }

            switch (WorldPanelInput.StepDoor(_input, keyboard,
                decision.Run.CanCallTrader, decision.Run.CanPressOn))
            {
                case DoorAction.Camp:
                    ApplyCampOut(decision);
                    return;
                case DoorAction.CallTrader:
                    ApplyCallTrader(decision);
                    return;
                case DoorAction.PressOn:
                    ApplyPressOn(decision);
                    return;
            }
        }
        else if (_run is not null)
        {
            _decisionRecorded = false;
        }

        // The rest of the panel keys, after the early holds have had their say. A panel that
        // owns the screen must be able to swallow them.
        OpenPanel(PanelKeys.Read(_input, keyboard, OnTheSurface));

        switch (_stack.LateHold(_overlay))
        {
            case WorldHold.Fort:
                UpdateFort(keyboard, mouse);
                return;
            case WorldHold.Character:
                UpdateInventory(keyboard);
                return;
        }

        if (_stack.LateHold(_overlay) == WorldHold.Settings)
        {
            ApplySettings(_overlay.StepSettings(_input, keyboard, LogicalMouse(mouse)));
            return;
        }

        if (_screen == GameScreen.WorldScene)
            UpdateCrouchToggle(keyboard);

        // A released pointer can click the active talk/shop/pickup prompt. A click anywhere
        // else returns to mouse-look; this prevents a UI click from becoming an attack.
        if (!_mouseLook && !_stack.Help && !_stack.Dialogue && !_stack.Shop && !_stack.Character
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
        if (!_stack.AnyOpen(_overlay, _runSummary is not null))
            UpdateCamera(gameTime, keyboard, mouse);

        if (_screen == GameScreen.WorldScene)
            UpdateSession(gameTime, keyboard, mouse);

        RestoreMouseLookAfterPanels();
    }

    /// <summary>
    /// True while any screen is holding the pointer.
    ///
    /// One list, and everything that frees the mouse must be on it. The shaft panel was not,
    /// so the frame after it opened the camera took the pointer straight back: the panel was
    /// on screen, the clicks went nowhere, and it looked as though the mouse support had
    /// simply not been written. Adding a screen and forgetting this line is the bug, so the
    /// list lives in exactly one place and both the Escape key and the camera read it.
    /// </summary>
    private bool AnyPanelOpen => _stack.AnyOpen(_overlay, _runSummary is not null);

    /// <summary>
    /// Close everything and give the camera straight back.
    ///
    /// Every panel used to close itself in its own way, and dialogue closed itself in two
    /// different places, so whether the camera came back depended on which path you took out.
    /// One exit means one behaviour: the pointer goes, the camera moves, no click needed.
    ///
    /// Conversation text is Game1's payload, not the stack's: Close clears the flags, then
    /// this drops the actor so the next open is a fresh talk.
    /// </summary>
    private void ClosePanels()
    {
        _stack.Close(_overlay);
        _conversationActor = null;
        _dialogueResponse = string.Empty;
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
        if (AnyPanelOpen) return;

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
        _session.Position = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);
        _session.Yaw = _camera.Yaw;
        _session.Pitch = _camera.Pitch;

        var step = StepSeconds(gameTime);
        _session.Player.Detection.SetCrouching(_camera.Crouching);
        _watchers?.Update(step, _session.Position);
        _session.Tick(step);

        if (_stack.Journal || _stack.Character)
        {
            return;
        }

        if (_stack.Shop)
        {
            UpdateShopInput(keyboard, mouse);
            return;
        }

        if (_stack.Dialogue)
        {
            UpdateDialogueInput(keyboard, mouse);
            return;
        }

        // Sprinting is the only thing that spends stamina yet, so it is what proves the
        // vitals on screen are the domain's numbers rather than painted ones.
        if (keyboard.IsKeyDown(Keys.LeftShift) && IsMoving(keyboard))
            _session.Player.Vitals.SpendStamina(18f * StepSeconds(gameTime));

        var cmd = SessionInput.Step(_input, keyboard, _session.Position, _camera.Yaw,
            _run is { Run.IsActive: true }, _dialogue, _world, OnTheSurface, _pickups);
        ApplySession(cmd, new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z));

        UpdateCombat(gameTime, keyboard);
    }

    private void ApplySession(SessionCommand cmd, WorldPoint player)
    {
        if (_session is null) return;

        switch (cmd.Action)
        {
            case SessionAction.BlockedSave:
                _session.ShowToast("Not down here. Camp to bank what you are carrying.");
                return;
            case SessionAction.Save:
                _session.ShowToast(_session.Save());
                return;
            case SessionAction.Load:
                LoadSession();
                return;
            case SessionAction.Pickpocket when cmd.Actor is not null:
                TryPickpocket(cmd.Actor);
                return;
            case SessionAction.OpenShop:
                if (_shop is null) return;
                _stack.Shop = true;
                _panels.ShopSelection = 0;
                SetMouseLook(false);
                return;
            case SessionAction.Talk when cmd.Actor is not null:
                OpenDialogue(cmd.Actor);
                return;
            case SessionAction.UseFixture:
                UseFixture(cmd.Fixture);
                return;
            case SessionAction.TakePickup when cmd.Pickup is not null:
                TakePickup(cmd.Pickup);
                return;
            case SessionAction.OpenDoor:
                if (_run is { BarsTheWay: true } && _world?.FindDoor(player, _camera.Yaw) is not null)
                    _session.ShowToast("Not while something in here is still moving.");
                else
                    TryOpenDoorAhead(player);
                return;
        }
    }

    /// <summary>
    /// The fight: enemies act, then the player does. Blocking is held rather than pressed,
    /// and attacking drops the guard, so the two cannot be used at once.
    /// </summary>
    private void UpdateCombat(GameTime gameTime, KeyboardState keyboard)
    {
        if (_session is null || _encounter is null) return;

        var step = StepSeconds(gameTime);
        SampleStance(step);

        var cmd = _combat.Tick(step, keyboard, _input.CurrentMouse, Clicked(_input.CurrentMouse),
            _mouseLook, _stack.Help, IsMoving(keyboard),
            _session, _encounter, _world, _run, _dialogue, _shop,
            _camera, _weaponView, _coach, _recorder, _sfx, _input);

        switch (cmd.Action)
        {
            case CombatAction.OpenShop:
                _stack.Shop = true;
                _panels.ShopSelection = 0;
                SetMouseLook(false, forPanel: true);
                return;
            case CombatAction.Talk when cmd.Actor is not null:
                OpenDialogue(cmd.Actor);
                return;
            case CombatAction.SelectSpell when cmd.SpellId is not null:
                SelectSpell(cmd.SpellId);
                return;
        }
    }

    private void OpenDialogue(SpeakingActor actor)
    {
        _conversationActor = actor;
        _stack.Dialogue = true;
        _panels.DialogueSelection = 0;
        var topics = actor.Talk();
        _dialogueResponse = topics.Count == 0
            ? $"{actor.DisplayName} has nothing to discuss."
            : $"{actor.DisplayName} looks your way. What do you want to know?";
        SetMouseLook(false);
    }

    /// <summary>Activate the prompt under a released mouse pointer instead of recapturing it.</summary>
    private bool TryActivateWorldPrompt(MouseState mouse)
    {
        if (_session is null || !Clicked(mouse)) return false;

        var pointer = LogicalMouse(mouse);
        var player = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);
        var actor = _dialogue?.FindActor(player, _camera.Yaw);
        if (actor is not null)
        {
            if (UiLayout.TalkPrompt.Contains((int)pointer.X, (int)pointer.Y))
            {
                OpenDialogue(actor);
                return true;
            }

            if (UiLayout.SecondaryPrompt.Contains((int)pointer.X, (int)pointer.Y))
            {
                if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                    && _shop is not null)
                {
                    _stack.Shop = true;
                    _panels.ShopSelection = 0;
                    SetMouseLook(false);
                    return true;
                }

                if (HasPickablePocket(actor)
                    && UiLayout.PickpocketPrompt.Contains((int)pointer.X, (int)pointer.Y))
                {
                    TryPickpocket(actor);
                    return true;
                }
            }
        }

        if (!UiLayout.SinglePrompt.Contains((int)pointer.X, (int)pointer.Y)) return false;

        var pickup = FindPickup(player, _camera.Yaw);
        if (pickup is not null)
        {
            TakePickup(pickup);
            return true;
        }

        return TryOpenDoorAhead(player);
    }

    /// <summary>
    /// Open whatever door the player is facing, and say so.
    ///
    /// One method because there are two ways to ask -- the key and the on-screen prompt -- and
    /// they had a copy each. The copies had already drifted in both directions: pressing E
    /// recorded the opening for telemetry and played no sound, while clicking the prompt played
    /// the sound and recorded nothing. Every door opened by hand was missing from the recording
    /// or missing from the audio depending on which the player used.
    /// </summary>
    private bool TryOpenDoorAhead(WorldPoint player)
    {
        if (_world is null || _session is null) return false;

        var result = _world.TryOpenDoor(player, _camera.Yaw, _session.Player, out var door);
        if (door is null) return false;

        if (door.Lock.IsOpen)
            _recorder.Record(PlayEventKind.DoorOpened, door.Definition.Id, 0f, 0f,
                _session.Player.Vitals.Health, _session.Player.Vitals.Prana);

        _session.ShowToast(result switch
        {
            LockResult.Opened => $"{door.Definition.Id} opened.",
            LockResult.Unlocked => "The key turns. The door opens.",
            LockResult.Failed => $"The lock resists. Security {door.Definition.Difficulty:0} required.",
            _ => "The door is already open."
        });

        _sfx?.Play(result switch
        {
            LockResult.Opened or LockResult.Unlocked => Sfx.Door,
            _ => Sfx.Denied
        }, 0.6f);

        return true;
    }

    private void UpdateDialogueInput(KeyboardState keyboard, MouseState mouse)
    {
        if (_conversationActor is null)
        {
            _stack.Dialogue = false;
            return;
        }

        var cmd = _panels.StepDialogueCommand(_input, keyboard, mouse, LogicalMouse(mouse),
            _conversationActor.AvailableTopics());
        switch (cmd.Action)
        {
            case DialogueAction.Dismiss:
                ClosePanels();
                return;
            case DialogueAction.Ask when cmd.Keyword is not null:
                AskDialogueTopic(cmd.Keyword);
                return;
        }
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

    private void ReportAttack(AttackOutcome outcome) =>
        _feel.ReportAttack(outcome, _session, _sfx);

    private void ReportCast(CastOutcome outcome)
    {
        if (_session is null) return;
        _feel.ReportCast(outcome, _session, _sfx);
    }

    /// <summary>
    /// Choosing and using an item.
    ///
    /// Testers found the inventory unintuitive because it was a list that did nothing: you
    /// could read your possessions but not drink, equip or wear any of them. Selection is
    /// driven from both the keyboard and the pointer so neither is the only way in.
    /// </summary>
    /// <summary>
    /// Walking the fort's corridor, and stepping into a room.
    ///
    /// Escape steps back one level rather than closing everything: from a room to the
    /// corridor, from the corridor out. A single Escape that dumps the player onto the yard
    /// from three rooms deep is the thing that makes a menu feel like a trap.
    /// </summary>
    private void UpdateFort(KeyboardState keyboard, MouseState mouse)
    {
        var cmd = _panels.StepFortCommand(_input, keyboard, mouse, LogicalMouse(mouse),
            _session is not null, _stack.FortRoom is not null);
        switch (cmd.Action)
        {
            case FortAction.Back:
                _stack.FortRoom = null;
                return;
            case FortAction.Close:
                ClosePanels();
                return;
            case FortAction.Enter:
                ApplyFortEnter(cmd.RoomIndex);
                return;
        }
    }

    private void ApplyFortEnter(int roomIndex)
    {
        if (_session is null) return;

        var rooms = FortRoster.All;
        if (roomIndex < 0 || roomIndex >= rooms.Count) return;

        var room = rooms[roomIndex];
        var rank = _session.Player.Legacy.Service.Rank;

        if (!room.IsOpen(rank))
        {
            _session.ShowToast(
                $"That door is shut to a {Ranks.TitleOf(rank)}. It wants a {Ranks.LabelOf(room.RequiredRank)}.");
            _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        _stack.FortRoom = room.Id;
        _sfx?.Play(Sfx.Door, 0.4f, volumeScale: 0.7f);
    }

    private void UpdateInventory(KeyboardState keyboard)
    {
        if (_session is null) return;

        UpdateStoneInput(keyboard);

        var items = _session.Player.Inventory.Items;
        if (!_panels.StepInventoryCommand(_input, keyboard, _input.CurrentMouse,
            LogicalMouse(_input.CurrentMouse), items.Count))
            return;

        ApplyInventoryUse();
    }

    private void ApplyInventoryUse()
    {
        if (_session is null) return;

        var items = _session.Player.Inventory.Items;
        if (_panels.InventorySelection < 0 || _panels.InventorySelection >= items.Count) return;

        var item = items[_panels.InventorySelection];
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
        _panels.InventorySelection = Math.Clamp(_panels.InventorySelection, 0,
            Math.Max(0, _session.Player.Inventory.Items.Count - 1));
    }

    private static bool IsMoving(KeyboardState keyboard) =>
        keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.A)
        || keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.D);

    private void UpdateCrouchToggle(KeyboardState keyboard)
    {
        if (!_forceCrouch
            && (Pressed(keyboard, Keys.LeftControl) || Pressed(keyboard, Keys.RightControl)))
            _crouchToggled = !_crouchToggled;

        _camera.Crouching = _forceCrouch || _crouchToggled;
    }

    /// <summary>
    /// Begin a session and populate the world around it. The camp is spawned here rather
    /// than by the session, because where a bandit stands is a scene fact, not a save fact —
    /// the save only remembers which ones are already dead.
    /// </summary>
    private void StartSession(GameSession session) => _director.Start(session);

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
    /// Note that a panel opened or closed.
    ///
    /// Watched rather than hooked into every panel, because there are nine of them and one
    /// forgotten call site is a silently wrong number rather than a visible bug.
    /// </summary>
    private void TrackPanelTime()
    {
        var open = AnyPanelOpen;
        if (open == _panelWasOpen) return;

        _panelWasOpen = open;
        _recorder.Record(PlayEventKind.Panel, open ? "open" : "closed", 0f, open ? 1f : 0f,
            _session?.Player.Vitals.Health ?? 0f, _session?.Player.Vitals.Prana ?? 0f);
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

        var (where, inDoorway) = run.Stance(_world, _camera.Position);
        _recorder.Record(PlayEventKind.Stance, where,
            _encounter.NearestEnemyRange(), inDoorway ? 1f : 0f,
            _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
    }

    private void TickVitalPulses(float deltaSeconds) =>
        _feel.TickVitalPulses(_session, deltaSeconds);

    private void Impact(float weight) => _feel.Impact(weight);

    private (float Yaw, float Pitch) ShakeOffset() => _feel.ShakeOffset(_clock);

    private void WatchSessionForTheFeel(GameSession session) =>
        _feel.WatchSession(session, _sfx);

    private void WatchForTheFeel(Encounter encounter) =>
        _feel.WatchEncounter(encounter, _sfx);

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

        WatchForTheFeel(encounter);
        WatchSessionForTheFeel(session);
    }

    /// <summary>The stream that decides what a cleared room gives up. Seeded from the mine.</summary>
    private Random _stoneDrops
    {
        get => _play.StoneDrops;
        set => _play.StoneDrops = value;
    }

    /// <summary>
    /// A cleared room sometimes gives up a stone.
    ///
    /// Not every room, because a stone in every room means the sockets are full by room three
    /// and the rest of the descent has no decisions left in it. Not payout-scaled either: a
    /// stone is variety rather than reward, and tying it to depth would make the deep rooms
    /// the only ones worth clearing for reasons that have nothing to do with the stones.
    /// </summary>
    private void OfferStone() =>
        _feel.OfferStone(_session, _run, _stoneDrops, _mineDepth, _sfx, _coach);

    /// <summary>Roughly one room in two.</summary>
    private const double StoneDropChance = 0.5;

    private void StartRun() => _director.StartRun();

    /// <summary>
    /// Put the run away and show what it was worth.
    ///
    /// Camping pays out here rather than in the domain because this is where the inventory
    /// lives; the ledger's job ends at deciding the number.
    /// </summary>
    /// <summary>What the run that just ended earned permanently, for the summary screen.</summary>
    private IReadOnlyList<string> _earnedAmulets
    {
        get => _play.EarnedAmulets;
        set => _play.EarnedAmulets = value;
    }

    private void EndRun(RunResult result) => _director.EndRun(result);

    private bool LoadSession() => _director.Load();

    protected override void OnDisplayChanged() =>
        _camera.SetProjection(GraphicsDevice.Viewport.AspectRatio);

    /// <summary>Roughly a pace. Shorter crouching, longer at a sprint.</summary>
    private const float StrideMetres = 1.9f;

    /// <summary>
    /// Advance the stride by how far the body actually moved, and step when it comes due.
    ///
    /// Deliberately silent outside the world scene, and silent while airborne — a player who
    /// jumps should not be taking paces in mid-air, and that is exactly what a distance-based
    /// pacer does if nobody stops it.
    /// </summary>
    private void Stride(float metres, KeyboardState keyboard) =>
        _feel.Step(metres, keyboard, _screen == GameScreen.WorldScene, _camera.Grounded,
            _camera.Crouching, _sfx);

    private void UpdateCamera(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        var world = _world;
        ResolveWalk? collide = _screen == GameScreen.WorldScene && world is not null
            ? (origin, delta, radius) =>
            {
                var current = new WorldPoint(origin.X, origin.Y, origin.Z);
                var resolved = world.Move(current,
                    new WorldPoint(delta.X, 0f, delta.Z), radius);
                return new Vector3(resolved.X, origin.Y, resolved.Z);
            }
            : null;

        var moved = _camera.Step(
            StepSeconds(gameTime),
            new WalkInput(
                Forward: keyboard.IsKeyDown(Keys.W),
                Back: keyboard.IsKeyDown(Keys.S),
                Left: keyboard.IsKeyDown(Keys.A),
                Right: keyboard.IsKeyDown(Keys.D),
                Sprint: keyboard.IsKeyDown(Keys.LeftShift),
                Jump: Pressed(keyboard, Keys.Space),
                HeldYaw: (keyboard.IsKeyDown(Keys.Right) ? 1f : 0f)
                    - (keyboard.IsKeyDown(Keys.Left) ? 1f : 0f),
                HeldPitch: (keyboard.IsKeyDown(Keys.Up) ? 1f : 0f)
                    - (keyboard.IsKeyDown(Keys.Down) ? 1f : 0f)),
            ReadMouseDelta(mouse),
            collide);

        if (moved.Landed && _screen == GameScreen.WorldScene)
            _sfx?.Play(Sfx.Land, 0.5f, volumeScale: 0.8f);

        if (moved.MetresWalked > 0f) Stride(moved.MetresWalked, keyboard);
    }

    private void UpdateCameraMatrices()
    {
        var (yaw, pitch) = ShakeOffset();
        _camera.RebuildView(yaw, pitch);
    }

    /// <summary>Where the player currently is, for the banner across the top of the HUD.</summary>
    private string LocationCaption() => _mineSeed is { } seed
        // The decimal seed, because that is what --mine takes: a mine worth replaying or
        // reporting can be asked for again exactly.
        // The cave's name, not only its number. A player who has learned that the Drowned
        // Level fears Arc needs to be told which cave they are in without opening anything.
        ? _cave is null
            ? $"MINE {seed}  ·  TIER {_mineDepth}"
            : $"{_cave.DisplayName.ToUpperInvariant()}  ·  TIER {_mineDepth}  ·  MINE {seed}"
        : "THE YARD  ·  RATNA BAY";

    private void ResetCamera()
    {
        var spawn = _world?.Manifest.PlayerSpawn;
        if (spawn is not null)
        {
            _camera.Reset(
                new Vector3(spawn.Position.X, spawn.Position.Y, spawn.Position.Z),
                spawn.Yaw, -0.12f, spawn.Position.Y);
        }
        else
        {
            _camera.Reset(new Vector3(0f, 2.4f, 8.5f), 0f, -0.12f, 2.4f);
        }

        _crouchToggled = false;
    }

    private void LoadWorldManifest() => _director.LoadWorldManifest();

    /// <summary>True while the player is standing in the yard rather than down a mine.</summary>
    private bool OnTheSurface => _mineSeed is null;

    /// <summary>The one pickup that is not part of the level it appears in.</summary>
    private const string CachePickupId = SessionDirector.CachePickupId;

    private void LoadDialogueManifest() => _content.LoadDialogueManifest();

    private void LoadWatchers() => _content.LoadWatchers();

    private void LoadPockets() => _content.LoadPockets();

    private void LoadPickups() => _content.LoadPickups();

    /// <summary>Put the gear back on the shelf, in memory and in the save.</summary>
    private void RestockTheStall() => _content.RestockTheStall();

    private void LoadShop() => _content.LoadShop();

    /// <summary>True when this actor is carrying something that has not been lifted yet.</summary>
    private bool HasPickablePocket(SpeakingActor actor) =>
        ParkedFeatures.Pickpocketing
        && _pockets.TryGetValue(actor.ActorId, out var target) && target.RemainingItems > 0;

    private void TryPickpocket(SpeakingActor actor)
    {
        if (!ParkedFeatures.Pickpocketing) return;
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

    private WorldPickup? FindPickup(WorldPoint player, float yaw, float range = 3.2f) =>
        SessionInput.FindPickup(_pickups, player, yaw, range);

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

        switch (_panels.StepShopCommand(_input, keyboard, mouse, LogicalMouse(mouse),
            _shop.Definition.Items.Count))
        {
            case ShopAction.Dismiss:
                _stack.Shop = false;
                return;
            case ShopAction.Buy:
                BuySelectedShopItem();
                return;
        }
    }

    private void BuySelectedShopItem()
    {
        if (_shop is null || _session is null) return;

        var result = _shop.Buy(_panels.ShopSelection, _session.Player.Vitals,
            _session.Player.Inventory, out var item);
        if (result == ShopPurchaseResult.Bought && item is not null)
        {
            _session.Player.Story.MarkLooted($"shop.{_shop.Definition.Id}.{item.Id}");
            _recorder.Record(PlayEventKind.ItemBought, item.Name, item.Price, 0f,
                _session.Player.Vitals.Health, _session.Player.Vitals.Prana, item.Kind);
            _session.ShowToast($"Bought {item.Name}.");
            _sfx?.Play(Sfx.Coin, 0.55f);
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

    private void LoadQuestManifest() => _content.LoadQuestManifest();

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

        _ui.Begin();
        var items = MenuItems;
        _screens.Menu.Draw(new MenuState(
            Items: items,
            Selection: _overlay.MenuSelection,
            Status: _menuStatus,
            Resuming: items[_overlay.MenuSelection] == ResumeItem,
            ShowSettings: _overlay.ShowSettings,
            Overlay: BuildOverlayState()), _screens.Overlay);
        EndUi();
    }

    /// <summary>The volume row's value, or why there isn't one.</summary>
    private string SoundVolumeLine()
    {
        if (_sfx is null || !_sfx.IsAvailable) return "unavailable on this machine";
        return _sfx.Volume <= 0f ? "off" : $"{_sfx.Volume * 100f:0}%";
    }

    /// <summary>
    /// Copies the live session into the small set of values the world HUD presents.
    ///
    /// Keeping this snapshot at the game/render boundary means HudRenderer never needs to
    /// know about save files, quest services, or the rest of Game1's orchestration state.
    /// </summary>
    private WorldHudState BuildWorldHudState() =>
        WorldHudBuilder.Build(_session, _encounter, _runSummary, _stack, _coach,
            _capture.IsCapturing, _healthPulse, _pranaPulse, _framesPerSecond, LocationCaption());

    /// <summary>Copies modal-screen state into the renderer-facing overlay snapshot.</summary>
    private OverlayState BuildOverlayState()
    {
        var activeRun = _run is { Run.IsActive: true } run ? run.Run : null;
        return new OverlayState(
            InRun: activeRun is not null,
            RoomsCleared: activeRun?.RoomsCleared ?? 0,
            PendingStones: activeRun?.Pending ?? 0,
            PauseItems: PauseItems,
            PauseSelection: _overlay.PauseSelection,
            // Same length as OverlayInput.SettingsRowCount, or the keyboard cannot reach
            // the last row the renderer draws.
            SettingsOptions: new[]
            {
                $"Display mode     {(_borderlessFullscreen ? "Borderless fullscreen" : "Windowed 1280x720")}",
                $"UI scale          {_uiScalePreference:0.0}x",
                $"Sound             {SoundVolumeLine()}",
                "Bindings          WASD move | E interact | J journal | I character",
                SettingsTelemetryLine()
            },
            SettingsSelection: _overlay.SettingsSelection,
            RecordingDirectory: PlayRecorder.DisplayDirectory);
    }

    private void DrawWorldScene()
    {
        if (_moodboard)
        {
            _spikes.DrawMoodboard(GraphicsDevice, _scene, _billboards, _camera, _primitiveEffect,
                _lights, _worldView.Stone, _ui, _clock, _assetCase);
            EndUi();
            return;
        }

        if (_stambhaPreview)
        {
            _spikes.DrawStambha(GraphicsDevice, _scene, _camera, _primitiveEffect, _ui);
            EndUi();
            return;
        }

        DrawAuthoredWorld();
        _figures.Draw(GraphicsDevice, _scene, _billboards, _camera, _dialogue, _watchers,
            _encounter, _cave);

        if (_capture.CoverMode)
        {
            _screens.Cover.Draw(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            return;
        }

        _ui.Begin();

        var hudState = BuildWorldHudState();
        FramePresenter.DrawWorldInterface(
            _stack.HidesHud || _hideInterface,
            _hideInterface,
            combatHud: () =>
            {
                DrawWeapon();
                _screens.Hud.DrawDamageFlash(hudState);
                if (ParkedFeatures.Sneaking) _screens.Hud.DrawSneakOverlay(hudState);
                DrawThreatArrows();
                DrawFloatingNumbers();
                _screens.Hud.DrawCrosshair(hudState);
                _screens.Hud.DrawHitMarker(hudState);
                _screens.Hud.DrawDamageDirections(hudState);
                _screens.Hud.DrawSpellBar(hudState, ItemSprites.JivaCrystal(GraphicsDevice));
                _screens.Hud.DrawCastBanner(hudState);
                DrawSurfaceSigns();
                _screens.Hud.DrawCoach(hudState);
                DrawCampDecision();
                _screens.Prompt.Draw(BuildPromptState());
                DrawRunLedger();
                _screens.Hud.DrawLocationBanner(hudState);
                if (ParkedFeatures.Sneaking) _screens.Hud.DrawAwareness(hudState);
                DrawEnemyNameplates();
                _screens.Hud.DrawObjective(hudState);
                _screens.Hud.DrawVitals(hudState);
                _screens.Hud.DrawStatusStrip(hudState);
            },
            toasts: () =>
            {
                _screens.Hud.DrawToasts(hudState);
                DrawContentErrors();
            },
            panels: () =>
            {
                if (_stack.Help) _screens.Overlay.DrawHelpOverlay(BuildOverlayState());
                if (_stack.Dialogue) DrawDialogue();
                if (_stack.Journal) DrawJournal();
                if (_stack.Fort && _session is not null)
                    _screens.Fort.Draw(_session.Player.Legacy, _panels.FortSelection, _stack.FortRoom);
                if (_stack.Character) DrawCharacterSheet();
                if (_stack.Shop) DrawShop();
                if (_stack.CampTrader) DrawCampTrader();
                if (_stack.Shaft) DrawDepthChoice();
                if (_stack.Paused && _runSummary is null) _screens.Overlay.DrawPause(BuildOverlayState());
                if (_runSummary is { } summary) DrawRunSummary(summary);
            },
            watches: DrawWatches,
            console: DrawConsole);

        EndUi();
    }

    private void DrawCampDecision()
    {
        if (_run is not { AtDecision: true } decision) return;
        _screens.Descent.DrawCampDecision(decision.Run);
    }


    private void DrawRunLedger()
    {
        if (_run is not { } active || !active.Run.IsActive || _runSummary is not null) return;
        _screens.Descent.DrawRunLedger(active.Run);
    }

    private void DrawCampTrader()
    {
        if (_session is null || _run is null) return;
        _screens.Descent.DrawCampTrader(_session.Player.Inventory, _run.Run, _panels.CampSelection);
    }

    private void DrawDepthChoice()
    {
        if (_session is null) return;
        _screens.Descent.DrawDepthChoice(
            _session.Player.Inventory.CountOf(SoulCrystals.LesserId), _panels.DepthSelection,
            tier => CaveThemeCatalog.For(_shaftSeeds[tier], tier));
    }

    private void DrawRunSummary(RunResult summary)
    {
        var pointer = LogicalMouse(_input.CurrentMouse);
        var hovered = UiLayout.SummaryButton.Contains((int)pointer.X, (int)pointer.Y);
        _screens.Descent.DrawRunSummary(summary, _session?.Player, _succession, hovered,
            _earnedAmulets);
    }

    /// <summary>
    /// Names over the three things in the yard, and a line saying what a Bhagiratha is for.
    ///
    /// Reported as not knowing what to do here, which was fair: a walled yard with no labels
    /// and no instruction is a room, not a hub. A name floating over each fixture answers
    /// "where do I go" from anywhere in the yard, without a tutorial saying it out loud.
    /// </summary>
    private void DrawSurfaceSigns()
    {
        if (!OnTheSurface || _session is null || _runSummary is not null) return;

        // Taught where each thing is true, rather than all at once on arrival. Standing in
        // front of the shaft is the moment "a shaft costs stones" means anything.
        _coach.Teach(Lessons.Yard, Lessons.TextOf(Lessons.Yard));

        switch (Surface.FixtureAt(new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z)))
        {
            case SurfaceFixture.Shaft:
                _coach.Teach(Lessons.Shaft, Lessons.TextOf(Lessons.Shaft));
                break;
            case SurfaceFixture.Trader:
                _coach.Teach(Lessons.Stall, Lessons.TextOf(Lessons.Stall));
                break;
        }

        var stones = _session.Player.Inventory.CountOf(SoulCrystals.LesserId);
        var gold = _session.Player.Vitals.Gold;

        var deepest = MineEntry.DeepestAffordable(_session.Player.Inventory);
        var next = Math.Min(MineEntry.MaxTier, deepest + 1);

        _ui.TextCentred(deepest >= MineEntry.MaxTier
                ? $"{stones} stones. The order will sell you any mine it has.  {gold} gold."
                : deepest > MineEntry.MinTier
                    ? $"{stones} stones opens tier {deepest}. Tier {next} wants {MineEntry.CostOf(next)}.  {gold} gold."
                    : $"The first mine is free. {MineEntry.CostOf(2)} stones opens a richer one — you have {stones}.",
            LogicalWidth / 2f, 48f, 14, UiTheme.Hint);

        Sign("THE SHAFT", "go down", Surface.Shaft, 5.6f, new Color(214, 186, 120));
        Sign("THE STALL", "spend gold", Surface.Trader, 4f, new Color(196, 176, 210));
        Sign("THE STAMBHA", "read it", Surface.Stambha, 5.4f, new Color(151, 206, 210));
    }

    private void Sign(string title, string subtitle, WorldPoint at, float height, Color colour)
    {
        var player = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);

        _screens.Markers.DrawSign(title, subtitle,
            new Vector3(at.X, at.Y + height, at.Z), player.FlatDistanceTo(at), colour,
            Projector());
    }

    private void DrawConsole()
    {
        if (!_consoleKeys.Open) return;

        _screens.Console.Draw(_scripts.Output, _consoleKeys.Buffer, _clock);
    }

    /// <summary>
    /// What the player can do with whatever is under the crosshair.
    ///
    /// Queries live here because they need the world, the session and the run. The renderer
    /// only paints the chips it is given.
    /// </summary>
    private PromptState BuildPromptState()
    {
        var player = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);
        return PromptBuilder.Build(_session, _camera, OnTheSurface, _dialogue, _shop, _world, _run,
            _pockets, FindPickup(player, _camera.Yaw));
    }

    private void DrawDialogue()
    {
        if (_conversationActor is null) return;
        _screens.Dialogue.Draw(_conversationActor, _dialogueResponse, _panels.DialogueSelection);
    }

    private void DrawJournal()
    {
        if (_session is null) return;
        _screens.Journal.Draw(_session.Player);
    }

    private void DrawCharacterSheet()
    {
        if (_session is null) return;
        _screens.Character.Draw(_session.Player, _panels.InventorySelection,
            ItemSprites.JivaCrystal(GraphicsDevice));
    }


    /// <summary>Number keys socket a found stone; zero takes the last one back out.</summary>
    private void UpdateStoneInput(KeyboardState keyboard)
    {
        if (_session is null) return;

        var stones = _session.Player.Stones;

        if (Pressed(keyboard, Keys.D0) && stones.Socketed.Count > 0)
        {
            if (stones.Unsocket(stones.Socketed[^1])) _sfx?.Play(Sfx.Coin, 0.3f);
            return;
        }

        for (var index = 0; index < 6 && index < stones.Loose.Count; index++)
        {
            if (!Pressed(keyboard, Keys.D1 + index)) continue;

            var id = stones.Loose[index];
            if (stones.Socket(id))
            {
                _session.ShowToast($"{StoneCatalog.Find(id)?.DisplayName} socketed.");
                _sfx?.Play(Sfx.Chime, 0.35f);
            }
            else
            {
                _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            }

            return;
        }
    }

    private void DrawShop()
    {
        if (_session is null || _shop is null) return;
        _screens.Shop.Draw(_shop, _session.Player.Vitals.Gold, _panels.ShopSelection);
    }

    /// <summary>
    /// Squared, for sorting only. Anything that compares against a distance in metres wants
    /// <see cref="MetresToCamera"/> — see the note there.
    /// </summary>
    private float DistanceToCamera(Enemy enemy) =>
        Vector3.DistanceSquared(_camera.Position,
            new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z));

    /// <summary>
    /// Real metres to an enemy.
    ///
    /// The nameplate code was using the squared form against a range of 26, so a plate only
    /// appeared within the square root of that -- about five metres -- and the distance-based
    /// shrink hit its floor almost immediately. The level of the thing walking at you was
    /// therefore unreadable until it was already on top of you, which is precisely when
    /// nobody has time to read it.
    /// </summary>
    private float MetresToCamera(Enemy enemy) =>
        Vector3.Distance(_camera.Position,
            new Vector3(enemy.Position.X, enemy.Position.Y, enemy.Position.Z));

    /// <summary>
    /// The weapon in the player's hand.
    ///
    /// Capture pose is set here because --swing / --cast are coordinator concerns. The blit
    /// itself lives on WeaponRenderer.
    /// </summary>
    private void DrawWeapon()
    {
        if (_session is null) return;

        var weapon = _session.Player.Combat.ActiveWeapon;

        if (_captureSwing is { } progress)
        {
            _weaponView.Swing(weapon, _session.Player.Combat.WeaponSweeps);
            _weaponView.Update(progress, moving: false, guarding: false);
        }

        if (_captureCast is { } castProgress)
        {
            _weaponView.Cast();
            _weaponView.Update(castProgress, moving: false, guarding: false);
        }

        _screens.Weapon.Draw(
            _weaponView,
            weapon,
            _session.Player.Combat.IsBlocking ? _session.Player.Equipment.Shield : null);
    }
    /// <summary>
    /// Nameplates for everything alive and close enough to matter.
    ///
    /// The projection happens here, where the camera is, and the renderer receives points on
    /// the canvas -- so a marker can never be projected with a different frame's camera than
    /// the one it is drawn over.
    /// </summary>
    /// <summary>
    /// Everything currently true of an enemy, in the order it matters to the player.
    ///
    /// Striking first because it is the one with a deadline attached — it is the moment to
    /// guard. Then what is being done to it, which is what tells a player their last spell or
    /// stone did something and is still doing it.
    /// </summary>
    private string StatusOf(Enemy enemy)
    {
        var states = new List<string>(4);

        if (_encounter is not null && _encounter.IsLunging(enemy)) states.Add("striking");
        if (enemy.IsStaggered) states.Add("staggered");
        if (enemy.IsBurning) states.Add("burning");
        if (enemy.IsChilled) states.Add("chilled");

        return string.Join(" · ", states);
    }

    private void DrawEnemyNameplates()
    {
        if (_encounter is null || _encounter.Enemies.Count == 0) return;

        // Far ones first, so a nearer plate overlaps a further one rather than the reverse.
        var sorted = new List<Enemy>(_encounter.Enemies);
        sorted.Sort((a, b) => DistanceToCamera(b).CompareTo(DistanceToCamera(a)));

        var projector = Projector();
        var plates = new List<NameplateState>();

        foreach (var enemy in sorted)
        {
            if (!enemy.IsAlive) continue;

            var distance = MetresToCamera(enemy);
            if (distance > MarkerRenderer.NameplateRange) continue;

            var feet = _encounter.DrawPositionOf(enemy);
            var head = feet + Vector3.Up * (_encounter.DrawHeightOf(enemy) + 0.34f);
            if (!projector.TryProject(head, out var anchor)) continue;

            // Shrink with distance, but never past readable. A plate that scales all the way
            // down is unreadable exactly when a player most wants to know what is coming.
            var scale = MathHelper.Clamp(
                1.25f - distance / MarkerRenderer.NameplateRange, 0.62f, 1f);

            plates.Add(new NameplateState(
                Anchor: anchor,
                Scale: scale,
                // Always, and labelled.
                //
                // It was hidden at level one and drawn as a bare number after a dot, so the
                // shallow rooms showed nothing and the deep ones showed "Bandit · 4" -- which
                // could be a level, a count, or a rank. Now that every body rolls its own
                // level out of a band, the number is the main thing a player reads off a room
                // on entry: five bandits at Lv 3 and one at Lv 6 is a different room from six
                // at Lv 3, and it should be legible from the doorway.
                Label: $"{enemy.DisplayName}   Lv {enemy.Archetype.Level}",
                // Every state that is true, not the first one.
                //
                // This was a priority chain, so a burning enemy that got staggered stopped
                // saying it was burning. The burn was still running -- Enemy.Tick counts it
                // down and applies it whatever else is happening -- but the readout said
                // otherwise, and a player reasonably concluded the stagger had cancelled it.
                // An effect the player cannot see is an effect they will not build on.
                Status: StatusOf(enemy),
                HealthFraction: MathHelper.Clamp(enemy.Health / enemy.MaxHealth, 0f, 1f),
                Vulnerable: enemy.IsVulnerable,
                Focused: ReferenceEquals(_encounter.Focused, enemy)));
        }

        _screens.Markers.DrawNameplates(plates);
    }

    private void DrawFloatingNumbers()
    {
        if (_encounter is null) return;

        _screens.Markers.DrawFloatingNumbers(_encounter.Feedback.Numbers, Projector());
    }

    private void DrawThreatArrows()
    {
        if (_encounter is null) return;

        _screens.Markers.DrawThreatArrows(_encounter.NearbyThreats());
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
            case "inventory" or "character": _stack.Character = true; break;
            case "journal": _stack.Journal = true; break;
            case "help": _stack.Help = true; break;
            case "depth" or "shaft":
                _panels.DepthSelection = 3;
                _stack.Shaft = true;
                break;
            case "shop" or "stall": _stack.Shop = true; break;
            case "camp" or "trader": _stack.CampTrader = true; break;
            case "fort": _stack.Fort = true; break;
            case "pause": _stack.Paused = true; break;
            case "dialogue":
                _conversationActor = _dialogue?.Actors.FirstOrDefault();
                _stack.Dialogue = _conversationActor is not null;
                _dialogueResponse =
                    "Northwatch is a border camp built around an older stone watchpost. Keep "
                    + "your eyes open on the road north, and do not travel it after dark "
                    + "unless you have a reason worth the risk.";
                break;
        }

    }

    /// <summary>This frame's world-to-canvas projection, for anything anchored in the world.</summary>
    private WorldProjector Projector() =>
        new(GraphicsDevice.Viewport, _camera.View, _camera.Projection, _ui.Scale,
            _ui.LogicalWidth, _ui.LogicalHeight);

    private void DrawContentErrors() =>
        _screens.Markers.DrawContentErrors(_assetErrors.Concat(_modelCache.Errors).ToList());



    private void DrawAuthoredWorld() =>
        _worldView.Draw(GraphicsDevice, _scene, _modelCache, _world, _pickups,
            OnTheSurface, _cave, _camera.View, _camera.Projection, _lights);

    // ------------------------------------------------------------------ session director

    ScreenStack ISessionHooks.Stack => _stack;
    FirstPersonView ISessionHooks.Camera => _camera;
    PlayRecorder ISessionHooks.Recorder => _recorder;
    Coach ISessionHooks.Coach => _coach;
    SoundBank? ISessionHooks.Sfx => _sfx;
    GameScreen ISessionHooks.Screen
    {
        get => _screen;
        set => _screen = value;
    }
    string ISessionHooks.MenuStatus
    {
        get => _menuStatus;
        set => _menuStatus = value;
    }
    string ISessionHooks.QuestObjectiveId
    {
        get => _questObjectiveId;
        set => _questObjectiveId = value;
    }
    IList<string> ISessionHooks.AssetErrors => _assetErrors;
    void ISessionHooks.SetMouseLook(bool enabled, bool forPanel) => SetMouseLook(enabled, forPanel);
    void ISessionHooks.SpawnEnemies() => SpawnEnemies();
    void ISessionHooks.WatchForTheRecord(Encounter encounter, GameSession session) =>
        WatchForTheRecord(encounter, session);
    void ISessionHooks.LoadQuestManifest() => LoadQuestManifest();
    void ISessionHooks.LoadDialogueManifest() => LoadDialogueManifest();
    void ISessionHooks.LoadWatchers() => LoadWatchers();
    void ISessionHooks.LoadPockets() => LoadPockets();
    void ISessionHooks.LoadPickups() => LoadPickups();
    void ISessionHooks.LoadShop() => LoadShop();
    void ISessionHooks.RefreshQuestObjective() => RefreshQuestObjective();
    void ISessionHooks.RestockTheStall() => RestockTheStall();
    void ISessionHooks.ResetCamera() => ResetCamera();
    void ISessionHooks.OfferStone() => OfferStone();
    void ISessionHooks.LeaveToMenu() => LeaveToMenu();
    bool ISessionHooks.SuspendedOnDisk
    {
        get => _suspendedDescentOnDisk;
        set => _suspendedDescentOnDisk = value;
    }

    // ------------------------------------------------------------------ the console's reach

    GameSession? IConsoleTarget.Session => _session;
    Encounter? IConsoleTarget.Encounter => _encounter;
    RunRuntime? IConsoleTarget.Run => _run;
    WorldRuntime? IConsoleTarget.World => _world;

    Vector3 IConsoleTarget.CameraPosition => _camera.Position;
    float IConsoleTarget.CameraYaw => _camera.Yaw;
    float IConsoleTarget.CameraPitch => _camera.Pitch;

    bool IConsoleTarget.NoClip
    {
        get => _camera.NoClip;
        set => _camera.NoClip = value;
    }

    bool IConsoleTarget.Invulnerable
    {
        get => _invulnerable;
        set
        {
            _invulnerable = value;

            // The flag lives on combat, which is what actually decides whether a blow lands.
            // Keeping a second copy here that nothing reads is how 'hud off' came to print a
            // cheerful message and do nothing at all.
            if (_session is not null) _session.Player.Combat.Invulnerable = value;
        }
    }

    bool IConsoleTarget.HideInterface
    {
        get => _hideInterface;
        set => _hideInterface = value;
    }

    /// <summary>
    /// Put the player somewhere, camera and session together.
    ///
    /// Both, because they are two records of the same fact: the camera is what is drawn from
    /// and the session is what is saved and what the world asks about. Moving one without the
    /// other produces a player who is in two places, which is worse than not moving at all.
    /// </summary>
    void IConsoleTarget.PlaceAt(Vector3 position)
    {
        _camera.Place(position);

        if (_session is not null)
            _session.Position = new WorldPoint(position.X, position.Y, position.Z);
    }

    void IConsoleTarget.LookAt(float yaw, float pitch)
    {
        _camera.Yaw = yaw;
        _camera.Pitch = MathHelper.Clamp(pitch, -_camera.PitchLimit, _camera.PitchLimit);
    }

    void IConsoleTarget.Descend(int tier, int? seed) =>
        EnterMine(seed ?? Environment.TickCount, tier);

    void IConsoleTarget.Surface()
    {
        _mineSeed = null;
        _world = null;
        _run = null;
        _runSummary = null;
        _succession = null;

        if (_session is not null) StartSession(_session);
        ResetCamera();
    }

    float IConsoleTarget.TimeScale
    {
        get => _timeScale;
        set => _timeScale = value;
    }

    void IConsoleTarget.WaitSeconds(float seconds) => _scripts.WaitSeconds = seconds;

    void IConsoleTarget.FailScript(string why) => FailScript(why);

    private void FailScript(string why) => _scripts.Fail(why);

    void IConsoleTarget.QuitWhenDone() => _scripts.QuitWhenDone = true;

    void IConsoleTarget.Watch(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) _scripts.Watches.Clear();
        else _scripts.Watches.Add(command);
    }

    IReadOnlyList<string> IConsoleTarget.Watches => _scripts.Watches;

    void IConsoleTarget.Queue(string statements) => _scripts.Enqueue(statements);

    /// <summary>Save a frame without ending the run, unlike --screenshot.</summary>
    string IConsoleTarget.Capture(string path)
    {
        _capture.Queue(path);
        return $"Saving {path}.";
    }

    /// <summary>
    /// What the crosshair is on.
    ///
    /// Enemies first, then geometry, because a body in front of a wall is the answer wanted
    /// almost every time. The geometry test is a plain ray march rather than a proper slab
    /// intersection: this is a debugging aid, and a metre of precision is enough to name a box.
    /// </summary>
    string IConsoleTarget.PickAt(int? screenX, int? screenY)
    {
        if (_world is null) return "No world.";

        var origin = _camera.Position;
        Vector3 direction;

        if (screenX is { } px && screenY is { } py)
        {
            // A ray through one pixel, so a thing seen in a screenshot can be asked about by
            // its coordinates rather than by nudging the camera until the crosshair lands on
            // it. Unprojecting the near and far planes is the only way to get this right for
            // an off-centre pixel; the crosshair formula below only holds at the centre.
            var viewport = GraphicsDevice.Viewport;
            var scale = viewport.Width / (float)LogicalWidth;
            var device = new Vector3(px * scale, py * scale, 0f);

            var near = viewport.Unproject(device, _camera.Projection, _camera.View, Matrix.Identity);
            var far = viewport.Unproject(new Vector3(device.X, device.Y, 1f),
                _camera.Projection, _camera.View, Matrix.Identity);

            direction = Vector3.Normalize(far - near);
            origin = near;
        }
        else
        {
            direction = Vector3.Normalize(new Vector3(
                -MathF.Sin(_camera.Yaw) * MathF.Cos(_camera.Pitch),
                MathF.Sin(_camera.Pitch),
                -MathF.Cos(_camera.Yaw) * MathF.Cos(_camera.Pitch)));
        }

        if (_encounter is not null)
        {
            foreach (var enemy in _encounter.Enemies.Where(enemy => enemy.IsAlive))
            {
                var to = new Vector3(enemy.Position.X, enemy.Position.Y + 0.9f, enemy.Position.Z)
                    - origin;
                var along = Vector3.Dot(to, direction);
                if (along <= 0f) continue;

                var miss = (to - direction * along).Length();
                if (miss < 0.9f)
                    return $"{enemy.DisplayName}  {enemy.Health:0} hp  {along:0.0} m";
            }
        }

        // Projectiles and spent arrows are billboards too, and they are the likeliest thing
        // a small bright artifact on a wall turns out to be.
        if (_encounter is not null)
        {
            foreach (var (position, _) in _encounter.Shots)
                if (NearlyUnder(position, origin, direction, 0.5f, out var range))
                    return $"a spent arrow  {range:0.0} m  at {position.X:0.0},{position.Y:0.0},{position.Z:0.0}";

            foreach (var bolt in _encounter.Bolts)
                if (NearlyUnder(bolt.Position, origin, direction, 0.6f, out var range))
                    return $"a bolt in flight  {range:0.0} m";
        }

        for (var step = 0.25f; step < 60f; step += 0.25f)
        {
            var at = origin + direction * step;

            foreach (var box in _world.Manifest.Geometry)
            {
                if (!box.Visible) continue;
                if (at.X < box.Min.X || at.X > box.Max.X) continue;
                if (at.Y < box.Min.Y || at.Y > box.Max.Y) continue;
                if (at.Z < box.Min.Z || at.Z > box.Max.Z) continue;

                return $"{box.Id}  {box.Material}  {step:0.0} m";
            }
        }

        return "Nothing within 60 m.";
    }

    /// <summary>Is this point close to the line the crosshair is on?</summary>
    private static bool NearlyUnder(Vector3 point, Vector3 origin, Vector3 direction,
        float tolerance, out float range)
    {
        var to = point - origin;
        range = Vector3.Dot(to, direction);

        return range > 0f && (to - direction * range).Length() < tolerance;
    }

    void IConsoleTarget.Say(string message) => _session?.ShowToast(message);

    /// <summary>
    /// Close the batch, drawing our own pointer last so it sits over everything.
    ///
    /// Doing it here rather than at each screen is deliberate: the pointer was previously
    /// added to one screen by hand and silently missing from the main menu. Game1 still
    /// decides whether to draw it — that depends on whether the mouse is driving the camera
    /// and whether this frame is a capture — and OverlayRenderer paints the arrow.
    /// </summary>
    private void EndUi()
    {
        if (!_mouseLook && !_capture.IsCapturing)
            _screens.Overlay.DrawPointer(LogicalMouse(_input.CurrentMouse));
        _ui.End();
    }

    private bool Pressed(KeyboardState current, Keys key) => _input.Pressed(current, key);
}
