using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Flags;

public readonly struct Rectangle : IFlagShape
{
    public readonly float X;
    public readonly float Y;
    public readonly float Width;
    public readonly float Height;
    public Color Color { get; }

    public Rectangle(float x, float y, float width, float height, Color color)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
    }

    public bool Contains(float x, float y) => x >= X &&
                                              x <= X + Width &&
                                              y >= Y &&
                                              y <= Y + Height;
}