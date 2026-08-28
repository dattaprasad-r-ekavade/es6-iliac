using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

internal enum CampAction { None, Dismiss, SellLoot, BuyStock }
internal readonly record struct CampCommand(CampAction Action, int StockIndex = -1)
{
    public static CampCommand Idle => new(CampAction.None);
    public static CampCommand Dismiss => new(CampAction.Dismiss);
}

internal enum ShaftAction { None, Dismiss, Commit }

internal enum FortAction { None, Close, Back, Enter }
internal readonly record struct FortCommand(FortAction Action, int RoomIndex = -1)
{
    public static FortCommand Idle => new(FortAction.None);
}

internal enum ShopAction { None, Dismiss, Buy }

internal enum DialogueAction { None, Dismiss, Ask }
internal readonly record struct DialogueCommand(DialogueAction Action, string? Keyword = null)
{
    public static DialogueCommand Idle => new(DialogueAction.None);
    public static DialogueCommand Dismiss => new(DialogueAction.Dismiss);
}

internal enum DoorAction { None, Camp, CallTrader, PressOn }

/// <summary>
/// Selection, hover and confirm for the world-scene panels: inventory, shop, dialogue,
/// shaft, camp trader, fort and the run-summary button.
///
/// Game1 still owns what a confirmed row does — buying, asking a topic, paying for a mine,
/// opening a fort door. This type must not take a <c>Game1</c> reference; it returns a
/// <c>CampCommand</c> / <c>FortCommand</c> / index instead.
///
/// Overlay-stack screens stay on <see cref="OverlayInput"/>. Do not fold those in here, and
/// do not put this selection logic back into Game1.
/// </summary>
internal sealed class WorldPanelInput
{
    public int InventorySelection { get; set; }
    public int ShopSelection { get; set; }
    public int DialogueSelection { get; set; }
    public int FortSelection { get; set; }
    public int CampSelection { get; set; }
    public int DepthSelection { get; set; } = MineEntry.MinTier;

