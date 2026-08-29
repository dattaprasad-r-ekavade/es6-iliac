using Microsoft.Xna.Framework.Input;
using RatnaBay.Client.Input;
using RatnaBay.Domain;
using RatnaBay.Engine.Input;

namespace RatnaBay.Client.Session;

/// <summary>
/// The shut door, and the question it asks.
///
/// This is the one decision the whole game is built on — bank what is in the pot, or open the
/// door and risk it — so the three things that happen at a door are kept together rather than
/// spread through a frame update: the player is told what the door means the first time they
/// meet one, the clock on their answer starts, and the answer is read.
///
/// Deliberately does not act on the answer. <c>Game1</c> still owns what camping and pressing
/// on do, because those move stones between a run and a save. This type owns only the moment
/// of asking, which is the part that has to be identical every time — nine recorded sessions
/// went into making the timing of that clock trustworthy, and it is measured from the first
/// frame the panel is up rather than from the frame the door opened.
/// </summary>
internal sealed class DoorPrompt
{
    private bool _offered;

    /// <summary>
    /// Ask, if there is a door. Returns what the player answered, or nothing.
    /// </summary>
    /// <param name="run">The live run, or null when there is none.</param>
    public DoorAction Step(RunRuntime? run, InputRouter input, KeyboardState keyboard,
        Coach coach, PlayRecorder recorder, PlayerVitals? vitals)
    {
        if (run is not { AtDecision: true } || vitals is null)
        {
            // Reset only when a run exists. Clearing it with no run at all would re-arm the
            // clock on a screen that has no door on it.
            if (run is not null) _offered = false;
            return DoorAction.None;
        }

        // Explained the first time it is actually in front of somebody with stones in the pot,
        // rather than on the way past a door that is worth nothing yet.
        coach.Teach(Lessons.FirstDoor, Lessons.TextOf(Lessons.FirstDoor));
        if (run.Run.CanCallTrader) coach.Teach(Lessons.Trader, Lessons.TextOf(Lessons.Trader));

        // The clock on the answer starts the first frame the panel is up. A phantom second
        // "offered" event once made the summariser report an 11.8 second hesitation that never
        // happened, so this fires exactly once per door.
        if (!_offered)
        {
            _offered = true;
            recorder.Record(PlayEventKind.DecisionOffered,
                $"after {run.Run.RoomsCleared} rooms",
                run.Run.Pending, run.Run.NextRoomPays, vitals.Health, vitals.Prana);
        }

        return WorldPanelInput.StepDoor(input, keyboard, run.Run.CanCallTrader, run.Run.CanPressOn);
    }
}
