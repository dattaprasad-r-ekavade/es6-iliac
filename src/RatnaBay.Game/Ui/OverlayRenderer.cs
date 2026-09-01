using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace RatnaBay.Client;

/// <summary>
/// Renders modal pause/help/settings screens from a small presentation snapshot, and the
/// custom mouse pointer.
///
/// Overlay-stack selection lives in OverlayInput. Game1 still owns what confirming a row
/// does. This class only owns layout and styling. Game1 still decides whether the pointer
/// is visible.
/// </summary>
internal sealed class OverlayRenderer
{
    private readonly UiCanvas _ui;

    public OverlayRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>
    /// Where to say something, on the two screens somebody reaches when they stop.
    ///
    /// The alpha's first outside player spent 110 minutes unable to open the first door and
    /// then quit. They had no way to say so from inside the game: the feedback form existed
    /// only on the itch page, which is a page you read before you play and never see again.
    /// An address short enough to read off the screen and type is worth more here than the
    /// form's seventy-character URL, which nobody is going to copy by hand.
    /// </summary>
    private const string FeedbackLine =
        "Stuck, or something broken?  feedback@datathecodie.com  ·  the form is on the itch page";

    public void DrawPause(OverlayState state)
    {
        _ui.Scrim();

        var panel = UiLayout.PausePanel(state.InRun);
        _ui.Panel(panel, UiTheme.PanelRaised, UiTheme.Accent);
        _ui.TextCentred("PAUSED", panel.Center.X, panel.Y + 26f, 24, UiTheme.Heading);

        if (state.InRun)
        {
            _ui.TextCentred($"{state.RoomsCleared} rooms cleared  ·  {state.PendingStones} stones at risk",
                panel.Center.X, panel.Y + 62f, 14, UiTheme.Accent);
            _ui.TextCentred("Setting it aside keeps all of it. Giving up keeps none.",
                panel.Center.X, panel.Y + 84f, 13, UiTheme.Muted);
        }

        for (var index = 0; index < state.PauseItems.Count; index++)
        {
            var bounds = UiLayout.PauseItem(state.InRun, index);
            var selected = index == state.PauseSelection;
            var giveUp = state.PauseItems[index].StartsWith("Give up", StringComparison.Ordinal);

            _ui.Row(bounds, selected, danger: giveUp);
            _ui.TextCentred(state.PauseItems[index], bounds.Center.X, bounds.Y + 10f, 16,
                UiTheme.RowText(selected));
        }

        _ui.TextCentred("Click or arrows select      Enter confirm      Esc resume",
            panel.Center.X, panel.Bottom - 30f, 13, UiTheme.HintDim);

        // Under the controls line, because somebody who has opened this menu to give up is
        // exactly the person worth hearing from and the least likely to go looking.
        _ui.TextCentred(FeedbackLine, panel.Center.X, panel.Bottom - 12f, 12, UiTheme.Muted);
    }

    public void DrawSettings(OverlayState state)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(3, 7, 12, 214));
        var panel = new Rectangle(260, 92, 760, 536);
        _ui.Panel(panel, new Color(7, 14, 21, 248), UiTheme.Border);
        _ui.Text("SETTINGS", new Vector2(panel.X + 32, panel.Y + 28), 28, Color.White);
        _ui.Text("Display, interface and current bindings", new Vector2(panel.X + 34, panel.Y + 70), 15,
            UiTheme.Hint);

        for (var index = 0; index < state.SettingsOptions.Count; index++)
        {
            var selected = index == state.SettingsSelection;
            var row = UiLayout.SettingsRow(index);
            _ui.Row(row, selected);
            _ui.TextFit(state.SettingsOptions[index], new Vector2(row.X + 16, row.Y + 10), row.Width - 32,
                16, selected ? Color.White : UiTheme.Body);
        }

        _ui.Text("Up / Down select   Left / Right change value   Enter toggle display   Esc close",
            new Vector2(panel.X + 32, panel.Bottom - 38), 13, UiTheme.Hint);
    }

    public void DrawHelpOverlay(OverlayState state)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(3, 7, 12, 200));
        var panel = new Rectangle(300, 96, 680, 476);
        _ui.Panel(panel, new Color(7, 14, 21, 244), UiTheme.Border);
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
                UiTheme.Accent);
            line++;
            placed++;

            foreach (var (key, action) in rows)
            {
                var y = panel.Y + 76f + line * 30f;
                _ui.Text(key, new Vector2(x, y), 16, UiTheme.Gold);
                _ui.TextFit(action, new Vector2(x + 112f, y), 184f, 16, new Color(214, 226, 222));
                line++;
                placed++;
            }
        }

        _ui.TextCentred($"This session is being recorded to {state.RecordingDirectory}",
            panel.X + panel.Width / 2f, panel.Bottom - 42f, 13, UiTheme.HintDim);

        _ui.TextCentred(FeedbackLine, panel.X + panel.Width / 2f, panel.Bottom - 22f, 12,
            UiTheme.Muted);
    }

    /// <summary>
    /// Our own mouse pointer: an arrow with a dark skirt so it survives any background.
    ///
    /// Game1 decides whether to call this — it depends on whether the mouse is driving the
    /// camera and whether the frame is a capture, and neither is something a renderer should
    /// know. Given a logical position, this just paints the arrow.
    /// </summary>
    public void DrawPointer(Vector2 position)
    {
        if (position.X < -8f || position.Y < -8f
            || position.X > UiLayout.Width + 8f || position.Y > UiLayout.Height + 8f) return;

        var x = (int)position.X;
        var y = (int)position.Y;

        for (var row = 0; row < 15; row++)
        {
            var width = row < 11 ? row + 1 : 15 - row + 2;
            if (width <= 0) continue;

            _ui.Fill(new Rectangle(x - 1, y + row - 1, width + 2, 3), UiTheme.PointerSkirt);
        }

        for (var row = 0; row < 15; row++)
        {
            var width = row < 11 ? row + 1 : 15 - row + 2;
            if (width <= 0) continue;

            _ui.Fill(new Rectangle(x, y + row, width, 1), Color.White);
        }
    }
}
