using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client.Session;

internal enum GameScreen
{
    MainMenu,
    WorldScene
}

/// <summary>
/// The live descent, the yard, and the character walking them.
///
/// Game1 still owns the camera, audio and what a confirmed row does. This bag is what the
/// session director mutates; Game1 reads it to build snapshots.
/// </summary>
internal sealed class PlayState
{
    public GameSession? Session;
    public WorldRuntime? World;
    public RunRuntime? Run;
    public RunResult? Summary;
    public SuccessionResult? Succession;
    public Encounter? Encounter;
    public int? MineSeed;
    public int MineRooms = 4;
    public int MineDepth = 1;
    public CaveTheme? Cave;
    public DialogueRuntime? Dialogue;
    public WatcherRuntime? Watchers;
    public Shop? Shop;
    public readonly List<WorldPickup> Pickups = new();
    public bool ResumingDescent;
    public Random StoneDrops = new(0);
    public IReadOnlyList<string> EarnedAmulets = Array.Empty<string>();
    public readonly Dictionary<string, PickpocketTarget> Pockets = new(StringComparer.Ordinal);

    /// <summary>
    /// True while the player is standing in the fort rather than the yard.
    ///
    /// A third place alongside the yard and a mine. Kept as its own flag rather than folded
    /// into MineSeed, because the fort is neither: it has no run, no pot and no way down.
    /// </summary>
    public bool InFort;

    public FortRuntime? Fort;
}

/// <summary>
/// What the session director needs from the coordinator. Game1 implements this; do not pass
/// <c>Game1</c> itself.
/// </summary>
internal interface ISessionHooks
{
    ScreenStack Stack { get; }
    FirstPersonView Camera { get; }
    PlayRecorder Recorder { get; }
    Coach Coach { get; }
    SoundBank? Sounds { get; }
    GameScreen Screen { get; set; }
    string MenuStatus { get; set; }
    string QuestObjectiveId { get; set; }
    IList<string> AssetErrors { get; }

    void SetMouseLook(bool enabled, bool forPanel = false);
    void SpawnEnemies();
    void WatchForTheRecord(Encounter encounter, GameSession session);
    void LoadQuestManifest();
    void LoadDialogueManifest();
    void LoadWatchers();
    void LoadPockets();
    void LoadPickups();
    void LoadShop();
    void RefreshQuestObjective();
    void RestockTheStall();
    void ResetCamera();
    void OfferStone();
    void LeaveToMenu();
    bool SuspendedOnDisk { get; set; }
}

/// <summary>
/// Start a session, enter a world, start and end a run, save and load.
///
/// Game1 still owns the camera pose after a load, audio, and toasts that are not the
/// session's own. This type must not take a <c>Game1</c> reference.
/// </summary>
internal sealed class SessionDirector
{
    public const int RoomsPerSegment = 8;

    public const string CachePickupId = "cache.fallen";

    public static readonly WorldPoint SurfaceCheckpoint = new(0f, 2.4f, 14.5f);

    private readonly PlayState _play;
    private readonly ISessionHooks _hooks;

    public SessionDirector(PlayState play, ISessionHooks hooks)
    {
        _play = play;
        _hooks = hooks;
    }

    public PlayState Play => _play;

    /// <summary>
    /// True while the player is standing in the yard.
    ///
    /// **Not simply "not in a mine".** There are three places now, and this used to be
    /// `MineSeed is null`, which the fort also satisfies — so the yard's signs were drawn down
    /// the fort's hall, `Surface.FixtureAt` was live in there, and standing near the fort's
    /// entrance put the player within reach of a shaft two worlds away. The daylight palette
    /// followed the same test.
    /// </summary>
    public bool OnTheSurface => _play.MineSeed is null && !_play.InFort;

    public void ApplyCave()
    {
        if (_play.Session is not null) _play.Session.Player.Spells.Cave = _play.Cave;
    }

