using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;

namespace SKSSL.Console.Commands;

[UsedImplicitly]
internal class HelpCommand : IConsoleCommand
{
    public string Name => "help";
    public string Command => "help <x>";

    public string Description => "Displays the list of commands where <x> is an " +
                                 "optional parameter to display more about a particular command.";

    public string Execute(string?[] arguments)
    {
        if (arguments.Length >= 1)
        {
            IConsoleCommand? command = GameConsoleOptions.Commands.FirstOrDefault(c => c.Name == arguments[0]);
            if (command != null)
            {
                return $"{command.Command}\n{command.Description}\n";
            }

            return "ERROR: Invalid command '" + arguments[0] + "'";
        }

        var help = new StringBuilder();
        GameConsoleOptions.Commands.Sort();
        foreach (IConsoleCommand command in GameConsoleOptions.Commands)
        {
            help.Append($"{command.Command}\n");
        }

        return help.ToString();
    }
}