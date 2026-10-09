using Microsoft.Xna.Framework;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities.Flags;

/// <summary>
/// An entire layer dedicated to certain types of object layers such as the
/// <see cref="ImageLayer"/> or <see cref="ShapeLayer"/>.
/// </summary>
public interface IFlagLayer
{
    Vector2 Position { get; internal set; }
    Vector2 Scale { get; internal set; }
    float Rotation { get; set; }
    Color Sample(float posX, float posY, Color underlying);

    /// Assign new position.
    public void Move(int x, int y) => Position = new Vector2(x, y);

    /// Additively adjust position.
    public void Nudge(int x, int y) => Position = new Vector2(Position.X + x, Position.Y + y);

    /// Assign new asymmetrical scale.
    public void Resize(int x, int y) => Scale = new Vector2(x, y);

    /// Assign new symmetrical scale.
    public void Resize(int v) => Resize(v, v);

    /// Additively adjust scale.
    public void IncrementScale(int x, int y) => Scale = new Vector2(Scale.X + x, Scale.Y + y);

    /// Additively adjust scale.
    public void IncrementScale(int v) => IncrementScale(v, v);
}