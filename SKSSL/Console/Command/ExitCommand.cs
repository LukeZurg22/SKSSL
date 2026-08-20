using System.Threading;

namespace SKSSL.Console.Command;

[JetBrains.Annotations.UsedImplicitly, RegisterCommand]
public class ExitCommand : IConsoleCommand
{
    public string Usage => "exit";
    public string Handle => "exit";
    public string Description => "Forcefully exits the game.";

    public string Execute(params string[] arguments)
    {
        var shutdownThread = new Thread(() =>
        {
            Thread.Sleep(5000);
            GameManager.Exit();
        })
        {
            Name = "ExitCommandThread",
            IsBackground = true
        };
        shutdownThread.Start();
        return "Exiting game.";
    }
}