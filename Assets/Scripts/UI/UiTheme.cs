using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loads Kenney RPG UI sprites and styles panels / buttons.
/// Prefers Resources/UI (player builds); falls back to AssetDatabase in editor.
/// </summary>
public static class UiTheme
{
    public static Sprite PanelBrown;
    public static Sprite PanelBeige;
    public static Sprite PanelInset;
    public static Sprite ButtonLong;
    public static Sprite ButtonLongPressed;
    public static Sprite ButtonSquare;
    public static Sprite BarBack;
    public static Sprite BarRed;
    public static Sprite BarBlue;
    public static Sprite BarGreen;
    public static Sprite BarYellow;
    public static Sprite ArrowRight;
    public static Sprite IconCircle;

    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        PanelBrown = Load("panel_brown");
        PanelBeige = Load("panel_beige");
        PanelInset = Load("panelInset_beige");
        ButtonLong = Load("buttonLong_beige");
        ButtonLongPressed = Load("buttonLong_beige_pressed");
        ButtonSquare = Load("buttonSquare_brown");
        BarBack = Load("barBack_horizontalMid");
        BarRed = Load("barRed_horizontalMid");
        BarBlue = Load("barBlue_horizontalBlue") ?? Load("barBlue_horizontalLeft");
        BarGreen = Load("barGreen_horizontalMid");
        BarYellow = Load("barYellow_horizontalMid");
        ArrowRight = Load("arrowBrown_right");
        IconCircle = Load("iconCircle_beige");
    }

    private static Sprite Load(string name)
    {
        var fromRes = Resources.Load<Sprite>("UI/" + name);
        if (fromRes != null) return fromRes;

#if UNITY_EDITOR
        var path = $"Assets/ThirdParty/KenneyUI/UiPackRpg/PNG/{name}.png";
        var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
#endif
        return null;
    }

    public static void StylePanel(Image img, Sprite sprite, Color? tint = null)
    {
        if (img == null) return;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = tint ?? Color.white;
        }
        else
        {
            img.color = tint ?? new Color(0.12f, 0.1f, 0.08f, 0.94f);
        }
    }

    public static void StyleButton(Button btn, Sprite normal, Sprite pressed)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null && normal != null)
        {
            img.sprite = normal;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.88f, 1f);
        colors.pressedColor = new Color(0.85f, 0.8f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;
        if (pressed != null)
        {
            var spriteState = btn.spriteState;
            spriteState.pressedSprite = pressed;
            btn.spriteState = spriteState;
            btn.transition = Selectable.Transition.SpriteSwap;
        }
    }
}
