using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Flags;

public sealed class ShapeLayer : IFlagLayer
{
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public float Rotation { get; set; }
    private readonly IFlagShape _shape;
    public ShapeLayer(IFlagShape shape) => _shape = shape;
    public Color Sample(float x, float y, Color underlying) => _shape.Contains(x, y) ? _shape.Color : underlying;
}