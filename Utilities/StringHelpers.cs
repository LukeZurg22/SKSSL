#nullable enable
using System;
using System.Globalization;
using System.Linq;

namespace SKSSL.Utilities;

public static class StringHelpers
{
    public static string ToPascalCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Split by underscore or dash
        var parts = input.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    /// Attempts to parse a string value into a numeric or enum-eric one.
    /// </summary>
    /// <param name="key">String that is attempting to be converted.</param>
    /// <param name="providedEnumType">Assuming key is an enum, this is the type of enum expected.</param>
    /// <returns>Object cast to expected type, or the key.</returns>
    public static T? TryParseValue<T>(this string key, Type? providedEnumType = null) where T : struct
    {
        if (string.IsNullOrEmpty(key))
            return null;

        // Get the key's type.
        Type targetType = typeof(T);

        // Try for an integer
        if (targetType == typeof(int) &&
            int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return (T?)(object)i;

        // Perhaps it's a short, instead?
        if (targetType == typeof(short) &&
            short.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            return (T?)(object)s;

        // Somehow, it should be a long?
        if (targetType == typeof(long) &&
            long.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return (T?)(object)l;

        // Clearly, it's a float!
        if (targetType == typeof(float) && float.TryParse(key, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var f))
            return (T?)(object)f;

        // It should be... a double?
        if (targetType == typeof(double) && double.TryParse(key, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var d))
            return (T?)(object)d;

        // It must be an Enum, then! And even better, the user provided the type!
        if (providedEnumType != null && Enum.IsDefined(providedEnumType, key))
            return (T?)Enum.Parse(providedEnumType, key);
        
        // Well clearly, it's an enum, but the correct type was not properly provided.
        return targetType.IsEnum switch
        {
            // Enum
            true when Enum.IsDefined(targetType, key) => (T?)Enum.Parse(targetType, key),
            // Try parsing as name
            true when Enum.TryParse(targetType, key, ignoreCase: true, out var enumResult) => (T)enumResult,
            // Try parsing as numeric
            true when long.TryParse(key, out long numericValue) => (T)Enum.ToObject(targetType, numericValue),
            _ => null // ACH!!!
        };
    }

    /// <summary>
    /// Calls <see cref="TryParseValue{T}"/> to attempt to wanton-grab a value, rather than being predictive.
    /// </summary>
    /// <returns>Object value of parsed value.</returns>
    public static object? TryParseValue(string line, Type? enumType = null)
    {
        line = line.Split('#')[0].Trim(); // remove comments
        if (string.IsNullOrEmpty(line))
            return null;

        // Try bool
        var boolResult = TryParseValue<bool>(line);
        if (boolResult.HasValue) return boolResult.Value;

        // Try integers
        var intResult = TryParseValue<int>(line);
        if (intResult.HasValue) return intResult.Value;

        var shortResult = TryParseValue<short>(line);
        if (shortResult.HasValue) return shortResult.Value;

        var longResult = TryParseValue<long>(line);
        if (longResult.HasValue) return longResult.Value;

        // Try floats/doubles
        var floatResult = TryParseValue<float>(line);
        if (floatResult.HasValue) return floatResult.Value;

        var doubleResult = TryParseValue<double>(line);
        if (doubleResult.HasValue) return doubleResult.Value;

        // Try enum if provided
        if (enumType != null && Enum.IsDefined(enumType, line))
            return Enum.Parse(enumType, line);

        // Default to string
        return line;
    }
}