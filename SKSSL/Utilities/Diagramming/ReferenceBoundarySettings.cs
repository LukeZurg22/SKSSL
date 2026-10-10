using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL.Utilities.Voronoi;

public sealed class ReferenceBoundarySettings
{
    public byte[] Pixels { get; }

    private int Width { get; }

    private int Height { get; }

    public ReferenceBoundarySettings(Texture2D texture)
    {
        Width = texture.Width;
        Height = texture.Height;
        Pixels = new byte[checked(checked(Width * Height) * 4)];
        texture.GetData(Pixels);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlack(int pixelIndex)
    {
        int i = pixelIndex << 2;
        return (Pixels[i] | Pixels[i + 1] | Pixels[i + 2]) == 0;
    }
}