using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/*
 * CREDIT:

 */

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// Voronoi implementation to create an image of organic-like cells using the <see cref="DelaunayTriangulator"/>
/// algorithm.
/// </summary>
/// <code>
/// private readonly Voronoi _voronoi = null!;
/// ...
/// _voronoi = new Voronoi(GraphicsDevice);
/// </code>
/// <remarks>
/// This implementation assumes the calls are being handled through Monogame in the following order:<br/>
/// 1. Instantiate the <see cref="Voronoi"/> class.<br/>
/// 2. Insert LoadContent() into the game class.<br/>
/// 3. Insert Draw() into the game class.<br/>
/// 4. Call GenerateDiagram() somewhere in Update(), or wherever desired.
/// </remarks>
/// <references>
/// 1. https://www.redblobgames.com/x/2022-voronoi-maps-tutorial/<br/>
/// 2. https://en.wikipedia.org/wiki/Delaunay_triangulation<br/>
/// 3. https://louis-dr.github.io/voronoimap.html<br/>
/// 4. https://mapbox.github.io/delaunator/
/// </references>
public class Voronoi
{
    private readonly DelaunayTriangulator _delaunay = new();
    public readonly GraphicsDevice _graphicsDevice;
    public const int PointCount = 2000;
    private List<Point> _points = [];

    private readonly List<Edge> _triangulationEdges = []; // For rendering triangulation edges.
    private List<Edge> _voronoiEdges = []; // For Rendering the "proper" edges of each cell.

    private readonly Dictionary<Point, VoronoiCell> _voronoiCells = [];
    private readonly Dictionary<Point, Color> _cellColors = [];

    private Texture2D _pixel = null!;
    private BasicEffect _effect;

    private readonly Random _random = new(32); // fixed seed → reproducible
    private readonly HashSet<int> _usedColors = []; // avoid exact RGB collisions

    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    // ReSharper disable once ConvertToConstant.Global
    // ReSharper disable MemberCanBePrivate.Global
    public ColorMode CellDrawMode = ColorMode.Unique;
    public Color UnifiedColor = Color.White;
    public Color PointColor = Color.Red;

    /// <returns>Resulting Voronoi image Diagram of Voronoi cells.</returns>
    public Texture2D GetImage() => _pixel;

    public Voronoi(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

    private HashSet<Edge> GenerateEdgesFromDelaunay(IEnumerable<Triangle> triangulation)
    {
        var voronoiEdges = new HashSet<Edge>();
        foreach (Triangle triangle in triangulation)
        {
            // Triangulation edges moved here from call above in order to prevent multiple-reiterations of the list.
            _triangulationEdges.Add(new Edge(triangle.Vertices[0], triangle.Vertices[1]));
            _triangulationEdges.Add(new Edge(triangle.Vertices[1], triangle.Vertices[2]));
            _triangulationEdges.Add(new Edge(triangle.Vertices[2], triangle.Vertices[0]));

            // Add edge to voronoi edges.
            foreach (Triangle neighbor in triangle.TrianglesWithSharedEdge)
                voronoiEdges.Add(new Edge(triangle.Circumcenter, neighbor.Circumcenter));
        }

        return voronoiEdges;
    }

    /// Setup image data for writing. It begins as a 1x1 white pixel, but will be expanded later.
    public void LoadContent()
    {
        _pixel = new Texture2D(_graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            World = Matrix.Identity,
            View = Matrix.Identity,
            Projection = Matrix.CreateOrthographicOffCenter(
                0,
                _graphicsDevice.Viewport.Width,
                _graphicsDevice.Viewport.Height,
                0,
                0,
                1)
        };
    }

    public void GenerateDiagram()
    {
        int width = _graphicsDevice.Viewport.Width;
        int height = _graphicsDevice.Viewport.Height;

        // Generate points.
        _points = [.._delaunay.GeneratePoints(PointCount, width, height)];

        // Make the triangles.
        var triangulation = _delaunay.BowyerWatson(_points).ToList();

        // Create Cells.
        GenerateCells(triangulation);

        _triangulationEdges.Clear(); // Clear the edges used to make the triangles.
        _voronoiEdges = [..GenerateEdgesFromDelaunay(triangulation)]; // Generate the voronoi edges.
    }

    private void GenerateCells(List<Triangle> triangulation)
    {
        _voronoiCells.Clear();
        foreach (Triangle triangle in triangulation)
        foreach (Point site in triangle.Vertices) // Every vertex of a Delaunay triangle is a Voronoi site
        {
            if (!_voronoiCells.TryGetValue(site, out VoronoiCell? cell))
            {
                cell = new VoronoiCell { Site = site, Vertices = [] };
                _voronoiCells[site] = cell;
            }

            // The circumcenter becomes a vertex of that site’s cell.
            cell.Vertices.Add(triangle.Circumcenter);
        }

        // Order the vertices of every cell clockwise / counter-clockwise.
        _cellColors.Clear();
        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            cell.Vertices = cell.Vertices
                .Distinct() // remove duplicates
                .OrderBy(v => Math.Atan2(v.Y - cell.Site.Y, v.X - cell.Site.X))
                .ToList();

            // Simple deterministic color from the site’s position
            _cellColors[cell.Site] = GetCellColor(cell);
        }
    }

