namespace RatnaBay.Client.Input;

/// <summary>What Escape means, given what currently owns the screen.</summary>
internal enum EscapeAction
{
    /// <summary>Nothing was open and nothing is running. Swallow it.</summary>
    None,

    /// <summary>Close the settings panel over the main menu.</summary>
    CloseSettings,

    /// <summary>Come back out of pause into the run.</summary>
    Resume,

    /// <summary>Step back from the shaft without buying a descent.</summary>
    LeaveShaft,

    /// <summary>Done reading the summary; go back up to the yard.</summary>
    ReturnToSurface,

    /// <summary>Shut whatever panel is over the world.</summary>
    ClosePanels,

    /// <summary>Nothing is open, so Escape pauses.</summary>
    Pause
}

/// <summary>
/// One key, unwinding one layer at a time.
///
/// Escape has to mean the least destructive thing available, and which thing that is depends
/// entirely on what is stacked over the world. Getting the order wrong is not a cosmetic bug:
/// this ladder used to end by dropping straight to the main menu, so the one key everybody
/// presses to pause was the one key that silently threw away a descent in progress.
///
/// Written as a decision with no side effects so the order can be read in one place and,
/// unlike the version that lived inside a frame update, argued about without tracing what each
/// branch did on the way past.
/// </summary>
internal static class EscapeLadder
{
    /// <summary>
    /// The topmost thing Escape should unwind.
    /// </summary>
    /// <param name="onMainMenu">The world is not up; only settings can be over the menu.</param>
    /// <param name="settingsOpen">Settings is showing.</param>
    /// <param name="paused">The pause screen has the run.</param>
    /// <param name="atShaft">The depth choice is up.</param>
    /// <param name="summaryShowing">A run has ended and its summary is being read.</param>
    /// <param name="anyPanelOpen">Anything else is over the world.</param>
    public static EscapeAction Read(
        bool onMainMenu,
        bool settingsOpen,
        bool paused,
        bool atShaft,
        bool summaryShowing,
        bool anyPanelOpen)
    {
        if (onMainMenu) return settingsOpen ? EscapeAction.CloseSettings : EscapeAction.None;

        if (paused) return EscapeAction.Resume;
        if (atShaft) return EscapeAction.LeaveShaft;
        if (summaryShowing) return EscapeAction.ReturnToSurface;
        if (anyPanelOpen) return EscapeAction.ClosePanels;

        // Nothing is open, so Escape pauses rather than leaving. Never a route to the menu:
        // that is what the pause screen's own entries are for, where the consequence is
        // spelled out and a descent can be set aside instead of lost.
        return EscapeAction.Pause;
    }
}
