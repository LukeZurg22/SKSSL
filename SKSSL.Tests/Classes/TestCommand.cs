using System;
using SKSSL.Console;

namespace SKSSL.Tests.Classes;

[RegisterCommand]
public class TestCommand : IConsoleCommand
{
    public bool CanExecute(object parameter)
    {
        throw new NotImplementedException();
    }

    public void Execute(object parameter)
    {
        throw new NotImplementedException();
    }

    public event EventHandler CanExecuteChanged;
    public string Handle { get; }
    public string Usage { get; }
    public string Description { get; }
    public string Execute(string[] arguments)
    {
        throw new NotImplementedException();
    }
}