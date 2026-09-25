#nullable enable
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SKSSL.Assets;
using SKSSL.Tests.Classes;
using SKSSL.Utilities.Voronoi;
using SKSSL.Utilities.Voronoi.PointDistributors;
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
    private Texture2D _densityTexture = null!;

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
            _voronoi.Draw(spriteBatch);

            //spriteBatch.Begin();
            //spriteBatch.Draw(_densityTexture, Vector2.Zero, Color.White);
            //spriteBatch.End();
        }
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

        Texture2D image = new EmbeddedContentManager(_game, typeof(Voronoi).Assembly)
            .LoadEmbeddedTexture(_game.GraphicsDevice, "GG_Map.png");
        _densityTexture = image;

        // Temp code to dynamically create a circular density map.
        //CreateCircularDensityMap(
        //    _game.GraphicsDevice,
        //    _game.GraphicsDevice.Viewport.Width,
        //    _game.GraphicsDevice.Viewport.Height
        //);

        DistributorImage distributor = new(_densityTexture, new DistributorImageSettings
        {
            DensityIntensity = 1.1,
            CenterCellSizeFactor = 0.8,
            EdgeCellSizeFactor = 2.7,
            AllowBlackGaps = false
        });

        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        /*await Task.Run(() =>*/
        _voronoi.GenerateDiagram(points: 50000,
            //width: 1200,
            //height: 800,
            randomness: 0,
            settlePoints: false,
            distributor: distributor,
            boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.Culled,
            flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells /*|
                   Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Points*/ /*| 
                   Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Edges*/);

        //for (int i = 0; i <= 800; i++) _voronoi.MarkCell(i);
        //
        //_voronoi.ColorMarkedCells(Color.BlanchedAlmond, Color.DarkGreen);
        generated = true;
    }
}