using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace RatnaBay.Engine.Render;

/// <summary>
/// A painted PNG beats a forged sprite, by key.
///
/// The sprites in this engine are generated in code, which is why the game has no artist and
/// still looks like one thing: every icon and every figure is lit by the same lamp through the
/// same ramp. That is worth keeping, and it is also a ceiling — some sprites want a hand.
///
/// So: drop <c>sword.png</c> into the override folder and the forge stops being asked for
/// <c>sword</c>. Nothing else changes. There is no second art system, no loader to write per
/// sprite, and no decision to make up front about which sprites are painted — a file appearing
/// or disappearing is the whole of it, one sprite at a time.
///
/// **The forge stays the source of the first draft.** The game can write every sprite it makes
/// to disk (see the client's --sprites), which is what you open, paint over, and save back
/// under the same name. That is why this is an override and not a replacement: the generated
/// version is the fallback, the starting point, and the thing that still fills every key
/// nobody has got to yet.
/// </summary>
public static class SpriteOverrides
{
    private static readonly Dictionary<string, Texture2D> Painted =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keys that were loaded from disk, for reporting what is in force.</summary>
    public static IReadOnlyCollection<string> Keys => Painted.Keys;

    /// <summary>
    /// Load every PNG in a folder, keyed by file name without its extension.
    ///
    /// The key is the file name because the key is what the calling code already asks for:
    /// <c>bandit</c>, <c>risen.vetala</c>, <c>fort.governor</c>. A mapping file would be one
    /// more thing to keep in step with the two lists it sits between.
    ///
    /// A missing folder is not a fault — most installs will not have one. A file that will not
    /// load is: it is a file somebody put there on purpose, and silently forging the default
    /// instead would look exactly like the paint not having been saved.
    /// </summary>
    public static void LoadFrom(GraphicsDevice device, string directory, ICollection<string> faults)
    {
        Clear();
        if (!Directory.Exists(directory)) return;

        foreach (var path in Directory.EnumerateFiles(directory, "*.png"))
        {
            try
            {
                using var stream = File.OpenRead(path);
                Painted[Path.GetFileNameWithoutExtension(path)] = Texture2D.FromStream(device, stream);
            }
            catch (Exception exception)
            {
                faults.Add($"sprite {Path.GetFileName(path)}: {exception.Message}");
            }
        }
    }

    /// <summary>The painted sprite for a key, if somebody has painted one.</summary>
    public static bool TryGet(string key, out Texture2D texture)
    {
        if (Painted.TryGetValue(key, out var painted))
        {
            texture = painted;
            return true;
        }

        texture = null!;
        return false;
    }

    public static void Clear()
    {
        foreach (var texture in Painted.Values) texture.Dispose();
        Painted.Clear();
    }
}
