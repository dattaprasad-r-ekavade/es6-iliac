using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace RatnaBay.Client;

/// <summary>Which pair of keys walks a list.</summary>
internal enum ListAxis
{
    Vertical,
    Horizontal
}

/// <summary>
/// One frame of list navigation: the row that is selected, the row the pointer is over,
/// and whether a key moved the selection.
///
/// Confirming a row is a separate question — settings sliders click without "activating",
/// and a click that missed every row must not fire the current one.
/// </summary>
internal readonly record struct ListPick(int Selection, int Hovered, bool KeyboardMoved)
{
    public bool Hovering => Hovered >= 0;

    public bool ClickedOnRow(InputRouter input, MouseState mouse) =>
        Hovering && input.Clicked(mouse);

    /// <summary>Enter, Space, or a click that landed on a row.</summary>
    public bool Confirmed(InputRouter input, KeyboardState keyboard, MouseState mouse) =>
        input.Pressed(keyboard, Keys.Enter)
        || input.Pressed(keyboard, Keys.Space)
        || ClickedOnRow(input, mouse);
}

/// <summary>
/// Wrap or clamp a selected row from an <see cref="InputRouter"/> snapshot.
///
/// **This is the piece a different game would reuse unchanged.** Row bounds are a callback,
/// not this game's <c>UiLayout</c>. The caller owns the selected index and what confirming
/// a row means.
/// </summary>
internal static class ListPicker
{
    public static ListPick Step(
        int selection,
        InputRouter input,
        KeyboardState keyboard,
        MouseState mouse,
        Vector2 pointer,
        int count,
        Func<int, Rectangle> row,
        ListAxis axis = ListAxis.Vertical,
        bool wrap = true)
    {
        if (count <= 0) return new ListPick(0, -1, false);

        selection = Math.Clamp(selection, 0, count - 1);

        var prev = axis == ListAxis.Vertical ? Keys.Up : Keys.Left;
        var next = axis == ListAxis.Vertical ? Keys.Down : Keys.Right;
        var moved = false;

        if (input.Pressed(keyboard, prev))
        {
            selection = wrap ? (selection + count - 1) % count : Math.Max(0, selection - 1);
            moved = true;
        }

        if (input.Pressed(keyboard, next))
        {
            selection = wrap ? (selection + 1) % count : Math.Min(count - 1, selection + 1);
            moved = true;
        }

        var hovered = Hovered(pointer, count, row);
        if (hovered >= 0) selection = hovered;

        return new ListPick(selection, hovered, moved);
    }

    public static int Hovered(Vector2 pointer, int count, Func<int, Rectangle> row)
    {
        var point = new Point((int)pointer.X, (int)pointer.Y);
        for (var index = 0; index < count; index++)
            if (row(index).Contains(point)) return index;

        return -1;
    }
}
