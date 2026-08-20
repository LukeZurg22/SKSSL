namespace SKSSL.Console;

/// <summary>
/// Use <see cref="RegisterCommandAttribute"/> to register commands. This interface is for interaction in the command
/// handler. Commands MUST be public!
/// </summary>
public interface IConsoleCommand
{
    /// <summary>
    /// The name of the command
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Invoked through this string
    /// </summary>
    string Command { get; }

    /// <summary>
    /// The description that is displayed with the 'help' command
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The action of the command.  The return string value is used as output in the console
    /// </summary>
    string Execute(params string[] arguments);
}