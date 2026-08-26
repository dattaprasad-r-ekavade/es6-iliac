using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client;

/// <summary>
/// The small, state-driven part of the in-world HUD.
///
/// Layout and presentation only. Game1 builds a <see cref="WorldHudState"/> snapshot and
/// coordinates draw order with the weapon, nameplates and world prompts.
/// </summary>
internal sealed class HudRenderer
{
    private readonly UiCanvas _ui;

    public HudRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>Domain events, rendered above the vitals. Newest last, fading as they expire.</summary>
    public void DrawToasts(WorldHudState state)
    {
        if (state.Toasts.Count == 0) return;

        var y = UiLayout.Height - 196f - state.Toasts.Count * 28f;
        foreach (var toast in state.Toasts)
        {
            var alpha = MathHelper.Clamp(toast.Remaining, 0f, 1f);
            _ui.TextCentred(toast.Message, UiLayout.Width / 2f, y, 17,
                new Color(240, 230, 202) * alpha);
            y += 28f;
        }
    }

    /// <summary>Level, gold and the one key that opens the rest. Bottom-right, compact.</summary>
    public void DrawStatusStrip(WorldHudState state)
    {
        if (!state.HasSession) return;

        var panel = new Rectangle(UiLayout.Width - 264, UiLayout.Height - 88, 240, 64);
        _ui.Panel(panel, new Color(6, 13, 20, 226), new Color(76, 101, 116));

        _ui.Text($"LEVEL {state.Level}", new Vector2(panel.X + 18, panel.Y + 12), 16, Color.White);
        _ui.TextRight($"{state.Gold} gold", panel.Right - 18, panel.Y + 12, 16,
            new Color(228, 197, 122));
        _ui.Text(state.WeaponName, new Vector2(panel.X + 18, panel.Y + 38), 13,
            state.IsBlocking ? new Color(232, 194, 116) : new Color(203, 216, 214));
        // Blank until the first averaging window closes: the counter used to show whatever
        // the opening, texture-generating window computed, so a build running at 700 fps
        // could report 4. A misleading diagnostic is worse than none.
        //
        // A capture is that same wrong number with an audience. --screenshot draws a few
        // frames and exits, so the averaging window closes on the startup frames, the rate
        // reads about 1, and it gets the red reserved for a build in trouble. That was the
        // first hard number a stranger read about how the game runs, on the store page.
        if (!state.ShowFrameRate) return;

        _ui.TextRight(state.FramesPerSecond > 0f ? $"{state.FramesPerSecond:0} fps" : "— fps",
            panel.Right - 18, panel.Y + 38, 13,
            state.FramesPerSecond is > 0f and < 50f
                ? new Color(228, 128, 118)
                : new Color(146, 174, 178));
    }

    /// <summary>Draws the non-panel HUD from one presentation snapshot.</summary>
    public void DrawDamageFlash(WorldHudState state)
    {
        if (state.DamageFlash <= 0f) return;

        var tint = new Color(150, 24, 28) * (state.DamageFlash * 0.45f);
        const int band = 90;
        _ui.Fill(new Rectangle(0, 0, UiLayout.Width, band), tint);
        _ui.Fill(new Rectangle(0, UiLayout.Height - band, UiLayout.Width, band), tint);
        _ui.Fill(new Rectangle(0, 0, band, UiLayout.Height), tint);
        _ui.Fill(new Rectangle(UiLayout.Width - band, 0, band, UiLayout.Height), tint);
    }

    /// <summary>Stealth vignette that keeps the crouched stance readable in the world.</summary>
    public void DrawSneakOverlay(WorldHudState state)
    {
        if (!state.IsCrouching) return;

        var edge = state.Awareness switch
        {
            AwarenessLevel.Alerted => new Color(108, 30, 28),
            AwarenessLevel.Suspicious => new Color(104, 76, 31),
            _ => new Color(13, 25, 32)
        };

        _ui.Fill(new Rectangle(0, 0, UiLayout.Width, 30), edge * 0.66f);
        _ui.Fill(new Rectangle(0, UiLayout.Height - 30, UiLayout.Width, 30), edge * 0.66f);
        _ui.Fill(new Rectangle(0, 0, 42, UiLayout.Height), edge * 0.54f);
        _ui.Fill(new Rectangle(UiLayout.Width - 42, 0, 42, UiLayout.Height), edge * 0.54f);
        _ui.Fill(new Rectangle(0, 30, 16, UiLayout.Height - 60), edge * 0.28f);
        _ui.Fill(new Rectangle(UiLayout.Width - 16, 30, 16, UiLayout.Height - 60), edge * 0.28f);
    }

