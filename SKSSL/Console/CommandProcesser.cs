using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SKSSL.Console
{
    internal class CommandProcessor
    {
        public static string Process(string buffer)
        {
            string commandName = GetCommandName(buffer);
            IConsoleCommand? command = GameConsoleOptions.Commands.FirstOrDefault(c => c.Name == commandName);
            var arguments = GetArguments(buffer);
            if (command == null)
            {
                return "ERROR: Command not found";
            }

            string commandOutput;
            try
            {
                commandOutput = command.Execute(arguments);
            }
            catch (Exception ex)
            {
                commandOutput = "ERROR: " + ex.Message;
            }

            return commandOutput;
        }

        private static string GetCommandName(string buffer)
        {
            var firstSpace = buffer.IndexOf(' ');
            return buffer[..(firstSpace < 0 ? buffer.Length : firstSpace)];
        }

        private static string?[] GetArguments(string buffer)
        {
            var firstSpace = buffer.IndexOf(' ');
            if (firstSpace < 0)
            {
                return []; // No arguments
            }

            // Extract the part of the buffer after the command
            var argsPart = buffer[(firstSpace + 1)..];
            var arguments = new List<string>();
            var currentArgument = new StringBuilder();
            bool insideQuotes = false;

            foreach (char c in argsPart)
            {
                switch (c)
                {
                    // Toggle insideQuotes state
                    case '"':
                        insideQuotes = !insideQuotes;
                        break;
                    // Split on spaces outside quotes
                    case ' ' when !insideQuotes:
                    {
                        if (currentArgument.Length > 0)
                        {
                            arguments.Add(currentArgument.ToString());
                            currentArgument.Clear();
                        }

                        break;
                    }
                    default:
                        currentArgument.Append(c);
                        break;
                }
            }

            // Add the last argument if there’s any
            if (currentArgument.Length > 0)
            {
                arguments.Add(currentArgument.ToString());
            }

            return arguments.ToArray();
        }
    }
}