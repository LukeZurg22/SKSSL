#nullable enable
using System.IO;
using FontStashSharp;
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

            if (_hoveredCell != null)
            {
                spriteBatch.Begin();
                Vector2 position = Vector2.Zero;
                spriteBatch.DrawString(font, _hoveredCell.ID.ToString(), position, Color.Wheat);
                position = new Vector2(0, 25);
                spriteBatch.DrawString(font, _hoveredCell.Vertices.Count.ToString(), position, Color.Wheat);
                position = new Vector2(0, 50);
                spriteBatch.DrawString(font, _hoveredCell.Site.ToString(), position, Color.Wheat);
                spriteBatch.End();
            }
        }
    }

    private SpriteFontBase font = null!;

    private /*async*/ void Update(GameTime gameTime)
    {
        if (generated)
        {
            MouseState mouse = Mouse.GetState();
            Point mousePosition = new(mouse.X, mouse.Y);

            _voronoi.TryGetCellAt(mousePosition, out _hoveredCell);
            if (_hoveredCell != null)
                _voronoi.DemarcateCells([_hoveredCell], Color.Black);

            if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162
                // ReSharper disable once HeuristicUnreachableCode
                _game.Quit();
#pragma warning restore CS0162

            return;
        }

        byte[] fontData = File.ReadAllBytes("ARIAL.ttf");
        var fontSystem = new FontSystem();
        fontSystem.AddFont(fontData);
        font = fontSystem.GetFont(32);

        Texture2D image = new EmbeddedContentManager(_game, typeof(Voronoi).Assembly)
            .LoadEmbeddedTexture(_game.GraphicsDevice, "PLAIN_ROUND.png");
        _densityTexture = image;

        // Temp code to dynamically create a circular density map.
        //UtilityImageGenerator.CreateCircularDensityMap(
        //    _game.GraphicsDevice,
        //    _game.GraphicsDevice.Viewport.Width,
        //    _game.GraphicsDevice.Viewport.Height
        //);

        DistributorImage distributor = new(_densityTexture, new DistributorImageSettings
        {
            DensityIntensity = 1.1,
            CenterCellSizeFactor = 0.8,
            EdgeCellSizeFactor = 1.7,
            AllowBlackGaps = true,
        });

        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        var diagramSettings = new Utilities.Voronoi.Voronoi.DiagramSettings
        {
            Randomness = 0,
            SettlePoints = false,
            Distributor = distributor,
            BoundaryMode = Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.Culled,
            Flags = Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells |
                    Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Points |
                    Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Edges /*|
                        Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Triangles*/
        };
        const int points = 11105;
        _voronoi.GenerateDiagram(
            points: points,
            //width: 1200,
            //height: 800,
            settings: diagramSettings
        );

        const int segmentSize = 255;

        for (uint i = 0; i < points; i++)
        {
            int segment = (int)(i / segmentSize);
            int value = (int)(i % segmentSize) * 255 / (segmentSize - 1);

            Color color = (segment % 3) switch
            {
                0 => new Color((byte)value, 0, 0),
                1 => new Color(0, (byte)value, 0),
                _ => new Color(0, 0, (byte)value)
            };

            _voronoi.ChangeCellColor(i, color);
        }
        _voronoi.ForceUpdate();

        //_voronoi.ColorMarkedCells(Color.DarkGreen, null, true);
        generated = true;
    }
}