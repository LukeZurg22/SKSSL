using Microsoft.Xna.Framework;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Do NOT use alongside <see cref="Transform3DComponent"/>
/// </summary>
public record Transform2DComponent : Component
{
    public Vector2 Position;

    /// Rotation in Radians.
    public float Rotation = 0f;

    public Vector2 Scale = Vector2.One;
}