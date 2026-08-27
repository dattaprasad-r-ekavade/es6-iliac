using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RatnaBay.Domain;

namespace RatnaBay.Client;

/// <summary>
/// Descent-loop panels: the shut door, the camp trader, the shaft, the ledger, and the
/// walk-out / did-not summary.
///
/// Input stays in Game1. Hit-test rectangles live in <see cref="UiLayout"/> so a click and
/// a painted row cannot drift apart.
/// </summary>
internal sealed class DescentRenderer
{
    private readonly UiCanvas _ui;

    public DescentRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>
    /// The whole game, in one panel.
    ///
    /// Both numbers are shown together on purpose: what is being staked, and what the next
    /// room pays. The escalating ratio between them is the pressure the loop runs on, and a
    /// player who has to work it out in their head is not feeling it.
    /// </summary>
    public void DrawCampDecision(RunState run)
    {
        var panel = new Rectangle(360, 358, 560, 260);
        _ui.Panel(panel, new Color(6, 12, 19, 240), UiTheme.Bronze);

        _ui.TextCentred("A CLEARED ROOM, AND A SHUT DOOR", panel.Center.X, panel.Y + 18f, 13,
            UiTheme.Bronze);

        _ui.TextCentred($"{run.Pending}", panel.X + 148f, panel.Y + 52f, 44, UiTheme.Accent);

        _ui.TextCentred($"stones held  ·  {run.Pending * SoulCrystals.LesserBasePrice} gold",
            panel.X + 148f, panel.Y + 104f, 13, UiTheme.Muted);

        _ui.TextCentred(run.IsExhausted ? "—" : $"+{run.NextRoomPays}",
            panel.Right - 148f, panel.Y + 52f, 44, new Color(214, 186, 120));
        _ui.TextCentred(run.IsExhausted ? "the mine is spent" : "the next room pays",
            panel.Right - 148f, panel.Y + 104f, 13, UiTheme.Muted);

        _ui.TextCentred(run.IsExhausted
                ? $"{run.RoomsCleared} rooms cleared. There is nothing deeper."
                : $"{run.RoomsCleared} rooms cleared  ·  staking {run.RiskRatio:0.0} : 1",
            panel.Center.X, panel.Y + 128f, 15, new Color(206, 212, 218));

        if (!run.IsExhausted)
            _ui.TextCentred("Fall in there and you carry out nothing.",
                panel.Center.X, panel.Y + 150f, 13, UiTheme.Warning);

        if (run.CanCallTrader)
            _ui.TextCentred($"T   whistle for a trader — {run.TraderCallCost} stones",
                panel.Center.X, panel.Y + 168f, 14, new Color(196, 176, 210));
        else if (run.TradersCalled > 0 || run.Pending > 0)
            _ui.TextCentred($"a trader would want {run.TraderCallCost} stones",
                panel.Center.X, panel.Y + 168f, 13, new Color(120, 116, 128));

        var camp = new Rectangle(panel.X + 24, panel.Bottom - 62, 248, 40);
        _ui.Panel(camp, new Color(17, 34, 28, 235), new Color(120, 178, 132));
        _ui.TextCentred($"C   Camp — bank {run.Pending}", camp.Center.X, camp.Y + 12f, 15,
            new Color(214, 240, 220));

        var press = new Rectangle(panel.Right - 272, panel.Bottom - 62, 248, 40);
        if (run.CanPressOn)
        {
            _ui.Panel(press, new Color(40, 24, 20, 235), UiTheme.Warning);
            _ui.TextCentred("E   Open it", press.Center.X, press.Y + 12f, 15, new Color(244, 214, 200));
            return;
        }

        _ui.Panel(press, new Color(18, 22, 26, 200), new Color(70, 78, 86));
        _ui.TextCentred("nothing deeper", press.Center.X, press.Y + 12f, 15, new Color(110, 120, 128));
    }

    /// <summary>A quiet running total, so the pot is never a surprise at the door.</summary>
    public void DrawRunLedger(RunState run)
    {
        var panel = new Rectangle(1016, 84, 240, 62);
        _ui.Panel(panel, new Color(5, 11, 18, 214), UiTheme.BorderDim);

        _ui.Text("AT RISK", new Vector2(panel.X + 14, panel.Y + 10), 12, UiTheme.Accent);
        _ui.Text($"{run.Pending}", new Vector2(panel.Right - 44, panel.Y + 8), 18, Color.White);
        _ui.Text($"room {run.RoomsCleared}  ·  {run.Pending * SoulCrystals.LesserBasePrice} gold",
            new Vector2(panel.X + 14, panel.Y + 34), 12, UiTheme.Muted);
    }

