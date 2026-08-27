using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Flat interface anchored to things in the world: nameplates, floating damage, threat arrows.
///
/// Drawn in the UI pass rather than as billboards, so text stays crisp at any distance and is
/// never clipped into a wall. Everything here needs one thing from the game loop — a
/// <see cref="WorldProjector"/> — and nothing else.
/// </summary>
internal sealed class MarkerRenderer
{
    /// <summary>Past this, a nameplate is clutter rather than information.</summary>
    public const float NameplateRange = 26f;

    private readonly UiCanvas _ui;

    public MarkerRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>
    /// A plate over each enemy with its name, its level and its state.
    ///
    /// One bar pinned to the middle of the screen could only ever describe whatever the
    /// crosshair was over, so a room with five things in it reported one of them. A plate each
    /// says which is hurt, and puts it where the player is already looking.
    /// </summary>
    public void DrawNameplates(IReadOnlyList<NameplateState> plates)
    {
        foreach (var plate in plates) DrawNameplate(plate);
    }

    private void DrawNameplate(NameplateState plate)
    {
        var scale = plate.Scale;
        var width = (int)(150 * scale);
        var barHeight = (int)(7 * scale);
        var nameSize = 13f * scale;

        var bar = new Rectangle((int)(plate.Anchor.X - width / 2f), (int)plate.Anchor.Y,
            width, barHeight);

        // Name and level above the bar; the level is the only warning a player gets that this
        // room is deeper than the last one.
        _ui.TextCentred(plate.Label, plate.Anchor.X, bar.Y - nameSize - 3f * scale, nameSize,
            plate.Focused ? Color.White : new Color(214, 206, 194));

        _ui.Fill(bar, new Color(12, 10, 12, 220));
        _ui.Fill(new Rectangle(bar.X, bar.Y, (int)(bar.Width * plate.HealthFraction), bar.Height),
            plate.Vulnerable ? new Color(232, 186, 96) : new Color(178, 62, 66));

        // The focused enemy gets a brighter rule, so "will this swing connect?" is still
        // answered — that was the one thing the centred bar was genuinely good at.
        _ui.Border(bar, plate.Focused ? new Color(238, 226, 200, 210) : new Color(0, 0, 0, 150));

        if (plate.Status.Length == 0) return;

        _ui.TextCentred(plate.Status, plate.Anchor.X, bar.Bottom + 2f * scale, 12f * scale,
            plate.Status == "striking" ? new Color(236, 148, 122) : UiTheme.Gold);
    }

    /// <summary>Damage and status, floating up from where it happened.</summary>
    public void DrawFloatingNumbers(IReadOnlyList<FloatingNumber> numbers, WorldProjector projector)
    {
        foreach (var number in numbers)
        {
            var fade = 1f - number.Age;
            var rise = number.Age * 46f;
            var selfInflicted = CombatFeedback.IsSelfInflicted(number);
            Vector2 position;

            if (selfInflicted)
            {
                // Damage taken belongs on the player, not out in the world.
                position = new Vector2(UiLayout.Width / 2f, UiLayout.Height / 2f + 78f - rise);
            }
            else
            {
                if (!projector.TryProject(
                        new Vector3(number.Origin.X, number.Origin.Y + 1.9f, number.Origin.Z),
                        out position))
                    continue;

                position.X += number.Drift;
                position.Y -= rise;
            }

            // Numbers were already being drawn for sword hits, but at melee range they sat
            // pale over a pale sprite and went unnoticed. A dark shadow behind them is what
            // makes them read against anything.
            var size = selfInflicted ? 19 : 24;
            _ui.TextCentred(number.Text, position.X + 2f, position.Y + 2f, size,
                new Color(0, 0, 0, 190) * fade);
            _ui.TextCentred(number.Text, position.X, position.Y, size, number.Colour * fade);
        }
    }

