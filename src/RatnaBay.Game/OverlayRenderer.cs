using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// Renders modal pause/help/settings screens from a small presentation snapshot.
///
/// Input and game-flow decisions remain in Game1. This class only owns layout and styling,
/// which gives AI changes to a menu a bounded surface and prevents them from touching the
/// simulation loop by accident.
/// </summary>
internal sealed class OverlayRenderer
{
    private const int LogicalWidth = 1280;
    private const int LogicalHeight = 720;

    private readonly Action<Rectangle, Color, Color> _drawPanel;
    private readonly Action<string, Vector2, float, Color> _text;
    private readonly Action<string, Vector2, float, float, Color> _textFit;
    private readonly Action<string, float, float, float, Color> _textCentred;
    private readonly Action<Rectangle, Color> _fill;

    public OverlayRenderer(
        Action<Rectangle, Color, Color> drawPanel,
        Action<string, Vector2, float, Color> text,
        Action<string, Vector2, float, float, Color> textFit,
        Action<string, float, float, float, Color> textCentred,
        Action<Rectangle, Color> fill)
    {
        _drawPanel = drawPanel;
        _text = text;
        _textFit = textFit;
        _textCentred = textCentred;
        _fill = fill;
    }

    public void DrawPause(OverlayState state)
    {
        _drawPanel(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 6, 10, 214),
            new Color(3, 6, 10, 0));

        var panel = new Rectangle(400, 196, 480, state.InRun ? 332 : 268);
        _drawPanel(panel, new Color(6, 12, 19, 246), new Color(151, 206, 210));
        _textCentred("PAUSED", panel.Center.X, panel.Y + 26f, 24, new Color(214, 226, 226));

        var top = panel.Y + 78f;
        if (state.InRun)
        {
            _textCentred($"{state.RoomsCleared} rooms cleared  ·  {state.PendingStones} stones at risk",
                panel.Center.X, panel.Y + 62f, 14, new Color(151, 206, 210));
            _textCentred("Setting it aside keeps all of it. Giving up keeps none.",
                panel.Center.X, panel.Y + 84f, 13, new Color(150, 162, 170));
            top = panel.Y + 118f;
        }

        for (var index = 0; index < state.PauseItems.Count; index++)
        {
            var bounds = PauseItemBounds(state.InRun, index);
            var selected = index == state.PauseSelection;
            var giveUp = state.PauseItems[index].StartsWith("Give up", StringComparison.Ordinal);

            _drawPanel(bounds,
                selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected
                    ? giveUp ? new Color(214, 118, 96) : new Color(224, 181, 88)
                    : new Color(54, 82, 91));
            _textCentred(state.PauseItems[index], bounds.Center.X, bounds.Y + 10f, 16,
                selected ? Color.White : new Color(192, 207, 205));
        }

        _textCentred("Click or arrows select      Enter confirm      Esc resume",
            panel.Center.X, panel.Bottom - 30f, 13, new Color(140, 156, 164));
    }

    public void DrawSettings(OverlayState state)
    {
        _fill(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 7, 12, 214));
        var panel = new Rectangle(260, 92, 760, 536);
        _drawPanel(panel, new Color(7, 14, 21, 248), new Color(91, 146, 159));
        _text("SETTINGS", new Vector2(panel.X + 32, panel.Y + 28), 28, Color.White);
        _text("Display, interface and current bindings", new Vector2(panel.X + 34, panel.Y + 70), 15,
            new Color(163, 191, 194));

        for (var index = 0; index < state.SettingsOptions.Count; index++)
        {
            var selected = index == state.SettingsSelection;
            var row = SettingsRowBounds(index);
            _drawPanel(row, selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected ? new Color(224, 181, 88) : new Color(54, 82, 91));
            _textFit(state.SettingsOptions[index], new Vector2(row.X + 16, row.Y + 10), row.Width - 32,
                16, selected ? Color.White : new Color(203, 216, 214));
        }

        _text("Up / Down select   Left / Right change value   Enter toggle display   Esc close",
            new Vector2(panel.X + 32, panel.Bottom - 38), 13, new Color(163, 191, 194));
    }

    public void DrawHelpOverlay(OverlayState state)
    {
        _fill(new Rectangle(0, 0, LogicalWidth, LogicalHeight), new Color(3, 7, 12, 200));
        var panel = new Rectangle(300, 96, 680, 476);
        _drawPanel(panel, new Color(7, 14, 21, 244), new Color(91, 146, 159));
        _textCentred("CONTROLS", panel.X + panel.Width / 2f, panel.Y + 26, 24, Color.White);

        (string Heading, (string Key, string Action)[] Rows)[] sections =
        {
            ("MOVING", new[]
            {
                ("W A S D", "move"),
                ("Mouse", "look"),
                ("Arrow keys", "look, without the mouse"),
                ("Shift", "sprint — spends stamina"),
                ("Space", "jump")
            }),
            ("FIGHTING", new[]
            {
                ("Left click", "attack"),
                ("Right click", "guard — one-handed only"),
                ("Q", "cast the readied spell"),
                ("4 5 6 7 8", "flame, rime, arc, mend, emberlight")
            }),
            ("THE WORLD", new[]
            {
                ("E", "talk, open, take"),
                ("B", "trade with a merchant"),
                ("I", "character, pack and skills"),
                ("J", "journal")
            }),
            ("THE GAME", new[]
            {
                ("Esc", "close what is open, then the menu"),
                ("M", "back to the menu"),
                ("Tab", "release the mouse"),
                ("F5 / F9", "save / load"),
                ("F11", "windowed / fullscreen"),
                ("F1", "close this")
            })
        };

        var total = sections.Sum(section => section.Rows.Length + 1);
        var target = total / 2f;
        var column = 0;
        var placed = 0;
        var line = 0;

        foreach (var (heading, rows) in sections)
        {
            if (column == 0 && placed > 0 && placed + (rows.Length + 1) / 2f > target)
            {
                column = 1;
                line = 0;
            }

            var x = panel.X + 40f + column * 316f;
            _text(heading, new Vector2(x, panel.Y + 82f + line * 30f), 13,
                new Color(151, 206, 210));
            line++;
            placed++;

            foreach (var (key, action) in rows)
            {
                var y = panel.Y + 76f + line * 30f;
                _text(key, new Vector2(x, y), 16, new Color(232, 194, 116));
                _textFit(action, new Vector2(x + 112f, y), 184f, 16, new Color(214, 226, 222));
                line++;
                placed++;
            }
        }

        _textCentred($"This session is being recorded to {state.RecordingDirectory}",
            panel.X + panel.Width / 2f, panel.Bottom - 42f, 13, new Color(140, 156, 164));
    }

    private static Rectangle PauseItemBounds(bool inRun, int index)
    {
        var panel = new Rectangle(400, 196, 480, inRun ? 332 : 268);
        var top = inRun ? panel.Y + 118 : panel.Y + 78;
        return new Rectangle(panel.X + 40, top + index * 46, panel.Width - 80, 38);
    }

    private static Rectangle SettingsRowBounds(int index) =>
        new(284, 214 + index * 56, 712, 42);
}

internal sealed record OverlayState(
    bool InRun,
    int RoomsCleared,
    int PendingStones,
    IReadOnlyList<string> PauseItems,
    int PauseSelection,
    IReadOnlyList<string> SettingsOptions,
    int SettingsSelection,
    string RecordingDirectory);
