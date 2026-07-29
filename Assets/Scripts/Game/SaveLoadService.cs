using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class SavedQuest
{
    public string Id;
    public bool Active;
    public bool Completed;
    public int Progress;
    public string StageText;
}

[Serializable]
public class SaveData
{
    /// <summary>Bump when the shape of this changes; older files are rejected rather
    /// than silently loading garbage into the player's stats.</summary>
    public int Version = SaveLoadService.CurrentVersion;

    public float Px, Py, Pz, Pyaw;
    public int Level, Xp, Gold;
    public float Health, Mana, Stamina;
    public float MaxHealth, MaxMana, MaxStamina;
    public float TimeOfDay01;
    public List<string> Discovered = new();
    public List<InvItem> Items = new();
    public List<SavedQuest> Quests = new();
    public List<string> KilledEnemies = new();
}

public class SaveLoadService : MonoBehaviour
{
    // v3: location ids became setting-neutral ("city_west" rather than a place name), so
    // a v2 slot's discovered-location list no longer resolves. v2 files also live under
    // the old company/product folder and filename, so they are orphaned rather than
    // migrated — the bump is what makes a hand-copied one fail loudly instead of quietly.
    public const int CurrentVersion = 3;

    public static SaveLoadService Instance { get; private set; }

    /// <summary>Stable save location for menu flows that exist before this component is created.</summary>
    public static string SaveFilePath =>
        System.IO.Path.Combine(Application.persistentDataPath, "kessil_save.json");

