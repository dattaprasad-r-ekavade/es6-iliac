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
    public float Health, Magicka, Stamina;
    public float MaxHealth, MaxMagicka, MaxStamina;
    public float TimeOfDay01;
    public List<string> Discovered = new();
    public List<InvItem> Items = new();
    public List<SavedQuest> Quests = new();
    public List<string> KilledEnemies = new();
}

public class SaveLoadService : MonoBehaviour
{
    public const int CurrentVersion = 2;

    public static SaveLoadService Instance { get; private set; }

    private string Path => System.IO.Path.Combine(Application.persistentDataPath, "iliac_bay_save.json");
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
            Magicka = stats.Magicka,
            Stamina = stats.Stamina,
            MaxHealth = stats.MaxHealth,
            MaxMagicka = stats.MaxMagicka,
            MaxStamina = stats.MaxStamina,
            TimeOfDay01 = TimeWeatherSystem.Instance != null ? TimeWeatherSystem.Instance.TimeOfDay01 : 0.4f,
            Discovered = DiscoveryTravelSystem.Instance != null ? DiscoveryTravelSystem.Instance.GetDiscoveredIds() : new List<string>(),
            Items = PlayerInventory.Instance != null ? new List<InvItem>(PlayerInventory.Instance.Items) : new List<InvItem>(),
            Quests = QuestSystem.Instance != null ? QuestSystem.Instance.CaptureState() : new List<SavedQuest>(),
            KilledEnemies = WorldState.GetKilledIds()
        };

        try
        {
            File.WriteAllText(Path, JsonUtility.ToJson(data, true));
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
        if (!File.Exists(Path))
        {
            GameHud.Instance?.ShowToast("No save found");
            return;
        }

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
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
        if (File.Exists(Path))
        {
            Load();
            return;
        }

        var player = PlayerRef.Transform;
        if (player != null)
        {
            var target = _hasCheckpoint ? _checkpoint : IliacBayWorldGenerator.GetPlayerSpawn();
            MovePlayer(player, target);
        }

        PlayerStats.Instance?.FullRestore();
    }

    private void Apply(SaveData data)
    {
        var player = PlayerRef.Transform;
        if (player != null)
        {
            MovePlayer(player, new Vector3(data.Px, data.Py, data.Pz));
            player.rotation = Quaternion.Euler(0f, data.Pyaw, 0f);
        }

        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            stats.Gold = data.Gold;
            stats.Health = data.Health;
            stats.Magicka = data.Magicka;
            stats.Stamina = data.Stamina;
            stats.MaxHealth = data.MaxHealth;
            stats.MaxMagicka = data.MaxMagicka;
            stats.MaxStamina = data.MaxStamina;
            // Level/Xp have private setters — use the restore helper.
            stats.RestoreProgress(data.Level, data.Xp);
        }

        DiscoveryTravelSystem.Instance?.LoadDiscovered(data.Discovered);
        QuestSystem.Instance?.RestoreState(data.Quests);
        WorldState.LoadKilled(data.KilledEnemies);
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
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
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
}
