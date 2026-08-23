using System;

namespace RatnaBay.Domain;

public enum RunOutcome
{
    /// <summary>Still down there.</summary>
    InProgress,

    /// <summary>Banked at a cleared room's exit and walked out.</summary>
    Camped,

    /// <summary>Did not.</summary>
    Died
}

/// <summary>What a run was worth, once it is over.</summary>
public readonly record struct RunResult(
    RunOutcome Outcome,
    int RoomsCleared,
    int StonesCarriedOut,
    int StonesLost,
    int Tier)
{
    public bool Survived => Outcome == RunOutcome.Camped;
}

/// <summary>
/// The ledger for one descent, and the decision at the heart of the game.
///
/// The only rule that matters here is that the payout rises with depth. A flat reward per room
/// makes banking immediately correct at every step — the pot grows while the prize does not, so
/// the question "one more room?" has a known answer and stops being a question. The Nth room of
/// a tier-T mine paying <c>N x T</c> is what keeps it open all the way down.
///
/// This knows nothing about geometry. The game layer tells it when a room is clear and when the
/// player is standing at the exit; it owns the arithmetic and the rules about when a run may
/// end, which are exactly the parts worth testing without a renderer attached.
/// </summary>
public sealed class RunState
{
    /// <summary>Cheapest mine. Tiers are the depth the player paid to reach.</summary>
    public const int MinTier = 1;

    private RunState() { }

    public int Seed { get; private set; }
    public int Tier { get; private set; } = MinTier;

    /// <summary>Payable rooms in this mine. The entrance is not one of them.</summary>
    public int Rooms { get; private set; }

    public int RoomsCleared { get; private set; }

    /// <summary>Stones earned but not yet banked. All of it is at risk.</summary>
    public int Pending { get; private set; }

    public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

    /// <summary>True while the run can still be pressed or banked.</summary>
    public bool IsActive => Outcome == RunOutcome.InProgress;

    /// <summary>Set by the game layer when nothing is left alive in the current room.</summary>
    public bool RoomIsClear { get; private set; }

    /// <summary>True when every payable room has been cleared; there is nothing left to press.</summary>
    public bool IsExhausted => RoomsCleared >= Rooms;

    public event Action? Changed;

    /// <summary>Raised when a room is cleared, with what it paid.</summary>
    public event Action<int>? RoomCleared;

    public static RunState Begin(int seed, int tier, int rooms)
    {
        return new RunState
        {
            Seed = seed,
            Tier = Math.Max(MinTier, tier),
            Rooms = Math.Max(1, rooms),

            // The entrance room counts as already cleared: it is empty by design, and the
            // player has to be able to open the first door without fighting anything.
            RoomIsClear = true
        };
    }

    /// <summary>What the Nth room of a tier-T mine pays.</summary>
    public static int PayoutFor(int roomNumber, int tier) =>
        Math.Max(0, roomNumber) * Math.Max(MinTier, tier);

    /// <summary>
    /// What clearing the next room would add. Zero once the mine is exhausted, which is how
    /// the game layer knows to offer the way out rather than another door.
    /// </summary>
    public int NextRoomPays => IsExhausted ? 0 : PayoutFor(RoomsCleared + 1, Tier);

    /// <summary>
    /// What is being staked against what is being played for. This is the number the camp
    /// screen exists to put in front of the player: at three rooms it is 1.5 : 1, at eight it
    /// is 4 : 1, and watching it climb is the pressure the whole loop runs on.
    /// </summary>
    public float RiskRatio => NextRoomPays <= 0 ? 0f : Pending / (float)NextRoomPays;

    /// <summary>
    /// Camping is offered only at the exit of a cleared room.
    ///
    /// If the player could bank the instant a fight turned, the mechanic collapses into "always
    /// bank when losing" and there is no risk left to press. Committing at the door is the
    /// decision: once it opens, you are in that room until it is clear or you are not.
    /// </summary>
    public bool CanCamp => IsActive && RoomIsClear && RoomsCleared > 0;

    /// <summary>Pressing on is only a choice while there is another room to press into.</summary>
    public bool CanPressOn => IsActive && RoomIsClear && !IsExhausted;

    /// <summary>The player has walked into the next room and the door has shut behind them.</summary>
    public void EnterRoom()
    {
        if (!IsActive || !RoomIsClear) return;

        RoomIsClear = false;
        Changed?.Invoke();
    }

    /// <summary>Nothing left alive in this room. Pays out, but only into the pending pot.</summary>
    public int ClearRoom()
    {
        if (!IsActive || RoomIsClear || IsExhausted) return 0;

        RoomsCleared++;
        var paid = PayoutFor(RoomsCleared, Tier);
        Pending += paid;
        RoomIsClear = true;

        RoomCleared?.Invoke(paid);
        Changed?.Invoke();
        return paid;
    }

    /// <summary>Bank the pot and end the run here.</summary>
    public RunResult Camp()
    {
        if (!CanCamp) return Result(RunOutcome.InProgress, carried: 0, lost: 0);

        var carried = Pending;
        Pending = 0;
        Outcome = RunOutcome.Camped;

        Changed?.Invoke();
        return Result(RunOutcome.Camped, carried, lost: 0);
    }

    /// <summary>
    /// Death forfeits the pot entirely.
    ///
    /// It is recoverable: the fallen Deepankar's cache is found once on the next descent into
    /// this mine. That is succession's job, not this one — but the amount is recorded here
    /// because this is the only place that still knows it.
    /// </summary>
    public RunResult Die()
    {
        if (!IsActive) return Result(Outcome, carried: 0, lost: 0);

        var lost = Pending;
        Pending = 0;
        Outcome = RunOutcome.Died;

        Changed?.Invoke();
        return Result(RunOutcome.Died, carried: 0, lost);
    }

    private RunResult Result(RunOutcome outcome, int carried, int lost) =>
        new(outcome, RoomsCleared, carried, lost, Tier);

    // ------------------------------------------------------------------ persistence

    public SavedRun Capture() => new()
    {
        Seed = Seed,
        Tier = Tier,
        Rooms = Rooms,
        RoomsCleared = RoomsCleared,
        Pending = Pending,
        RoomIsClear = RoomIsClear,
        Outcome = Outcome.ToString()
    };

    public static RunState? Restore(SavedRun? saved)
    {
        if (saved is null || saved.Rooms <= 0) return null;

        var run = Begin(saved.Seed, saved.Tier, saved.Rooms);
        run.RoomsCleared = Math.Clamp(saved.RoomsCleared, 0, run.Rooms);
        run.Pending = Math.Max(0, saved.Pending);
        run.RoomIsClear = saved.RoomIsClear;
        run.Outcome = Enum.TryParse<RunOutcome>(saved.Outcome, out var outcome)
            ? outcome
            : RunOutcome.InProgress;

        return run;
    }
}

public sealed class SavedRun
{
    public int Seed { get; init; }
    public int Tier { get; init; } = RunState.MinTier;
    public int Rooms { get; init; }
    public int RoomsCleared { get; init; }
    public int Pending { get; init; }
    public bool RoomIsClear { get; init; }
    public string Outcome { get; init; } = nameof(RunOutcome.InProgress);
}
