namespace RatnaBay.Client;

/// <summary>
/// Things that are built, tested, and deliberately not reachable.
///
/// Parking is not deleting. The domain rules and their tests stay exactly where they are and
/// keep running, so a parked feature cannot rot: if it is ever switched back on it works, and
/// if somebody breaks it in passing the build says so that day rather than a year later.
///
/// What a switch removes is the player-facing surface — the key, the prompt, the line in the
/// controls overlay. A feature nobody can reach costs nothing to keep and a great deal to
/// rewrite, and the reason it was parked is written next to the switch so the decision can be
/// argued with rather than rediscovered.
/// </summary>
public static class ParkedFeatures
{
    /// <summary>
    /// Pickpocketing.
    ///
    /// **Parked 2026-08-25.** It was built for a town full of people to move through, and the
    /// pivot to a run loop took that away: a descent has nothing with pockets in it, and the
    /// yard has one trader you are meant to trade with rather than rob. Testers never found it
    /// even when it was the only route to a key, which was the first sign it had no home.
    ///
    /// **What stays:** <see cref="RatnaBay.Domain.Pickpocketing"/>, its rules, and its tests.
    /// It shares the Security skill with lockpicking, so nothing is orphaned by switching it
    /// off — Security is still trained and still used.
    ///
    /// **When it might come back:** the fort. Ten rooms with occupants who will not talk to you
    /// until you have rank or gold is exactly the situation this was written for, and that is
    /// iteration 19. Turning it back on is this line and nothing else.
    /// </summary>
    /// <remarks>
    /// A field rather than a constant on purpose. A <c>const false</c> folds at compile time
    /// and every guarded line becomes provably dead, which fills a warning-clean build with
    /// unreachable-code noise and buries the warnings that matter.
    /// </remarks>
    public static readonly bool Pickpocketing = false;

    /// <summary>
    /// Lockpicking, and the Security skill that exists only to serve it.
    ///
    /// **Parked 2026-08-25.** Nothing in the live game makes a lock worth picking. Mine doors
    /// are shut rather than locked, deliberately — the gate on pressing deeper is meant to be
    /// the player's nerve, not their Security skill — and the yard has no doors at all. The
    /// one difficulty-fifteen door left in the game is in the authored world, which the run
    /// loop no longer loads.
    ///
    /// So this was already dormant by content before it was ever switched off here. What the
    /// switch does is stop the game showing a skill that cannot be trained: with picking and
    /// pickpocketing both parked, nothing anywhere reports use of Security.
    ///
    /// **What stays:** <see cref="RatnaBay.Domain.Lockable"/> in full. Every door in the game
    /// is one, locked or not, and the picking path still works if content ever asks for it.
    ///
    /// **When it might come back:** with the fort, alongside pickpocketing, or the first time
    /// a mine wants a strongroom that costs something to open.
    /// </summary>
    public static readonly bool Lockpicking = false;

    /// <summary>
    /// Sneaking, and the Stealth skill.
    ///
    /// **Parked 2026-08-25.** Not a decision so much as an observation: Stealth is read by the
    /// detection system and trained by nothing at all, and generated mines place no watchers
    /// for it to hide from. Crouching still works and still makes the player harder to see —
    /// it is only the skill that is dead, and it has been dead since it was written.
    ///
    /// **The reason it will probably not come back at all**, which is worth writing down
    /// rather than rediscovering: this game pays you *per room cleared*, and the door shuts
    /// behind you until the room is empty. Stealth's whole proposition is avoiding the fight.
    /// Here the fight is the income and the room will not let you leave without it, so there
    /// is nothing to sneak past — everything has to die for you to be paid.
    ///
    /// Stealth earns its place in roguelikes where avoidance reaches the objective another
    /// way, or where it is a passive wake-up stat rather than a verb. This design has no
    /// objective except clearing, and a passive stat governing watchers that do not exist is
    /// not a mechanic.
    ///
    /// **What is worth keeping from it** is the ambush, and that belongs to combat rather than
    /// to stealth: a room's occupants already rise when it is entered, so there is a window in
    /// which they cannot fight back. Rewarding a blow landed in it is the one good idea here,
    /// wearing the right clothes.
    ///
    /// **When it might come back:** watchers in the fort, if the fort ever wants them.
    /// </summary>
    public static readonly bool Sneaking = false;

    /// <summary>
    /// Whether a skill is worth showing on the character sheet.
    ///
    /// A skill nothing trains is worse than no skill: it reads as progress that is not
    /// happening, and a player who spends a run trying to raise it has been misled by the
    /// interface rather than beaten by the game.
    /// </summary>
    public static bool SkillIsLive(string skillId) => skillId switch
    {
        RatnaBay.Domain.Skills.Security => Lockpicking || Pickpocketing,
        RatnaBay.Domain.Skills.Stealth => Sneaking,
        _ => true
    };
}
