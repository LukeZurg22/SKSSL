using System.Runtime.CompilerServices;
using static System.Math;

namespace SKSSL.Mathematics;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
public static class Indexers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Morton(uint x, uint y)
    {
        return Part1By1(x) | (Part1By1(y) << 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Part1By1(uint value)
        {
            ulong x = value;
            x = (x | x << 16) & 0x0000FFFF0000FFFFUL;
            x = (x | x << 8) & 0x00FF00FF00FF00FFUL;
            x = (x | x << 4) & 0x0F0F0F0F0F0F0F0FUL;
            x = (x | x << 2) & 0x3333333333333333UL;
            x = (x | x << 1) & 0x5555555555555555UL;
            return x;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hilbert(float x, float y, float width, float height)
    {
        const byte bits = 16;
        const uint max = (1u << bits) - 1;
        uint ix = (uint)Clamp(Round(x / width * max), 0, max);
        uint iy = (uint)Clamp(Round(y / height * max), 0, max);
        return XYToHilbert(ix, iy, bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint XYToHilbert(uint x, uint y, byte bits)
    {
        uint d = 0;
        for (int s = bits - 1; s >= 0; s--)
        {
            uint rx = (x >> s) & 1;
            uint ry = (y >> s) & 1;

            d += ((3 * rx) ^ ry) << (2 * s);

            if (ry != 0)
                continue;

            if (rx == 1)
            {
                x = maxCoordinate(bits) - x;
                y = maxCoordinate(bits) - y;
            }

            (x, y) = (y, x);
        }

        return d;
        static uint maxCoordinate(int bits) => (1u << bits) - 1;
    }
}