using SKSSL.Console;
using XnaGame = Microsoft.Xna.Framework.Game;


namespace SolKom.Console.Commands
{
    internal class ExitCommand : IConsoleCommand
    {
        public string Command => "exit";
        public string Name => "exit";
        public string Description => "Forcefully exits the game.";

        private readonly XnaGame? game;
        public ExitCommand(XnaGame? game)
        {
            this.game = game;
        }
        public string Execute(string?[] arguments)
        {
            game.Exit();
            return "Exiting the game";
        }
    }
}