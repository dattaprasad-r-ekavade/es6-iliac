using Microsoft.Xna.Framework;

namespace RatnaBay.Client;

/// <summary>
/// The question, in the words it deserves.
///
/// Short enough to be read rather than dismissed, specific about what is sent, and honest
/// that the answer changes nothing about the game. "Yes" is not the default and is not
/// styled to look like the safe one.
/// </summary>
internal sealed class ConsentRenderer
{
    private readonly UiCanvas _ui;

    public ConsentRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(int selection)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(6, 10, 16));

        // Tall enough for the text and the two answers to be separate things. The first
        // version put the buttons through the last three lines of the explanation, which
        // is a poor look on the one screen that is asking permission.
        var panel = new Rectangle(300, 120, 680, 484);
        _ui.Panel(panel, new Color(8, 16, 24, 250), UiTheme.Accent);

        _ui.TextCentred("BEFORE YOU PLAY", panel.Center.X, panel.Y + 30f, 26, Color.White);
        _ui.TextCentred("This is an alpha, and it is being tuned from how people actually play.",
            panel.Center.X, panel.Y + 76f, 15, new Color(190, 203, 200));

        var lines = new[]
        {
            "May the game send a record of your runs to its developer?",
            "",
            "What it sends:  rooms cleared, what killed what, how long you",
            "spent deciding at a door, what you bought, when you died.",
            "",
            "What it never sends:  your name, your files, your location, or",
            "anything you type. There is nothing to type.",
            "",
            "You are a random number to us, and deleting the game forgets it.",
            "The game plays exactly the same either way, and you can change",
            "this whenever you like in Settings."
        };

        for (var index = 0; index < lines.Length; index++)
            _ui.TextCentred(lines[index], panel.Center.X, panel.Y + 128f + index * 24f, 14,
                index == 0 ? UiTheme.Heading : new Color(172, 186, 190));

        string[] answers = { "Yes, send them", "No, keep them here" };
        for (var index = 0; index < answers.Length; index++)
        {
            var bounds = UiLayout.ConsentButton(index);
            var selected = index == selection;

            _ui.Row(bounds, selected);
            _ui.TextCentred(answers[index], bounds.Center.X, bounds.Y + 14f, 16,
                UiTheme.RowText(selected));
        }
    }
}
