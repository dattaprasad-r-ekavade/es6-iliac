using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and enables P0/P1 gameplay systems when the cutscene hands off to the player.
/// </summary>
public class GameSystemsBootstrap : MonoBehaviour
{
    [SerializeField] private NpcArchetype[] npcArchetypes;

    public static GameSystemsBootstrap Instance { get; private set; }

    private bool _started;
    private Transform _player;

    private void Awake()
    {
        // Main is loaded again when the player returns to the title. The systems root is
        // persistent, so without replacing the old copy that reload leaves two complete
        // singleton graphs alive and New Game talks to whichever one happened to Awake last.
        // Prefer the freshly-authored scene copy: it has a clean `_started` flag and will be
        // wired to the new player created by Main.
        if (Instance != null && Instance != this)
        {
            var staleRoot = Instance.gameObject;
            Instance = this;
            if (Application.isPlaying)
            {
                staleRoot.SetActive(false);
                Destroy(staleRoot);
            }
            else DestroyImmediate(staleRoot);
        }
        else
        {
            Instance = this;
        }

        if (Application.isPlaying && gameObject.name == "GameSystems")
            DontDestroyOnLoad(gameObject);
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

        Require<PlayerStats>(player.gameObject);
        Require<PlayerInventory>(player.gameObject);
        Require<PlayerEquipment>(player.gameObject);
        Require<SpellCaster>(player.gameObject);
        Require<SkillSystem>(player.gameObject);
        var combat = Require<PlayerCombat>(player.gameObject);
        Require<PlayerInteract>(player.gameObject);
        Require<PlayerSafetyGuard>(player.gameObject);
        combat.enabled = true;

        var systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Runtime/GameSystems");
            systems = prefab != null ? Instantiate(prefab) : new GameObject("GameSystems");
            systems.name = "GameSystems";
        }

        var time = Require<TimeWeatherSystem>(systems);
        var disc = Require<DiscoveryTravelSystem>(systems);
        Require<QuestSystem>(systems);
        var hud = Require<GameHud>(systems);
        var save = Require<SaveLoadService>(systems);

        var sun = FindAnyObjectByType<Light>();
        time.Configure(sun, player);
        disc.Configure(player);
        hud.Build(player);
        EnsurePlayerAtCaldemarSpawn(player);
        player.GetComponent<PlayerSafetyGuard>()?.GrantSpawnProtection();
        save.SetCheckpoint(player.position);

        SpawnWorldContent();

        GameHud.Instance?.ShowToast("Ratna Bay — M Map · J Journal · I Inv · E Talk");
        Debug.Log("[GameSystems] P0/P1 systems online.");
    }

    private static T Require<T>(GameObject owner) where T : Component
    {
        var component = owner.GetComponent<T>();
        if (component != null) return component;
        throw new MissingComponentException(
            $"{owner.name} must come from the runtime prefab and contain {typeof(T).Name}.");
    }

    /// <summary>
    /// Populate the world with NPCs and enemies.
    ///
    /// Every position here is snapped with <see cref="KessilWorldGenerator.SnapToGround"/>,
    /// which used to return the Caldemar spawn pad no matter what it was given — so all
    /// of this content spawned in a single pile on the start plaza, leaving the bandit camp
    /// and the ruin empty and the far-city greeters nowhere near their cities.
    /// </summary>
    private void SpawnWorldContent()
    {
        // Bandit camp — well south of the Caldemar safe zone.
        var camp = new GameObject("BanditCamp_Kelrith");
        camp.transform.position = WorldLayout.BanditCamp;
        ReconcileHostileSpawns();

        foreach (var archetype in npcArchetypes)
            SpawnNpc(archetype);
    }

    /// <summary>
    /// Make the live hostile population match the killed-id set restored from a save.
    /// Saved kills remove enemies, while enemies killed after that save are recreated
    /// when the older save is loaded.
    /// </summary>
    public void ReconcileHostileSpawns()
    {
        var livingIds = new HashSet<string>();
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
        foreach (var enemy in enemies)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.SpawnId)) continue;

            if (WorldState.IsKilled(enemy.SpawnId) || enemy.Health <= 0f || !enemy.gameObject.activeInHierarchy)
            {
                Destroy(enemy.gameObject);
                continue;
            }

            // A stable spawn id represents one enemy. Remove accidental duplicates.
            if (!livingIds.Add(enemy.SpawnId))
                Destroy(enemy.gameObject);
        }

        for (int i = 0; i < 3; i++)
        {
            var pos = WorldLayout.BanditCamp + new Vector3(i * 4f - 4f, 0f, (i % 2) * 3f);
            EnsureHostileSpawn(livingIds, $"bandit_camp_{i}", "Bandit", pos,
                new Color(0.45f, 0.2f, 0.15f), 50f + i * 5f,
                i == 0 ? "character-male-a" : null);
        }

        for (int i = 0; i < 2; i++)
        {
            var pos = WorldLayout.CoastalRuin + (i == 0 ? Vector3.right : Vector3.left) * 3f;
            EnsureHostileSpawn(livingIds, $"coastal_ruin_skeleton_{i}", "Skeleton", pos,
                new Color(0.75f - i * 0.05f, 0.75f - i * 0.05f, 0.7f), 40f, null);
        }
    }

    private static void EnsureHostileSpawn(HashSet<string> livingIds, string spawnId, string displayName,
        Vector3 pos, Color color, float health, string modelId)
    {
        if (WorldState.IsKilled(spawnId) || livingIds.Contains(spawnId)) return;

        SnapToGround(ref pos);
        if (EnemyBrain.Spawn(displayName, pos, color, health, modelId, spawnId) != null)
            livingIds.Add(spawnId);
    }

    private static void SpawnNpc(NpcArchetype archetype)
    {
        if (archetype == null || string.IsNullOrWhiteSpace(archetype.Id)) return;
        var anchor = WorldLayout.FindSite(archetype.AnchorSiteId);
        if (!anchor.HasValue)
        {
            Debug.LogError($"[GameSystems] NPC '{archetype.Id}' has unknown anchor site '{archetype.AnchorSiteId}'.");
            return;
        }

        var pos = anchor.Value.TravelPosition + archetype.Offset;
        SnapToGround(ref pos);
        NpcInteractable.Spawn(archetype.DisplayName, pos, archetype.Tint, archetype.Lines,
            archetype.Merchant, archetype.QuestGiver, archetype.ModelId);
    }

    private static void EnsurePlayerAtCaldemarSpawn(Transform player)
    {
        if (player == null) return;
        var pos = KessilWorldGenerator.GetPlayerSpawn();
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
        pos = KessilWorldGenerator.PlaceOnLand(pos);
    }
}
