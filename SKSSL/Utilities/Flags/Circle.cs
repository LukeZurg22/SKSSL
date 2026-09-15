using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Flags;

public struct Circle : IFlagShape
{
    public Vector2 Position { get; set; }
    public readonly float Radius;
    public Color Color { get; }

    public Circle(Vector2 position, float radius, Color color)
    {
        Position = position;
        Radius = radius;
        Color = color;
    }

    public bool Contains(float x, float y)
    {
        float dx = x - Position.X;
        float dy = y - Position.Y;
        return dx * dx + dy * dy <= Radius * Radius;
    }
}