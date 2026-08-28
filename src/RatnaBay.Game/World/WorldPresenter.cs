using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// Turns a live world into boxes, lights and imported props.
///
/// Game-specific: it knows about caves, the yard, pickups and the authored manifest.
/// The primitives it calls — <see cref="SceneRenderer"/>, <see cref="ModelCache"/> — are not.
/// A different game would write a different presenter against the same two types.
/// </summary>
internal sealed class WorldPresenter
{
    public void Draw(
        GraphicsDevice device,
        SceneRenderer scene,
        ModelCache models,
        WorldRuntime? world,
        IReadOnlyList<WorldPickup> pickups,
        bool onTheSurface,
        CaveTheme? cave,
        Matrix view,
        Matrix projection,
        List<PointLight> lights,
        ref StoneTextures.StonePalette stone)
    {
        if (world is null)
        {
            device.Clear(new Color(40, 58, 68));
            return;
        }

        device.Clear(new Color(96, 121, 136));

        // The manifest has carried a light per room since the generator was written, and
        // nothing ever read them: BasicEffect had no point lights to put them in. They cost
        // nothing to honour now.
        lights.Clear();
        foreach (var light in world.Manifest.Lights ?? new List<WorldLight>())
        {
            var position = light.Position.ToWorldPoint();
            // Lanterns are for a mine, where they are the only light there is. In the yard
            // they sit on top of daylight and a warm key, and at full strength they burned the
            // ground under the player to flat white -- which is why the camp looked washed out
            // and why the bottom of the shaft glowed. A quarter is enough to say a lamp is lit.
            var strength = MathHelper.Clamp(light.Intensity, 0f, 8f) * (onTheSurface ? 0.5f : 2.1f);

            lights.Add(new PointLight(
                new Vector3(position.X, position.Y, position.Z),
                ToXna(light.Color).ToVector3() * strength,
                MathF.Max(0.5f, light.Range)));
        }

        // Daylight above ground, and the mine's dark below it.
        //
        // The cave lighting was applied to everywhere, so the yard came out lit and textured
        // like an interior with a sky pasted over it. Coming up out of a mine should not look
        // like walking into another room — that contrast is the entire reason the surface
        // exists, and it is carried almost completely by the light.
        if (onTheSurface)
        {
            stone = StoneTextures.StonePalette.Sandstone;
            scene.SetCaveAmbience(
                ambient: new Vector3(0.52f, 0.54f, 0.60f),
                keyDirection: new Vector3(-0.35f, -1f, -0.28f),
                keyColour: new Vector3(0.86f, 0.78f, 0.62f));
        }
        else
        {
            // The cave's own rock. Derived from the seed, so it matches what the shaft screen
            // promised without either side storing the answer.
            stone = cave is null
                ? StoneTextures.StonePalette.Granite
                : StoneTextures.StonePalette.FromTheme(cave);

            scene.SetCaveAmbience(
                ambient: new Vector3(0.10f, 0.10f, 0.12f),
                keyDirection: new Vector3(-0.4f, -1f, -0.25f),
                keyColour: new Vector3(0.20f, 0.20f, 0.26f));
        }

        foreach (var geometry in world.Manifest.Geometry ?? new List<WorldGeometry>())
        {
            if (!geometry.Visible) continue;
            scene.DrawWorldBox(Vec(geometry.Min), Vec(geometry.Max), ToXna(geometry.Color),
                geometry.Material);
        }

        foreach (var door in world.Doors)
        {
            if (door.Lock.IsOpen) continue;
            scene.DrawWorldBox(Vec(door.Definition.Min), Vec(door.Definition.Max),
                ToXna(door.Definition.Color));
        }

        foreach (var prop in world.Manifest.Props ?? new List<WorldProp>())
        {
            if (!prop.Visible) continue;
            var position = prop.Position.ToWorldPoint();
            models.Draw(prop.Model, new Vector3(position.X, position.Y, position.Z),
                prop.Scale, prop.Rotation, view, projection);
        }

        foreach (var pickup in pickups)
        {
            var position = pickup.Position.ToWorldPoint();
            models.Draw(pickup.Model, new Vector3(position.X, position.Y, position.Z),
                pickup.Scale, 0f, view, projection);
        }
    }

    private static Vector3 Vec(WorldVector v) => new(v.X, v.Y, v.Z);

    private static Color ToXna(WorldColor color) => new(
        (byte)MathHelper.Clamp(color.R, 0, 255),
        (byte)MathHelper.Clamp(color.G, 0, 255),
        (byte)MathHelper.Clamp(color.B, 0, 255),
        (byte)MathHelper.Clamp(color.A, 0, 255));
}
