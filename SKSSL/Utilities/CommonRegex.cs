using System.Text.RegularExpressions;
// ReSharper disable UnusedMember.Global

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
    public static readonly Regex AlphaAlphaNumericUnderlineRegex
        = new("^[_A-Za-z][A-Za-z0-9_]*$");

    // Windows reserved device names
    public static readonly Regex ReservedNames
        = new(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase);
}