    /// <summary>
    /// The trader's pack, priced in the pot.
    ///
    /// The pot is on the panel at all times, because every price here is a stone not carried
    /// out, and that is the only thing that makes any of it a decision.
    /// </summary>
    public void DrawCampTrader(Inventory inventory, RunState run, int selection)
    {
        var (lootItems, lootStones) = CampTrader.ValueOfLoot(inventory);
        var panel = new Rectangle(320, 168, 640, 384);
        var rows = 1 + CampTrader.Stock.Count;

        _ui.Scrim();
        _ui.Panel(panel, UiTheme.PanelRaised, UiTheme.Bronze);

        _ui.TextCentred("SOMEBODY CAME DOWN", panel.Center.X, panel.Y + 24f, 24,
            UiTheme.Heading);
        _ui.TextCentred($"{run.Pending} stones in the pot  ·  the next whistle costs {run.TraderCallCost}",
            panel.Center.X, panel.Y + 58f, 14, UiTheme.Accent);

        for (var index = 0; index < rows; index++)
        {
            var row = UiLayout.CampRow(index);
            var selected = index == selection;

            var name = index == 0
                ? lootItems > 0 ? $"Sell {lootItems} pieces of loot" : "Nothing to sell"
                : CampTrader.Stock[index - 1].Name;

            var price = index == 0
                ? lootItems > 0 ? $"+{lootStones} stones" : "—"
                : $"{CampTrader.Stock[index - 1].Stones} stones";

            var affordable = index == 0
                ? lootItems > 0
                : run.Pending >= CampTrader.Stock[index - 1].Stones;

            _ui.Row(row, selected);

            var ink = !affordable ? new Color(122, 112, 108)
                : selected ? Color.White
                : UiTheme.RowIdleText;

            _ui.Text(name, new Vector2(row.X + 18, row.Y + 12), 16, ink);
            _ui.TextRight(price, row.Right - 18, row.Y + 12, 15,
                index == 0
                    ? UiTheme.Accent
                    : affordable ? new Color(214, 186, 120) : UiTheme.Warning);
        }

        _ui.TextCentred("Nothing here outlives the descent. Dying still costs you all of it.",
            panel.Center.X, panel.Bottom - 52f, 13, UiTheme.Warning);
        _ui.TextCentred("Click or arrows choose      Enter trade      Esc back to the door",
            panel.Center.X, panel.Bottom - 28f, 13, UiTheme.HintDim);
    }

    /// <summary>
    /// The price of every depth, and what each is worth, at the moment of committing.
    /// </summary>
    public void DrawDepthChoice(int stones, int selection)
    {
        var panel = new Rectangle(320, 148, 640, 452);

        _ui.Scrim();
        _ui.Panel(panel, UiTheme.PanelRaised, UiTheme.Bronze);

        _ui.TextCentred("WHICH MINE", panel.Center.X, panel.Y + 24f, 24, UiTheme.Heading);
        _ui.TextCentred($"{stones} jiva stones in hand", panel.Center.X, panel.Y + 58f, 14,
            UiTheme.Accent);

        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        {
            var cost = MineEntry.CostOf(tier);
            var affordable = stones >= cost;
            var selected = tier == selection;
            var row = UiLayout.DepthRow(tier);

            _ui.Row(row, selected);

            var ink = !affordable ? new Color(112, 100, 96)
                : selected ? Color.White
                : UiTheme.RowIdleText;

            _ui.Text($"Tier {tier}", new Vector2(row.X + 18, row.Y + 6), 18, ink);
            _ui.TextRight(cost == 0 ? "free" : $"{cost} stones", row.Right - 18, row.Y + 8, 16,
                affordable ? new Color(214, 186, 120) : UiTheme.Warning);
            _ui.TextFit(MineEntry.DescriptionOf(tier), new Vector2(row.X + 18, row.Y + 28),
                row.Width - 150, 12, UiTheme.Muted);
        }

        var breakEven = MineEntry.RoomsToBreakEven(selection);
        _ui.TextCentred(breakEven == 0
                ? "Pays one stone a room. Nothing to make back."
                : $"Pays {selection} a room, rising. {breakEven} rooms before the door pays for itself.",
            panel.Center.X, panel.Bottom - 78f, 14, new Color(206, 212, 218));

        _ui.TextCentred("A harder mine, not a longer one. How far you go is decided at each door.",
            panel.Center.X, panel.Bottom - 52f, 13, UiTheme.Accent);

        _ui.TextCentred("Click or arrows choose      Enter descend      Esc step back",
            panel.Center.X, panel.Bottom - 26f, 13, UiTheme.HintDim);
    }

