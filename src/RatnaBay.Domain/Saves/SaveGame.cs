using System.Text.Json;
using System.Text.Json.Serialization;

namespace RatnaBay.Domain;

/// <summary>Everything a save slot holds.</summary>
public sealed class SaveData
{
    /// <summary>
    /// Bump when the shape of this changes; older files are rejected rather than silently
    /// loading garbage into the player's stats.
    /// </summary>
    // Capture writes the current version explicitly. Zero means a hand-written or truncated
    // object omitted the version and must not be mistaken for a valid current save.
    public int Version { get; set; }

    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public float PlayerZ { get; set; }
    public float PlayerYaw { get; set; }

    public SavedVitals Vitals { get; set; } = new();

    /// <summary>Equipped set. Ids only — the catalog supplies the numbers.</summary>
    public string WeaponId { get; set; } = EquipmentCatalog.UnarmedId;
    public string ArmourId { get; set; } = string.Empty;

    /// <summary>Use-based skill progress. Character level derives from the total.</summary>
    public List<SavedSkill> Skills { get; set; } = new();

    public List<ItemStack> Items { get; set; } = new();
    public List<SavedQuest> Quests { get; set; } = new();
    public List<string> KilledEnemies { get; set; } = new();
    public List<string> KnownTopics { get; set; } = new();
    public List<string> Discovered { get; set; } = new();

    public float TimeOfDay01 { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public string SpawnId { get; set; } = string.Empty;

    /// <summary>
    /// The current objective. Its bearing is deliberately not stored — that regenerates from
    /// wherever the player turns out to be standing on load.
    /// </summary>
    public SavedObjective? Objective { get; set; }

    public StorySnapshot Story { get; set; } = new();

    /// <summary>Successors spent, and the cache waiting to be fetched.</summary>
    public SavedLegacy? Legacy { get; set; }
    public string SavedUtc { get; set; } = string.Empty;
}

/// <summary>
/// Capture and restore of a whole character.
///
/// Version is checked before anything is applied, so a file from an incompatible build fails
/// loudly instead of half-loading. Serialization lives here rather than in the game layer
/// because the save format is a game rule, not a rendering concern.
/// </summary>
public static class SaveGame
{
    /// <summary>
    /// v3: location ids became setting-neutral, so a v2 slot's discovered-location list no
    /// longer resolves. v4: prana replaced mana and vitals moved into their own block.
    /// </summary>
    public const int CurrentVersion = 4;

    /// <summary>The oldest version that can still be migrated forward.</summary>
    public const int MinimumMigratableVersion = 3;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static SaveData Capture(PlayerCharacter player, WorldPoint position, float yaw = 0f,
        string sceneId = "", string spawnId = "")
    {
        return new SaveData
        {
            Version = CurrentVersion,
            PlayerX = position.X, PlayerY = position.Y, PlayerZ = position.Z, PlayerYaw = yaw,
            Vitals = player.Vitals.Capture(),
            WeaponId = player.Equipment.WeaponId,
            ArmourId = player.Equipment.ArmourId,
            Skills = player.Skills.Capture().ToList(),
            Items = player.Inventory.Items.ToList(),
            Quests = player.Quests.Capture().ToList(),
            KilledEnemies = player.World.GetKilledIds().ToList(),
            KnownTopics = player.Dialogue.Capture().ToList(),
            Story = player.Story.Capture(),
            Legacy = player.Legacy.Capture(),
            Objective = player.Objective.Capture(),
            SceneId = sceneId,
            SpawnId = spawnId,
            SavedUtc = DateTime.UtcNow.ToString("O")
        };
    }

    /// <summary>
    /// Apply a save to a character. Order matters: skills go in before equipment so an
    /// auto-equip cannot outrank what the save actually recorded.
    /// </summary>
    public static void Restore(PlayerCharacter player, SaveData data)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(data);
        if (!IsRestorable(data, out var validationError))
            throw new ArgumentException(validationError, nameof(data));

        player.Skills.Restore(data.Skills);
        player.Vitals.Restore(data.Vitals);

        player.Inventory.Clear();
        foreach (var stack in data.Items)
            player.Inventory.Add(stack.Id, stack.Name, stack.Count, stack.Kind);

        player.Equipment.Restore(data.WeaponId, data.ArmourId);
        player.World.LoadKilled(data.KilledEnemies);
        player.Dialogue.Restore(data.KnownTopics);
        player.Story.Restore(data.Story);
        player.Legacy.Restore(data.Legacy);

        // Derived from the saved route rather than stored twice: one source of truth for
        // which path this character walks.
        player.LifePath.Select(player.Story.State.RouteId);
        player.Quests.Restore(data.Quests);
        player.ReconcileQuestStoryFlags();
        player.Objective.Restore(data.Objective);
    }

    public static string Serialize(SaveData data) => JsonSerializer.Serialize(data, Options);

    /// <summary>
    /// Read a save. Returns false with a readable reason rather than throwing, so a corrupt
    /// or outdated slot can be reported to the player instead of crashing the load.
    /// </summary>
    public static bool TryRead(string? json, out SaveData? data, out string error)
    {
        data = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The save file is empty.";
            return false;
        }

        SaveData? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SaveData>(json, Options);
        }
        catch (JsonException exception)
        {
            error = $"The save file could not be read: {exception.Message}";
            return false;
        }

        if (parsed is null)
        {
            error = "The save file could not be read.";
            return false;
        }

        if (!IsRestorable(parsed, out error)) return false;

        if (parsed.Version > CurrentVersion)
        {
            error = $"This save is from a newer version ({parsed.Version}); this build reads {CurrentVersion}.";
            return false;
        }

        if (parsed.Version < MinimumMigratableVersion)
        {
            error = $"Ignoring save from version {parsed.Version} (current is {CurrentVersion}).";
            return false;
        }

        if (parsed.Version < CurrentVersion) Migrate(parsed);

        data = parsed;
        return true;
    }

    private static bool IsRestorable(SaveData data, out string error)
    {
        error = string.Empty;

        if (data.Vitals is null || data.Skills is null || data.Items is null
            || data.Quests is null || data.KilledEnemies is null || data.KnownTopics is null
            || data.Story is null)
        {
            error = "The save file is missing required data.";
            return false;
        }

        if (data.Skills.Any(skill => skill is null || string.IsNullOrWhiteSpace(skill.Id))
            || data.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id)
                || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Kind))
            || data.Quests.Any(quest => quest is null || string.IsNullOrWhiteSpace(quest.Id)))
        {
            error = "The save file contains an invalid inventory, skill, or quest entry.";
            return false;
        }

        if (data.Story.Profile is null || data.Story.Flags is null
            || data.Story.Evidence is null || data.Story.Evidence.Any(evidence => evidence is null)
            || data.Story.DialogueChoices is null || data.Story.Companion is null
            || data.Story.OpenedLocks is null || data.Story.LootedObjects is null
            || data.Story.SkippedCinematics is null || data.Story.KnownTopics is null)
        {
            error = "The save file contains invalid story data.";
            return false;
        }

        if (data.Objective is not null && string.IsNullOrWhiteSpace(data.Objective.Title))
        {
            error = "The save file contains an invalid objective.";
            return false;
        }

        return true;
    }

    /// <summary>Bring an older but still readable save up to the current shape.</summary>
    private static void Migrate(SaveData data)
    {
        // v3 slots predate the setting-neutral location ids, so their discovered list is
        // dropped rather than resolved against names that no longer exist.
        if (data.Version == 3) data.Discovered.Clear();

        data.Version = CurrentVersion;
    }
}
