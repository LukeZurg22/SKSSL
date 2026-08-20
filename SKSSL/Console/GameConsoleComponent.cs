using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Console;

// WARN: Does not work!
//  1. UPDATE: This needs to be added to the SSLGame components list. Not the ECS components, but the MonoGame Game Comp.
//  list. The real hard part is accessing the console renderer when it is instanced-internal-readonly.
//  SSLGame.Instance may help with this, though, so an equally useful Get-Method would be handy. Might need to be in
//   SSlGame? Not sure about it, maybe direct access is better.
//  2. I made a game setting just for this. That setting still needs to be read.
internal class GameConsoleComponent : DrawableGameComponent
{
    public bool IsOpen => consoleRenderer.IsOpen;
    private readonly GameConsole console;
    private readonly SpriteBatch? spriteBatch;
    internal readonly InputProcessor inputProcesser;
    internal readonly ConsoleRenderer consoleRenderer;

    /// Access the renderer using SSLGame instance and hope it's there.
    public static ConsoleRenderer? GetRenderer()
        => (SSLGame.Instance.Components.First(gameComponent => gameComponent is GameConsoleComponent)
            as GameConsoleComponent)?.consoleRenderer;

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