using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System.Collections.Generic;

namespace RatnaBay.Client.Ui;

internal readonly record struct VitalBarState(float Value, float Max, float Pulse = 0f);

internal readonly record struct ToastHud(string Message, float Remaining);

internal readonly record struct SocketHud(string ShortName);

internal sealed record SpellHudState(
    bool HasSpell,
    string Name,
    float Cost,
    bool Affordable,
    bool LightActive,
    float LightRemaining,
    IReadOnlyList<SocketHud> Stones);

/// <summary>
/// Everything the in-world HUD is allowed to know.
///
/// Built by Game1 once per frame from the live session. HudRenderer must not open the save
/// file, the quest service, or the rest of the coordinator — if a value is missing here, add
/// it to the snapshot rather than reaching through.
/// </summary>
internal sealed record WorldHudState(
    bool HasSession,
    bool IsCrouching,
    AwarenessLevel Awareness,
    float Suspicion,
    float DamageFlash,
    float HitMarker,
    float KillMarker,
    IReadOnlyList<DamageDirection> DamageDirections,
    float CastBanner,
    Color CastTint,
    Color CastColour,
    string CastLine,
    string LocationCaption,
    string? ObjectiveTitle,
    string ObjectiveDirections,
    string ObjectiveBearing,
    VitalBarState Health,
    VitalBarState Prana,
    VitalBarState Stamina,
    IReadOnlyList<ToastHud> Toasts,
    int Level,
    int Gold,
    string WeaponName,
    bool IsBlocking,
    float FramesPerSecond,
    bool ShowFrameRate,
    SpellHudState Spell,
    string CoachLine,
    float CoachOpacity);
