using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SKSSL.Tests;
/*internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var game = new TestGame();
        game.Run();
    }
}*/

/// <summary>
/// Test game class for direct pseudo "live environment" tests.
/// </summary>
/// <remarks>
/// Content Loader support is minimal, if nonexistent.
/// </remarks>
internal sealed class UnitGame : SSLGame
{
    private readonly GraphicsDevice _graphics;

    public UnitGame()
    {
        _graphics = this.GraphicsDevice;

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        // Load whatever you're testing here.
        // Example:
        //
        // var texture = Content.Load<Texture2D>("test_texture");

        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        base.Draw(gameTime);
    }
}