using System;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SKSSL.Utilities.Voronoi;

public sealed class ReferenceBoundarySettings
{
    public byte[] Pixels { get; }

    public int Width { get; }

    public int Height { get; }

    public byte BlackThreshold { get; init; }

    /// <summary>
    /// Additional traversal cost imposed by black reference pixels.
    /// The pixels remain traversable; this only influences cell ownership.
    /// </summary>
    public float GuideStrength { get; init; }

    /// <summary>
    /// Maximum distance from a Voronoi edge at which the reference guide
    /// has influence.
    /// </summary>
    public float GuideRadius { get; init; }
    
    public ReferenceBoundarySettings(
        Texture2D texture,
        byte blackThreshold = 25,
        float guideRadius = 96f,
        float guideStrength = 25f)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfLessThan(guideStrength, 0f);

        Width = texture.Width;
        Height = texture.Height;
        BlackThreshold = blackThreshold;
        GuideStrength = guideStrength;
        GuideRadius = guideRadius;

        int length = checked(Width * Height);
        Pixels = new byte[checked(length * 4)];

        texture.GetData(Pixels);
    }
}