// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
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
    public static bool AreFloatsEqual(float a, float b) => Math.Abs(a - b) < Epsilon;
}