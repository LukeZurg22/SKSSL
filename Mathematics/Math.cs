namespace SKSSL.Mathematics;

public static class Floats
{
    private const float Epsilon = 0.0001f;
    public static bool AreFloatsEqual(float a, float b) => Math.Abs(a - b) < Epsilon;
}