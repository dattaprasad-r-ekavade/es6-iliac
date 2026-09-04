using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client.Render;

/// <summary>
/// Write every sprite the game forges to disk, as the first draft of a painted one.
///
/// The point is not to look at them. It is that a painted sprite has to start from the shape,
/// scale and palette the game already uses, or it lands in the world the wrong size and lit by
/// a different lamp than everything beside it. Dumping the generated version gives whoever
/// paints it the exact canvas: right dimensions, right silhouette, right key light, already
/// keyed to the name the game will ask for.
///
/// The workflow this exists for:
///
///     build\RatnaBay.exe --sprites art\
///     (open art\sword.png in Pixelorama, paint, save)
///     copy it to src\RatnaBay.Game\Content\Art\Sprites\sword.png
///
/// and the game stops forging that one. See <see cref="SpriteOverrides"/>. Nothing has to be
/// painted for the game to run, and painting one does not commit anybody to painting the rest.
/// </summary>
internal static class SpriteSheetWriter
{
    /// <summary>
    /// Every key the game asks for, and what it draws.
    ///
    /// Deliberately one list rather than a scan of the call sites: a key that is not here is a
    /// sprite nobody can find to paint, and the two places that matter -- this dump and the
    /// folder the game loads overrides from -- have to agree on the vocabulary.
    ///
    /// Not everything is here. Dialogue actors and watchers are keyed off ids that come from
    /// content (dialogue.mara, watcher.gate.02), so the set depends on which manifest is
    /// loaded and this dump does not enumerate them. They can still be overridden -- name the
    /// file after the key -- and the key is whatever the manifest calls that actor.
    /// </summary>
    public static int Write(GraphicsDevice device, string directory)
    {
        Directory.CreateDirectory(directory);
        var written = 0;

        foreach (var (key, texture) in Items(device).Concat(Figures(device)))
        {
            var path = Path.Combine(directory, $"{key}.png");
            using var stream = File.Create(path);
            texture.SaveAsPng(stream, texture.Width, texture.Height);
            written++;
        }

        Console.WriteLine($"Wrote {written} sprites to {Path.GetFullPath(directory)}.");
        Console.WriteLine("Paint one, then save it into Content/Art/Sprites under the same name.");
        return written;
    }

    private static IEnumerable<(string Key, Texture2D Texture)> Items(GraphicsDevice device)
    {
        yield return ("pickaxe", ItemSprites.Pickaxe(device));
        yield return ("sword", ItemSprites.Sword(device));
        yield return ("jiva", ItemSprites.JivaCrystal(device));
        yield return ("goldbars", ItemSprites.GoldBars(device));
        yield return ("risen.chhaya", ItemSprites.ChhayaSprite(device));
        yield return ("risen.vetala", ItemSprites.VetalaSprite(device));
        yield return ("risen.pishacha", ItemSprites.PishachaSprite(device));

        // Named individually rather than through ItemSprites.Risen, which keys off an
        // archetype id and answers null for the three boss ids -- so a loop over them dumped
        // nothing and said nothing about it.
        yield return ("boss.khanda", ItemSprites.BreakerSprite(device));
        yield return ("boss.netra", ItemSprites.WardenSprite(device));
        yield return ("boss.chhala", ItemSprites.HarrierSprite(device));
    }

    private static IEnumerable<(string Key, Texture2D Texture)> Figures(GraphicsDevice device)
    {
        yield return ("bandit", CharacterSprites.Get(device, "bandit", CharacterPalette.Bandit));
        yield return ("bandit_archer",
            CharacterSprites.Get(device, "bandit_archer", CharacterPalette.Guard));

        // The fort's ten, by room, because that is how the presenter asks for them.
        foreach (var room in FortRoster.All)
            yield return (room.Id, CharacterSprites.Get(device, room.Id, PaletteFor(room.Id)));
    }

    /// <summary>
    /// The same mapping <c>FigurePresenter</c> uses, and it has to stay the same one.
    ///
    /// A sprite dumped under a different palette than the game draws it with is worse than no
    /// dump: somebody paints over a citizen and the game puts a guard in the room.
    /// </summary>
    private static CharacterPalette PaletteFor(string? palette) => palette?.ToLowerInvariant() switch
    {
        "guard" => CharacterPalette.Guard,
        "merchant" => CharacterPalette.Merchant,
        "bandit" => CharacterPalette.Bandit,
        "wolf" => CharacterPalette.Wolf,
        _ => CharacterPalette.Citizen
    };
}
