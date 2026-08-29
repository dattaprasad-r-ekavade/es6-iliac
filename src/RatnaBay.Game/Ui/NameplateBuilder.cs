using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System.Collections.Generic;

namespace RatnaBay.Client.Ui;

/// <summary>
/// Turns a live encounter into the plates drawn over it.
///
/// Lives beside <see cref="WorldHudBuilder"/> and follows the same rule: the coordinator hands
/// over a camera and a projector, this decides what a player should be able to read off the
/// room, and <see cref="MarkerRenderer"/> paints it without ever meeting an <c>Enemy</c>.
///
/// The projection is done here rather than inside the renderer so a plate can never be
/// projected with a different frame's camera than the one it is drawn over.
/// </summary>
internal static class NameplateBuilder
{
    /// <summary>Everything alive, close enough to matter, furthest first.</summary>
    public static List<NameplateState> Build(
        Encounter encounter, WorldProjector projector, Vector3 camera)
    {
        var plates = new List<NameplateState>();
        if (encounter.Enemies.Count == 0) return plates;

        // Far ones first, so a nearer plate overlaps a further one rather than the reverse.
        var sorted = new List<Enemy>(encounter.Enemies);
        sorted.Sort((a, b) => SquaredTo(camera, b).CompareTo(SquaredTo(camera, a)));

        foreach (var enemy in sorted)
        {
            if (!enemy.IsAlive) continue;

            var distance = MetresTo(camera, enemy);
            if (distance > MarkerRenderer.NameplateRange) continue;

            var feet = encounter.DrawPositionOf(enemy);
            var head = feet + Vector3.Up * (encounter.DrawHeightOf(enemy) + 0.34f);
            if (!projector.TryProject(head, out var anchor)) continue;

            // Shrink with distance, but never past readable. A plate that scales all the way
            // down is unreadable exactly when a player most wants to know what is coming.
            var scale = MathHelper.Clamp(
                1.25f - distance / MarkerRenderer.NameplateRange, 0.62f, 1f);

            plates.Add(new NameplateState(
                Anchor: anchor,
                Scale: scale,
                // Always, and labelled.
                //
                // It was hidden at level one and drawn as a bare number after a dot, so the
                // shallow rooms showed nothing and the deep ones showed "Bandit · 4" -- which
                // could be a level, a count, or a rank. Now that every body rolls its own
                // level out of a band, the number is the main thing a player reads off a room
                // on entry: five bandits at Lv 3 and one at Lv 6 is a different room from six
                // at Lv 3, and it should be legible from the doorway.
                Label: $"{enemy.DisplayName}   Lv {enemy.Archetype.Level}",
                Status: StatusOf(encounter, enemy),
                HealthFraction: MathHelper.Clamp(enemy.Health / enemy.MaxHealth, 0f, 1f),
                Vulnerable: enemy.IsVulnerable,
                Focused: ReferenceEquals(encounter.Focused, enemy)));
        }

        return plates;
    }

    /// <summary>
    /// Everything currently true of an enemy, in the order it matters to the player.
    ///
    /// Striking first because it is the one with a deadline attached — it is the moment to
    /// guard. Then what is being done to it, which is what tells a player their last spell or
    /// stone did something and is still doing it.
    ///
    /// Every state that is true, not the first one. This was a priority chain, so a burning
    /// enemy that got staggered stopped saying it was burning. The burn was still running —
    /// Enemy.Tick counts it down and applies it whatever else is happening — but the readout
    /// said otherwise, and a player reasonably concluded the stagger had cancelled it. An
    /// effect the player cannot see is an effect they will not build on.
    /// </summary>
    private static string StatusOf(Encounter encounter, Enemy enemy)
    {
        var states = new List<string>(4);

        if (encounter.IsLunging(enemy)) states.Add("striking");
        if (enemy.IsStaggered) states.Add("staggered");
        if (enemy.IsBurning) states.Add("burning");
        if (enemy.IsChilled) states.Add("chilled");

        return string.Join(" · ", states);
    }

    /// <summary>Squared, for sorting only. Anything compared against metres wants MetresTo.</summary>
    private static float SquaredTo(Vector3 camera, Enemy enemy) =>
        Vector3.DistanceSquared(camera, At(enemy));

    /// <summary>
    /// Real metres to an enemy.
    ///
    /// The nameplate code was using the squared form against a range of 26, so a plate only
    /// appeared within the square root of that — about five metres — and the distance-based
    /// shrink hit its floor almost immediately. The level of the thing walking at you was
    /// therefore unreadable until it was already on top of you, which is precisely when
    /// nobody has time to read it.
    /// </summary>
    private static float MetresTo(Vector3 camera, Enemy enemy) =>
        Vector3.Distance(camera, At(enemy));

    private static Vector3 At(Enemy enemy) =>
        new(enemy.Position.X, enemy.Position.Y, enemy.Position.Z);
}
