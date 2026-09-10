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
/// 2. Call voronoi.LoadContent() in game.<br/>
/// 3. Call voronoi.Draw() in game.<br/>
/// 4. Call GenerateDiagram() in Update(), or wherever desired.
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
    private readonly SpriteBatch _spriteBatch;
    public const int DefaultPointCount = 2000;
    private Point[] _points = [];

    private readonly List<Edge> _triangulationEdges = []; // For rendering triangulation edges.
    private readonly List<Edge> _voronoiEdges = []; // For Rendering the "proper" edges of each cell.

    private readonly Dictionary<Point, VoronoiCell> _voronoiCells = [];
    private readonly Dictionary<Point, Color> _cellColors = [];

    private Texture2D _pixelMap = null!;
    private bool _isGenerated = false;
    private BasicEffect _effect;

    private readonly Random _random = new(32); // fixed seed → reproducible
    private readonly HashSet<int> _usedColors = []; // avoid exact RGB collisions

    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    // ReSharper disable once ConvertToConstant.Global
    // ReSharper disable MemberCanBePrivate.Global
    public ColorMode CellDrawMode;
    public Color UnifiedColor;
    public Color PointColor;

    public Voronoi(
        GraphicsDevice graphicsDevice,
        SpriteBatch? spriteBatch,
        ColorMode cellDrawMode = ColorMode.Unique,
        Color? unifiedColor = null,
        Color? pointColor = null)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch ?? new SpriteBatch(graphicsDevice);

        CellDrawMode = cellDrawMode;
        UnifiedColor = unifiedColor ?? Color.White;
        PointColor = pointColor ?? Color.Red;
    }

    /// Setup image data for writing. It begins as a 1x1 white pixel, but will be expanded later.
    public void LoadContent()
    {
        _pixelMap = new Texture2D(_graphicsDevice, 1, 1);
        _pixelMap.SetData([Color.White]);

        _width = _graphicsDevice.Viewport.Width;
        _height = _graphicsDevice.Viewport.Height;

        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            World = Matrix.Identity,
            View = Matrix.Identity,
            Projection = Matrix.CreateOrthographicOffCenter(
                0,
                _width,
                _height,
                0,
                0,
                1)
        };
    }

    /// <value>_graphicsDevice.Viewport.Width</value>
    private int _width;

    /// <value>_graphicsDevice.Viewport.Height</value>
    private int _height;

    // TODO: Allow the ability to generate / render a set of cells that follow explicitly-provided borders.

    #region Generation Methods

    /// <summary>
    /// Generates a set of voronoi cells and internally inserts them into the data of this <see cref="Voronoi"/>
    /// class object.
    /// </summary>
    /// <returns>Generated 2D image of pixel data generated from diagram.</returns>
    /// <remarks>
    /// This is the merged, more-efficient version of the old handling provided here.
    /// <code>
    /// // Generate points.
    /// _points = [.._delaunay.GeneratePoints(PointCount, width, height)];
    /// ...
    /// // Make the triangles.
    /// var triangulation = _delaunay.BowyerWatson(_points).ToList();
    /// ...
    /// // Create Cells.
    /// GenerateCells(triangulation);
    /// ...
    /// // Clear the edges used to make the triangles.
    /// _triangulationEdges.Clear();
    /// ...
    /// // Generate the voronoi edges.
    /// _voronoiEdges = [..GenerateEdgesFromDelaunay(triangulation)];
    /// </code>
    /// It also calls <see cref="GetTexture"/> to populate the pixel data.
    /// </remarks>
    /// <param name="pointCount"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="distribution"></param>
    /// <param name="randomness">
    /// Evenness of distribution on a scale of 0.00 -> 1.00; only works with the
    /// <see cref="DelaunayTriangulator.PointDistribution.RandomJitter"/> distribution.
    /// </param>
    /// <param name="customSamplingAlgorithm">
    /// Provided custom method with integer and an empty list of <see cref="Point"/>s as parameters.
    /// Must algorithmically decide the positioning of the point X and Y positions.
    /// </param>
    /// <param name="flags">Convenient Enum toggle of various parts of a Voronoi diagram.</param>
    /// <param name="thickness">Thickness of Edges, if they are rendered.</param>
    /// <param name="pointSize">Size of Points, if they are rendered.</param>
    public Texture2D GenerateDiagram(
        int pointCount = DefaultPointCount,
        int? width = null,
        int? height = null,
        DelaunayTriangulator.PointDistribution distribution = DelaunayTriangulator.PointDistribution.RandomSystem,
        double randomness = 0.8,
        Action<int, List<Point>>? customSamplingAlgorithm = null,
        VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cells,
        float thickness = 1f,
        float pointSize = 2f)
    {
        _isGenerated = false;
        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;

        //@formatter:off
        // Depending on the distribution type, and the custom sampling algorithm provided, there are multiple ways
        //  to generate a set of points.
        _points = distribution == DelaunayTriangulator.PointDistribution.Custom && customSamplingAlgorithm == null
            ? throw new NullReferenceException("Specified custom distribution without providing an algorithm!")
            : distribution == DelaunayTriangulator.PointDistribution.Custom
                ? [.._delaunay.GeneratePoints(pointCount, width.Value, height.Value, customSamplingAlgorithm!)]
                : [.._delaunay.GeneratePoints(pointCount, width.Value, height.Value, distribution, randomness)];
        //@formatter:on

        // Make the triangles.
        var triangulation = _delaunay.BowyerWatson(_points);

#if DEBUG
        ValidateNeighbors((List<Triangle>)triangulation);
#endif

        /*
         * Previously, each separate generate function was used in sequence. However the multiple-iteration
         * warning made me grow concerned over performance, so I merged the functions into one.
         */
        // Clear old data.
        _triangulationEdges.Clear();
        _voronoiCells.Clear();
        _cellColors.Clear();
        _voronoiEdges.Clear();
        _usedColors.Clear();

        // GENERATE CELLS
        foreach (Triangle triangle in triangulation)
        {
            // POINTS
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

            // VORONOI EDGES
            // Triangulation edges moved here from call above in order to prevent multiple-reiterations of the list.
            _triangulationEdges.Add(new Edge(triangle.Vertices[0], triangle.Vertices[1]));
            _triangulationEdges.Add(new Edge(triangle.Vertices[1], triangle.Vertices[2]));
            _triangulationEdges.Add(new Edge(triangle.Vertices[2], triangle.Vertices[0]));

            // Add edge to voronoi edges.
            foreach (Triangle? neighbor in triangle.Neighbors)
            {
                if (neighbor != null && triangle.Id < neighbor.Id)
                    _voronoiEdges.Add(new Edge(triangle.Circumcenter, neighbor.Circumcenter));
            }
        }

        // Order the vertices of every cell clockwise / counter-clockwise.
        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            cell.Vertices = cell.Vertices
                .Distinct() // remove duplicates
                .OrderBy(v => Math.Atan2(v.Y - cell.Site.Y, v.X - cell.Site.X))
                .ToList();

            // Simple deterministic color from the site’s position
            _cellColors[cell.Site] = GetCellColor(cell);
        }

        _isGenerated = true;
        // Update the existing internal pixel map with visual changes.
        GetTexture(flags, thickness, pointSize);
        return _pixelMap;
    }

    /// Toggle handling for <see cref="GetTexture"/> to check if something was generated.
    private VoronoiRenderingFlags _previousFlags;

    private float _previousThickness;
    private float _previousPointSize;
    private bool _textureValid;

    /// <summary>
    /// Gets the voronoi object's rasterized texture, or creates one.
    /// </summary>
    /// <returns>Texture2D reference of the Voronoi image result.</returns>
    /// <remarks>
    /// Make sure that the Diagram data is generated first through <see cref="GenerateDiagram"/>.
    /// When this function is called, it also assigns the internal pixel data to the new image.
    /// </remarks>
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMethodReturnValue.Global
    public Texture2D GetTexture(VoronoiRenderingFlags flags, float thickness, float pointSize)
    {
        if (!_isGenerated)
            throw new InvalidOperationException("Attempted to get Voronoi texture before generating a diagram.");

        // If current flags are present whilst previous flags exist and aren't none, it means that a map
        //  was generated.
        if (_textureValid &&
            flags == _previousFlags &&
            Math.Abs(thickness - _previousThickness) < 0.01f &&
            Math.Abs(pointSize - _previousPointSize) < 0.01f)
            return _pixelMap;

        var output = new RenderTarget2D(
            _graphicsDevice,
            _width,
            _height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None
        );

        var previousTargets = _graphicsDevice.GetRenderTargets();

        _graphicsDevice.SetRenderTarget(output);

        // Prevent graphics device from going haywire and pointing at off-screen targets.
        try
        {
            _graphicsDevice.Clear(Color.Transparent);

            // Draw filled Voronoi cells first.
            if (flags.HasFlag(VoronoiRenderingFlags.Cells))
                foreach (VoronoiCell cell in _voronoiCells.Values)
                    DrawPolygon(cell.Vertices, _cellColors[cell.Site]);

            // Drawing triangles.
            if (flags.HasFlag(VoronoiRenderingFlags.Triangles))
                DrawEdges(_triangulationEdges, Color.Transparent, thickness);

            // Drawing edges.
            if (flags.HasFlag(VoronoiRenderingFlags.Edges))
                DrawEdges(_voronoiEdges, Color.DarkGray, thickness);

            // Drawing the dots.
            if (flags.HasFlag(VoronoiRenderingFlags.Points))
                DrawPoints(pointSize);
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
        }

        Texture2D oldTexture = _pixelMap;
        _pixelMap = output;
        _previousFlags = flags;
        _previousThickness = thickness;
        _previousPointSize = pointSize;
        _textureValid = true;
        if (oldTexture is RenderTarget2D oldTarget)
            oldTarget.Dispose();

        return output;
    }

    #endregion

    #region Obsolete Generation Functions

    [Obsolete($"If generating a diagram, use {nameof(GenerateDiagram)}")]
    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnusedMember.Global
    public void GenerateCells(IEnumerable<Triangle> triangulation)
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

    #endregion

    #region Spritebatch-Less Drawing

    private static readonly short[] QuadIndices = [0, 1, 2, 0, 2, 3];

    private void DrawEdges(IEnumerable<Edge> edges, Color color, float thickness)
    {
        foreach (Edge edge in edges)
            DrawLine(edge.Point1, edge.Point2, color, thickness);
    }

    private void DrawLine(Point p1, Point p2, Color color, float thickness)
    {
        Vector2 start = new((float)p1.X, (float)p1.Y);
        Vector2 end = new((float)p2.X, (float)p2.Y);
        Vector2 direction = end - start;

        float length = direction.Length();
        if (length <= 0f)
            return;

        direction /= length;

        Vector2 perpendicular = new(-direction.Y, direction.X);
        Vector2 offset = perpendicular * (thickness * 0.5f);

        var vertices = new[]
        {
            new VertexPositionColor(new Vector3(start + offset, 0f), color),
            new VertexPositionColor(new Vector3(start - offset, 0f), color),
            new VertexPositionColor(new Vector3(end - offset, 0f), color),
            new VertexPositionColor(new Vector3(end + offset, 0f), color)
        };

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                vertices,
                0,
                4,
                QuadIndices,
                0,
                2);
        }
    }

    private void DrawPoints(float size = 2f)
    {
        float halfSize = size * 0.5f;

        foreach (Point point in _points)
        {
            float x = (float)point.X;
            float y = (float)point.Y;

            var vertices = new[]
            {
                new VertexPositionColor(new Vector3(x - halfSize, y - halfSize, 0f), PointColor),
                new VertexPositionColor(new Vector3(x + halfSize, y - halfSize, 0f), PointColor),
                new VertexPositionColor(new Vector3(x + halfSize, y + halfSize, 0f), PointColor),
                new VertexPositionColor(new Vector3(x - halfSize, y + halfSize, 0f), PointColor)
            };

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    vertices,
                    0,
                    4,
                    QuadIndices,
                    0,
                    2);
            }
        }
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

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                verts,
                0,
                verts.Length,
                indices,
                0,
                triangleCount);
        }
    }


    // ReSharper disable once UnusedMember.Local
    [Obsolete]
    private void DrawTriangle(Triangle triangle, Color color)
    {
        var vertices = new[]
        {
            //@formatter:off
            new VertexPositionColor(new Vector3((float)triangle.Vertices[0].X, (float)triangle.Vertices[0].Y, 0), color),
            new VertexPositionColor(new Vector3((float)triangle.Vertices[1].X, (float)triangle.Vertices[1].Y, 0), color),
            new VertexPositionColor(new Vector3((float)triangle.Vertices[2].X, (float)triangle.Vertices[2].Y, 0), color),
            //@formatter:on
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

    #endregion

    #region Spritebatch-focused drawing

    /// <summary>
    /// Draw a flat 
    /// </summary>
    /// <param name="spriteBatch"></param>
    /// <param name="thickness"></param>
    /// <param name="flags"></param>
    // ReSharper disable once UnusedMember.Global
    public void Draw(
        SpriteBatch spriteBatch,
        float thickness = 1f,
        VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cells)
    {
        spriteBatch.Begin();

        // Draw filled Voronoi cells first
        if ((flags & VoronoiRenderingFlags.Cells) != 0)
            foreach (VoronoiCell cell in _voronoiCells.Values)
                DrawPolygon(cell.Vertices, _cellColors[cell.Site]);

        if ((flags & VoronoiRenderingFlags.Triangles) != 0)
            DrawEdges(_triangulationEdges, Color.Transparent, thickness, spriteBatch);

        if ((flags & VoronoiRenderingFlags.Edges) != 0)
            DrawEdges(_voronoiEdges, Color.DarkGray, thickness, spriteBatch);

        if ((flags & VoronoiRenderingFlags.Points) != 0)
            DrawPoints(spriteBatch);
        spriteBatch.End();
    }

    // ReSharper disable once UnusedMember.Global
    public void Draw(float thickness = 1f, VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cells)
        => Draw(_spriteBatch, thickness, flags);

    private void DrawPoints(SpriteBatch spriteBatch)
    {
        foreach (Point point in _points)
            spriteBatch.Draw(_pixelMap, new Rectangle((int)point.X, (int)point.Y, 2, 2), PointColor);
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
            _pixelMap,
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

    #region Helper(s)

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

    #endregion

    private static void ValidateNeighbors(List<Triangle> triangles)
    {
        foreach (Triangle t in triangles)
        {
            Validate(t, 0, t.Neighbor0);
            Validate(t, 1, t.Neighbor1);
            Validate(t, 2, t.Neighbor2);
        }

        return;

        static void Validate(Triangle t, int edge, Triangle? n)
        {
            if (n == null)
                return;

            Point a;
            Point b;

            switch (edge)
            {
                case 0:
                    a = t.Vertices[0];
                    b = t.Vertices[1];
                    break;
                case 1:
                    a = t.Vertices[1];
                    b = t.Vertices[2];
                    break;
                default:
                    a = t.Vertices[2];
                    b = t.Vertices[0];
                    break;
            }

            int reciprocal = n.IndexOfEdge(a, b);

            if (reciprocal < 0)
                throw new InvalidOperationException(
                    "Neighbor does not share the expected edge.");

            Triangle? reverse = reciprocal switch
            {
                0 => n.Neighbor0,
                1 => n.Neighbor1,
                _ => n.Neighbor2
            };

            if (!ReferenceEquals(reverse, t))
                throw new InvalidOperationException(
                    "Neighbor relationship is not reciprocal.");
        }
    }

    public enum ColorMode : byte
    {
        Unified,
        Unique,
        Deterministic_Random,
        Deterministic_Lines,
        Random,
    }

    [Flags]
    // ReSharper disable UnusedMember.Global
    public enum VoronoiRenderingFlags
    {
        //@formatter:off
        /// Used as a "render nothing" and a null toggle.
        None      = 0,
        /// Draw dots.
        Points     = 1 << 0,
        /// Draw "proper" voronoi cells.
        Cells      = 1 << 1,
        /// Draw edges between cells.
        Edges     = 1 << 2,
        /// Draw the triangles that make up each cell.
        Triangles = 1 << 3,
        
        CellsAndEdges = Cells | Edges,
        All = Points | Cells | Edges | Triangles
        //@formatter:on
    }
}