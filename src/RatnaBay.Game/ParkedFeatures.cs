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
}
