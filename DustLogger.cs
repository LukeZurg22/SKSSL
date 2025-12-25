using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace SKSSL;

/// <summary>
/// A built-in error handler coming from the DustToDust project. This follows Microsoft's guidelines for
/// performance-efficient error logging into a console. The errors type's here are generalized for use elsewhere.
/// </summary>
public static partial class DustLogger
{
    private static readonly ILogger logger;

    /// <summary>
    /// Enumerable containing available error codes which are used in the <see cref="DustLogger"/>.
    /// <code>
    /// [ERROR CODE ENTRY]  [CODE]      
    /// INFORMATIONAL_PRINT = 0x0,
    ///         -warnings-
    /// GENERAL_WARNING     = 0x1,
    /// META_DATA_WARNING   = 0x2,
    /// FILE_WARNING        = 0x3,
    /// SYSTEM_WARNING      = 0x4,
    ///         -errors-
    /// GENERAL_ERROR       = 0x5,
    /// META_DATA_ERROR     = 0x6,
    /// FILE_ERROR          = 0x7,
    /// SYSTEM_ERROR        = 0x8,
    /// </code>
    /// </summary>
    public enum LOG : byte
    {
        // Info
        INFORMATIONAL_PRINT = 0x0,

        // Warnings
        /// General warning. Not unimportant enough to be INFO.
        /// Default to this if generally unsure.
        GENERAL_WARNING = 0x1,

        /// Warning concerning invalid metadata.
        META_DATA_WARNING = 0x2,

        /// Warning concerning [de]serialization.
        FILE_WARNING = 0x3,

        /// Warning of possible system or operating system issue.
        SYSTEM_WARNING = 0x4,

        // Errors
        /// Error with no specific root cause. Default to this if unsure, but there is definitely a problem.
        GENERAL_ERROR = 0x5,

        /// Error involving file metadata.
        META_DATA_ERROR = 0x6,

        /// Error involving a file's contents.
        FILE_ERROR = 0x7,

        /// Error involving system failure.
        SYSTEM_ERROR = 0x8,
    }

#pragma warning disable CA2017
    internal static readonly Action<ILogger, string, Exception?> INFO_PRINT =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId((byte)LOG.INFORMATIONAL_PRINT, nameof(INFO_PRINT)), "[INFO]: {Message}");

    internal static readonly Action<ILogger, Exception?> GENERAL_WARNING =
        LoggerMessage.Define(LogLevel.Warning, new EventId((byte)LOG.GENERAL_WARNING, nameof(GENERAL_WARNING)),
            "[GENERAL WARNING]: {Message}");

    internal static readonly Action<ILogger, Exception?> META_WARNING =
        LoggerMessage.Define(LogLevel.Warning, new EventId((byte)LOG.META_DATA_WARNING, nameof(META_WARNING)),
            "[METADATA WARNING]: {Message}");

    internal static readonly Action<ILogger, Exception?> FILE_WARNING =
        LoggerMessage.Define(LogLevel.Warning, new EventId((byte)LOG.FILE_WARNING, nameof(FILE_WARNING)),
            "[FILE WARNING]: {Message}");

    internal static readonly Action<ILogger, Exception?> SYSTEM_WARNING =
        LoggerMessage.Define(LogLevel.Warning, new EventId((byte)LOG.SYSTEM_WARNING, nameof(SYSTEM_WARNING)),
            "[SYSTEM WARNING]: {Message}");

    internal static readonly Action<ILogger, Exception?> GENERAL_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId((byte)LOG.GENERAL_ERROR, nameof(GENERAL_ERROR)),
            "[GENERAL ERROR]: {Message}");

    internal static readonly Action<ILogger, Exception?> META_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId((byte)LOG.META_DATA_ERROR, nameof(META_ERROR)),
            "[METADATA ERROR]: {Message}");

    internal static readonly Action<ILogger, Exception?> FILE_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId((byte)LOG.FILE_ERROR, nameof(FILE_ERROR)),
            "[FILE ERROR]: {Message}");

    internal static readonly Action<ILogger, Exception?> SYSTEM_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId((byte)LOG.SYSTEM_ERROR, nameof(SYSTEM_ERROR)),
            "[SYSTEM ERROR]: {Message}");
#pragma warning restore CA2017

    static DustLogger()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder
            => builder.SetMinimumLevel(LogLevel.Debug).AddConsole()); // Should work with console.
        logger = loggerFactory.CreateLogger<Program>();
    }

    /// <inheritdoc cref="Log(string,SKSSL.DustLogger.LOG,bool)"/>
    /// Overload using enum, which is cast to byte.
    public static void Log(string message, LOG log, bool outputToFile = false) => Log(message, (byte)log, outputToFile);

    /// <summary>
    /// <seealso cref="LOG"/>
    /// </summary>
    /// <param name="message">The message that is being output to console.</param>
    /// <param name="level">Logging level and type. Defaults to 0 (INFO).</param>
    /// <param name="outputToFile">Dictates if this message should be logged.</param> // TODO: File-logging is not implemented yet!
    public static void Log(string message, int level = 0, bool outputToFile = false)
    {
        var e = (LOG)level; // cast to internal enum
        var exception = new Exception(message);
        switch (e)
        {
            // Errors
            case LOG.GENERAL_ERROR:
                GENERAL_ERROR(logger, exception);
                break;
            case LOG.META_DATA_ERROR:
                META_ERROR(logger, exception);
                break;
            case LOG.FILE_ERROR:
                FILE_ERROR(logger, exception);
                break;
            case LOG.SYSTEM_ERROR:
                SYSTEM_ERROR(logger, exception);
                break;
            // Warnings
            case LOG.META_DATA_WARNING:
                META_WARNING(logger, exception);
                break;
            case LOG.GENERAL_WARNING:
                GENERAL_WARNING(logger, exception);
                break;
            case LOG.FILE_WARNING:
                FILE_WARNING(logger, exception);
                break;
            case LOG.SYSTEM_WARNING:
                SYSTEM_WARNING(logger, exception);
                break;
            // Info
            case LOG.INFORMATIONAL_PRINT:
            default:
                INFO_PRINT(logger, message, null);
                break;
        }
    }
}