using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestData
{
    public string Id;
    public string Title;
    public string Description;
    public string StageText;
    public bool Active;
    public bool Completed;
    public int TargetCount;
    public int Progress;
    public string TargetEnemy;
    public string TargetLocationId;
}

public class QuestSystem : MonoBehaviour
{
    public static QuestSystem Instance { get; private set; }
    public readonly List<QuestData> Quests = new();
    public event Action OnChanged;

    private void Awake()
    {
        Instance = this;
        if (Quests.Count == 0) Seed();
    }

    private void Seed()
    {
        Quests.Add(new QuestData
        {
            Id = "main_bay",
            Title = "Winds of the Iliac",
            Description = "Learn the lay of the bay. Discover Wayrest or Sentinel, then return to Daggerfall's gate.",
            StageText = "Discover Wayrest or Sentinel",
            Active = true,
            TargetLocationId = "wayrest"
        });
        Quests.Add(new QuestData
        {
            Id = "bounty_bandits",
            Title = "Glenumbra Bounty",
            Description = "Bandits prey on the southern road from Daggerfall. Clear their camp.",
            StageText = "Slay bandits (0/3)",
            Active = true,
            TargetCount = 3,
            TargetEnemy = "Bandit"
        });
        Quests.Add(new QuestData
        {
            Id = "ruin_scout",
            Title = "Coastal Ruin",
            Description = "Scout the ruin south of Daggerfall and survive whatever lurks there.",
            StageText = "Discover Coastal Ruin",
            Active = true,
            TargetLocationId = "coastal_ruin"
        });
    }

    public void NotifyEnemyKilled(string enemyName)
    {
        foreach (var q in Quests)
        {
            if (!q.Active || q.Completed || q.TargetCount <= 0) continue;
            if (!string.IsNullOrEmpty(q.TargetEnemy) &&
                enemyName.IndexOf(q.TargetEnemy, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                q.Progress++;
                q.StageText = $"Slay bandits ({q.Progress}/{q.TargetCount})";
                if (q.Progress >= q.TargetCount) Complete(q);
                else OnChanged?.Invoke();
            }
        }
    }

    public void NotifyLocation(string locationId)
    {
        foreach (var q in Quests)
        {
            if (!q.Active || q.Completed) continue;
            if (q.TargetLocationId == locationId)
            {
                if (q.Id == "main_bay")
                {
                    q.StageText = "Return toward Daggerfall (or explore freely)";
                    // complete on discovering either major city beyond daggerfall
                    Complete(q);
                }
                else
                {
                    Complete(q);
                }
            }
            // main quest also completes for sentinel
            if (q.Id == "main_bay" && (locationId == "wayrest" || locationId == "sentinel"))
            {
                Complete(q);
            }
        }
    }

    private void Complete(QuestData q)
    {
        if (q.Completed) return;
        q.Completed = true;
        q.Active = false;
        q.StageText = "Completed";
        PlayerStats.Instance?.AddXp(50);
        if (PlayerStats.Instance != null) PlayerStats.Instance.Gold += 40;
        GameHud.Instance?.ShowToast($"Quest complete: {q.Title}");
        OnChanged?.Invoke();
    }

    /// <summary>Snapshot for the save file.</summary>
    public List<SavedQuest> CaptureState()
    {
        var list = new List<SavedQuest>(Quests.Count);
        foreach (var q in Quests)
        {
            list.Add(new SavedQuest
            {
                Id = q.Id,
                Active = q.Active,
                Completed = q.Completed,
                Progress = q.Progress,
                StageText = q.StageText
            });
        }
        return list;
    }

    /// <summary>Restore from a save file. Quests absent from the file keep their seeded state.</summary>
    public void RestoreState(List<SavedQuest> saved)
    {
        if (saved == null) return;
        foreach (var s in saved)
        {
            var q = Quests.Find(x => x.Id == s.Id);
            if (q == null) continue;
            q.Active = s.Active;
            q.Completed = s.Completed;
            q.Progress = s.Progress;
            if (!string.IsNullOrEmpty(s.StageText)) q.StageText = s.StageText;
        }
        OnChanged?.Invoke();
    }

    // Subscribed in Start only: OnEnable used to run as well, and since both fired
    // once with the system already alive, every discovery was handled twice.
    private void Start()
    {
        if (DiscoveryTravelSystem.Instance != null)
            DiscoveryTravelSystem.Instance.OnDiscovered += OnDisc;
    }

    private void OnDestroy()
    {
        if (DiscoveryTravelSystem.Instance != null)
            DiscoveryTravelSystem.Instance.OnDiscovered -= OnDisc;
        if (Instance == this) Instance = null;
    }

    private void OnDisc(DiscoveryTravelSystem.Location loc)
    {
        NotifyLocation(loc.Id);
    }
}

public class NpcInteractable : MonoBehaviour
{
    public string NpcName = "Citizen";
    public string[] Lines = { "Well met, traveler." };
    public bool IsMerchant;
    public bool IsQuestGiver;

    private void Reset()
    {
        Lines = new[] { "The bay is restless of late." };
    }

    public void Interact()
    {
        string line = Lines[UnityEngine.Random.Range(0, Lines.Length)];
        if (IsMerchant)
        {
            var stats = PlayerStats.Instance;
            var inventory = PlayerInventory.Instance;
            if (stats != null && inventory != null && stats.Gold >= 10)
            {
                stats.Gold -= 10;
                inventory.Add("health_potion", "Health Potion", 1, "potion");
                line = "Potion for ten gold. Don't die out there.";
            }
            else line = "Come back with coin if you want supplies.";
        }
        if (IsQuestGiver)
        {
            line = "Clear the Glenumbra bandits and the road will thank you.";
            QuestSystem.Instance?.NotifyLocation("bandit_camp");
        }
        GameHud.Instance?.ShowDialogue(NpcName, line);
    }

    public static GameObject Spawn(string name, Vector3 pos, Color color, string[] lines, bool merchant = false, bool questGiver = false, string modelId = null)
    {
        GameObject go = CharacterLibrary.Instantiate(modelId, 2.1f);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            var r = go.GetComponent<Renderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
            r.sharedMaterial = m;
        }

        go.name = "NPC_" + name.Replace(" ", "_");
        go.transform.position = pos;
        WorldTagger.SetLayerRecursive(go, GameLayers.Npc);
        if (go.GetComponent<Collider>() == null)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
        }

        var npc = go.GetComponent<NpcInteractable>() ?? go.AddComponent<NpcInteractable>();
        npc.NpcName = name;
        npc.Lines = lines;
        npc.IsMerchant = merchant;
        npc.IsQuestGiver = questGiver;
        return go;
    }
}

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float range = 3f;

    private void Update()
    {
        if (GameHud.Instance != null && GameHud.Instance.AnyMenuOpen) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        var cam = GetComponentInChildren<Camera>();
        var origin = cam != null ? cam.transform.position : transform.position + Vector3.up;
        var dir = cam != null ? cam.transform.forward : transform.forward;
        if (Physics.SphereCast(origin, 0.4f, dir, out var hit, range,
                GameLayers.InteractMask, QueryTriggerInteraction.Ignore))
        {
            var npc = hit.collider.GetComponentInParent<NpcInteractable>();
            if (npc != null) npc.Interact();
            else GameHud.Instance?.ShowToast("Nothing to use");
        }
        else GameHud.Instance?.ShowToast("Nothing nearby (E)");
    }
}
