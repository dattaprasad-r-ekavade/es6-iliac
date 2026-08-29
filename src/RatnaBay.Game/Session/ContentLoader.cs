using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RatnaBay.Client.Session;

/// <summary>
/// Loads authored manifests into the live play state: dialogue, watchers, pockets, pickups,
/// the stall, and quests.
///
/// Game1 still owns what happens after a buy or a lifted pocket. This type must not take a
/// <c>Game1</c> reference.
/// </summary>
internal sealed class ContentLoader
{
    private readonly PlayState _play;
    private readonly IList<string> _errors;

    public ContentLoader(PlayState play, IList<string> errors)
    {
        _play = play;
        _errors = errors;
    }

    public void LoadDialogueManifest()
    {
        if (_play.Session is null) return;

        // Nobody is standing about in a generated mine.
        //
        // The dialogue manifest carries its own actor positions, authored against the
        // Northwatch scene, and it was loaded on entering any world at all. Those fixed
        // coordinates then landed wherever they happened to land inside a cave, so Mara and
        // Vesa were waiting underground offering to talk about the old road -- the pivot left
        // them behind and nothing ever told them to go home.
        //
        // The yard is built in code and has its own trader, so a descent needs no actors at
        // all. Cleared rather than left stale, because a mine entered from the surface would
        // otherwise inherit whoever was loaded up there.
        if (_play.MineSeed is not null)
        {
            _play.Dialogue = null;
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Dialogue", "northwatch.json");
        if (!DialogueRuntime.TryLoad(path, _play.Session.Player.Dialogue, out var dialogue, out var error))
        {
            _errors.Add(error);
            _play.Dialogue = null;
            return;
        }

        _play.Dialogue = dialogue;
    }

    public void LoadWatchers()
    {
        if (_play.Session is null || _play.World is null) return;
        if (_play.Watchers is null)
            _play.Watchers = new WatcherRuntime(_play.World.Manifest, _play.World.Collision,
                _play.Session.Player.Detection);
        else
            _play.Watchers.Reload(_play.World.Manifest);
    }

    public void LoadPockets()
    {
        _play.Pockets.Clear();

        // Parked. Building no targets is what switches the whole feature off: the prompt, the
        // key and the action all read from this and all find nothing.
        if (!ParkedFeatures.Pickpocketing) return;

        if (_play.Session is null || _play.Dialogue is null) return;

        foreach (var actor in _play.Dialogue.Actors)
        {
            var pocket = _play.Dialogue.PocketOf(actor.ActorId);
            var alreadyLifted = _play.Session.Player.Story.State.LootedObjects.Contains(
                $"pickpocket.{actor.ActorId}", StringComparer.Ordinal);

            // Contents come from the manifest so a pocket can hold something that matters —
            // the watchpost key rather than a nameless purse.
            var contents = alreadyLifted || pocket is null
                ? Array.Empty<ItemStack>()
                : pocket.Items.Select(item => new ItemStack
                {
                    Id = item.Id, Name = item.Name, Kind = item.Kind, Count = item.Count
                }).ToArray();

            _play.Pockets[actor.ActorId] = new PickpocketTarget(pocket?.Difficulty ?? 0f, contents);
        }
    }

    public void LoadPickups()
    {
        _play.Pickups.Clear();
        if (_play.Session is null || _play.World is null) return;

        foreach (var pickup in _play.World.Manifest.Pickups ?? new List<WorldPickup>())
        {
            if (_play.Session.Player.Story.State.LootedObjects.Contains(
                    $"pickup.{pickup.Id}", StringComparer.Ordinal))
                continue;

            _play.Pickups.Add(pickup);
        }
    }

    /// <summary>Put the gear back on the shelf, in memory and in the save.</summary>
    public void RestockTheStall()
    {
        if (_play.Shop is null || _play.Session is null) return;

        foreach (var itemId in _play.Shop.Restock())
            _play.Session.Player.Story.ForgetLooted($"shop.{_play.Shop.Definition.Id}.{itemId}");
    }

    public void LoadShop()
    {
        if (_play.Session is null) return;
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Shops", "northwatch.json");
        if (!ShopManifest.TryLoad(path, out var manifest, out var error))
        {
            _errors.Add(error);
            _play.Shop = null;
            return;
        }

        var definition = manifest!.ToDefinitions().FirstOrDefault();
        if (definition is null)
        {
            _play.Shop = null;
            return;
        }

        _play.Shop = new Shop(definition);
        foreach (var item in definition.Items)
            if (_play.Session.Player.Story.State.LootedObjects.Contains(
                    $"shop.{definition.Id}.{item.Id}", StringComparer.Ordinal))
                _play.Shop.MarkSoldOut(item.Id);
    }

    public void LoadQuestManifest()
    {
        if (_play.Session is null) return;

        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Quests", "northwatch.json");
        if (!QuestManifest.TryLoad(path, out var manifest, out var error))
        {
            _errors.Add(error);
            return;
        }

        _play.Session.Player.Quests.RegisterRange(manifest!.ToDefinitions());
    }
}
