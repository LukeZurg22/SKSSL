using System.Threading.Tasks;
using static System.Math;
using static System.MathF;

namespace SKSSL.Utilities;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable once UnusedType.Global
public sealed class DensityMap
{
    private readonly float[] _density;
    public int Width { get; }
    public int Height { get; }

    public DensityMap(Texture2D texture)
    {
        Width = texture.Width;
        Height = texture.Height;
        var pixels = new Color[Width * Height];
        texture.GetData(pixels);
        _density = new float[pixels.Length];

        Parallel.For(0, pixels.Length, i =>
        {
            Color c = pixels[i];

            // Grayscale input: R, G, and B should be similar. Samples nearest Texel.
            // TODO: Bilinear Interpolation, or Cubic Interpolation, or whatever else works if this isn't good enough.
            float luminance = (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) / 255f;
            _density[i] = luminance;
        });
    }

    public float Sample(float x, float y)
    {
        int ix = Clamp((int)x, 0, Width - 1);
        int iy = Clamp((int)y, 0, Height - 1);
        return _density[iy * Width + ix];
    }

    public static float GetRadius(float luminance, float darkRadius, float brightRadius, float contrast = 1.5f)
    {
        float density = Pow(Clamp(luminance, 0f, 1f), contrast);
        return MathHelper.Lerp(darkRadius, brightRadius, density);
    }
}