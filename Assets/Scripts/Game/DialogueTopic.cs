using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DialogueCondition
{
    public string Key;
    public string Operator = "equals";
    public string Value;
}

[CreateAssetMenu(menuName = "Kessil/Dialogue Topic", fileName = "DialogueTopic")]
public sealed class DialogueTopic : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string keyword;
    [SerializeField] private string actorId;
    [SerializeField] private string factionId;
    [SerializeField] private string response;
    [SerializeField] private DialogueCondition[] conditions;

    public string Id => id;
    public string Keyword => keyword;
    public string ActorId => actorId;
    public string FactionId => factionId;
    public string Response => response;
    public IReadOnlyList<DialogueCondition> Conditions => conditions ?? Array.Empty<DialogueCondition>();
}

public sealed class DialogueContext
{
    public string ActorId;
    public string FactionId;
    public string LocationId;
    public int Disposition;
}
