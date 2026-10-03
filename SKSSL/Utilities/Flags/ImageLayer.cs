namespace SKSSL.Utilities.Flags;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public sealed class ImageLayer : IFlagLayer
{
    private readonly Color[] _pixels;
    public int Width { get; }
    public int Height { get; }
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public float Rotation { get; set; }

    public ImageLayer(Texture2D texture, Vector2 position, Vector2 scale)
    {
        Width = texture.Width;
        Height = texture.Height;

        _pixels = new Color[Width * Height];
        texture.GetData(_pixels);

        Position = position;
        Scale = scale;
    }

    public Color Sample(float x, float y, Color underlying)
    {
        float u = (x - Position.X) / Scale.X;
        float v = (y - Position.Y) / Scale.Y;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return underlying;

        int px = MathHelper.Clamp((int)(u * Width), 0, Width - 1);
        int py = MathHelper.Clamp((int)(v * Height), 0, Height - 1);

        Color image = _pixels[py * Width + px];
        float alpha = image.A / 255f;
        return Color.Lerp(underlying, image, alpha);
    }
}