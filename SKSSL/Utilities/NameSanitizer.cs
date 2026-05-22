using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static SKSSL.Utilities.CommonRegex;

namespace SKSSL.Utilities;

public static partial class NameSanitizer
{
    public static string Sanitize(string input, string replacement = "_", int maxLength = 255)
    {
        if (string.IsNullOrWhiteSpace(input)) return "unnamed";

        // Remove control chars and invalid filename chars
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Normalize(NormalizationForm.FormC))
        {
            if (char.IsControl(ch) || invalid.Contains(ch)) { sb.Append(replacement); continue; }
            sb.Append(ch);
        }

        // Collapse multiple replacements
        var cleaned = Regex.Replace(sb.ToString(), Regex.Escape(replacement) + "{2,}", replacement);

        // Trim spaces/dots from ends
        cleaned = cleaned.Trim().Trim('.', ' ');

        // Avoid reserved names
        if (ReservedNames.IsMatch(cleaned)) cleaned = "_" + cleaned;

        // Enforce max length
        if (cleaned.Length == 0) cleaned = "unnamed";
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength].Trim('.', ' ');

        return cleaned.Length == 0 ? "unnamed" : cleaned;
    }
}