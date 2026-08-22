namespace RatnaBay.Domain;

/// <summary>
/// The two engine helpers the ported systems relied on. Kept here so the domain has no
/// dependency on any framework's math library.
/// </summary>
internal static class MathUtil
{
    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    public static float Lerp(float from, float to, float t) => from + (to - from) * Clamp01(t);
}
