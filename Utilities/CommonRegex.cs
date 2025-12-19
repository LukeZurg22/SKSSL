using System.Text.RegularExpressions;

namespace SKSSL.Utilities;

/// <summary>
/// Establishes common REGEX patterns used elsewhere.
/// </summary>
public static partial class CommonRegex
{
    /// <summary>
    /// For strings that begin with a letter or underscore, and the remaining are alphanumeric or underscores.
    /// This documentation is overwritten by REGEX, anyway.
    /// </summary>
    [GeneratedRegex(@"^[_A-Za-z][A-Za-z0-9_]*$")]
    public static partial Regex AlphaAlphaNumericUnderlineRegex();
}