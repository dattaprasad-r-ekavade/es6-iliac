using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small in-game placeholder for the King's audience. It is deliberately data-light for
/// VS2, but it is a real clickable assignment screen rather than a developer-only hotkey.
/// </summary>
public sealed class GreyThreadAssignmentPanel : MonoBehaviour
{
    public event Action<string, string> Submitted;
    public bool IsVisible => _root != null && _root.activeSelf;

    private GameObject _root;
    private InputField _nameField;

    public void Show()
    {
        if (_root == null) Build();
        _root.SetActive(true);
        _nameField.Select();
        _nameField.ActivateInputField();
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Build()
    {
        UiTheme.EnsureLoaded();
        EnsureEventSystem();

        var canvasGo = new GameObject("VS2_AssignmentCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _root = new GameObject("AssignmentPanel", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvasGo.transform, false);
        var rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(760f, 620f);
        _root.GetComponent<Image>().color = UiTheme.Panel;

        MakeText(_root.transform, "Title", "THE KING'S AUDIENCE", 34, TextAnchor.UpperCenter,
            new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), UiTheme.WarmAccent);
        MakeText(_root.transform, "Prompt", "Every soul must contribute. State your name and inclination.",
            18, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.84f), UiTheme.Silver);

        MakeText(_root.transform, "NameLabel", "Name", 18, TextAnchor.MiddleLeft,
            new Vector2(0.12f, 0.63f), new Vector2(0.32f, 0.69f), UiTheme.MutedSilver);
        _nameField = MakeInput(_root.transform, "The Castaway", new Vector2(0.31f, 0.62f), new Vector2(0.88f, 0.70f));

        MakeText(_root.transform, "RouteLabel", "Choose your inclination", 18, TextAnchor.MiddleCenter,
            new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.60f), UiTheme.MutedSilver);
        MakeButton(_root.transform, "Warrior", "City Guard", "route.warrior", new Vector2(0.12f, 0.38f), new Vector2(0.48f, 0.50f));
        MakeButton(_root.transform, "Mage", "The Arcanum", "route.mage", new Vector2(0.52f, 0.38f), new Vector2(0.88f, 0.50f));
        MakeButton(_root.transform, "Trade", "Docks / Commerce", "route.trade", new Vector2(0.12f, 0.23f), new Vector2(0.48f, 0.35f));
        MakeButton(_root.transform, "Refuse", "Refuse Assignment", "route.refuse", new Vector2(0.52f, 0.23f), new Vector2(0.88f, 0.35f));
        MakeText(_root.transform, "Hint", "The rough slice uses placeholder training spaces; your choice changes the route.",
            15, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.17f), UiTheme.MutedSilver);
    }

    private void MakeButton(Transform parent, string title, string subtitle, string route, Vector2 min, Vector2 max)
    {
        var go = new GameObject(title + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var button = go.GetComponent<Button>();
        UiTheme.StyleButton(button, UiTheme.ButtonLong, UiTheme.ButtonLongPressed);
        button.onClick.AddListener(() => SubmitRoute(route));
        MakeText(go.transform, "Title", title, 22, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.92f), UiTheme.Silver);
        MakeText(go.transform, "Subtitle", subtitle, 14, TextAnchor.MiddleCenter,
            new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.48f), UiTheme.MutedSilver);
    }

    private InputField MakeInput(Transform parent, string placeholder, Vector2 min, Vector2 max)
    {
        var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.color = UiTheme.Inset;
        var input = go.GetComponent<InputField>();
        input.text = placeholder;
        input.characterLimit = 32;
        var text = MakeText(go.transform, "Text", placeholder, 20, TextAnchor.MiddleLeft,
            new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), UiTheme.Silver);
        input.textComponent = text;
        input.caretColor = UiTheme.WarmAccent;
        return input;
    }

    private void SubmitRoute(string route)
    {
        string name = _nameField != null ? _nameField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(name)) name = "The Castaway";
        Submitted?.Invoke(name, route);
    }

    private static Text MakeText(Transform parent, string name, string content, int size,
        TextAnchor anchor, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }
}
