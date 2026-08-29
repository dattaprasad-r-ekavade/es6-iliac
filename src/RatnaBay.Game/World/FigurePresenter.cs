using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RatnaBay.Client.World;

/// <summary>
/// Camera-facing figures: speakers, watchers, enemies, bolts.
///
/// Game-specific: it picks Ratna Bay textures and tints. The primitive it calls —
/// <see cref="BillboardRenderer"/> — is not. A different game writes a different presenter
/// against the same renderer.
/// </summary>
internal sealed class FigurePresenter
{
    /// <summary>
    /// Bolts are camera-facing glows in the element's colour, so what is crossing the room is
    /// legible at a glance: orange is fire, pale blue is frost, gold is shock. Arrows are
    /// small, pale and fast, so they read as shafts, not spells.
    /// </summary>
    private static readonly Color ArrowColour = new(226, 214, 186);

    public void Draw(
        GraphicsDevice device,
        SceneRenderer scene,
        BillboardRenderer billboards,
        FirstPersonView camera,
        DialogueRuntime? dialogue,
        WatcherRuntime? watchers,
        Encounter? encounter,
        CaveTheme? cave)
    {
        DrawActors(device, scene, billboards, camera, dialogue);
        DrawWatchers(device, billboards, camera, watchers);
        DrawEnemies(device, billboards, camera, encounter, cave);
        DrawBolts(device, billboards, camera, encounter);
    }

    private static void DrawActors(
        GraphicsDevice device,
        SceneRenderer scene,
        BillboardRenderer billboards,
        FirstPersonView camera,
        DialogueRuntime? dialogue)
    {
        if (dialogue is null || dialogue.Actors.Count == 0) return;

        // The bracket the flame sits in. Small, dark, and entirely a silhouette at this scale.
        scene.DrawCube(new Vector3(-4.62f, 1.86f, -3.2f), new Vector3(0.5f, 0.16f, 0.34f),
            new Color(52, 48, 46), 0f);
        scene.DrawCube(new Vector3(-4.72f, 1.62f, -3.2f), new Vector3(0.26f, 0.42f, 0.24f),
            new Color(44, 41, 40), 0f);

        billboards.Begin(camera.View, camera.Projection);
        var sorted = new List<SpeakingActor>(dialogue.Actors);
        sorted.Sort((a, b) => DistanceSquared(camera.Position, b.Position)
            .CompareTo(DistanceSquared(camera.Position, a.Position)));

        foreach (var actor in sorted)
        {
            var texture = CharacterSprites.Get(device, $"dialogue.{actor.ActorId}",
                PaletteFor(actor.Palette));
            var feet = new Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
            billboards.Draw(texture, feet, actor.Height, camera.Yaw, Color.White);
        }

        RestoreWorldState(device);
    }

    private static void DrawWatchers(
        GraphicsDevice device,
        BillboardRenderer billboards,
        FirstPersonView camera,
        WatcherRuntime? watchers)
    {
        if (watchers is null || watchers.Watchers.Count == 0) return;

        billboards.Begin(camera.View, camera.Projection);
        foreach (var watcher in watchers.Watchers)
        {
            var texture = CharacterSprites.Get(device, $"watcher.{watcher.Definition.Id}",
                CharacterPalette.Guard);
            var feet = new Vector3(watcher.Position.X, watcher.Position.Y, watcher.Position.Z);
            var tint = watcher.LastSeen ? new Color(255, 168, 148) : Color.White;
            billboards.Draw(texture, feet, 1.85f, camera.Yaw, tint);
        }

        RestoreWorldState(device);
    }

