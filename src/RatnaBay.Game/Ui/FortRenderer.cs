using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

/// <summary>The fort directory and a close-up, one-passage-at-a-time conversation.</summary>
internal sealed class FortRenderer
{
    private readonly UiCanvas _ui;
    private readonly GraphicsDevice _device;

    public FortRenderer(UiCanvas ui, GraphicsDevice device)
    {
        _ui = ui;
        _device = device;
    }

    public static Rectangle DoorRow(int index) => UiLayout.FortDoor(index);

    public void Draw(Legacy legacy, int selection, string? openRoomId, int page)
    {
        _ui.Scrim(UiTheme.Scrim, UiTheme.NoBorder);
        if (openRoomId is not null && FortRoster.Find(openRoomId) is { } room)
        {
            DrawRoom(room, legacy, page);
            return;
        }

        var service = legacy.Service;
        var panel = UiLayout.FortPanel;
        _ui.Panel(panel, UiTheme.Panel, UiTheme.Bronze);
        _ui.Text("THE FORT", new Vector2(panel.X + 32, panel.Y + 24), 28, UiTheme.Heading);
        _ui.TextRight(Ranks.LabelOf(service.Rank), panel.Right - 32, panel.Y + 32, 16, UiTheme.Accent);
        _ui.Text(
            $"{legacy.CurrentName}  /  {service.DescentsSurvived} descents  /  "
                + $"{service.StonesBanked} stones banked",
            new Vector2(panel.X + 32, panel.Y + 60), 15, UiTheme.Muted);

        for (var index = 0; index < FortRoster.All.Count; index++)
        {
            var entry = FortRoster.All[index];
            var row = DoorRow(index);
            var isOpen = entry.IsOpen(service.Rank);
            var (fill, border) = UiTheme.Row(index == selection);
            _ui.Row(row, fill, border);
            _ui.Text(entry.DisplayName, new Vector2(row.X + 16, row.Y + 9), 16,
                isOpen ? UiTheme.Body : UiTheme.Disabled);
            var unheard = 0;
            foreach (var fragment in entry.AvailableTo(service.Rank, legacy.DeepestEver))
                if (!legacy.HasHeard(fragment.Id)) unheard++;
            var label = !isOpen ? $"Requires {Ranks.LabelOf(entry.RequiredRank)}"
                : unheard > 0 ? $"{entry.Occupant}  /  {unheard} unheard" : entry.Occupant;
            _ui.TextRight(label, row.Right - 16, row.Y + 10, 15,
                !isOpen ? UiTheme.Muted : unheard > 0 ? UiTheme.Accent : UiTheme.Body);
        }

        var next = Ranks.Next(service.Rank);
        _ui.TextCentred(next is null ? "Every door is open to you."
            : $"Next: {next.Title} / {next.Descents} descents / {next.Stones} stones banked",
            panel.Center.X, 606, 15, UiTheme.Muted);
        _ui.TextCentred("Arrows choose   /   Enter open   /   Esc leave", panel.Center.X, 636, 14, UiTheme.Hint);
    }

    private void DrawRoom(FortRoom room, Legacy legacy, int page)
    {
        var fragments = room.AvailableTo(legacy.Service.Rank, legacy.DeepestEver);
        page = Math.Clamp(page, 0, fragments.Count);
        var panel = UiLayout.ConversationPanel;
        var portrait = UiLayout.ConversationPortrait;
        var text = UiLayout.ConversationText;

        _ui.Fill(panel, UiTheme.Panel);
        _ui.Text("THE FORT", new Vector2(text.X, panel.Y + 24), 14, UiTheme.Accent);
        _ui.TextRight(Ranks.LabelOf(legacy.Service.Rank), panel.Right - 44, panel.Y + 24, 14, UiTheme.Muted);

        DialoguePortraits.Draw(_ui, _device, room.Id, portrait);
        _ui.PortraitEdge(portrait, UiTheme.Panel);
        _ui.TextCentred(room.DisplayName, portrait.Center.X, 592, 22, UiTheme.Heading);
        _ui.TextCentred("BHAGIRATHA  /  " + legacy.CurrentName, portrait.Center.X, 628, 13, UiTheme.Muted);

        _ui.Text(room.Occupant, new Vector2(text.X, 140), 38, UiTheme.Heading);
        _ui.Text(room.Office.ToUpperInvariant(), new Vector2(text.X + 2, 190), 14, UiTheme.Accent);
        _ui.Fill(new Rectangle(text.X, 220, text.Width, 1), UiTheme.Rule);
        _ui.Text(page == 0 ? "" : $"{page} / {fragments.Count}",
            new Vector2(text.X, text.Y + 12), 13, UiTheme.Muted);

        // Centred in the space between the rule and the scene line, rather than pinned under
        // the rule.
        //
        // A passage is one line at rank one and nine lines deep in the fort, and the block it
        // sits in is sized for the nine. Pinned to the top, a greeting left a third of the
        // panel empty below it and the screen read as unfinished; centred, a short line sits
        // in the portrait's eyeline and a long one still starts where it always did, because
        // the offset goes to zero as the text fills the space.
        var body = page == 0 ? room.Greeting : fragments[page - 1].Text;
        const float bodyTop = 278f;
        const float bodyBottom = 500f;
        var bodyHeight = _ui.MeasureWrapped(body, text.Width - 8, 19, maxLines: 9);
        var bodyY = bodyTop + Math.Max(0f, (bodyBottom - bodyTop - bodyHeight) * 0.5f);

        _ui.TextWrapped(body, new Vector2(text.X, bodyY), text.Width - 8, 19,
            UiTheme.Body, maxLines: 9);
        _ui.TextWrapped(room.Description, new Vector2(text.X, 514), text.Width, 14, UiTheme.Muted, maxLines: 2);

        DrawButton(UiLayout.ConversationPrevious, "Left  /  Previous", page > 0, false);
        DrawButton(UiLayout.ConversationNext, page < fragments.Count ? "Enter  /  Continue" : "Enter  /  Leave", true, true);
        DrawButton(UiLayout.ConversationLeave, "Esc  /  Leave", true, false);
    }

    private void DrawButton(Rectangle bounds, string label, bool enabled, bool selected)
    {
        var (fill, border) = UiTheme.Row(selected);
        _ui.Row(bounds, fill, enabled ? border : UiTheme.BorderDim);
        _ui.TextCentred(label, bounds.Center.X, bounds.Y + 11, 15,
            enabled ? UiTheme.Body : UiTheme.Disabled);
    }
}