    public void ReturnToTheSurface()
    {
        _play.Summary = null;
        _play.Succession = null;
        _play.InFort = false;
        EnterWorld(null);
    }

    /// <summary>
    /// Step through the gate in the west wall, into the order's own rooms.
    ///
    /// A change of place rather than a panel. The fort used to be a list of doors drawn over
    /// the yard; it is a manifest now, so entering it is the same operation as entering a mine
    /// -- discard the world, load the other one, put the player at its spawn.
    ///
    /// Refused mid-descent. There is no way into the fort from underground and no reason to
    /// want one, but the guard is here rather than assumed because the console can put the
    /// player anywhere.
    /// </summary>
    public void EnterTheFort()
    {
        if (_play.Session is null) return;

        if (_play.Run is { Run.IsActive: true })
        {
            _play.Session.ShowToast("Not from down here.");
            return;
        }

        _play.InFort = true;
        _play.Fort = new FortRuntime();
        _play.World = null;
        _play.MineSeed = null;
        _play.Cave = null;

        LoadWorldManifest();

        // Rank decides which doors stand open, and it is applied to the world rather than
        // built into it -- see FortHall. A promotion between visits opens a door without the
        // fort having to be generated a second way.
        //
        // RestoreOpenedDoors rather than opening each by hand: it takes ids, applies them to
        // the door runtimes and rebuilds collision, which is exactly the job, and a second way
        // of doing it would be a second way to get it wrong.
        _play.World?.RestoreOpenedDoors(
            _play.Fort.OpenDoorsFor(_play.Session.Player.Legacy.Service.Rank).ToList());

        PlaceAtFortSpawn();
        _play.Session.ShowToast("The fort. Ten doors, and what is behind them.");
    }

    /// <summary>
    /// Walking back out of the entrance is how you leave.
    ///
    /// No prompt and no key. The fort has one way in and the same way out, and a place you
    /// leave by walking out of is a place rather than a screen -- which is the entire point of
    /// having built it as geometry. The threshold is behind the spawn, so arriving does not
    /// immediately trigger it.
    /// </summary>
    public void LeaveTheFortIfWalkedOut(bool anyPanelOpen)
    {
        if (!_play.InFort || anyPanelOpen) return;
        if (_hooks.Camera.Position.Z < FortHall.Spawn.Z + 2.4f) return;

        LeaveTheFort();
    }

    /// <summary>Back out to the yard, from inside the fort.</summary>
    public void LeaveTheFort()
    {
        if (!_play.InFort) return;

        _play.InFort = false;
        _play.Fort = null;
        _play.World = null;
        _play.MineSeed = null;

        LoadWorldManifest();
        _hooks.Camera.Position =
            new Vector3(Surface.FortGate.X + 2f, FortHall.Spawn.Y, Surface.FortGate.Z);
        _hooks.Camera.Yaw = 1.57f;
        _play.Session?.ShowToast("Back in the yard.");
    }

    private void PlaceAtFortSpawn()
    {
        _hooks.Camera.Position = new Vector3(FortHall.Spawn.X, FortHall.Spawn.Y, FortHall.Spawn.Z);
        _hooks.Camera.Yaw = FortHall.SpawnYaw;
        _hooks.Camera.Pitch = -0.06f;
    }

    public void EnterWorld(int? mineSeed, bool newCharacter = false, int tier = 1)
    {
        _play.MineSeed = mineSeed;
        _play.MineRooms = RoomsPerSegment;
        _play.MineDepth = Math.Clamp(tier, MineEntry.MinTier, MineEntry.MaxTier);
        _play.Cave = mineSeed is { } seed ? CaveThemeCatalog.For(seed, _play.MineDepth) : null;
        _play.World = null;
        _play.Run = null;
        _play.Summary = null;

        var session = newCharacter || _play.Session is null ? GameSession.NewGame() : _play.Session;
        Start(session);
        ApplyCave();
        _hooks.ResetCamera();
        _hooks.MenuStatus = string.Empty;
        _hooks.Screen = GameScreen.WorldScene;
        _hooks.SetMouseLook(true);
    }

