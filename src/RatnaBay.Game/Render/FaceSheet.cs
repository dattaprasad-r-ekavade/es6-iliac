using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client.Render;

/// <summary>
/// Every face in every mood, as one PNG.
///
/// The whole reason procedural portraits are affordable is that a change to a brow can be
/// judged in one picture instead of ten conversations. One row per occupant, one column
/// per expression, generated at authoring size so what is written to disk is exactly the
/// pixels the forge produced.
///
/// Game-specific: it walks the fort roster. A capture host would not know who lives here.
/// </summary>
internal static class FaceSheet
{
    public static void Write(GraphicsDevice device, string path, string? only, int scale)
    {
        var rooms = FortRoster.All.Where(room => FaceCatalog.Find(room.Id) is not null).ToList();

        // --face narrows the sheet to one occupant, which is the only way to get a useful
        // --face-scale: the Reach profile caps a texture at 2048 on a side, and ten rows of
        // anything past double blows straight through it.
        if (only is not null)
            rooms = rooms.Where(room =>
                room.Id.Contains(only, StringComparison.OrdinalIgnoreCase)).ToList();

        if (rooms.Count == 0)
        {
            Console.WriteLine($"No face matched '{only}'.");
            return;
        }

        var perPage = Math.Max(1, (2048 - 32) / (PortraitForge.Height + 16));
        if (rooms.Count > perPage)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var stem = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            for (var page = 0; page * perPage < rooms.Count; page++)
            {
                var slice = rooms.Skip(page * perPage).Take(perPage).ToList();
                WritePage(device, slice, scale,
                    Path.Combine(directory, $"{stem}-{page + 1}{extension}"));
            }

            return;
        }

        WritePage(device, rooms, scale, path);
    }

    private static void WritePage(GraphicsDevice device, List<FortRoom> rooms, int scale,
        string path)
    {
        var moods = Enum.GetValues<Expression>();
        var cellInner = new Point(PortraitForge.Width * scale, PortraitForge.Height * scale);

        var pad = 4 * scale;
        var cellW = cellInner.X + pad;
        var cellH = cellInner.Y + pad;
        var sheetW = cellW * moods.Length + pad;
        var sheetH = cellH * rooms.Count + pad;

        var sheet = new Color[sheetW * sheetH];
        Array.Fill(sheet, new Color(18, 22, 28));

        for (var row = 0; row < rooms.Count; row++)
        {
            var face = FaceCatalog.Find(rooms[row].Id)!;

            for (var column = 0; column < moods.Length; column++)
            {
                var pixels = PortraitForge.Render(face, moods[column]);
                var originX = pad + column * cellW;
                var originY = pad + row * cellH;

                for (var y = 0; y < cellInner.Y; y++)
                for (var x = 0; x < cellInner.X; x++)
                {
                    var source = pixels[y / scale * PortraitForge.Width + x / scale];
                    if (source.A == 0) continue;
                    sheet[(originY + y) * sheetW + originX + x] = source;
                }
            }
        }

        using var texture = new Texture2D(device, sheetW, sheetH);
        texture.SetData(sheet);

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using (var stream = File.Create(fullPath))
            texture.SaveAsPng(stream, sheetW, sheetH);

        Console.WriteLine(
            $"Saved {rooms.Count} face(s) x {moods.Length} moods ({sheetW}x{sheetH}) to {fullPath}");
    }
}
