using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SolKom.Console.Commands;

namespace SKSSL.Console;

internal class GameConsoleComponent : DrawableGameComponent
{
    public bool IsOpen => consoleRenderer.IsOpen;
    private readonly GameConsole console;
    private readonly SpriteBatch? spriteBatch;
    internal readonly InputProcessor inputProcesser;
    internal readonly ConsoleRenderer consoleRenderer;

    public GameConsoleComponent(GameConsole console, Game? game, SpriteBatch? spriteBatch) : base(game)
    {
        this.console = console;
        this.spriteBatch = spriteBatch;

        inputProcesser = new InputProcessor(new CommandProcessor());
        inputProcesser.Open += (_, _) => consoleRenderer?.Open();
        inputProcesser.Close += (_, _) => consoleRenderer?.Close();
        consoleRenderer = new ConsoleRenderer(game, spriteBatch, inputProcesser);
    }

    public override void Draw(GameTime gameTime)
    {
        if (!console.Enabled)
        {
            return;
        }

        spriteBatch?.Begin();
        consoleRenderer.Draw(gameTime);
        spriteBatch?.End();
        base.Draw(gameTime);
    }

    public override void Update(GameTime gameTime)
    {
        if (!console.Enabled)
        {
            return;
        }

        consoleRenderer.Update(gameTime);
        base.Update(gameTime);
    }

    public void WriteLine(string text) => inputProcesser.AddToOutput(text);
}