    public void Start(GameSession session)
    {
        var changingCharacter = !ReferenceEquals(_play.Session, session);
        _play.Session = session;
        LoadWorldManifest();
        _hooks.LoadQuestManifest();
        _hooks.LoadDialogueManifest();
        _hooks.LoadWatchers();
        _hooks.LoadPockets();
        _hooks.LoadPickups();
        _hooks.LoadShop();
        if (changingCharacter) session.Player.Quests.Changed += _hooks.RefreshQuestObjective;
        _hooks.Stack.Dialogue = false;
        _hooks.Stack.Journal = false;
        _hooks.Stack.Character = false;
        _hooks.Stack.Shop = false;
        _hooks.QuestObjectiveId = string.Empty;
        _play.World?.RestoreOpenedDoors(session.Player.Story.State.OpenedLocks);
        _play.Encounter = new Encounter(session);
        _hooks.WatchForTheRecord(_play.Encounter, session);
        _hooks.SpawnEnemies();
        StartRun();

        if (!changingCharacter) return;

        session.Player.Vitals.Died += () =>
        {
            if (_play.Run is { Run.IsActive: true })
            {
                var lostRun = _play.Run.Die();
                _hooks.Recorder.Record(PlayEventKind.Died, $"after {lostRun.RoomsCleared} rooms",
                    lostRun.StonesLost, 0f, 0f);

                _play.Succession = Succession.Promote(session.Player, lostRun,
                    _play.MineSeed ?? 0, _play.Run.DeepestRoom);

                EndRun(lostRun);
                return;
            }

            session.ShowToast("You were defeated — returned to safe ground.");
            session.Player.Vitals.FullRestore();
            session.Player.Combat.ClearCombat();
            _hooks.ResetCamera();
        };
    }

    public void StartRun()
    {
        _play.Run = null;
        _play.Summary = null;

        if (_play.World is null || _play.MineSeed is not { } seed) return;
        if (_play.World.Manifest.Rooms.Count < 2) return;

        _play.Run = new RunRuntime(_play.World.Manifest, seed, _play.MineDepth, _play.MineRooms);
        _play.Session?.Player.Stones.ClearForDescent();

        if (_play.Session?.Player.Legacy.Has(AmuletEffect.Bearer) == true)
            _play.Session.Player.Inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1,
                SoulCrystals.ItemKind);

        _play.StoneDrops = new Random(seed * 397 + _play.MineDepth);

        _hooks.Recorder.Record(PlayEventKind.RunStarted, _play.World.Manifest.Id, seed, _play.MineDepth,
            _play.Session?.Player.Vitals.Health ?? 0f);

        _play.Run.RoomEntered += room =>
        {
            _play.Session?.Player.Combat.EnterRoom();
            _hooks.Recorder.Record(PlayEventKind.RoomEntered,
                $"room {room}", room, 0f, _play.Session?.Player.Vitals.Health ?? 0f,
                _play.Session?.Player.Vitals.Prana ?? 0f);
            _hooks.Coach.Teach(Lessons.FirstRoom, Lessons.TextOf(Lessons.FirstRoom));
            _hooks.Coach.Teach(Lessons.Rising, Lessons.TextOf(Lessons.Rising));
        };

