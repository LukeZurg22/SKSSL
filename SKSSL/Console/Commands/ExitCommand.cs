using JetBrains.Annotations;

namespace SKSSL.Console.Commands;

[UsedImplicitly]
internal class ExitCommand : IConsoleCommand
{
    public string Command => "exit";
    public string Name => "exit";
    public string Description => "Forcefully exits the game.";

    public string Execute(string?[] arguments)
    {
        GameManager.Exit();
        return "Exiting the game";
    }
}