using System;

namespace SKSSL.Console.Command;

[JetBrains.Annotations.UsedImplicitly, RegisterCommand]
public class ClearScreenCommand : IConsoleCommand
{
    public string Name => "clear";
    public string Command => "clear";
    public string Description => "Clears the console output";


    private Action TAs;
    
    void Test()
    {
        TAs = Test;
    }
    
    public string Execute(string?[]? arguments = null)
    {
        //consoleRenderer.Clear();
        return string.Empty;
    }
}