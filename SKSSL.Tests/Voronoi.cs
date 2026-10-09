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
using CellMap = SKSSL.Utilities.CellMap;
using Point = System.Drawing.Point;

// ReSharper disable UnusedMember.Local

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private const bool TOGGLE_EXECUTABLE_CLOSURE = false;
    private Utilities.Voronoi.Voronoi _voronoi = null!;
    private CellMap _cellMap = null!;
    private bool generated = false;
    private UnitGame _game = null!;
    private VoronoiCell? _hoveredCell;
    private Texture2D _densityTexture = null!;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFontBase _font = null!;
    private Texture2D _borderMap = null!;
    private const int _points = 10000;

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        using var game = new UnitGame(() => { }, () => { }, MimicUpdate, MimicDraw);
        _game = game;

        // Mimic game object class instantiation.
        _spriteBatch = new SpriteBatch(_game.GraphicsDevice);
        _voronoi = new Utilities.Voronoi.Voronoi(_game.GraphicsDevice);

        //MeasureProfiler.StartCollectingData();
        MimicLoadContent(); // Mimic LoadContent call.
        MimicInitialize(); // Mimic Initialize call.

        //MeasureProfiler.SaveData();
        game.Run();
        Assert.IsTrue(generated);
    }

    #region Initial Loading

    private void MimicInitialize()
    {
        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        var diagramSettings = new Utilities.Voronoi.Voronoi.DiagramSettings
        {
            Randomness = 0,
            // Image Heightmap distributor.
            Distributor = new DistributorImage(_densityTexture, new DistributorImageSettings
            {
                DensityIntensity = 1.1,
                CenterCellSizeFactor = 1,
                EdgeCellSizeFactor = 7,
                AllowBlackGaps = false,
            }),
            BoundaryMode = Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.CulledCircular,
            Flags = Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.CellsPoints,
            ReferenceBoundary = new ReferenceBoundarySettings(_borderMap)
        };
        (Texture2D PixelMap, Texture2D? OverlayMap) diagram = _voronoi.GenerateDiagram(
            points: _points,
            //width: 1200,
            //height: 800,
            settings: diagramSettings
        );
        _cellMap = new CellMap(diagram.PixelMap, diagram.OverlayMap);

        Black(_points);

        //_cellMap.ChangeCellColor(0, Color.LightGoldenrodYellow);
        //_cellMap.ChangeCellColor(1, Color.Gold);
        //_cellMap.ForceUpdate();
        generated = true;
    }

    private void Black(int points)
    {
        Color color = Color.Black;
        //for (uint i = 0; i < points; i++)
        //    _cellMap.ChangeCellColor(i, color);
    }

    private void Regions(int points)
    {
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
            //_cellMap.ChangeCellColor(i, color);
        }
    }


    private void MimicLoadContent()
    {
        byte[] fontData = File.ReadAllBytes("ARIAL.ttf");
        var fontSystem = new FontSystem();
        fontSystem.AddFont(fontData);
        _font = fontSystem.GetFont(32);

        Texture2D heightMap = new EmbeddedContentManager(_game, typeof(Voronoi).Assembly)
            .LoadEmbeddedTexture(_game.GraphicsDevice, "GG_Map.png");
        _densityTexture = heightMap;

        Texture2D borderMap = new EmbeddedContentManager(_game, typeof(Voronoi).Assembly)
            .LoadEmbeddedTexture(_game.GraphicsDevice, "GG_Borders.png");
        _borderMap = borderMap;
    }

    #endregion

    #region DRAW & UPDATE

    private /*async*/ void MimicUpdate(GameTime gameTime)
    {
        if (!generated)
            return;

        MouseState mouse = Mouse.GetState();
        Point mousePosition = new(mouse.X, mouse.Y);

        /*_cellMap.ClearDemarcatedCells();
        if (_voronoi.TryGetCellAt(mousePosition, out VoronoiCell? cell))
        {
            _cellMap.DemarcateCell(cell.ID);
            _hoveredCell = cell;
        }
        else
        {
            _hoveredCell = null;
        }

        _cellMap.RebuildDemarcatedCellBorders(Color.Wheat, 1f);*/


        if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162
            // ReSharper disable once HeuristicUnreachableCode
            _game.Quit();
#pragma warning restore CS0162
    }

    private void MimicDraw(GameTime gameTime)
    {
        _cellMap.Draw(_spriteBatch);

        _spriteBatch.Begin();
        Vector2 position = Vector2.Zero;
        bool nullCell = _hoveredCell != null;
        _spriteBatch.DrawString(_font, nullCell
            ? $"RENDER: {_hoveredCell?.ID.ToString()}"
            : "null", position, Color.Wheat);
        position = new Vector2(0, 25);
        _spriteBatch.DrawString(_font, nullCell
            ? $"VERTICES: {_hoveredCell?.Vertices.Count.ToString()}"
            : "null", position, Color.Wheat);
        position = new Vector2(0, 50);
        _spriteBatch.DrawString(_font, nullCell
            ? $"SITE: {_hoveredCell?.Site.ToString()}"
            : "null", position, Color.Wheat);
        _spriteBatch.End();
    }

    #endregion
}