    /// <summary>
    /// Small markers around the crosshair for living enemies nearby.
    ///
    /// Testers lost track of bandits the moment they left the view. The marker fades with
    /// distance so it reads as "something is over there", not as a wallhack.
    /// </summary>
    public void DrawThreatArrows(IEnumerable<(Enemy Enemy, float Bearing, float Distance)> threats)
    {
        const float centreX = UiLayout.Width / 2f;
        const float centreY = UiLayout.Height / 2f;
        const float radius = 172f;

        foreach (var (_, bearing, distance) in threats)
        {
            // Anything comfortably in front is already visible; do not clutter the view.
            if (MathF.Abs(bearing) < 0.42f) continue;

            var nearness = MathHelper.Clamp(1f - distance / 26f, 0.25f, 1f);
            var colour = new Color(226, 168, 96) * (0.5f + nearness * 0.45f);

            var x = centreX + MathF.Sin(bearing) * radius;
            var y = centreY - MathF.Cos(bearing) * radius;

            // A small triangle pointing outward along the bearing.
            for (var step = 0; step < 8; step++)
            {
                var width = 8 - step;
                var px = x + MathF.Sin(bearing) * step;
                var py = y - MathF.Cos(bearing) * step;
                _ui.Fill(new Rectangle((int)px - width / 2, (int)py - 1, Math.Max(1, width), 2),
                    colour);
            }
        }
    }

    /// <summary>
    /// A name floating over a fixture in the yard.
    ///
    /// Fades in with distance rather than out: a label is most useful from across the yard and
    /// just noise when you are stood at the thing it names.
    /// </summary>
    public void DrawSign(string title, string subtitle, Vector3 anchor, float flatDistance,
        Color colour, WorldProjector projector)
    {
        if (!projector.TryProject(anchor, out var screen)) return;

        var fade = MathHelper.Clamp((flatDistance - 3f) / 5f, 0f, 1f);
        if (fade <= 0.02f) return;

        // A heavier shadow than the mine needs. These sit against sunlit sandstone, and a pale
        // label on a pale wall is a label nobody reads.
        for (var dx = -2; dx <= 2; dx += 2)
        for (var dy = -2; dy <= 2; dy += 2)
        {
            if (dx == 0 && dy == 0) continue;
            _ui.TextCentred(title, screen.X + dx, screen.Y + dy, 17, new Color(0, 0, 0, 190) * fade);
        }

        _ui.TextCentred(title, screen.X, screen.Y, 17, colour * fade);
        _ui.TextCentred(subtitle, screen.X + 1f, screen.Y + 21f, 12, new Color(0, 0, 0, 170) * fade);
        _ui.TextCentred(subtitle, screen.X, screen.Y + 20f, 12, new Color(228, 232, 236) * fade);
    }

    /// <summary>
    /// Content that failed to load, said out loud.
    ///
    /// These were only ever shown on the Renderer Lab screen, so a damaged install dropped
    /// the player into an empty void with a working HUD and no explanation. Saves already
    /// follow the rule that a half-load must fail loudly; content now does too.
    /// </summary>
    public void DrawContentErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0) return;

        var panel = new Rectangle(300, 84, 680, 44 + errors.Count * 22);
        _ui.Panel(panel, new Color(38, 12, 12, 238), new Color(198, 96, 88));
        _ui.Text("CONTENT FAILED TO LOAD", new Vector2(panel.X + 16, panel.Y + 12), 14,
            new Color(255, 196, 186));

        var y = panel.Y + 36f;
        foreach (var error in errors)
        {
            _ui.TextFit(error, new Vector2(panel.X + 16, y), 648f, 13, new Color(240, 208, 202));
            y += 22f;
        }
    }
}

/// <summary>
/// One enemy's nameplate, already projected and sized.
///
/// A snapshot rather than the enemy itself, so the renderer cannot reach back into the fight
/// and the projection happens once, where the camera lives.
/// </summary>
internal readonly record struct NameplateState(
    Vector2 Anchor,
    float Scale,
    string Label,
    string Status,
    float HealthFraction,
    bool Vulnerable,
    bool Focused);
