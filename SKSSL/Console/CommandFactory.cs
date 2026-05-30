using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SKSSL.Utilities;

namespace SKSSL.Console;

public static class CommandFactory
{
    public static List<IConsoleCommand> CreateCommands()
    {
        Type commandType = typeof(IConsoleCommand); // Assuming ICommand is the interface for commands
        var assembly = Assembly.GetExecutingAssembly(); // Get the current assembly

        // Find all types in the assembly that have the [Command] attribute and implement ICommand
        var commandTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<CommandAttribute>() != null && commandType.IsAssignableFrom(t))
            .ToList();

        // Instantiate each command
        var commands = commandTypes.Select(Activator.CreateInstance).Cast<IConsoleCommand>().ToList();
        return commands;
    }
}