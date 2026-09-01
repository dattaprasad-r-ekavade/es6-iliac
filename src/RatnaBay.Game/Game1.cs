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

/// <summary>
/// MonoGame lifecycle coordinator: devices, the frame, and draw order.
///
/// Screens, HUD and overlay layout live in <c>Ui/</c>. Input sampling lives in
/// <c>Input/InputRouter</c>. Overlay-stack selection lives in <c>Input/OverlayInput</c>.
/// Look and walk live in <c>Engine/FirstPersonView</c>. Game rules live in
/// <c>RatnaBay.Domain</c>. New work belongs in one of those, not in this class, unless
/// it is genuinely a lifecycle concern. A second game should not subclass this type — see
/// <c>Docs/ENGINE.md</c>.
/// </summary>
public sealed class Game1 : Game, IConsoleTarget
{
    private const int LogicalWidth = UiLayout.Width;
    private const int LogicalHeight = UiLayout.Height;

    private enum GameScreen
    {
        MainMenu,
        WorldScene
    }

    private readonly GraphicsDeviceManager _graphics;

    /// <summary>Imported props: loading, measuring, normalising and drawing.</summary>
    private readonly ModelCache _modelCache = new();

    /// <summary>Boxes, the crystal and the carved quad, with the two shaders.</summary>
    private SceneRenderer _scene = null!;

    private readonly List<string> _assetErrors = new();
    private FontSystem _fontSystem = null!;
    private FontSystem _headingFontSystem = null!;

    /// <summary>One white pixel, stretched for every filled rectangle. Painted by the canvas.</summary>
    private Texture2D _white = null!;

    /// <summary>
    /// Shared by SceneRenderer. Begin takes it each frame so the spike scenes can retune it
    /// for a shot and restore it afterwards.
    /// </summary>
    private BasicEffect _primitiveEffect = null!;

    /// <summary>The stone this room is cut from. One per cave theme, later.</summary>
    private StoneTextures.StonePalette _stone = StoneTextures.StonePalette.Granite;

    /// <summary>
    /// The lights affecting the current draw, nearest first.
    ///
    /// The shader takes four. A room with more torches than that is not a lighting problem,
    /// it is a level design problem, and clamping quietly is the right response either way.
    /// </summary>
    private readonly List<PointLight> _lights = new();

    private readonly InputRouter _input = new();

    /// <summary>
    /// Consent, title menu, pause and settings: selection, hover and confirm.
    /// Side effects of a confirmed row stay in this class.
    /// </summary>
    private readonly OverlayInput _overlay = new();

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
    private bool _showHelp;
    private bool _ignoreMouseDeltaThisFrame;

    private GameScreen _screen = GameScreen.MainMenu;
    private string _menuStatus = string.Empty;
    private bool _borderlessFullscreen = true;

    /// <summary>
    /// Metres walked since the last footstep.
    ///
    /// Paced by distance rather than by a timer, which is the only version that stays honest:
    /// a timer keeps stepping when the player walks into a wall, and goes out of step the
    /// moment they sprint. Distance is also free — the collision resolver already reports how
    /// far the body actually moved, which is not the same as how far it tried to.
    /// </summary>
    private float _stride;
    private bool _crouchToggled;
    private bool _forceCrouch;

    /// <summary>The live character. Null until a game is started or loaded.</summary>
    private GameSession? _session;

    /// <summary>
    /// The developer console, and what it has said.
    ///
    /// Kept here rather than in a screen renderer because it acts on the game rather than
    /// drawing it: everything it can reach is on IConsoleTarget, implemented below.
    /// </summary>
    private ConsoleRouter? _console;

    private readonly List<ConsoleLine> _consoleOutput = new();
    private string _consoleInput = string.Empty;
    private bool _consoleOpen;

    /// <summary>Where the player is in their own history, walking back with Up.</summary>
    private int _consoleHistory = -1;

    /// <summary>--exec / --script: commands to run once the world exists.</summary>
    private string? _consoleScript;

    /// <summary>
    /// Statements still to run, and the frames to let pass before the next one.
    ///
    /// A script cannot be run all at once. --exec used to execute inside LoadContent, before a
    /// single Update had happened, so anything that asked about the simulation got the state
    /// the world was built with: 'descend; enemies' reported an empty room because nothing had
    /// yet ticked to notice the player was standing in it. Statements are pumped one per frame
    /// instead, and 'wait' holds the rest of the script while the game runs.
    /// </summary>
    private readonly Queue<string> _scriptQueue = new();

    /// <summary>
    /// Simulated seconds still to let pass before the next statement.
    ///
    /// Seconds rather than frames, and this is not a detail. Capture mode runs uncapped, so
    /// "wait 120 frames" was about a tenth of a second of game time -- long enough to make a
    /// script look like it had waited and short enough that an enemy was still coming up out
    /// of the floor. It read as a rendering bug for an hour. A script asks for time; how many
    /// frames that takes is the machine's business.
    /// </summary>
    /// <summary>
    /// True when this process was launched with --script.
    ///
    /// Gates anything that assumes a person is at the keyboard -- mouse capture, and the
    /// borderless fullscreen the game otherwise opens in.
    /// </summary>
    private bool _scripted;

    /// <summary>When the last "this run is going nowhere" note was written. See NoteIfStuck.</summary>
    private DateTime _lastStuckNote = DateTime.MinValue;

    private float _scriptWaitSeconds;

    /// <summary>Seconds of held W left on a scripted walk. See IConsoleTarget.WalkForward.</summary>
    private float _scriptWalkSeconds;

    /// <summary>Set by a failed 'assert'. Becomes the process exit code.</summary>
    private bool _scriptFailed;

    /// <summary>A --script path that does not exist, reported once there is a screen to say so on.</summary>
    private string? _scriptMissing;

    /// <summary>1 when a scripted run found a failure, 0 otherwise. Read by Program.</summary>
    public int ScriptExitCode => _scriptFailed ? 1 : 0;

    /// <summary>Set by 'quit', so a test script can end the run itself.</summary>
    private bool _scriptQuitWhenDone;

    /// <summary>Commands re-run every frame and pinned to the screen, for watching a value move.</summary>
    private readonly List<string> _watches = new();

    /// <summary>How fast simulated time runs. Set by 'time'; 1 is normal.</summary>
    private float _timeScale = 1f;

    private bool _invulnerable;
    private bool _hideInterface;

    /// <summary>The enemies in the scene and the fight with them.</summary>
    private Encounter? _encounter;
    private WorldRuntime? _world;
    private DialogueRuntime? _dialogue;
    private SpeakingActor? _conversationActor;
    private int _dialogueSelection;
    private string _dialogueResponse = string.Empty;
    private bool _dialogueOpen;
    private bool _showJournal;

    /// <summary>The fort, opened with F on the surface. Null room id means the corridor.</summary>
    private bool _showFort;

    private int _fortSelection;
    private string? _openFortRoom;

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
    private float _hitstop;

    /// <summary>How far the camera is still owed a shake, and how hard.</summary>
    private float _shake;
    private float _shakeStrength;

    private BillboardRenderer _billboards = null!;

    /// <summary>The weapon in hand, and the swing it is part-way through.</summary>
    private readonly WeaponView _weaponView = new();
    private readonly UiCanvas _ui;

    /// <summary>
    /// Every 2D screen renderer. Built in LoadContent, not in the constructor.
    ///
    /// Two of them — the fort and the weapon — take a <see cref="GraphicsDevice"/> so they can
    /// forge a texture on demand, and MonoGame does not create the device until after the
    /// constructor has run. Building this early captured a null and crashed the first time the
    /// weapon was drawn. Not readonly for that reason.
    /// </summary>
    private UiScreens _screens = null!;
    private readonly CaptureHost _capture;

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
    private float _healthPulse;
    private float _pranaPulse;
    private float _lastHealth;
    private float _lastPrana;

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
    private bool _decisionRecorded;

    /// <summary>The descent in progress, when the loaded world is a mine.</summary>
    private RunRuntime? _run;

    /// <summary>The run that just ended, while its summary is on screen.</summary>
    private RunResult? _runSummary;

    /// <summary>What the last death cost, shown beside the run summary.</summary>
    private SuccessionResult? _succession;

    /// <summary>A trader has been whistled down and is unpacking.</summary>
    private bool _campTraderOpen;
    private int _campSelection;

    /// <summary>The shaft panel is open and a depth is being chosen.</summary>
    private bool _choosingDepth;

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
    private CaveTheme? _cave;

    /// <summary>
    /// Tell the caster which cave it is standing in.
    ///
    /// Called after the session exists rather than alongside the derivation, because starting
    /// a new character replaces the session — and the assignment made before that replacement
    /// went to the object that was about to be thrown away.
    /// </summary>
    private void ApplyCave()
    {
        if (_session is not null) _session.Player.Spells.Cave = _cave;
    }
    private int _depthSelection = 1;

    /// <summary>
    /// True while walking back into a descent that was set aside.
    ///
    /// The one case where a mine's dead must stay dead: the rooms already cleared before the
    /// game was put down are still cleared when it is picked up.
    /// </summary>
    private bool _resumingDescent;

    /// <summary>
    /// Rooms built per segment of mine.
    ///
    /// Small enough that the first one arrives quickly and the work of building the next lands
    /// while the player is busy, large enough that a join is rare.
    /// </summary>
    private const int RoomsPerSegment = 8;

    /// <summary>Seconds left on a click that arrived before the last swing had finished.</summary>
    private float _swingBuffered;

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

    /// <summary>
    /// Seconds since the game started, for anything that moves on its own.
    ///
    /// Deliberately not <c>gameTime.TotalGameTime</c>: a screenshot run advances a fixed number
    /// of frames rather than real time, and a capture has to be reproducible. Accumulating the
    /// same step the rest of the simulation uses keeps <c>--screenshot</c> deterministic.
    /// </summary>
    private float _clock;

