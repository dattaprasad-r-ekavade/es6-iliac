using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Domain;

/// <summary>
/// What a fallen Deepankar left where they fell.
///
/// Recoverable once, on the next descent into that mine. This is what keeps a death costly
/// without ever being terminal: stones are also what opens mines, so a total wipe could
/// otherwise leave a player unable to descend at all. It is also simply what an order like
/// this would do for its own.
/// </summary>
public sealed class FallenCache
{
    public required int MineSeed { get; init; }
    public required int Tier { get; init; }

    /// <summary>Which room the body is in, so the successor knows where to look.</summary>
    public required int RoomIndex { get; init; }

    public required int Stones { get; init; }

    /// <summary>Who it was. Flavour, but it is the only name a run ever gets.</summary>
    public string Name { get; init; } = string.Empty;

    public bool IsWorthFetching => Stones > 0;
}

/// <summary>What changed when one Deepankar replaced another.</summary>
public readonly record struct SuccessionResult(
    int Level,
    int UnspentXpCleared,
    int ItemsLost,
    int StonesLeftBehind)
{
    public bool LeftACache => StonesLeftBehind > 0;
}

/// <summary>
/// Death, and the person who takes the lamp afterwards.
///
/// The successor is a new person trained to the standard the order has reached but not yet
/// promoted: levels already earned are kept, progress toward the next is not. That is the
/// gentlest of the options that still costs something, and the only one that cannot produce a
/// wall — a player who dies repeatedly stops advancing but never goes backwards past a rank
/// they have held.
/// </summary>
public static class Succession
{
    /// <summary>The share of a pack that goes into the ground with its owner.</summary>
    public const float PackLost = 0.5f;

    /// <summary>
    /// Names for successors, in order. A run that ends is a person who died, and a list of
    /// names costs nothing while making the loss land as something other than a reset.
    /// </summary>
    private static readonly string[] Names =
    {
        "Ilamai", "Vetri", "Nandan", "Kavya", "Arul", "Thenral", "Selvan", "Maruthu",
        "Ezhil", "Pavai", "Kannan", "Amuda", "Velan", "Iniya", "Murugan", "Tamizh"
    };

    public static string NameFor(int index) =>
        Names[Math.Abs(index) % Names.Length];

    /// <summary>
    /// Bury one and raise the next.
    ///
    /// The equipped weapon and armour are deliberately not taken. "Half your gear" is the
    /// rule, but a successor who arrives unarmed cannot earn the stones needed to re-equip,
    /// and a loss that cannot be recovered from is the one thing this design must not produce.
    /// </summary>
    public static SuccessionResult Promote(PlayerCharacter player, RunResult run,
        int mineSeed, int roomIndex)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        var unspent = player.Vitals.Xp;
        var lost = HalveThePack(player.Inventory);

        player.Vitals.ClearUnspentXp();
        player.Vitals.FullRestore();
        player.Combat.ClearCombat();

        player.Legacy.Fall(new FallenCache
        {
            MineSeed = mineSeed,
            Tier = Math.Max(RunState.MinTier, run.Tier),
            RoomIndex = Math.Max(1, roomIndex),
            Stones = Math.Max(0, run.StonesLost),
            Name = player.Legacy.CurrentName
        });

        return new SuccessionResult(player.Vitals.Level, unspent, lost, run.StonesLost);
    }

    /// <summary>
    /// Half of every stack, rounded down, and the equipped kit untouched.
    ///
    /// Rounding down means a single potion is a single potion lost. That is the intended
    /// weight of a death; rounding the other way would make small packs immortal.
    /// </summary>
    private static int HalveThePack(Inventory inventory)
    {
        var lost = 0;

        foreach (var stack in inventory.Items.ToList())
        {
            // Keys are not loot. Losing the key to a door already opened would strand the
            // player behind their own progress.
            if (string.Equals(stack.Kind, "key", StringComparison.OrdinalIgnoreCase)) continue;

            var take = (int)MathF.Ceiling(stack.Count * PackLost);
            if (take <= 0) continue;

            inventory.Consume(stack.Id, take);
            lost += take;
        }

        return lost;
    }
}

/// <summary>
/// The line of Deepankars this save has spent, and what the last one left behind.
///
/// Kept apart from <see cref="PlayerVitals"/> because it outlives a character: the vitals
/// belong to whoever is currently holding the lamp, and this belongs to the save.
/// </summary>
public sealed class Legacy
{
    private int _generation;

    /// <summary>How many have died. The first Deepankar is generation zero.</summary>
    public int Generation => _generation;

    /// <summary>The body waiting to be found, if there is one.</summary>
    public FallenCache? Fallen { get; private set; }

    public string CurrentName => Succession.NameFor(_generation);

    public event Action? Changed;

    public void Fall(FallenCache cache)
    {
        // A second death before the first body is found replaces it. The design says the cache
        // is recoverable once; keeping a queue of them would turn a losing streak into a
        // stockpile waiting to be collected in one trip.
        Fallen = cache.IsWorthFetching ? cache : null;
        _generation++;
        Changed?.Invoke();
    }

    /// <summary>The cache has been picked up, or the mine it was in is gone for good.</summary>
    public void Recover()
    {
        if (Fallen is null) return;

        Fallen = null;
        Changed?.Invoke();
    }

    /// <summary>True when this descent is the one that can reach the body.</summary>
    public bool CanRecoverIn(int mineSeed) =>
        Fallen is { } cache && cache.MineSeed == mineSeed;

    public SavedLegacy Capture() => new()
    {
        Generation = _generation,
        MineSeed = Fallen?.MineSeed ?? 0,
        Tier = Fallen?.Tier ?? 0,
        RoomIndex = Fallen?.RoomIndex ?? 0,
        Stones = Fallen?.Stones ?? 0,
        Name = Fallen?.Name ?? string.Empty,
        HasFallen = Fallen is not null
    };

    public void Restore(SavedLegacy? saved)
    {
        _generation = Math.Max(0, saved?.Generation ?? 0);
        Fallen = saved is { HasFallen: true, Stones: > 0 }
            ? new FallenCache
            {
                MineSeed = saved.MineSeed,
                Tier = Math.Max(RunState.MinTier, saved.Tier),
                RoomIndex = Math.Max(1, saved.RoomIndex),
                Stones = saved.Stones,
                Name = saved.Name
            }
            : null;

        Changed?.Invoke();
    }

    public void Reset()
    {
        _generation = 0;
        Fallen = null;
        Changed?.Invoke();
    }
}

public sealed class SavedLegacy
{
    public int Generation { get; init; }
    public bool HasFallen { get; init; }
    public int MineSeed { get; init; }
    public int Tier { get; init; }
    public int RoomIndex { get; init; }
    public int Stones { get; init; }
    public string Name { get; init; } = string.Empty;
}
