using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.Localization;

/// <summary>
/// A public class used in acquiring the localization of any object defined in the .loc files of the Localization folder.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class Loc
{
    const string defaultLanguage = "en-US";
    public static string CurrentLanguage = defaultLanguage; // TODO: Load this from settings
    public static readonly string SystemCulture;

    internal static ConcurrentDictionary<string, string> Localizations { get; }

    static Loc()
    {
        SystemCulture = CultureInfo.CurrentCulture.Name;
        Localizations = new ConcurrentDictionary<string, string>();
        switch (SystemCulture)
        {
            case "en-US": // TODO: Add dynamically-supported languages rather than statically-defined.
            case "de-DE":
            case "fr-FR":
                Thread.CurrentThread.CurrentCulture = new CultureInfo(SystemCulture);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(SystemCulture);
                CurrentLanguage = SystemCulture;
                break;
            default: // English is the default if the culture isn't found, as well as being an option.
                CurrentLanguage = defaultLanguage;
                break;
        }
    }

    /// <summary>
    /// Returns a localized string. However, localizations can have variables, which are defined in {$brackets} with an
    /// accompanying '$' to indicate that it is indeed a localizable variable. The parser ignores all lines
    /// beginning with '#'.
    /// </summary>
    /// <example>
    /// Example Usage:
    /// <code>
    ///public void MyFunctionName()
    ///{
    ///     // Define in a folder within Localizations the following:
    ///     // my-localization-id = There are {$alpha} ways to paint something {$beta}
    ///     //
    ///     // Then call the following function:
    ///     var myLocale = Loc.Get("my-localization-id",
    ///         ("alpha", 25)
    ///         ("beta", Color.White)
    ///     );
    /// }
    /// </code>
    /// </example>
    public static string Get(string localeID, params (string variableName, object variableValue)[]? values)
    {
        string output = Localizations.GetValueOrDefault(localeID, localeID);
        if (values == null)
            return output; // If there's no provided values, then the existing localization will be enough.

        // In the case that there are multiple variables of the same name, this shouldn't budge or break.
        // However, it is not advised to do so in one's localizations, because it's... stupid?
        foreach ((string key, object value) in values)
        {
            string placeholder = "{$" + key + "}";
            output = output.Replace(placeholder, value.ToString() ?? "invalid-value");
        }

        return output;
    }

    /// <summary>
    /// Clears and initializes localization depending on the current assigned language culture.
    /// </summary>
    /// <param name="localizationFolder">Directory Path of the localization folder, which contains sub-folders based on language culture.</param>
    public static void Initialize(string localizationFolder)
    {
        Localizations.Clear();

        string language = CultureInfo.CurrentCulture.Name; // e.g., "en-US", "de-DE"

        // Attempt to use requested language folder
        string folder = Path.Combine(localizationFolder, language);

        // Fall back to default if missing
        if (!Directory.Exists(folder))
        {
            Debug.WriteLine(
                $"Localization folder for \"{language}\" does not exist! Using default \"{defaultLanguage}\" instead.",
                nameof(Loc));
            folder = Path.Combine(localizationFolder, defaultLanguage);
        }

        var files = Directory.GetFiles(folder, "*.ftl*", SearchOption.AllDirectories);
        Parallel.ForEach(files, file =>
        {
            string[] contents = File.ReadAllLines(file);
            foreach (string line in contents)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int index = line.IndexOf('=');
                if (index == -1 || line[0].Equals('#'))
                {
                    if (!line[0].Equals('#'))
                        DustLogger.Log( $"Invalid localization in file \"{file}\": {line}", 3);
                    continue;
                }

                string key = index >= 0 ? line[..index].Trim() : line;
                string value = index >= 0 ? line[(index + 1)..].Trim() : key;
                Localizations[key] = value;
            }
        });
    }
}