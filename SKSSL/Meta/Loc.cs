using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Net;
using Fluent.Net.RuntimeAst;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL;

/// <summary>
/// A public class used in acquiring the localization of any object defined in the .loc files of the Localization folder.
/// </summary>
public static class Loc
{
    private const string defaultLanguage = "en-US";
    public static string CurrentLanguage; // TODO: Load this from settings

    /// <summary>
    /// The localization entries as stored in the game's per-language-culture folder.
    /// Consists of a list of values and (=) keys.
    /// </summary>
    static Loc()
    {
        string systemCulture = CultureInfo.CurrentCulture.Name;
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
    public static string Get(string? localeID, params (string variableName, object variableValue)[]? values)
    {
        if (localeID == null)
            return "";

        foreach (MessageContext messageContext in MessageContexts)
        {
            Message? message = messageContext.GetMessage(localeID);

            if (message is null)
                continue;

            var args = values?.ToDictionary(
                v => v.variableName,
                v => v.variableValue
            );

            return messageContext.Format(message, args, new List<FluentError>());
        }

        return localeID;
    }

    /// <summary>
    /// Clears and initializes localization depending on the current assigned language culture.
    /// Localization folder path is optional, which is assigned the default path or vice versa depending on nullability.
    /// </summary>
    /// <param name="localeDirectory">
    /// Localization directory, which contains which contains sub-folders based on language culture.
    /// </param>
    public static void Load(string localeDirectory)
    {
        ParseExceptions.Clear();

        // Loading localizations should be clear on program init.

        // Get user's current language culture.
        string language = CultureInfo.CurrentCulture.Name; // e.g., "en-US", "de-DE"

        // Attempt to use requested language folder
        string languageFolder = Path.Combine(localeDirectory, language);

        // Fall back to default if missing
        if (!Directory.Exists(languageFolder))
        {
            Log(
                $"Localization folder for \"{language}\" does not exist! Using default \"{defaultLanguage}\" instead.\n" +
                $"{localeDirectory}",
                LOG.FILE_WARNING);
            languageFolder = Path.Combine(localeDirectory, defaultLanguage);
        }

        // If it still doesn't exist after it was defaulted, then something is wrong, and this directory
        //  should be ignored.
        if (!Directory.Exists(languageFolder))
        {
            Log($"Localization folder for \"{language}\" does not exist, either! See filepath above!", LOG.FILE_ERROR);
            return;
        }

        // Get all localization files and load them.
        var files = Directory.GetFiles(languageFolder, "*.ftl*", SearchOption.AllDirectories);
        Parallel.ForEach(files, ProcessLocaleFile);

        // Spew exceptions.
        foreach (ParseException exception in ParseExceptions)
            Log(exception.Message, LOG.FILE_WARNING);
    }

    private static readonly ConcurrentBag<MessageContext> MessageContexts = [];
    private static readonly IList<ParseException> ParseExceptions = new List<ParseException>();

    private static MessageContext GetMessages(string ftlPath)
    {
        using var streamReader = new StreamReader(ftlPath);
        var options = new MessageContextOptions { UseIsolating = false };
        var messageContext = new MessageContext(CurrentLanguage, options);
        var errors = messageContext.AddMessages(streamReader);
        foreach (ParseException? error in errors)
        {
            ParseExceptions.Add(error);
        }

        return messageContext;
    }

    /// Read all file contents and insert into dictionary.
    private static void ProcessLocaleFile(string file)
    {
        MessageContext messageContext = GetMessages(file);
        MessageContexts.Add(messageContext);
    }
}