    /// <summary>
    /// The enemies, as camera-facing sprites.
    ///
    /// Drawn far to near so the alpha-tested cutouts never punch a hole in something behind
    /// them that has not been drawn yet.
    /// </summary>
    private static void DrawEnemies(
        GraphicsDevice device,
        BillboardRenderer billboards,
        FirstPersonView camera,
        Encounter? encounter,
        CaveTheme? cave)
    {
        if (encounter is null || encounter.Enemies.Count == 0) return;

        billboards.Begin(camera.View, camera.Projection);

        var sorted = new List<Enemy>(encounter.Enemies);
        sorted.Sort((a, b) => DistanceSquared(camera.Position, b.Position)
            .CompareTo(DistanceSquared(camera.Position, a.Position)));

        foreach (var enemy in sorted)
        {
            var feet = encounter.DrawPositionOf(enemy);
            var tint = encounter.TintOf(enemy);

            // A chilled enemy is visibly cold, so frost reads as more than a slower walk.
            // The cave's own colour, lightly. A quarter rather than a wash, because the three
            // tiers of risen are told apart by their flesh colours and a full tint would make
            // a chhaya in the Ossuary look like a vetala anywhere else. The spec asks for a
            // preta set per theme; a palette shift is what that means in a game whose art is
            // generated rather than drawn.
            if (cave is not null)
                tint = Color.Lerp(tint,
                    new Color(cave.Accent.R, cave.Accent.G, cave.Accent.B), 0.25f);

            // Burning had no tint at all, which was a gap for Flame long before Cinder
            // existed: a spell whose whole identity is "lowest burst, highest total once it
            // burns" was invisible for the entire part that matters.
            if (enemy.IsBurning)
                tint = new Color(Math.Min(255, tint.R / 2 + 160), tint.G / 2 + 78, tint.B / 3);

            // Chill wins when both apply. Two tints averaged is a third colour that means
            // neither, and frost is the one the player has to react to.
            if (enemy.IsChilled) tint = new Color(tint.R / 2 + 90, tint.G / 2 + 110, tint.B);

            billboards.Draw(SpriteFor(device, enemy), feet, encounter.DrawHeightOf(enemy),
                camera.Yaw, tint);
        }

        RestoreWorldState(device);
    }

    private static void DrawBolts(
        GraphicsDevice device,
        BillboardRenderer billboards,
        FirstPersonView camera,
        Encounter? encounter)
    {
        if (encounter is null) return;

        var shots = encounter.Shots.ToList();
        if (encounter.Bolts.Count == 0 && shots.Count == 0) return;

        billboards.Begin(camera.View, camera.Projection);

        foreach (var shot in shots)
        {
            billboards.Draw(BoltSprites.Get(device, ArrowColour),
                shot.Position, 0.2f, camera.Yaw, Color.White);
        }

        foreach (var bolt in encounter.Bolts)
        {
            var texture = BoltSprites.Get(device, bolt.Colour);

            // A pulse so a bolt reads as burning energy rather than a thrown pebble.
            var pulse = 0.52f + MathF.Sin(bolt.Spin) * 0.06f;
            billboards.Draw(texture, bolt.Position - Vector3.Up * (pulse * 0.5f), pulse,
                camera.Yaw, Color.White);
        }

        RestoreWorldState(device);
    }

    /// <summary>
    /// The sprite an enemy is drawn with.
    ///
    /// Every enemy used to be a bandit. That was survivable while there was one kind of thing
    /// to fight and became untenable the moment depth started sending different ones: the
    /// whole reason tiers exist is that a room announces how hard it is before the fight
    /// starts, and it cannot do that if the hard thing looks like the easy thing.
    /// </summary>
    private static Texture2D SpriteFor(GraphicsDevice device, Enemy enemy)
    {
        var id = enemy.Archetype.Id;

        var risen = ItemSprites.Risen(device, id);
        if (risen is not null) return risen;

        return id == EnemyCatalog.ArcherId
            ? CharacterSprites.Get(device, "bandit_archer", CharacterPalette.Guard)
            : CharacterSprites.Get(device, "bandit", CharacterPalette.Bandit);
    }

    private static CharacterPalette PaletteFor(string? palette) => palette?.ToLowerInvariant() switch
    {
        "guard" => CharacterPalette.Guard,
        "merchant" => CharacterPalette.Merchant,
        "bandit" => CharacterPalette.Bandit,
        "wolf" => CharacterPalette.Wolf,
        _ => CharacterPalette.Citizen
    };

    private static float DistanceSquared(Vector3 camera, WorldPoint point) =>
        Vector3.DistanceSquared(camera, new Vector3(point.X, point.Y, point.Z));

    /// <summary>The billboard pass leaves its own render state behind; the world expects the default.</summary>
    private static void RestoreWorldState(GraphicsDevice device)
    {
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
    }
}
