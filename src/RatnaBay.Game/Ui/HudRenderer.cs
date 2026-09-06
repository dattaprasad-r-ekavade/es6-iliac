using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client.Ui;

/// <summary>
/// The small, state-driven part of the in-world HUD.
///
/// Layout and presentation only. Game1 builds a <see cref="WorldHudState"/> snapshot and
/// coordinates draw order with the weapon, nameplates and world prompts. The teaching
/// line lives here too: it is HUD, not a panel.
/// </summary>
internal sealed class HudRenderer
{
    private readonly UiCanvas _ui;

    public HudRenderer(UiCanvas ui) => _ui = ui;

    /// <summary>Domain events, rendered above the vitals. Newest last, fading as they expire.</summary>
    public void DrawToasts(WorldHudState state)
    {
        var first = Math.Max(0, state.Toasts.Count - 3);
        for (var index = first; index < state.Toasts.Count; index++)
        {
            var toast = state.Toasts[index];
            var row = UiLayout.HudToast(index - first);
            var fade = MathHelper.Clamp(toast.Remaining, 0f, 1f);
            _ui.Fill(row, UiTheme.PanelSheer * fade);
            _ui.TextFitCentred(toast.Message, row.Center.X, row.Y + 4, row.Width - 24, 15, UiTheme.Body * fade);
        }
    }

    public void DrawStatusStrip(WorldHudState state)
    {
        if (!state.HasSession) return;
        var panel = UiLayout.HudStatus;
        _ui.Panel(panel, UiTheme.PanelSheer, UiTheme.BorderDim);
        _ui.Text($"LEVEL {state.Level}", new Vector2(panel.X + 14, panel.Y + 10), 14, UiTheme.Muted);
        _ui.TextRight($"{state.Gold} gold", panel.Right - 14, panel.Y + 10, 16, UiTheme.GoldBright);
        _ui.TextFit(state.WeaponName, new Vector2(panel.X + 14, panel.Y + 36), 154, 15,
            state.IsBlocking ? UiTheme.Gold : UiTheme.Body);
        _ui.TextRight(state.IsBlocking ? "GUARD" : "I / Pack", panel.Right - 14, panel.Y + 37, 13, UiTheme.Hint);
        if (state.ShowFrameRate && state.FramesPerSecond > 0)
            _ui.TextRight($"{state.FramesPerSecond:0} fps", panel.Right, panel.Y - 22, 12, UiTheme.Muted);
    }

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

    public void DrawLocationBanner(WorldHudState state)
    {
        var panel = UiLayout.HudLocation;
        _ui.Fill(panel, UiTheme.PanelSheer);
        _ui.Fill(new Rectangle(panel.X + 72, panel.Bottom - 1, panel.Width - 144, 1), UiTheme.Rule);
        _ui.TextFitCentred(state.LocationCaption, panel.Center.X, panel.Y + 8, panel.Width - 24, 14, UiTheme.Heading);
    }

    public void DrawAwareness(WorldHudState state)
    {
        if (!state.HasSession) return;

        var panel = new Rectangle(UiLayout.Width - 264, 24, 240, 48);
        var colour = state.Awareness switch
        {
            AwarenessLevel.Alerted => new Color(188, 65, 68),
            AwarenessLevel.Suspicious => UiTheme.Bronze,
            _ => UiTheme.BorderDim
        };
        _ui.Panel(panel, UiTheme.PanelSheer, colour);
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

        var panel = UiLayout.HudObjective;
        _ui.Panel(panel, UiTheme.PanelSheer, UiTheme.Bronze);
        _ui.Text("OBJECTIVE", new Vector2(panel.X + 18, panel.Y + 14), 13,
            new Color(239, 196, 111));
        _ui.TextFit(state.ObjectiveTitle, new Vector2(panel.X + 18, panel.Y + 36), 284f, 20, Color.White);
        _ui.TextFit(state.ObjectiveDirections, new Vector2(panel.X + 18, panel.Y + 64), 284f, 15,
            UiTheme.Body);
        if (state.ObjectiveBearing.Length > 0)
            _ui.TextFit(state.ObjectiveBearing, new Vector2(panel.X + 18, panel.Y + 88), 284f, 15,
                UiTheme.Gold);
    }

