namespace SKSSL;

/// <summary>
/// Supported texture-types in the system. Defaults to <see cref="DIFFUSE"/>.
/// </summary>
/// <remarks>
/// This will not inherently do anything besides permit additional map types. Rendering must be implemented separately.
/// </remarks>
public enum TextureMap : byte
{
    /// Plain color information.
    DIFFUSE = 0,

    /// Normal-data.
    NORMAL = 1,

    /// Height data.
    DISPLACEMENT = 2,

    /// Glow data.
    EMISSIVE = 3,

    // Unused as of 20260106
    //GLOSSY,
}