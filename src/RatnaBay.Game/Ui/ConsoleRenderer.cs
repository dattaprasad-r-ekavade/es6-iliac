using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// The developer console, and the values pinned beside it.
///
/// Drawing only. What the commands do lives in <c>GameConsole</c>, and what the keys mean
/// lives in <c>Game1</c>; this was the last screen still painting through the game loop's own
/// helpers rather than through <see cref="UiCanvas"/>.
/// </summary>
internal sealed class ConsoleRenderer
{
    /// <summary>How much of the screen the log takes when it is open.</summary>
    private const int LogHeight = 330;

    private readonly UiCanvas _ui;

    public ConsoleRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>
    /// The log, the prompt, and a caret that blinks.
    ///
    /// Over the top of the game rather than pausing it: the one time anybody opens this is
    /// when they want to see what is happening while they type.
    /// </summary>
    public void Draw(IReadOnlyList<ConsoleLine> output, string input, float clock)
    {
        var panel = new Rectangle(0, 0, UiLayout.Width, LogHeight);
        _ui.Fill(panel, new Color(4, 8, 13, 242));
        _ui.Fill(new Rectangle(0, panel.Bottom - 2, UiLayout.Width, 2), UiTheme.Accent);

        // Newest at the bottom, next to the prompt, so the eye does not have to travel.
        var y = panel.Bottom - 62f;
        for (var index = output.Count - 1; index >= 0 && y > 8f; index--)
        {
            var line = output[index];
            var colour = line.Tone switch
            {
                ConsoleTone.Echo => UiTheme.Accent,
                ConsoleTone.Error => UiTheme.Error,
                _ => new Color(206, 216, 214)
            };

            _ui.TextFit(line.Text, new Vector2(16, y), UiLayout.Width - 32, 14, colour);
            y -= 20f;
        }

        var prompt = new Rectangle(8, panel.Bottom - 44, UiLayout.Width - 16, 30);
        _ui.Fill(prompt, new Color(12, 22, 30, 250));
        _ui.Border(prompt, new Color(76, 112, 124));

        _ui.Text(">", new Vector2(prompt.X + 10, prompt.Y + 6), 15, UiTheme.Gold);
        _ui.TextFit(input, new Vector2(prompt.X + 28, prompt.Y + 6), prompt.Width - 48, 15,
            Color.White);

        // A caret that blinks, so an empty prompt still looks alive.
        if ((int)(clock * 2f) % 2 == 0)
        {
            var width = input.Length * 7.4f;
            _ui.Fill(new Rectangle((int)(prompt.X + 30 + width), prompt.Y + 7, 2, 17),
                UiTheme.Gold);
        }

        _ui.TextRight("~ or Esc closes   ·   Tab completes   ·   Up walks back",
            prompt.Right - 12, prompt.Y + 8, 12, new Color(120, 140, 148));
    }

    /// <summary>
    /// Watched commands, re-run every frame and pinned to the corner.
    ///
    /// Out of the way of the crosshair on purpose: the point of a watch is to see a number
    /// move while playing, which stops working if it sits where the fight is.
    /// </summary>
    public void DrawWatches(IReadOnlyList<string> output)
    {
        if (output.Count == 0) return;

        var panel = new Rectangle(UiLayout.Width - 430, 150, 410, 16 + output.Count * 18);
        _ui.Fill(panel, new Color(4, 8, 13, 214));
        _ui.Border(panel, new Color(96, 132, 142));

        var y = panel.Y + 12f;
        foreach (var line in output)
        {
            _ui.TextFit(line, new Vector2(panel.X + 12, y), panel.Width - 24, 13,
                new Color(196, 214, 212));
            y += 18f;
        }
    }
}
