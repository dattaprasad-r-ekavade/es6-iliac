using Microsoft.Xna.Framework;
using System;
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
    private readonly UiCanvas _ui;

    public OverlayRenderer(UiCanvas ui) => _ui = ui;

    public void DrawPause(OverlayState state)
    {
        _ui.Panel(UiLayout.FullScreen, new Color(3, 6, 10, 214), new Color(3, 6, 10, 0));

        var panel = UiLayout.PausePanel(state.InRun);
        _ui.Panel(panel, new Color(6, 12, 19, 246), new Color(151, 206, 210));
        _ui.TextCentred("PAUSED", panel.Center.X, panel.Y + 26f, 24, new Color(214, 226, 226));

        if (state.InRun)
        {
            _ui.TextCentred($"{state.RoomsCleared} rooms cleared  ·  {state.PendingStones} stones at risk",
                panel.Center.X, panel.Y + 62f, 14, new Color(151, 206, 210));
            _ui.TextCentred("Setting it aside keeps all of it. Giving up keeps none.",
                panel.Center.X, panel.Y + 84f, 13, new Color(150, 162, 170));
        }

        for (var index = 0; index < state.PauseItems.Count; index++)
        {
            var bounds = UiLayout.PauseItem(state.InRun, index);
            var selected = index == state.PauseSelection;
            var giveUp = state.PauseItems[index].StartsWith("Give up", StringComparison.Ordinal);

            _ui.Panel(bounds,
                selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected
                    ? giveUp ? new Color(214, 118, 96) : new Color(224, 181, 88)
                    : new Color(54, 82, 91));
            _ui.TextCentred(state.PauseItems[index], bounds.Center.X, bounds.Y + 10f, 16,
                selected ? Color.White : new Color(192, 207, 205));
        }

        _ui.TextCentred("Click or arrows select      Enter confirm      Esc resume",
            panel.Center.X, panel.Bottom - 30f, 13, new Color(140, 156, 164));
    }

    public void DrawSettings(OverlayState state)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(3, 7, 12, 214));
        var panel = new Rectangle(260, 92, 760, 536);
        _ui.Panel(panel, new Color(7, 14, 21, 248), new Color(91, 146, 159));
        _ui.Text("SETTINGS", new Vector2(panel.X + 32, panel.Y + 28), 28, Color.White);
        _ui.Text("Display, interface and current bindings", new Vector2(panel.X + 34, panel.Y + 70), 15,
            new Color(163, 191, 194));

        for (var index = 0; index < state.SettingsOptions.Count; index++)
        {
            var selected = index == state.SettingsSelection;
            var row = UiLayout.SettingsRow(index);
            _ui.Panel(row, selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                selected ? new Color(224, 181, 88) : new Color(54, 82, 91));
            _ui.TextFit(state.SettingsOptions[index], new Vector2(row.X + 16, row.Y + 10), row.Width - 32,
                16, selected ? Color.White : new Color(203, 216, 214));
        }

        _ui.Text("Up / Down select   Left / Right change value   Enter toggle display   Esc close",
            new Vector2(panel.X + 32, panel.Bottom - 38), 13, new Color(163, 191, 194));
    }

    public void DrawHelpOverlay(OverlayState state)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(3, 7, 12, 200));
        var panel = new Rectangle(300, 96, 680, 476);
        _ui.Panel(panel, new Color(7, 14, 21, 244), new Color(91, 146, 159));
        _ui.TextCentred("CONTROLS", panel.X + panel.Width / 2f, panel.Y + 26, 24, Color.White);

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
            _ui.Text(heading, new Vector2(x, panel.Y + 82f + line * 30f), 13,
                new Color(151, 206, 210));
            line++;
            placed++;

            foreach (var (key, action) in rows)
            {
                var y = panel.Y + 76f + line * 30f;
                _ui.Text(key, new Vector2(x, y), 16, new Color(232, 194, 116));
                _ui.TextFit(action, new Vector2(x + 112f, y), 184f, 16, new Color(214, 226, 222));
                line++;
                placed++;
            }
        }

        _ui.TextCentred($"This session is being recorded to {state.RecordingDirectory}",
            panel.X + panel.Width / 2f, panel.Bottom - 42f, 13, new Color(140, 156, 164));
    }
}
