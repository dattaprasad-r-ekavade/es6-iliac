using Microsoft.Xna.Framework;
using RatnaBay.Domain;

namespace RatnaBay.Client;

internal sealed class ShopRenderer
{
    private readonly UiCanvas _ui;

    public ShopRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(Shop shop, int gold, int selection)
    {
        // Tall enough for four rows of three. The stall carries ten things and the last row
        // used to run off the bottom of the panel and through the help text.
        var panel = new Rectangle(250, 100, 780, 552);
        _ui.Panel(panel, new Color(5, 11, 18, 248), new Color(205, 157, 98));
        _ui.Text("SHOP", new Vector2(panel.X + 30, panel.Y + 26), 13,
            new Color(214, 183, 108));
        _ui.TextFit(shop.Definition.DisplayName, new Vector2(panel.X + 30, panel.Y + 54), 520f, 27,
            Color.White);
        _ui.TextRight($"{gold} gold", panel.Right - 30, panel.Y + 62, 17,
            new Color(228, 197, 122));

        var items = shop.Definition.Items;
        if (items.Count == 0)
            _ui.Text("No stock.", new Vector2(panel.X + 30, panel.Y + 126), 17, new Color(174, 188, 186));
        else
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var selected = index == selection;
                var sold = shop.IsSoldOut(item.Id);
                var affordable = !sold && gold >= item.Price;
                var tile = UiLayout.ShopItem(index);

                _ui.Panel(tile,
                    selected ? new Color(74, 67, 43, 245) : new Color(17, 27, 35, 220),
                    selected ? new Color(224, 181, 88) : new Color(54, 82, 91));

                var ink = sold ? new Color(112, 122, 122)
                    : !affordable ? new Color(146, 130, 124)
                    : selected ? Color.White
                    : new Color(203, 216, 214);

                _ui.TextFit(item.Name, new Vector2(tile.X + 12, tile.Y + 10), tile.Width - 24, 16, ink);
                _ui.TextFit(ItemUse.Describe(item.Id, item.Kind), new Vector2(tile.X + 12, tile.Y + 34),
                    tile.Width - 24, 12, new Color(140, 156, 164));

                _ui.Text(sold ? "SOLD OUT" : $"{item.Price} gold",
                    new Vector2(tile.X + 12, tile.Bottom - 24), 15,
                    sold ? new Color(142, 157, 157)
                        : affordable ? new Color(228, 197, 122)
                        : new Color(196, 118, 96));

                if (item.Count > 1)
                    _ui.TextRight($"x{item.Count}", tile.Right - 12, tile.Bottom - 24, 14,
                        new Color(150, 162, 170));
            }
        }

        _ui.Text("Click to buy      Arrows move      Enter buy      B / Esc close",
            new Vector2(panel.X + 30, panel.Bottom - 34), 13, new Color(163, 191, 194));
    }
}
