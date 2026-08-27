using Microsoft.Xna.Framework;
using System;

namespace RatnaBay.Client;

/// <summary>
/// A software canvas for a face, and deliberately not <see cref="SpriteForge"/>.
///
/// The forge is right for everything it does and wrong for this, for two reasons that both
/// only bite at close range.
///
/// **It writes thickness, not form.** A shape's height comes from how far each pixel sits from
/// that shape's own outline, so every part is a flat-topped mesa with rounded edges. That is
/// perfect for a sword and hopeless for a face, because it cannot make a hollow: an eye socket
/// has to be *further back* than the cheek beside it, and a chamfer has no way to say so. Eyes
/// end up pasted on top of a forehead instead of set into one.
///
/// **And it quantises into a five-step ramp**, which is the entire reason the world sprites
/// read as pixel art. At thirty-two pixels that banding is the style. At three hundred it is
/// just banding.
///
/// So this stores a real height field in continuous units, composites parts with a **smooth
/// maximum** so they fuse into one surface rather than stacking with a depth test, allows
/// parts to be *subtracted* to carve sockets and folds, and lights the result with a key, a
/// fill, ambient occlusion and a rim — continuously, no ramp. Everything is computed at
/// <see cref="Supersample"/> times the output size and box-filtered down at the end, which is
/// what removes the staircase edges the forge has no answer for.
/// </summary>
public sealed class FaceField
{
    /// <summary>
    /// Rendered at this multiple and averaged down.
    ///
    /// Two is the whole difference between an edge that steps and an edge that curves. It also
    /// smooths the normals before they are lit, so a cheekbone reads as a gradient rather than
    /// as a contour line.
    /// </summary>
    public const int Supersample = 2;

    private readonly int _width;
    private readonly int _height;

    /// <summary>Surface height in the same units as the coordinates. Toward the viewer is up.</summary>
    private readonly float[] _z;

    /// <summary>Base colour before any light touches it.</summary>
    private readonly Vector3[] _albedo;

    /// <summary>How shiny each pixel is. Skin is barely, an eye is very.</summary>
    private readonly float[] _gloss;

    /// <summary>Coverage, for an edge that fades rather than steps.</summary>
    private readonly float[] _alpha;

    private const float Empty = -4000f;

    public FaceField(int outputWidth, int outputHeight)
    {
        Output = new Point(outputWidth, outputHeight);
        _width = outputWidth * Supersample;
        _height = outputHeight * Supersample;

        _z = new float[_width * _height];
        _albedo = new Vector3[_z.Length];
        _gloss = new float[_z.Length];
        _alpha = new float[_z.Length];

        Array.Fill(_z, Empty);
    }

    public Point Output { get; }

    private static Vector3 Linear(Color colour) =>
        new(colour.R / 255f, colour.G / 255f, colour.B / 255f);

    /// <summary>
    /// Blend two heights into one surface instead of picking the taller.
    ///
    /// This is the single most important line in the file. A hard maximum leaves a crease
    /// wherever two parts meet, which is what makes assembled-primitive faces look assembled;
    /// a smooth maximum fuses them over <paramref name="k"/> units, so a nose grows out of a
    /// face and a cheek runs into a jaw. The <c>k * t * (1 - t)</c> term is the fillet.
    /// </summary>
    private static float SmoothMax(float a, float b, float k, out float weight)
    {
        if (k <= 0f)
        {
            weight = b > a ? 1f : 0f;
            return MathF.Max(a, b);
        }

        var t = Math.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
        weight = t;
        return a + (b - a) * t + k * t * (1f - t);
    }

    // ------------------------------------------------------------------ building

    /// <summary>
    /// Add a half-ellipsoid: the workhorse, and a genuinely three-dimensional one.
    ///
    /// <paramref name="z"/> is where the part's equator sits, so a shape can be pushed behind
    /// or pulled in front of its neighbours without changing its outline. That is what lets an
    /// eyeball sit inside a socket rather than on it.
    /// </summary>
    public void Ellipsoid(float cx, float cy, float rx, float ry, float rz, float z,
        Color colour, float blend = 8f, float gloss = 0.04f)
    {
        var s = Supersample;
        var minX = Math.Max(0, (int)((cx - rx) * s) - 2);
        var maxX = Math.Min(_width - 1, (int)((cx + rx) * s) + 2);
        var minY = Math.Max(0, (int)((cy - ry) * s) - 2);
        var maxY = Math.Min(_height - 1, (int)((cy + ry) * s) + 2);

        var tint = Linear(colour);

        for (var py = minY; py <= maxY; py++)
        for (var px = minX; px <= maxX; px++)
        {
            var x = (px + 0.5f) / s;
            var y = (py + 0.5f) / s;

            var nx = (x - cx) / rx;
            var ny = (y - cy) / ry;
            var d2 = nx * nx + ny * ny;
            if (d2 >= 1.15f) continue;

            var index = py * _width + px;

            // Coverage fades over the last sliver so the silhouette is not a staircase.
            var coverage = Math.Clamp((1f - d2) / 0.06f, 0f, 1f);
            if (coverage <= 0f) continue;

            var dome = d2 >= 1f ? 0f : MathF.Sqrt(1f - d2);
            var candidate = z + rz * dome;

            _z[index] = SmoothMax(_z[index], candidate, blend, out var weight);

            var mix = weight * coverage;
            _albedo[index] = Vector3.Lerp(_albedo[index], tint, mix);
            _gloss[index] += (gloss - _gloss[index]) * mix;
            _alpha[index] = MathF.Max(_alpha[index], coverage);
        }
    }

