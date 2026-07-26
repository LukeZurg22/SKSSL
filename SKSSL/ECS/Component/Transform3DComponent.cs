using Microsoft.Xna.Framework;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Do NOT use alongside <see cref="Transform2DComponent"/>
/// </summary>
public record Transform3DComponent : Component
{
    public Vector3 Position;

    /// Rotation Quaternion for three-dimensional space.
    public Quaternion Rotation = System.Numerics.Quaternion.Zero;

    public Vector3 Scale = Vector3.One;
}