        _play.Run.RoomCleared += paid =>
        {
            _play.Session?.ShowToast(
                $"Room clear.  +{paid} stones held  ({_play.Run.Run.Pending} at risk)");
            _hooks.Sounds?.Play(Sfx.Coin, MathHelper.Clamp(paid / 12f, 0.3f, 1f));
            _hooks.Recorder.Record(PlayEventKind.RoomCleared, $"room {_play.Run.DeepestRoom}", paid,
                _play.Run.Run.Pending, _play.Session?.Player.Vitals.Health ?? 0f,
                _play.Session?.Player.Vitals.Prana ?? 0f);
            _hooks.OfferStone();
        };
    }

    public void EndRun(RunResult result)
    {
        _play.Summary = result;
        if (result.Survived) _play.Succession = null;
        _hooks.SetMouseLook(false, forPanel: true);

        _play.EarnedAmulets = _play.Session is null
            ? Array.Empty<string>()
            : _play.Session.Player.Legacy.RecordDepth(result.RoomsCleared);

        if (_play.Session is not null && _play.Session.Player.Legacy.Service.Record(result))
        {
            var rank = _play.Session.Player.Legacy.Service;
            _play.Session.ShowToast($"The order raises you. You are {Ranks.LabelOf(rank.Rank)}.");
            _hooks.Sounds?.Play(Sfx.Chime, 0.85f);
        }

        foreach (var id in _play.EarnedAmulets)
        {
            _play.Session?.ShowToast($"{AmuletCatalog.Find(id)?.DisplayName} — kept for good.");
            _hooks.Sounds?.Play(Sfx.Chime, 0.7f);
        }

        _hooks.RestockTheStall();
        _hooks.Coach.Teach(result.Survived ? Lessons.Banked : Lessons.Died,
            Lessons.TextOf(result.Survived ? Lessons.Banked : Lessons.Died));

        if (!result.Survived && result.StonesLost > 0)
            _hooks.Coach.Teach(Lessons.Body, Lessons.TextOf(Lessons.Body));

        _hooks.Recorder.Record(PlayEventKind.RunEnded,
            result.Survived ? "camped" : "died", result.RoomsCleared, result.Tier,
            _play.Session?.Player.Vitals.Health ?? 0f);
        _hooks.Recorder.Flush();

        if (_play.Session is null) return;

        var saveMessage = _play.Session.CompleteRun(result, SurfaceCheckpoint);
        if (!string.Equals(saveMessage, "Saved.", StringComparison.Ordinal))
            _hooks.MenuStatus = saveMessage;
    }

    public bool Load()
    {
        if (_play.Session is null) Start(GameSession.NewGame());

        if (!_play.Session!.TryLoad(out var message))
        {
            if (_hooks.Screen == GameScreen.MainMenu) _hooks.MenuStatus = message;
            else _play.Session.ShowToast(message);
            return false;
        }

        _hooks.Camera.Position = new Microsoft.Xna.Framework.Vector3(
            _play.Session.Position.X, _play.Session.Position.Y, _play.Session.Position.Z);
        _hooks.Camera.Yaw = _play.Session.Yaw;
        _play.World?.RestoreOpenedDoors(_play.Session.Player.Story.State.OpenedLocks);
        _hooks.LoadPockets();
        _hooks.LoadPickups();
        _hooks.LoadShop();
        _hooks.RefreshQuestObjective();

        _play.Encounter = new Encounter(_play.Session);
        _hooks.WatchForTheRecord(_play.Encounter, _play.Session);
        _hooks.SpawnEnemies();
        StartRun();

        _play.Session.ShowToast(message);
        _hooks.MenuStatus = string.Empty;
        return true;
    }

    public void LoadWorldManifest()
    {
        if (_play.World is not null) return;

        if (_play.MineSeed is { } seed)
        {
            var manifest = MineGenerator.Generate(seed, _play.MineRooms, _play.MineDepth);

            if (!_play.ResumingDescent)
                _play.Session?.Player.World.ForgetKilledIn(manifest.Id);

            PlaceTheFallen(manifest, seed);

            if (!WorldRuntime.TryCreate(manifest, out var generated, out var generationError))
            {
                _hooks.AssetErrors.Add(generationError);
                return;
            }

            _play.World = generated;
            return;
        }

        // Indoors, between runs. Built from the roster, so a room added to the fort is a room
        // in the world without anybody drawing a corridor for it.
        if (_play.InFort)
        {
            if (!WorldRuntime.TryCreate(FortHall.Build(), out var fort, out var fortError))
            {
                _hooks.AssetErrors.Add(fortError);
                return;
            }

            _play.World = fort;
            return;
        }

        if (!WorldRuntime.TryCreate(Surface.Build(), out var yard, out var yardError))
        {
            _hooks.AssetErrors.Add(yardError);
            return;
        }

        _play.World = yard;
    }

    /// <summary>Go down, at a depth that has been paid for.</summary>
    public void EnterMine(int seed, int tier) => EnterWorld(seed, tier: tier);

    /// <summary>Walk back into the mine that was put down.</summary>
    public void ResumeSuspendedDescent()
    {
        _play.MineSeed = null;
        _play.World = null;
        _play.Run = null;
        _play.Summary = null;

        if (_play.Session is null && !Load()) return;
        if (_play.Session is null) return;

        if (!_play.Session.HasSuspendedDescent)
        {
            _hooks.MenuStatus = "There is no descent to return to.";
            return;
        }

        var descent = _play.Session.Descent!;
        _play.ResumingDescent = true;
        _play.MineRooms = descent.Rooms;
        _play.MineDepth = descent.Depth;
        _play.MineSeed = descent.Seed;
        _play.World = null;
        _play.Cave = CaveThemeCatalog.For(descent.Seed, descent.Depth);

        Start(_play.Session);
        ApplyCave();

        _hooks.Camera.Position = new Vector3(
            _play.Session.Position.X, _play.Session.Position.Y, _play.Session.Position.Z);
        _hooks.Camera.Yaw = _play.Session.Yaw;
        _hooks.Camera.Pitch = _play.Session.Pitch;
        _hooks.Camera.StandingEyeY = _play.Session.Position.Y;

        _play.Run?.Resume(descent);
        _play.ResumingDescent = false;
        _play.Session.ConsumeDescent();
        _hooks.SuspendedOnDisk = false;

        _play.Session.ShowToast($"Back in the dark. {_play.Run?.Run.Pending ?? 0} stones still at risk.");
        _hooks.MenuStatus = string.Empty;
        _hooks.Screen = GameScreen.WorldScene;
        _hooks.SetMouseLook(true);
    }

    /// <summary>Put the run down mid-descent, to be walked back into later.</summary>
    public void SuspendDescent()
    {
        if (_play.Session is null || _play.Run is not { Run.IsActive: true } run
            || _play.MineSeed is not { } seed)
            return;

        var camera = _hooks.Camera;
        var message = _play.Session.Suspend(
            run.Capture(seed, _play.MineRooms, _play.MineDepth),
            new WorldPoint(camera.Position.X, camera.Position.Y, camera.Position.Z),
            camera.Yaw, camera.Pitch);

        _hooks.SuspendedOnDisk = _play.Session.HasSuspendedDescent;
        _play.Session.ShowToast(message);
        _hooks.LeaveToMenu();
    }

    /// <summary>
    /// Give up on a descent, at the full price of one.
    ///
    /// It costs exactly what dying costs — the pot, half the pack, and progress toward the
    /// next level — because anything cheaper is a button that cancels a fight going badly.
    /// </summary>
    public void AbandonDescent()
    {
        if (_play.Session is null || _play.Run is not { Run.IsActive: true } run) return;

        var result = run.Die();
        _hooks.Recorder.Record(PlayEventKind.Died, "gave up", result.StonesLost, 0f,
            _play.Session.Player.Vitals.Health, _play.Session.Player.Vitals.Prana);

        _play.Succession = Succession.Promote(_play.Session.Player, result,
            _play.MineSeed ?? 0, run.DeepestRoom);

        _play.Session.Descent = null;
        _hooks.SuspendedOnDisk = false;
        _hooks.Stack.Paused = false;
        EndRun(result);
    }

    private void PlaceTheFallen(WorldManifest manifest, int seed)
    {
        if (_play.Session is null) return;

        var cache = _play.Session.Player.Legacy.Fallen;
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
}
