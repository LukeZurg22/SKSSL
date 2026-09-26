using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

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
public partial class Voronoi
{
    // I/O and Seeding
    private readonly GraphicsDevice _graphicsDevice;
    private const int DefaultPointCount = 2000;
    private Point[] _points = [];

    /// <value>_graphicsDevice.Viewport.Width</value>
    private int _width;

    /// <value>_graphicsDevice.Viewport.Height</value>
    private int _height;

    // Data Storage
    private readonly List<Edge> _voronoiEdges = []; // For Rendering the "proper" edges of each cell.
    private readonly Dictionary<Point, VoronoiCell> _voronoiCells = []; // TODO: use an array.
    private readonly List<CellVoronoiEdge> _cellVoronoiEdges = [];
    private List<CellVoronoiEdge>?[] _edgesByCell = [];
    private DistributorImage? _imageDistributor = null; // For heightmap distribution.

    //      A graph may become too large, it'll need to be divided into "chunks" / spacial grids.
    private SpatialGrid? _spatialGrid;

    // Coloring and Visualization
    private readonly Dictionary<uint, int> _cellIndices = [];
    private Color[] _cellOverrideColors; // Override colors for cells. Indexed by ID.
    private Color[] _cellRawColors; // Raw "provincial" colors of cells. Indexed by ID.
    private readonly HashSet<int> _usedColors = []; // avoid exact RGB collisions
    private readonly Random _random = new(32); // fixed seed → reproducible

    // Data Caching
    private VertexPositionColor[] _cellBatchVertices = []; //  CELLS
    private int _cellBatchPrimitiveCount;
    private VertexPositionColor[] _edgeBatchVertices = []; //  EDGES
    private int _edgeBatchPrimitiveCount;
    private VertexPositionColor[] _pointBatchVertices = []; //  POINTS
    private int _pointBatchPrimitiveCount;
    private VertexPositionColor[] _triangleBatchVertices = []; //  TRIANGLES
    private int _triangleBatchPrimitiveCount;
    private VertexPositionColor[] _demarcateBatchVertices = []; //  DEMARCATIONS
    private int _demarcateBatchPrimitiveCount;

    // Rendering Basics
    private Texture2D _pixelMap = null!;
    private BasicEffect _effect;

    // Rendering Options
    private VoronoiBoundaryMode _boundaryMode = VoronoiBoundaryMode.Culled;
    private readonly ColorMode CellDrawMode;
    private readonly Color _pointColor;
    private readonly Color _edgeColor;
    private readonly Color _flatColor; // Flat color to "erase" other cells when using overrides.

    // Cache Flags
    private VoronoiRenderingFlags _previousFlags;
    private VoronoiBoundaryMode _previousBoundaryMode;
    private float _previousThickness;
    private float _previousPointSize;
    private bool _textureValid;
    private bool _isGenerated = false;

    private readonly Dictionary<uint, VoronoiCell> _cellsById = [];
    private VoronoiCell[] _voronoiCellArray = [];

    // TODO: Support constrained and density-driven Voronoi generation.
    //  .
    //  1. Explicit boundaries
    //     a. Accept an image or polygon boundary.
    //     b. Extract functional edges / regions from the image.
    //        Color coding may be useful for identifying boundaries.
    //     c. Treat extracted boundaries as clipping/constraining geometry.
    //  .
    //  2. Hierarchical polygon generation
    //     a. Generate a sparse Voronoi diagram.
    //     b. Use its edges as candidate large-scale polygon boundaries.
    //     c. Merge/partition cells into larger regions.
    //     d. Optionally generate a denser Voronoi diagram inside each region.
    //  .
    //  3. Density-map-driven generation
    //     e. Support combining density maps with explicit boundaries.

    //  TODO: Implement LOD & Mip-Mapping, to coincide with culling when out-of view of the "camera".

    // TODO: Add a flashing "Selected" highlight which uses Marked Cells. This will be rough.
    //      To make this performant, recreate a Color highlight map every time a cell is marked, then:
    //      - Simply decrease and increase opacity in the draw call via a Math.Lerp() or something.
    //      Replace existing "highlight" terminology with "selected", and then use "highlight" for the literal highlighting.


    #region Construction & Mono Code

