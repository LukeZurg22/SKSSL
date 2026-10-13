using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using static SKSSL.Mathematics.IntegerExtensions;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public static class ColorUtilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color EncodeAsColor(this uint i)
    {
        if (i is InvalidatedUint or > MaxEncodableUint)
            throw new ArgumentOutOfRangeException(nameof(i));
        var r = (byte)(i & 0xFF);
        var g = (byte)((i >> 8) & 0xFF);
        var b = (byte)((i >> 16) & 0xFF);
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