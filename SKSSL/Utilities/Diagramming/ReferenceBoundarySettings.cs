using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL.Utilities;

public sealed class ReferenceBoundarySettings
{
    private readonly bool[] _blackPixels;
    public int Width { get; }

    public int Height { get; }

    public ReferenceBoundarySettings(Texture2D texture)
    {
        Width = texture.Width;
        Height = texture.Height;

        int pixelCount = checked(Width * Height);

        // Read RGBA temporarily, then retain only the black/white mask.
        var rgba = new byte[checked(pixelCount * 4)];
        texture.GetData(rgba);

        // == Debugging for black pixels.
        // Debug.WriteLine($"Texture format: {texture.Format}");
        // Debug.WriteLine($"Texture size: {texture.Width}x{texture.Height}");
        // for (int i = 0; i < Math.Min(10, pixelCount); i++)
        // {
        //     int j = i * 4;
        //     Debug.WriteLine($"Pixel {i}: R={rgba[j]}, G={rgba[j + 1]}, B={rgba[j + 2]}, A={rgba[j + 3]}");
        // }

        // Due to a mistake I made by inputting a transparent&black map, the entire thing was read as a boundary map.
        // Transparent pixels are intended to not be read, so in order to make this flexible, I deemed it more effective
        // to read transparent pixels as "white" or "non-border" pixels.
        _blackPixels = new bool[pixelCount];
        for (int i = 0, j = 0; i < pixelCount; i++, j += 4)
            _blackPixels[i] = rgba[j + 3] != 0 && (rgba[j] | rgba[j + 1] | rgba[j + 2]) == 0;
        
        //@formatter:off
        //#if DEBUG
        //    int blackCount = 0;
        //    foreach (bool pixel in _blackPixels) if (pixel) blackCount++;
        //    Debug.WriteLine($"_blackPixels: {blackCount} black / {_blackPixels.Length} total");
        //#endif
        //@formatter:on
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBlack(int pixelIndex) => _blackPixels[pixelIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool[] CreateResizedBlackMask(int targetWidth, int targetHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        var result = new bool[checked(targetWidth * targetHeight)];
        for (int y = 0; y < targetHeight; y++)
        {
            // Source rows overlapped by this destination row.
            int sourceY0 = (int)((long)y * Height / targetHeight);
            int sourceY1 = (int)(((long)(y + 1) * Height - 1) / targetHeight);
            for (int x = 0; x < targetWidth; x++)
            {
                int sourceX0 = (int)((long)x * Width / targetWidth);
                int sourceX1 = (int)(((long)(x + 1) * Width - 1) / targetWidth);

                bool isBlack = false;

                for (int sy = sourceY0; sy <= sourceY1 && !isBlack; sy++)
                for (int sx = sourceX0; sx <= sourceX1; sx++)
                {
                    if (!IsBlack(sy * Width + sx))
                        continue;

                    isBlack = true;
                    break;
                }

                result[y * targetWidth + x] = isBlack;
            }
        }

        return result;
    }

    public bool[] GetMask() => _blackPixels;
}