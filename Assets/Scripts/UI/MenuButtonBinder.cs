using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires menu buttons at runtime so they survive scene save.
/// </summary>
public class MenuButtonBinder : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameFlowController flow;

    private void Awake()
    {
        if (flow == null) flow = FindAnyObjectByType<GameFlowController>();
        if (startButton != null) startButton.onClick.AddListener(() => flow?.OnClickStart());
        if (continueButton != null) continueButton.onClick.AddListener(() => flow?.OnClickContinue());
        if (quitButton != null) quitButton.onClick.AddListener(() => flow?.OnClickQuit());
    }
}
