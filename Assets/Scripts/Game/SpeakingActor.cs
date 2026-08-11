using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A named character you can talk to, using the topic knowledge base.
///
/// Morrowind's model, not a conversation tree: the player picks a keyword and the actor
/// answers from a shared pool, filtered by who they are, where they are, and what the story
/// state says. <see cref="TopicDialogueService"/> already did all of that — it simply had
/// nothing in any scene pointing at it.
///
/// Topics offered are the intersection of what this actor can answer and what the player
/// knows to ask, which is what makes learning a keyword from one person and taking it to
/// another the core verb rather than a feature.
/// </summary>
public sealed class SpeakingActor : MonoBehaviour
{
    [SerializeField] private string actorId;
    [SerializeField] private string displayName = "Citizen";
    [SerializeField] private string factionId;
    [SerializeField] private string locationId;

    /// <summary>Keywords this actor volunteers on a first meeting, teaching them to the player.</summary>
    [SerializeField] private string[] opensWith = { "estmere" };

    public string ActorId => actorId;
    public string DisplayName => displayName;

    public void Configure(string id, string display, string faction, string location, params string[] opening)
    {
        actorId = id;
        displayName = display;
        factionId = faction;
        locationId = location;
        if (opening != null && opening.Length > 0) opensWith = opening;
    }

    public DialogueContext Context => new()
    {
        ActorId = actorId,
        FactionId = factionId,
        LocationId = locationId,
        Disposition = 50
    };

    /// <summary>
    /// Topics this actor will actually answer right now. Filtered through the same resolver
    /// the response uses, so the menu can never offer a keyword that produces silence.
    /// </summary>
    public List<string> AvailableTopics()
    {
        var available = new List<string>();
        var service = TopicDialogueService.Instance;
        if (service == null) return available;

        foreach (var keyword in service.KnownTopics)
            if (service.Resolve(keyword, Context) != null)
                available.Add(keyword);

        available.Sort(System.StringComparer.OrdinalIgnoreCase);
        return available;
    }

    /// <summary>Ask about a keyword. Null when this actor has nothing to say about it.</summary>
    public string Ask(string keyword) => TopicDialogueService.Instance?.Respond(keyword, Context);

    /// <summary>
    /// Open conversation. Teaches whatever this actor volunteers, then hands the topic list to
    /// the HUD.
    /// </summary>
    public void Talk()
    {
        var service = TopicDialogueService.Instance;
        if (service != null)
            foreach (var keyword in opensWith ?? System.Array.Empty<string>())
                service.LearnTopic(keyword);

        var topics = AvailableTopics();
        if (topics.Count == 0)
        {
            GameHud.Instance?.ShowDialogue(displayName, "They have nothing to say to you.");
            return;
        }

        GameHud.Instance?.ShowTopicMenu(this, topics);
    }
}
