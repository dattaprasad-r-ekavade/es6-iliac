using Microsoft.Xna.Framework;
using RatnaBay.Domain;

namespace RatnaBay.Client;

internal sealed class DialogueRenderer
{
    private readonly UiCanvas _ui;

    public DialogueRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(SpeakingActor actor, string response, int selection)
    {
        var topics = actor.AvailableTopics();
        var panel = UiLayout.DialoguePanel;
        _ui.Panel(panel, new Color(5, 11, 18, 248), new Color(151, 206, 210));

        _ui.Text(actor.DisplayName, new Vector2(panel.X + 24, panel.Y + 20), 26, Color.White);
        _ui.TextWrapped(response, new Vector2(panel.X + 24, panel.Y + 62),
            panel.Width - 48, 18, new Color(216, 228, 223), maxLines: 4);

        if (topics.Count == 0)
        {
            _ui.Text("Nothing you know to ask reaches them.",
                new Vector2(panel.X + 24, UiLayout.DialogueTopic(0).Y + 6), 17,
                new Color(174, 188, 186));
        }
        else
        {
            for (var index = 0; index < topics.Count && index < UiLayout.DialogueRows; index++)
            {
                var selected = index == selection;
                var row = UiLayout.DialogueTopic(index);
                _ui.Fill(row, selected ? new Color(74, 67, 43, 240) : new Color(17, 27, 35, 190));
                _ui.Text($"{index + 1}. {topics[index]}", new Vector2(row.X + 12, row.Y + 6), 17,
                    selected ? new Color(245, 209, 124) : new Color(206, 219, 217));
            }

            if (topics.Count > UiLayout.DialogueRows)
                _ui.Text($"+{topics.Count - UiLayout.DialogueRows} more",
                    new Vector2(panel.X + 24, UiLayout.DialogueTopic(UiLayout.DialogueRows).Y + 4), 14,
                    new Color(142, 157, 157));
        }

        _ui.Text("Enter ask      Esc close", new Vector2(panel.X + 24, panel.Bottom - 30), 15,
            new Color(170, 197, 200));
    }
}
