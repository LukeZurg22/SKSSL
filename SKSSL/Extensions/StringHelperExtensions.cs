using System.Globalization;
using System.Text.RegularExpressions;

namespace SKSSL.Extensions;

public static class StringHelpers
{
    /// <summary>
    /// Removes "..._&lt;value&gt;" from the end of a string value.
    /// </summary>
    /// <returns>Provided string value with any endings removed.</returns>
    public static string RemoveUnderscoreEndingTag(this string value)
    {
        int i = value.LastIndexOf('_');
        if (i >= 0)
            value = value[..i];
        return value;
    }
    
    /// <returns>A "..._&lt;value&gt;" ending tag from a provided string value.</returns>
    public static string GetUnderscoreEndingTag(this string value)
    {
        int i = value.LastIndexOf('_');
        return i >= 0 ? value[(i + 1)..] : "";
    }
    
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
    [Obsolete("Use TryParseValue(this string...) instead.")]
    public static object? TryParseValue(string line, Type? enumType = null)
    {
        line = line.Split('#')[0].Trim(); // remove comments
        if (string.IsNullOrEmpty(line))
            return null;

        // Try bool
        var boolResult = TryParseValue<bool>(line);
        if (boolResult.HasValue) return boolResult.Value;

        // Try integer
        var intResult = TryParseValue<int>(line);
        if (intResult.HasValue) return intResult.Value;

        // Try short
        var shortResult = TryParseValue<short>(line);
        if (shortResult.HasValue) return shortResult.Value;

        // Try long
        var longResult = TryParseValue<long>(line);
        if (longResult.HasValue) return longResult.Value;

        // Try floats/doubles
        var floatResult = TryParseValue<float>(line);
        if (floatResult.HasValue) return floatResult.Value;

        // Try double
        var doubleResult = TryParseValue<double>(line);
        if (doubleResult.HasValue) return doubleResult.Value;

        // Try enum if provided
        if (enumType != null && Enum.IsDefined(enumType, line))
            return Enum.Parse(enumType, line);

        return line; // Default to string
    }
    
    /// Returns new string in camel_case.
    public static string ToCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove non-alphanumeric characters and capitalize words
        string result = Regex.Replace(input, "[^a-zA-Z0-9]+", " ");
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        result = textInfo.ToTitleCase(result).Replace(" ", "");

        // Lowercase first letter
        return char.ToLower(result[0]) + result[1..];
    }

    /// <summary>
    /// Extension method for <see cref="string.IsNullOrEmpty"/>
    /// </summary>
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    
    /// <summary>
    /// Extension method for <see cref="string.IsNullOrWhiteSpace"/>
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
}