    public Voronoi(
        GraphicsDevice graphicsDevice,
        ColorMode cellDrawMode = ColorMode.Semi_Deterministic_Unique,
        Color? unifiedColor = null,
        Color? pointColor = null,
        Color? flatColor = null)
    {
        CellDrawMode = cellDrawMode;
        _graphicsDevice = graphicsDevice;
        _cellRawColors = new Color[DefaultPointCount + 1];
        _cellOverrideColors = new Color[DefaultPointCount + 1];
        _edgeColor = unifiedColor ?? Color.Gray;
        _pointColor = pointColor ?? Color.Red;
        _flatColor = flatColor ?? Color.Wheat;
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
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            0f, _width, _height, 0f, 0f, 1f
        );
    }

    private void SetScreenProjection()
    {
        Viewport viewport = _graphicsDevice.Viewport;

        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(
            0f, viewport.Width, viewport.Height, 0f, 0f, 1f
        );
    }

    #endregion

    #region Diagram Generation

    /// <summary>
    /// Generates a set of voronoi cells and internally inserts them into the data of this <see cref="Voronoi"/>
    /// class object.
    /// </summary>
    /// <returns>Generated 2D image of pixel data generated from diagram.</returns>
    /// <remarks>
    /// Also calls <see cref="UpdateTexture"/> to populate the pixel data.
    /// </remarks>
    /// <param name="settings"></param>
    /// <param name="points"></param>
    /// <param name="width">Width of diagram in pixels.</param>
    /// <param name="height">Height of diagram in pixels.</param>
    public void GenerateDiagram(
        int points = DefaultPointCount,
        int? width = null,
        int? height = null,
        DiagramSettings? settings = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 1);

        settings ??= new DiagramSettings();

        _isGenerated = false;
        _textureValid = false;
        _boundaryMode = settings.BoundaryMode;

        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;

        if (settings.Distributor is DistributorImage distributorImage)
            _imageDistributor = distributorImage;

        SetDiagramProjection();

        using DelaunayTriangulator delaunay = new();

        // Create a list of seeded points and then handle distribution using a provided distributor.
        int maxX = width.Value;
        int maxY = height.Value;
        var pointsList = delaunay.CreatePointsList(maxX, maxY);
        settings.Distributor.Generate(ref pointsList, points, maxX, maxY, settings.Randomness);
        _points = pointsList.ToArray();

        // Clear color storage. New sizes are +1 due to point amount being 1-based indexed.
        Array.Clear(_cellOverrideColors, 0, _cellRawColors.Length);
        Array.Clear(_cellRawColors, 0, _cellRawColors.Length);
        Array.Resize(ref _cellOverrideColors, _points.Length + 1);
        Array.Resize(ref _cellRawColors, _points.Length + 1);

        // Clear and repopulate cell index dictionary.
        _cellIndices.Clear();
        for (int i = 0; i < _points.Length; i++)
            _cellIndices[_points[i].ID] = i;

        // Make the triangles.
        var triangulation = delaunay.BowyerWatson(_points);

        // Clear old data.
        _cellsById.Clear();
        _voronoiCells.Clear();
        _voronoiEdges.Clear();
        _usedColors.Clear();
        _cellVoronoiEdges.Clear();
        _voronoiEdges.Capacity = Math.Max(_voronoiEdges.Capacity, points * 3);
        _voronoiCells.EnsureCapacity(_points.Length);

        // POPULATE

        #region Populate Cells

        PopulateVoronoiCells();

        // Lloyd relaxation requires rebuilding the triangulation and Voronoi cells, which can be a little expensive
        //  for large graphs.
        if (settings.SettlePoints)
        {
#if DEBUG
            Debug.WriteLine("Lloyd: starting");
#endif
            delaunay.LloydSettlePoints(_voronoiCells.Values);
#if DEBUG
            Debug.WriteLine("Lloyd: finished");
#endif
            _voronoiCells.Clear();
            triangulation = delaunay.BowyerWatson(_points);
            PopulateVoronoiCells();
        }

#if DEBUG
        ValidateNeighbors(triangulation);
