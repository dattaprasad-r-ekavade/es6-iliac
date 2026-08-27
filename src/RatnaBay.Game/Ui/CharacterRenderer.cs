using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;

namespace RatnaBay.Client;

/// <summary>
/// Character sheet: vitals, stones, equipped slots, pack and live skills.
///
/// Item selection and input stay in Game1. This class owns layout so a change to the pack
/// grid cannot wander into combat or save logic.
/// </summary>
internal sealed class CharacterRenderer
{
    private readonly UiCanvas _ui;

    public CharacterRenderer(UiCanvas ui) => _ui = ui;

    public void Draw(PlayerCharacter player, int inventorySelection, Texture2D? crystal)
    {
        var vitals = player.Vitals;
        var panel = new Rectangle(90, 70, 1100, 580);
        _ui.Panel(panel, UiTheme.Panel, new Color(117, 153, 166));
        _ui.Text("CHARACTER", new Vector2(panel.X + 30, panel.Y + 22), 13,
            UiTheme.GoldDim);

        var name = string.IsNullOrWhiteSpace(player.Story.State.Profile.Name)
            ? "Northwatch Wanderer"
            : player.Story.State.Profile.Name;
        _ui.TextFit(name, new Vector2(panel.X + 30, panel.Y + 52), 440f, 28, Color.White);
        _ui.TextRight($"{vitals.Gold} gold", panel.Right - 30, panel.Y + 60, 17,
            UiTheme.GoldBright);

        var leftX = panel.X + 30;
        var skillsX = panel.X + 800;
        var top = panel.Y + 112;

        _ui.Text("VITALS", new Vector2(leftX, top), 13, UiTheme.Accent);
        _ui.Text($"Level {vitals.Level}   XP {vitals.Xp} / {vitals.XpToLevel}",
            new Vector2(leftX, top + 34), 17, Color.White);
        _ui.Text($"Health     {vitals.Health:0} / {vitals.MaxHealth:0}",
            new Vector2(leftX, top + 70), 16, new Color(224, 116, 105));
        _ui.Text($"Prana       {vitals.Prana:0} / {vitals.MaxPrana:0}",
            new Vector2(leftX, top + 100), 16, new Color(112, 174, 225));
        _ui.Text($"Stamina   {vitals.Stamina:0} / {vitals.MaxStamina:0}",
            new Vector2(leftX, top + 130), 16, new Color(117, 194, 137));
        _ui.Text($"Jiva stones drawn: {vitals.Channeled}",
            new Vector2(leftX, top + 182), 15, UiTheme.Prompt);

        DrawStoneSlots(player, leftX, top + 224, crystal);
        DrawEquippedSlots(player);
        DrawPack(player, inventorySelection);

        _ui.Text("SKILLS", new Vector2(skillsX, top), 13, UiTheme.Accent);
        var skillY = top + 34;
        foreach (var skillId in Skills.All)
        {
            // Parked skills are not shown. Nothing trains Security or Stealth now that
            // picking and sneaking are switched off, and a number that cannot move is a
            // promise the interface is making on the game's behalf and not keeping.
            if (!ParkedFeatures.SkillIsLive(skillId)) continue;

            _ui.TextFit(Skills.Label(skillId), new Vector2(skillsX, skillY), 205f, 16,
                UiTheme.Body);
            _ui.TextRight(player.Skills.LevelOf(skillId).ToString("0.0"), panel.Right - 34,
                skillY, 16, Color.White);
            skillY += 37;
        }

        _ui.Text("Arrows or hover to choose      Enter to use or equip      1-6 socket a stone      I / K / Esc close",
            new Vector2(panel.X + 30, panel.Bottom - 34), 13, UiTheme.Hint);
    }

    /// <summary>
    /// The sockets and whatever the mountain has given up this run.
    ///
    /// Put on the character screen rather than in its own window because it is read mid-run,
    /// between rooms, with a decision waiting at a door — and a second screen to open is a
    /// second reason not to bother.
    /// </summary>
    private void DrawStoneSlots(PlayerCharacter player, int x, int y, Texture2D? crystal)
    {
        var stones = player.Stones;

        _ui.Text("STONES", new Vector2(x, y), 13, UiTheme.Accent);

        var capacity = stones.Capacity;
        if (capacity == 0)
        {
            _ui.Text("Nothing you hold has a socket.", new Vector2(x, y + 28), 14,
                new Color(150, 160, 162));
            return;
        }

        for (var slot = 0; slot < capacity; slot++)
        {
            var cell = new Rectangle(x + slot * 46, y + 26, 40, 40);
            var filled = slot < stones.Socketed.Count;

            _ui.Panel(cell,
                filled ? new Color(52, 34, 74, 240) : new Color(14, 22, 30, 220),
                filled ? new Color(178, 132, 226) : new Color(58, 78, 88));

            if (!filled || crystal is null) continue;

            var stone = StoneCatalog.Find(stones.Socketed[slot]);
            if (stone is null) continue;

            _ui.Sprite(crystal, new Rectangle(cell.X + 4, cell.Y + 4, 32, 32), Color.White);
        }

        var listY = y + 76;

        if (stones.Socketed.Count > 0)
        {
            foreach (var id in stones.Socketed)
            {
                var stone = StoneCatalog.Find(id);
                if (stone is null) continue;

                _ui.TextFit($"{stone.DisplayName} — {stone.Description}",
                    new Vector2(x, listY), 430f, 13, new Color(214, 184, 244));
                listY += 22;
            }

            listY += 6;
        }

        if (stones.Loose.Count == 0)
        {
            _ui.Text(stones.Socketed.Count > 0 ? "Nothing else found." : "Nothing found yet.",
                new Vector2(x, listY), 13, new Color(150, 160, 162));
            return;
        }

        _ui.Text("FOUND — press the number to socket", new Vector2(x, listY), 12,
            new Color(196, 170, 120));
        listY += 22;

        for (var index = 0; index < stones.Loose.Count && index < 6; index++)
        {
            var stone = StoneCatalog.Find(stones.Loose[index]);
            if (stone is null) continue;

            _ui.TextFit($"{index + 1}.  {stone.DisplayName} — {stone.Description}",
                new Vector2(x, listY), 430f, 13,
                stones.HasRoom ? new Color(226, 220, 208) : new Color(140, 132, 126));
            listY += 22;
        }

        if (!stones.HasRoom)
            _ui.Text("Every socket is full. Press 0 to empty the last one.",
                new Vector2(x, listY + 4), 12, new Color(214, 150, 120));
    }

