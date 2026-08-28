using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client;

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
    public bool DecisionRecorded;
    public Random StoneDrops = new(0);
    public IReadOnlyList<string> EarnedAmulets = Array.Empty<string>();
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
    SoundBank? Sfx { get; }
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

    public bool OnTheSurface => _play.MineSeed is null;

    public void ApplyCave()
    {
        if (_play.Session is not null) _play.Session.Player.Spells.Cave = _play.Cave;
    }

    public void ReturnToTheSurface()
    {
        _play.Summary = null;
        _play.Succession = null;
        EnterWorld(null);
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
        _play.DecisionRecorded = false;

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
            _hooks.Sfx?.Play(Sfx.Coin, MathHelper.Clamp(paid / 12f, 0.3f, 1f));
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
            _hooks.Sfx?.Play(Sfx.Chime, 0.85f);
        }

        foreach (var id in _play.EarnedAmulets)
        {
            _play.Session?.ShowToast($"{AmuletCatalog.Find(id)?.DisplayName} — kept for good.");
            _hooks.Sfx?.Play(Sfx.Chime, 0.7f);
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

        if (!WorldRuntime.TryCreate(Surface.Build(), out var yard, out var yardError))
        {
            _hooks.AssetErrors.Add(yardError);
            return;
        }

        _play.World = yard;
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
