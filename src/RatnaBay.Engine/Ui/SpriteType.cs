using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace RatnaBay.Engine.Ui;

/// <summary>
/// An 8×8 glyph atlas drawn as sprites: a pixel typeface with no font file behind it.
///
/// The whole face is the byte table below — public-domain IBM VGA 8×8 (font8x8, Daniel Hepper
/// / Marcel Sondaar) — baked into one atlas at load. No .ttf, no rasteriser, no hinting, and
/// nothing to ship: which is the argument for it as much as the look is.
///
/// Drawn at integer scale with point sampling, because that is the only way a pixel face
/// stays a pixel face. A glyph at 1.4× is a glyph with some rows two pixels tall and some one,
/// and the eye reads that as damage rather than as style.
///
/// Came from the second game built on this engine, where the whole HUD is drawn this way, and
/// is offered here as an alternative to the FontStash path rather than a replacement: the two
/// live side by side on <see cref="UiCanvas"/> and the game picks one.
/// </summary>
public sealed class SpriteType : IDisposable
{
    public const int Glyph = 8;

    private const int Cols = 16;
    private const int First = 32;
    private const int Count = 95;
    private const int Up = 95;
    private const int Down = 96;
    private const int Times = 97;
    private const int Dot = 98;
    private const int Slots = 99;

    /// <summary>
    /// How many cuts of the face are baked. Three, and the reason is in <see cref="HandFor"/>.
    /// </summary>
    public const int Hands = 3;

    private readonly Texture2D _atlas;
    private readonly byte[] _advance = new byte[Slots];

    /// <summary>
    /// font8x8_basic printable ASCII (U+0020–U+007E). Public domain, Daniel Hepper /
    /// Marcel Sondaar / IBM VGA.
    /// Each glyph is 8 row bytes; the least-significant bit is the left pixel.
    /// </summary>
    private static readonly byte[] Bits =
    [
        0, 0, 0, 0, 0, 0, 0, 0, 24, 60, 60, 24, 24, 0, 24, 0, 54, 54, 0, 0, 0, 0, 0, 0,
        54, 54, 127, 54, 127, 54, 54, 0, 12, 62, 3, 30, 48, 31, 12, 0, 0, 99, 51, 24, 12, 102, 99, 0,
        28, 54, 28, 110, 59, 51, 110, 0, 6, 6, 3, 0, 0, 0, 0, 0, 24, 12, 6, 6, 6, 12, 24, 0,
        6, 12, 24, 24, 24, 12, 6, 0, 0, 102, 60, 255, 60, 102, 0, 0, 0, 12, 12, 63, 12, 12, 0, 0,
        0, 0, 0, 0, 0, 12, 12, 6, 0, 0, 0, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 12, 0,
        96, 48, 24, 12, 6, 3, 1, 0, 62, 99, 115, 123, 111, 103, 62, 0, 12, 14, 12, 12, 12, 12, 63, 0,
        30, 51, 48, 28, 6, 51, 63, 0, 30, 51, 48, 28, 48, 51, 30, 0, 56, 60, 54, 51, 127, 48, 120, 0,
        63, 3, 31, 48, 48, 51, 30, 0, 28, 6, 3, 31, 51, 51, 30, 0, 63, 51, 48, 24, 12, 12, 12, 0,
        30, 51, 51, 30, 51, 51, 30, 0, 30, 51, 51, 62, 48, 24, 14, 0, 0, 12, 12, 0, 0, 12, 12, 0,
        0, 12, 12, 0, 0, 12, 12, 6, 24, 12, 6, 3, 6, 12, 24, 0, 0, 0, 63, 0, 0, 63, 0, 0,
        6, 12, 24, 48, 24, 12, 6, 0, 30, 51, 48, 24, 12, 0, 12, 0, 62, 99, 123, 123, 123, 3, 30, 0,
        12, 30, 51, 51, 63, 51, 51, 0, 63, 102, 102, 62, 102, 102, 63, 0, 60, 102, 3, 3, 3, 102, 60, 0,
        31, 54, 102, 102, 102, 54, 31, 0, 127, 70, 22, 30, 22, 70, 127, 0, 127, 70, 22, 30, 22, 6, 15, 0,
        60, 102, 3, 3, 115, 102, 124, 0, 51, 51, 51, 63, 51, 51, 51, 0, 30, 12, 12, 12, 12, 12, 30, 0,
        120, 48, 48, 48, 51, 51, 30, 0, 103, 102, 54, 30, 54, 102, 103, 0, 15, 6, 6, 6, 70, 102, 127, 0,
        99, 119, 127, 127, 107, 99, 99, 0, 99, 103, 111, 123, 115, 99, 99, 0, 28, 54, 99, 99, 99, 54, 28, 0,
        63, 102, 102, 62, 6, 6, 15, 0, 30, 51, 51, 51, 59, 30, 56, 0, 63, 102, 102, 62, 54, 102, 103, 0,
        30, 51, 7, 14, 56, 51, 30, 0, 63, 45, 12, 12, 12, 12, 30, 0, 51, 51, 51, 51, 51, 51, 63, 0,
        51, 51, 51, 51, 51, 30, 12, 0, 99, 99, 99, 107, 127, 119, 99, 0, 99, 99, 54, 28, 28, 54, 99, 0,
        51, 51, 51, 30, 12, 12, 30, 0, 127, 99, 49, 24, 76, 102, 127, 0, 30, 6, 6, 6, 6, 6, 30, 0,
        3, 6, 12, 24, 48, 96, 64, 0, 30, 24, 24, 24, 24, 24, 30, 0, 8, 28, 54, 99, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 255, 12, 12, 24, 0, 0, 0, 0, 0, 0, 0, 30, 48, 62, 51, 110, 0,
        7, 6, 6, 62, 102, 102, 59, 0, 0, 0, 30, 51, 3, 51, 30, 0, 56, 48, 48, 62, 51, 51, 110, 0,
        0, 0, 30, 51, 63, 3, 30, 0, 28, 54, 6, 15, 6, 6, 15, 0, 0, 0, 110, 51, 51, 62, 48, 31,
        7, 6, 54, 110, 102, 102, 103, 0, 12, 0, 14, 12, 12, 12, 30, 0, 48, 0, 48, 48, 48, 51, 51, 30,
        7, 6, 102, 54, 30, 54, 103, 0, 14, 12, 12, 12, 12, 12, 30, 0, 0, 0, 51, 127, 127, 107, 99, 0,
        0, 0, 31, 51, 51, 51, 51, 0, 0, 0, 30, 51, 51, 51, 30, 0, 0, 0, 59, 102, 102, 62, 6, 15,
        0, 0, 110, 51, 51, 62, 48, 120, 0, 0, 59, 110, 102, 6, 15, 0, 0, 0, 62, 3, 30, 48, 31, 0,
        8, 12, 62, 12, 12, 44, 24, 0, 0, 0, 51, 51, 51, 51, 110, 0, 0, 0, 51, 51, 51, 30, 12, 0,
        0, 0, 99, 107, 127, 127, 54, 0, 0, 0, 99, 54, 28, 54, 99, 0, 0, 0, 51, 51, 51, 62, 48, 31,
        0, 0, 63, 25, 12, 38, 63, 0, 56, 12, 12, 7, 12, 12, 56, 0, 24, 24, 24, 0, 24, 24, 24, 0,
        7, 12, 12, 56, 12, 12, 7, 0, 110, 59, 0, 0, 0, 0, 0, 0
    ];

