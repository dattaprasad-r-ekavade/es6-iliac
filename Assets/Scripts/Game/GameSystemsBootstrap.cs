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

    public void StartGameplaySystems(Transform player)
    {
        if (_started) return;
        _started = true;
        _player = player;

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

    private void SpawnWorldContent()
    {
        // Bandit camp — well south of Daggerfall safe zone
        var camp = new GameObject("BanditCamp_Glenumbra");
        camp.transform.position = new Vector3(-1750f, 25f, 850f);
        for (int i = 0; i < 3; i++)
        {
            var pos = camp.transform.position + new Vector3(i * 4f - 4f, 0f, (i % 2) * 3f);
            SnapToGround(ref pos);
            EnemyBrain.Spawn("Bandit", pos, new Color(0.45f, 0.2f, 0.15f), 50f + i * 5f, modelId: i == 0 ? "character-male-a" : null);
        }

        // Ruin undead — far from Daggerfall safe zone
        var ruinPos = new Vector3(-2200f, 25f, 700f);
        SnapToGround(ref ruinPos);
        EnemyBrain.Spawn("Skeleton", ruinPos + Vector3.right * 3f, new Color(0.75f, 0.75f, 0.7f), 40f);
        EnemyBrain.Spawn("Skeleton", ruinPos + Vector3.left * 3f, new Color(0.7f, 0.7f, 0.65f), 40f);

        // Daggerfall NPCs near spawn
        var merchantPos = new Vector3(-1985f, 25f, 1460f);
        SnapToGround(ref merchantPos);
        NpcInteractable.Spawn("Mira the Provisioner", merchantPos, new Color(0.35f, 0.45f, 0.7f),
            new[] { "Potions and rumors, traveler.", "Wayrest lies far east across the hills." }, merchant: true, modelId: "character-female-b");

        var guardPos = new Vector3(-2010f, 25f, 1475f);
        SnapToGround(ref guardPos);
        NpcInteractable.Spawn("Gate Guard Ralen", guardPos, new Color(0.4f, 0.4f, 0.45f),
            new[] { "Keep your blade sheathed in the city.", "Bandits haunt the southern road." }, modelId: "character-male-e");

        var questPos = new Vector3(-1990f, 25f, 1435f);
        SnapToGround(ref questPos);
        NpcInteractable.Spawn("Captain Alid", questPos, new Color(0.55f, 0.4f, 0.25f),
            new[] { "Clear the Glenumbra bandits.", "The bay remembers those who wander it." }, questGiver: true, modelId: "character-male-c");

        // Wayrest / Sentinel greeters (far)
        var wr = new Vector3(2180f, 23f, 1560f);
        SnapToGround(ref wr);
        NpcInteractable.Spawn("Wayrest Dockhand", wr, new Color(0.3f, 0.5f, 0.4f),
            new[] { "Welcome to Wayrest, jewel of the Bjoulsae." }, modelId: "character-male-b");

        var sen = new Vector3(-1580f, 19f, -1960f);
        SnapToGround(ref sen);
        NpcInteractable.Spawn("Sentinel Scout", sen, new Color(0.7f, 0.55f, 0.3f),
            new[] { "Hot wind and hotter steel — this is Sentinel." }, modelId: "character-female-d");
    }

    private static void EnsurePlayerAtDaggerfallSpawn(Transform player)
    {
        if (player == null) return;
        var pos = IliacBayWorldGenerator.SnapToWalkable(player.position);
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;
    }

    private static void SnapToGround(ref Vector3 pos)
    {
        pos = IliacBayWorldGenerator.SnapToWalkable(pos);
    }
}
