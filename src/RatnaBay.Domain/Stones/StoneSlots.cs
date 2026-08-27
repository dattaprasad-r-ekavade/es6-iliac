using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// The sockets in the player's gear, and what is currently in them.
///
/// The rule that shapes everything here is from the design: **stones are found below, never
/// carried down.** You descend with your gear and its empty sockets, and what the mountain
/// gives you decides how this run plays.
///
/// That is what makes stones the *tactical* layer rather than a second progression track.
/// Amulets and levels accumulate and carry build identity across runs; stones answer this
/// cave and are gone when you leave it. Letting them persist would collapse the two layers
/// into one and turn every run into the same run with better numbers — which is the exact
/// failure the short run length was chosen to avoid.
/// </summary>
public sealed class StoneSlots
{
    private readonly PlayerEquipment _equipment;

    /// <summary>The order's permanent gains, which can add a socket. Null in isolated tests.</summary>
    private readonly Legacy? _legacy;

    private readonly List<string> _socketed = new();
    private readonly List<string> _loose = new();

    public StoneSlots(PlayerEquipment equipment, Legacy? legacy = null)
    {
        _equipment = equipment;
        _legacy = legacy;

        // Changing gear mid-run can shrink the sockets available. Anything that no longer
        // fits falls back into the pack rather than vanishing.
        _equipment.Changed += Reseat;
    }

    public event Action? Changed;

    /// <summary>Sockets in the weapon and armour currently held.</summary>
    public int Capacity => SocketsOf(_equipment.Weapon.Tier, _equipment.WeaponId)
        + SocketsOf(_equipment.Armour?.Tier ?? 0, _equipment.ArmourId)
        + (_legacy?.Has(AmuletEffect.Socket) == true ? 1 : 0);

    /// <summary>
    /// How many sockets a piece has.
    ///
    /// Tier, not rarity: a better weapon is a weapon with more room in it, which gives the
    /// gold spent in town a second reason to matter beyond the damage number.
    /// </summary>
    private static int SocketsOf(int tier, string? id) =>
        string.IsNullOrEmpty(id) || id == EquipmentCatalog.UnarmedId ? 0 : Math.Max(1, tier);

    /// <summary>Stones currently in a socket, and therefore currently doing something.</summary>
    public IReadOnlyList<string> Socketed => _socketed;

    /// <summary>Stones found this descent that are not in a socket.</summary>
    public IReadOnlyList<string> Loose => _loose;

    public bool HasRoom => _socketed.Count < Capacity;

    /// <summary>The mountain gives one up.</summary>
    public void Found(string? stoneId)
    {
        if (!StoneCatalog.Exists(stoneId)) return;

        _loose.Add(stoneId!);
        Changed?.Invoke();
    }

    /// <summary>
    /// Put a loose stone into a socket. Refuses when there is no room or no such stone.
    /// </summary>
    public bool Socket(string? stoneId)
    {
        if (!StoneCatalog.Exists(stoneId) || !HasRoom) return false;
        if (!_loose.Remove(stoneId!)) return false;

        _socketed.Add(stoneId!);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Take one back out.
    ///
    /// Free, and deliberately so. A socket the player is afraid to fill is a socket that does
    /// nothing, and inside a six-minute run there is no time to agonise — the interesting
    /// decision is which stone answers this cave, not whether to risk committing at all.
    /// </summary>
    public bool Unsocket(string? stoneId)
    {
        if (!_socketed.Remove(stoneId!)) return false;

        _loose.Add(stoneId!);
        Changed?.Invoke();
        return true;
    }

    /// <summary>True when a stone with this effect is in a socket.</summary>
    public bool Has(StoneEffect effect) =>
        _socketed.Any(id => StoneCatalog.Find(id)?.Effect == effect);

    /// <summary>
    /// Empty every socket and drop everything found. Called when a descent begins.
    ///
    /// On beginning rather than on ending, because a run can end in ways nobody gets to run
    /// code for — dying, quitting, closing the window. Clearing on entry is the only version
    /// that cannot leave last run's stones socketed at the start of this one.
    /// </summary>
    public void ClearForDescent()
    {
        if (_socketed.Count == 0 && _loose.Count == 0) return;

        _socketed.Clear();
        _loose.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Push out anything that no longer has a socket to sit in.
    ///
    /// Swapping a two-socket weapon for a one-socket one has to do something defined, and
    /// silently dropping the stone is the version a player would call a bug. The last one
    /// socketed is the one that comes out, so the choice made earliest survives.
    /// </summary>
    private void Reseat()
    {
        var capacity = Capacity;
        var moved = false;

        while (_socketed.Count > capacity)
        {
            var last = _socketed[^1];
            _socketed.RemoveAt(_socketed.Count - 1);
            _loose.Add(last);
            moved = true;
        }

        if (moved) Changed?.Invoke();
    }
}
