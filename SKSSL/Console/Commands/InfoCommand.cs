using System;
using System.Text;
using SKSSL.Console;
using SKSSL.Utilities;

namespace SolKom.Console.Commands;

[Command]
public class InfoCommand : IConsoleCommand
{
    public string Command => "info x-y-z";
    public string Name => "info";
    public string Description => "Displays info about either a sector (x), system (x-y) or planet (x-y-z).";

    public string Execute(string?[] arguments)
    {
        if (arguments.Length != 1)
            return $"Too few or too many arguments ({arguments.Length})";

        var output = new StringBuilder();
        throw new NotImplementedException();
        return output.ToString();
    }
}