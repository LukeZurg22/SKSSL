using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

public record TextureMaps
{
    public enum TextureType
    {
        DIFFUSE,
        NORMAL,

        // Unused
        DISPLACEMENT,
        GLOSSY,
    }

    public Texture2D? Diffuse { get; set; }
    public Texture2D? Normal { get; set; }
    public Texture2D? Displacement { get; set; }
    public Texture2D? Metallic { get; set; }
    public Texture2D? Roughness { get; set; }
    public Texture2D? Emissive { get; set; }

    public TextureMaps()
    {
    }

    // Positional constructor for normal use
    public TextureMaps(Texture2D? diffuse, Texture2D? normal = null)
    {
        Diffuse = diffuse;
        Normal = normal;
    }
}