using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

// ReSharper disable UnusedMember.Global

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

    private const uint InvalidatedUint = uint.MaxValue;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color EncodeAsColor(this uint i)
    {
        if (i is InvalidatedUint or > 0x00FFFFFE /*aka MaxRasterUnsignedInteger*/)
            throw new ArgumentOutOfRangeException(nameof(i));

        uint encoded = i /*+ 1u*/; // Fixed for 0-based indexing, not 1-based.
        var r = (byte)(encoded & 0xFF);
        var g = (byte)((encoded >> 8) & 0xFF);
        var b = (byte)((encoded >> 16) & 0xFF);
        const byte alpha = byte.MaxValue;
        return new Color(r, g, b, alpha);
    }

    public static uint DecodeAsUint(this Color color)
    {
        uint encoded = color.R | ((uint)color.G << 8) | ((uint)color.B << 16);
        return encoded == 0 ? InvalidatedUint : encoded - 1u;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint HashPackedColor(this uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return value;
        }
    }

}