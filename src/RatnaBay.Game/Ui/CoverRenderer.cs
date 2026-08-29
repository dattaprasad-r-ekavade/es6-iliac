using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

/// <summary>
/// The store cover, drawn over a real mine.
///
/// A raw screenshot makes a poor cover: the game is a dark brick corridor, and what is
/// actually interesting about it is a decision, which does not photograph. So the mine is
/// pushed back into being a backdrop -- darkened, vignetted, lit from below as if by the
/// lamp -- and the thing on top is the choice the game is built on: five mines, each one
/// costing more to enter than the last.
///
/// It has to survive being shrunk to 315x250 in a gallery, so there are exactly three
/// levels of information: the title, one line saying what it is, and a ladder of numbers
/// that reads as texture at thumbnail size and as the premise at full size.
///
/// Drawn in raw device pixels rather than through the UI transform, which exists to letter
/// -box a 1280x720 layout and would leave bars down the sides of a 1260x1000 frame. That is
/// the one deliberate <see cref="SpriteBatch"/> exception AGENTS.md names.
/// </summary>
internal sealed class CoverRenderer
{
    private readonly UiCanvas _ui;

    public CoverRenderer(UiCanvas canvas) => _ui = canvas;

    public void Draw(int width, int height)
    {
        // The UI transform assumes the 16:9 logical canvas. This composition is its own shape,
        // so it is drawn 1:1 and the font picker is told the scale is honest.
        _ui.OverrideScale(1f);
        _ui.Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);

        // Push the mine back. It is scenery here, not the subject.
        _ui.Fill(new Rectangle(0, 0, width, height), new Color(4, 8, 13, 132));

        // A vignette in horizontal bands: cheap, and the only shape that matters is dark at
        // the edges and open in the middle.
        for (var band = 0; band < 40; band++)
        {
            var thickness = height / 40;
            var y = band * thickness;
            var toEdge = MathF.Abs(band - 19.5f) / 19.5f;
            var strength = MathF.Pow(toEdge, 2.2f) * 0.86f;
            _ui.Fill(new Rectangle(0, y, width, thickness + 1),
                new Color(2, 5, 9) * strength);
        }

        // Lamplight from where the player's hand would be.
        for (var ring = 12; ring > 0; ring--)
        {
            var radius = ring * 46;
            _ui.Fill(new Rectangle(width / 2 - radius, height - 150 - radius / 3, radius * 2, radius / 2),
                new Color(196, 140, 74) * 0.012f);
        }

        var centre = width / 2f;

        _ui.TextCentred("RATNA BAY", centre, height * 0.20f, 116, new Color(243, 236, 224));

        _ui.Fill(new Rectangle((int)(centre - 210), (int)(height * 0.335f), 420, 2),
            new Color(205, 157, 98, 190));

        _ui.TextCentred("AN ENDLESS MINE", centre, height * 0.355f, 27,
            new Color(176, 205, 208));

        // The premise, read out of the economy rather than typed in, so the cover cannot end
        // up advertising prices the game no longer charges.
        var ladderTop = (int)(height * 0.465f);
        for (var tier = MineEntry.MinTier; tier <= MineEntry.MaxTier; tier++)
        {
            var index = tier - MineEntry.MinTier;
            var row = new Rectangle((int)(centre - 300), ladderTop + index * 74, 600, 62);
            var cost = MineEntry.CostOf(tier);

            // Each mine deeper in the ladder is drawn a shade hotter and a shade brighter, so
            // at thumbnail size the block reads as something escalating.
            var heat = index / (float)(MineEntry.MaxTier - MineEntry.MinTier);
            var edge = new Color(
                (int)MathHelper.Lerp(72, 214, heat),
                (int)MathHelper.Lerp(104, 132, heat),
                (int)MathHelper.Lerp(118, 84, heat));

            _ui.Fill(row, new Color(8, 15, 23) * MathHelper.Lerp(0.62f, 0.86f, heat));
            _ui.Border(row, edge);

            _ui.Text($"TIER {tier}", new Vector2(row.X + 26, row.Y + 19), 24,
                new Color(226, 233, 232));
            _ui.TextRight(cost == 0 ? "free" : $"{cost} stones", row.Right - 26, row.Y + 20, 22,
                cost == 0 ? new Color(150, 200, 158) : new Color(232, 194, 116));
        }

        _ui.TextCentred("Every room pays more than the last. Every door asks if that is enough.",
            centre, height - 92f, 25, new Color(198, 210, 210));

        _ui.Batch.End();
    }
}