    /// <summary>Crosshair and stealth eye, driven only by the current HUD snapshot.</summary>
    public void DrawCrosshair(WorldHudState state)
    {
        const int cx = UiLayout.Width / 2;
        const int cy = UiLayout.Height / 2;

        if (state.IsCrouching)
        {
            var colour = state.Awareness switch
            {
                AwarenessLevel.Alerted => new Color(238, 91, 78, 240),
                AwarenessLevel.Suspicious => new Color(239, 190, 91, 240),
                _ => new Color(220, 235, 226, 240)
            };
            DrawSneakEye(cx, cy, colour);
            _ui.TextCentred("SNEAK", cx, cy + 22, 11, colour);
            return;
        }

        var shadow = new Color(0, 0, 0, 165);
        var ink = new Color(244, 248, 246, 225);
        foreach (var (colour, grow) in new[] { (shadow, 1), (ink, 0) })
        {
            _ui.Fill(new Rectangle(cx - 10 - grow, cy - grow, 7 + grow * 2, 2 + grow * 2), colour);
            _ui.Fill(new Rectangle(cx + 3 - grow, cy - grow, 7 + grow * 2, 2 + grow * 2), colour);
            _ui.Fill(new Rectangle(cx - grow, cy - 10 - grow, 2 + grow * 2, 7 + grow * 2), colour);
            _ui.Fill(new Rectangle(cx - grow, cy + 3 - grow, 2 + grow * 2, 7 + grow * 2), colour);
        }
    }

    /// <summary>Hit confirmation marker at the centre of the screen.</summary>
    public void DrawHitMarker(WorldHudState state)
    {
        var strength = MathF.Max(state.HitMarker, state.KillMarker);
        if (strength <= 0f) return;

        const int cx = UiLayout.Width / 2;
        const int cy = UiLayout.Height / 2;
        var colour = (state.KillMarker > 0f
            ? new Color(255, 214, 122)
            : new Color(255, 252, 246)) * strength;
        var spread = (int)(6f + (1f - strength) * 7f);

        for (var i = 0; i < 4; i++)
        {
            var dx = i < 2 ? (i == 0 ? -1 : 1) : 0;
            var dy = i < 2 ? 0 : (i == 2 ? -1 : 1);
            for (var step = 0; step < 7; step++)
            {
                var x = cx + dx * (spread + step);
                var y = cy + dy * (spread + step);
                _ui.Fill(new Rectangle(x - 1, y - 1, 2, 2), colour);
            }
        }
    }

    /// <summary>Incoming-damage arcs around the crosshair.</summary>
    public void DrawDamageDirections(WorldHudState state)
    {
        const float centreX = UiLayout.Width / 2f;
        const float centreY = UiLayout.Height / 2f;
        const float radius = 132f;

        foreach (var direction in state.DamageDirections)
        {
            var fade = direction.Duration <= 0f ? 0f : direction.Remaining / direction.Duration;
            var colour = new Color(232, 96, 88) * (fade * 0.9f);
            for (var offset = -0.34f; offset <= 0.34f; offset += 0.02f)
            {
                var angle = direction.Bearing + offset;
                var thickness = 5f - MathF.Abs(offset) * 8f;
                var x = centreX + MathF.Sin(angle) * radius;
                var y = centreY - MathF.Cos(angle) * radius;
                _ui.Fill(new Rectangle((int)x - 2, (int)y - 2, (int)MathF.Max(2f, thickness),
                    (int)MathF.Max(2f, thickness)), colour);
            }
        }
    }

    /// <summary>Short-lived cast tint and result sentence.</summary>
    public void DrawCastBanner(WorldHudState state)
    {
        if (state.CastBanner <= 0f) return;

        var tintStrength = MathF.Max(0f, state.CastBanner - 0.55f) / 0.45f;
        if (tintStrength > 0f)
        {
            var tint = state.CastTint * (tintStrength * 0.2f);
            const int band = 72;
            _ui.Fill(new Rectangle(0, 0, UiLayout.Width, band), tint);
            _ui.Fill(new Rectangle(0, UiLayout.Height - band, UiLayout.Width, band), tint);
            _ui.Fill(new Rectangle(0, 0, band, UiLayout.Height), tint);
            _ui.Fill(new Rectangle(UiLayout.Width - band, 0, band, UiLayout.Height), tint);
        }

        var fade = MathHelper.Clamp(state.CastBanner * 1.6f, 0f, 1f);
        _ui.TextCentred(state.CastLine, UiLayout.Width / 2f, UiLayout.Height / 2f + 118f, 19,
            state.CastColour * fade);
    }

