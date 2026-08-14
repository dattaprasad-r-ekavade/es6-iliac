using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Protects the adopted Ratna Bay vocabulary at the player-facing data boundary. Internal
/// ids and old save aliases may retain codenames; dialogue, quests, NPC copy and menu text may not.
/// </summary>
public sealed class IndicContentTests
{
    private static readonly string[] RetiredPlayerTerms =
    {
        "kessil bay", "halbrand", "sarrakh", "caldemar", "estmere", "qadris",
        "everspire", "ivory concord", "the arcanum", "soul crystal", "black crystal",
        "osric selwyn", "terrin selwyn"
    };

    [Test]
    public void DialogueKnowledgeBaseUsesOnlyAdoptedVocabularyAndUniqueResolvers()
    {
        var topics = Resources.LoadAll<DialogueTopic>("Data/Dialogue");
        Assert.IsNotEmpty(topics);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var topic in topics)
        {
            Assert.IsTrue(ids.Add(topic.Id), $"Duplicate dialogue id: {topic.Id}");
            AssertAdopted($"{topic.Id} keyword", topic.Keyword);
            AssertAdopted($"{topic.Id} response", topic.Response);

            string conditions = string.Join(";", topic.Conditions
                .Select(c => $"{c.Key}|{c.Operator}|{c.Value}")
                .OrderBy(value => value, StringComparer.Ordinal));
            string signature = string.Join("::", topic.Keyword, topic.ActorId,
                topic.FactionId, conditions);
            Assert.IsTrue(signatures.Add(signature),
                $"Two dialogue assets have the same resolver signature: {signature}");
        }

        CollectionAssert.IsSubsetOf(new[]
        {
            "topic_jiva_stones", "topic_black_jiva", "topic_stambha", "topic_the_raja"
        }, ids);
    }

    [Test]
    public void QuestNpcAndProductCopyUsesAdoptedVocabulary()
    {
        foreach (var quest in Resources.LoadAll<QuestDefinition>("Data/Quests"))
            AssertAdopted($"quest {quest.Id}",
                $"{quest.Title}\n{quest.Description}\n{quest.InitialStageText}");

        foreach (var npc in Resources.LoadAll<NpcArchetype>("Data/Npcs"))
            AssertAdopted($"NPC {npc.Id}",
                $"{npc.DisplayName}\n{string.Join("\n", npc.Lines)}");

        Assert.AreEqual("Ratna Bay", PlayerSettings.productName);
        AssertSiteName("city_west", "Sabhapur");
        AssertSiteName("city_east", "Ratnapur");
        AssertSiteName("city_south", "Marukot");
    }

    [Test]
    public void GeneratedPlayerFacingTextDoesNotRegressToTheRetiredSetting()
    {
        string main = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/Main.unity"));
        StringAssert.Contains("m_Text: RATNA BAY", main);
        StringAssert.DoesNotContain("m_Text: KESSIL BAY", main);
        StringAssert.DoesNotContain("look out over the Kessil Bay", main);
        StringAssert.DoesNotContain("HALBRAND \\xB7 SARRAKH", main);

        foreach (string path in Directory.GetFiles(
                     Path.Combine(Application.dataPath, "Scenes/Chapter01"), "*.unity"))
            StringAssert.DoesNotContain("label: Back to Estmere", File.ReadAllText(path), path);
    }

    [Test]
    public void RuntimeNarrativeAndHudCopyDoesNotRegressToWesternTerms()
    {
        string runtimeCopy = string.Join("\n", new[]
        {
            ReadSource("Scripts/Game/GreyThreadDirector.cs"),
            ReadSource("Scripts/Game/GreyThreadAssignmentPanel.cs"),
            ReadSource("Scripts/Game/GameHud.cs"),
            ReadSource("Scripts/Game/NpcInteractable.cs"),
            ReadSource("Scripts/Game/SpellCaster.cs"),
            ReadSource("Scripts/Cinematics/IntroCutsceneDirector.cs")
        });

        foreach (string retiredPhrase in new[]
        {
            "The King's ship", "The King questions", "Terrin names",
            "soul-crystal delivery", "Crown Council recognition",
            "Recover health, mana", "no crystal to draw on",
            "Speak with Captain Alid in Caldemar"
        })
            StringAssert.DoesNotContain(retiredPhrase, runtimeCopy,
                $"Runtime player-facing copy still contains '{retiredPhrase}'.");
    }

    private static void AssertAdopted(string owner, string text)
    {
        string candidate = text ?? string.Empty;
        foreach (string retired in RetiredPlayerTerms)
            Assert.IsFalse(candidate.IndexOf(retired, StringComparison.OrdinalIgnoreCase) >= 0,
                $"{owner} still contains retired player-facing term '{retired}'.");
    }

    private static void AssertSiteName(string id, string expected)
    {
        WorldLayout.Site? site = WorldLayout.FindSite(id);
        Assert.IsTrue(site.HasValue, $"Missing world site: {id}");
        Assert.AreEqual(expected, site.Value.DisplayName);
    }

    private static string ReadSource(string assetsRelativePath) =>
        File.ReadAllText(Path.Combine(Application.dataPath,
            assetsRelativePath.Replace('/', Path.DirectorySeparatorChar)));
}