    /// <summary>
    /// Logical-to-screen scale. Text is rasterized at this many device pixels per logical
    /// pixel so glyphs land 1:1 on the display instead of being resampled.
    /// </summary>
    private float _uiScalePreference = 1f;

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
        _capture = new CaptureHost(
            ParseOption(args, "--screenshot"),
            ParseOption(args, "--cover"),
            int.TryParse(ParseOption(args, "--warmup"), out var warmup) ? warmup : null);
        _facesPath = ParseOption(args, "--faces");
        _faceOnly = ParseOption(args, "--face");
        _faceSheetScale = Math.Clamp(
            int.TryParse(ParseOption(args, "--face-scale"), out var faceScale) ? faceScale : 2,
            1, 8);

        _forceCrouch = HasArgument(args, "--sneak");

        // Deterministic camera for screenshots, so a change to look or movement can be
        // compared frame against frame instead of described.
        if (float.TryParse(ParseOption(args, "--swing"), out var swing)) _captureSwing = swing;
        if (float.TryParse(ParseOption(args, "--cast"), out var cast)) _captureCast = cast;

        // Opens a screen for a capture, so an interface change can be looked at rather than
        // described. Screenshot mode only.
        _captureScreen = ParseOption(args, "--show");
        _stambhaPreview = HasArgument(args, "--stambha");

        // Commands to run once there is a world to run them against. This is what makes the
        // game inspectable from outside: --yard --exec "goto shaft; look at shaft" --screenshot
        // takes a picture of a thing nobody could previously walk to.
        _consoleScript = ParseOption(args, "--exec");

        // A file of them, for a test that is longer than a command line. Read here and folded
        // into the same queue, so --script and --exec behave identically from then on.
        var scriptFile = ParseOption(args, "--script");
        _scripted = !string.IsNullOrWhiteSpace(scriptFile);
        if (scriptFile is not null)
        {
            // A named script that is not there is a broken invocation, not an empty one. It
            // used to be ignored, so a mistyped path ran the game with no commands at all and
            // exited zero -- a gate that passes by doing nothing.
            if (!File.Exists(scriptFile))
            {
                _scriptMissing = scriptFile;
            }
            else
            {
                var joined = string.Join(';',
                    ConsoleRouter.ReadScript(File.ReadAllLines(scriptFile)));
                _consoleScript = _consoleScript is null ? joined : _consoleScript + ";" + joined;
            }
        }

        // --yard opens on the surface rather than the menu, so the one place the player
        // starts and returns to can be looked at rather than described.
        //
        // A script implies one. Commands run against the world, and the menu has none: the
        // smoke script asked where it was standing and was told "no world", which made the
        // documented invocation unable to pass. Anything that names a scene of its own --
        // a mine, the moodboard, the pillar -- still gets that instead.
        _startOnTheSurface = HasArgument(args, "--yard");
        _moodboard = HasArgument(args, "--moodboard");
        _assetCase = HasArgument(args, "--assets");
        if (_assetCase) _moodboard = true;
        if (int.TryParse(ParseOption(args, "--mine"), out var mineSeed)) _mineSeed = mineSeed;
        if (int.TryParse(ParseOption(args, "--rooms"), out var mineRooms)) _mineRooms = mineRooms;
        if (int.TryParse(ParseOption(args, "--depth"), out var mineDepth)) _mineDepth = mineDepth;

        // Asking for a mine and being shown the title screen is a papercut; --mine means play it.
        if (_mineSeed is not null) _screen = GameScreen.WorldScene;

        if (_consoleScript is not null
            && _mineSeed is null
            && !_moodboard
            && !_stambhaPreview
            && _screen == GameScreen.MainMenu)
            _startOnTheSurface = true;
        if (float.TryParse(ParseOption(args, "--yaw"), out var yaw)) _startYaw = yaw;
        if (float.TryParse(ParseOption(args, "--pitch"), out var pitch)) _startPitch = pitch;
        if (_capture.CoverMode)
        {
            // itch.io wants 630x500 and displays it at 315x250. Rendered at double that so
            // the type survives a high-density screen, and so the same file can be cropped
            // for a banner later without going back to the game.
            _mineSeed ??= 20789;
            _mineDepth = 4;
            _screen = GameScreen.WorldScene;
            _startPitch ??= -0.06f;
        }

        if (_capture.ApplyWindow(_graphics, Window, LogicalWidth, LogicalHeight))
            _borderlessFullscreen = false;

        // A scripted run is a test, not a sitting: it must not seize the display.
        //
        // Capture runs were already windowed, because ApplyWindow above sizes them, but a
        // plain --script run went borderless fullscreen and took over the screen for as long
        // as the gate ran. verify.ps1 now runs two of them on every invocation, so that is
        // twice a build, on top of every ad-hoc script.
        if (HasArgument(args, "--windowed") || !string.IsNullOrWhiteSpace(scriptFile))
        {
            _borderlessFullscreen = false;
            _graphics.PreferredBackBufferWidth = LogicalWidth;
            _graphics.PreferredBackBufferHeight = LogicalHeight;
            _graphics.IsFullScreen = false;
            Window.IsBorderless = false;
        }

        _ui = new UiCanvas(LogicalWidth, LogicalHeight);
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
        // A script and a capture are both tests, and neither is a sitting anybody played.
        // Their recordings are noise in the corpus the playtest exists to produce: the sixty
        // most recent recordings on the server are already mostly gate runs, median seven
        // events each, none of them a complete run. verify.ps1 launches the client twice, so
        // this was two junk uploads per build.
        var capturing = _capture.IsCapturing || _scripted;

        _askingConsent = !capturing
            && !_consent.Asked
            && !string.IsNullOrWhiteSpace(Telemetry.Endpoint);

        if (!_askingConsent && !capturing) _uploader.SendPending(_recorder.FilePath);

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

        var spriteBatch = new SpriteBatch(GraphicsDevice);
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

        if (_scene.LoadCaveShader(Content, "Effects/CaveLighting") is { } shaderFault)
            _assetErrors.Add($"cave lighting: {shaderFault}");

        _white = new Texture2D(GraphicsDevice, 1, 1);
        _white.SetData(new[] { Color.White });

        // Everything the canvas paints with, handed over once. It is constructed before
        // LoadContent runs, so it cannot take these in its constructor.
        _ui.Attach(spriteBatch, _white, _fontSystem, _headingFontSystem);

