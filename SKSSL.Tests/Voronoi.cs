using System.Threading;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi;

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private Utilities.Voronoi.Voronoi _voronoi;

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        bool generated = false;
        Texture2D texture = null!;
        SpriteBatch spriteBatch = null!;
        using var game = new UnitGame(Initialize, LoadContent, Update, Draw);
        spriteBatch = new SpriteBatch(game.GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(game.GraphicsDevice, spriteBatch);
        game.Run();
        Thread.Sleep(1000);
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
            texture = _voronoi.GenerateDiagram(
                distribution: DelaunayTriangulator.PointDistribution.RandomJitter,
                randomness: 0.4f,
                flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.CellsAndEdges);
            generated = true;
        }

        void Draw(GameTime gameTime)
        {
            // ReSharper disable once AccessToModifiedClosure
            //_voronoi.Draw(spriteBatch!);
            spriteBatch.Begin();
            spriteBatch.Draw(texture, Vector2.Zero, Color.White);
            spriteBatch.End();
        }
    }
}