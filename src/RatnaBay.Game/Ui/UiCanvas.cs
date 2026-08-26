using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace RatnaBay.Client;

/// <summary>
/// The one drawing surface every screen and HUD renderer uses.
///
/// Game1 still owns SpriteBatch, fonts and the white pixel. Renderers must not sample devices,
/// open SpriteBatch themselves, or reach back into Game1: they receive this canvas and a
/// snapshot, then paint. That is the boundary that keeps an AI change to a panel from
/// touching the simulation loop.
/// </summary>
internal sealed class UiCanvas
{
    private readonly Action<Rectangle, Color, Color> _panel;
    private readonly Action<string, Vector2, float, Color> _text;
    private readonly Action<string, Vector2, float, float, Color> _textFit;
    private readonly Action<string, float, float, float, Color> _textCentred;
    private readonly Action<string, float, float, float, Color> _textRight;
    private readonly Func<string, Vector2, float, float, Color, int, float> _textWrapped;
    private readonly Action<Rectangle, Color> _fill;
    private readonly Action<Rectangle, Color> _border;
    private readonly Action<Texture2D, Rectangle, Color> _sprite;

    public UiCanvas(
        Action<Rectangle, Color, Color> panel,
        Action<string, Vector2, float, Color> text,
        Action<string, Vector2, float, float, Color> textFit,
        Action<string, float, float, float, Color> textCentred,
        Action<string, float, float, float, Color> textRight,
        Func<string, Vector2, float, float, Color, int, float> textWrapped,
        Action<Rectangle, Color> fill,
        Action<Rectangle, Color> border,
        Action<Texture2D, Rectangle, Color> sprite)
    {
        _panel = panel;
        _text = text;
        _textFit = textFit;
        _textCentred = textCentred;
        _textRight = textRight;
        _textWrapped = textWrapped;
        _fill = fill;
        _border = border;
        _sprite = sprite;
    }

    public void Panel(Rectangle bounds, Color fill, Color border) => _panel(bounds, fill, border);

    public void Text(string value, Vector2 position, float scale, Color color) =>
        _text(value, position, scale, color);

    public void TextFit(string value, Vector2 position, float maxWidth, float scale, Color color) =>
        _textFit(value, position, maxWidth, scale, color);

    public void TextCentred(string value, float centreX, float y, float scale, Color color) =>
        _textCentred(value, centreX, y, scale, color);

    public void TextRight(string value, float right, float y, float scale, Color color) =>
        _textRight(value, right, y, scale, color);

    public float TextWrapped(string value, Vector2 position, float maxWidth, float scale,
        Color color, int maxLines = 6) =>
        _textWrapped(value, position, maxWidth, scale, color, maxLines);

    public void Fill(Rectangle bounds, Color color) => _fill(bounds, color);

    public void Border(Rectangle bounds, Color color) => _border(bounds, color);

    public void Sprite(Texture2D texture, Rectangle destination, Color color) =>
        _sprite(texture, destination, color);
}
