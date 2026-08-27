using Microsoft.Xna.Framework;
using RatnaBay.Domain;

namespace RatnaBay.Client;

/// <summary>
/// Hit-test and draw rectangles for every interactive panel.
///
/// Input handlers and renderers must share these. A clickable row that is not exactly the row
/// on screen is how a menu starts ignoring the mouse, and duplicating the numbers in Game1
/// and a renderer is how that drift happens.
/// </summary>
internal static class UiLayout
{
    public const int Width = 1280;
    public const int Height = 720;

    public const int DialogueRows = 6;
    public const int ShopColumns = 3;
    public const int InventoryColumns = 3;
    public const int InventoryRows = 12;
    public const int InventoryLeft = 440;
    public const int InventoryTop = 288;
    public const int InventoryTileWidth = 138;
    public const int InventoryTileHeight = 52;

    public static Rectangle FullScreen => new(0, 0, Width, Height);

    public static Rectangle MenuItem(int index) => new(120, 286 + index * 56, 368, 42);

    public static Rectangle SettingsRow(int index) => new(284, 214 + index * 56, 712, 42);

    public static Rectangle ConsentButton(int index) => new(360 + index * 296, 534, 264, 46);

    public static Rectangle CampRow(int index) => new(360, 262 + index * 52, 560, 44);

    public static Rectangle DepthRow(int tier) =>
        new(348, 236 + (tier - MineEntry.MinTier) * 56, 584, 48);

    public static Rectangle PausePanel(bool inRun) => new(400, 196, 480, inRun ? 332 : 268);

    public static Rectangle PauseItem(bool inRun, int index)
    {
        var panel = PausePanel(inRun);
        var top = inRun ? panel.Y + 118 : panel.Y + 78;
        return new Rectangle(panel.X + 40, top + index * 46, panel.Width - 80, 38);
    }

    public static Rectangle DialoguePanel => new(352, 150, 576, 420);

    public static Rectangle DialogueTopic(int index) => new(376, 300 + index * 34, 528, 30);

    public static Rectangle ShopItem(int index) => new(
        282 + index % ShopColumns * 246,
        200 + index / ShopColumns * 96,
        230, 84);

    public static Rectangle TalkPrompt => new(302, 596, 224, 42);
    public static Rectangle SecondaryPrompt => new(534, 596, 212, 42);
    public static Rectangle PickpocketPrompt => new(754, 596, 224, 42);
    public static Rectangle SinglePrompt => new(388, 596, 504, 42);

    /// <summary>
    /// Where a prompt's text sits inside its panel.
    ///
    /// Derived rather than typed. The single prompt's text was written at (404, 608) in four
    /// places while the panel itself came from <see cref="SinglePrompt"/> — so moving the panel
    /// left the words behind, and only in some of the four.
    /// </summary>
    public static Vector2 PromptText(Rectangle prompt) => new(prompt.X + 16, prompt.Y + 12);

    /// <summary>How wide a prompt's text may run before it must shrink.</summary>
    public static float PromptTextWidth(Rectangle prompt) => prompt.Width - 32;

    public static Rectangle InventoryTile(int index) => new(
        InventoryLeft + index % InventoryColumns * (InventoryTileWidth + 6),
        InventoryTop + index / InventoryColumns * (InventoryTileHeight + 6),
        InventoryTileWidth, InventoryTileHeight);

    public static Rectangle EquippedSlot(int index) =>
        new(InventoryLeft + index * 216, 202, 210, 52);

    public static Rectangle SummaryButton => new(492, 500, 296, 42);
}
