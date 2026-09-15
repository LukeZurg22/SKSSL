using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Flags;

/// <summary>
/// Represents an abstract shape upon a map.
/// </summary>
public interface IFlagShape
{
    bool Contains(float x, float y);
    Color Color { get; }
}