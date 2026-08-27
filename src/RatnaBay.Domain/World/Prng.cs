namespace RatnaBay.Domain;

/// <summary>
/// A deliberately small generator, written out rather than taken from the framework.
///
/// Seeds get quoted in bug reports and shared between players, so "seed 4211 is the same
/// mine everywhere, forever" has to be a property of this file — not of whichever runtime
/// happens to be installed.
///
/// This is SplitMix32: a counter plus an avalanche. The first version here was a bare
/// xorshift, and its low bits were correlated enough that `% 5` returned the same direction
/// several steps running — every mine came out a straight corridor. The mixing step is not
/// decoration; without it the layout variety this whole class exists for does not happen.
/// </summary>
public sealed class Prng
{
    private uint _state;

    public Prng(int seed) => _state = unchecked((uint)seed);

    public uint NextUInt()
    {
        unchecked
        {
            _state += 0x9E3779B9u;
            var z = _state;
            z = (z ^ (z >> 16)) * 0x21F0AAADu;
            z = (z ^ (z >> 15)) * 0x735A2D97u;
            return z ^ (z >> 15);
        }
    }

    /// <summary>Scaled from the high bits, which are the well-mixed ones.</summary>
    public int Next(int exclusiveMax) =>
        exclusiveMax <= 1 ? 0 : (int)(((ulong)NextUInt() * (ulong)exclusiveMax) >> 32);

    public float NextFloat(float min, float max) =>
        min + (max - min) * (NextUInt() / (float)uint.MaxValue);
}
