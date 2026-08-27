using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// The fort: ten rooms, and whoever will talk to you in them.
///
/// Drawn as a list of doors rather than as a place to walk through, and that is a deliberate
/// staging decision rather than a shortcut. The iteration's risk is **content authoring
/// throughput** — the number that decides whether this game is finishable — and that number is
/// about writing rooms and occupants, not about modelling corridors. Building the geometry
/// first would have spent the expensive weeks before learning anything about the cheap ones.
///
/// The rooms, their occupants and their fragments are all data, so the walk-through version
/// when it comes is a different renderer over the same content.
/// </summary>
internal sealed class FortRenderer
{
    private readonly UiCanvas _ui;

    public FortRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>Where each door sits, so drawing and hit-testing cannot drift apart.</summary>
    public static Rectangle DoorRow(int index) => new(280, 176 + index * 42, 720, 38);

    public void Draw(Legacy legacy, int selection, string? openRoomId)
    {
        var service = legacy.Service;

        _ui.Scrim();
        _ui.Panel(new Rectangle(240, 96, 800, 560), UiTheme.PanelRaised, UiTheme.Bronze);

        _ui.TextCentred("THE FORT", 640f, 118f, 24, UiTheme.Heading);
        _ui.TextCentred(
            $"{legacy.CurrentName}, {Ranks.LabelOf(service.Rank)}"
            + $"  ·  {service.DescentsSurvived} descents  ·  {service.StonesBanked} stones banked",
            640f, 150f, 14, UiTheme.Accent);

        if (openRoomId is not null && FortRoster.Find(openRoomId) is { } open)
        {
            DrawRoom(open, legacy);
            return;
        }

        var rank = service.Rank;

        for (var index = 0; index < FortRoster.All.Count; index++)
        {
            var room = FortRoster.All[index];
            var row = DoorRow(index);
            var isOpen = room.IsOpen(rank);
            var selected = index == selection;

            _ui.Row(row, selected && isOpen);

            var ink = !isOpen ? new Color(104, 96, 92)
                : selected ? Color.White
                : UiTheme.RowIdleText;

            _ui.Text(room.DisplayName, new Vector2(row.X + 16, row.Y + 9), 16, ink);

            if (isOpen)
            {
                // How much of this room's story is still unheard. A door with something behind
                // it should say so from the corridor, or a player has to open all ten after
                // every run to find out which one changed.
                var available = room.AvailableTo(rank, legacy.DeepestEver);
                var unheard = 0;
                foreach (var fragment in available)
                    if (!legacy.HasHeard(fragment.Id)) unheard++;

                _ui.TextRight(
                    unheard > 0 ? $"{room.Occupant}  ·  {unheard} new" : room.Occupant,
                    row.Right - 16, row.Y + 10, 14,
                    unheard > 0 ? UiTheme.Accent : UiTheme.Muted);
            }
            else
            {
                _ui.TextRight($"shut — {Ranks.LabelOf(room.RequiredRank)}",
                    row.Right - 16, row.Y + 10, 13, UiTheme.Warning);
            }
        }

        var next = Ranks.Next(rank);
        _ui.TextCentred(next is null
                ? "Every door in the fort is open to you."
                : $"{next.Title} at {next.Descents} descents and {next.Stones} stones banked.",
            640f, 610f, 13, UiTheme.Muted);

        _ui.TextCentred("Arrows choose      Enter open      Esc leave",
            640f, 632f, 13, UiTheme.HintDim);
    }

    private void DrawRoom(FortRoom room, Legacy legacy)
    {
        _ui.TextCentred(room.DisplayName.ToUpperInvariant(), 640f, 190f, 22, Color.White);
        _ui.TextCentred($"{room.Occupant}  ·  {room.Office}", 640f, 222f, 15, UiTheme.Accent);
        _ui.TextCentred(room.Description, 640f, 248f, 13, UiTheme.Muted);

        var y = 292f;
        _ui.TextFit($"“{room.Greeting}”", new Vector2(300, y), 680f, 16,
            new Color(226, 220, 208));
        y += 40f;

        var fragments = room.AvailableTo(legacy.Service.Rank, legacy.DeepestEver);

        if (fragments.Count == 0)
        {
            _ui.TextCentred("They have nothing more to say to you yet.", 640f, y, 14,
                UiTheme.Muted);
        }

        foreach (var fragment in fragments)
        {
            // Marked as heard on being shown, so a fragment is new exactly once. Reading it is
            // the event, not dismissing it — a player who walks away mid-sentence has still
            // been told.
            var isNew = legacy.Hear(fragment.Id);

            foreach (var line in Wrap(fragment.Text, 92))
            {
                _ui.TextFit(line, new Vector2(300, y), 680f, 14,
                    isNew ? new Color(232, 226, 212) : new Color(168, 172, 174));
                y += 22f;
            }

            y += 12f;
        }

        _ui.TextCentred("Esc  step back into the corridor", 640f, 632f, 13, UiTheme.HintDim);
    }

    /// <summary>Break a line at word boundaries so a long fragment does not run off the panel.</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > width)
            {
                yield return line;
                line = word;
                continue;
            }

            line = line.Length == 0 ? word : $"{line} {word}";
        }

        if (line.Length > 0) yield return line;
    }
}
