using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RatnaBay.Client.Render;

/// <summary>Close-up art is independent of the small billboards used to populate the world.</summary>
internal static class DialoguePortraits
{
    private static Texture2D? _atlas;
    private static readonly string[] Keys =
    {
        "fort.gate", "fort.hall", "fort.assay", "fort.forge",
        "fort.physician", "fort.registrar", "fort.shrine", "fort.barracks",
        "fort.clerk", "fort.governor", "uttara", "trader"
    };

    public static void Draw(UiCanvas ui, GraphicsDevice device, string key, Rectangle bounds)
    {
        if (_atlas is null)
        {
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory,
                "Content", "Art", "Portraits", "cast.png"));
            _atlas = Texture2D.FromStream(device, stream);
        }

        var index = Array.IndexOf(Keys, key);
        if (index < 0) index = 11;
        // Derive each edge independently: image generators can return a size not divisible by four.
        var x = index % 4 * _atlas.Width / 4;
        var y = index / 4 * _atlas.Height / 3;
        var right = (index % 4 + 1) * _atlas.Width / 4;
        var bottom = (index / 4 + 1) * _atlas.Height / 3;
        ui.Sprite(_atlas, bounds, new Rectangle(x, y, right - x, bottom - y), Color.White);
    }

    public static void Clear()
    {
        _atlas?.Dispose();
        _atlas = null;
    }
}
