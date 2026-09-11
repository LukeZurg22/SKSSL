using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SKSSL.Tests.Classes;
using SKSSL.Utilities.Voronoi;
using Point = System.Drawing.Point;

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private const bool TOGGLE_EXECUTABLE_CLOSURE = false;
    private Utilities.Voronoi.Voronoi _voronoi;
    bool generated = false;
    private UnitGame _game;
    private Texture2D _diagram = null!;
    private VoronoiCell? _hoveredCell;

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        SpriteBatch spriteBatch = null!;
        using var game = new UnitGame(() => { }, () => { _voronoi.LoadContent(); }, Update, Draw);
        _game = game;
        spriteBatch = new SpriteBatch(game.GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(game.GraphicsDevice);
        game.Run();
        Assert.IsTrue(generated);
        return;

        // ReSharper disable AccessToModifiedClosure
        void Draw(GameTime gameTime)
        {
            if (spriteBatch != null)
            {
                _voronoi.Draw(spriteBatch);
            }

        }
    }


    private void Update(GameTime gameTime)
    {
        if (generated)
        {
            MouseState mouse = Mouse.GetState();
            Point mousePosition = new(mouse.X, mouse.Y);

            _voronoi.TryGetCellAt(mousePosition, out _hoveredCell);
            _voronoi.SetHighlightedCells([_hoveredCell], Color.Black);
            
            if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162
                // ReSharper disable once HeuristicUnreachableCode
                _game.Quit();
#pragma warning restore CS0162

            return;
        }

        _diagram = _voronoi.GenerateDiagram(
            width: 800,
            pointCount: 3000,
            boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.Culled,
            distribution: DelaunayTriangulator.PointDistribution.RandomJitter,
            flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Points |
                   Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells |
                   Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Edges);

        generated = true;
    }
}