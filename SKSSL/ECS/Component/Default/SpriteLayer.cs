using Microsoft.Xna.Framework;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

/// <summary>
/// Sublayer to sprite component. 
/// </summary>
public struct SpriteLayer()
{
    [YamlMember(Alias = "image")] public string Handle;
    public Rectangle? Source; // Nullable to inherit parent SpriteComponent value.
    public Vector2? Offset; // Nullable to inherit parent SpriteComponent value.
    public Vector2? Scale; // Nullable to inherit parent SpriteComponent value.
    public bool FlipX = false;
    public bool FlipY = false;
    public Color Tint = Color.White;
}