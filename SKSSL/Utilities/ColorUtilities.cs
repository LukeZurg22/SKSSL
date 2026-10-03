using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public static class ColorUtilities
{
    /// <summary>
    /// Deterministically generate a color from an integer.
    /// </summary>
    /// <remarks>
    /// The same ID always produces the same color. Colors are not
    /// mathematically guaranteed to be unique because the 32-bit hash
    /// is reduced to 24 bits of RGB color.
    /// </remarks>
    /// <param name="id">Unsigned Integer used to generate the color.</param>
    /// <returns>A deterministic <see cref="Color"/> derived from the ID.</returns>
    public static Color GetSemiUniqueColor(uint id)
    {
        unchecked
        {
            uint x = id;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return new Color((byte)(x & 0xFF), (byte)((x >> 8) & 0xFF), (byte)((x >> 16) & 0xFF));
        }
    }
}