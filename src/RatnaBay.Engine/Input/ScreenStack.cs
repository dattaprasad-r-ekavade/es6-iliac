using Microsoft.Xna.Framework.Input;

namespace RatnaBay.Engine.Input;

/// <summary>A world-scene overlay that swallows the rest of the frame.</summary>
public enum WorldHold
{
    None,
    Pause,
    Summary,
    CampTrader,
    Shaft,
    Fort,
    Character,
    Settings
}

/// <summary>
/// Which world panels are open, and which one owns the frame.
///
/// Game1 still owns what a confirmed row does, the run-summary payload, and conversation
/// text. This type must not take a <c>Game1</c> reference.
/// </summary>
public sealed class ScreenStack
{
    public bool Paused { get; set; }
    public bool Help { get; set; }
    public bool Fort { get; set; }
    public bool Character { get; set; }
    public bool Journal { get; set; }
    public bool Shop { get; set; }
    public bool Dialogue { get; set; }
    public bool Shaft { get; set; }
    public bool CampTrader { get; set; }
    public string? FortRoom { get; set; }

    /// <summary>
    /// True while any screen is holding the pointer.
    ///
    /// One list, and everything that frees the mouse must be on it. The shaft panel was not,
    /// so the frame after it opened the camera took the pointer straight back.
    /// </summary>
    public bool AnyOpen(OverlayInput overlay, bool hasSummary) =>
        Dialogue || Shop || Journal || Character || Help || Fort
        || overlay.ShowSettings || Paused || Shaft || CampTrader || hasSummary;

    /// <summary>Panels that cover the combat HUD. Pause, shaft and camp do not.</summary>
    public bool HidesHud => Help || Journal || Character || Shop;

    /// <summary>Pause, summary, camp, shaft — before F/J/I toggles.</summary>
    public WorldHold EarlyHold(bool hasSummary)
    {
        if (Paused) return WorldHold.Pause;
        if (hasSummary) return WorldHold.Summary;
        if (CampTrader) return WorldHold.CampTrader;
        if (Shaft) return WorldHold.Shaft;
        return WorldHold.None;
    }

    /// <summary>Fort, character, settings — after the toggle keys have run.</summary>
    public WorldHold LateHold(OverlayInput overlay)
    {
        if (Fort) return WorldHold.Fort;
        if (Character) return WorldHold.Character;
        if (overlay.ShowSettings) return WorldHold.Settings;
        return WorldHold.None;
    }

    /// <summary>
    /// Close the dismissable stack. Pause, shaft, camp and the run summary are not this:
    /// they have their own exits.
    /// </summary>
    public void Close(OverlayInput overlay)
    {
        Dialogue = false;
        Shop = false;
        Journal = false;
        Character = false;
        Help = false;
        overlay.ShowSettings = false;
        Fort = false;
        FortRoom = null;
    }

    public void OpenFort()
    {
        Fort = true;
        Journal = false;
        Character = false;
        FortRoom = null;
    }

    public void OpenJournal()
    {
        Journal = true;
        Character = false;
    }

    public void OpenCharacter()
    {
        Character = true;
        Journal = false;
    }

    public bool ClickClosesHelp(InputRouter input, MouseState mouse) =>
        Help && input.Clicked(mouse);
}