    /// <summary>A tapered cylinder for necks, hair clumps and braids.</summary>
    public void Tube(float x0, float y0, float x1, float y1, float r0, float r1, float rz,
        float z, Color colour, float blend = 8f, float gloss = 0.04f)
    {
        var steps = (int)MathF.Max(2f, MathF.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var r = r0 + (r1 - r0) * t;
            Ellipsoid(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r, r, rz, z, colour, blend, gloss);
        }
    }

    /// <summary>
    /// Push the surface back without changing the outline.
    ///
    /// The thing the forge cannot do at all, and most of what makes a face read: eye sockets,
    /// the crease beside the nose, the seam of the lips, the hollow under a cheekbone. A face
    /// is as much subtraction as addition and the low-resolution version had none of it.
    /// </summary>
    public void Carve(float cx, float cy, float rx, float ry, float depth, float softness = 2f)
    {
        var s = Supersample;
        var minX = Math.Max(0, (int)((cx - rx) * s) - 2);
        var maxX = Math.Min(_width - 1, (int)((cx + rx) * s) + 2);
        var minY = Math.Max(0, (int)((cy - ry) * s) - 2);
        var maxY = Math.Min(_height - 1, (int)((cy + ry) * s) + 2);

        for (var py = minY; py <= maxY; py++)
        for (var px = minX; px <= maxX; px++)
        {
            var x = (px + 0.5f) / s;
            var y = (py + 0.5f) / s;

            var nx = (x - cx) / rx;
            var ny = (y - cy) / ry;
            var d2 = nx * nx + ny * ny;
            if (d2 >= 1f) continue;

            var index = py * _width + px;
            if (_alpha[index] <= 0f) continue;

            var falloff = MathF.Pow(1f - d2, softness);
            _z[index] -= depth * falloff;
        }
    }

    /// <summary>
    /// Raise the surface by an amount that falls to nothing at the edge.
    ///
    /// **This is what facial detail has to be made of, and getting it wrong was the whole of
    /// the second attempt.** An <see cref="Ellipsoid"/> has a flat equator, so its rim is a
    /// cliff wherever it sits above the surface around it — which means every brow ridge,
    /// cheekbone and eyelid drew its own hard oval outline and the face looked like a mask
    /// assembled from washers. A bump adds zero at its own boundary, so it has no boundary to
    /// see: it can only ever be a swelling of the surface that was already there.
    ///
    /// Ellipsoids are still right for anything with a silhouette of its own — the head, the
    /// neck, a mass of hair, a helmet. Nothing on the face itself.
    /// </summary>
    public void Bump(float cx, float cy, float rx, float ry, float amount, float softness = 1.6f)
        => Carve(cx, cy, rx, ry, -amount, softness);

    /// <summary>Lay colour on the surface without disturbing it: lips, brows, a painted mark.</summary>
    public void Stain(float cx, float cy, float rx, float ry, Color colour, float strength = 1f,
        float softness = 1.4f, float gloss = -1f)
    {
        var s = Supersample;
        var minX = Math.Max(0, (int)((cx - rx) * s) - 2);
        var maxX = Math.Min(_width - 1, (int)((cx + rx) * s) + 2);
        var minY = Math.Max(0, (int)((cy - ry) * s) - 2);
        var maxY = Math.Min(_height - 1, (int)((cy + ry) * s) + 2);

        var tint = Linear(colour);

        for (var py = minY; py <= maxY; py++)
        for (var px = minX; px <= maxX; px++)
        {
            var x = (px + 0.5f) / s;
            var y = (py + 0.5f) / s;

            var nx = (x - cx) / rx;
            var ny = (y - cy) / ry;
            var d2 = nx * nx + ny * ny;
            if (d2 >= 1f) continue;

            var index = py * _width + px;
            if (_alpha[index] <= 0f) continue;

            var mix = MathF.Pow(1f - d2, softness) * strength;
            _albedo[index] = Vector3.Lerp(_albedo[index], tint, mix);
            if (gloss >= 0f) _gloss[index] += (gloss - _gloss[index]) * mix;
        }
    }

    // ------------------------------------------------------------------ lighting