    private void DrawEquippedSlots(PlayerCharacter player)
    {
        _ui.Text("EQUIPPED", new Vector2(UiLayout.InventoryLeft, 182), 13, UiTheme.Accent);

        var weapon = player.Equipment.Weapon;
        var armour = player.Equipment.Armour;

        string[] labels = { "IN HAND", "WORN" };
        string[] names = { weapon.DisplayName, armour?.DisplayName ?? "Nothing" };
        string[] notes =
        {
            $"{weapon.Damage:0} damage   {weapon.Range:0.0} m   {(weapon.CanBlock ? "guards" : "no guard")}",
            armour is null ? "no protection" : $"{armour.Armour:0} damage reduction"
        };

        for (var index = 0; index < 2; index++)
        {
            var slot = UiLayout.EquippedSlot(index);
            var filled = index == 0 || armour is not null;

            _ui.Panel(slot, new Color(14, 24, 32, 235),
                filled ? new Color(120, 150, 130) : new Color(54, 68, 76));

            _ui.Text(labels[index], new Vector2(slot.X + 12, slot.Y + 8), 11,
                new Color(140, 168, 160));
            _ui.TextFit(names[index], new Vector2(slot.X + 12, slot.Y + 24), slot.Width - 24, 15,
                filled ? Color.White : new Color(128, 138, 142));
            _ui.TextRight(notes[index], slot.Right - 12, slot.Y + 8, 11, UiTheme.Muted);
        }
    }

    private void DrawPack(PlayerCharacter player, int inventorySelection)
    {
        var items = player.Inventory.Items;
        _ui.Text("PACK", new Vector2(UiLayout.InventoryLeft, 268), 13, UiTheme.Accent);

        if (items.Count == 0)
        {
            _ui.Text("Empty. Everything down there drops something.",
                new Vector2(UiLayout.InventoryLeft, UiLayout.InventoryTop + 8), 15,
                UiTheme.Faint);
            return;
        }

        var selection = Math.Clamp(inventorySelection, 0, items.Count - 1);
        var shown = Math.Min(items.Count, UiLayout.InventoryRows);

        for (var index = 0; index < shown; index++)
        {
            var item = items[index];
            var tile = UiLayout.InventoryTile(index);
            var selected = index == selection;
            var inHand = string.Equals(item.Id, player.Equipment.WeaponId, StringComparison.Ordinal)
                || string.Equals(item.Id, player.Equipment.ArmourId, StringComparison.Ordinal);

            // Not the shared row: what is in hand keeps its own border when the selection is
            // elsewhere, because "which of these two swords am I holding" is the question the
            // pack exists to answer.
            _ui.Panel(tile,
                selected ? UiTheme.RowSelected : UiTheme.RowIdle,
                selected ? UiTheme.RowSelectedBorder
                    : inHand ? new Color(120, 150, 130)
                    : UiTheme.RowIdleBorder);

            _ui.TextFit(item.Name, new Vector2(tile.X + 10, tile.Y + 9), tile.Width - 20, 14,
                selected ? Color.White : UiTheme.Body);

            if (item.Count > 1)
                _ui.TextRight($"x{item.Count}", tile.Right - 10, tile.Y + 30, 13,
                    UiTheme.GoldBright);

            if (inHand)
                _ui.Text("equipped", new Vector2(tile.X + 10, tile.Y + 31), 11,
                    new Color(150, 200, 158));
        }

        var rowsUsed = (shown + UiLayout.InventoryColumns - 1) / UiLayout.InventoryColumns;
        var belowPack = UiLayout.InventoryTop + rowsUsed * (UiLayout.InventoryTileHeight + 6);

        if (items.Count > shown)
            _ui.TextRight($"+{items.Count - shown} more", UiLayout.InventoryLeft + 426, belowPack + 4, 12,
                UiTheme.Faint);

        var chosen = items[selection];
        var detail = new Rectangle(UiLayout.InventoryLeft, belowPack + 22, 426, 78);

        _ui.Panel(detail, new Color(8, 16, 24, 232), new Color(72, 104, 118));
        _ui.TextFit(chosen.Name, new Vector2(detail.X + 14, detail.Y + 10), detail.Width - 28, 16,
            Color.White);
        _ui.TextFit(ItemUse.Describe(chosen.Id, chosen.Kind),
            new Vector2(detail.X + 14, detail.Y + 34), detail.Width - 28, 13,
            new Color(196, 212, 210));

        var verb = ItemUse.DescribeAction(chosen.Id, chosen.Kind);
        _ui.TextFit(verb == "—"
                ? "Nothing happens when you use this."
                : $"Enter or click to {verb.ToLowerInvariant()}",
            new Vector2(detail.X + 14, detail.Y + 56), detail.Width - 28, 13,
            verb == "—" ? UiTheme.Faint : UiTheme.Gold);
    }
}
