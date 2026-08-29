using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Linq;

namespace RatnaBay.Client.Ui;

/// <summary>
/// Builds the world HUD snapshot from the live session.
///
/// Lives beside <see cref="WorldHudState"/> so HudRenderer never has to know about save
/// files or the coordinator.
/// </summary>
internal static class WorldHudBuilder
{
    public static WorldHudState Build(
        GameSession? session,
        Encounter? encounter,
        RunResult? summary,
        ScreenStack stack,
        Coach coach,
        bool capturing,
        float healthPulse,
        float pranaPulse,
        float framesPerSecond,
        string locationCaption)
    {
        var detection = session?.Player.Detection;
        var objective = session?.Player.Objective;
        var vitals = session?.Player.Vitals;
        var feedback = encounter?.Feedback;

        var health = vitals is null
            ? default
            : new VitalBarState(vitals.Health, vitals.MaxHealth, healthPulse);
        var prana = vitals is null
            ? default
            : new VitalBarState(vitals.Prana, vitals.MaxPrana, pranaPulse);
        var stamina = vitals is null
            ? default
            : new VitalBarState(vitals.Stamina, vitals.MaxStamina);

        var activeObjective = objective is { HasObjective: true } objectiveValue
            ? objectiveValue
            : null;
        var objectiveTitle = activeObjective?.Title;
        var objectiveDirections = activeObjective?.Directions ?? string.Empty;
        var objectiveBearing = activeObjective is null || session is null
            ? string.Empty
            : activeObjective.BearingLine(session.Position);

        return new WorldHudState(
            HasSession: session is not null,
            IsCrouching: detection?.IsCrouching == true,
            Awareness: detection?.Awareness ?? AwarenessLevel.Unaware,
            Suspicion: detection?.Suspicion ?? 0f,
            DamageFlash: encounter is { DamageFlash: > 0f } flashing
                ? flashing.DamageFlash / Encounter.DamageFlashSeconds
                : 0f,
            HitMarker: feedback?.HitMarker ?? 0f,
            KillMarker: feedback?.KillMarker ?? 0f,
            DamageDirections: feedback?.Directions.ToArray() ?? Array.Empty<DamageDirection>(),
            CastBanner: feedback?.CastBanner ?? 0f,
            CastTint: feedback?.CastTint ?? Color.Transparent,
            CastColour: feedback?.CastColour ?? Color.White,
            CastLine: feedback?.CastLine ?? string.Empty,
            LocationCaption: locationCaption,
            ObjectiveTitle: objectiveTitle,
            ObjectiveDirections: objectiveDirections,
            ObjectiveBearing: objectiveBearing,
            Health: health,
            Prana: prana,
            Stamina: stamina,
            Toasts: session?.Toasts.Select(toast => new ToastHud(toast.Message, toast.Remaining)).ToArray()
                ?? Array.Empty<ToastHud>(),
            Level: vitals?.Level ?? 0,
            Gold: vitals?.Gold ?? 0,
            WeaponName: session?.Player.Combat.ActiveWeapon.DisplayName ?? string.Empty,
            IsBlocking: session?.Player.Combat.IsBlocking == true,
            FramesPerSecond: framesPerSecond,
            ShowFrameRate: !capturing,
            Spell: SpellOf(session),
            CoachLine: summary is not null || stack.Shaft || stack.CampTrader
                ? string.Empty
                : coach.Line,
            CoachOpacity: coach.Opacity);
    }

    private static SpellHudState SpellOf(GameSession? session)
    {
        if (session is null) return new SpellHudState(false, string.Empty, 0f, false, false, 0f,
            Array.Empty<SocketHud>());

        var caster = session.Player.Spells;
        var spell = SpellCatalog.Get(caster.SelectedSpellId);
        if (spell is null) return new SpellHudState(false, string.Empty, 0f, false, false, 0f,
            Array.Empty<SocketHud>());

        var cost = caster.CostOf(spell);
        var stones = session.Player.Stones.Socketed
            .Select(StoneCatalog.Find)
            .Where(stone => stone is not null)
            .Select(stone => new SocketHud(ShortNameOf(stone!)))
            .ToArray();

        return new SpellHudState(
            HasSpell: true,
            Name: spell.DisplayName,
            Cost: cost,
            Affordable: session.Player.Vitals.Prana >= cost
                || session.Player.Inventory.Has(SoulCrystals.LesserId),
            LightActive: caster.LightActive,
            LightRemaining: caster.LightRemaining,
            Stones: stones);
    }

    /// <summary>"Cinder Stone" is too long under a 34-pixel icon; "Cinder" is not.</summary>
    private static string ShortNameOf(StoneDefinition stone)
    {
        var space = stone.DisplayName.IndexOf(' ');
        return space > 0 ? stone.DisplayName[..space] : stone.DisplayName;
    }
}