        // Here rather than in the constructor: the device exists by now. See the field.
        _screens = new UiScreens(_ui, GraphicsDevice);

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
            _scriptQuitWhenDone = true;
            return;
        }

        if (_consoleScript is null) return;

        var statements = ConsoleRouter.SplitStatements(_consoleScript);

        // Checked as a whole before the first one runs. A script that names a command nothing
        // registered is a script that was written against a different build, and finding that
        // out at statement forty means the thirty-nine asserts before it already reported
        // success on a run that was never going to finish.
        _console ??= GameConsole.Build(this);
        var unknown = _console.UnknownCommands(statements);
        if (unknown.Count > 0)
        {
            FailScript($"Unknown command(s): {string.Join(", ", unknown)}. Try 'help'.");
            _scriptQuitWhenDone = true;
            return;
        }

        foreach (var statement in statements)
            _scriptQueue.Enqueue(statement);
    }

    protected override void UnloadContent()
    {
        // --faces returns out of LoadContent before any of this exists, so there is nothing
        // here to release and every line below would throw on a null.
        if (_facesPath is not null) return;

        _capture.Dispose();
        _fontSystem.Dispose();
        _headingFontSystem.Dispose();
        _white.Dispose();
        _primitiveEffect.Dispose();
        _billboards.Dispose();
        StoneTextures.Clear();
        PropTextures.Clear();
        ItemSprites.Clear();
        PortraitForge.Clear();
        // A sitting that ends by closing the window is still a sitting worth reading back --
        // and, now, worth sending. Flushed first so the file on disk is the whole sitting, then
        // offered to the uploader with no in-progress file to skip, because there no longer is
        // one. Three seconds: a player quitting is not made to wait on a network, and anything
        // unsent is still picked up by the next launch if there ever is one.
        _recorder.Flush();
        if (!_capture.IsCapturing && !_scripted)
            _uploader?.SendNow(inProgress: null, TimeSpan.FromSeconds(3));

        _ambientAudio?.Dispose();
        _sfx?.Dispose();
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

    private static float RealSeconds(GameTime gameTime) =>
        MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrameSeconds);

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

        if (_hitstop > 0f) _hitstop = MathF.Max(0f, _hitstop - real);
        if (_shake > 0f) _shake = MathF.Max(0f, _shake - real);

        PumpScript(RealSeconds(gameTime) * _timeScale);
        UpdateWatches();

        // First, and it swallows the frame when it is open: a console you cannot type an S
        // into without walking backwards is not a console.
        UpdateConsole(keyboard);
        if (_consoleOpen)
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
            else if (_paused)
            {
                ResumeFromPause();
            }
            else if (_choosingDepth)
            {
                _choosingDepth = false;
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
    /// <summary>
    /// Turn mouse look on or off.
    ///
    /// Refused outright during a capture, and during a script, for the same reason: nobody is
    /// holding the mouse. Mouse look re-centres the system cursor every frame, so a scripted
    /// run took the cursor away from whoever was using the machine and kept warping it back to
    /// the middle of the game window for as long as the gate ran. A script drives the camera
    /// with `look` and `walk`; the mouse has nothing to contribute and every opportunity to
    /// interfere.
    /// </summary>
    private void SetMouseLook(bool enabled, bool forPanel = false)
    {
        if (_capture.IsCapturing || _scripted) enabled = false;

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
        if (_ui.Scale <= 0f) return Vector2.Zero;

        var viewport = GraphicsDevice.Viewport;
        var offsetX = (viewport.Width - LogicalWidth * _ui.Scale) * 0.5f;
        var offsetY = (viewport.Height - LogicalHeight * _ui.Scale) * 0.5f;
        return new Vector2((mouse.X - offsetX) / _ui.Scale, (mouse.Y - offsetY) / _ui.Scale);
    }

    /// <summary>
    /// The console owns the keyboard while it is open.
    ///
    /// Typing has to be read from key transitions rather than from a text-input event, because
    /// MonoGame's TextInput is not wired here and the rest of the game samples keys through
    /// InputRouter. It is enough for a command line: letters, digits, and the handful of
    /// punctuation a command needs.
    /// </summary>
    private void UpdateConsole(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.OemTilde) || Pressed(keyboard, Keys.Oem8))
        {
            _consoleOpen = !_consoleOpen;
            SetMouseLook(!_consoleOpen, forPanel: true);
            return;
        }

        if (!_consoleOpen) return;

        if (Pressed(keyboard, Keys.Escape))
        {
            _consoleOpen = false;
            SetMouseLook(true);
            return;
        }

        if (Pressed(keyboard, Keys.Enter))
        {
            RunConsole(_consoleInput);
            _consoleInput = string.Empty;
            _consoleHistory = -1;
        }
        else if (Pressed(keyboard, Keys.Back) && _consoleInput.Length > 0)
        {
            _consoleInput = _consoleInput[..^1];
        }
        else if (Pressed(keyboard, Keys.Tab))
        {
            // Completing the command word only. Arguments differ per command and guessing at
            // them would be worse than not offering.
            var candidates = _console?.Complete(_consoleInput) ?? new List<string>();
            if (candidates.Count == 1) _consoleInput = candidates[0] + " ";
            else if (candidates.Count > 1)
                _consoleOutput.Add(new ConsoleLine(string.Join("  ", candidates), ConsoleTone.Info));
        }
        else if (Pressed(keyboard, Keys.Up)) WalkHistory(-1);
        else if (Pressed(keyboard, Keys.Down)) WalkHistory(1);
        else
        {
            foreach (var key in keyboard.GetPressedKeys())
            {
                if (_input.WasDown(key)) continue;

                var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
                var character = CharacterFor(key, shift);
                if (character != '\0' && _consoleInput.Length < 160) _consoleInput += character;
            }
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
        if (_scriptWaitSeconds > 0f)
        {
            _scriptWaitSeconds = MathF.Max(0f, _scriptWaitSeconds - simulatedSeconds);
            return;
        }

        if (_scriptQueue.Count == 0)
        {
            // A script that asked to quit does so once it has run out of things to say.
            if (_scriptQuitWhenDone)
            {
                _scriptQuitWhenDone = false;
                Console.WriteLine(_scriptFailed ? "SCRIPT FAILED" : "SCRIPT PASSED");
                Exit();
            }

            return;
        }

        var statement = _scriptQueue.Dequeue();
        var before = _consoleOutput.Count;
        RunConsole(statement);

        // Echoed to stdout as well as to the overlay: a scripted run has nobody watching the
        // screen, and a command that failed silently reads as one that worked.
        for (var index = before; index < _consoleOutput.Count; index++)
        {
            var line = _consoleOutput[index];
            Console.WriteLine((line.Tone == ConsoleTone.Error ? "[!] " : "    ") + line.Text);
        }
    }

    /// <summary>Re-run the pinned commands, so their answers are current when drawn.</summary>
    private void UpdateWatches()
    {
        _watchOutput.Clear();
        if (_watches.Count == 0 || _console is null) return;

        foreach (var watch in _watches)
            foreach (var line in _console.RunQuiet(watch)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries))
                _watchOutput.Add(line.TrimEnd());
    }

    private readonly List<string> _watchOutput = new();

    private void DrawWatches() => _screens.Console.DrawWatches(_watchOutput);

    private void WalkHistory(int direction)
    {
        var history = _console?.History;
        if (history is null || history.Count == 0) return;

        if (_consoleHistory < 0) _consoleHistory = history.Count;
        _consoleHistory = Math.Clamp(_consoleHistory + direction, 0, history.Count);

        _consoleInput = _consoleHistory >= history.Count ? string.Empty : history[_consoleHistory];
    }

    /// <summary>Run a line and keep the output, bounded so a loop cannot eat the frame.</summary>
    private void RunConsole(string line)
    {
        _console ??= GameConsole.Build(this);

        foreach (var output in _console.Execute(line))
        {
            // A form feed from 'clear' empties the log rather than printing.
            if (output.Text == "")
            {
                _consoleOutput.Clear();
                continue;
            }

            _consoleOutput.Add(output);
        }

        while (_consoleOutput.Count > 200) _consoleOutput.RemoveAt(0);
    }

    /// <summary>What a key types. Only what a command line needs.</summary>
    private static char CharacterFor(Keys key, bool shift)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            var letter = (char)('a' + (key - Keys.A));
            return shift ? char.ToUpperInvariant(letter) : letter;
        }

        if (key is >= Keys.D0 and <= Keys.D9 && !shift) return (char)('0' + (key - Keys.D0));
        if (key is >= Keys.NumPad0 and <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));

        return key switch
        {
            Keys.Space => ' ',
            Keys.OemPeriod or Keys.Decimal => '.',
            Keys.OemMinus or Keys.Subtract => '-',
            Keys.OemQuotes => '"',
            Keys.OemSemicolon => shift ? ':' : ';',
            Keys.OemComma => ',',
            _ => '\0'
        };
    }

    private bool Clicked(MouseState mouse) => _input.Clicked(mouse);

    protected override void Draw(GameTime gameTime)
    {
        ApplyCaptureScreen();
        _capture.BeginFrame(GraphicsDevice);

        _fpsFrames++;
        var elapsed = _fpsClock.Elapsed.TotalSeconds;
        if (elapsed >= 0.5)
        {
            _framesPerSecond = (float)(_fpsFrames / elapsed);
            _fpsFrames = 0;
            _fpsClock.Restart();
        }

        GraphicsDevice.Clear(new Color(9, 15, 25));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _ui.Resize(GraphicsDevice.Viewport, _uiScalePreference);
        UpdateCameraMatrices();

        // The one point per frame where the camera is settled and nothing has drawn yet. The
        // scene renderer takes its whole per-frame context here rather than as six arguments
        // on each of forty draw calls, which is how one of them ends up passing a stale view.
        _scene.Begin(_primitiveEffect, _camera.View, _camera.Projection, _camera.Position,
            _camera.Yaw, _stone, _lights);

        // The question owns the screen until it is answered.
        if (_askingConsent)
        {
            _ui.Begin();
            DrawConsent();
            EndUi();
        }
        else
        {
            switch (_screen)
            {
                case GameScreen.MainMenu:
                    DrawMenu();
                    break;
                case GameScreen.WorldScene:
                    DrawWorldScene();
                    break;
            }
        }

        base.Draw(gameTime);

        _capture.EndFrame(GraphicsDevice,
            hold: _scriptQueue.Count > 0 || _scriptWaitSeconds > 0f,
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

    /// <summary>The game is stopped and the pause screen owns the input.</summary>
    private bool _paused;

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
        if (_consent.Allowed) _uploader?.SendPending(_recorder.FilePath);
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
        _resumingDescent = true;
        _mineRooms = descent.Rooms;
        _mineDepth = descent.Depth;
        _mineSeed = descent.Seed;
        _world = null;

        // A resumed descent is the same cave it was when it was set aside.
        _cave = CaveThemeCatalog.For(descent.Seed, descent.Depth);

        StartSession(_session);
        ApplyCave();

        // Where they were standing, not the mine's entrance.
        _camera.Position = new Vector3(_session.Position.X, _session.Position.Y, _session.Position.Z);
        _camera.Yaw = _session.Yaw;
        _camera.Pitch = _session.Pitch;
        _camera.StandingEyeY = _session.Position.Y;

        _run?.Resume(descent);
        _resumingDescent = false;
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

    /// <summary>
    /// Drop into a world, generated or authored.
    ///
    /// Both paths go through here because the world has to be discarded and rebuilt when the
    /// kind of world changes. Leaving the old one in place is how "Start New Game" after a
    /// descent used to hand back the mine you had just left.
    /// </summary>
    private void EnterWorld(int? mineSeed, bool newCharacter = false, int tier = 1)
    {
        _mineSeed = mineSeed;

        // How much mine is built at a time. There is always another segment underneath: a
        // level you can finish ends the run for you, and pressing on stops being a risk the
        // moment the game is the one deciding when to stop.
        _mineRooms = RoomsPerSegment;
        _mineDepth = Math.Clamp(tier, MineEntry.MinTier, MineEntry.MaxTier);

        // One derivation, read by the renderer for its palette and by the caster for its
        // resistances. Neither stores it, so they cannot disagree with the shaft screen.
        _cave = mineSeed is { } seed ? CaveThemeCatalog.For(seed, _mineDepth) : null;

        _world = null;
        _run = null;
        _runSummary = null;

        var session = newCharacter || _session is null ? GameSession.NewGame() : _session;
        StartSession(session);
        ApplyCave();
        ResetCamera();

        _menuStatus = string.Empty;
        _screen = GameScreen.WorldScene;
        SetMouseLook(true);
    }

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
    /// The trader at a cleared room's exit.
    ///
    /// Everything here is priced in the pot, because down a mine the pot is the purse: what is
    /// spent was never carried out, and what is sold is at risk exactly like the rest. That is
    /// what stops a camp being a way to bank early.
    /// </summary>
    private void UpdateCampTrader(KeyboardState keyboard, MouseState mouse)
    {
        if (_session is null || _run is null) { _campTraderOpen = false; return; }

        var run = _run.Run;
        var rows = CampRowCount();

        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.T))
        {
            _campTraderOpen = false;
            SetMouseLook(true);
            return;
        }

        if (Pressed(keyboard, Keys.Up)) _campSelection = (_campSelection + rows - 1) % rows;
        if (Pressed(keyboard, Keys.Down)) _campSelection = (_campSelection + 1) % rows;

        var chosen = false;
        var pointer = LogicalMouse(mouse);
        for (var index = 0; index < rows; index++)
        {
            if (!UiLayout.CampRow(index).Contains((int)pointer.X, (int)pointer.Y)) continue;

            _campSelection = index;
            chosen = Clicked(mouse);
            break;
        }

        if (!chosen && !Pressed(keyboard, Keys.Enter) && !Pressed(keyboard, Keys.Space)) return;

        if (_campSelection == 0)
        {
            var paid = CampTrader.SellLoot(_session.Player.Inventory, run);
            _session.ShowToast(paid > 0
                ? $"They take the lot. +{paid} stones, and the pot is {run.Pending}."
                : "Nothing in your pack they want.");

            if (paid > 0)
                _recorder.Record(PlayEventKind.LootSold, "loot", paid, run.Pending,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana);

            return;
        }

        var good = CampTrader.Stock[_campSelection - 1];
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

        _choosingDepth = true;
        _depthSelection = Math.Clamp(
            MineEntry.DeepestAffordable(_session.Player.Inventory), 1, MineEntry.MaxTier);

        // A fresh set of offers each time the shaft is opened. Backing out and looking again
        // re-rolls them, which is deliberate: it is free, it costs no stones, and a player who
        // does not like any of today's caves should be able to come back tomorrow.
        // Seeds chosen so the five offers are five different caves.
        //
        // They used to be rolled independently, and a theme is a pure function of its seed and
        // tier, so the board collided constantly: five themes into four free slots leaves the
        // free granite workings duplicated by a paid tier about three times in five. A shaft
        // offering "Tier 1, free: cold grey granite" beside "Tier 5, 80 stones: cold grey
        // granite" is asking the player to pay eighty stones for the same tactical problem,
        // which is the opposite of the choice this screen exists to present.
        //
        // The seed is re-rolled rather than the theme being overridden, because CaveThemeCatalog
        // .For is the single authority the shaft, the generator and the renderer all read. Fix
        // it here and the three still agree; override the label and they do not.
        //
        // Tier one is always granite by design, so it is seeded first and claims that theme.
        var roll = new Random(Environment.TickCount);
        var taken = new HashSet<string>(StringComparer.Ordinal);

        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        {
            // Bounded: with more tiers than themes a repeat is unavoidable, and a shaft that
            // hangs looking for the impossible is far worse than one that repeats a cave.
            for (var attempt = 0; attempt < 24; attempt++)
            {
                _shaftSeeds[tier] = roll.Next(int.MinValue, int.MaxValue);
                if (taken.Add(CaveThemeCatalog.For(_shaftSeeds[tier], tier).Id)) break;
            }
        }

        SetMouseLook(false, forPanel: true);
    }

    private void UpdateDepthChoice(KeyboardState keyboard, MouseState mouse)
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

        var chosen = false;
        var pointer = LogicalMouse(mouse);
        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        {
            if (!UiLayout.DepthRow(tier).Contains((int)pointer.X, (int)pointer.Y)) continue;

            _depthSelection = tier;
            chosen = Clicked(mouse);
            break;
        }

        if (!chosen && !Pressed(keyboard, Keys.Enter) && !Pressed(keyboard, Keys.Space)) return;

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

        EnterMine(returning ? fallen!.MineSeed : _shaftSeeds[_depthSelection], _depthSelection);

        _session.ShowToast(returning
            ? $"The same shaft. {fallen!.Name} is still down there, in room {fallen.RoomIndex}."
            : cost > 0
                ? $"{cost} stones, and the shaft opens. Tier {_depthSelection}."
                : "The picked-over workings. They cost nothing and pay like it.");
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
        _overlay.PauseSelection = 0;
        SetMouseLook(false, forPanel: true);
    }

    private void ResumeFromPause()
    {
        _paused = false;
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
        if (!_overlay.StepPause(_input, keyboard, mouse, LogicalMouse(mouse), items.Length, inRun))
            return;

        switch (items[_overlay.PauseSelection])
        {
            case "Resume":
                ResumeFromPause();
                break;

            case "Settings":
                _overlay.OpenSettings();
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
            new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z),
            _camera.Yaw, _camera.Pitch);

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
        _overlay.ShowSettings = false;
        SetMouseLook(false);
        _screen = GameScreen.MainMenu;
        _overlay.MenuSelection = 0;
    }

    private void UpdateGameScreen(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        // M was a second silent way out of a run. It opens the same pause screen now.
        if (Pressed(keyboard, Keys.M)) Pause();

        if (_paused)
        {
            UpdatePause(keyboard, mouse);
            return;
        }

        if (Pressed(keyboard, Keys.F1))
        {
            if (_showHelp) ClosePanels();
            else { _showHelp = true; SetMouseLook(false, forPanel: true); }
        }

        // A screen with no way out but a function key is a screen some players will be stuck
        // on. Anywhere on the controls overlay closes it.
        if (_showHelp && Clicked(mouse)) ClosePanels();
        // The run summary owns the screen: nothing else is reachable until it is dismissed.
        if (_runSummary is not null)
        {
            var onTheWayUp = UiLayout.SummaryButton
                .Contains((int)LogicalMouse(mouse).X, (int)LogicalMouse(mouse).Y);

            if ((onTheWayUp && Clicked(mouse))
                || Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Escape)
                || Pressed(keyboard, Keys.Space))
            {
                // Up into the yard rather than out to a menu. A loop that ends at a title
                // screen is not a loop; the whole point of the surface is having somewhere
                // to arrive with what you carried out.
                ReturnToTheSurface();
            }

            return;
        }

        if (_campTraderOpen)
        {
            UpdateCampTrader(keyboard, mouse);
            return;
        }

        if (_choosingDepth)
        {
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

            if (Pressed(keyboard, Keys.C))
            {
                var result = decision.Camp();
                _recorder.Record(PlayEventKind.Camped, $"after {result.RoomsCleared} rooms",
                    result.StonesCarriedOut, 0f, _session.Player.Vitals.Health);
                EndRun(result);
                return;
            }

            // A trader can be whistled for at the same moment, because what is in their pack
            // is information the press-on choice needs.
            // No null check on the session here: the enclosing branch already made it, and
            // repeating it inside an && told the compiler the field might be null on the way
            // past, which cost a warning further down for nothing.
            if (Pressed(keyboard, Keys.T) && decision.Run.CanCallTrader)
            {
                var fare = decision.Run.TraderCallCost;
                if (decision.Run.TrySpend(fare))
                {
                    decision.Run.NoteTraderCalled();
                    _campTraderOpen = true;
                    _campSelection = 0;
                    SetMouseLook(false, forPanel: true);

                    _recorder.Record(PlayEventKind.TraderCalled,
                        $"call {decision.Run.TradersCalled}", fare, decision.Run.Pending,
                        _session.Player.Vitals.Health, _session.Player.Vitals.Prana);

                    _session.ShowToast($"{fare} stones, and somebody comes down the ladder.");
                }

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

        // Only above ground. The fort is what a run is for, not something to read halfway
        // down a shaft with a door waiting.
        if (Pressed(keyboard, Keys.F) && OnTheSurface)
        {
            if (_showFort) ClosePanels();
            else
            {
                _showFort = true;
                _showJournal = false;
                _showCharacter = false;
                _openFortRoom = null;
                _fortSelection = 0;
                SetMouseLook(false, forPanel: true);
            }
        }
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

        if (_showFort)
        {
            UpdateFort(keyboard, mouse);
            return;
        }

        if (_showCharacter)
        {
            UpdateInventory(keyboard);
            return;
        }
        if (Pressed(keyboard, Keys.F2))
        {
            _overlay.ShowSettings = !_overlay.ShowSettings;
            if (_overlay.ShowSettings) SetMouseLook(false, forPanel: true);
        }

        if (_overlay.ShowSettings)
        {
            ApplySettings(_overlay.StepSettings(_input, keyboard, LogicalMouse(mouse)));
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
        if (!AnyPanelOpen) UpdateCamera(gameTime, keyboard, mouse);

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
    private bool AnyPanelOpen =>
        _dialogueOpen || _showShop || _showJournal || _showCharacter || _showHelp || _showFort
        || _overlay.ShowSettings || _paused || _choosingDepth || _campTraderOpen
        || _runSummary is not null;

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
        _overlay.ShowSettings = false;
        _showFort = false;
        _openFortRoom = null;

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
            var actor = _dialogue?.FindActor(_session.Position, _camera.Yaw);
            if (actor is not null) TryPickpocket(actor);
        }

        if (Pressed(keyboard, Keys.B))
        {
            var actor = _dialogue?.FindActor(_session.Position, _camera.Yaw);
            if (actor is not null && actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                && _shop is not null)
            {
                _showShop = true;
                _shopSelection = 0;
                SetMouseLook(false);
            }
        }

        if (Pressed(keyboard, Keys.E)) Interact();

        UpdateCombat(gameTime, keyboard);
    }

    /// <summary>
    /// Talk, open, take — whatever the crosshair is on. What the E key does.
    ///
    /// Its own method so a script can call it. Opening a door is the single most important
    /// interaction in the game and had no test that could reach it: the console could walk and
    /// teleport but never press a key, so no gate could tell a door that opens from one that
    /// does not.
    /// </summary>
    private void Interact()
    {
        var player = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);
        var actor = _dialogue?.FindActor(player, _camera.Yaw);
        if (actor is not null)
        {
            OpenDialogue(actor);
            return;
        }

        if (_world is null) return;

        var fixture = OnTheSurface ? Surface.FixtureAt(player) : SurfaceFixture.None;
        var pickup = FindPickup(player, _camera.Yaw);

        if (fixture != SurfaceFixture.None)
        {
            UseFixture(fixture);
        }
        else if (pickup is not null)
        {
            TakePickup(pickup);
        }
        else if (_run is { BarsTheWay: true } && _world.FindDoor(player, _camera.Yaw) is not null)
        {
            _session.ShowToast("Not while something in here is still moving.");
        }
        else
        {
            TryOpenDoorAhead(player);
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
        _coach.Tick(step);
        TickVitalPulses(step);
        SampleStance(step);
        _encounter.Update(step, _camera.Position, _camera.Yaw);
        if (_world is not null) _run?.Update(_world, _camera.Position, _encounter);
        _weaponView.Update(step, IsMoving(keyboard), _session.Player.Combat.IsBlocking);

        // Only while the pointer is captured, so a click that is reclaiming the mouse does
        // not also swing the sword.
        if (!_mouseLook || _showHelp) return;

        var mouse = _input.CurrentMouse;
        _session.Player.Combat.SetBlocking(mouse.RightButton == ButtonState.Pressed);

        // A click that arrives a fraction early is remembered rather than thrown away.
        //
        // The recordings are blunt about this: between twenty-nine and sixty clicks a run did
        // nothing at all, because a sword swings every 0.45 seconds and people press faster
        // than that. A click that produces no swing and no sound reads as the game not
        // listening, so the player presses harder, and the log fills with nothing.
        if (_swingBuffered > 0f) _swingBuffered -= step;

        // Regardless of whether anything is being aimed at.
        //
        // This used to require a focused enemy, which meant the fix above only applied to
        // somebody already in a fight -- and the player who needed it most was not. The
        // alpha's one outside player spent an hour swinging at a door in an empty corridor:
        // no enemy, so no buffering, so every click that arrived inside the 0.45s cooldown
        // did nothing, made no sound and moved nothing. Five of them are in their recording
        // as "too soon", and the rest of that hour is the behaviour the comment above
        // predicts -- pressing harder at a game that appears not to be listening.
        //
        // A swing at air is a legitimate thing to ask for. It already works when the weapon
        // is ready; it should not silently stop working because the weapon is not.
        if (Clicked(mouse) && !_session.Player.Combat.IsReady)
            _swingBuffered = SwingBufferSeconds;

        var releaseBuffered = _swingBuffered > 0f && _session.Player.Combat.IsReady;
        if (releaseBuffered) _swingBuffered = 0f;

        if (Clicked(mouse) || releaseBuffered)
        {
            // Only a real click talks to people. A released buffer is a swing the player asked
            // for a moment ago and nothing else — letting it fall through here would open a
            // shop with no click behind it, which is exactly the kind of ghost input this
            // change exists to remove rather than add.
            var actor = releaseBuffered
                ? null
                : _dialogue?.FindActor(
                    new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z),
                    _camera.Yaw);

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

            // A click that never became a swing is recorded as what it was.
            //
            // Cooldown and exhaustion were being folded in with misses, so every impatient
            // click counted against the hit rate. A sword swings every 0.45 seconds; six
            // sessions of "melee lands 28%" may be mostly mashing.
            var struck = _encounter.Focused;
            if (outcome.Result is AttackResult.OnCooldown or AttackResult.Exhausted)
            {
                // A click that was buffered is not a refused click: it fires the moment the
                // weapon is free, a median 0.17s later. Recording it as balked put 297 clicks
                // in the log as lost that the player got, which is the log describing a
                // failure that did not happen -- the same shape as a burning enemy whose
                // nameplate stopped saying so.
                if (_swingBuffered <= 0f)
                    _recorder.Record(PlayEventKind.MeleeBalked,
                        outcome.Result == AttackResult.OnCooldown ? "too soon" : "no stamina",
                        0f, 0f, _session.Player.Vitals.Health, _session.Player.Vitals.Prana);
            }
            else
            {
                _recorder.Record(PlayEventKind.MeleeSwing,
                    _session.Player.Combat.ActiveWeapon.DisplayName,
                    outcome.Damage, outcome.Result == AttackResult.Hit ? 1f : 0f,
                    _session.Player.Vitals.Health, _session.Player.Vitals.Prana,
                    struck?.Archetype.DisplayName ?? string.Empty,
                    struck is null ? 0f : _encounter.PlayerPosition.FlatDistanceTo(struck.Position));
            }

            // The arm moves whenever the swing actually happened — a hit and a miss look the
            // same from behind the weapon, which is what makes missing feel like missing
            // rather than like the button not working.
            if (outcome.Swung)
                _weaponView.Swing(_session.Player.Combat.ActiveWeapon,
                    _session.Player.Combat.WeaponSweeps);
            ReportAttack(outcome);
        }
        if (Pressed(keyboard, Keys.Q))
        {
            var cast = _encounter.PlayerCast(_camera.Position, _camera.Yaw, _camera.Forward);
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
                    _showShop = true;
                    _shopSelection = 0;
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
            var row = UiLayout.DialogueTopic(index);
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
    /// <summary>How heavy the equipped weapon sounds. Two-handed is slow and low; a bow is neither.</summary>
    private float SwingWeight() => _session?.Player.Equipment.Weapon.Class switch
    {
        WeaponClass.TwoHanded => 0.9f,
        WeaponClass.Ranged => 0.15f,
        _ => 0.35f
    };

    private void ReportAttack(AttackOutcome outcome)
    {
        if (outcome.Result == AttackResult.Exhausted)
        {
            _session?.ShowToast("Too exhausted.");
            _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        if (outcome.Result == AttackResult.NoAmmunition)
        {
            _session?.ShowToast("Out of arrows.");
            _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        // The swing plays on every swing, landed or not, because it is the sound of the input
        // being received. A game that is silent when you miss feels unresponsive rather than
        // feeling like you missed.
        //
        // Weight comes from the weapon's class, so a greatsword drops in pitch and gains
        // volume against a knife without either being written out separately. That is the
        // whole reason the sounds take a weight rather than being one file each.
        if (outcome.Swung)
        {
            _sfx?.Play(Sfx.Swing, SwingWeight(), volumeScale: 0.75f);

            // A swing puts the weapon in the way. Two-handed costs the most, blunt some, a
            // blade nothing — which is the whole reason a mage would carry a blade.
            _session?.Player.Spells.Encumber(
                _session.Player.Equipment.Weapon.CastDelaySeconds);
        }

        if (outcome.Result != AttackResult.Hit) return;

        // Landing is the sound and the freeze together. Weight comes from the damage actually
        // dealt, so a greatsword lands heavier than a knife without either being special-cased.
        var weight = MathHelper.Clamp(outcome.Damage / 45f, 0.25f, 1f);
        if (outcome.WasOpening) weight = MathF.Min(1f, weight * 1.4f);

        _sfx?.Play(Sfx.HitFlesh, weight);
        Impact(weight);
    }

    private void ReportCast(CastOutcome outcome)
    {
        if (_session is null) return;

        if (outcome.Result == CastResult.Landed || outcome.Result == CastResult.Missed)
            _sfx?.Play(Sfx.Cast, 0.5f, volumeScale: 0.85f);

        switch (outcome.Result)
        {
            case CastResult.NoCharge:
                _session.ShowToast("No prana, and no jiva stone to draw on.");
                _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
                break;
            case CastResult.Shouldering:
                _session.ShowToast($"Both hands are on the {_session.Player.Equipment.Weapon.DisplayName}.");
                _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
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
    /// <summary>
    /// Walking the fort's corridor, and stepping into a room.
    ///
    /// Escape steps back one level rather than closing everything: from a room to the
    /// corridor, from the corridor out. A single Escape that dumps the player onto the yard
    /// from three rooms deep is the thing that makes a menu feel like a trap.
    /// </summary>
    private void UpdateFort(KeyboardState keyboard, MouseState mouse)
    {
        if (_session is null) { _showFort = false; return; }

        if (Pressed(keyboard, Keys.Escape))
        {
            if (_openFortRoom is not null) _openFortRoom = null;
            else ClosePanels();
            return;
        }

        if (_openFortRoom is not null) return;

        var rooms = FortRoster.All;

        if (Pressed(keyboard, Keys.Up))
            _fortSelection = Math.Max(0, _fortSelection - 1);
        if (Pressed(keyboard, Keys.Down))
            _fortSelection = Math.Min(rooms.Count - 1, _fortSelection + 1);

        var pointer = LogicalMouse(mouse);
        var clicked = false;

        for (var index = 0; index < rooms.Count; index++)
        {
            if (!FortRenderer.DoorRow(index).Contains((int)pointer.X, (int)pointer.Y)) continue;

            _fortSelection = index;
            clicked = Clicked(mouse);
            break;
        }

        if (!clicked && !Pressed(keyboard, Keys.Enter) && !Pressed(keyboard, Keys.Space)) return;

        var room = rooms[_fortSelection];
        var rank = _session.Player.Legacy.Service.Rank;

        if (!room.IsOpen(rank))
        {
            _session.ShowToast(
                $"That door is shut to a {Ranks.TitleOf(rank)}. It wants a {Ranks.LabelOf(room.RequiredRank)}.");
            _sfx?.Play(Sfx.Denied, 0.2f, volumeScale: 0.7f);
            return;
        }

        _openFortRoom = room.Id;
        _sfx?.Play(Sfx.Door, 0.4f, volumeScale: 0.7f);
    }

    private void UpdateInventory(KeyboardState keyboard)
    {
        if (_session is null) return;

        UpdateStoneInput(keyboard);

        var items = _session.Player.Inventory.Items;
        if (items.Count == 0)
        {
            _inventorySelection = 0;
            return;
        }

        _inventorySelection = Math.Clamp(_inventorySelection, 0, items.Count - 1);

        // Left and right walk the row; up and down step between rows. A grid navigated as a
        // list is a grid that fights the player's eyes.
        if (Pressed(keyboard, Keys.Left) || Pressed(keyboard, Keys.A))
            _inventorySelection = (_inventorySelection + items.Count - 1) % items.Count;
        if (Pressed(keyboard, Keys.Right) || Pressed(keyboard, Keys.D))
            _inventorySelection = (_inventorySelection + 1) % items.Count;
        if (Pressed(keyboard, Keys.Up) || Pressed(keyboard, Keys.W))
            _inventorySelection = (_inventorySelection + items.Count - UiLayout.InventoryColumns) % items.Count;
        if (Pressed(keyboard, Keys.Down) || Pressed(keyboard, Keys.S))
            _inventorySelection = (_inventorySelection + UiLayout.InventoryColumns) % items.Count;

        var mouse = _input.CurrentMouse;
        var pointer = LogicalMouse(mouse);
        var hovered = -1;
        for (var index = 0; index < items.Count && index < UiLayout.InventoryRows; index++)
            if (UiLayout.InventoryTile(index).Contains((int)pointer.X, (int)pointer.Y))
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

        NoteIfStuck(where);
    }

    /// <summary>
    /// Say so, in the log, when a run stops going anywhere.
    ///
    /// Read the stance samples of a session that went nowhere and the story is there, but only
    /// to somebody already looking for it: the shape of the alpha's one outside player being
    /// stuck for 110 minutes was eight room-to-corridor transitions and then a ninety-two
    /// minute silence, which took an afternoon and a hand-written script to see. An absence is
    /// the hardest thing to spot in a log and the easiest to mistake for boredom.
    ///
    /// Four minutes before the first one, then every five, so a long fight or a careful player
    /// reading a panel is never called stuck, and a genuinely stuck session accumulates a
    /// visible row of them rather than one line that could be a blip.
    /// </summary>
    private void NoteIfStuck(string where)
    {
        var idle = _recorder.SinceProgress;
        if (idle < TimeSpan.FromMinutes(4)) return;

        var now = DateTime.UtcNow;
        if (now - _lastStuckNote < TimeSpan.FromMinutes(5)) return;

        _lastStuckNote = now;
        _recorder.Record(PlayEventKind.Stuck, where, (float)idle.TotalMinutes, 0f,
            _session?.Player.Vitals.Health ?? 0f, _session?.Player.Vitals.Prana ?? 0f);
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
    /// Freeze the world briefly, and shove the camera.
    ///
    /// Called from wherever a blow lands. <paramref name="weight"/> is 0 for a graze and 1 for
    /// a killing blow on something large, and everything else scales off it — a light hit gets
    /// two frames and a nudge, a heavy one gets six and a jolt.
    /// </summary>
    private void Impact(float weight)
    {
        var w = MathHelper.Clamp(weight, 0f, 1f);

        // Take the longer of the two rather than adding, or a flurry stacks into a lockup.
        _hitstop = MathF.Max(_hitstop, 0.030f + 0.055f * w);

        _shake = MathF.Max(_shake, 0.10f + 0.14f * w);
        _shakeStrength = MathF.Max(_shakeStrength, 0.0022f + 0.0075f * w);
    }

    /// <summary>
    /// The rotational offset a running shake adds to the view, in yaw and pitch.
    ///
    /// Rotation rather than translation, because the camera is the player's head: moving the
    /// eye through the world clips it into geometry, and turning it does not. Two frequencies
    /// that do not divide into each other, so a long shake never repeats visibly.
    ///
    /// Returned rather than applied. Adding it to <c>_camera.Yaw</c> would be simpler and would
    /// be a bug: those fields persist, so every shake would leave the player aiming somewhere
    /// slightly different from where they were, and a long fight would walk the view away by
    /// degrees with nobody able to say why.
    /// </summary>
    private (float Yaw, float Pitch) ShakeOffset()
    {
        if (_shake <= 0f) return (0f, 0f);

        var falloff = _shake * _shake;
        var strength = _shakeStrength * falloff * 60f;

        return (MathF.Sin(_clock * 71f) * strength,
                MathF.Sin(_clock * 53f) * strength * 0.75f);
    }

    /// <summary>Wire the session's own good news to a sound.</summary>
    private void WatchSessionForTheFeel(GameSession session)
    {
        session.Player.Vitals.LevelGained += _ => _sfx?.Play(Sfx.Chime, 0.5f);
    }

    /// <summary>Wire the encounter's events to what the player hears and feels.</summary>
    private void WatchForTheFeel(Encounter encounter)
    {
        encounter.EnemyDefeated += enemy =>
        {
            _sfx?.Play(Sfx.Death, Weight(enemy));
            Impact(0.85f);
        };

        encounter.SpellLanded += (_, _, _) =>
        {
            _sfx?.Play(Sfx.HitFlesh, 0.45f, volumeScale: 0.8f);
            Impact(0.35f);
        };

        encounter.PlayerStruck += (damage, guarded) =>
        {
            var weight = MathHelper.Clamp(damage / 30f, 0.2f, 1f);

            _sfx?.Play(guarded ? Sfx.Block : Sfx.Hurt, weight);

            // Being hit shakes harder than hitting. The player should not have to read the
            // health bar to know something went wrong.
            Impact(guarded ? weight * 0.5f : MathF.Min(1f, weight * 1.15f));
        };

        // Bigger things land heavier, which is most of what makes a pishacha feel like one.
        static float Weight(Enemy enemy) =>
            MathHelper.Clamp(enemy.Archetype.MaxHealth / 260f, 0.35f, 1f);
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

        WatchForTheFeel(encounter);
        WatchSessionForTheFeel(session);
    }

    /// <summary>Start the ledger for this descent, if the loaded world is a mine at all.</summary>
    /// <summary>The stream that decides what a cleared room gives up. Seeded from the mine.</summary>
    private Random _stoneDrops = new(0);

    /// <summary>
    /// A cleared room sometimes gives up a stone.
    ///
    /// Not every room, because a stone in every room means the sockets are full by room three
    /// and the rest of the descent has no decisions left in it. Not payout-scaled either: a
    /// stone is variety rather than reward, and tying it to depth would make the deep rooms
    /// the only ones worth clearing for reasons that have nothing to do with the stones.
    /// </summary>
    private void OfferStone()
    {
        if (_session is null || _run is null) return;
        if (_stoneDrops.NextDouble() > StoneDropChance) return;

        var available = StoneCatalog.AvailableAt(_mineDepth);
        if (available.Count == 0) return;

        var stone = available[_stoneDrops.Next(available.Count)];
        _session.Player.Stones.Found(stone.Id);

        _session.ShowToast($"{stone.DisplayName} found.  {stone.Description}");
        _sfx?.Play(Sfx.Chime, 0.4f);
        _coach.Teach(Lessons.Stones, Lessons.TextOf(Lessons.Stones));
    }

    /// <summary>Roughly one room in two.</summary>
    private const double StoneDropChance = 0.5;

    private void StartRun()
    {
        _run = null;
        _runSummary = null;

        if (_world is null || _mineSeed is not { } seed) return;
        if (_world.Manifest.Rooms.Count < 2) return;

        _run = new RunRuntime(_world.Manifest, seed, _mineDepth, _mineRooms);

        // Stones are found below and never carried down. Cleared on entry rather than on exit,
        // because a run can end in ways nobody gets to run code for — dying, quitting, closing
        // the window — and only clearing here cannot leave last run's stones in the sockets.
        _session?.Player.Stones.ClearForDescent();

        // Bearer's Mark: one more stone than was bought, every descent. Given on entry rather
        // than held in the pack, so it cannot be stockpiled by starting runs and backing out.
        if (_session?.Player.Legacy.Has(AmuletEffect.Bearer) == true)
            _session.Player.Inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1,
                SoulCrystals.ItemKind);

        // A deterministic stream per mine, so the same seed gives the same stones. A run worth
        // reporting can be asked for again exactly, which the recorder depends on.
        _stoneDrops = new Random(seed * 397 + _mineDepth);
        _decisionRecorded = false;

        _recorder.Record(PlayEventKind.RunStarted, _world.Manifest.Id, seed, _mineDepth,
            _session?.Player.Vitals.Health ?? 0f);

        _run.RoomEntered += room =>
        {
            _session?.Player.Combat.EnterRoom();

            _recorder.Record(PlayEventKind.RoomEntered,
                $"room {room}", room, 0f, _session?.Player.Vitals.Health ?? 0f,
                _session?.Player.Vitals.Prana ?? 0f);

            // The door has just shut and the floor is opening. Both halves of that are new,
            // and neither is obvious from watching it happen once.
            _coach.Teach(Lessons.FirstRoom, Lessons.TextOf(Lessons.FirstRoom));
            _coach.Teach(Lessons.Rising, Lessons.TextOf(Lessons.Rising));
        };

        _run.RoomCleared += paid =>
        {
            _session?.ShowToast($"Room clear.  +{paid} stones held  ({_run.Run.Pending} at risk)");

            // The pot growing is the thing the whole loop turns on, so it gets its own sound
            // rather than sharing the kill that happened to end the room.
            _sfx?.Play(Sfx.Coin, MathHelper.Clamp(paid / 12f, 0.3f, 1f));
            _recorder.Record(PlayEventKind.RoomCleared, $"room {_run.DeepestRoom}", paid,
                _run.Run.Pending, _session?.Player.Vitals.Health ?? 0f,
                _session?.Player.Vitals.Prana ?? 0f);

            OfferStone();
        };
    }

    /// <summary>
    /// Put the run away and show what it was worth.
    ///
    /// Camping pays out here rather than in the domain because this is where the inventory
    /// lives; the ledger's job ends at deciding the number.
    /// </summary>
    /// <summary>What the run that just ended earned permanently, for the summary screen.</summary>
    private IReadOnlyList<string> _earnedAmulets = Array.Empty<string>();

    private void EndRun(RunResult result)
    {
        _runSummary = result;
        if (result.Survived) _succession = null;
        SetMouseLook(false, forPanel: true);

        // The ratchet, and the reason it is recorded here rather than on a successful bank:
        // amulets are earned by going deeper than the order ever has, whether the person who
        // went there came back or not. A run that ends in a corpse two rooms past the previous
        // best still pays, which is the whole question this iteration exists to answer.
        _earnedAmulets = _session is null
            ? Array.Empty<string>()
            : _session.Player.Legacy.RecordDepth(result.RoomsCleared);

        // Standing is read from what the order has actually done, and only a banked run counts
        // toward it. The order is not impressed by how deep somebody got if the stones stayed
        // down there — the promise that a lost run still pays is amulets, deliberately separate.
        if (_session is not null && _session.Player.Legacy.Service.Record(result))
        {
            var rank = _session.Player.Legacy.Service;
            _session.ShowToast($"The order raises you. You are {Ranks.LabelOf(rank.Rank)}.");
            _sfx?.Play(Sfx.Chime, 0.85f);
        }

        foreach (var id in _earnedAmulets)
        {
            _session?.ShowToast($"{AmuletCatalog.Find(id)?.DisplayName} — kept for good.");
            _sfx?.Play(Sfx.Chime, 0.7f);
        }

        // A descent is a supply run for the stall as much as for the player. Restocking here
        // is also what keeps a death from being unrecoverable: half the pack is gone, and the
        // shelf that could replace it has to have something on it.
        //
        // The sale is written to the save as a looted object so that it survives a reload, so
        // restocking has to unwrite it. Clearing only the shop's own set looked right and did
        // nothing: LoadShop re-reads those marks on every descent and every load, so the
        // shelf emptied permanently the first time the player went back down.
        RestockTheStall();

        _coach.Teach(result.Survived ? Lessons.Banked : Lessons.Died,
            Lessons.TextOf(result.Survived ? Lessons.Banked : Lessons.Died));

        if (!result.Survived && result.StonesLost > 0)
            _coach.Teach(Lessons.Body, Lessons.TextOf(Lessons.Body));

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

        _camera.Position = new Vector3(_session.Position.X, _session.Position.Y, _session.Position.Z);
        _camera.Yaw = _session.Yaw;
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
        _ui.Resize(GraphicsDevice.Viewport, _uiScalePreference);
        _camera.SetProjection(GraphicsDevice.Viewport.AspectRatio);
    }

    /// <summary>Roughly a pace. Shorter crouching, longer at a sprint.</summary>
    private const float StrideMetres = 1.9f;

    /// <summary>
    /// Advance the stride by how far the body actually moved, and step when it comes due.
    ///
    /// Deliberately silent outside the world scene, and silent while airborne — a player who
    /// jumps should not be taking paces in mid-air, and that is exactly what a distance-based
    /// pacer does if nobody stops it.
    /// </summary>
    private void Stride(float metres, KeyboardState keyboard)
    {
        if (_screen != GameScreen.WorldScene || !_camera.Grounded) return;

        _stride += metres;

        var sprinting = keyboard.IsKeyDown(Keys.LeftShift);
        var length = StrideMetres * (_camera.Crouching ? 0.72f : sprinting ? 1.25f : 1f);
        if (_stride < length) return;

        _stride = 0f;

        // Quiet, and quieter still when crouching: the sneak is a promise the audio has to
        // keep, or the stealth read is a lie the moment anybody has headphones on.
        _sfx?.Play(Sfx.Step, sprinting ? 0.55f : 0.3f,
            volumeScale: _camera.Crouching ? 0.28f : 0.5f);
    }

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

        var seconds = StepSeconds(gameTime);

        // A scripted walk is held here rather than faked further down, so it goes through the
        // same collision callback and the same grounding as the key does. A route that a wall
        // blocks must block this too, or the test proves nothing.
        var scriptedWalk = _scriptWalkSeconds > 0f;
        if (scriptedWalk) _scriptWalkSeconds = MathF.Max(0f, _scriptWalkSeconds - seconds);

        var moved = _camera.Step(
            seconds,
            new WalkInput(
                Forward: keyboard.IsKeyDown(Keys.W) || scriptedWalk,
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

    private void LoadWorldManifest()
    {
        if (_world is not null) return;

        if (_mineSeed is { } seed)
        {
            var manifest = MineGenerator.Generate(seed, _mineRooms, _mineDepth);

            // A fresh descent finds the mine as it was, not as the last one left it.
            //
            // Succession sends a successor back into the mine that killed their predecessor,
            // and the dead were staying dead: a recorded run cleared eight rooms for five
            // kills and banked thirty-six stones, because seven of the eight were already
            // empty when it walked in. Going back for a body has to be a descent, not a walk.
            if (!_resumingDescent)
                _session?.Player.World.ForgetKilledIn(manifest.Id);

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
    /// Put the last Bhagiratha's cache into the mine that killed them.
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
                ? "A Bhagiratha's Cache"
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

        // Nobody is standing about in a generated mine.
        //
        // The dialogue manifest carries its own actor positions, authored against the
        // Northwatch scene, and it was loaded on entering any world at all. Those fixed
        // coordinates then landed wherever they happened to land inside a cave, so Mara and
        // Vesa were waiting underground offering to talk about the old road -- the pivot left
        // them behind and nothing ever told them to go home.
        //
        // The yard is built in code and has its own trader, so a descent needs no actors at
        // all. Cleared rather than left stale, because a mine entered from the surface would
        // otherwise inherit whoever was loaded up there.
        if (_mineSeed is not null)
        {
            _dialogue = null;
            return;
        }

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

        // Parked. Building no targets is what switches the whole feature off: the prompt, the
        // key and the action all read from this and all find nothing.
        if (!ParkedFeatures.Pickpocketing) return;

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

    /// <summary>Put the gear back on the shelf, in memory and in the save.</summary>
    private void RestockTheStall()
    {
        if (_shop is null || _session is null) return;

        foreach (var itemId in _shop.Restock())
            _session.Player.Story.ForgetLooted($"shop.{_shop.Definition.Id}.{itemId}");
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

        // Left and right move across a row; up and down move between rows.
        if (Pressed(keyboard, Keys.Left))
            _shopSelection = (_shopSelection + items.Count - 1) % items.Count;
        if (Pressed(keyboard, Keys.Right))
            _shopSelection = (_shopSelection + 1) % items.Count;
        if (Pressed(keyboard, Keys.Up))
            _shopSelection = (_shopSelection + items.Count - UiLayout.ShopColumns) % items.Count;
        if (Pressed(keyboard, Keys.Down))
            _shopSelection = (_shopSelection + UiLayout.ShopColumns) % items.Count;

        var pointer = LogicalMouse(mouse);
        for (var index = 0; index < items.Count; index++)
        {
            // Through the same helper the renderer uses, so a scrolled grid cannot end up
            // drawing one thing where the mouse buys another.
            if (ShopRenderer.TileFor(index, _shopSelection, items.Count) is not { } row) continue;
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
    private WorldHudState BuildWorldHudState()
    {
        var detection = _session?.Player.Detection;
        var objective = _session?.Player.Objective;
        var vitals = _session?.Player.Vitals;
        var feedback = _encounter?.Feedback;

        var health = vitals is null
            ? default
            : new VitalBarState(vitals.Health, vitals.MaxHealth, _healthPulse);
        var prana = vitals is null
            ? default
            : new VitalBarState(vitals.Prana, vitals.MaxPrana, _pranaPulse);
        var stamina = vitals is null
            ? default
            : new VitalBarState(vitals.Stamina, vitals.MaxStamina);

        var activeObjective = objective is { HasObjective: true } objectiveValue
            ? objectiveValue
            : null;
        var objectiveTitle = activeObjective?.Title;
        var objectiveDirections = activeObjective?.Directions ?? string.Empty;
        var objectiveBearing = activeObjective is null || _session is null
            ? string.Empty
            : activeObjective.BearingLine(_session.Position);

        return new WorldHudState(
            HasSession: _session is not null,
            IsCrouching: detection?.IsCrouching == true,
            Awareness: detection?.Awareness ?? AwarenessLevel.Unaware,
            Suspicion: detection?.Suspicion ?? 0f,
            DamageFlash: _encounter is { DamageFlash: > 0f } encounter
                ? encounter.DamageFlash / Encounter.DamageFlashSeconds
                : 0f,
            HitMarker: feedback?.HitMarker ?? 0f,
            KillMarker: feedback?.KillMarker ?? 0f,
            DamageDirections: feedback?.Directions.ToArray() ?? Array.Empty<DamageDirection>(),
            CastBanner: feedback?.CastBanner ?? 0f,
            CastTint: feedback?.CastTint ?? Color.Transparent,
            CastColour: feedback?.CastColour ?? Color.White,
            CastLine: feedback?.CastLine ?? string.Empty,
            LocationCaption: LocationCaption(),
            ObjectiveTitle: objectiveTitle,
            ObjectiveDirections: objectiveDirections,
            ObjectiveBearing: objectiveBearing,
            Health: health,
            Prana: prana,
            Stamina: stamina,
            Toasts: _session?.Toasts.Select(toast => new ToastHud(toast.Message, toast.Remaining)).ToArray()
                ?? Array.Empty<ToastHud>(),
            Level: vitals?.Level ?? 0,
            Gold: vitals?.Gold ?? 0,
            WeaponName: _session?.Player.Combat.ActiveWeapon.DisplayName ?? string.Empty,
            IsBlocking: _session?.Player.Combat.IsBlocking == true,
            FramesPerSecond: _framesPerSecond,
            ShowFrameRate: !_capture.IsCapturing,
            Spell: BuildSpellHud(),
            CoachLine: _runSummary is not null || _choosingDepth || _campTraderOpen
                ? string.Empty
                : _coach.Line,
            CoachOpacity: _coach.Opacity);
    }

    private SpellHudState BuildSpellHud()
    {
        if (_session is null) return new SpellHudState(false, string.Empty, 0f, false, false, 0f,
            Array.Empty<SocketHud>());

        var caster = _session.Player.Spells;
        var spell = SpellCatalog.Get(caster.SelectedSpellId);
        if (spell is null) return new SpellHudState(false, string.Empty, 0f, false, false, 0f,
            Array.Empty<SocketHud>());

        var cost = caster.CostOf(spell);
        var stones = _session.Player.Stones.Socketed
            .Select(StoneCatalog.Find)
            .Where(stone => stone is not null)
            .Select(stone => new SocketHud(ShortNameOf(stone!)))
            .ToArray();

        return new SpellHudState(
            HasSpell: true,
            Name: spell.DisplayName,
            Cost: cost,
            Affordable: _session.Player.Vitals.Prana >= cost
                || _session.Player.Inventory.Has(SoulCrystals.LesserId),
            LightActive: caster.LightActive,
            LightRemaining: caster.LightRemaining,
            Stones: stones);
    }

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
                _lights, _stone, _ui, _clock, _assetCase);
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

        // A full-screen panel owns the screen. Leaving the combat HUD drawing underneath it
        // was most of why testers called the inventory cluttered.
        // 'hud off' counts as a panel owning the screen here: it is a request for a clean
        // picture, and a clean picture has no vitals in the corner of it.
        var panelOpen = _showHelp || _showJournal || _showCharacter || _showShop || _hideInterface;
        var hudState = BuildWorldHudState();

        if (!panelOpen)
        {
            DrawWeapon();
            _screens.Hud.DrawDamageFlash(hudState);
            // Both of these report on a system with nothing to report: no live world places
            // a watcher, so the awareness meter has read UNAWARE in every screenshot ever
            // taken of this game. Crouching still works; it just no longer pretends.
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
        }

        if (!_hideInterface)
        {
            // Not while the door decision is up.
            //
            // Toasts are drawn after the decision panel, and clearing a room fires three of
            // them -- the kill, the room, and anything the room dropped -- at the same instant
            // the panel opens. They land straight across its middle, so "stones held", the
            // staking ratio and the trader line are all read through "Splitting Stone found."
            // The most important screen in the game was illegible at the moment it appeared.
            //
            // Suppressed rather than moved, because the panel already says what they say: the
            // room count and the stones are on it, in larger type, and they stay there instead
            // of fading after two seconds.
            if (_run is not { AtDecision: true }) _screens.Hud.DrawToasts(hudState);
            DrawContentErrors();
        }

        if (_showHelp) _screens.Overlay.DrawHelpOverlay(BuildOverlayState());
        if (_dialogueOpen) DrawDialogue();
        if (_showJournal) DrawJournal();
        if (_showFort && _session is not null)
            _screens.Fort.Draw(_session.Player.Legacy, _fortSelection, _openFortRoom);
        if (_showCharacter) DrawCharacterSheet();
        if (_showShop) DrawShop();
        if (_campTraderOpen) DrawCampTrader();
        if (_choosingDepth) DrawDepthChoice();
        if (_paused && _runSummary is null) _screens.Overlay.DrawPause(BuildOverlayState());
        if (_runSummary is { } summary) DrawRunSummary(summary);

        if (!_hideInterface) DrawWatches();
        DrawConsole();

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
        _screens.Descent.DrawCampTrader(_session.Player.Inventory, _run.Run, _campSelection);
    }

    private void DrawDepthChoice()
    {
        if (_session is null) return;
        _screens.Descent.DrawDepthChoice(
            _session.Player.Inventory.CountOf(SoulCrystals.LesserId), _depthSelection,
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
        if (!_consoleOpen) return;

        _screens.Console.Draw(_consoleOutput, _consoleInput, _clock);
    }

    /// <summary>
    /// What the player can do with whatever is under the crosshair.
    ///
    /// Queries live here because they need the world, the session and the run. The renderer
    /// only paints the chips it is given.
    /// </summary>
    private PromptState BuildPromptState()
    {
        if (_session is null) return PromptState.Empty;

        var player = new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z);
        var chips = new List<PromptChip>();

        if (OnTheSurface)
        {
            var fixture = Surface.FixtureAt(player);
            if (fixture == SurfaceFixture.None) return PromptState.Empty;

            var stones = _session.Player.Inventory.CountOf(SoulCrystals.LesserId);
            var line = fixture switch
            {
                SurfaceFixture.Shaft => $"E  Open a shaft   ({stones} stones)",
                SurfaceFixture.Trader => "E  Trade",
                _ => "E  Read the carving"
            };

            chips.Add(new PromptChip(line, UiLayout.SinglePrompt, PromptRole.Interact));
            return new PromptState(chips);
        }

        var actor = _dialogue?.FindActor(player, _camera.Yaw);
        if (actor is not null)
        {
            chips.Add(new PromptChip($"Click / E  Talk to {actor.DisplayName}",
                UiLayout.TalkPrompt, PromptRole.Talk, Fit: true));

            if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                && _shop is not null)
            {
                chips.Add(new PromptChip("B  Shop", UiLayout.SecondaryPrompt, PromptRole.Interact));
            }

            // A pocket worth picking was previously only advertised on guards, so the one
            // pocket in the slice that matters — the trader carrying the watchpost key —
            // had no prompt at all and testers never found it.
            if (HasPickablePocket(actor))
            {
                chips.Add(new PromptChip("P  Pick pocket", UiLayout.PickpocketPrompt, PromptRole.Pocket));
            }

            return new PromptState(chips);
        }

        var pickup = FindPickup(player, _camera.Yaw);
        if (pickup is not null)
        {
            chips.Add(new PromptChip($"Click / E  Take {pickup.Name} x{pickup.Count}",
                UiLayout.SinglePrompt, PromptRole.Talk, Fit: true));
            return new PromptState(chips);
        }

        // The camp decision is a bigger question about the same door; two prompts on one
        // doorway would just be noise.
        if (_world is null || _run is { AtDecision: true }) return PromptState.Empty;
        var door = _world.FindDoor(player, _camera.Yaw);
        if (door is null) return PromptState.Empty;

        var hasKey = !string.IsNullOrEmpty(door.Definition.KeyItemId)
            && _session.Player.Inventory.Has(door.Definition.KeyItemId);

        if (_run is { BarsTheWay: true })
        {
            chips.Add(new PromptChip("Barred  |  clear this room first",
                UiLayout.SinglePrompt, PromptRole.Barred));
            return new PromptState(chips);
        }

        var text = !door.Lock.IsLocked ? "Click / E  Open door"
            : hasKey ? "Click / E  Unlock with your key"
            : $"Locked  |  a key, or Security {door.Definition.Difficulty:0}";
        chips.Add(new PromptChip(text, UiLayout.SinglePrompt, PromptRole.Interact));
        return new PromptState(chips);
    }

    private void DrawDialogue()
    {
        if (_conversationActor is null) return;
        _screens.Dialogue.Draw(_conversationActor, _dialogueResponse, _dialogueSelection);
    }

    private void DrawJournal()
    {
        if (_session is null) return;
        _screens.Journal.Draw(_session.Player);
    }

    private void DrawCharacterSheet()
    {
        if (_session is null) return;
        _screens.Character.Draw(_session.Player, _inventorySelection,
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
        _screens.Shop.Draw(_shop, _session.Player.Vitals.Gold, _shopSelection);
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



    /// <summary>"Cinder Stone" is too long under a 34-pixel icon; "Cinder" is not.</summary>
    private static string ShortNameOf(StoneDefinition stone)
    {
        var space = stone.DisplayName.IndexOf(' ');
        return space > 0 ? stone.DisplayName[..space] : stone.DisplayName;
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
            case "depth" or "shaft":
                // Through the real opener, not by setting the flag.
                //
                // Setting _choosingDepth by hand left every shaft seed at its default zero, and
                // a theme is a pure function of seed and tier, so the captured board was one no
                // player ever sees -- the same five caves every time, with tier five showing
                // the granite that tier one is always given. A screenshot of a state that
                // cannot occur is worse than no screenshot: it was read, here, as a design
                // fault in theme selection.
                OpenTheShaft();
                _depthSelection = 3;
                break;
            case "shop" or "stall": _showShop = true; break;
            case "camp" or "trader": _campTraderOpen = true; break;
            case "fort": _showFort = true; break;
            case "pause": _paused = true; break;
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

    /// <summary>This frame's world-to-canvas projection, for anything anchored in the world.</summary>
    private WorldProjector Projector() =>
        new(GraphicsDevice.Viewport, _camera.View, _camera.Projection, _ui.Scale,
            _ui.LogicalWidth, _ui.LogicalHeight);

    private void DrawContentErrors() =>
        _screens.Markers.DrawContentErrors(_assetErrors.Concat(_modelCache.Errors).ToList());



    private void DrawAuthoredWorld() =>
        _worldView.Draw(GraphicsDevice, _scene, _modelCache, _world, _pickups,
            OnTheSurface, _cave, _camera.View, _camera.Projection, _lights, ref _stone);

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

    void IConsoleTarget.WaitSeconds(float seconds) => _scriptWaitSeconds = seconds;

    /// <summary>
    /// Walk forward for a while, through the controller rather than around it.
    ///
    /// Holds the script for the same span, so a script reads as a sequence of moves rather
    /// than as a set of overlapping timers.
    /// </summary>
    void IConsoleTarget.WalkForward(float seconds)
    {
        _scriptWalkSeconds = seconds;
        _scriptWaitSeconds = seconds;
    }

    /// <summary>
    /// Press E, and say what it did — the door state is the answer a script needs.
    ///
    /// Reported by looking at the door ahead before and after, because Interact itself is
    /// deliberately void: what the key does is show a toast or open something, not return.
    /// </summary>
    string IConsoleTarget.Use()
    {
        var before = _world is null
            ? null
            : _world.FindDoor(
                new WorldPoint(_camera.Position.X, _camera.Position.Y, _camera.Position.Z),
                _camera.Yaw);

        Interact();

        if (before is null) return "Used what was ahead.";
        return before.Lock.IsOpen ? $"Opened {before.Definition.Id}." : $"{before.Definition.Id} did not open.";
    }

    void IConsoleTarget.FailScript(string why) => FailScript(why);

    /// <summary>Record a scripted failure, and say so on stdout for an unattended run.</summary>
    private void FailScript(string why)
    {
        _scriptFailed = true;
        Console.WriteLine("[!] " + why);
    }

    void IConsoleTarget.QuitWhenDone() => _scriptQuitWhenDone = true;

    void IConsoleTarget.Watch(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) _watches.Clear();
        else _watches.Add(command);
    }

    IReadOnlyList<string> IConsoleTarget.Watches => _watches;

    void IConsoleTarget.Queue(string statements)
    {
        foreach (var statement in ConsoleRouter.SplitStatements(statements))
            _scriptQueue.Enqueue(statement);
    }

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
