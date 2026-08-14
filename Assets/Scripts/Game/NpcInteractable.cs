using UnityEngine;

public class NpcInteractable : MonoBehaviour
{
    public string NpcName = "Citizen";
    public string[] Lines = { "Well met, traveler." };
    public bool IsMerchant;
    public bool IsQuestGiver;

    private void Reset() => Lines = new[] { "The bay is restless of late." };

    public void Interact()
    {
        string line = Lines[Random.Range(0, Lines.Length)];
        if (IsMerchant) line = Trade();
        if (IsQuestGiver)
        {
            line = "Clear the Vela bandits and the road will thank you.";
            QuestSystem.Instance?.NotifyLocation("bandit_camp");
        }
        GameHud.Instance?.ShowDialogue(NpcName, line);
    }

    private const int PotionPrice = 10;

    /// <summary>
    /// Placeholder shop: one purchase per interaction, restocking whichever supply the player
    /// is shortest of. A real shop UI replaces this — but the gold must always change hands
    /// first, because it silently stopped doing so once already (2026-07-26 audit).
    ///
    /// Jiva stones are offered only when the player is nearly out, so the merchant does not
    /// compete with potions while the player is well supplied.
    /// </summary>
    private string Trade()
    {
        var stats = PlayerStats.Instance;
        var inventory = PlayerInventory.Instance;
        if (stats == null || inventory == null)
            return "Come back with coin if you want supplies.";

        bool shortOfCharge = inventory.CountOf(SoulCrystals.LesserId) < 2;
        if (shortOfCharge && stats.Gold >= SoulCrystals.LesserBasePrice)
        {
            stats.Gold -= SoulCrystals.LesserBasePrice;
            inventory.Add(SoulCrystals.LesserId, SoulCrystals.LesserName, 1, SoulCrystals.ItemKind);
            return $"A lesser jiva stone, {SoulCrystals.LesserBasePrice} gold. They came dearer last season.";
        }

        if (stats.Gold >= PotionPrice)
        {
            stats.Gold -= PotionPrice;
            inventory.Add("health_potion", "Health Potion", 1, "potion");
            return "Potion for ten gold. Don't die out there.";
        }

        return "Come back with coin if you want supplies.";
    }

    public static GameObject Spawn(string name, Vector3 pos, Color color, string[] lines,
        bool merchant = false, bool questGiver = false, string modelId = null)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/Runtime/Npc");
        if (prefab == null)
            throw new MissingReferenceException("The runtime NPC prefab has not been generated.");
        var go = Object.Instantiate(prefab);
        var visual = CharacterLibrary.Instantiate(modelId, 2.1f);
        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            Object.Destroy(visual.GetComponent<Collider>());
            var renderer = visual.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); else material.color = color;
            renderer.sharedMaterial = material;
        }
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        go.name = "NPC_" + name.Replace(" ", "_");
        go.transform.position = pos;
        WorldTagger.SetLayerRecursive(go, GameLayers.Npc);
        var npc = go.GetComponent<NpcInteractable>();
        npc.NpcName = name;
        npc.Lines = lines;
        npc.IsMerchant = merchant;
        npc.IsQuestGiver = questGiver;
        return go;
    }
}
