using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace RatnaBay.Client;

internal sealed record MenuState(
    IReadOnlyList<string> Items,
    int Selection,
    string Status,
    bool Resuming,
    bool ShowSettings,
    OverlayState Overlay);

internal sealed class MenuRenderer
{
    private readonly UiCanvas _ui;

    public MenuRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(MenuState state, OverlayRenderer overlay)
    {
        _ui.Fill(UiLayout.FullScreen, new Color(3, 7, 12, 178));
        _ui.Panel(new Rectangle(64, 62, 1152, 596), new Color(5, 11, 18, 232), UiTheme.Border);

        _ui.Text("RATNA BAY", new Vector2(98, 96), 38, Color.White);
        // This screen described the story slice long after the game stopped being one. It is
        // the first thing a stranger reads, and it was promising them exploration, trading and
        // sneaking -- one of which is parked and none of which is what they are about to play.
        _ui.Text("AN ENDLESS MINE", new Vector2(101, 153), 13, new Color(161, 211, 218));
        _ui.TextFit("Go down, clear rooms, and decide when to stop", new Vector2(101, 181), 420f, 15,
            new Color(184, 197, 196));

        _ui.Panel(new Rectangle(96, 222, 416, 390), new Color(8, 16, 24, 238), UiTheme.BorderDim);
        _ui.Text("MAIN MENU", new Vector2(124, 246), 14, UiTheme.GoldDim);

        for (var index = 0; index < state.Items.Count; index++)
        {
            var itemBounds = UiLayout.MenuItem(index);
            var selected = index == state.Selection;
            var (fill, border) = UiTheme.Row(selected);
            _ui.Row(itemBounds, fill, border);
            _ui.Text((index + 1).ToString("00"), new Vector2(itemBounds.X + 16, itemBounds.Y + 9), 13,
                selected ? new Color(245, 209, 124) : new Color(112, 148, 155));
            _ui.Text(state.Items[index], new Vector2(itemBounds.X + 62, itemBounds.Y + 7), 18,
                UiTheme.RowText(selected));
        }

        _ui.Panel(new Rectangle(560, 222, 592, 390), new Color(8, 16, 24, 226), UiTheme.BorderDim);

        _ui.Text(state.Resuming ? "BELOW RATNA BAY" : "THE YARD AT RATNA BAY",
            new Vector2(592, 246), 14, UiTheme.Accent);
        _ui.Text(state.Resuming ? "A DESCENT" : "TAKE UP THE LAMP",
            new Vector2(592, 280), 24, Color.White);

        var blurb = state.Resuming
            ? new[]
            {
                "A mine that has never been walked before.",
                "Clear a room and it pays. Clear the next and it pays more.",
                "Camp at a door to bank it, or open the door and risk the lot."
            }
            : new[]
            {
                "Buy your way into a mine. The first one costs nothing.",
                "Every room you clear is worth more than the last one.",
                "Every shut door asks whether that is enough."
            };

        for (var line = 0; line < blurb.Length; line++)
            _ui.TextFit(blurb[line], new Vector2(592, 326 + line * 24), 500f, 15,
                new Color(190, 203, 200));

        _ui.Text("WHAT YOU CAN DO", new Vector2(592, 414), 12, UiTheme.GoldDim);

        var doing = state.Resuming
            ? new[] { "Fight through generated rooms", "Bank your stones, or press on", "Die and lose the lot" }
            : new[] { "Fight through generated rooms", "Bank your stones, or press on", "Die, and send the next one down" };

        for (var line = 0; line < doing.Length; line++)
            _ui.Text(doing[line], new Vector2(592, 442 + line * 26), 14, new Color(190, 215, 208));

        _ui.Text("Click or hover to choose      Up / Down select      Enter confirm      Esc safe",
            new Vector2(98, 610), 14, UiTheme.Hint);
        if (!string.IsNullOrWhiteSpace(state.Status))
            _ui.TextFit(state.Status, new Vector2(592, 542), 520f, 14, UiTheme.Error);
        if (state.ShowSettings) overlay.DrawSettings(state.Overlay);
    }
}
