using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private Utilities.Voronoi.Voronoi _voronoi;

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        bool generated = false;
        SpriteBatch spriteBatch = null!;
        using var game = new UnitGame(Initialize, LoadContent, Update, Draw);
        spriteBatch = new SpriteBatch(game.GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(game.GraphicsDevice);
        game.Run();
        game.Exit();
        return;

        void Initialize()
        {
        }

        void LoadContent()
        {
            _voronoi.LoadContent();
        }


        void Update(GameTime gameTime)
        {
            if (generated)
                return;
            _voronoi.GenerateDiagram();
            generated = true;
        }

        void Draw(GameTime gameTime)
        {
            // ReSharper disable once AccessToModifiedClosure
            _voronoi.Draw(spriteBatch!);
        }
    }
}