    public void DrawRunSummary(RunResult summary, PlayerCharacter? player,
        SuccessionResult? succession, bool buttonHovered,
        IReadOnlyList<string>? earnedAmulets = null)
    {
        _ui.Panel(UiLayout.FullScreen, new Color(3, 6, 10, 226), UiTheme.NoBorder);

        // Taller when the run earned something permanent. The ratchet is the reason to come
        // back, and a screen that mentions it in the margin is a screen that hides it.
        var earned = earnedAmulets ?? Array.Empty<string>();
        var panel = new Rectangle(360, 176, 560, 360 + earned.Count * 46 + 28);
        var accent = summary.Survived ? new Color(120, 178, 132) : new Color(196, 96, 88);
        _ui.Panel(panel, UiTheme.PanelRaised, accent);

        _ui.TextCentred(summary.Survived ? "YOU WALKED OUT" : "YOU DID NOT",
            panel.Center.X, panel.Y + 30f, 26, accent);

        _ui.TextCentred($"{summary.RoomsCleared} rooms cleared at tier {summary.Tier}",
            panel.Center.X, panel.Y + 78f, 15, new Color(206, 212, 218));

        if (summary.Survived)
        {
            _ui.TextCentred($"+{summary.StonesCarriedOut}", panel.Center.X, panel.Y + 124f, 52,
                UiTheme.Accent);
            _ui.TextCentred(
                $"jiva stones banked  ·  {summary.StonesCarriedOut * SoulCrystals.LesserBasePrice} gold",
                panel.Center.X, panel.Y + 186f, 14, UiTheme.Muted);
        }
        else
        {
            _ui.TextCentred($"−{summary.StonesLost}", panel.Center.X, panel.Y + 124f, 52,
                new Color(196, 96, 88));
            _ui.TextCentred(summary.StonesLost > 0
                    ? $"left where you fell  ·  {summary.StonesLost * SoulCrystals.LesserBasePrice} gold"
                    : "you had nothing to lose yet",
                panel.Center.X, panel.Y + 186f, 14, UiTheme.Muted);
        }

        if (!summary.Survived && player is not null)
        {
            var legacy = player.Legacy;
            var successor = legacy.CurrentName;

            _ui.TextCentred($"{successor} takes the lamp  ·  Deepankar the {Ordinal(legacy.Generation + 1)}",
                panel.Center.X, panel.Bottom - 88f, 15, new Color(214, 200, 170));

            if (legacy.Fallen is { } cache)
                _ui.TextCentred(
                    $"{cache.Name} lies in room {cache.RoomIndex} with {cache.Stones} stones. Go and fetch them.",
                    panel.Center.X, panel.Bottom - 64f, 13, UiTheme.Accent);
            else if (succession is { } cost && cost.ItemsLost > 0)
                _ui.TextCentred($"{cost.ItemsLost} items went into the ground.",
                    panel.Center.X, panel.Bottom - 64f, 13, UiTheme.Muted);
        }

        DrawRatchet(panel, player, earned);

        var button = UiLayout.SummaryButton;
        _ui.Row(button, buttonHovered);
        _ui.TextCentred("Back to the surface", button.Center.X, button.Y + 12f, 16,
            UiTheme.RowText(buttonHovered));
    }

    /// <summary>
    /// What the order gained, whether or not the person did.
    ///
    /// The one part of this screen that has to read the same after a death as after a win.
    /// A losing run that shows only what it cost is a losing run that argues against playing
    /// again, and this iteration exists precisely to make the opposite true.
    /// </summary>
    private void DrawRatchet(Rectangle panel, PlayerCharacter? player,
        IReadOnlyList<string> earned)
    {
        if (player is null) return;

        var y = panel.Bottom - 116f - earned.Count * 46f;

        foreach (var id in earned)
        {
            var amulet = AmuletCatalog.Find(id);
            if (amulet is null) continue;

            _ui.TextCentred($"{amulet.DisplayName}  ·  kept for good",
                panel.Center.X, y, 17, UiTheme.Accent);
            _ui.TextCentred(amulet.Description, panel.Center.X, y + 22f, 13, UiTheme.Muted);
            y += 46f;
        }

        var points = player.Skills.UnspentPoints;
        if (points > 0)
        {
            _ui.TextCentred(
                points == 1 ? "1 skill point to spend" : $"{points} skill points to spend",
                panel.Center.X, y, 15, new Color(214, 200, 170));
            y += 26f;
        }

        // What to aim at next. A ratchet the player cannot see the next tooth of is a ratchet
        // they have to take on faith.
        if (AmuletCatalog.NextAfter(player.Legacy.DeepestEver) is not { } next) return;

        _ui.TextCentred(
            $"Deepest ever: room {player.Legacy.DeepestEver}."
            + $"  Reach room {next.Depth} for {next.Amulet.DisplayName}.",
            panel.Center.X, y, 13, UiTheme.Muted);
    }

    private static string Ordinal(int value) => (value % 100) switch
    {
        11 or 12 or 13 => $"{value}th",
        _ => (value % 10) switch
        {
            1 => $"{value}st",
            2 => $"{value}nd",
            3 => $"{value}rd",
            _ => $"{value}th"
        }
    };
}
