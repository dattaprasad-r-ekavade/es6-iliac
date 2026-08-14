using UnityEngine;

/// <summary>
/// The sky, as flat painted bands.
///
/// Until now <see cref="ArtDirection.ApplyEnvironment"/> disabled the skybox and let the camera
/// clear to the fog colour, so the top half of every outdoor frame was one flat band of khaki —
/// the same value as the ground haze, with no horizon at all. That reads as a wall, not a sky,
/// and it was the most damaging thing in the first playthrough capture.
///
/// The fix stays inside the art direction rather than reaching for a gradient: miniature
/// painting renders sky as **stacked flat registers**, often a deep band above and a pale or
/// gilded strip at the horizon, with a hard edge between them. So this quantises into a handful
/// of bands rather than blending — the same rule <see cref="ProceduralSurface"/> follows.
///
/// **Why a skybox and not a dome.** The first attempt built an inverted sphere at 2400 m. Fog
/// is linear and ends at 340 m, so the entire dome rendered as solid fog colour and the frame
/// did not change at all. Skyboxes are not fogged; geometry always is. The dome also hit the
/// documented serialisation trap — a runtime-created material does not survive a scene save.
/// The baked skybox asset avoids both.
/// </summary>
public static class ProceduralSky
{
    /// <summary>Bands from horizon to zenith. Few enough to read as registers, not a ramp.</summary>
    public const int Bands = 5;

    /// <summary>Equirectangular, so it must be 2:1. Small because the bands are flat.</summary>
    public const int Width = 128;
    public const int Height = 64;

    /// <summary>
    /// A latitude-longitude sky for <c>Skybox/Panoramic</c>. The lower half is below the
    /// horizon and is never really seen, but it is filled with the horizon band so a downward
    /// glance past a ledge does not reveal a hole.
    /// </summary>
    public static Texture2D BuildTexture(in ArtDirection.Preset preset)
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false)
        {
            name = "T_Sky",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var below = Color.Lerp(preset.SkyLow, preset.Palette.Contour, 0.25f);
        var pixels = new Color32[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            // v = 0 is straight down, 0.5 is the horizon, 1 is the zenith.
            float v = y / (float)(Height - 1);
            Color colour;

            if (v < 0.5f)
            {
                colour = below;
            }
            else
            {
                // Square-rooted so the horizon band is the widest — that is the part of the sky
                // a standing figure actually occupies, and the register the city reads against.
                float up = (v - 0.5f) * 2f;
                int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(up) * Bands), 0, Bands - 1);
                colour = Color.Lerp(preset.SkyLow, preset.SkyHigh, band / (float)(Bands - 1));
            }

            colour.a = 1f;
            for (int x = 0; x < Width; x++) pixels[y * Width + x] = colour;
        }

        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false);
        return texture;
    }
}