    public Color GetCellColor(VoronoiCell cell)
    {
        Color color;
        uint hash;
        byte r, g, b;
        switch (CellDrawMode)
        {
            case ColorMode.Deterministic_Lines:
                hash = (uint)cell.Site.X + (uint)cell.Site.Y;
                hash ^= hash >> 16;
                r = (byte)hash;
                hash ^= hash >> 15;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                break;
            case ColorMode.Deterministic_Random: // Throw together a lazy hash based on cell Site vertex.
                hash = (uint)cell.Site.X ^ (uint)cell.Site.Y;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                r = (byte)hash;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                break;
            case ColorMode.Random: // Truly random 0 -> 255
                color = new Color(
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    255);
                break;
            case ColorMode.Unique: // Pure random RGB – three integer ops, no floats
                int rgb;
                do rgb = _random.Next(0x1000000); // 0 … 16 777 215
                while (!_usedColors.Add(rgb));
                color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);
                break;
            case ColorMode.Unified:
            default:
                color = UnifiedColor;
                break;
        }

        return color;
    }

    #region Draw Methods

    public void Draw(
        SpriteBatch spriteBatch,
        float thickness = 1f,
        VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cell)
    {
        spriteBatch.Begin();

        // Draw filled Voronoi cells first
        if (flags.HasFlag(VoronoiRenderingFlags.Cell))
            foreach (VoronoiCell cell in _voronoiCells.Values)
                DrawPolygon(cell.Vertices, _cellColors[cell.Site]);

        if (flags.HasFlag(VoronoiRenderingFlags.Triangles))
            DrawEdges(_triangulationEdges, Color.Transparent, thickness, spriteBatch);

        if (flags.HasFlag(VoronoiRenderingFlags.Edges))
            DrawEdges(_voronoiEdges, Color.DarkGray, thickness, spriteBatch);

        if (flags.HasFlag(VoronoiRenderingFlags.Point))
            DrawPoints(spriteBatch);
        spriteBatch.End();
    }

    private void DrawPolygon(List<Point> vertices, Color color)
    {
        if (vertices.Count < 3) return;

        // Convert to VertexPositionColor once
        var verts = new VertexPositionColor[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            verts[i] = new VertexPositionColor(
                new Vector3((float)vertices[i].X, (float)vertices[i].Y, 0), color);
        }

        // Triangle-fan indices: 0-1-2, 0-2-3, 0-3-4, …
        int triangleCount = vertices.Count - 2;
        var indices = new short[triangleCount * 3];
        for (int i = 0; i < triangleCount; i++)
        {
            indices[i * 3 + 0] = 0;
            indices[i * 3 + 1] = (short)(i + 1);
            indices[i * 3 + 2] = (short)(i + 2);
        }

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                verts, 0, verts.Length,
                indices, 0, triangleCount);
        }
    }

    // ReSharper disable once UnusedMember.Local
    [Obsolete]
    private void DrawTriangle(Triangle triangle, Color color)
    {
        var vertices = new[]
        {
            new VertexPositionColor(
                new Vector3((float)triangle.Vertices[0].X, (float)triangle.Vertices[0].Y, 0),
                color),

            new VertexPositionColor(
                new Vector3((float)triangle.Vertices[1].X, (float)triangle.Vertices[1].Y, 0),
                color),

            new VertexPositionColor(
                new Vector3((float)triangle.Vertices[2].X, (float)triangle.Vertices[2].Y, 0),
                color),
        };

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                vertices,
                0,
                1);
        }
    }

    private void DrawPoints(SpriteBatch spriteBatch)
    {
        foreach (Point point in _points)
            spriteBatch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y, 2, 2), PointColor);
    }

    private void DrawEdges(IEnumerable<Edge> edges, Color color, float thickness, SpriteBatch spriteBatch)
    {
        foreach (Edge edge in edges)
            DrawLine(edge.Point1, edge.Point2, color, thickness, spriteBatch);
    }

    private void DrawLine(Point p1, Point p2, Color color, float thickness, SpriteBatch spriteBatch)
    {
        var start = new Vector2((float)p1.X, (float)p1.Y);
        var end = new Vector2((float)p2.X, (float)p2.Y);

        Vector2 edge = end - start;
        float angle = MathF.Atan2(edge.Y, edge.X);

        spriteBatch.Draw(
            _pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(edge.Length(), thickness),
            SpriteEffects.None,
            0f);
    }

    #endregion

    public enum ColorMode : byte
    {
        Unified,
        Unique,
        Deterministic_Random,
        Deterministic_Lines,
        Random,
    }

    /// ReSharper disable UnusedMember.Global
    [Flags]
    public enum VoronoiRenderingFlags : byte
    {
        None = 0,
        Point = 1,
        Edges = 2,
        Outline = Point | Edges,
        Triangles = 4,
        Cell = 5,
        CellAndEdges = Cell | Edges,
        All = (Point | Edges | Triangles | Cell | CellAndEdges) << 1
    }
}