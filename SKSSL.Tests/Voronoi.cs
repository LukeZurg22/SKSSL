#nullable enable
using System;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

        _densityTexture = CreateCircularDensityMap(
            _game.GraphicsDevice,
            _game.GraphicsDevice.Viewport.Width,
            _game.GraphicsDevice.Viewport.Height
        );

        DistributorImage distributor = new(_densityTexture, 2);

        // EU5 has around 30k~. This can generate 100k and highlight cells somewhat smoothly. The catch is that this
        //  does not guarantee to be performance when extra data like province goods is hooked-up. That might require
        //  some kind of culling.
        /*await Task.Run(() =>*/
        _voronoi.GenerateDiagram(points: 10000,
            //width: 1200,
            //height: 800,
            randomness: 0,
            settlePoints: false,
            distributor: distributor,
            boundaryMode: Utilities.Voronoi.Voronoi.VoronoiBoundaryMode.Culled,
            flags: Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Cells /*|
                   Utilities.Voronoi.Voronoi.VoronoiRenderingFlags.Points*/);

        //for (int i = 0; i <= 800; i++) _voronoi.MarkCell(i);
        //
        //_voronoi.ColorMarkedCells(Color.BlanchedAlmond, Color.DarkGreen);
        generated = true;
    }

    [UsedImplicitly]
    public static Texture2D CreateCircularDensityMap(
        GraphicsDevice graphicsDevice,
        int width, int height,
        float radius = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var pixels = new Color[width * height];

        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;

        // Use the smaller dimension so the density remains circular.
        float maxRadius = MathF.Min(width, height) * 0.5f * radius;

        float inverseRadius = 1.0f / maxRadius;

        for (int y = 0; y < height; y++)
        {
            float dy = y - centerY;

            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float normalized = distance * inverseRadius;

                // 1 at center -> 0 at radius.
                float density = 1.0f - MathF.Min(normalized, 1.0f);

                // Smooth the falloff.
                density = density * density * (3.0f - 2.0f * density);
                byte value = (byte)(density * 255.0f);
                pixels[y * width + x] = new Color(value, value, value, (byte)255);
            }
        }

        Texture2D texture = new(graphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }
}