    private SpriteType(Texture2D atlas) => _atlas = atlas;

    public static SpriteType Bake(GraphicsDevice device)
    {
        var rows = (Slots * Hands + Cols - 1) / Cols;
        var width = Cols * Glyph;
        var height = rows * Glyph;
        var pixels = new Color[width * height];
        var type = new SpriteType(new Texture2D(device, width, height, false, SurfaceFormat.Color));

        for (var i = 0; i < Count; i++)
        {
            var glyph = Bits.AsSpan(i * Glyph, Glyph);

            // The advance is the base cut's, for every cut. A hand that changed the width
            // would change where the next letter starts, so the same sentence would measure
            // differently depending on which letters happened to be cut which way -- and a
            // panel laid out around it would move as the text was re-hashed.
            type._advance[i] = AdvanceOf(glyph);

            for (var hand = 0; hand < Hands; hand++)
                Stamp(pixels, width, i + hand * Slots, Cut(glyph, hand, i));
        }

        foreach (var (slot, bits, advance) in new (int, byte[], byte)[]
        {
            (Up, Triangle(up: true), 8),
            (Down, Triangle(up: false), 8),
            (Times, Cross(), 7),
            (Dot, MidDot(), 6)
        })
        {
            type._advance[slot] = advance;

            // Marks are cut once and used in all three hands. A wobbling arrow is a broken
            // arrow: these are read as symbols rather than as letters, and an eye forgives an
            // uneven 'e' where it will not forgive an uneven ▲.
            for (var hand = 0; hand < Hands; hand++)
                Stamp(pixels, width, slot + hand * Slots, bits);
        }

        type._advance[0] = 4;

        type._atlas.SetData(pixels);
        return type;
    }

