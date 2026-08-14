using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
    /// <summary>Equipped set. Ids only — EquipmentCatalog supplies the numbers.</summary>
    public string WeaponId;
    public string ArmourId;
    /// <summary>Use-based skill progress. Character level derives from the total.</summary>
    public List<SavedSkill> SkillLevels = new();
    public float TimeOfDay01;
    public List<string> Discovered = new();
    public List<InvItem> Items = new();
    public List<SavedQuest> Quests = new();
    public List<string> KilledEnemies = new();
    public string SceneId;
    public string SpawnId;
    public StorySnapshot Story = new();
    public string SavedUtc;
}

public class SaveLoadService : MonoBehaviour
{
    // v3: location ids became setting-neutral ("city_west" rather than a place name), so
    // a v2 slot's discovered-location list no longer resolves. v2 files also live under
    // the old company/product folder and filename, so they are orphaned rather than
    // migrated — the bump is what makes a hand-copied one fail loudly instead of quietly.
    public const int CurrentVersion = 4;

    public static SaveLoadService Instance { get; private set; }

    /// <summary>Stable save location for menu flows that exist before this component is created.</summary>
    private static string _saveFilePathOverride;
    public static string SaveFilePath => string.IsNullOrWhiteSpace(_saveFilePathOverride)
        ? Path.Combine(Application.persistentDataPath, "kessil_save.json")
        : _saveFilePathOverride;

    public static void ConfigureSaveFilePath(string path) => _saveFilePathOverride = path;
    public static void ResetSaveFilePath() => _saveFilePathOverride = null;

