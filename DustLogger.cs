using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;

using Microsoft.Extensions.Logging.Console;

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
    /// GENERAL_ERROR       = 0x1,
    /// META_DATA_ERROR     = 0x2,
    /// FILE_ERROR          = 0x3,
    /// SYSTEM_ERROR        = 0x4,
    /// </code>
    /// </summary>
    public enum LOG : byte
    {
        INFORMATIONAL_PRINT = 0x0,
        GENERAL_ERROR = 0x1,
        META_DATA_ERROR = 0x2,
        FILE_ERROR = 0x3,
        SYSTEM_ERROR = 0x4,
    }

    internal static readonly Action<ILogger, string, Exception?> INFO_PRINT =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(0, nameof(INFO_PRINT)), "[INFO]: {Message}");

    internal static readonly Action<ILogger, Exception?> GENERAL_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(GENERAL_ERROR)), $"[GENERAL ERROR]");

    internal static readonly Action<ILogger, Exception?> META_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(META_ERROR)), $"[METADATA ERROR]");

    internal static readonly Action<ILogger, Exception?> FILE_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, nameof(FILE_ERROR)), $"[FILE ERROR]");

    internal static readonly Action<ILogger, Exception?> SYSTEM_ERROR =
        LoggerMessage.Define(LogLevel.Error, new EventId(4, nameof(SYSTEM_ERROR)), $"[SYSTEM ERROR]");

    static DustLogger()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder
            => builder.SetMinimumLevel(LogLevel.Debug).AddConsole()
        );
        // WARN: Console isn't set here. It might not even work.

        logger = loggerFactory.CreateLogger<Program>();
    }

    /// <inheritdoc cref="Log(string,SKSSL.DustLogger.LOG,bool)"/>
    /// Overload using enum, which is cast to byte.
    public static void Log(string message, LOG log = LOG.INFORMATIONAL_PRINT, bool outputToFile = false) => Log(message, (byte)log, outputToFile);

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
            case LOG.META_DATA_ERROR:
                META_ERROR(logger, exception);
                break;
            case LOG.GENERAL_ERROR:
                GENERAL_ERROR(logger, exception);
                break;
            case LOG.FILE_ERROR:
                FILE_ERROR(logger, exception);
                break;
            case LOG.SYSTEM_ERROR:
                SYSTEM_ERROR(logger, exception);
                break;
            case LOG.INFORMATIONAL_PRINT:
            default:
                INFO_PRINT(logger, message, null);
                break;
        }
    }
}