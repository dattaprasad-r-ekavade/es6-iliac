using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Typed access to the single project input-actions asset. Consumers ask for intent here
/// instead of polling Keyboard.current or Mouse.current directly.
/// </summary>
public static class GameInput
{
    private const string ResourcePath = "Input/KessilInputActions";
    private static InputActionAsset _asset;

    public static InputActionAsset Asset
    {
        get
        {
            if (_asset != null) return _asset;
            _asset = Resources.Load<InputActionAsset>(ResourcePath);
            if (_asset == null)
                throw new InvalidOperationException(
                    $"Input actions asset is missing at Resources/{ResourcePath}.inputactions.");
            _asset.Enable();
            return _asset;
        }
    }

    public static InputAction Move => Find("Move");
    public static InputAction Look => Find("Look");
    public static InputAction Jump => Find("Jump");
    public static InputAction Sprint => Find("Sprint");
    public static InputAction PrimaryAttack => Find("PrimaryAttack");
    public static InputAction SecondaryAttack => Find("SecondaryAttack");
    /// <summary>Held, not pressed. Only effective with a one-handed weapon equipped.</summary>
    public static InputAction Block => Find("Block");
    public static InputAction UsePotion => Find("UsePotion");
    public static InputAction Interact => Find("Interact");
    public static InputAction Save => Find("Save");
    public static InputAction Load => Find("Load");
    public static InputAction ToggleMap => Find("ToggleMap");
    public static InputAction ToggleJournal => Find("ToggleJournal");
    public static InputAction ToggleInventory => Find("ToggleInventory");
    public static InputAction ToggleWait => Find("ToggleWait");
    public static InputAction Cancel => Find("Cancel");
    public static InputAction Submit => Find("Submit");
    public static InputAction Navigate => Find("Navigate");
    public static InputAction Travel => Find("Travel");
    public static InputAction WaitOneHour => Find("WaitOneHour");
    public static InputAction WaitEightHours => Find("WaitEightHours");
    public static InputAction WaitDay => Find("WaitDay");
    public static InputAction Skip => Find("Skip");
    public static InputAction RouteWarrior => Find("RouteWarrior");
    public static InputAction RouteMage => Find("RouteMage");
    public static InputAction RouteTrade => Find("RouteTrade");
    public static InputAction RouteRefuse => Find("RouteRefuse");

    private static InputAction Find(string actionName)
    {
        var action = Asset.FindAction(actionName, throwIfNotFound: false);
        if (action == null)
            throw new InvalidOperationException($"Input action '{actionName}' is missing.");
        return action;
    }
}
