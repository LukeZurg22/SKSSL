using System;

namespace SKSSL.Console.Commands;

internal class ClearScreenCommand: IConsoleCommand
{
    public string Name => "clear";
    public string Command => "clear";
    public string Description => "Clears the console output";

    private readonly ConsoleRenderer consoleRenderer;
    public ClearScreenCommand(ConsoleRenderer consoleRenderer) => this.consoleRenderer = consoleRenderer;

    private Action TAs;
    
    void Test()
    {
        TAs = Test;
    }
    
    public string Execute(string?[]? arguments = null)
    {
        consoleRenderer.Clear();
        return string.Empty;
    }
}