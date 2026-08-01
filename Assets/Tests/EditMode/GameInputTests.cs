using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

public sealed class GameInputTests
{
    [Test]
    public void AssetContainsTheCompleteKeyboardMouseContract()
    {
        var asset = GameInput.Asset;
        Assert.IsNotNull(asset);
        Assert.AreEqual(1, asset.actionMaps.Count, "W-03 requires one shared action map.");

        string[] required =
        {
            "Move", "Look", "Jump", "Sprint", "PrimaryAttack", "SecondaryAttack",
            "UsePotion", "Interact", "Save", "Load", "ToggleMap", "ToggleJournal",
            "ToggleInventory", "ToggleWait", "Cancel", "Submit", "Navigate", "Travel",
            "WaitOneHour", "WaitEightHours", "WaitDay", "Skip",
            "RouteWarrior", "RouteMage", "RouteTrade", "RouteRefuse"
        };

        CollectionAssert.AreEquivalent(
            required,
            asset.actionMaps[0].actions.Select(action => action.name).ToArray());
    }

    [TestCase("Move", "<Keyboard>/w")]
    [TestCase("Move", "<Keyboard>/upArrow")]
    [TestCase("Look", "<Mouse>/delta")]
    [TestCase("PrimaryAttack", "<Mouse>/leftButton")]
    [TestCase("PrimaryAttack", "<Keyboard>/1")]
    [TestCase("Interact", "<Keyboard>/e")]
    [TestCase("Save", "<Keyboard>/f5")]
    [TestCase("Load", "<Keyboard>/f9")]
    [TestCase("Skip", "<Mouse>/rightButton")]
    [TestCase("RouteWarrior", "<Keyboard>/f1")]
    [TestCase("RouteMage", "<Keyboard>/f2")]
    [TestCase("RouteTrade", "<Keyboard>/f3")]
    [TestCase("RouteRefuse", "<Keyboard>/f4")]
    public void CriticalBindingIsPresent(string actionName, string effectivePath)
    {
        InputAction action = GameInput.Asset.FindAction(actionName, throwIfNotFound: true);
        Assert.IsTrue(
            action.bindings.Any(binding => binding.effectivePath == effectivePath),
            $"{actionName} is missing {effectivePath}.");
    }

    [Test]
    public void EveryBindingIsKeyboardOrMouse()
    {
        foreach (var map in GameInput.Asset.actionMaps)
        foreach (var binding in map.bindings.Where(binding => !binding.isComposite))
        {
            if (binding.isPartOfComposite || !string.IsNullOrEmpty(binding.path))
            {
                Assert.IsTrue(
                    binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>"),
                    $"W-03 scope drift: {binding.action} uses {binding.path}.");
            }
        }
    }
}
