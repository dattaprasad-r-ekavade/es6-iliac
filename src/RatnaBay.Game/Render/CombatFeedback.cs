using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>A number that floats up from where a blow landed and fades.</summary>
public sealed class FloatingNumber
{
    public required string Text { get; init; }
    public required Color Colour { get; init; }
    public WorldPoint Origin { get; init; }
    public float Remaining { get; set; }
    public float Duration { get; init; }

    /// <summary>Screen-space drift, so two hits in the same spot do not stack illegibly.</summary>
    public float Drift { get; init; }

    public float Age => Duration <= 0f ? 1f : 1f - Remaining / Duration;
}

/// <summary>The direction a blow came from, for the incoming-damage arc.</summary>
public sealed class DamageDirection
{
    public float Bearing { get; init; }
    public float Remaining { get; set; }
    public float Duration { get; init; }
}

/// <summary>
/// The short-lived, purely visual half of a fight.
///
/// Playtesters could not tell whether a swing had connected or where a blow had come from.
/// None of this changes a rule — it reports rules that already ran — so it lives here rather
/// than in the domain, and it is all time-boxed state that expires on its own.
/// </summary>
public sealed class CombatFeedback
{
    private const float NumberSeconds = 1.1f;
    private const float MarkerSeconds = 0.32f;
    private const float DirectionSeconds = 1.35f;

    /// <summary>Cap so a crowded fight cannot bury the screen in numbers.</summary>
    private const int MaxNumbers = 14;

    /// <summary>How long the cast banner stays up.</summary>
    private const float CastSeconds = 1.6f;

    private readonly List<FloatingNumber> _numbers = new();
    private readonly List<DamageDirection> _directions = new();
    private float _hitMarker;
    private float _killMarker;
    private float _blockedMarker;
    private int _driftStep;

    public IReadOnlyList<FloatingNumber> Numbers => _numbers;

    /// <summary>What the last cast was and what it did. Empty when nothing is showing.</summary>
    public string CastLine { get; private set; } = string.Empty;

    public Color CastColour { get; private set; } = Color.White;

    /// <summary>0 to 1 while the cast banner is showing.</summary>
    public float CastBanner => _castRemaining <= 0f ? 0f : _castRemaining / CastSeconds;

    /// <summary>Element colour for the screen tint on a cast. Transparent when idle.</summary>
    public Color CastTint { get; private set; } = Color.Transparent;

    private float _castRemaining;
    public IReadOnlyList<DamageDirection> Directions => _directions;

    /// <summary>0 to 1 while the crosshair marker is showing.</summary>
    public float HitMarker => _hitMarker <= 0f ? 0f : _hitMarker / MarkerSeconds;

    public float KillMarker => _killMarker <= 0f ? 0f : _killMarker / MarkerSeconds;
    public float BlockedMarker => _blockedMarker <= 0f ? 0f : _blockedMarker / MarkerSeconds;

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        _hitMarker = MathF.Max(0f, _hitMarker - deltaSeconds);
        _castRemaining = MathF.Max(0f, _castRemaining - deltaSeconds);
        if (_castRemaining <= 0f) CastLine = string.Empty;
        _killMarker = MathF.Max(0f, _killMarker - deltaSeconds);
        _blockedMarker = MathF.Max(0f, _blockedMarker - deltaSeconds);

        for (var index = _numbers.Count - 1; index >= 0; index--)
        {
            _numbers[index].Remaining -= deltaSeconds;
            if (_numbers[index].Remaining <= 0f) _numbers.RemoveAt(index);
        }

        for (var index = _directions.Count - 1; index >= 0; index--)
        {
            _directions[index].Remaining -= deltaSeconds;
            if (_directions[index].Remaining <= 0f) _directions.RemoveAt(index);
        }
    }

    /// <summary>The player landed a blow.</summary>
    public void PlayerHit(WorldPoint at, float damage, bool killed)
    {
        _hitMarker = MarkerSeconds;
        if (killed) _killMarker = MarkerSeconds;

        Add(new FloatingNumber
        {
            Text = $"{damage:0}",
            Colour = killed ? new Color(255, 226, 150) : new Color(255, 244, 232),
            Origin = at,
            Remaining = NumberSeconds,
            Duration = NumberSeconds,
            Drift = NextDrift()
        });
    }

    /// <summary>A status effect landed rather than a blow.</summary>
    public void PlayerEffect(WorldPoint at, string label, Color colour)
    {
        Add(new FloatingNumber
        {
            Text = label,
            Colour = colour,
            Origin = at,
            Remaining = NumberSeconds,
            Duration = NumberSeconds,
            Drift = NextDrift()
        });
    }

    /// <summary>
    /// The player took a blow. <paramref name="bearing"/> is where it came from relative to
    /// the way they are facing, so the arc can point at whatever hit them.
    /// </summary>
    public void PlayerHurt(float damage, float bearing, bool guarded)
    {
        if (guarded) _blockedMarker = MarkerSeconds;

        _directions.Add(new DamageDirection
        {
            Bearing = bearing,
            Remaining = DirectionSeconds,
            Duration = DirectionSeconds
        });

        Add(new FloatingNumber
        {
            Text = guarded ? $"-{damage:0} blocked" : $"-{damage:0}",
            Colour = guarded ? new Color(196, 214, 226) : new Color(240, 128, 118),
            Origin = default,
            Remaining = NumberSeconds,
            Duration = NumberSeconds,

            // Player damage is drawn at a fixed screen spot, so no drift is wanted.
            Drift = 0f
        });
    }

    /// <summary>
    /// A spell was cast: what it was, and what it found.
    ///
    /// Testers could see the arm move but could not tell whether anything had been cast,
    /// what it was, or whether it had hit. This is the sentence that answers all three.
    /// </summary>
    public void Cast(string spellName, string outcome, Color colour)
    {
        CastLine = $"{spellName} — {outcome}";
        CastColour = colour;
        CastTint = colour;
        _castRemaining = CastSeconds;
    }

    /// <summary>True for numbers that belong over the player rather than over a target.</summary>
    public static bool IsSelfInflicted(FloatingNumber number) => number.Origin == default;

    public void Clear()
    {
        _numbers.Clear();
        _directions.Clear();
        _hitMarker = 0f;
        _killMarker = 0f;
        _blockedMarker = 0f;
        _castRemaining = 0f;
        CastLine = string.Empty;
    }

    private void Add(FloatingNumber number)
    {
        _numbers.Add(number);
        if (_numbers.Count > MaxNumbers) _numbers.RemoveRange(0, _numbers.Count - MaxNumbers);
    }

    /// <summary>Alternating sideways offset so repeated hits do not overlap exactly.</summary>
    private float NextDrift()
    {
        _driftStep = (_driftStep + 1) % 4;
        return (_driftStep - 1.5f) * 22f;
    }
}
