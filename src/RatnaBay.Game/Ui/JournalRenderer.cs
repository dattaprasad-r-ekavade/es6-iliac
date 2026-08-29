using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

internal sealed class JournalRenderer
{
    /// <summary>Entries the panel will grow to hold before it stops and scrolls nothing.</summary>
    private const int MaxEntries = 5;

    private readonly UiCanvas _ui;

    public JournalRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(PlayerCharacter player)
    {
        var quests = player.Quests.Quests;

        // Sized to what is in it, the way the pause screen is.
        //
        // It was a fixed 880 by 556 whatever it held, so one quest sat in the top corner of a
        // slab of empty panel taller than the run summary. A panel that does not fit its
        // contents reads as a screen that is missing something.
        var body = quests.Count == 0 ? 1 : Math.Min(quests.Count, MaxEntries);
        var height = Math.Clamp(150 + body * 76, 220, 556);

        const int width = 700;
        var panel = new Rectangle((UiLayout.Width - width) / 2, (UiLayout.Height - height) / 2,
            width, height);
        _ui.Panel(panel, new Color(5, 11, 18, 246), new Color(182, 137, 71));
        _ui.Text("JOURNAL", new Vector2(panel.X + 30, panel.Y + 24), 13,
            UiTheme.GoldDim);
        _ui.Text("Current work", new Vector2(panel.X + 30, panel.Y + 56), 28, Color.White);

        if (quests.Count == 0)
        {
            _ui.Text("No quests have been recorded.", new Vector2(panel.X + 30, panel.Y + 112), 17,
                UiTheme.Prompt);
        }
        else
        {
            var y = panel.Y + 108;
            foreach (var quest in quests)
            {
                var colour = quest.IsCompleted ? new Color(143, 180, 142)
                    : quest.IsActive ? Color.White : UiTheme.Faint;
                _ui.TextFit(quest.Title, new Vector2(panel.X + 30, y), 440f, 19, colour);
                var state = quest.IsCompleted ? "COMPLETE"
                    : quest.IsActive ? quest.StageText : "Not accepted";
                _ui.TextFit(state, new Vector2(panel.X + 54, y + 30), 760f, 15,
                    quest.IsCompleted ? new Color(143, 180, 142) : UiTheme.Body);
                y += 76;
                if (y > panel.Bottom - 60) break;
            }
        }

        _ui.Text("J / Esc close", new Vector2(panel.X + 30, panel.Bottom - 34), 13,
            UiTheme.Hint);
    }
}