    public void DrawLocationBanner(WorldHudState state) =>
        _ui.TextCentred(state.LocationCaption, UiLayout.Width / 2f, 24f, 15,
            new Color(196, 214, 214));

    /// <summary>Awareness state and suspicion amount.</summary>
    public void DrawAwareness(WorldHudState state)
    {
        if (!state.HasSession) return;

        var panel = new Rectangle(UiLayout.Width - 264, 24, 240, 48);
        var colour = state.Awareness switch
        {
            AwarenessLevel.Alerted => new Color(188, 65, 68),
            AwarenessLevel.Suspicious => new Color(205, 157, 98),
            _ => new Color(76, 101, 116)
        };
        _ui.Panel(panel, new Color(6, 13, 20, 226), colour);
        _ui.Text("AWARENESS", new Vector2(panel.X + 14, panel.Y + 8), 12, Color.White);
        _ui.TextRight(state.Awareness.ToString().ToUpperInvariant(), panel.Right - 14, panel.Y + 8,
            12, state.Awareness == AwarenessLevel.Unaware
                ? new Color(180, 196, 194) : colour);
        _ui.Fill(new Rectangle(panel.X + 14, panel.Y + 29, panel.Width - 28, 7),
            new Color(20, 27, 33));
        _ui.Fill(new Rectangle(panel.X + 14, panel.Y + 29,
            (int)((panel.Width - 28) * MathHelper.Clamp(state.Suspicion, 0f, 1f)), 7), colour);
    }

    /// <summary>Objective text with the bearing calculated before rendering begins.</summary>
    public void DrawObjective(WorldHudState state)
    {
        if (state.ObjectiveTitle is null) return;

        var panel = new Rectangle(24, 24, 360, 116);
        _ui.Panel(panel, new Color(7, 15, 22, 226), new Color(182, 137, 71));
        _ui.Text("OBJECTIVE", new Vector2(panel.X + 18, panel.Y + 14), 13,
            new Color(239, 196, 111));
        _ui.TextFit(state.ObjectiveTitle, new Vector2(panel.X + 18, panel.Y + 36), 324f, 20, Color.White);
        _ui.TextFit(state.ObjectiveDirections, new Vector2(panel.X + 18, panel.Y + 64), 324f, 15,
            new Color(206, 220, 212));
        if (state.ObjectiveBearing.Length > 0)
            _ui.TextFit(state.ObjectiveBearing, new Vector2(panel.X + 18, panel.Y + 88), 324f, 15,
                new Color(232, 194, 116));
    }

    /// <summary>Health, prana, and stamina bars in the bottom-left HUD panel.</summary>
    public void DrawVitals(WorldHudState state)
    {
        if (!state.HasSession) return;

        var panel = new Rectangle(24, UiLayout.Height - 164, 344, 140);
        _ui.Panel(panel, new Color(6, 13, 20, 232), new Color(78, 128, 148));
        var barX = panel.X + 18;
        var barWidth = panel.Width - 36;
        DrawVitalBar(new Rectangle(barX, panel.Y + 20, barWidth, 26), "HEALTH", state.Health,
            new Color(198, 68, 74));
        DrawVitalBar(new Rectangle(barX, panel.Y + 58, barWidth, 26), "PRANA", state.Prana,
            new Color(74, 134, 216));
        DrawVitalBar(new Rectangle(barX, panel.Y + 96, barWidth, 26), "STAMINA", state.Stamina,
            new Color(98, 172, 106));
    }

