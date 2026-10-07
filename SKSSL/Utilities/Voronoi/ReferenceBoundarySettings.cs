using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL.Utilities.Voronoi;

public sealed class ReferenceBoundarySettings
{
    public byte[] Pixels { get; }

    public int Width { get; }

    public int Height { get; }
    
    public ReferenceBoundarySettings(Texture2D texture)
    {
        Width = texture.Width;
        Height = texture.Height;

        int length = checked(Width * Height);
        Pixels = new byte[checked(length * 4)];

        texture.GetData(Pixels);
    }
}