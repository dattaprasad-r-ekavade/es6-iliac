using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

internal sealed class DialogueRenderer
{
    private readonly UiCanvas _ui;
    private readonly GraphicsDevice _device;

    public DialogueRenderer(UiCanvas ui, GraphicsDevice device)
    {
        _ui = ui;
        _device = device;
    }

    public void Draw(SpeakingActor actor, string response, int selection)
    {
        var topics = actor.AvailableTopics();
        var panel = UiLayout.ConversationPanel;
        var portrait = UiLayout.ConversationPortrait;
        _ui.Scrim(UiTheme.Scrim, UiTheme.NoBorder);
        _ui.Fill(panel, UiTheme.Panel);
        _ui.Text("RATNA BAY", new Vector2(572, panel.Y + 24), 14, UiTheme.Accent);
        DialoguePortraits.Draw(_ui, _device, actor.ActorId, portrait);
        _ui.PortraitEdge(portrait, UiTheme.Panel);
        _ui.TextFit(actor.DisplayName, new Vector2(572, 140), 596, 32, UiTheme.Heading);
        _ui.Text("IN CONVERSATION", new Vector2(574, 192), 14, UiTheme.Accent);
        _ui.TextWrapped(response, new Vector2(572, 232), 596, 17, UiTheme.Body, maxLines: 5);

        var start = Math.Max(0, selection) / UiLayout.DialogueRows * UiLayout.DialogueRows;
        for (var index = start; index < topics.Count && index < start + UiLayout.DialogueRows; index++)
        {
            var row = UiLayout.DialogueTopic(index - start);
            var (fill, border) = UiTheme.Row(index == selection);
            _ui.Row(row, fill, border);
            _ui.TextFit($"{index + 1}.  {topics[index]}", new Vector2(row.X + 14, row.Y + 6),
                row.Width - 28, 16, UiTheme.RowText(index == selection));
        }
        if (topics.Count == 0)
            _ui.Text("Nothing you know to ask reaches them.", new Vector2(572, 370), 17, UiTheme.Muted);

        _ui.Text("Arrows choose  /  Enter ask", new Vector2(572, 600), 15, UiTheme.Hint);
        var leave = UiLayout.ConversationLeave;
        var (leaveFill, leaveBorder) = UiTheme.Row(false);
        _ui.Row(leave, leaveFill, leaveBorder);
        _ui.TextCentred("Esc  /  Leave", leave.Center.X, leave.Y + 11, 15, UiTheme.Body);
        _ui.TextCentred(actor.DisplayName, portrait.Center.X, 596, 22, UiTheme.Heading);
    }
}
