using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RatnaBay.Domain;

namespace RatnaBay.Client;

/// <summary>
/// The weapon in the player's hand.
///
/// Drawn in the UI pass rather than the 3D one, which is how Daggerfall did it: the weapon
/// is a sprite at the edge of the screen, not a modelled object in the world, so it never
/// clips through a wall and never needs a rig. Pose and swing live on <see cref="WeaponView"/>;
/// this class only blits the resulting sprite.
/// </summary>
internal sealed class WeaponRenderer
{
    private readonly UiCanvas _ui;
    private readonly GraphicsDevice _device;

    public WeaponRenderer(UiCanvas ui, GraphicsDevice device)
    {
        _ui = ui;
        _device = device;
    }

    /// <param name="raisedShield">
    /// The shield currently held across the body, or null when the guard is down. A shield
    /// painted on screen at all times is furniture; one that appears when the guard goes up
    /// is feedback.
    /// </param>
    public void Draw(WeaponView view, WeaponDefinition weapon, ShieldDefinition? raisedShield)
    {
        // The hand replaces the weapon while a spell is going off. The player's hand is the
        // only part of them they ever see, and showing the sword through a cast made the most
        // distinctive thing a mage does look like the most ordinary thing a warrior does.
        var texture = view.IsCasting
            ? WeaponSprites.CastingHand(_device)
            : WeaponSprites.Get(_device, weapon);

        var pose = view.Pose();

        if (raisedShield is { } shield)
        {
            var shieldTexture = WeaponSprites.Shield(_device, shield);
            _ui.Batch.Draw(
                shieldTexture,
                UiLayout.ShieldGrip,
                null,
                Color.White,
                -0.12f,
                new Vector2(shieldTexture.Width / 2f, shieldTexture.Height),
                pose.Scale * 1.55f,
                SpriteEffects.None,
                0f);
        }

        // The grip, not the corner: rotating about the hand is what makes it swing rather
        // than spin.
        var origin = new Vector2(texture.Width / 2f, texture.Height);

        _ui.Batch.Draw(
            texture,
            pose.Position,
            null,
            Color.White,
            pose.Rotation,
            origin,
            pose.Scale,
            SpriteEffects.None,
            0f);
    }
}
