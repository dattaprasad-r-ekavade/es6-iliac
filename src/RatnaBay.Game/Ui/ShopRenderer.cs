using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

internal sealed class ShopRenderer
{
    private readonly UiCanvas _ui;

    public ShopRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>
    /// How far the grid has scrolled, in rows, to keep the selection on screen.
    ///
    /// The stall used to be sized for its stock — "tall enough for four rows of three", written
    /// when it carried ten things. It carries fifteen now, the fifth row ran off the bottom of
    /// the panel, and the footer was drawn straight through it. A panel that has to grow every
    /// time a line is added to a manifest will be wrong again the next time, so this scrolls
    /// instead: the frame is fixed and the stock moves inside it.
    /// </summary>
    public static int FirstVisibleRow(int selection, int count)
    {
        var rows = (count + UiLayout.ShopColumns - 1) / UiLayout.ShopColumns;
        if (rows <= UiLayout.ShopVisibleRows) return 0;

        var selectedRow = Math.Max(0, selection) / UiLayout.ShopColumns;
        var top = selectedRow - UiLayout.ShopVisibleRows / 2;

        return Math.Clamp(top, 0, rows - UiLayout.ShopVisibleRows);
    }

    /// <summary>Where an item sits on screen, or nothing when it is scrolled out of view.</summary>
    public static Rectangle? TileFor(int index, int selection, int count)
    {
        var first = FirstVisibleRow(selection, count);
        var row = index / UiLayout.ShopColumns - first;

        if (row < 0 || row >= UiLayout.ShopVisibleRows) return null;

        return UiLayout.ShopItem(row * UiLayout.ShopColumns + index % UiLayout.ShopColumns);
    }

    public void Draw(Shop shop, int gold, int selection)
    {
        var panel = UiLayout.ShopPanel;
        _ui.Panel(panel, UiTheme.Panel, UiTheme.Bronze);

        _ui.Text("SHOP", new Vector2(panel.X + 30, panel.Y + 26), 13, UiTheme.GoldDim);
        _ui.TextFit(shop.Definition.DisplayName, new Vector2(panel.X + 30, panel.Y + 54), 520f, 27,
            Color.White);
        _ui.TextRight($"{gold} gold", panel.Right - 30, panel.Y + 62, 17, UiTheme.GoldBright);

        var items = shop.Definition.Items;

        if (items.Count == 0)
        {
            _ui.Text("No stock.", new Vector2(panel.X + 30, UiLayout.ShopGridTop), 17,
                UiTheme.Prompt);
        }
        else
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (TileFor(index, selection, items.Count) is not { } tile) continue;

                var item = items[index];
                var selected = index == selection;
                var sold = shop.IsSoldOut(item.Id);
                var affordable = !sold && gold >= item.Price;

                var (fill, border) = UiTheme.Row(selected);
                _ui.Row(tile, fill, border);

                var ink = sold ? new Color(112, 122, 122)
                    : !affordable ? new Color(146, 130, 124)
                    : selected ? Color.White
                    : UiTheme.Body;

                _ui.TextFit(item.Name, new Vector2(tile.X + 12, tile.Y + 9), tile.Width - 24, 16, ink);
                _ui.TextFit(ItemUse.Describe(item.Id, item.Kind),
                    new Vector2(tile.X + 12, tile.Y + 31), tile.Width - 24, 12, UiTheme.HintDim);

                _ui.Text(sold ? "SOLD OUT" : $"{item.Price} gold",
                    new Vector2(tile.X + 12, tile.Bottom - 22), 15,
                    sold ? UiTheme.Faint
                        : affordable ? UiTheme.GoldBright
                        : UiTheme.Warning);

                if (item.Count > 1)
                    _ui.TextRight($"x{item.Count}", tile.Right - 12, tile.Bottom - 22, 14,
                        UiTheme.Muted);
            }

            // Say so when there is stock above or below, rather than letting it vanish.
            var rows = (items.Count + UiLayout.ShopColumns - 1) / UiLayout.ShopColumns;
            if (rows > UiLayout.ShopVisibleRows)
            {
                var first = FirstVisibleRow(selection, items.Count);
                var shown = $"{first * UiLayout.ShopColumns + 1}"
                    + $"–{Math.Min(items.Count, (first + UiLayout.ShopVisibleRows) * UiLayout.ShopColumns)}"
                    + $" of {items.Count}";

                _ui.TextRight(shown, panel.Right - 30, panel.Bottom - 32, 12, UiTheme.Muted);
            }
        }

        _ui.Text("Click to buy      Arrows move      Enter buy      B / Esc close",
            new Vector2(panel.X + 30, panel.Bottom - 32), 13, UiTheme.Hint);
    }
}
