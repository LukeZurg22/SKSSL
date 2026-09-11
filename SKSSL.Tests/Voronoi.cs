using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Tests.Classes;
using SKSSL.Utilities.Voronoi;

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private const bool TOGGLE_EXECUTABLE_CLOSURE = false;
    private Utilities.Voronoi.Voronoi _voronoi;
    bool generated = false;
    private UnitGame _game;
    private Texture2D _diagram = null!;

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        SpriteBatch spriteBatch = null!;
        using var game = new UnitGame(() => { }, () => { _voronoi.LoadContent(); }, Update, Draw);
        _game = game;
        spriteBatch = new SpriteBatch(game.GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(game.GraphicsDevice, spriteBatch);
        game.Run();
        Assert.IsTrue(generated);
        return;

        // ReSharper disable AccessToModifiedClosure
        void Draw(GameTime gameTime)
        {
            //_voronoi.Draw(spriteBatch!);
            spriteBatch?.Begin();
            spriteBatch?.Draw(_diagram, Vector2.Zero, Color.White);
            spriteBatch?.End();
        }
    }

    private void Update(GameTime gameTime)
    {
        if (generated)
        {
            // The toggle is there for utility.
            if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162 // Unreachable code detected
                // ReSharper disable once HeuristicUnreachableCode
                _game.Quit();
#pragma warning restore CS0162 // Unreachable code detected
            return;
        }

        _diagram = _voronoi.GenerateDiagram(
            pointCount: 2060,
            boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.Culled,
            distribution: DelaunayTriangulator.PointDistribution.RandomJitter,
            flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells);
        generated = true;
    }
}