#endif

        #endregion

        // PROCESS
        ProcessCells(settings.BoundaryMode);

        // CELL CLIPPING
        GapGeometry? gapGeometry = null;
        if (_imageDistributor is { AllowBlackGaps: true } dim)
        {
            gapGeometry = BuildGapGeometry(dim.GapPolygons, dim.GapMask.Width, dim.GapMask.Height, _width, _height);
            ClipCellsAgainstGaps(gapGeometry, _voronoiCells.Values); // WARN: This clipping is off.
        }

        BuildCellArray();
        BuildCellGeometry();

        // TODO: RenderDebugEdgesThroughGaps +--> Settings
        BuildVoronoiEdges(triangulation, settings.BoundaryMode, gapGeometry, false);
        BuildSelectCellEdges(); // This is to improve performance for selection.

        // Points.
        if ((settings.Flags & VoronoiRenderingFlags.Points) != 0)
            BuildPointBatch(settings.PointSize);

        // Edges.
        if ((settings.Flags & VoronoiRenderingFlags.Edges) != 0)
            BuildEdgeBatch(settings.Thickness);

        // Triangles. (Best not to use these, though. They're ugly.
        // ReSharper disable once PossibleMultipleEnumeration ; False positive.
        if ((settings.Flags & VoronoiRenderingFlags.Triangles) != 0)
            BuildTriangleBatch(triangulation, settings.BoundaryMode);

        // Cells. (Star of the show.)
        if ((settings.Flags & VoronoiRenderingFlags.Cells) != 0)
        {
            BuildCellVertexBatch();
            BuildCellColorBatch([]);
        }

        _isGenerated = true;

        // Update the existing internal pixel map with visual changes.
        UpdateTexture(settings.BoundaryMode, settings.Flags, settings.Thickness, settings.PointSize);

        // A spatial grid, aka a bucket grid is needed to subdivide the voronoi map into workable chunks.
        // This is for performance.
        BuildSpatialGrid();
        return;

        void PopulateVoronoiCells()
        {
            foreach (Triangle triangle in triangulation)
            {
                foreach (Point site in triangle.Vertices)
                {
                    if (!_voronoiCells.TryGetValue(site, out VoronoiCell? cell))
                    {
                        cell = new VoronoiCell(site, []);
                        _voronoiCells.Add(site, cell);
                        _cellsById.Add(site.ID, cell);
                    }

                    cell.Vertices.Add(triangle.Circumcenter);
                }

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
            }
        }
    }

    /// Prepares the original cells before any special culling or other operations.
    private void ProcessCells(VoronoiBoundaryMode boundaryMode)
    {
        _usedColors.Clear();

        Parallel.ForEach(_voronoiCells.Values, cell =>
        {
            cell.Vertices = cell.Vertices
                .Distinct()
                .ToList();

            SortVerticesAround(cell.Vertices, cell.Site);

            bool touchesOutside = cell.Vertices.Any(v => v.X < 0 || v.X > _width || v.Y < 0 || v.Y > _height);
            if (touchesOutside)
            {
                switch (boundaryMode)
                {
                    case VoronoiBoundaryMode.Culled:
                        cell.Vertices.Clear();
                        break;

                    case VoronoiBoundaryMode.HardEdgeOpen:
                    default:
                        cell.Vertices = ClipPolygonToBounds(cell.Vertices, _width, _height);
                        break;
                }
            }

            _cellRawColors[_cellIndices[cell.ID]] = GetCellColor(cell);

            cell.IsCulled = IsArtificialBoundarySite(cell.Site) ||
                            (boundaryMode == VoronoiBoundaryMode.Culled &&
                             (cell.IsBoundary || cell.Vertices.Count == 0));
        });
    }

    private void BuildSpatialGrid()
    {
        // ReSharper disable once PossibleLossOfFraction
        int gridSize = Math.Max(10, (int)Math.Sqrt(_voronoiCellArray.Length / 4));
        float cellWidth = (float)_width / gridSize;
        float cellHeight = (float)_height / gridSize;
        var cells = _voronoiCellArray;
        var bucketIndices = new int[cells.Length];

        for (int i = 0; i < cells.Length; i++)
        {
            VoronoiCell cell = cells[i];
            int x = Math.Clamp((int)(cell.Site.X / cellWidth), 0, gridSize - 1);
            int y = Math.Clamp((int)(cell.Site.Y / cellHeight), 0, gridSize - 1);
            bucketIndices[i] = y * gridSize + x;
        }

        int bucketCount = gridSize * gridSize;
        var buckets = new List<VoronoiCell>[bucketCount];

        for (int i = 0; i < bucketCount; i++)
            buckets[i] = [];

        for (int i = 0; i < cells.Length; i++)
            buckets[bucketIndices[i]].Add(cells[i]);

        _spatialGrid = new SpatialGrid(buckets, gridSize, cellWidth, cellHeight);
    }

    private void BuildSelectCellEdges()
    {
        long maxSiteId = -1;
        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            maxSiteId = Math.Max(maxSiteId, edge.SiteA.ID);
            if (edge.SiteB.HasValue)
                maxSiteId = Math.Max(maxSiteId, edge.SiteB.Value.ID);
        }

        if (_edgesByCell.Length != maxSiteId + 1)
            _edgesByCell = new List<CellVoronoiEdge>?[maxSiteId + 1];
        else Array.Clear(_edgesByCell);

        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            var edgesA = _edgesByCell[edge.SiteA.ID];
            if (edgesA == null)
            {
                edgesA = [];
                _edgesByCell[edge.SiteA.ID] = edgesA;
            }

            edgesA.Add(edge);
            if (!edge.SiteB.HasValue)
                continue;

            Point siteB = edge.SiteB.Value;
            var edgesB = _edgesByCell[siteB.ID];
            if (edgesB == null)
            {
                edgesB = [];
                _edgesByCell[siteB.ID] = edgesB;
            }

            edgesB.Add(edge);
        }
    }

    /// <summary>
    /// Gets the voronoi object's rasterized texture, or creates one.
    /// </summary>
    /// <returns>Texture2D reference of the Voronoi image result.</returns>
    /// <remarks>
    /// Make sure that the Diagram data is generated first through <see cref="GenerateDiagram"/>.
    /// When this function is called, it also assigns the internal pixel data to the new image.
    /// </remarks>
    private void UpdateTexture(
        VoronoiBoundaryMode boundaryMode,
        VoronoiRenderingFlags flags,
        float thickness,
        float pointSize,
        bool forceUpdate = false)
    {
        if (!_isGenerated)
            throw new InvalidOperationException(
                "Attempted to get Voronoi texture before generating a diagram.");

        if (!forceUpdate &&
            _textureValid &&
            flags == _previousFlags &&
            boundaryMode == _previousBoundaryMode &&
            Math.Abs(thickness - _previousThickness) < 0.01f &&
            Math.Abs(pointSize - _previousPointSize) < 0.01f)
            return;

        var output = new RenderTarget2D(
            _graphicsDevice,
            _width,
            _height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);

        var previousTargets = _graphicsDevice.GetRenderTargets();

        _graphicsDevice.SetRenderTarget(output);

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

            //if (_imageDistributor is { AllowBlackGaps: true })
            //    DrawGapBatch();
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
    }

    public void Draw(SpriteBatch? spriteBatch)
    {
        if (!_isGenerated || spriteBatch == null)
            return;

        spriteBatch.Begin();
        spriteBatch.Draw(_pixelMap, Vector2.Zero, Color.White);
        spriteBatch.End();

        DrawHighlightedCells();
    }

    #endregion

    #region TryGet Methods

    /// <summary>
    /// Get a <see cref="VoronoiCell"/> definition using a Cell ID.
    /// </summary>
    /// <param name="cellID"></param>
    /// <param name="cell"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGetCell(uint cellID, [NotNullWhen(true)] out VoronoiCell? cell)
        => _cellsById.TryGetValue(cellID, out cell);

    /// <summary>
    /// Attempt to get a cell at a provided screen position.
    /// </summary>
    /// <param name="position">Mouse position / screen position.</param>
    /// <param name="cell">Output cell for use elsewhere. Null if not found.</param>
    /// <returns>True if found cell, false if not. Out will be null if false.</returns>
    /// <remarks>May cause lag at immense diagram sizes, mostly around 50k and beyond.</remarks>
    // ReSharper disable once UnusedMethodReturnValue.Global
    public bool TryGetCellAt(System.Drawing.Point position, [NotNullWhen(true)] out VoronoiCell? cell)
    {
        cell = null;
        return _spatialGrid != null && _spatialGrid.TryGetCellAt(position, out cell);
    }

    #endregion
}