using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client;

/// <summary>Where and how the held weapon should be drawn this frame.</summary>
public readonly record struct WeaponPose(Vector2 Position, float Rotation, float Scale);

/// <summary>
/// The weapon in hand, and the arm behind it.
///
/// Daggerfall's model: the weapon is a sprite at the edge of the screen that arcs across when
/// you swing. There is no rig and no animation clip — a swing is a curve over time applied to
/// one sprite's position and rotation, so adding a new weapon costs nothing in animation.
///
/// The swing is deliberately a little longer than the weapon's cooldown so a fast weapon
/// still reads as a complete motion rather than a twitch.
/// </summary>
public sealed class WeaponView
{
    /// <summary>
    /// Rest pose, in logical UI pixels, anchored at the grip.
    ///
    /// Below the bottom of the screen on purpose: the hand is off-frame and the blade rises
    /// into view, which is what stops it reading as a sword floating in the corner.
    /// </summary>
    private static readonly Vector2 RestPosition = new(1036f, 784f);

    /// <summary>The sprite is authored small; this is the size it is actually held at.</summary>
    private const float BaseScale = 1.5f;

    private const float RestRotation = -0.34f;

    /// <summary>
    /// A swing rotates around the grip. Translating the whole sprite makes the hilt leave the
    /// player's hand and makes the motion read like a flying sword rather than a hand swing.
    /// </summary>
    private static readonly Vector2 SwingTravel = Vector2.Zero;

    /// <summary>
    /// Radians the blade rotates through toward the centered target. The negative sign matches
    /// the sprite's top-to-bottom blade orientation; a positive arc sweeps the tip away from
    /// the target even though the grip remains correctly anchored.
    /// </summary>
    private const float SwingArc = -0.92f;

    /// <summary>Guarding lifts the weapon across the body.</summary>
    private static readonly Vector2 GuardOffset = new(-150f, -78f);

    private const float GuardRotation = -1.28f;
    private const float GuardBlend = 9f;

    /// <summary>How far the weapon sways while walking.</summary>
    private static readonly Vector2 BobAmount = new(14f, 11f);

    private float _swingRemaining;
    private float _swingDuration;
    private float _castRemaining;
    private float _castDuration;
    private float _bobPhase;
    private float _guard;

    /// <summary>True while a swing is still playing out.</summary>
    public bool IsSwinging => _swingRemaining > 0f;

    /// <summary>True while a cast is playing out.</summary>
    public bool IsCasting => _castRemaining > 0f;

    /// <summary>
    /// Casting is a different motion from swinging.
    ///
    /// A swing sweeps the blade across the body; a cast pulls the hand back and pushes it
    /// forward, so the two read as different actions rather than the same one relabelled.
    /// </summary>
    public void Cast()
    {
        _castDuration = 0.46f;
        _castRemaining = _castDuration;

        // A cast interrupts a swing rather than layering on top of it.
        _swingRemaining = 0f;
    }

    /// <summary>Start a swing. Its length follows the weapon, so a greatsword feels heavier.</summary>
    public void Swing(WeaponDefinition weapon)
    {
        _swingDuration = MathF.Max(0.28f, weapon.Cooldown * 1.15f);
        _swingRemaining = _swingDuration;
    }

    public void Update(float deltaSeconds, bool moving, bool guarding)
    {
        if (deltaSeconds <= 0f) return;

        if (_swingRemaining > 0f) _swingRemaining = MathF.Max(0f, _swingRemaining - deltaSeconds);
        if (_castRemaining > 0f) _castRemaining = MathF.Max(0f, _castRemaining - deltaSeconds);

        // The bob only advances while actually walking, so standing still is still.
        if (moving) _bobPhase += deltaSeconds * 7.4f;

        // Eased rather than snapped, so raising the guard is a movement and not a jump cut.
        var target = guarding ? 1f : 0f;
        _guard += (target - _guard) * MathF.Min(1f, deltaSeconds * GuardBlend);
    }

    public WeaponPose Pose()
    {
        var position = RestPosition;
        var rotation = RestRotation;
        var scale = BaseScale;

        // Walking sway. A figure-of-eight rather than a straight bounce.
        var bob = new Vector2(
            MathF.Sin(_bobPhase) * BobAmount.X,
            MathF.Abs(MathF.Cos(_bobPhase)) * BobAmount.Y);
        position += bob;

        // Guard.
        position += GuardOffset * _guard;
        rotation += GuardRotation * _guard;

        if (_castRemaining > 0f)
        {
            var castT = 1f - _castRemaining / _castDuration;

            // Draw back for the first third, then thrust forward and settle.
            var thrust = castT < 0.33f
                ? -EaseOut(castT / 0.33f) * 0.4f
                : (castT - 0.33f) / 0.67f;

            // Up and inward rather than across: the hand presents rather than sweeps.
            position += new Vector2(-96f, -150f) * thrust;
            rotation += 0.55f * thrust;

            // A pulse at the moment the spell leaves the hand.
            var flare = MathF.Sin(MathHelper.Clamp(castT, 0f, 1f) * MathF.PI);
            scale += BaseScale * 0.26f * flare;

            return new WeaponPose(position, rotation, scale);
        }

        if (_swingRemaining <= 0f) return new WeaponPose(position, rotation, scale);

        // 0 at the start of the swing, 1 at the end.
        var t = 1f - _swingRemaining / _swingDuration;

        // Fast on the way down, slower on the recovery — that asymmetry is most of what
        // makes a swing read as a strike rather than a wave.
        var strike = t < 0.35f
            ? EaseOut(t / 0.35f)
            : 1f - EaseIn((t - 0.35f) / 0.65f);

        // The grip is the SpriteBatch origin, so keeping the position fixed makes the blade
        // sweep around the hand while the hilt stays planted.
        position += SwingTravel * strike;
        rotation += SwingArc * strike;

        // A slight push toward the viewer at the moment of contact.
        scale += BaseScale * 0.14f * strike;

        return new WeaponPose(position, rotation, scale);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseIn(float t) => t * t;
}
