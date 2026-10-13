using System.Linq;
using System.Text;

namespace SKSSL.Console.Command;

[JetBrains.Annotations.UsedImplicitly, RegisterCommand]
public class HelpCommand : IConsoleCommand
{
    public string Handle => "help";
    public string Usage => "help <command>";

    public string Description => "Displays the list of commands, or more information of a particular command fed in the <command> parameter.";

    public string Execute(params string[] arguments)
    {
        // For displaying specific commands.
        if (arguments.Length >= 1)
        {
            IConsoleCommand? command = GameConsoleOptions.Commands.FirstOrDefault(c => c.Handle == arguments[0]);
            if (command != null)
            {
                return $"{command.Usage}: {command.Description}\n";
            }

            return "ERROR: Invalid command '" + arguments[0] + "'";
        }

        // For displaying all commands at once in a simple list, making this multi-use.
        var help = new StringBuilder();
        GameConsoleOptions.Commands.Sort();
        foreach (IConsoleCommand command in GameConsoleOptions.Commands)
        {
            help.Append($"{command.Usage}\n");
        }

        return help.ToString();
    }
}