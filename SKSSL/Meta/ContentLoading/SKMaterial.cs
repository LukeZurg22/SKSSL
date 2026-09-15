using System;

namespace SKSSL;

// ReSharper disable UnusedMember.Global
/// <summary>
/// Texture mapping fields mapped to various types of textures.
/// <example>Diffuse, Normal, Displacement, etc.</example>
/// </summary>
public readonly struct SKMaterial : IDisposable
{
    /// Internal array of texture references used in this material.
    public readonly TextureLease[] Textures = new TextureLease[Enum.GetValues<TextureMap>().Length];
    
    // @formatter:off
    /// Albedo / color map.
    public TextureLease Diffuse { get => Textures[0]; set => Textures[0] = value; }

    /// Lighting height map.
    public TextureLease Normal { get => Textures[1]; set => Textures[1] = value; }

    /// Physical displacement map.
    public TextureLease Displacement { get => Textures[2]; set => Textures[2] = value; }

    /// Lighting emission map.
    public TextureLease Emissive { get => Textures[3]; set => Textures[3] = value; }
    
    // IMPL: occlusion, detail mask, etc. Everything below is a bit out-of-current-scope.
    //public Texture2D Specular { get; set; }
    //public Texture2D Metallic { get; set; }
    //public Vector4 TintColor;   // Tint color multiplier
    //public float Smoothness;    // 0-1
    //public float Metallic;      // 0-1
    // @formatter:on

    /// Blank constructor for creating partial materials incrementally.
    public SKMaterial()
    {
    }

    /// Constructor to manually assign all textures.
    public SKMaterial(
        TextureLease diffuse,
        TextureLease normal,
        TextureLease displacement,
        TextureLease emissive)
    {
        Diffuse = diffuse;
        Normal = normal;
        Displacement = displacement;
        Emissive = emissive;
    }

    public void Dispose()
    {
        foreach (TextureLease texture in Textures)
            texture.Dispose();
    }
}