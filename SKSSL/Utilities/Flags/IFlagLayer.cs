using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Flags;

/// <summary>
/// An entire layer dedicated to certain types of object layers such as the
/// <see cref="ImageLayer"/> or <see cref="ShapeLayer"/>.
/// </summary>
public interface IFlagLayer
{
    Vector2 Position { get; set; }
    Vector2 Scale { get; set; }
    float Rotation { get; set; }
    Color Sample(float x, float y, Color underlying);
}