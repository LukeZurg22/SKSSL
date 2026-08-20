namespace SKSSL.Console.Command;

[JetBrains.Annotations.UsedImplicitly, RegisterCommand]
public class ClearScreenCommand : IConsoleCommand
{
    public string Name => "clear";
    public string Command => "clear";
    public string Description => "Clears the console output";

    public string Execute(params string[] arguments)
    {
        GameConsoleComponent.GetRenderer()?.Clear();
        return string.Empty;
    }
}