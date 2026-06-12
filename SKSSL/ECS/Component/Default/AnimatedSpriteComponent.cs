using System.Collections.Generic;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.ECS;

public record AnimateSpriteComponent : SpriteComponent
{
    // ->+ IsVisible = true
    // -->+ SpriteComponent Fields
    
    public int CurrentFrame = 0;

    /// Speed that which the animations' frames progress in Frames/Second.
    public float AnimationSpeed = 10.0f; // 10 FPS

    public List<string> AnimationFrames = [];
}