    /// <summary>True when a save slot exists. Loading still validates its contents and version.</summary>
    public static bool HasSaveFile => File.Exists(SaveFilePath);
    private Vector3 _checkpoint;
    private bool _hasCheckpoint;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f5Key.wasPressedThisFrame) Save();
        if (kb.f9Key.wasPressedThisFrame) Load();
    }

    public void SetCheckpoint(Vector3 pos)
    {
        _checkpoint = pos;
        _hasCheckpoint = true;
    }

    public void Save()
    {
        var player = PlayerRef.Transform;
        var stats = PlayerStats.Instance;
        if (player == null || stats == null) return;

        var data = new SaveData
        {
            Version = CurrentVersion,
            Px = player.position.x,
            Py = player.position.y,
            Pz = player.position.z,
            Pyaw = player.eulerAngles.y,
            Level = stats.Level,
            Xp = stats.Xp,
            Gold = stats.Gold,
            Health = stats.Health,
            Mana = stats.Mana,
            Stamina = stats.Stamina,
            MaxHealth = stats.MaxHealth,
            MaxMana = stats.MaxMana,
            MaxStamina = stats.MaxStamina,
            TimeOfDay01 = TimeWeatherSystem.Instance != null ? TimeWeatherSystem.Instance.TimeOfDay01 : 0.4f,
            Discovered = DiscoveryTravelSystem.Instance != null ? DiscoveryTravelSystem.Instance.GetDiscoveredIds() : new List<string>(),
            Items = PlayerInventory.Instance != null ? new List<InvItem>(PlayerInventory.Instance.Items) : new List<InvItem>(),
            Quests = QuestSystem.Instance != null ? QuestSystem.Instance.CaptureState() : new List<SavedQuest>(),
            KilledEnemies = WorldState.GetKilledIds()
        };

        try
        {
            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Could not write save file: {e.Message}");
            GameHud.Instance?.ShowToast("Save failed");
            return;
        }

        SetCheckpoint(player.position);
        GameHud.Instance?.ShowToast("Game saved (F5)");
    }

    public void Load()
    {
        if (!HasSaveFile)
        {
            GameHud.Instance?.ShowToast("No save found");
            return;
        }

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFilePath));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Could not read save file: {e.Message}");
            GameHud.Instance?.ShowToast("Save file unreadable");
            return;
        }

        if (data == null)
        {
            GameHud.Instance?.ShowToast("Save file unreadable");
            return;
        }

        if (data.Version != CurrentVersion)
        {
            Debug.LogWarning($"[Save] Ignoring save from version {data.Version} (current is {CurrentVersion}).");
            GameHud.Instance?.ShowToast($"Save is from an older build (v{data.Version}) — not loaded");
            return;
        }

        Apply(data);
        GameHud.Instance?.ShowToast("Game loaded (F9)");
    }

    public void ReloadOrCheckpoint()
    {
        if (HasSaveFile)
        {
            Load();
            return;
        }

        var player = PlayerRef.Transform;
        if (player != null)
        {
            var target = _hasCheckpoint ? _checkpoint : KessilWorldGenerator.GetPlayerSpawn();
            MovePlayer(player, target);
        }

        PlayerStats.Instance?.FullRestore();
    }

    private void Apply(SaveData data)
    {
        var player = PlayerRef.Transform;
        if (player != null)
        {
            var savedPosition = new Vector3(data.Px, data.Py, data.Pz);
            var reconciledPosition = ReconcileSavedPlayerHeight(player, savedPosition);
            MovePlayer(player, reconciledPosition);
            player.rotation = Quaternion.Euler(0f, data.Pyaw, 0f);
        }

        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            stats.Gold = data.Gold;
            stats.Health = data.Health;
            stats.Mana = data.Mana;
            stats.Stamina = data.Stamina;
            stats.MaxHealth = data.MaxHealth;
            stats.MaxMana = data.MaxMana;
            stats.MaxStamina = data.MaxStamina;
            // Level/Xp have private setters — use the restore helper.
            stats.RestoreProgress(data.Level, data.Xp);
        }

        DiscoveryTravelSystem.Instance?.LoadDiscovered(data.Discovered);
        QuestSystem.Instance?.RestoreState(data.Quests);
        WorldState.LoadKilled(data.KilledEnemies);
        if (GameSystemsBootstrap.Instance != null)
            GameSystemsBootstrap.Instance.ReconcileHostileSpawns();
        else
            DespawnAlreadyKilled();

        if (PlayerInventory.Instance != null && data.Items != null)
        {
            PlayerInventory.Instance.Items.Clear();
            PlayerInventory.Instance.Items.AddRange(data.Items);
        }

        if (TimeWeatherSystem.Instance != null)
            TimeWeatherSystem.Instance.SetTimeOfDay01(data.TimeOfDay01);
    }

    /// <summary>
    /// Enemies are spawned once when gameplay starts, so loading a save has to remove
    /// the ones the player had already killed — otherwise every cleared camp refilled.
    /// </summary>
    private static void DespawnAlreadyKilled()
    {
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
        foreach (var enemy in enemies)
        {
            if (enemy != null && WorldState.IsKilled(enemy.SpawnId))
                Destroy(enemy.gameObject);
        }
    }

    private static void MovePlayer(Transform player, Vector3 position)
    {
        // CharacterController overrides direct transform writes, so it has to be off
        // for the teleport to stick.
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = position;
        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Terrain is generated data and may change shape between prototype builds. Preserve
    /// a save's authored X/Z exactly, but repair Y when the old point is now embedded,
    /// floating far above the surface, or submerged.
    /// </summary>
    private static Vector3 ReconcileSavedPlayerHeight(Transform player, Vector3 savedPosition)
    {
        var controller = player != null ? player.GetComponent<CharacterController>() : null;
        var grounded = KessilWorldGenerator.SnapCharacterToGround(savedPosition, controller);
        if (grounded == savedPosition) return savedPosition;

        float verticalError = Mathf.Abs(savedPosition.y - grounded.y);
        bool unsafeHeight = savedPosition.y <= WorldLayout.WaterLevel + 0.5f
                            || verticalError > 1.5f;
        if (!unsafeHeight) return savedPosition;

        Debug.Log(
            $"[Save] Reconciled saved terrain height from {savedPosition.y:0.00} " +
            $"to {grounded.y:0.00} without changing X/Z.");
        return new Vector3(savedPosition.x, grounded.y, savedPosition.z);
    }
}
