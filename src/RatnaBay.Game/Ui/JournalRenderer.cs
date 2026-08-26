using Microsoft.Xna.Framework;
using RatnaBay.Domain;

namespace RatnaBay.Client;

internal sealed class JournalRenderer
{
    private readonly UiCanvas _ui;

    public JournalRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(PlayerCharacter player)
    {
        var panel = new Rectangle(200, 82, 880, 556);
        _ui.Panel(panel, new Color(5, 11, 18, 246), new Color(182, 137, 71));
        _ui.Text("JOURNAL", new Vector2(panel.X + 30, panel.Y + 24), 13,
            new Color(214, 183, 108));
        _ui.Text("Current work", new Vector2(panel.X + 30, panel.Y + 56), 28, Color.White);

        var quests = player.Quests.Quests;
        if (quests.Count == 0)
        {
            _ui.Text("No quests have been recorded.", new Vector2(panel.X + 30, panel.Y + 112), 17,
                new Color(174, 188, 186));
        }
        else
        {
            var y = panel.Y + 108;
            foreach (var quest in quests)
            {
                var colour = quest.IsCompleted ? new Color(143, 180, 142)
                    : quest.IsActive ? Color.White : new Color(142, 157, 157);
                _ui.TextFit(quest.Title, new Vector2(panel.X + 30, y), 440f, 19, colour);
                var state = quest.IsCompleted ? "COMPLETE"
                    : quest.IsActive ? quest.StageText : "Not accepted";
                _ui.TextFit(state, new Vector2(panel.X + 54, y + 30), 760f, 15,
                    quest.IsCompleted ? new Color(143, 180, 142) : new Color(203, 216, 214));
                y += 76;
                if (y > panel.Bottom - 70) break;
            }
        }

        _ui.Text("J / Esc close", new Vector2(panel.X + 30, panel.Bottom - 34), 13,
            new Color(163, 191, 194));
    }
}
