using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Console;

internal class GameConsoleComponent : DrawableGameComponent
{
    public bool IsOpen => consoleRenderer.IsOpen;
    private readonly GameConsole console;
    private readonly SpriteBatch spriteBatch;
    internal readonly InputProcessor inputProcesser;
    internal readonly ConsoleRenderer consoleRenderer;

    /// Access the renderer using SSLGame instance and hope it's there.
    public static ConsoleRenderer? GetRenderer()
        => (SSLGame.Instance.Components.First(gameComponent => gameComponent is GameConsoleComponent)
            as GameConsoleComponent)?.consoleRenderer;

    public GameConsoleComponent(GameConsole console, Game? game, SpriteBatch spriteBatch) : base(game)
    {
        this.console = console;
        this.spriteBatch = spriteBatch;
        inputProcesser = new InputProcessor();
        inputProcesser.Open += (_, _) => consoleRenderer?.Open();
        inputProcesser.Close += (_, _) => consoleRenderer?.Close();
        consoleRenderer = new ConsoleRenderer(game, spriteBatch, inputProcesser);
    }

    public override void Draw(GameTime gameTime)
    {
        if (!console.Enabled)
            return;

        spriteBatch.Begin();
        consoleRenderer.Draw(gameTime);
        spriteBatch.End();
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