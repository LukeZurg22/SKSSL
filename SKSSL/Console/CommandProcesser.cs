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
            IConsoleCommand? command = GameConsoleOptions.Commands.FirstOrDefault(c => c.Handle == commandName);
            string[] arguments = GetArguments(buffer);

            if (command == null)
            {
                return $"[INVALID COMMAND] No command found! {buffer}";
            }

            string commandOutput;

            // Try-catch to prevent commands from ever crashing the program. This means all commands are wrapped
            //  in an overhead-inducing try-catch statement, but there is little more one can do about safety without
            //  demanding that all implementations of commands also include their own try-catch statements. The logging
            //  without writing to file keeps the file output clean, and the console itself will spit out an error.
            try
            {
                commandOutput = command.Execute(arguments);
            }
            catch (Exception ex)
            {
                commandOutput = "[COMMAND ERROR]: " + ex.Message;
                Log(ex, LOG.SYSTEM_WARNING, false);
            }

            return commandOutput;
        }

        private static string GetCommandName(string buffer)
        {
            var firstSpace = buffer.IndexOf(' ');
            return buffer[..(firstSpace < 0 ? buffer.Length : firstSpace)];
        }

        private static string[] GetArguments(string buffer)
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