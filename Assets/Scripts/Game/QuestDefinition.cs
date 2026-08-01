using UnityEngine;

[CreateAssetMenu(menuName = "Kessil/Quest Definition", fileName = "QuestDefinition")]
public sealed class QuestDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string title;
    [SerializeField, TextArea] private string description;
    [SerializeField] private string initialStageText;
    [SerializeField] private int targetCount;
    [SerializeField] private string targetEnemy;
    [SerializeField] private string targetLocationId;

    public string Id => id;
    public string Title => title;
    public string Description => description;
    public string InitialStageText => initialStageText;
    public int TargetCount => targetCount;
    public string TargetEnemy => targetEnemy;
    public string TargetLocationId => targetLocationId;
}
