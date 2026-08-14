using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A playtest movement multiplier, cycled with <b>F3</b>.
///
/// Crossing a 1.6 km city to check one building takes five minutes at the authored speed, and
/// most of a playtest is spent walking back to the thing you wanted to look at again. This
/// exists so testing the game is not gated on travelling through it.
///
/// **It is a cheat and announces itself.** Every change toasts the new multiplier, so a session
/// cannot quietly be played at eight times speed and then reported as if it were normal — that
/// would make pacing feedback worthless, and pacing feedback is the main reason to playtest.
///
/// Resets to 1 on load, so it can never be saved into a build's default behaviour.
/// </summary>
public static class DebugSpeed
{
    private static readonly float[] Steps = { 1f, 3f, 8f };
    private static int _step;

    public static float Multiplier => Steps[_step];
    public static bool IsCheating => _step != 0;

    /// <summary>Cycles to the next multiplier and returns it.</summary>
    public static float Cycle()
    {
        _step = (_step + 1) % Steps.Length;
        return Multiplier;
    }

    public static void Reset() => _step = 0;

    /// <summary>
    /// Polls the toggle key. Read directly from the keyboard rather than through an input
    /// action so that adding a debug affordance does not mean editing the shipped
    /// `.inputactions` asset — this is scaffolding, and it should be removable in one file.
    /// </summary>
    public static bool ToggleRequested()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.f3Key.wasPressedThisFrame;
    }
}
