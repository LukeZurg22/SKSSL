// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using Microsoft.Xna.Framework;

namespace SKSSL.Mathematics;

public static class Floats
{
    /// <summary>
    /// Assigned Epsilon Value for <see cref="AreFloatsEqual"/> floating point numerical comparison.
    /// <value>0.0001f</value>
    /// </summary>
    private const float Epsilon = 0.0001f;
    
    /// <summary>
    /// Compares two floating point values by the following:<br/>
    /// Get Result For "Is Math.Abs(a-b) less than <see cref="Epsilon"/>?"
    /// </summary>
    /// <returns>Whether the two floating point numbers are equal or not.</returns>
    public static bool AreFloatsEqual(float a, float b, float ep = Epsilon) => Math.Abs(a - b) < ep;
}

// ReSharper disable once ConvertIfStatementToReturnStatement
public static class V3Extensions
{
    /// <summary>
    /// Calculates the normal of three points.
    /// </summary>
    /// <returns>Normal Vector to provided points.</returns>
    public static Vector3 GetNormal(float sx, float sy, float sz)
    {
        if (sx < sy && sx < sz)
            return new Vector3(-Math.Sign(sx), 0, 0);
        if (sy < sz)
            return new Vector3(0, -Math.Sign(sy), 0);
        return new Vector3(0, 0, -Math.Sign(sz));
    }
}