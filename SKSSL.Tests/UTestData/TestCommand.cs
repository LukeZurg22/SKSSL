using System;
using System.Windows.Input;

namespace SKSSL.Tests.UTestData;

public class TestCommand : ICommand
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
}