    /// <summary>True when a save slot exists. Loading still validates its contents and version.</summary>
    public static bool HasSaveFile => File.Exists(SaveFilePath);
    public static bool HasValidSave => TryReadSave(out _, out _);
    private Vector3 _checkpoint;
    private bool _hasCheckpoint;
    public bool IsLoading { get; private set; }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameInput.Save.WasPressedThisFrame()) Save();
        if (GameInput.Load.WasPressedThisFrame()) Load();
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
            WeaponId = PlayerEquipment.Instance != null ? PlayerEquipment.Instance.WeaponId : null,
            ArmourId = PlayerEquipment.Instance != null ? PlayerEquipment.Instance.ArmourId : null,
            SkillLevels = SkillSystem.Instance != null ? SkillSystem.Instance.Capture() : new List<SavedSkill>(),
            TimeOfDay01 = TimeWeatherSystem.Instance != null ? TimeWeatherSystem.Instance.TimeOfDay01 : 0.4f,
            Discovered = DiscoveryTravelSystem.Instance != null ? DiscoveryTravelSystem.Instance.GetDiscoveredIds() : new List<string>(),
            Items = PlayerInventory.Instance != null ? new List<InvItem>(PlayerInventory.Instance.Items) : new List<InvItem>(),
            Quests = QuestSystem.Instance != null ? QuestSystem.Instance.CaptureState() : new List<SavedQuest>(),
            KilledEnemies = WorldState.GetKilledIds()
        };
        data.SceneId = SceneTransitionService.Instance != null
            ? SceneTransitionService.Instance.ActiveContentSceneName
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        data.SpawnId = SceneTransitionService.Instance != null
            ? SceneTransitionService.Instance.ActiveSpawnId
            : string.Empty;
        data.Story = StoryDirector.Instance != null ? StoryDirector.Instance.Capture() : new StorySnapshot();
        data.Story.KnownTopics = TopicDialogueService.Instance != null
            ? TopicDialogueService.Instance.CaptureKnownTopics()
            : new List<string>();
        data.SavedUtc = DateTime.UtcNow.ToString("O");

        try
        {
            WriteAtomic(JsonUtility.ToJson(data, true));
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
        if (IsLoading) return;
        IsLoading = true;
        if (!HasSaveFile)
        {
            GameHud.Instance?.ShowToast("No save found");
            IsLoading = false;
            return;
        }

        if (!TryReadSave(out var data, out var error))
        {
            if (error != null && error.StartsWith("Ignoring", StringComparison.Ordinal))
                Debug.LogWarning($"[Save] {error}");
            else
                Debug.LogError($"[Save] Could not read save file: {error}");
            GameHud.Instance?.ShowToast("Save file unreadable or incompatible");
            IsLoading = false;
            return;
        }

        string currentScene = SceneTransitionService.Instance != null
            ? SceneTransitionService.Instance.ActiveContentSceneName
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (SceneTransitionService.Instance != null
            && !string.IsNullOrWhiteSpace(data.SceneId)
            && data.SceneId != currentScene
            && Application.CanStreamedLevelBeLoaded(data.SceneId))
        {
            StartCoroutine(TransitionAndApply(data));
            return;
        }

        Apply(data);
        IsLoading = false;
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
            // Level/Xp/Channeled have private setters — use the restore helpers.
            stats.RestoreProgress(data.Level, data.Xp);
            // Channeling is persisted in the story snapshot, which is what dialogue reads.
            stats.RestoreChanneled(Mathf.RoundToInt(data.Story?.PlayerChanneled ?? 0f));
            PlayerEquipment.Instance?.Restore(data.WeaponId, data.ArmourId);
            SkillSystem.Instance?.Restore(data.SkillLevels);
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

        StoryDirector.Instance?.Restore(data.Story);
        TopicDialogueService.Instance?.RestoreKnownTopics(data.Story?.KnownTopics);
    }

    private System.Collections.IEnumerator TransitionAndApply(SaveData data)
    {
        yield return SceneTransitionService.Instance.TransitionTo(data.SceneId, data.SpawnId);
        if (!string.IsNullOrEmpty(SceneTransitionService.Instance.LastError))
        {
            Debug.LogError($"[Save] Could not restore scene '{data.SceneId}': {SceneTransitionService.Instance.LastError}");
            IsLoading = false;
            yield break;
        }
        Apply(data);
        IsLoading = false;
        GameHud.Instance?.ShowToast("Game loaded (F9)");
    }

    public static bool TryReadSave(out SaveData data, out string error)
    {
        data = null;
        error = null;
        if (!File.Exists(SaveFilePath)) { error = "Save file does not exist."; return false; }
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SaveFilePath));
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }

        if (data == null) { error = "JSON contained no save object."; return false; }
        if (data.Version == 3)
        {
            // v3 had no story state. Preserve its exploration state but initialise the
            // Chapter 01 contract explicitly rather than pretending those fields existed.
            data.Version = CurrentVersion;
            data.Story = new StorySnapshot();
            data.SceneId = MigrateSceneName(data.SceneId ?? "Main");
            return true;
        }
        if (data.Version != CurrentVersion)
        {
            error = $"Ignoring save from version {data.Version} (current is {CurrentVersion}).";
            data = null;
            return false;
        }
        data.Story ??= new StorySnapshot();
        data.SceneId = MigrateSceneName(data.SceneId);
        return true;
    }

    /// <summary>
    /// Chapter 01's scenes were renamed to setting-neutral identities on 2026-08-12 — the
    /// building rather than the city, so a setting rename never orphans a save again.
    ///
    /// <see cref="SaveData.SceneId"/> is misnamed: it holds the Unity *scene name*, not a
    /// <c>scene.*</c> id, because that is what <c>CanStreamedLevelBeLoaded</c> and
    /// <c>TransitionTo</c> take. So the rename really did invalidate saves, and this map is
    /// what stops a mid-chapter save from silently failing to restore its scene.
    ///
    /// This is the migration the v2 and v3 bumps deliberately did not do. It is worth doing
    /// here because the mapping is exactly known, rather than guessed at.
    /// </summary>
    private static string MigrateSceneName(string sceneName)
    {
        return sceneName switch
        {
            "Estmere_Docks" => "Docks",
            "Estmere_Palace" => "Palace",
            "Estmere_Palace_Aftermath" => "Palace_Aftermath",
            "Estmere_Arcanum" => "Order_Hall",
            "Estmere_Harbor" => "Harbor",
            "Estmere_Prison" => "Prison",
            "Estmere_SeaCave" => "Sea_Cave",
            "Estmere_SecuredTower" => "Secured_Tower",
            "Estmere_Region" => "Capital_Region",
            "Estmere_Exterior" => "Capital_Exterior",
            "Caldemar_Arrival" => "Council_Arrival",
            _ => sceneName
        };
    }

    private static void WriteAtomic(string json)
    {
        var path = SaveFilePath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        var backup = path + ".bak";
        File.WriteAllText(temporary, json);
        if (File.Exists(path))
        {
            File.Copy(path, backup, true);
            File.Delete(path);
        }
        File.Move(temporary, path);
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
