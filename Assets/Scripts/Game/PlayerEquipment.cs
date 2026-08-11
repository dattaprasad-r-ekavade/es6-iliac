using System;
using UnityEngine;

/// <summary>
/// What the player is holding and wearing.
///
/// Before this existed the inventory was cosmetic: the Iron Sword in the player's pack did
/// nothing, and melee damage was a hardcoded field on <see cref="PlayerCombat"/>. Loot,
/// merchants and route rewards were all hollow as a result.
///
/// Only ids are stored, so the save carries ids and <see cref="EquipmentCatalog"/> supplies
/// the numbers. Rebalancing cannot invalidate a save.
/// </summary>
public sealed class PlayerEquipment : MonoBehaviour
{
    public static PlayerEquipment Instance { get; private set; }

    public string WeaponId { get; private set; } = EquipmentCatalog.UnarmedId;
    public string ArmourId { get; private set; } = string.Empty;

    public event Action OnChanged;

    /// <summary>Never null — an empty or unknown id resolves to unarmed.</summary>
    public WeaponDefinition Weapon => EquipmentCatalog.GetWeapon(WeaponId);

    /// <summary>Null when nothing is worn.</summary>
    public ArmourDefinition Armour => EquipmentCatalog.GetArmour(ArmourId);

    public float ArmourValue => Armour?.Armour ?? 0f;
    public bool CanBlock => Weapon.CanBlock;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        // Equip whatever the player is already carrying, so a fresh character is not
        // inexplicably swinging bare hands past the sword in their pack.
        if (WeaponId == EquipmentCatalog.UnarmedId) AutoEquipBest();
    }

    /// <summary>
    /// Equip by item id. Refuses ids the player does not hold, so a UI bug cannot conjure
    /// gear. Returns false when the item is absent or is not equippable.
    /// </summary>
    public bool Equip(string itemId)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory != null && inventory.CountOf(itemId) <= 0) return false;

        if (EquipmentCatalog.IsWeapon(itemId))
        {
            WeaponId = itemId;
            OnChanged?.Invoke();
            return true;
        }

        if (EquipmentCatalog.IsArmour(itemId))
        {
            ArmourId = itemId;
            OnChanged?.Invoke();
            return true;
        }

        return false;
    }

    public void UnequipWeapon()
    {
        WeaponId = EquipmentCatalog.UnarmedId;
        OnChanged?.Invoke();
    }

    public void UnequipArmour()
    {
        ArmourId = string.Empty;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Equip the highest-tier weapon and armour the player actually holds. Used on spawn and
    /// after gear is returned at B500, where the alternative is the player walking out of the
    /// prison unarmed without being told why.
    /// </summary>
    public void AutoEquipBest()
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null) return;

        WeaponDefinition bestWeapon = null;
        ArmourDefinition bestArmour = null;

        foreach (var item in inventory.Items)
        {
            if (EquipmentCatalog.IsWeapon(item.Id))
            {
                var candidate = EquipmentCatalog.GetWeapon(item.Id);
                if (bestWeapon == null || candidate.Tier > bestWeapon.Tier) bestWeapon = candidate;
            }
            else if (EquipmentCatalog.IsArmour(item.Id))
            {
                var candidate = EquipmentCatalog.GetArmour(item.Id);
                if (bestArmour == null || candidate.Tier > bestArmour.Tier) bestArmour = candidate;
            }
        }

        if (bestWeapon != null) WeaponId = bestWeapon.Id;
        if (bestArmour != null) ArmourId = bestArmour.Id;
        OnChanged?.Invoke();
    }

    private string _stashedWeaponId;
    private string _stashedArmourId;

    /// <summary>True while gear is held by a gaoler rather than by the player.</summary>
    public bool GearIsStashed { get; private set; }

    /// <summary>
    /// Strip the equipped set and remember it.
    ///
    /// The convergence contract requires all four routes to enter B600 unarmed so that B630's
    /// escape can be authored once, and the beat sheet requires gear to be *stored, never
    /// destroyed*. Items stay in the pack; only the equipped set is surrendered.
    /// </summary>
    public void StashGear()
    {
        if (GearIsStashed) return;
        _stashedWeaponId = WeaponId;
        _stashedArmourId = ArmourId;
        GearIsStashed = true;
        WeaponId = EquipmentCatalog.UnarmedId;
        ArmourId = string.Empty;
        OnChanged?.Invoke();
    }

    /// <summary>Hand it back. Safe to call when nothing was stashed.</summary>
    public void RestoreStashedGear()
    {
        if (!GearIsStashed) return;
        GearIsStashed = false;
        Restore(_stashedWeaponId, _stashedArmourId);
        _stashedWeaponId = null;
        _stashedArmourId = null;
    }

    /// <summary>Restore from a save. Ids are validated against the catalog on the way in.</summary>
    public void Restore(string weaponId, string armourId)
    {
        WeaponId = EquipmentCatalog.IsWeapon(weaponId) ? weaponId : EquipmentCatalog.UnarmedId;
        ArmourId = EquipmentCatalog.IsArmour(armourId) ? armourId : string.Empty;
        OnChanged?.Invoke();
    }
}
