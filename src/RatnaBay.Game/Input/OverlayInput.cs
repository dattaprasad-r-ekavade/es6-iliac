using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace RatnaBay.Client;

/// <summary>What the settings panel asked the coordinator to do this frame.</summary>
internal enum SettingsAction
{
    None,
    ToggleDisplay,
    NudgeScale,
    NudgeVolume
}

internal readonly record struct SettingsCommand(SettingsAction Action, float Nudge = 0f)
{
    public static SettingsCommand Idle => new(SettingsAction.None);
}

/// <summary>
/// Selection, hover and confirm for the overlay stack: consent, title menu, pause, settings.
///
/// Game1 still owns what happens when a row is confirmed — starting a game, toggling
/// fullscreen, saving a descent. This type must not take a <c>Game1</c> reference; it
/// returns a chosen index or a <see cref="SettingsCommand"/> instead.
///
/// Inventory, shop, dialogue, the shaft and <c>UpdateGameScreen</c> stay on Game1 until
/// their own cuts. Do not fold those in here.
/// </summary>
internal sealed class OverlayInput
{
    public const int ConsentButtons = 2;

    /// <summary>
    /// Display, UI scale, volume, bindings, telemetry. Must match the options
    /// <c>Game1.BuildOverlayState</c> hands the renderer, or the keyboard cannot reach
    /// the last row.
    /// </summary>
    public const int SettingsRowCount = 5;

    public int MenuSelection { get; set; }
    public int PauseSelection { get; set; }
    public int SettingsSelection { get; set; }
    public int ConsentSelection { get; set; }
    public bool ShowSettings { get; set; }

    public void OpenSettings()
    {
        ShowSettings = true;
        SettingsSelection = 0;
    }

    /// <summary>Which consent button was confirmed this frame, or -1.</summary>
    public int StepConsent(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer)
    {
        var pick = ListPicker.Step(ConsentSelection, input, keyboard, mouse, pointer,
            ConsentButtons, UiLayout.ConsentButton, ListAxis.Horizontal, wrap: false);
        ConsentSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse) ? ConsentSelection : -1;
    }

    /// <summary>True when the current title-menu row should fire.</summary>
    public bool StepMenu(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int itemCount, out bool moved)
    {
        var pick = ListPicker.Step(MenuSelection, input, keyboard, mouse, pointer,
            itemCount, UiLayout.MenuItem);
        MenuSelection = pick.Selection;
        moved = pick.KeyboardMoved;
        return pick.Confirmed(input, keyboard, mouse);
    }

    /// <summary>True when the current pause row should fire.</summary>
    public bool StepPause(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int itemCount, bool inRun)
    {
        var pick = ListPicker.Step(PauseSelection, input, keyboard, mouse, pointer,
            itemCount, index => UiLayout.PauseItem(inRun, index));
        PauseSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse);
    }

    public SettingsCommand StepSettings(InputRouter input, KeyboardState keyboard, Vector2 pointer)
    {
        var mouse = input.CurrentMouse;
        var pick = ListPicker.Step(SettingsSelection, input, keyboard, mouse, pointer,
            SettingsRowCount, UiLayout.SettingsRow);
        SettingsSelection = pick.Selection;

        var clicked = input.Clicked(mouse);
        var toggled = input.Pressed(keyboard, Keys.Enter) || (clicked && pick.Hovered == 0);
        if (SettingsSelection == 0 && toggled)
            return new SettingsCommand(SettingsAction.ToggleDisplay);

        var nudge = 0f;
        if (input.Pressed(keyboard, Keys.Right)) nudge = 1f;
        else if (input.Pressed(keyboard, Keys.Left)) nudge = -1f;
        else if (clicked && (pick.Hovered == 1 || pick.Hovered == 2))
        {
            var row = UiLayout.SettingsRow(pick.Hovered);
            nudge = pointer.X < row.Center.X ? -1f : 1f;
        }

        if (nudge == 0f) return SettingsCommand.Idle;
        if (SettingsSelection == 1) return new SettingsCommand(SettingsAction.NudgeScale, nudge);
        if (SettingsSelection == 2) return new SettingsCommand(SettingsAction.NudgeVolume, nudge);
        return SettingsCommand.Idle;
    }
}
