using System;
using System.Linq;
using System.Text;
using SKSSL.Console;

namespace SolKom.Console.Commands
{
    internal class HelpCommand : IConsoleCommand
    {
        public string Command => "help <x>";
        public string Name => "help";
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
}