    /// <summary>
    /// The readied spell, its cost, and the stones socketed beside it.
    ///
    /// Spells were bound to keys but never shown, so testers reported them as unimplemented.
    /// Socketed stones sit here rather than only on the character screen because a stone that
    /// changes a swing for the rest of a descent has to be knowable without stopping.
    /// </summary>
    public void DrawSpellBar(WorldHudState state, Texture2D? crystal)
    {
        if (!state.Spell.HasSpell) return;

        var spell = state.Spell;
        var panel = new Rectangle(UiLayout.Width / 2 - 150, UiLayout.Height - 96, 300, 60);
        _ui.Panel(panel, new Color(6, 13, 20, 214), new Color(74, 106, 132));

        _ui.Text("READIED", new Vector2(panel.X + 14, panel.Y + 9), 12, new Color(146, 174, 178));
        _ui.TextFit(spell.Name, new Vector2(panel.X + 14, panel.Y + 28), 176f, 19,
            spell.Affordable ? Color.White : new Color(198, 132, 126));

        _ui.TextRight($"{spell.Cost:0} prana", panel.Right - 14, panel.Y + 9, 13,
            spell.Affordable ? new Color(150, 190, 232) : new Color(216, 128, 120));
        _ui.TextRight(spell.Affordable ? "Q to cast" : "no charge", panel.Right - 14, panel.Y + 30, 13,
            new Color(146, 174, 178));

        if (spell.LightActive)
            _ui.TextCentred($"Emberlight {spell.LightRemaining:0}s",
                UiLayout.Width / 2f, panel.Y - 24f, 13, new Color(232, 194, 116));

        if (crystal is null || spell.Stones.Count == 0) return;

        const int cellSize = 34;
        var width = spell.Stones.Count * (cellSize + 4) + 12;
        var stones = new Rectangle(panel.Right + 12, panel.Y + 8, width, cellSize + 16);
        _ui.Panel(stones, new Color(14, 8, 22, 214), new Color(122, 88, 168));

        for (var index = 0; index < spell.Stones.Count; index++)
        {
            var cell = new Rectangle(stones.X + 8 + index * (cellSize + 4), stones.Y + 8,
                cellSize, cellSize);
            _ui.Sprite(crystal, cell, Color.White);
            _ui.TextCentred(spell.Stones[index].ShortName, cell.Center.X, cell.Bottom - 2f, 10,
                new Color(214, 184, 244));
        }
    }

    private void DrawVitalBar(Rectangle bounds, string label, VitalBarState value, Color colour)
    {
        var fraction = value.Max <= 0f ? 0f : MathHelper.Clamp(value.Value / value.Max, 0f, 1f);
        _ui.Fill(bounds, new Color(20, 27, 33));
        _ui.Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * fraction), bounds.Height), colour);
        if (value.Pulse > 0f)
        {
            _ui.Fill(new Rectangle(bounds.X, bounds.Y, (int)(bounds.Width * fraction), bounds.Height),
                new Color(255, 255, 255) * (value.Pulse * 0.42f));
            _ui.Border(bounds, new Color(226, 240, 255) * value.Pulse);
            _ui.Border(new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4),
                new Color(226, 240, 255) * (value.Pulse * 0.7f));
        }
        else
        {
            _ui.Border(bounds, new Color(0, 0, 0, 110));
        }
        _ui.Text(label, new Vector2(bounds.X + 10, bounds.Y + 5), 14, Color.White);
        var readout = value.Pulse > 0f
            ? Color.Lerp(Color.White, new Color(198, 232, 255), value.Pulse)
            : Color.White;
        _ui.TextRight($"{value.Value:0} / {value.Max:0}", bounds.Right - 10, bounds.Y + 5,
            value.Pulse > 0f ? 16 : 14, readout);
    }

    private void DrawSneakEye(int cx, int cy, Color colour)
    {
        var shadow = new Color(0, 0, 0, 190);
        _ui.Fill(new Rectangle(cx - 17, cy - 9, 34, 3), shadow);
        _ui.Fill(new Rectangle(cx - 17, cy + 6, 34, 3), shadow);
        _ui.Fill(new Rectangle(cx - 13, cy - 6, 6, 3), shadow);
        _ui.Fill(new Rectangle(cx + 7, cy - 6, 6, 3), shadow);
        _ui.Fill(new Rectangle(cx - 13, cy + 3, 6, 3), shadow);
        _ui.Fill(new Rectangle(cx + 7, cy + 3, 6, 3), shadow);
        _ui.Fill(new Rectangle(cx - 14, cy - 7, 28, 2), colour);
        _ui.Fill(new Rectangle(cx - 14, cy + 5, 28, 2), colour);
        _ui.Fill(new Rectangle(cx - 10, cy - 5, 5, 2), colour);
        _ui.Fill(new Rectangle(cx + 5, cy - 5, 5, 2), colour);
        _ui.Fill(new Rectangle(cx - 10, cy + 3, 5, 2), colour);
        _ui.Fill(new Rectangle(cx + 5, cy + 3, 5, 2), colour);
        _ui.Fill(new Rectangle(cx - 4, cy - 4, 8, 8), colour);
        _ui.Fill(new Rectangle(cx - 1, cy - 1, 2, 2), new Color(20, 26, 27));
    }
}