    /// <summary>
    /// One glyph, cut three ways.
    ///
    /// Type set by hand is uneven: the same letter twice in a word is never quite the same
    /// letter, because the ink took differently and the sort sat differently in the stick.
    /// A bitmap face has the opposite problem -- every 'e' on the screen is pixel-identical,
    /// which is exactly what makes a wall of it read as a spreadsheet.
    ///
    /// Three cuts, then, chosen per letter and per position, so a word is set from three
    /// slightly different sorts:
    ///
    /// - **0, the clean cut.** The face as drawn. Roughly half of everything.
    /// - **1, the inked cut.** One extra pixel where a stroke already ends, so the letter
    ///   looks like it took a little more ink. Never outside the letter's own bounding box:
    ///   spreading past it would touch the neighbour and read as a rendering fault.
    /// - **2, the worn cut.** One pixel lifted from a corner, the way a sort loses its edge.
    ///
    /// Deliberately small. At eight pixels a letter has about forty lit cells, so a single
    /// pixel is a two-per-cent change -- and two pixels is a different letter. Every attempt
    /// at something bolder here reads as damage rather than as character.
    /// </summary>
    private static byte[] Cut(ReadOnlySpan<byte> rows, int hand, int glyph)
    {
        var cut = new byte[Glyph];
        for (var y = 0; y < Glyph; y++) cut[y] = rows[y];
        if (hand == 0) return cut;

        var top = -1;
        var bottom = -1;
        for (var y = 0; y < Glyph; y++)
        {
            if (cut[y] == 0) continue;
            if (top < 0) top = y;
            bottom = y;
        }

        // Nothing to cut in a space, and nothing safe to cut in a two-row mark.
        if (top < 0 || bottom - top < 2) return cut;

        if (hand == 1)
        {
            // Inked: a single nub above one end of the top stroke -- where a pen starts and
            // leaves a little more ink than it meant to.
            //
            // Above, not below, and that is not a preference. A nub under a round letter is a
            // descender: an inked 'o' read as a 'q' and "one" came out "qne". The head of a
            // letter has no such reading, so the same pixel there is just weight.
            //
            // A nub, not a thickened row, for the same class of reason. Thickening ORs a
            // stroke into the row beside it, which closes the counter of every e, a, o and g
            // on the screen -- at eight pixels a counter is one or two pixels of hole, and
            // filling it turns the letter into a blob.
            if (top - 1 < 0) return cut;

            var head = cut[top];
            cut[top - 1] |= (byte)(glyph % 2 == 0 ? head & (byte)-head : HighestBit(head));
            return cut;
        }

        // Worn: one pixel off the outside edge of the bottom stroke, and only when that stroke
        // is three or more pixels wide, so a stem never loses half its width and a serif never
        // vanishes entirely. Wear belongs at the foot, which is the part that takes the weight.
        var foot = cut[bottom];
        if (BitCount(foot) < 3) return cut;

        cut[bottom] = (byte)(foot & ~(glyph % 2 == 0 ? HighestBit(foot) : foot & (byte)-foot));
        return cut;
    }

    private static byte HighestBit(byte value)
    {
        var bit = 0;
        for (var i = 0; i < 8; i++)
            if ((value & (1 << i)) != 0) bit = i;

        return (byte)(1 << bit);
    }

    private static int BitCount(byte value)
    {
        var count = 0;
        for (var i = 0; i < 8; i++)
            if ((value & (1 << i)) != 0) count++;

        return count;
    }

    /// <summary>
    /// Which cut a letter is set in, decided by where it is rather than by chance.
    ///
    /// **This must be a pure function of the text and the position, and never of time.** Text
    /// is redrawn every frame; a random cut per frame would make every label on the screen
    /// crawl. The mix below is the cheapest thing that scatters well: the letter, its index,
    /// and the length of the line it is in, so the same word in two different sentences is
    /// still set differently.
    /// </summary>
    private static int HandFor(char value, int index, int length)
    {
        var mix = value * 2654435761u + (uint)index * 2246822519u + (uint)length * 3266489917u;
        mix ^= mix >> 15;
        mix *= 2246822519u;
        mix ^= mix >> 13;

        // Weighted: half clean, and the worn cut rarer than the inked one. An even three-way
        // split looked like a font with a bug in it -- unevenness reads as craft only while
        // most of the letters are still the plain ones.
        return (mix % 8) switch
        {
            0 or 1 or 2 or 3 or 4 => 0,
            5 or 6 => 1,
            _ => 2
        };
    }