    /// <summary>Health, prana, and stamina bars in the bottom-left HUD panel.</summary>
    public void DrawVitals(WorldHudState state)
    {
        if (!state.HasSession) return;
        var panel = UiLayout.HudVitals;
        _ui.Panel(panel, UiTheme.PanelSheer, UiTheme.BorderDim);
        DrawVitalBar(new Rectangle(panel.X + 14, panel.Y + 11, panel.Width - 28, 22), "HEALTH", state.Health, UiTheme.Health);
        DrawVitalBar(new Rectangle(panel.X + 14, panel.Y + 40, panel.Width - 28, 22), "PRANA", state.Prana, UiTheme.Prana);
        DrawVitalBar(new Rectangle(panel.X + 14, panel.Y + 69, panel.Width - 28, 22), "STAMINA", state.Stamina, UiTheme.Stamina);
    }

    public void DrawSpellBar(WorldHudState state, Texture2D? crystal)
    {
        if (!state.Spell.HasSpell) return;
        var spell = state.Spell;
        var panel = UiLayout.HudSpell;
        _ui.Panel(panel, UiTheme.PanelSheer, UiTheme.BorderDim);
        _ui.Text("Q / CAST", new Vector2(panel.X + 16, panel.Y + 10), 13, UiTheme.Accent);
        _ui.TextRight($"{spell.Cost:0} prana", panel.Right - 16, panel.Y + 10, 14,
            spell.Affordable ? UiTheme.Prana : UiTheme.Warning);
        _ui.TextFit(spell.Name, new Vector2(panel.X + 16, panel.Y + 31), panel.Width - 120, 22,
            spell.Affordable ? UiTheme.Heading : UiTheme.Warning);
        if (!spell.Affordable) _ui.TextRight("No charge", panel.Right - 16, panel.Y + 37, 13, UiTheme.Warning);
        if (spell.LightActive)
            _ui.TextCentred($"Emberlight {spell.LightRemaining:0}s", panel.Center.X, panel.Y - 24, 14, UiTheme.Gold);
        if (crystal is null || spell.Stones.Count == 0) return;
        var slots = UiLayout.HudSockets;
        var step = Math.Min(46, slots.Width / spell.Stones.Count);
        for (var index = 0; index < spell.Stones.Count; index++)
        {
            var cell = new Rectangle(slots.X + index * step, slots.Y, step - 3, 38);
            _ui.Panel(cell, UiTheme.PanelSheer, UiTheme.BorderDim);
            _ui.Sprite(crystal, new Rectangle(cell.Center.X - 10, cell.Y + 2, 20, 20), Color.White);
            _ui.TextFitCentred(spell.Stones[index].ShortName, cell.Center.X, cell.Y + 23, cell.Width - 4, 10, UiTheme.Accent);
        }
    }

    private void DrawVitalBar(Rectangle bounds, string label, VitalBarState value, Color colour)
    {
        var fraction = value.Max <= 0 ? 0 : MathHelper.Clamp(value.Value / value.Max, 0, 1);
        _ui.Text(label, new Vector2(bounds.X, bounds.Y - 1), 11, UiTheme.Muted);
        _ui.TextRight($"{value.Value:0} / {value.Max:0}", bounds.Right, bounds.Y - 1, 12, UiTheme.Heading);
        var track = new Rectangle(bounds.X, bounds.Y + 15, bounds.Width, 6);
        _ui.Fill(track, UiTheme.Track);
        var fill = new Rectangle(track.X, track.Y, (int)(track.Width * fraction), track.Height);
        _ui.Fill(fill, colour);
        _ui.Fill(new Rectangle(fill.X, fill.Y, fill.Width, 1), UiTheme.Heading * 0.3f);
        if (value.Pulse > 0) _ui.Fill(fill, UiTheme.Heading * (value.Pulse * 0.5f));
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

    /// <summary>
    /// The teaching line, under the location banner.
    ///
    /// Up near the top rather than over the vitals: it must be readable without pulling the
    /// eye off the middle of the screen, and it must never sit where a fight is happening. It
    /// fades rather than snapping, and it never blocks anything — a player who ignores it
    /// entirely loses nothing but the explanation.
    /// </summary>
    public void DrawCoach(WorldHudState state)
    {
        if (state.CoachLine.Length == 0 || state.CoachOpacity <= 0.02f) return;

        var fade = state.CoachOpacity;
        var panel = UiLayout.CoachPanel;

        _ui.Fill(panel, UiTheme.PanelRaised * (fade * 0.86f));
        _ui.Border(panel, UiTheme.Accent * (fade * 0.7f));
        _ui.TextFitCentred(state.CoachLine, panel.Center.X, panel.Y + 17f, panel.Width - 40f, 15,
            UiTheme.Heading * fade);
    }
}
