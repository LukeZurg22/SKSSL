#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FontStashSharp;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SKSSL.Assets;
using SKSSL.Tests.Classes;
using SKSSL.Utilities;
using SKSSL.Utilities.Voronoi;

// ReSharper disable UnusedMember.Local

namespace SKSSL.Tests;

[TestClass, UsedImplicitly, TestSubject(typeof(Utilities.Voronoi.Voronoi))]
public class Voronoi
{
    private const bool TOGGLE_EXECUTABLE_CLOSURE = false;
    private DiagramMap _diagramMap = null!;
    private bool generated = false;
    private UnitGame _game = null!;
    private Texture2D _densityTexture = null!;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFontBase _font = null!;
    private Texture2D _borderMap = null!;
    private readonly HashSet<uint> _selectedIds = [];

    [TestMethod, UsedImplicitly]
    public void TEST_VORONOI_GEN()
    {
        using var game = new UnitGame(() => { }, () => { }, MimicUpdate, MimicDraw);
        _game = game;

        // Mimic game object class instantiation.
        _spriteBatch = new SpriteBatch(_game.GraphicsDevice);

        //MeasureProfiler.StartCollectingData();
        MimicLoadContent(); // Mimic LoadContent call.
        MimicInitialize(); // Mimic Initialize call.

        //MeasureProfiler.SaveData();
        game.Run();
        Assert.IsTrue(generated);
    }

    #region INIT

    [TestMethod, UsedImplicitly]
    public void MimicInitialize()
    {
        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        const int siteCount = 1000;
        DiagramMap diagram = VoronoiDiagramMapPipeline.FromHeightmap(
            _game.GraphicsDevice,
            _densityTexture,
            1920, 1080,
            new VoronoiOptions
            {
                SiteCount = siteCount,
                OverlayFlags = DiagramOverlayOptions.Sites,
            } /*,
            new ReferenceBoundarySettings(_borderMap)*/);
        _diagramMap = diagram;
        _diagramMap.SetDiagramProjection(Matrix.Identity, Matrix.Identity, _game.GraphicsDevice.Viewport.Width,
            _game.GraphicsDevice.Viewport.Height);

        // Highlight a cell in translucent yellow.
        _diagramMap.SetCellHighlight(cellId: 42, color: new Color(12, 220, 0, 160));
        _diagramMap.SetHighlightOpacity(1f);

        // Flash every 250 milliseconds.
        _diagramMap.SetHighlightFlashing(
            enabled: true,
            interval: TimeSpan.FromMilliseconds(250),
            smooth: true
        );
        var selector = DiagramMap.CreateRadialWeightSelector(
            _diagramMap.GetCellPosition, center: _diagramMap.GetCentre(), radius: 250, falloffPower: 2);

        var cells = _diagramMap.SelectCellClumps(100, 5, 10, selector);
        foreach (var cellGroup in cells)
            _diagramMap.DemarcateCells(cellGroup, true);

        Regions(siteCount);

        //_cellMap.ChangeCellColor(0, Color.LightGoldenrodYellow);
        //_cellMap.ChangeCellColor(1, Color.Gold);
        //_cellMap.ForceUpdate();
        generated = true;
    }

    private void Black(int points)
    {
        Color color = Color.Black;
        for (uint i = 0; i < points; i++)
            _diagramMap.SetCellOverride(i, color);
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
            _diagramMap.SetCellOverride(i, color);
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
            .LoadEmbeddedTexture(_game.GraphicsDevice, "GG_Borders_1.png");
        _borderMap = borderMap;
    }

    #endregion

    #region DRAW & UPDATE

    private /*async*/ void MimicUpdate(GameTime gameTime)
    {
        if (!generated)
            return;

        MouseState mouse = Mouse.GetState();
        if (_diagramMap.TryGetCellAt(mouse.Position, out uint id))
        {
            System.Console.WriteLine($"Hovered cell ID: {id}");
            _diagramMap.ClearCellOverrides(_selectedIds);
            _selectedIds.Clear(); // Inefficient, but all I care about are results.
            _selectedIds.Add(id);
            _diagramMap.SetCellOverride(id, Color.Yellow);
        }

        _diagramMap.SetCellOverrides([1, 2, 3, 4, 5, 6, 7, 8], Color.Red);
        _diagramMap.SetCellOverrides([9, 10, 11, 12, 13, 14, 15, 16], Color.Green);
        _diagramMap.SetCellOverrides([0], Color.PaleTurquoise);

        _diagramMap.Update(gameTime);

        if (TOGGLE_EXECUTABLE_CLOSURE)
#pragma warning disable CS0162
            // ReSharper disable once HeuristicUnreachableCode
            _game.Quit();
#pragma warning restore CS0162
    }

    private void MimicDraw(GameTime gameTime)
    {
        _diagramMap.Draw(_spriteBatch);

        _spriteBatch.Begin();
        Vector2 position = Vector2.Zero;
        _spriteBatch.DrawString(_font, $"RENDER: {_selectedIds.First().ToString()}", position, Color.Black);
        position = new Vector2(0, 25);
        //_spriteBatch.DrawString(_font, nullCell
        //    ? $"VERTICES: {_hoveredCell?.Vertices.Count.ToString()}"
        //    : "null", position, Color.Wheat);
        _spriteBatch.End();
    }

    #endregion
}