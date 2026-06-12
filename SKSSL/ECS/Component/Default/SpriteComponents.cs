using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using YamlDotNet.Serialization;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS;

/// <summary>
/// SKSSL-provided default Sprite Component.
/// </summary>
public record SpriteComponent : Component
{
    /* // Pseudo-Code Map-out
     * - image: my_favorite_sprite
     *   source: [0,0]
     *   tint: white
     *   visible: true
     *   order: 0
     *   flipX: false
     *   flipY: false
     *   layers:
     *    - image: my_overlay
     *    - image: my_overlay_other
     */

    /// "Root" handle of sprite without extension.
    [YamlMember(Alias = "image")] public string Handle;

    /// An integer ID handle to assign a numerical ID for faster O(1) lookup.
    /// <remarks>
    /// Only works if the implemented systems accommodate this, and that the handle is stored in an indexable array.
    /// </remarks>
    [YamlIgnore, JsonIgnore]
    public int HandleId { get; private set; } = -1;

    /// Called to assign numeric ID at runtime for easier lookup.
    public void SetHandleNumericId(int id) => HandleId = id;

    /// Pixel coordinates on texture atlas, if this sprite involves one.
    public Rectangle? Source;

    public Color Tint = Color.White;

    /// Determines whether this entity is visible.
    [YamlMember(Alias = "visible")] public bool IsVisible = true;

    /// Assignable Layer for draw prioritization..
    [YamlMember(Alias = "order")] public int Order = 0;

    public bool FlipX = false;
    public bool FlipY = false;

    /// Layers containing overlapping sprite data. Not recursive!
    /// <remarks>All should be drawn in the order they were declared,
    /// all within the layer of the current SpriteComponent.</remarks>
    [YamlMember(Alias = "layers", Order = 99)]
    public SpriteLayer[] Layers = [];
}