using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared presentation palette for the game's restrained, carved-metal UI.
/// Kenney sprites are retained as subtle nine-slice borders, but the bright
/// parchment treatment is replaced with translucent charcoal and cool silver.
/// </summary>
public static class UiTheme
{
    public static readonly Color Panel = new Color(0.035f, 0.04f, 0.045f, 0.94f);
    public static readonly Color PanelSoft = new Color(0.055f, 0.06f, 0.065f, 0.88f);
    public static readonly Color Inset = new Color(0.015f, 0.018f, 0.022f, 0.90f);
    public static readonly Color Silver = new Color(0.82f, 0.84f, 0.82f, 1f);
    public static readonly Color MutedSilver = new Color(0.58f, 0.61f, 0.60f, 1f);
    public static readonly Color WarmAccent = new Color(0.70f, 0.58f, 0.38f, 1f);

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
            if (sprite == IconCircle)
                img.color = tint ?? Silver;
            else if (sprite == PanelInset || sprite == PanelBeige)
                img.color = IsWhite(tint) ? Inset : tint ?? Inset;
            else
                img.color = IsWhite(tint) ? Panel : tint ?? Panel;
        }
        else
        {
            img.color = tint ?? Panel;
        }
    }

    public static void StyleButton(Button btn, Sprite normal, Sprite pressed)
    {
        if (btn == null) return;
        var img = btn.targetGraphic as Image;
        if (img != null)
        {
            if (normal != null)
            {
                img.sprite = normal;
                img.type = Image.Type.Sliced;
            }
            img.color = new Color(0.08f, 0.085f, 0.09f, 0.96f);
        }
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.32f, 1f);
        colors.pressedColor = new Color(0.72f, 0.74f, 0.73f, 1f);
        colors.selectedColor = new Color(1.18f, 1.15f, 1.08f, 1f);
        colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        btn.transition = Selectable.Transition.ColorTint;
    }

    private static bool IsWhite(Color? value)
    {
        if (!value.HasValue) return false;
        var c = value.Value;
        return c.r > 0.98f && c.g > 0.98f && c.b > 0.98f;
    }
}
