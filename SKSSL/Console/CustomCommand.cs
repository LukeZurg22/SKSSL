using System;
using SKSSL.Console;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace SolKom.Console.Commands
{
    public class CustomCommand : IConsoleCommand, IComparable<IConsoleCommand>
    {
        public string Name { get; private set; }
        public string Command { get; private set; }
        public string Description { get; private set; }

        private readonly Func<string[], string> action;

        public CustomCommand(string name, string command, Func<string[], string> action, string description)
        {
            Command = name;
            Name = command;
            Description = description;
            this.action = action;
        }

        public string Execute(string?[] arguments)
        {
            return action(arguments);
        }

        public int CompareTo(IConsoleCommand? other)
            => string.Compare(Command, other?.Command, StringComparison.Ordinal);
    }
}