using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public sealed class RuntimePrefabTests
{
    [Test]
    public void PlayerPrefabOwnsCoreGameplayComponents()
    {
        var prefab = Resources.Load<GameObject>("Prefabs/Runtime/Player");
        Assert.IsNotNull(prefab);
        Assert.IsNotNull(prefab.GetComponent<CharacterController>());
        Assert.IsNotNull(prefab.GetComponent<SimplePlayerController>());
        Assert.IsNotNull(prefab.GetComponent<PlayerStats>());
        Assert.IsNotNull(prefab.GetComponent<PlayerInventory>());
        Assert.IsNotNull(prefab.GetComponent<PlayerCombat>());
        Assert.IsNotNull(prefab.GetComponent<PlayerInteract>());
        Assert.IsNotNull(prefab.GetComponent<PlayerSafetyGuard>());
        Assert.IsNotNull(prefab.GetComponentInChildren<Camera>(true));
        Assert.IsNotNull(prefab.GetComponentInChildren<AudioListener>(true));
    }

    [Test]
    public void SystemsPrefabOwnsCoreServices()
    {
        var prefab = Resources.Load<GameObject>("Prefabs/Runtime/GameSystems");
        Assert.IsNotNull(prefab);
        Assert.IsNotNull(prefab.GetComponent<GameSfx>());
        Assert.IsNotNull(prefab.GetComponent<StoryDirector>());
        Assert.IsNotNull(prefab.GetComponent<TopicDialogueService>());
        Assert.IsNotNull(prefab.GetComponent<CinematicRunner>());
        Assert.IsNotNull(prefab.GetComponent<GameSystemsBootstrap>());
        Assert.IsNotNull(prefab.GetComponent<TimeWeatherSystem>());
        Assert.IsNotNull(prefab.GetComponent<DiscoveryTravelSystem>());
        Assert.IsNotNull(prefab.GetComponent<QuestSystem>());
        Assert.IsNotNull(prefab.GetComponent<GameHud>());
        Assert.IsNotNull(prefab.GetComponent<SaveLoadService>());
        Assert.IsNotNull(prefab.GetComponent<AudioSource>());
    }

    [Test]
    public void DialogueKnowledgeBaseContainsInspectableStableTopics()
    {
        var topics = Resources.LoadAll<DialogueTopic>("Data/Dialogue");
        Assert.GreaterOrEqual(topics.Length, 3);
        var ids = new HashSet<string>();
        foreach (var topic in topics)
        {
            Assert.IsTrue(ids.Add(topic.Id), $"Duplicate dialogue topic id: {topic.Id}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(topic.Keyword));
            Assert.IsFalse(string.IsNullOrWhiteSpace(topic.Response));
        }
        Assert.IsTrue(System.Array.Exists(topics, t =>
            t.Id == "topic_black_crystals" && System.Linq.Enumerable.Any(t.Conditions,
                c => c.Key == "evidence_count" && c.Operator == "min")));
    }

    [Test]
    public void QuestAndCinematicContractsAreAuthoredAssets()
    {
        var quests = Resources.LoadAll<QuestDefinition>("Data/Quests");
        Assert.AreEqual(3, quests.Length);
        Assert.IsTrue(System.Array.Exists(quests, q => q.Id == "bounty_bandits" && q.TargetCount == 3));
        var cinematics = Resources.LoadAll<CinematicSequence>("Data/Cinematics");
        Assert.AreEqual(1, cinematics.Length);
        Assert.AreEqual("cin.title_crawl", cinematics[0].Id);
        Assert.IsTrue(System.Array.Exists(cinematics[0].EndState, f => f.Id == "flag.title_crawl_shown"));
    }

    [Test]
    public void NpcArchetypesUseStableUniqueIdsAndKnownAnchors()
    {
        var archetypes = Resources.LoadAll<NpcArchetype>("Data/Npcs");
        Assert.AreEqual(5, archetypes.Length);
        var ids = new HashSet<string>();
        foreach (var archetype in archetypes)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(archetype.Id));
            Assert.IsTrue(ids.Add(archetype.Id), $"Duplicate NPC id: {archetype.Id}");
            Assert.IsNotNull(WorldLayout.FindSite(archetype.AnchorSiteId),
                $"Unknown site for {archetype.Id}: {archetype.AnchorSiteId}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(archetype.DisplayName));
            Assert.AreNotEqual(archetype.DisplayName, archetype.Id);
        }
    }

    [Test]
    public void NpcAndHudPrefabsOwnTheirRuntimeRoots()
    {
        var npc = Resources.Load<GameObject>("Prefabs/Runtime/Npc");
        Assert.IsNotNull(npc);
        Assert.IsNotNull(npc.GetComponent<NpcInteractable>());
        Assert.IsNotNull(npc.GetComponent<CapsuleCollider>());

        var hud = Resources.Load<GameObject>("Prefabs/Runtime/Hud");
        Assert.IsNotNull(hud);
        Assert.IsNotNull(hud.GetComponent<Canvas>());
        Assert.IsNotNull(hud.transform.Find("Hud/Vitals/HealthBg/HealthFill"));
        Assert.IsNotNull(hud.transform.Find("MapPanel/Card/MapFrame/MapImage"));
        Assert.IsNotNull(hud.transform.Find("DialoguePanel/DlgCard/Body"));
    }
}
