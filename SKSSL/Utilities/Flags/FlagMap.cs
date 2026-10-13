using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Flags;

/// <summary>
/// A mathematical and raster-based composition system for creating country flags.
/// Coordinates supplied to layers are normalized to the range [0, 1].
/// </summary>
public sealed class FlagMap
{
    private readonly List<IFlagLayer> _layers = [];

    public Color Background { get; set; } = Color.White;

    public int Width { get; }
    public int Height { get; }

    public FlagMap(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void Add(IFlagLayer layer) => _layers.Add(layer);

    public Color Sample(float x, float y)
        => _layers.Aggregate(Background, (current, layer) => layer.Sample(x, y, current));

    /// <summary>
    /// Rasterize a flag map to a linear array.
    /// </summary>
    /// <returns>1D array of size Width*Height containing sampled UV pixel data.</returns>
    public Color[] Rasterize()
    {
        var pixels = new Color[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            float u = (x + 0.5f) / Width;
            float v = (y + 0.5f) / Height;
            pixels[y * Width + x] = Sample(u, v);
        }

        return pixels;
    }

    /// <summary>
    /// Calls <see cref="Rasterize"/> and creates a mapped texture.
    /// </summary>
    /// <param name="graphicsDevice"></param>
    /// <returns>A new <see cref="Texture2D"/></returns>
    public Texture2D CreateTexture(GraphicsDevice graphicsDevice)
    {
        var pixels = Rasterize();
        var texture = new Texture2D(graphicsDevice, Width, Height, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }
}