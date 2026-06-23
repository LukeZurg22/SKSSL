using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

// ReSharper disable UnusedMember.Global
/// <summary>
/// Texture mapping fields mapped to various types of textures.
/// <example>Diffuse, Normal, Displacement, etc.</example>
/// </summary>
public record SKMaterial
{
    /// Internal array of texture references used in this material.
    public readonly Texture2D?[] Textures = new Texture2D?[4];
    
    // @formatter:off
    /// Albedo / color map.
    public Texture2D? Diffuse
    {
        get => Textures[0];
        set => Textures[0] = value;
    }

    /// Lighting height map.
    public Texture2D? Normal
    {
        get => Textures[1];
        set => Textures[1] = value;
    }

    /// Physical displacement map.
    public Texture2D? Displacement
    {
        get => Textures[2];
        set => Textures[2] = value;
    }

    /// Lighting emission map.
    public Texture2D? Emissive
    {
        get => Textures[3];
        set => Textures[3] = value;
    }
    // IMPL: occlusion, detail mask, etc. Everything below is a bit out-of-current-scope.
    //public Texture2D? Specular { get; set; }
    //public Texture2D? Metallic { get; set; }
    //public Vector4 TintColor;   // Tint color multiplier
    //public float Smoothness;    // 0-1
    //public float Metallic;      // 0-1
    // @formatter:on

    // Positional constructor for normal use

    /// Emissive, Displacement, etc. are null. Diffuse is the only layer given a texture.
    /// <returns>Default error texture when a material is not found.</returns>
    public static SKMaterial Error(Texture2D errorTexture) => new() { Diffuse = errorTexture };

    /// Blank constructor for creating materials incrementally.
    public SKMaterial()
    {
    }

    /// Constructor to manually assign all textures.
    public SKMaterial(
        Texture2D diffuse,
        Texture2D normal,
        Texture2D displacement,
        Texture2D emissive)
    {
        Diffuse = diffuse;
        Normal = normal;
        Displacement = displacement;
        Emissive = emissive;
    }
}