    public bool StepInventory(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int count)
    {
        if (count <= 0)
        {
            InventorySelection = 0;
            return false;
        }

        var shown = Math.Min(count, UiLayout.InventoryRows);
        var pick = ListPicker.StepGrid(InventorySelection, input, keyboard, mouse, pointer,
            count, UiLayout.InventoryColumns, i => i < shown ? UiLayout.InventoryTile(i) : null,
            wrap: true, wasd: true);
        InventorySelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse);
    }

    public bool StepShop(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int count)
    {
        if (count <= 0) return false;

        var pick = ListPicker.StepGrid(ShopSelection, input, keyboard, mouse, pointer,
            count, UiLayout.ShopColumns,
            i => ShopRenderer.TileFor(i, ShopSelection, count),
            wrap: true);
        ShopSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse);
    }

    /// <summary>Which topic was confirmed this frame, or -1. Number keys only move the highlight.</summary>
    public int StepDialogue(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int topicCount)
    {
        if (topicCount <= 0) return -1;

        var digit = ListPicker.DigitIndex(input, keyboard, topicCount);
        if (digit >= 0) DialogueSelection = digit;

        var visible = Math.Min(topicCount, UiLayout.DialogueRows);
        var pick = ListPicker.Step(DialogueSelection, input, keyboard, mouse, pointer,
            topicCount, i => i < visible ? UiLayout.DialogueTopic(i) : default);
        DialogueSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse) ? DialogueSelection : -1;
    }

    public bool StepFort(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int roomCount)
    {
        if (roomCount <= 0) return false;

        var pick = ListPicker.Step(FortSelection, input, keyboard, mouse, pointer,
            roomCount, FortRenderer.DoorRow, wrap: false);
        FortSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse);
    }

    /// <summary>Dismiss, sell the pack, or buy a stock row. Game1 applies the trade.</summary>
    public CampCommand StepCampCommand(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int rows, bool sessionLive)
    {
        if (!sessionLive) return CampCommand.Dismiss;
        if (input.Pressed(keyboard, Keys.Escape) || input.Pressed(keyboard, Keys.T))
            return CampCommand.Dismiss;
        if (!StepCamp(input, keyboard, mouse, pointer, rows)) return CampCommand.Idle;
        if (CampSelection == 0) return new CampCommand(CampAction.SellLoot);
        return new CampCommand(CampAction.BuyStock, CampSelection - 1);
    }

    /// <summary>Dismiss the shaft, or commit the highlighted depth. Game1 pays and enters.</summary>
    public ShaftAction StepShaftCommand(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, bool sessionLive)
    {
        if (!sessionLive) return ShaftAction.Dismiss;
        if (input.Pressed(keyboard, Keys.Escape)) return ShaftAction.Dismiss;
        return StepDepth(input, keyboard, mouse, pointer) ? ShaftAction.Commit : ShaftAction.None;
    }

    /// <summary>Step back a room, close the fort, or enter a highlighted door. Game1 checks rank.</summary>
    public FortCommand StepFortCommand(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, bool sessionLive, bool inRoom)
    {
        if (!sessionLive) return new FortCommand(FortAction.Close);
        if (input.Pressed(keyboard, Keys.Escape))
            return new FortCommand(inRoom ? FortAction.Back : FortAction.Close);
        if (inRoom) return FortCommand.Idle;
        if (!StepFort(input, keyboard, mouse, pointer, FortRoster.All.Count)) return FortCommand.Idle;
        return new FortCommand(FortAction.Enter, FortSelection);
    }

    /// <summary>True when the highlighted pack item should be used.</summary>
    public bool StepInventoryCommand(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int count) =>
        StepInventory(input, keyboard, mouse, pointer, count);

    /// <summary>Dismiss the stall, or buy the highlighted stock.</summary>
    public ShopAction StepShopCommand(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int count)
    {
        if (input.Pressed(keyboard, Keys.Escape) || input.Pressed(keyboard, Keys.B))
            return ShopAction.Dismiss;
        if (count <= 0) return ShopAction.None;
        return StepShop(input, keyboard, mouse, pointer, count) ? ShopAction.Buy : ShopAction.None;
    }

    /// <summary>Dismiss the talk, or ask the highlighted topic. Game1 resolves the line.</summary>
    public DialogueCommand StepDialogueCommand(InputRouter input, KeyboardState keyboard,
        MouseState mouse, Vector2 pointer, IReadOnlyList<string>? topics)
    {
        if (topics is null) return DialogueCommand.Dismiss;
        if (input.Pressed(keyboard, Keys.Escape)) return DialogueCommand.Dismiss;
        if (topics.Count == 0) return DialogueCommand.Idle;
        var chosen = StepDialogue(input, keyboard, mouse, pointer, topics.Count);
        return chosen >= 0
            ? new DialogueCommand(DialogueAction.Ask, topics[chosen])
            : DialogueCommand.Idle;
    }

    /// <summary>C camp, T whistle, E press on — only while a shut door is in front of the player.</summary>
    public static DoorAction StepDoor(InputRouter input, KeyboardState keyboard,
        bool canCallTrader, bool canPressOn)
    {
        if (input.Pressed(keyboard, Keys.C)) return DoorAction.Camp;
        if (input.Pressed(keyboard, Keys.T) && canCallTrader) return DoorAction.CallTrader;
        if (input.Pressed(keyboard, Keys.E) && canPressOn) return DoorAction.PressOn;
        return DoorAction.None;
    }

    public bool StepCamp(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer, int rows)
    {
        if (rows <= 0) return false;

        var pick = ListPicker.Step(CampSelection, input, keyboard, mouse, pointer,
            rows, UiLayout.CampRow);
        CampSelection = pick.Selection;
        return pick.Confirmed(input, keyboard, mouse);
    }

    public bool StepDepth(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer)
    {
        var min = MineEntry.MinTier;
        var count = MineEntry.MaxTier - min + 1;
        var index = DepthSelection - min;

        var pick = ListPicker.Step(index, input, keyboard, mouse, pointer,
            count, i => UiLayout.DepthRow(i + min), wrap: false);
        DepthSelection = pick.Selection + min;
        return pick.Confirmed(input, keyboard, mouse);
    }

    public bool StepSummary(InputRouter input, KeyboardState keyboard, MouseState mouse,
        Vector2 pointer)
    {
        var pick = ListPicker.Step(0, input, keyboard, mouse, pointer,
            1, _ => UiLayout.SummaryButton, wrap: false);
        return pick.Confirmed(input, keyboard, mouse);
    }
}
