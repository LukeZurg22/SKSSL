using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using SKSSL.Registry;
// ReSharper disable UnusedType.Global

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.Localization;

/// <summary>
/// A public class used in acquiring the localization of any object defined in the .loc files of the Localization folder.
/// </summary>
public static class Loc
{
    private const string defaultLanguage = "en-US";
    public static string CurrentLanguage = defaultLanguage; // TODO: Load this from settings

    /// <summary>
    /// The localization entries as stored in the game's per-language-culture folder.
    /// Consists of a list of values and (=) keys.
    /// </summary>
    internal static ConcurrentDictionary<string, string> Localizations { get; }

    static Loc()
    {
        string systemCulture = CultureInfo.CurrentCulture.Name;
        Localizations = new ConcurrentDictionary<string, string>();
        switch (systemCulture)
        {
            case "en-US": // TODO: Add dynamically-supported languages rather than statically-defined.
            case "de-DE":
            case "fr-FR":
                Thread.CurrentThread.CurrentCulture = new CultureInfo(systemCulture);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(systemCulture);
                CurrentLanguage = systemCulture;
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
    /// Localization folder path is optional, which is assigned the default path or vice versa depending on nullability.
    /// </summary>
    /// <param name="localePath">Directory Path of the localization folder, which contains sub-folders based on language culture.</param>
    public static void Load(string? localePath = null)
    {
        Localizations.Clear();

        // Cautious handling that permits lazy initialization.
        switch (localePath)
        {
            // If null, then use default localization.
            case null:
                localePath = GameLoader.FOLDER_LOCALIZATION;
                break;
            // Override default localization folder in case a new one was provided. 
            default:
                GameLoader.FOLDER_LOCALIZATION = localePath;
                break;
        }

        // Get user's current language culture.
        string language = CultureInfo.CurrentCulture.Name; // e.g., "en-US", "de-DE"

        // Attempt to use requested language folder
        string languageFolder = Path.Combine(localePath, language);

        // Fall back to default if missing
        if (!Directory.Exists(languageFolder))
        {
            Debug.WriteLine(
                $"Localization folder for \"{language}\" does not exist! Using default \"{defaultLanguage}\" instead.",
                nameof(Loc));
            languageFolder = Path.Combine(localePath, defaultLanguage);
        }

        var files = Directory.GetFiles(languageFolder, "*.ftl*", SearchOption.AllDirectories);
        Parallel.ForEach(files, file =>
        {
            string[] contents = File.ReadAllLines(file);
            foreach (string line in contents)
            {
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // If the line begins with #, or there is no '=', then there's a problem!
                int index = line.IndexOf('=');
                if (index == -1 || line[0].Equals('#'))
                {
                    if (!line[0].Equals('#'))
                        DustLogger.Log($"Invalid localization in file \"{file}\": {line}", 3);
                    continue;
                }

                // Get left (key) and right (value) hand sides, and
                //  add to localizations folder. Get() handles the rest.
                string key = index >= 0 ? line[..index].Trim() : line;
                string value = index >= 0 ? line[(index + 1)..].Trim() : key;
                Localizations[key] = value;
            }
        });
    }
}