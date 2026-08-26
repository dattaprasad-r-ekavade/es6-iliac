using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace RatnaBay.Client;

/// <summary>
/// The small, state-driven part of the in-world HUD.
///
/// Game1 still owns the SpriteBatch and font implementation, but this class owns the
/// layout and presentation of status information. Keeping the canvas as callbacks means
/// HUD work can be changed without reaching into the game loop or duplicating its rendering
/// primitives.
/// </summary>
internal sealed class HudRenderer
{
    private const int LogicalWidth = 1280;
    private const int LogicalHeight = 720;

    private readonly Action<Rectangle, Color, Color> _drawPanel;
    private readonly Action<string, Vector2, float, Color> _text;
    private readonly Action<string, float, float, float, Color> _textCentred;
    private readonly Action<string, float, float, float, Color> _textRight;

    public HudRenderer(
        Action<Rectangle, Color, Color> drawPanel,
        Action<string, Vector2, float, Color> text,
        Action<string, float, float, float, Color> textCentred,
        Action<string, float, float, float, Color> textRight)
    {
        _drawPanel = drawPanel;
        _text = text;
        _textCentred = textCentred;
        _textRight = textRight;
    }

    /// <summary>Domain events, rendered above the vitals. Newest last, fading as they expire.</summary>
    public void DrawToasts(GameSession? session)
    {
        if (session is null || session.Toasts.Count == 0) return;

        var y = LogicalHeight - 196f - session.Toasts.Count * 28f;
        foreach (var toast in session.Toasts)
        {
            var alpha = MathHelper.Clamp(toast.Remaining, 0f, 1f);
            _textCentred(toast.Message, LogicalWidth / 2f, y, 17,
                new Color(240, 230, 202) * alpha);
            y += 28f;
        }
    }

    /// <summary>Level, gold and the one key that opens the rest. Bottom-right, compact.</summary>
    public void DrawStatusStrip(GameSession? session, float framesPerSecond)
    {
        if (session is null) return;

        var vitals = session.Player.Vitals;
        var panel = new Rectangle(LogicalWidth - 264, LogicalHeight - 88, 240, 64);
        _drawPanel(panel, new Color(6, 13, 20, 226), new Color(76, 101, 116));

        _text($"LEVEL {vitals.Level}", new Vector2(panel.X + 18, panel.Y + 12), 16, Color.White);
        _textRight($"{vitals.Gold} gold", panel.Right - 18, panel.Y + 12, 16,
            new Color(228, 197, 122));
        var combat = session.Player.Combat;
        _text(combat.ActiveWeapon.DisplayName, new Vector2(panel.X + 18, panel.Y + 38), 13,
            combat.IsBlocking ? new Color(232, 194, 116) : new Color(203, 216, 214));
        // Blank until the first averaging window closes: the counter used to show whatever
        // the opening, texture-generating window computed, so a build running at 700 fps
        // could report 4. A misleading diagnostic is worse than none.
        _textRight(framesPerSecond > 0f ? $"{framesPerSecond:0} fps" : "— fps",
            panel.Right - 18, panel.Y + 38, 13,
            framesPerSecond is > 0f and < 50f
                ? new Color(228, 128, 118)
                : new Color(146, 174, 178));
    }
}
