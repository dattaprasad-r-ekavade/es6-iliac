using Microsoft.Xna.Framework.Input;

namespace RatnaBay.Client.Input;

/// <summary>Which panel a key asked for this frame.</summary>
public enum PanelRequest
{
    None,
    Help,
    Fort,
    Journal,
    Character,
    Settings,

    /// <summary>Tab: hand the pointer back, or take it again.</summary>
    ToggleMouseLook
}

/// <summary>
/// The keys that open and close the panels over the world.
///
/// Seven bindings that were seven near-identical blocks in the middle of a 180-line
/// <c>UpdateGameScreen</c>: press the key, close it if it is open, otherwise open it and give
/// the pointer to the panel. Reading them was harder than it should have been, because the
/// shape they share was spelled out seven times rather than once.
///
/// Follows the pattern the other input types set: this decides what was *asked for* and
/// nothing else. Game1 still owns what a panel does when it opens, and this type must not take
/// a <c>Game1</c> reference — that rule is what stops the input layer growing back into the
/// coordinator it was cut out of.
/// </summary>
public static class PanelKeys
{
    /// <summary>
    /// The controls overlay, which is reachable from anywhere.
    ///
    /// Read before the early holds on purpose, and the reason is written down in Game1: F1
    /// used to fire on the frame between pause returning and the summary taking the screen.
    /// Help is the one panel a player may need while another has the screen.
    /// </summary>
    public static PanelRequest ReadHelp(InputRouter input, KeyboardState keyboard) =>
        input.Pressed(keyboard, Keys.F1) ? PanelRequest.Help : PanelRequest.None;

    /// <summary>
    /// The rest, which a panel already holding the screen must be able to swallow.
    ///
    /// Read *after* the early holds, and that ordering is load-bearing: the shaft panel, the
    /// camp trader and the run summary each own the screen while they are up, and journal or
    /// inventory opening on top of the depth choice is a stack of two panels neither of which
    /// expected the other. Collapsing these into one read at the top of the frame quietly
    /// removed that guard — which is exactly what happened when this type was first written.
    ///
    /// <paramref name="onTheSurface"/> gates the fort: it is what a run is *for*, not something
    /// to read halfway down a shaft with a door waiting.
    /// </summary>
    public static PanelRequest Read(InputRouter input, KeyboardState keyboard, bool onTheSurface)
    {
        if (input.Pressed(keyboard, Keys.Tab)) return PanelRequest.ToggleMouseLook;
        if (onTheSurface && input.Pressed(keyboard, Keys.F)) return PanelRequest.Fort;
        if (input.Pressed(keyboard, Keys.J)) return PanelRequest.Journal;

        // I and K both, because the sheet is "inventory" to one player and "kit" to another,
        // and neither should have to find that out by pressing every key.
        if (input.Pressed(keyboard, Keys.I) || input.Pressed(keyboard, Keys.K))
            return PanelRequest.Character;

        if (input.Pressed(keyboard, Keys.F2)) return PanelRequest.Settings;

        return PanelRequest.None;
    }
}
