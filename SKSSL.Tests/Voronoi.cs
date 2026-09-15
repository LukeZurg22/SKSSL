#nullable enable
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
    private Utilities.Voronoi.Voronoi _voronoi = null!;
    private bool generated = false;
    private UnitGame _game = null!;
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
        void Draw(GameTime gameTime) => _voronoi.Draw(spriteBatch);
    }


    private /*async*/ void Update(GameTime gameTime)
    {
        if (generated)
        {
            MouseState mouse = Mouse.GetState();
            Point mousePosition = new(mouse.X, mouse.Y);

            _voronoi.TryGetCellAt(mousePosition, out _hoveredCell);
            if (_hoveredCell != null)
                _voronoi.SetHighlightedCells([_hoveredCell], Color.Black);

            if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162
                // ReSharper disable once HeuristicUnreachableCode
                _game.Quit();
#pragma warning restore CS0162

            return;
        }

        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        /*await Task.Run(() =>*/
        _voronoi.GenerateDiagram(pointCount: 40000,
            //width: 1200,
            //height: 800,
            distribution: DelaunayTriangulator.PointDistribution.RandomJitter,
            boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.HardEdge,
            flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells) /*)*/;

        for (int i = 0; i <= 2040; i++) _voronoi.MarkCell(i);

        _voronoi.ColorMarkedCells(Color.BlanchedAlmond, Color.DarkGreen);
        generated = true;
    }
}