    /// <summary>
    /// Light the field and average it down.
    ///
    /// Three lights and an occlusion term, all continuous. The key models the lamp the rest of
    /// the game is lit by; the fill is cool and comes from the other side so the shadow side of
    /// a face is not simply dark; the rim runs along silhouette edges and is what separates a
    /// head from the panel behind it. Occlusion is read from the height field itself — a pixel
    /// well below its own neighbourhood is in a hollow, and hollows are where faces get their
    /// depth.
    /// </summary>
    public Color[] Resolve()
    {
        var key = Vector3.Normalize(new Vector3(-0.46f, -0.62f, 0.64f));
        var fill = Vector3.Normalize(new Vector3(0.72f, -0.10f, 0.52f));
        var fillTint = new Vector3(0.42f, 0.50f, 0.66f);
        var rimTint = new Vector3(0.62f, 0.66f, 0.78f);

        var occlusion = Occlusion();
        var lit = new Vector3[_z.Length];

        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var index = y * _width + x;
            if (_alpha[index] <= 0f) continue;

            var normal = NormalAt(x, y);
            var albedo = _albedo[index];
            var ao = occlusion[index];

            var direct = MathF.Max(0f, Vector3.Dot(normal, key));
            var bounce = MathF.Max(0f, Vector3.Dot(normal, fill));

            // Skin does not go dark, it goes red: light that enters near the terminator scatters
            // under the surface and comes back warm. Faked as a band around the terminator, which
            // is the cheapest version of it that still reads as flesh rather than plastic.
            var scatter = MathF.Max(0f, 1f - MathF.Abs(direct - 0.22f) * 3.4f) * 0.20f;

            var colour =
                albedo * (0.30f * ao + direct * 0.92f * ao)
                + albedo * fillTint * (bounce * 0.34f * ao)
                + new Vector3(0.55f, 0.20f, 0.14f) * albedo * scatter;

            // A tight highlight where the surface faces the lamp squarely.
            var half = Vector3.Normalize(key + Vector3.UnitZ);
            var spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, half)), 26f) * _gloss[index] * 2.4f;
            colour += new Vector3(1f, 0.97f, 0.92f) * spec * ao;

            // Rim: strongest where the surface turns away from the viewer.
            var facing = MathF.Max(0f, normal.Z);
            var rim = MathF.Pow(1f - facing, 4.2f) * 0.5f;
            colour += rimTint * rim;

            lit[index] = colour;
        }

        return Downsample(lit);
    }

    /// <summary>
    /// How buried each pixel is, from the height field alone.
    ///
    /// A separable box blur of the surface, compared against the surface. Where the blur sits
    /// well above the actual height the pixel is in a hollow — an eye socket, the seam of the
    /// lips, under the jaw — and gets darkened. No rays, no samples, one pass each way.
    /// </summary>
    private float[] Occlusion()
    {
        const int Radius = 7 * Supersample;

        var blurred = new float[_z.Length];
        var scratch = new float[_z.Length];

        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var sum = 0f;
            var count = 0;
            for (var d = -Radius; d <= Radius; d += 2)
            {
                var sx = x + d;
                if (sx < 0 || sx >= _width) continue;
                var value = _z[y * _width + sx];
                if (value <= Empty * 0.5f) continue;
                sum += value;
                count++;
            }

            scratch[y * _width + x] = count > 0 ? sum / count : _z[y * _width + x];
        }

        for (var y = 0; y < _height; y++)
        for (var x = 0; x < _width; x++)
        {
            var sum = 0f;
            var count = 0;
            for (var d = -Radius; d <= Radius; d += 2)
            {
                var sy = y + d;
                if (sy < 0 || sy >= _height) continue;
                sum += scratch[sy * _width + x];
                count++;
            }

            var index = y * _width + x;
            var average = count > 0 ? sum / count : _z[index];
            var buried = average - _z[index];
            blurred[index] = Math.Clamp(1f - buried * 0.055f, 0.30f, 1.06f);
        }

        return blurred;
    }

    private Vector3 NormalAt(int x, int y)
    {
        float At(int sx, int sy)
        {
            sx = Math.Clamp(sx, 0, _width - 1);
            sy = Math.Clamp(sy, 0, _height - 1);
            var index = sy * _width + sx;
            return _alpha[index] > 0f ? _z[index] : _z[y * _width + x];
        }

        var dx = (At(x + 1, y) - At(x - 1, y)) * 0.5f * Supersample;
        var dy = (At(x, y + 1) - At(x, y - 1)) * 0.5f * Supersample;

        return Vector3.Normalize(new Vector3(-dx, -dy, 1f));
    }

    private Color[] Downsample(Vector3[] lit)
    {
        var pixels = new Color[Output.X * Output.Y];
        const float Inverse = 1f / (Supersample * Supersample);

        for (var y = 0; y < Output.Y; y++)
        for (var x = 0; x < Output.X; x++)
        {
            var colour = Vector3.Zero;
            var alpha = 0f;

            for (var sy = 0; sy < Supersample; sy++)
            for (var sx = 0; sx < Supersample; sx++)
            {
                var index = (y * Supersample + sy) * _width + x * Supersample + sx;
                colour += lit[index];
                alpha += _alpha[index];
            }

            colour *= Inverse;
            alpha *= Inverse;

            pixels[y * Output.X + x] = new Color(
                Math.Clamp(colour.X, 0f, 1f),
                Math.Clamp(colour.Y, 0f, 1f),
                Math.Clamp(colour.Z, 0f, 1f),
                Math.Clamp(alpha, 0f, 1f));
        }

        return pixels;
    }
}
