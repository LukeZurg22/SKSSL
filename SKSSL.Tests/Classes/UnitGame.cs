#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

// ReSharper disable RedundantOverriddenMember

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
/// Test game class for direct pseudo "live environment" tests. Dedicated method handles can be provided for
/// direct testing through an outer class.
/// </summary>
/// <remarks>
/// Content Loader support is minimal, if nonexistent.
/// </remarks>
internal sealed class UnitGame : SSLGame
{
    private readonly GraphicsDevice _graphics;

    private readonly Action? InitalizeMethod = null!;
    private readonly Action? LoadContentMethod = null!;
    private readonly Action<GameTime>? UpdateMethod = null!;
    private readonly Action<GameTime>? DrawMethod = null!;


    public UnitGame(Action? initialize, Action? loadContent, Action<GameTime>? update, Action<GameTime>? draw)
    {
        InitalizeMethod = initialize;
        LoadContentMethod = loadContent;
        UpdateMethod = update;
        DrawMethod = draw;

        _graphics = GraphicsDevice;

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        InitalizeMethod?.Invoke();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        LoadContentMethod?.Invoke();
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();
        UpdateMethod?.Invoke(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        DrawMethod?.Invoke(gameTime);
        base.Draw(gameTime);
    }
}