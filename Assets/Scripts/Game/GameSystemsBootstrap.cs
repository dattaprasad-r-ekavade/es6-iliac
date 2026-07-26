using UnityEngine;

/// <summary>
/// Spawns and enables P0/P1 gameplay systems when the cutscene hands off to the player.
/// </summary>
public class GameSystemsBootstrap : MonoBehaviour
{
    public static GameSystemsBootstrap Instance { get; private set; }

    private bool _started;
    private Transform _player;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void StartGameplaySystems(Transform player)
    {
        if (_started) return;
        _started = true;
        _player = player;
        PlayerRef.Set(player);
        WorldTagger.SetLayerRecursive(player.gameObject, GameLayers.Player);

        var stats = player.GetComponent<PlayerStats>() ?? player.gameObject.AddComponent<PlayerStats>();
        var inv = player.GetComponent<PlayerInventory>() ?? player.gameObject.AddComponent<PlayerInventory>();
        var combat = player.GetComponent<PlayerCombat>() ?? player.gameObject.AddComponent<PlayerCombat>();
        var interact = player.GetComponent<PlayerInteract>() ?? player.gameObject.AddComponent<PlayerInteract>();
        player.gameObject.AddComponent<PlayerSafetyGuard>();
        combat.enabled = true;

        var systems = GameObject.Find("GameSystems");
        if (systems == null) systems = new GameObject("GameSystems");

        var time = systems.GetComponent<TimeWeatherSystem>() ?? systems.AddComponent<TimeWeatherSystem>();
        var disc = systems.GetComponent<DiscoveryTravelSystem>() ?? systems.AddComponent<DiscoveryTravelSystem>();
        var quests = systems.GetComponent<QuestSystem>() ?? systems.AddComponent<QuestSystem>();
        var hud = systems.GetComponent<GameHud>() ?? systems.AddComponent<GameHud>();
        var save = systems.GetComponent<SaveLoadService>() ?? systems.AddComponent<SaveLoadService>();

        var sun = FindFirstObjectByType<Light>();
        time.Configure(sun, player);
        disc.Configure(player);
        hud.Build(player);
        EnsurePlayerAtDaggerfallSpawn(player);
        player.GetComponent<PlayerSafetyGuard>()?.GrantSpawnProtection();
        save.SetCheckpoint(player.position);

        SpawnWorldContent();
        save.Save(); // autosave checkpoint at start of gameplay

        GameHud.Instance?.ShowToast("Iliac Bay — M Map · J Journal · I Inv · E Talk");
        Debug.Log("[GameSystems] P0/P1 systems online.");
    }

    /// <summary>
    /// Populate the world with NPCs and enemies.
    ///
    /// Every position here is snapped with <see cref="IliacBayWorldGenerator.SnapToGround"/>,
    /// which used to return the Daggerfall spawn pad no matter what it was given — so all
    /// of this content spawned in a single pile on the start plaza, leaving the bandit camp
    /// and the ruin empty and the far-city greeters nowhere near their cities.
    /// </summary>
    private void SpawnWorldContent()
    {
        // Bandit camp — well south of the Daggerfall safe zone.
        var camp = new GameObject("BanditCamp_Glenumbra");
        camp.transform.position = WorldLayout.BanditCamp;
        for (int i = 0; i < 3; i++)
        {
            var pos = WorldLayout.BanditCamp + new Vector3(i * 4f - 4f, 0f, (i % 2) * 3f);
            SnapToGround(ref pos);
            EnemyBrain.Spawn("Bandit", pos, new Color(0.45f, 0.2f, 0.15f), 50f + i * 5f,
                modelId: i == 0 ? "character-male-a" : null, spawnId: $"bandit_camp_{i}");
        }

        // Ruin undead — far from the Daggerfall safe zone.
        for (int i = 0; i < 2; i++)
        {
            var pos = WorldLayout.CoastalRuin + (i == 0 ? Vector3.right : Vector3.left) * 3f;
            SnapToGround(ref pos);
            EnemyBrain.Spawn("Skeleton", pos, new Color(0.75f - i * 0.05f, 0.75f - i * 0.05f, 0.7f), 40f,
                spawnId: $"coastal_ruin_skeleton_{i}");
        }

        // Daggerfall NPCs, placed relative to the plaza so they follow it if it moves.
        var plaza = WorldLayout.DaggerfallSpawnPad;

        SpawnNpc(plaza + new Vector3(15f, 0f, 10f), "Mira the Provisioner", new Color(0.35f, 0.45f, 0.7f),
            new[] { "Potions and rumors, traveler.", "Wayrest lies far east across the hills." },
            merchant: true, modelId: "character-female-b");

        SpawnNpc(plaza + new Vector3(-10f, 0f, 25f), "Gate Guard Ralen", new Color(0.4f, 0.4f, 0.45f),
            new[] { "Keep your blade sheathed in the city.", "Bandits haunt the southern road." },
            modelId: "character-male-e");

        SpawnNpc(plaza + new Vector3(10f, 0f, -15f), "Captain Alid", new Color(0.55f, 0.4f, 0.25f),
            new[] { "Clear the Glenumbra bandits.", "The bay remembers those who wander it." },
            questGiver: true, modelId: "character-male-c");

        // Greeters at the other two cities, on their own travel pads.
        var wayrest = WorldLayout.FindSite("wayrest");
        if (wayrest.HasValue)
            SpawnNpc(wayrest.Value.TravelPosition + new Vector3(-20f, 0f, 10f), "Wayrest Dockhand",
                new Color(0.3f, 0.5f, 0.4f),
                new[] { "Welcome to Wayrest, jewel of the Bjoulsae." }, modelId: "character-male-b");

        var sentinel = WorldLayout.FindSite("sentinel");
        if (sentinel.HasValue)
            SpawnNpc(sentinel.Value.TravelPosition + new Vector3(20f, 0f, -10f), "Sentinel Scout",
                new Color(0.7f, 0.55f, 0.3f),
                new[] { "Hot wind and hotter steel — this is Sentinel." }, modelId: "character-female-d");
    }

    private static void SpawnNpc(Vector3 pos, string name, Color color, string[] lines,
        bool merchant = false, bool questGiver = false, string modelId = null)
    {
        SnapToGround(ref pos);
        NpcInteractable.Spawn(name, pos, color, lines, merchant, questGiver, modelId);
    }

    private static void EnsurePlayerAtDaggerfallSpawn(Transform player)
    {
        if (player == null) return;
        var pos = IliacBayWorldGenerator.GetPlayerSpawn();
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Place world content on dry land at (or near) where it was authored — not at the
    /// player spawn, which is what the old snap helper returned for everything.
    /// </summary>
    private static void SnapToGround(ref Vector3 pos)
    {
        pos = IliacBayWorldGenerator.PlaceOnLand(pos);
    }
}
