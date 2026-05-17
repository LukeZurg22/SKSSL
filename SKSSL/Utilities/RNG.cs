
// ReSharper disable InconsistentNaming

using RandN;
using RandN.Distributions;

namespace SKSSL.Utilities;

public static class RNG
{
    private static readonly SmallRng RANDOM_NUMBER_GENERATOR = SmallRng.Create();

    /// <summary>
    /// A simple function just to get a random number for testing.
    /// </summary>
    /// <param name="max">Maximum value to generate. This is inclusive.</param>
    /// <returns></returns>
    public static int RN(int max) => RN(0, max);

    /// <summary>
    /// A simple function just to get a random number for testing.
    /// </summary>
    /// <param name="min">Minimum value. This is inclusive.</param>
    /// <param name="max">Maximum value to generate. This is inclusive.</param>
    /// <returns></returns>
    public static int RN(int min, int max) => Uniform.NewInclusive(min, max).Sample(RANDOM_NUMBER_GENERATOR);

    /// <summary>
    /// An assisting Extension function designed to get a random value from an Enumerable.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>A random enum-value from the enum.</returns>
    /// <exception cref="Exception"></exception>
    public static T GetRandomElement<T>() where T : struct, IConvertible
    {
        if (!typeof(T).IsEnum) { throw new Exception("Random enum variable is not an enum."); }

        Array values = Enum.GetValues(typeof(T));
        int index = Uniform.NewInclusive(0, values.Length - 1).Sample(RANDOM_NUMBER_GENERATOR);
        return (T)values.GetValue(index)!;
    }

    public static T RandomEnumValue<T>(int min = 0)
    {
        var values = (T[])Enum.GetValues(typeof(T));
        int index = Uniform.NewInclusive(min, values.Length - 1).Sample(RANDOM_NUMBER_GENERATOR);
        return values[index];
    }
    
    public static string GetRandomElement(string[] array)
    {
        int index = Uniform.NewInclusive(0, array.Length - 1).Sample(RANDOM_NUMBER_GENERATOR);
        return array[index];
    }
    
    public static T GetRandomElement<T>(this T[] array)
    {
        int index = Uniform.NewInclusive(0, array.Length - 1).Sample(RANDOM_NUMBER_GENERATOR);
        return array[index];
    }
}