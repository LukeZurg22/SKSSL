using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    private const int DefaultPointCount = 2000;
    private readonly DelaunayTriangulator _delaunay = new();
    public readonly GraphicsDevice _graphicsDevice;
    private Point[] _points = [];

    /// <value>_graphicsDevice.Viewport.Width</value>
    private int _width;

    /// <value>_graphicsDevice.Viewport.Height</value>
    private int _height;

    // Data Storage
    private readonly List<Edge> _voronoiEdges = []; // For Rendering the "proper" edges of each cell.
    private readonly Dictionary<Point, VoronoiCell> _voronoiCells = [];
    private readonly List<CellVoronoiEdge> _cellVoronoiEdges = [];

    // Coloring and Visualization
    private readonly Dictionary<Point, Color> _cellRawColors = []; // Raw "provincial" colors of cells.
    private readonly Dictionary<Point, Color> _cellOverrideColors = []; // Override colors for cells.
    private readonly HashSet<int> _usedColors = []; // avoid exact RGB collisions
    private readonly Random _random = new(32); // fixed seed → reproducible

    // Data Caching
    private VertexPositionColor[] _cellBatchVertices = [];
    private int _cellBatchPrimitiveCount;
    private VertexPositionColor[] _edgeBatchVertices = [];
    private int _edgeBatchPrimitiveCount;
    private VertexPositionColor[] _pointBatchVertices = [];
    private int _pointBatchPrimitiveCount;
    private VertexPositionColor[] _triangleBatchVertices = [];
    private int _triangleBatchPrimitiveCount;
    private VertexPositionColor[] _highlightBatchVertices = [];
    private int _highlightBatchPrimitiveCount;

    // Rendering Basics
    private Texture2D _pixelMap = null!;
    private BasicEffect _effect;

    // Rendering Options
    private VoronoiBoundaryMode _boundaryMode = VoronoiBoundaryMode.Culled;
    private readonly ColorMode CellDrawMode;
    private readonly Color _pointColor;
    private readonly Color _edgeColor;

    // Cache Flags
    private VoronoiRenderingFlags _previousFlags;
    private VoronoiBoundaryMode _previousBoundaryMode;
    private float _previousThickness;
    private float _previousPointSize;
    private bool _textureValid;
    private bool _isGenerated = false;

    #region Construction & Mono Code

    public Voronoi(
        GraphicsDevice graphicsDevice,
        ColorMode cellDrawMode = ColorMode.Unique,
        Color? unifiedColor = null,
        Color? pointColor = null)
    {
        _graphicsDevice = graphicsDevice;
        CellDrawMode = cellDrawMode;
        _edgeColor = unifiedColor ?? Color.Gray;
        _pointColor = pointColor ?? Color.Red;
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
        };
        SetDiagramProjection();
    }

    private void SetDiagramProjection(Matrix? world = null, Matrix? view = null)
    {
        world ??= Matrix.Identity;
        view ??= Matrix.Identity;
        _effect.World = world.Value;
        _effect.View = view.Value;
        _effect.Projection =
            Matrix.CreateOrthographicOffCenter(
                0f,
                _width,
                _height,
                0f,
                0f,
                1f);
    }

    private void SetScreenProjection()
    {
        Viewport viewport = _graphicsDevice.Viewport;

        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection =
            Matrix.CreateOrthographicOffCenter(
                0f,
                viewport.Width,
                viewport.Height,
                0f,
                0f,
                1f);
    }

    #endregion

    // TODO: Allow the ability to generate / render a set of cells that follow explicitly-provided borders.
    //  a. Feed image
    //   1. determine functional edges. color-coding may be needed.
    //  b. Generate random series of larger polygons. If using voronoi, the edges will be useful.

    #region Diagram Generation

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
    /// <param name="boundaryMode"></param>
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
        VoronoiBoundaryMode boundaryMode = VoronoiBoundaryMode.Culled,
        VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cells,
        float thickness = 1f,
        float pointSize = 2f)
    {
        _isGenerated = false;
        _textureValid = false;
        _boundaryMode = boundaryMode;

        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;
        SetDiagramProjection();

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
        _voronoiCells.Clear();
        _cellRawColors.Clear();
        _voronoiEdges.Clear();
        _usedColors.Clear();
        _cellVoronoiEdges.Clear();

        _voronoiEdges.Capacity = Math.Max(_voronoiEdges.Capacity, pointCount * 3);

        _voronoiCells.EnsureCapacity(_points.Length);
        _cellRawColors.EnsureCapacity(_points.Length);

        // ReSharper disable once PossibleMultipleEnumeration
        foreach (Triangle triangle in triangulation)
        {
            // Build cells.
            foreach (Point site in triangle.Vertices)
            {
                if (!_voronoiCells.TryGetValue(site, out VoronoiCell? cell))
                {
                    cell = new VoronoiCell(site, []);
                    _voronoiCells.Add(site, cell);
                }

                cell.Vertices.Add(triangle.Circumcenter);
            }

            #region Mark Boundaries

            // Mark boundary cells.
            if (triangle.Neighbor0 == null)
            {
                _voronoiCells[triangle.Vertices[0]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[1]].IsBoundary = true;
            }

            if (triangle.Neighbor1 == null)
            {
                _voronoiCells[triangle.Vertices[1]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[2]].IsBoundary = true;
            }

            // ReSharper disable once InvertIf
            if (triangle.Neighbor2 == null)
            {
                _voronoiCells[triangle.Vertices[2]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[0]].IsBoundary = true;
            }

            #endregion
        }

        // PROCESS CELLS
        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            cell.Vertices = cell.Vertices
                .Distinct()
                .ToList();

            cell.Vertices.Sort((a, b) =>
            {
                double aa = Math.Atan2(a.Y - cell.Site.Y, a.X - cell.Site.X);
                double ab = Math.Atan2(b.Y - cell.Site.Y, b.X - cell.Site.X);
                return aa.CompareTo(ab);
            });

            bool touchesOutside =
                cell.Vertices.Any(v =>
                    v.X < 0 ||
                    v.X > _width ||
                    v.Y < 0 ||
                    v.Y > _height);

            if (touchesOutside)
            {
                if (boundaryMode == VoronoiBoundaryMode.Culled)
                {
                    // This is an unbounded boundary cell.
                    // Do not render it.
                    cell.Vertices.Clear();
                }
                else
                {
                    // Flatten the cell against the diagram boundary.
                    cell.Vertices = ClipPolygonToBounds(cell.Vertices, _width, _height);
                }
            }

            _cellRawColors[cell.Site] = GetCellColor(cell);
        }

        // Every IsBoundary value is now final. It's important that this be here and not in the initial loop
        // or else it will generate edges outside of the graph.
        // ReSharper disable once PossibleMultipleEnumeration
        foreach (Triangle triangle in triangulation)
            AddVoronoiEdges(triangle, boundaryMode);

        if ((flags & VoronoiRenderingFlags.Points) != 0)
            BuildPointBatch(pointSize);

        if ((flags & VoronoiRenderingFlags.Edges) != 0)
            BuildEdgeBatch(thickness);

        // ReSharper disable once PossibleMultipleEnumeration ; False positive.
        if ((flags & VoronoiRenderingFlags.Triangles) != 0)
            BuildTriangleBatch(triangulation);

        if ((flags & VoronoiRenderingFlags.Cells) != 0)
            BuildCellBatch();

        _isGenerated = true;
        // Update the existing internal pixel map with visual changes.
        GetTexture(boundaryMode, flags, thickness, pointSize);
        return _pixelMap;
    }


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
    public Texture2D GetTexture(
        VoronoiBoundaryMode boundaryMode,
        VoronoiRenderingFlags flags,
        float thickness,
        float pointSize)
    {
        if (!_isGenerated)
            throw new InvalidOperationException("Attempted to get Voronoi texture before generating a diagram.");

        // If current flags are present whilst previous flags exist and aren't none, it means that a map
        //  was generated.
        if (_textureValid &&
            flags == _previousFlags &&
            boundaryMode == _previousBoundaryMode &&
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
            SetDiagramProjection();
            _graphicsDevice.Clear(Color.Transparent);

            if (flags.HasFlag(VoronoiRenderingFlags.Cells))
                DrawCellBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Triangles))
                DrawTriangleBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Edges))
                DrawEdgeBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Points))
                DrawPointBatch();
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
        }

        Texture2D oldTexture = _pixelMap;
        _pixelMap = output;

        _previousBoundaryMode = boundaryMode;
        _previousThickness = thickness;
        _previousPointSize = pointSize;
        _previousFlags = flags;

        _textureValid = true;
        if (oldTexture is RenderTarget2D oldTarget)
            oldTarget.Dispose();

        return output;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!_isGenerated)
            return;

        spriteBatch.Begin();
        spriteBatch.Draw(_pixelMap, Vector2.Zero, Color.White);
        spriteBatch.End();

        DrawHighlightedCells();
    }

    #endregion

    #region Utility

    // ReSharper disable once UnusedMethodReturnValue.Global
    public bool TryGetCellAt(System.Drawing.Point position, out VoronoiCell? cell)
    {
        cell = null;

        if (_voronoiCells.Count == 0)
            return false;

        long bestDistance = long.MaxValue;
        foreach (VoronoiCell candidate in _voronoiCells.Values)
        {
            // Culled boundary cells don't visually exist.
            if (_boundaryMode == VoronoiBoundaryMode.Culled && candidate.IsBoundary)
                continue;

            long dx = (long)(candidate.Site.X - position.X);
            long dy = (long)(candidate.Site.Y - position.Y);
            long distance = dx * dx + dy * dy;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            cell = candidate;
        }

        return cell != null;
    }

    public void SetHighlightedCells(
        IEnumerable<VoronoiCell> cells,
        Color color,
        float thickness = 1f)
    {
        var highlightedSites = new HashSet<Point>();

        foreach (VoronoiCell cell in cells)
        {
            if (ShouldCullBoundarySite(cell.Site))
                continue;

            highlightedSites.Add(cell.Site);
        }

        var vertices = new List<VertexPositionColor>(_cellVoronoiEdges.Count * 6);
        float halfThickness = thickness * 0.5f;
        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            bool aHighlighted = highlightedSites.Contains(edge.SiteA);
            bool bHighlighted = edge.SiteB.HasValue && highlightedSites.Contains(edge.SiteB.Value);

            // We only want the outside boundary of the highlighted region.
            if (aHighlighted == bHighlighted)
                continue;

            Vector2 start = new((float)edge.Point1.X, (float)edge.Point1.Y);
            Vector2 end = new((float)edge.Point2.X, (float)edge.Point2.Y);
            Vector2 direction = end - start;

            float lengthSquared = direction.LengthSquared();
            if (lengthSquared <= 0.000001f)
                continue;

            direction *= 1f / MathF.Sqrt(lengthSquared);

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * halfThickness;

            Vector3 a = new(start + perpendicular, 0f);
            Vector3 b = new(start - perpendicular, 0f);
            Vector3 c = new(end - perpendicular, 0f);
            Vector3 d = new(end + perpendicular, 0f);

            vertices.Add(new VertexPositionColor(a, color));
            vertices.Add(new VertexPositionColor(b, color));
            vertices.Add(new VertexPositionColor(c, color));
            vertices.Add(new VertexPositionColor(a, color));
            vertices.Add(new VertexPositionColor(c, color));
            vertices.Add(new VertexPositionColor(d, color));
        }

        _highlightBatchVertices = vertices.ToArray();
        _highlightBatchPrimitiveCount = _highlightBatchVertices.Length / 3;
    }

    public void DrawHighlightedCells()
    {
        if (_highlightBatchPrimitiveCount == 0)
            return;

        SetScreenProjection();

        BlendState previousBlend = _graphicsDevice.BlendState;
        RasterizerState previousRasterizer = _graphicsDevice.RasterizerState;

        try
        {
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _highlightBatchVertices,
                    0,
                    _highlightBatchPrimitiveCount);
            }
        }
        finally
        {
            _graphicsDevice.BlendState = previousBlend;
            _graphicsDevice.RasterizerState = previousRasterizer;
        }
    }

    #endregion

    #region Helper(s)

    /// <summary>
    /// Marks whether a cell at a given site should be culled.
    /// </summary>
    /// <param name="site">Point at which a cell is expected to be.</param>
    /// <returns>
    /// True if the culling mode is <see cref="VoronoiBoundaryMode.Culled"/>, the point belongs to a valid cell,
    /// and that cell is a boundary cell or simply has no vertices. Otherwise... it returns false.
    /// </returns>
    private bool ShouldCullBoundarySite(Point site) =>
        _boundaryMode == VoronoiBoundaryMode.Culled &&
        _voronoiCells.TryGetValue(site, out VoronoiCell? cell) &&
        (cell.IsBoundary || cell.Vertices.Count == 0);

    private bool ClipLineToBounds(Point p1, Point p2, out Point clipped1, out Point clipped2)
    {
        float x1 = (float)p1.X;
        float y1 = (float)p1.Y;
        float x2 = (float)p2.X;
        float y2 = (float)p2.Y;

        float dx = x2 - x1;
        float dy = y2 - y1;

        float t0 = 0f;
        float t1 = 1f;

        // Left: x >= 0
        if (!Clip(-dx, x1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Right: x <= width
        if (!Clip(dx, _width - x1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Top: y >= 0
        if (!Clip(-dy, y1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Bottom: y <= height
        if (!Clip(dy, _height - y1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        clipped1 = new Point(
            (int)Math.Round(x1 + dx * t0),
            (int)Math.Round(y1 + dy * t0));

        clipped2 = new Point(
            (int)Math.Round(x1 + dx * t1),
            (int)Math.Round(y1 + dy * t1));

        return true;

        bool Clip(float p, float q)
        {
            if (Math.Abs(p) < 0.000001f)
                return q >= 0f;

            float r = q / p;

            if (p < 0f)
            {
                if (r > t1)
                    return false;

                if (r > t0)
                    t0 = r;
            }
            else
            {
                if (r < t0)
                    return false;

                if (r < t1)
                    t1 = r;
            }

            return true;
        }
    }

    private void AddVoronoiEdges(Triangle triangle, VoronoiBoundaryMode boundaryMode)
    {
        // Edge 0: vertices 0 -> 1
        AddVoronoiEdge(
            triangle,
            triangle.Neighbor0,
            triangle.Vertices[0],
            triangle.Vertices[1],
            boundaryMode);

        // Edge 1: vertices 1 -> 2
        AddVoronoiEdge(
            triangle,
            triangle.Neighbor1,
            triangle.Vertices[1],
            triangle.Vertices[2],
            boundaryMode);

        // Edge 2: vertices 2 -> 0
        AddVoronoiEdge(
            triangle,
            triangle.Neighbor2,
            triangle.Vertices[2],
            triangle.Vertices[0],
            boundaryMode);
    }

    private void AddVoronoiEdge(
        Triangle triangle,
        Triangle? neighbor,
        Point a,
        Point b,
        VoronoiBoundaryMode boundaryMode)
    {
        if (neighbor != null)
        {
            if (triangle.Id >= neighbor.Id)
                return;

            // In Culled mode, don't render an edge belonging to a boundary cell that is being culled.
            if (boundaryMode == VoronoiBoundaryMode.Culled &&
                (ShouldCullBoundarySite(a) || ShouldCullBoundarySite(b)))
                return;

            Point p1 = triangle.Circumcenter;
            Point p2 = neighbor.Circumcenter;

            if (!ClipLineToBounds(p1, p2, out Point clipped1, out Point clipped2))
                return;

            _voronoiEdges.Add(new Edge(clipped1, clipped2));
            _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, clipped1, clipped2));

            return;
        }

        // No neighboring triangle means this is a convex-hull edge,
        // so its Voronoi edge extends to infinity.
        if (boundaryMode == VoronoiBoundaryMode.Culled)
            return;

        if (TryGetBoundaryVoronoiEdge(triangle, a, b, out Point start, out Point end))
            _voronoiEdges.Add(new Edge(start, end));
    }

    private bool TryGetBoundaryVoronoiEdge(
        Triangle triangle,
        Point a,
        Point b,
        out Point start,
        out Point end)
    {
        Vector2 origin = new((float)triangle.Circumcenter.X, (float)triangle.Circumcenter.Y);

        Vector2 va = new((float)a.X, (float)a.Y);
        Vector2 vb = new((float)b.X, (float)b.Y);

        Vector2 edge = vb - va;

        if (edge.LengthSquared() < 0.000001f)
        {
            start = default;
            end = default;
            return false;
        }

        /*
         * There are two possible normals to the Delaunay edge.
         *
         * Pick the one pointing AWAY from the third vertex of the
         * triangle. That is the direction of the unbounded Voronoi ray.
         */

        Vector2 normal = new(
            -edge.Y,
            edge.X);

        normal.Normalize();

        Vector2 midpoint = (va + vb) * 0.5f;

        Point thirdPoint;

        if (triangle.Vertices[0] != a &&
            triangle.Vertices[0] != b)
        {
            thirdPoint = triangle.Vertices[0];
        }
        else if (triangle.Vertices[1] != a &&
                 triangle.Vertices[1] != b)
        {
            thirdPoint = triangle.Vertices[1];
        }
        else
        {
            thirdPoint = triangle.Vertices[2];
        }

        Vector2 third = new(
            (float)thirdPoint.X,
            (float)thirdPoint.Y);

        // Make normal point away from the triangle.
        if (Vector2.Dot(normal, third - midpoint) > 0f)
            normal = -normal;

        /*
         * Intersect the ray:
         *
         *     origin + normal * t
         *
         * with the diagram rectangle.
         *
         * This gives us the portion of the infinite Voronoi ray
         * that is actually visible inside the diagram.
         */

        float tMin = 0f;
        float tMax = float.MaxValue;

        if (!ClipRayAxis(
                origin.X,
                normal.X,
                0f,
                _width,
                ref tMin,
                ref tMax))
        {
            start = default;
            end = default;
            return false;
        }

        if (!ClipRayAxis(
                origin.Y,
                normal.Y,
                0f,
                _height,
                ref tMin,
                ref tMax))
        {
            start = default;
            end = default;
            return false;
        }

        if (tMax < tMin || tMax < 0f)
        {
            start = default;
            end = default;
            return false;
        }

        tMin = Math.Max(tMin, 0f);

        Vector2 p1 = origin + normal * tMin;
        Vector2 p2 = origin + normal * tMax;

        start = new Point(
            (int)Math.Round(Math.Clamp(p1.X, 0f, _width)),
            (int)Math.Round(Math.Clamp(p1.Y, 0f, _height)));

        end = new Point(
            (int)Math.Round(Math.Clamp(p2.X, 0f, _width)),
            (int)Math.Round(Math.Clamp(p2.Y, 0f, _height)));

        return start != end;
    }

    private static bool ClipRayAxis(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        if (Math.Abs(direction) < 0.000001f)
        {
            // Ray is parallel to this axis.
            return origin >= min && origin <= max;
        }

        float t1 = (min - origin) / direction;
        float t2 = (max - origin) / direction;
        if (t1 > t2)
            (t1, t2) = (t2, t1);

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);

        return tMin <= tMax;
    }

    private static List<Point> ClipPolygonToBounds(List<Point> polygon, int width, int height)
    {
        if (polygon.Count < 3)
            return [];

        var result = polygon;

        result = ClipPolygon(
            result,
            p => p.X >= 0,
            (a, b) => IntersectVertical(a, b, 0));

        result = ClipPolygon(
            result,
            p => p.X <= width,
            (a, b) => IntersectVertical(a, b, width));

        result = ClipPolygon(
            result,
            p => p.Y >= 0,
            (a, b) => IntersectHorizontal(a, b, 0));

        result = ClipPolygon(
            result,
            p => p.Y <= height,
            (a, b) => IntersectHorizontal(a, b, height));

        return result;
    }

    private static List<Point> ClipPolygon(
        List<Point> polygon,
        Func<Point, bool> inside,
        Func<Point, Point, Point> intersection)
    {
        if (polygon.Count == 0)
            return [];

        var result = new List<Point>();

        Point previous = polygon[^1];
        bool previousInside = inside(previous);

        foreach (Point current in polygon)
        {
            bool currentInside = inside(current);

            if (currentInside)
            {
                if (!previousInside)
                    result.Add(intersection(previous, current));

                result.Add(current);
            }
            else if (previousInside)
            {
                result.Add(intersection(previous, current));
            }

            previous = current;
            previousInside = currentInside;
        }

        return result
            .Distinct()
            .ToList();
    }

    private static Point IntersectVertical(
        Point a,
        Point b,
        float x)
    {
        var dx = (b.X - a.X);

        if (Math.Abs(dx) < 0.000001f)
            return new Point(
                (int)Math.Round(x),
                a.Y);

        var t = ((x - a.X) / dx);
        var y = (a.Y + (b.Y - a.Y) * t);

        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }

    private static Point IntersectHorizontal(
        Point a,
        Point b,
        float y)
    {
        var dy = b.Y - a.Y;

        if (Math.Abs(dy) < 0.000001f)
            return new Point(a.X, (int)Math.Round(y));

        var t = (y - a.Y) / dy;
        var x = a.X + (b.X - a.X) * t;

        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }

    private void DrawTriangleBatch()
    {
        if (_triangleBatchPrimitiveCount == 0)
            return;

        BlendState previousBlendState = _graphicsDevice.BlendState;
        RasterizerState previousRasterizerState = _graphicsDevice.RasterizerState;

        try
        {
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _triangleBatchVertices,
                    0,
                    _triangleBatchPrimitiveCount);
            }
        }
        finally
        {
            _graphicsDevice.BlendState = previousBlendState;
            _graphicsDevice.RasterizerState = previousRasterizerState;
        }
    }

    private void BuildTriangleBatch(IEnumerable<Triangle> triangulation)
    {
        var triangles =
            triangulation as ICollection<Triangle> ??
            triangulation.ToList();

        _triangleBatchVertices =
            new VertexPositionColor[triangles.Count * 3];

        int vertexIndex = 0;

        foreach (Triangle triangle in triangles)
        {
            int rgb;
            do rgb = _random.Next(0x1000000); // 0 … 16 777 215
            while (!_usedColors.Add(rgb));
            var color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(
                    new Vector3((float)triangle.Vertices[0].X, (float)triangle.Vertices[0].Y, 0f), color);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(
                    new Vector3((float)triangle.Vertices[1].X, (float)triangle.Vertices[1].Y, 0f), color);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(
                    new Vector3((float)triangle.Vertices[2].X, (float)triangle.Vertices[2].Y, 0f), color);
        }

        _triangleBatchPrimitiveCount = triangles.Count;
    }

    private void BuildPointBatch(float size)
    {
        var vertices = new List<VertexPositionColor>(_points.Length * 6);
        float half = size * 0.5f;

        foreach (Point point in _points)
        {
            if (ShouldCullBoundarySite(point))
                continue;

            float x = (float)point.X;
            float y = (float)point.Y;

            Vector3 a = new(x - half, y - half, 0f);
            Vector3 b = new(x + half, y - half, 0f);
            Vector3 c = new(x + half, y + half, 0f);
            Vector3 d = new(x - half, y + half, 0f);

            vertices.Add(new VertexPositionColor(a, _pointColor));
            vertices.Add(new VertexPositionColor(b, _pointColor));
            vertices.Add(new VertexPositionColor(c, _pointColor));

            vertices.Add(new VertexPositionColor(a, _pointColor));
            vertices.Add(new VertexPositionColor(c, _pointColor));
            vertices.Add(new VertexPositionColor(d, _pointColor));
        }

        _pointBatchVertices = vertices.ToArray();
        _pointBatchPrimitiveCount = _pointBatchVertices.Length / 3;
    }

    private void DrawPointBatch()
    {
        if (_pointBatchPrimitiveCount == 0)
            return;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _pointBatchVertices,
                0,
                _pointBatchPrimitiveCount);
        }
    }

    private void DrawEdgeBatch()
    {
        if (_edgeBatchPrimitiveCount == 0)
            return;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _edgeBatchVertices,
                0,
                _edgeBatchPrimitiveCount);
        }
    }

    private void DrawCellBatch()
    {
        if (_cellBatchPrimitiveCount == 0)
            return;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _cellBatchVertices,
                0,
                _cellBatchPrimitiveCount);
        }
    }

    private void BuildEdgeBatch(float thickness)
    {
        var vertices = new List<VertexPositionColor>(_voronoiEdges.Count * 6);
        float halfThickness = thickness * 0.5f;
        foreach (Edge edge in _voronoiEdges)
        {
            Point p1 = edge.Point1;
            Point p2 = edge.Point2;

            Vector2 start = new((float)p1.X, (float)p1.Y);
            Vector2 end = new((float)p2.X, (float)p2.Y);

            Vector2 direction = end - start;
            float lengthSquared = direction.LengthSquared();

            if (lengthSquared <= 0.000001f)
                continue;

            direction *= 1f / MathF.Sqrt(lengthSquared);

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * halfThickness;
            Vector3 a = new(start + perpendicular, 0f);
            Vector3 b = new(start - perpendicular, 0f);
            Vector3 c = new(end - perpendicular, 0f);
            Vector3 d = new(end + perpendicular, 0f);

            // Two triangles.
            vertices.Add(new VertexPositionColor(a, _edgeColor));
            vertices.Add(new VertexPositionColor(b, _edgeColor));
            vertices.Add(new VertexPositionColor(c, _edgeColor));

            vertices.Add(new VertexPositionColor(a, _edgeColor));
            vertices.Add(new VertexPositionColor(c, _edgeColor));
            vertices.Add(new VertexPositionColor(d, _edgeColor));
        }

        _edgeBatchVertices = vertices.ToArray();
        _edgeBatchPrimitiveCount = _edgeBatchVertices.Length / 3;
    }

    private void BuildCellBatch()
    {
        int triangleCount = 0;

        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            if (cell.Vertices.Count >= 3)
                triangleCount += cell.Vertices.Count - 2;
        }

        _cellBatchVertices = new VertexPositionColor[triangleCount * 3];

        int vertexIndex = 0;
        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            var vertices = cell.Vertices;
            if (vertices.Count < 3)
                continue;

            Color color = _cellRawColors[cell.Site];
            Vector3 origin = new((float)vertices[0].X, (float)vertices[0].Y, 0f);

            for (int i = 1; i < vertices.Count - 1; i++)
            {
                _cellBatchVertices[vertexIndex++] =
                    new VertexPositionColor(
                        origin,
                        color);

                _cellBatchVertices[vertexIndex++] =
                    new VertexPositionColor(
                        new Vector3(
                            (float)vertices[i].X,
                            (float)vertices[i].Y,
                            0f),
                        color);

                _cellBatchVertices[vertexIndex++] =
                    new VertexPositionColor(
                        new Vector3(
                            (float)vertices[i + 1].X,
                            (float)vertices[i + 1].Y,
                            0f),
                        color);
            }
        }

        _cellBatchPrimitiveCount = _cellBatchVertices.Length / 3;
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
                color = _edgeColor;
                break;
        }

        return color;
    }

    private static void ValidateNeighbors(List<Triangle> triangles)
    {
        foreach (Triangle t in triangles)
        {
            Validate(t, 0, t.Neighbor0);
            Validate(t, 1, t.Neighbor1);
            Validate(t, 2, t.Neighbor2);
        }

        return;

        void Validate(Triangle t, int edge, Triangle? n)
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
                throw new InvalidOperationException("Neighbor does not share the expected edge.");

            Triangle? reverse = reciprocal switch
            {
                0 => n.Neighbor0,
                1 => n.Neighbor1,
                _ => n.Neighbor2
            };

            if (!ReferenceEquals(reverse, t))
                throw new InvalidOperationException("Neighbor relationship is not reciprocal.");
        }
    }

    #endregion

    #region Enums

    public enum VoronoiBoundaryMode : byte
    {
        Culled,

        // ReSharper disable once UnusedMember.Global
        // Indirectly referenced by omission.
        HardEdge
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

    #endregion
}

internal struct CellVoronoiEdge
{
    public readonly Point SiteA;
    public readonly Point? SiteB;
    public readonly Point Point1;
    public readonly Point Point2;

    public CellVoronoiEdge(
        Point siteA,
        Point? siteB,
        Point point1,
        Point point2)
    {
        SiteA = siteA;
        SiteB = siteB;
        Point1 = point1;
        Point2 = point2;
    }
}