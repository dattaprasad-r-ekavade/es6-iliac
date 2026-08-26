using Microsoft.Xna.Framework.Input;

namespace RatnaBay.Client;

/// <summary>
/// Samples device input once per frame and owns edge detection between frames.
///
/// Screen handlers are responsible for deciding what a key means, but they should all observe
/// the same snapshot. Keeping sampling here prevents a handler from seeing a different mouse
/// state than the one used by the frame coordinator.
/// </summary>
internal sealed class InputRouter
{
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public KeyboardState CurrentKeyboard { get; private set; }
    public MouseState CurrentMouse { get; private set; }

    public void Sample()
    {
        CurrentKeyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        CurrentMouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
    }

    public bool Pressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public bool Clicked(MouseState current) =>
        current.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    public void CommitKeyboard() => _previousKeyboard = CurrentKeyboard;

    public void CommitMouse() => _previousMouse = CurrentMouse;

    public void Commit()
    {
        CommitKeyboard();
        CommitMouse();
    }
}
