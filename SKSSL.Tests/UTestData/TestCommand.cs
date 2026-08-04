using System;
using System.Windows.Input;
using SKSSL.Console;

namespace SKSSL.Tests.UTestData;

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
    public string Name { get; }
    public string Command { get; }
    public string Description { get; }
    public string Execute(string[] arguments)
    {
        throw new NotImplementedException();
    }
}