    /// <summary>Map a UI size onto 1× / 2× / 3× sprite cells. Keep scale low so stems stay open.</summary>
    public static int PixelScale(float requested, bool heading)
    {
        if (heading && requested >= 28f) return 3;
        if (heading || requested >= 12f) return 2;
        return 1;
    }

    public static float LineHeight(int pixelScale) => Glyph * pixelScale + 2;

    /// <summary>Width in logical pixels. Independent of which cut each letter is set in.</summary>
    public float Measure(string value, int pixelScale)
    {
        if (string.IsNullOrEmpty(value)) return 0f;
        var w = 0;
        foreach (var c in value)
            w += _advance[IndexOf(c)];
        return w * pixelScale;
    }

    public void Draw(SpriteBatch batch, string value, Vector2 position, int pixelScale, Color color)
    {
        if (string.IsNullOrEmpty(value) || color.A == 0) return;

        var x = (int)MathF.Round(position.X);
        var y = (int)MathF.Round(position.Y);
        var shadow = new Color((byte)0, (byte)0, (byte)0, (byte)Math.Min(color.A, (byte)110));

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var index = IndexOf(c);
            var src = Cell(index + HandFor(c, i, value.Length) * Slots);
            var dest = new Rectangle(x, y, Glyph * pixelScale, Glyph * pixelScale);
            if (shadow.A > 20)
                batch.Draw(_atlas, Offset(dest, 1, 1), src, shadow);
            batch.Draw(_atlas, dest, src, color);
            x += _advance[index] * pixelScale;
        }
    }

    /// <summary>
    /// True when every character in the string has a glyph in this face.
    ///
    /// The face is printable ASCII and a handful of marks. Ratna Bay has one line of
    /// Devanagari in it -- the verse on the pillar -- and rendering that as a row of question
    /// marks is worse than rendering it in a different typeface. The canvas asks this and
    /// falls back for the whole string rather than mixing faces inside one line.
    /// </summary>
    public static bool Handles(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;

        foreach (var c in value)
        {
            if (c is '▲' or '▲' or '▼' or '▼' or '×' or '✕' or '✖' or '·' or '•'
                or '—' or '–' or '−' or '’' or '‘' or '‛' or '“' or '”') continue;

            if (c < First || c >= First + Count) return false;
        }

        return true;
    }

    public void Dispose() => _atlas.Dispose();

    private static Rectangle Cell(int index) =>
        new(index % Cols * Glyph, index / Cols * Glyph, Glyph, Glyph);

    private static Rectangle Offset(Rectangle r, int x, int y) =>
        new(r.X + x, r.Y + y, r.Width, r.Height);

    private static int IndexOf(char c) => c switch
    {
        '▲' or '\u25B2' => Up,
        '▼' or '\u25BC' => Down,
        '×' or '✕' or '✖' => Times,
        '·' or '•' => Dot,
        '—' or '–' or '−' => '-' - First,
        '’' or '‘' or '‛' => '\'' - First,
        '“' or '”' => '"' - First,
        _ when c >= First && c < First + Count => c - First,
        _ => '?' - First
    };

    private static void Stamp(Color[] pixels, int width, int index, ReadOnlySpan<byte> rows)
    {
        var ox = index % Cols * Glyph;
        var oy = index / Cols * Glyph;
        for (var y = 0; y < Glyph; y++)
        {
            var bits = rows[y];
            for (var x = 0; x < Glyph; x++)
            {
                if ((bits & (1 << x)) == 0) continue;
                pixels[(oy + y) * width + ox + x] = Color.White;
            }
        }
    }

    private static byte AdvanceOf(ReadOnlySpan<byte> rows)
    {
        var max = 0;
        var any = false;
        for (var y = 0; y < Glyph; y++)
        {
            var bits = rows[y];
            for (var x = 0; x < Glyph; x++)
            {
                if ((bits & (1 << x)) == 0) continue;
                any = true;
                if (x > max) max = x;
            }
        }

        return (byte)(any ? Math.Min(8, max + 2) : 4);
    }

    private static byte[] Triangle(bool up)
    {
        var rows = new byte[Glyph];
        for (var y = 0; y < 7; y++)
        {
            var t = up ? y : 6 - y;
            var left = 3 - t / 2;
            var right = 4 + t / 2;
            byte bits = 0;
            for (var x = left; x <= right; x++)
                bits |= (byte)(1 << x);
            rows[y] = bits;
        }

        return rows;
    }

    private static byte[] Cross()
    {
        return
        [
            0b01000010,
            0b00100100,
            0b00011000,
            0b00011000,
            0b00100100,
            0b01000010,
            0, 0
        ];
    }

    private static byte[] MidDot() => [0, 0, 0, 0b00011000, 0b00011000, 0, 0, 0];
}
