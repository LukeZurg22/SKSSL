namespace SKSSL.Console;

/// <summary>
/// Use <see cref="RegisterCommandAttribute"/> to register commands. This interface is for interaction in the command
/// handler. Commands MUST be public!
/// </summary>
public interface IConsoleCommand
{
    /// <summary>
    /// Handle of the command to invoke.
    /// </summary>
    string Handle { get; }

    /// <summary>
    /// Developer-provided usage guide. (I.e. "MyCommand (my param) (other param)")
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// Verbose description that is displayed with the 'help' command.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The action of the command. The return string value is used as output in the console.
    /// </summary>
    /// <param name="arguments">Optional string parameters, which can be neglected for once-off commands.</param>
    string Execute(params string[] arguments);
}