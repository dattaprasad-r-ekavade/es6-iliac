using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client.Ui;

/// <summary>
/// Builds the world prompt snapshot from the live session.
///
/// Lives beside <see cref="PromptState"/> so PromptRenderer only paints the chips it is given.
/// </summary>
internal static class PromptBuilder
{
    public static PromptState Build(
        GameSession? session,
        FirstPersonView camera,
        bool onTheSurface,
        DialogueRuntime? dialogue,
        Shop? shop,
        WorldRuntime? world,
        RunRuntime? run,
        IReadOnlyDictionary<string, PickpocketTarget> pockets,
        WorldPickup? pickup)
    {
        if (session is null) return PromptState.Empty;

        var player = new WorldPoint(camera.Position.X, camera.Position.Y, camera.Position.Z);
        var chips = new List<PromptChip>();

        if (onTheSurface)
        {
            var fixture = Surface.FixtureAt(player);
            if (fixture == SurfaceFixture.None) return PromptState.Empty;

            var stones = session.Player.Inventory.CountOf(SoulCrystals.LesserId);
            var line = fixture switch
            {
                SurfaceFixture.Shaft => $"E  Open a shaft   ({stones} stones)",
                SurfaceFixture.Trader => "E  Trade",
                _ => "E  Read the carving"
            };

            chips.Add(new PromptChip(line, UiLayout.SinglePrompt, PromptRole.Interact));
            return new PromptState(chips);
        }

        var actor = dialogue?.FindActor(player, camera.Yaw);
        if (actor is not null)
        {
            chips.Add(new PromptChip($"Click / E  Talk to {actor.DisplayName}",
                UiLayout.TalkPrompt, PromptRole.Talk, Fit: true));

            if (actor.Palette.Equals("merchant", StringComparison.OrdinalIgnoreCase)
                && shop is not null)
            {
                chips.Add(new PromptChip("B  Shop", UiLayout.SecondaryPrompt, PromptRole.Interact));
            }

            // A pocket worth picking was previously only advertised on guards, so the one
            // pocket in the slice that matters — the trader carrying the watchpost key —
            // had no prompt at all and testers never found it.
            if (HasPickablePocket(actor, pockets))
            {
                chips.Add(new PromptChip("P  Pick pocket", UiLayout.PickpocketPrompt, PromptRole.Pocket));
            }

            return new PromptState(chips);
        }

        if (pickup is not null)
        {
            chips.Add(new PromptChip($"Click / E  Take {pickup.Name} x{pickup.Count}",
                UiLayout.SinglePrompt, PromptRole.Talk, Fit: true));
            return new PromptState(chips);
        }

        // The camp decision is a bigger question about the same door; two prompts on one
        // doorway would just be noise.
        if (world is null || run is { AtDecision: true }) return PromptState.Empty;
        var door = world.FindDoor(player, camera.Yaw);
        if (door is null) return PromptState.Empty;

        var hasKey = !string.IsNullOrEmpty(door.Definition.KeyItemId)
            && session.Player.Inventory.Has(door.Definition.KeyItemId);

        if (run is { BarsTheWay: true })
        {
            chips.Add(new PromptChip("Barred  |  clear this room first",
                UiLayout.SinglePrompt, PromptRole.Barred));
            return new PromptState(chips);
        }

        var text = !door.Lock.IsLocked ? "Click / E  Open door"
            : hasKey ? "Click / E  Unlock with your key"
            : $"Locked  |  a key, or Security {door.Definition.Difficulty:0}";
        chips.Add(new PromptChip(text, UiLayout.SinglePrompt, PromptRole.Interact));
        return new PromptState(chips);
    }

    private static bool HasPickablePocket(SpeakingActor actor,
        IReadOnlyDictionary<string, PickpocketTarget> pockets) =>
        ParkedFeatures.Pickpocketing
        && pockets.TryGetValue(actor.ActorId, out var target) && target.RemainingItems > 0;
}
