using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Clipper2Lib;
using LibTessDotNet.Double;
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
    private VoronoiCell[] _voronoiCellArray = [];

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
    private VertexPositionColor[] _highlightBatchVertices = []; //  HIGHLIGHTS
    private int _highlightBatchPrimitiveCount;

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

    private Texture2D? _highlightMask;
    private Color _highlightColor = Color.Yellow;
    private readonly Dictionary<uint, VoronoiCell> _cellsById = [];

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

    #region Diagram Generation

    /// <summary>
    /// Generates a set of voronoi cells and internally inserts them into the data of this <see cref="Voronoi"/>
    /// class object.
    /// </summary>
    /// <returns>Generated 2D image of pixel data generated from diagram.</returns>
    /// <remarks>
    /// Also calls <see cref="UpdateTexture"/> to populate the pixel data.
    /// </remarks>
    /// <param name="points"></param>
    /// <param name="width">Width of diagram in pixels.</param>
    /// <param name="height">Height of diagram in pixels.</param>
    /// <param name="settlePoints">Toggle for easing points to a settled arrangement.</param>
    /// <param name="randomness">
    ///     Evenness of distribution on a scale of 0.00 -> 1.00; only works with the
    /// </param>
    /// <param name="distributor">
    ///     Provided custom point distributor that decides the positioning of the point X and Y positions.
    /// </param>
    /// <param name="boundaryMode"></param>
    /// <param name="flags">Convenient Enum toggle of various parts of a Voronoi diagram.</param>
    /// <param name="thickness">Thickness of Edges, if they are rendered.</param>
    /// <param name="pointSize">Size of Points, if they are rendered.</param>
    public void GenerateDiagram(
        int points = DefaultPointCount,
        int? width = null,
        int? height = null,
        bool settlePoints = false,
        double randomness = 0.8,
        IPointDistributor? distributor = null,
        VoronoiBoundaryMode boundaryMode = VoronoiBoundaryMode.Culled,
        VoronoiRenderingFlags flags = VoronoiRenderingFlags.Cells,
        float thickness = 1f,
        float pointSize = 2f)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 1);

        _isGenerated = false;
        _textureValid = false;
        _boundaryMode = boundaryMode;

        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;

        distributor ??= new DistributorRandomJitter();
        if (distributor is DistributorImage distributorImage)
            _imageDistributor = distributorImage;

        SetDiagramProjection();

        using DelaunayTriangulator delaunay = new();

        // Create a list of seeded points and then handle distribution using a provided distributor.
        int maxX = width.Value;
        int maxY = height.Value;
        var pointsList = delaunay.CreatePointsList(maxX, maxY);
        distributor.Generate(ref pointsList, points, maxX, maxY, randomness);
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

        PopulateVoronoiCells();

        // Lloyd relaxation requires rebuilding the triangulation and Voronoi cells, which can be a little expensive
        //  for large graphs.
        if (settlePoints)
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

        ProcessCells(boundaryMode, triangulation);

        // Separate from Vertices list.
        if (_imageDistributor is { AllowBlackGaps: true } dim)
        {
            GapGeometry gapGeometry = BuildGapGeometry(
                dim.GapPolygons, dim.GapMask.Width, dim.GapMask.Height, _width, _height);
            ClipCellsAgainstGaps(gapGeometry, _voronoiCells.Values);
        }

        BuildCellArray();
        BuildCellGeometry();

        // Pre-building for the highlighter. This is here for caching to improve performance during
        // active runtime update calls.
        BuildEdgesByCell();

        // Points.
        if ((flags & VoronoiRenderingFlags.Points) != 0)
            BuildPointBatch(pointSize);

        // Edges.
        if ((flags & VoronoiRenderingFlags.Edges) != 0)
            BuildEdgeBatch(thickness);

        // Triangles. (Best not to use these, though. They're ugly.
        // ReSharper disable once PossibleMultipleEnumeration ; False positive.
        if ((flags & VoronoiRenderingFlags.Triangles) != 0)
            BuildTriangleBatch(triangulation);

        // Cells. (Star of the show.)
        if ((flags & VoronoiRenderingFlags.Cells) != 0)
            BuildCellBatch([]);

        _isGenerated = true;

        // Update the existing internal pixel map with visual changes.
        UpdateTexture(boundaryMode, flags, thickness, pointSize);

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
    private void ProcessCells(VoronoiBoundaryMode boundaryMode, List<Triangle> triangulation)
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
            cell.IsCulled = boundaryMode == VoronoiBoundaryMode.Culled && (cell.IsBoundary || cell.Vertices.Count == 0);
        });

        foreach (Triangle triangle in triangulation)
            AddVoronoiEdges(triangle, boundaryMode);
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

    private void BuildEdgesByCell()
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

        SpatialGrid? grid = _spatialGrid;

        if (grid == null || grid.Buckets.Length == 0)
        {
            return false;
        }

        int gridX = Math.Clamp((int)(position.X / grid.CellWidth), 0, grid.Size - 1);
        int gridY =
            Math.Clamp((int)(position.Y / grid.CellHeight), 0, grid.Size - 1);

        /*
         * Only cells in neighboring spatial buckets need
         * to be tested.
         */
        for (int y = gridY - 1; y <= gridY + 1; y++)
        {
            if (y < 0 || y >= grid.Size)
                continue;

            for (int x = gridX - 1; x <= gridX + 1; x++)
            {
                if (x < 0 || x >= grid.Size)
                    continue;

                var bucket = grid.Buckets[y * grid.Size + x];
                foreach (VoronoiCell candidate in bucket)
                {
                    if (ShouldCullBoundarySite(candidate.Site))
                        continue;

                    if (!IsPointInsideCell(candidate, position.X, position.Y))
                        continue;

                    cell = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Cell Highlighting

    private readonly HashSet<uint> _highlightedCellIds = [];

    public void SetHighlightedCells(IEnumerable<VoronoiCell> cells, Color color, float thickness = 1f)
    {
        ArgumentNullException.ThrowIfNull(cells);

        _highlightedCellIds.Clear();

        foreach (VoronoiCell cell in cells)
        {
            if (ShouldCullBoundarySite(cell.Site))
                continue;

            _highlightedCellIds.Add(cell.ID);
        }

        RebuildCellSelectBorderBatch(color, thickness);
    }

    public void SetHighlightedCell(uint cellId, Color color, float thickness = 1f)
    {
        _highlightedCellIds.Clear();
        if (!_cellIndices.ContainsKey(cellId))
        {
            ClearHighlightBatch();
            return;
        }

        if (_cellsById.TryGetValue(cellId, out VoronoiCell? cell) && ShouldCullBoundarySite(cell.Site))
        {
            ClearHighlightBatch();
            return;
        }

        _highlightedCellIds.Add(cellId);

        RebuildCellSelectBorderBatch(color, thickness);
    }

    public void SetHighlightedColor(Color color, float thickness = 1f)
    {
        uint packedColor = color.PackedValue;
        _highlightedCellIds.Clear();
        for (int i = 0; i < _voronoiCellArray.Length; i++)
            if (_cellRawColors[i].PackedValue == packedColor)
                _highlightedCellIds.Add(_voronoiCellArray[i].ID);
        RebuildCellSelectBorderBatch(color, thickness);
    }

    public void SetHighlightedColors(IEnumerable<Color> colors, Color highlightColor, float thickness = 1f)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var packedColors = colors
            .Select(color => color.PackedValue)
            .ToHashSet();

        _highlightedCellIds.Clear();

        for (int i = 0; i < _voronoiCellArray.Length; i++)
            if (packedColors.Contains(_cellRawColors[i].PackedValue))
                _highlightedCellIds.Add(_voronoiCellArray[i].ID);

        RebuildCellSelectBorderBatch(highlightColor, thickness);
    }

    // ReSharper disable once UnusedMember.Global
    public void ClearHighlightedCells()
    {
        _highlightedCellIds.Clear();
        ClearHighlightBatch();
    }

    private void RebuildCellSelectBorderBatch(Color color, float thickness)
    {
        if (_highlightedCellIds.Count == 0 || _cellVoronoiEdges.Count == 0)
        {
            ClearHighlightBatch();
            return;
        }

        /*
         * Every Voronoi edge is considered exactly once.
         * An edge is highlighted when exactly one of its two cells is selected.
         *
         * Therefore:
         *     selected <-> selected   = internal edge, skip
         *     selected <-> unselected = boundary, draw
         *     selected <-> outside    = boundary, draw
         */
        var vertices = new List<VertexPositionColor>(_highlightedCellIds.Count * 18);

        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            bool aHighlighted =
                _highlightedCellIds.Contains(edge.SiteA.ID);

            bool bHighlighted =
                edge.SiteB.HasValue &&
                _highlightedCellIds.Contains(edge.SiteB.Value.ID);

            if (aHighlighted == bHighlighted)
                continue;

            AddHighlightEdge(
                vertices,
                edge.Point1,
                edge.Point2,
                color,
                thickness);
        }

        _highlightBatchVertices = vertices.ToArray();
        _highlightBatchPrimitiveCount =
            _highlightBatchVertices.Length / 3;
    }

    private void BuildCellGeometry()
    {
        int cellCount = _voronoiCellArray.Length;

        if (cellCount == 0)
        {
            _cellGeometry = [];
            _cellGeometryOffsets = [];
            _cellGeometryCounts = [];
            return;
        }

        var geometries = new Vector3[cellCount][];
        var counts = new int[cellCount];

        Parallel.For(0, cellCount, i =>
        {
            VoronoiCell cell = _voronoiCellArray[i];
            Vector3[] geometry;

            if (cell.Vertices.Count < 3 && (cell.RenderPaths == null || cell.RenderPaths.Count == 0)) geometry = [];
            else if (cell.RenderPaths == null) geometry = TessellateOriginalCellPositions(cell.Vertices);
            else if (cell.RenderPaths.Count == 0) geometry = [];
            else geometry = TessellateClippedCellPositions(cell.RenderPaths);

            geometries[i] = geometry;
            counts[i] = geometry.Length;
        });

        int total = 0;
        var offsets = new int[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            offsets[i] = total;
            total += counts[i];
        }

        var geometryBuffer = new Vector3[total];
        for (int i = 0; i < cellCount; i++)
        {
            var source = geometries[i];
            if (source.Length == 0)
                continue;

            source.AsSpan().CopyTo(geometryBuffer.AsSpan(offsets[i]));
        }

        _cellGeometry = geometryBuffer;
        _cellGeometryOffsets = offsets;
        _cellGeometryCounts = counts;
    }

    private void BuildCellArray()
    {
        int count = _cellIndices.Count;
        if (_voronoiCellArray.Length != count)
            _voronoiCellArray = new VoronoiCell[count];

        foreach (VoronoiCell cell in _voronoiCells.Values)
            _voronoiCellArray[_cellIndices[cell.ID]] = cell;
    }

    private static Vector3[] TessellateOriginalCellPositions(List<Point> vertices)
    {
        int count = vertices.Count;
        if (count < 3)
            return [];

        var result = new Vector3[(count - 2) * 3];
        ReadOnlySpan<Point> points = CollectionsMarshal.AsSpan(vertices);
        Point origin = points[0];
        Vector3 originPosition = new(origin.X, origin.Y, 0f);
        int index = 0;
        for (int i = 1; i < count - 1; i++)
        {
            Point b = points[i];
            Point c = points[i + 1];
            result[index++] = originPosition;
            result[index++] = new Vector3(b.X, b.Y, 0f);
            result[index++] = new Vector3(c.X, c.Y, 0f);
        }

        return result;
    }

    private static Vector3[] TessellateClippedCellPositions(Paths64 paths)
    {
        if (paths.Count == 0)
            return [];

        var tess = new Tess();
        foreach (Path64 path in paths)
        {
            if (path.Count < 3)
                continue;

            var contour = new ContourVertex[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Point64 point = path[i];
                contour[i].Position = new Vec3(point.X, point.Y, 0.0);
            }

            tess.AddContour(contour);
        }

        tess.Tessellate();

        if (tess.ElementCount == 0)
            return [];

        var result = new Vector3[tess.ElementCount * 3];

        for (int i = 0; i < tess.ElementCount; i++)
        {
            int elementIndex = i * 3;
            result[elementIndex] = ToVector3(tess.Vertices[tess.Elements[elementIndex]].Position);
            result[elementIndex + 1] = ToVector3(tess.Vertices[tess.Elements[elementIndex + 1]].Position);
            result[elementIndex + 2] = ToVector3(tess.Vertices[tess.Elements[elementIndex + 2]].Position);
        }

        return result;

        static Vector3 ToVector3(Vec3 p) =>
            new((float)p.X, (float)p.Y, 0f);
    }

    private void ClearHighlightBatch()
    {
        _highlightBatchVertices = [];
        _highlightBatchPrimitiveCount = 0;
    }

    private static void AddHighlightEdge(
        List<VertexPositionColor> vertices,
        Point point1,
        Point point2,
        Color color,
        float thickness)
    {
        Vector2 p1 = new(point1.X, point1.Y);
        Vector2 p2 = new(point2.X, point2.Y);

        Vector2 direction = p2 - p1;
        if (direction.LengthSquared() <= 0.000001f)
            return;

        direction.Normalize();

        Vector2 normal = new Vector2(-direction.Y, direction.X) * (thickness * 0.5f);
        Vector3 v1 = new(p1 - normal, 0f);
        Vector3 v2 = new(p1 + normal, 0f);
        Vector3 v3 = new(p2 + normal, 0f);
        Vector3 v4 = new(p2 - normal, 0f);

        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v2, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v4, color));
    }

    private void DrawHighlightedCells()
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

    #region Cell Coloring

    private readonly HashSet<uint> _markedCells = [];

    /// <summary>
    /// Using a position on a Voronoi diagram, attempts to get a cell and add it to a marked-cells list. 
    /// </summary>
    /// <param name="point"></param>
    // ReSharper disable once UnusedMember.Global
    public void MarkCell(System.Drawing.Point point)
    {
        if (!TryGetCellAt(point, out VoronoiCell? cell))
            return;

        MarkCell(cell.ID);
    }

    /// <summary>
    /// Add a cell instance ID into a list of marked-cells. 
    /// </summary>
    /// <param name="cell"></param>
    // ReSharper disable once UnusedMember.Global
    public void MarkCell(uint cell)
    {
        if (_cellsById.ContainsKey(cell))
            _markedCells.Add(cell);
    }

    /// <summary>
    /// Calls <see cref="ChangeCellColors"/> using the cell ids marked by <see cref="MarkCell(uint)"/>.
    /// </summary>
    /// <param name="color">Color to replace cells.</param>
    /// <param name="blankColor"></param>
    /// <param name="ignoreFlatColor"></param>
    /// <param name="clear">Clear internally marked cells. False by default.</param>
    public void ColorMarkedCells(
        Color color,
        Color? blankColor = null,
        bool ignoreFlatColor = false,
        bool clear = false)
    {
        blankColor ??= _flatColor;
        ChangeCellColors(_markedCells, color, blankColor.Value, ignoreFlatColor);
        if (clear) ClearMarkedCells();
    }

    public void ClearMarkedCells() => _markedCells.Clear();

    /// <summary>
    /// Sets all cells to a single color, which defaults to a provided/default flat color.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public void ChangeAllCellColors(Color? color = null)
    {
        if (!_isGenerated)
            return;

        color ??= _flatColor;
        BuildUniformCellBatch();
        UpdateTexture(_previousBoundaryMode, _previousFlags, _previousThickness, _previousPointSize, true);
        return;

        void BuildUniformCellBatch()
        {
            int count = _cellGeometry.Length;

            if (_cellBatchVertices.Length < count)
                _cellBatchVertices = new VertexPositionColor[count];

            for (int i = 0; i < count; i++)
            {
                _cellBatchVertices[i] = new VertexPositionColor(_cellGeometry[i], color.Value);
            }

            _cellBatchPrimitiveCount = count / 3;
        }
    }

    /// <summary>
    /// Change multiple cells to a set color.
    /// </summary>
    /// <param name="idsAffected"></param>
    /// <param name="color"></param>
    /// <param name="blankColor"></param>
    /// <param name="ignoreFlatColor"></param>
    private void ChangeCellColors(HashSet<uint> idsAffected, Color color, Color blankColor, bool ignoreFlatColor)
    {
        if (!_isGenerated)
            return;

        // Rebuild french cell batch with colors provided. Internal logic will handle the way they are colored,
        //  and outside of this function they are handled as if no ids were affected.
        BuildCellBatch(idsAffected, ignoreFlatColor, (color, blankColor));
        UpdateTexture(
            _previousBoundaryMode,
            _previousFlags,
            _previousThickness,
            _previousPointSize,
            true);
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
                return color;
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
                return color;
            case ColorMode.Random: // Truly random 0 -> 255
                color = new Color(
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    255);
                return color;
            case ColorMode.Semi_Deterministic_Unique:
                // Grab a deterministic semi-unique color and go an integer check.
                // Naturally if it isn't unique, then it is reaching the birthday-paradox point
                color = ColorUtilities.GetSemiUniqueColor(cell.ID);
                int reversed = (color.R << 16) | (color.G << 8) | color.B;
                lock (_usedColors)
                {
                    // If the semi-unique color turns out to no longer be unique, then
                    // defaulting to the thread-dangerous unique color generator is the
                    // next best option. I am aware this causes an inner-dependency, and
                    // may also cause a little overhead. The deterministic method is faster
                    // than relying on the Random class to do its calls, and that for maps
                    // approximately smaller than 2000x2000, this would be incredibly efficient.
                    // As far as I see it, it's a small, but nevertheless preferred -optimization.
                    if (!_usedColors.Add(reversed)) goto case ColorMode.Unique;
                }

                return color;
            case ColorMode.Unique: // Pure random RGB – three integer ops, no floats
                int rgb;
                lock (_random)
                lock (_usedColors)
                {
                    do rgb = _random.Next(0x1000000); // 0 … 16 777 215
                    while (!_usedColors.Add(rgb));
                }

                color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);
                break;
            case ColorMode.Unified:
            default:
                color = _edgeColor;
                break;
        }

        return color;
    }

    #endregion

    #region I/O

    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    // ReSharper disable once UnusedMember.Global
    public void Save(string filePath) // TODO: Add test case for Voronoi Save()
    {
        var data = new VoronoiSaveData
        {
            Width = _width,
            Height = _height,
            BoundaryMode = _boundaryMode,

            Points = _points
                .Select(p => new PointData { X = p.X, Y = p.Y })
                .ToList(),

            Cells = _voronoiCells.Values
                .Select(c => new CellData
                {
                    X = c.Site.X,
                    Y = c.Site.Y,
                    IsBoundary = c.IsBoundary,
                    Vertices = c.Vertices
                        .Select(v => new PointData { X = v.X, Y = v.Y })
                        .ToList()
                })
                .ToList(),

            Edges = _voronoiEdges
                .Select(e => new EdgeData
                {
                    Point1 = new PointData { X = e.Point1.X, Y = e.Point1.Y },
                    Point2 = new PointData { X = e.Point2.X, Y = e.Point2.Y }
                })
                .ToList(),

            CellEdges = _cellVoronoiEdges
                .Select(e => new CellEdgeData
                {
                    SiteA = new PointData { X = e.SiteA.X, Y = e.SiteA.Y },
                    SiteB = e.SiteB.HasValue
                        ? new PointData { X = e.SiteB.Value.X, Y = e.SiteB.Value.Y }
                        : null,
                    Point1 = new PointData { X = e.Point1.X, Y = e.Point1.Y },
                    Point2 = new PointData { X = e.Point2.X, Y = e.Point2.Y }
                })
                .ToList(),

            RawColors = _cellRawColors
                .Select(c => new ColorData { R = c.R, G = c.G, B = c.B, A = c.A })
                .ToArray(),

            OverrideColors = _cellOverrideColors
                .Select(c => new ColorData { R = c.R, G = c.G, B = c.B, A = c.A })
                .ToArray()
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, _serializerOptions));
    }

    // ReSharper disable once UnusedMember.Global
    public void Load(string filePath) // TODO: Add test case for Voronoi Load()
    {
        var data = JsonSerializer.Deserialize<VoronoiSaveData>(File.ReadAllText(filePath));
        if (data == null)
            throw new InvalidDataException("Invalid Voronoi save file.");

        _width = data.Width;
        _height = data.Height;
        _boundaryMode = data.BoundaryMode;
        _points = data.Points.Select(p => new Point(p.X, p.Y)).ToArray();

        var pointLookup = _points.ToDictionary(p => (p.X, p.Y));

        _voronoiCells.Clear();
        _voronoiEdges.Clear();
        _cellVoronoiEdges.Clear();
        Array.Clear(_edgesByCell);
        _cellIndices.Clear();
        _cellsById.Clear();

        foreach (VoronoiCell cell in _voronoiCells.Values)
        {
            _cellIndices[cell.ID] = _cellIndices.Count;
            _cellsById[cell.ID] = cell;
        }

        BuildCellArray();
        BuildCellGeometry();

        foreach (CellData savedCell in data.Cells)
        {
            Point site = pointLookup[(savedCell.X, savedCell.Y)];
            var vertices = savedCell.Vertices.Select(v => new Point(v.X, v.Y)).ToList();
            _voronoiCells[site] = new VoronoiCell(site, vertices) { IsBoundary = savedCell.IsBoundary };
        }

        foreach (EdgeData edge in data.Edges)
        {
            var a = new Point(edge.Point1.X, edge.Point1.Y);
            var b = new Point(edge.Point2.X, edge.Point2.Y);
            _voronoiEdges.Add(new Edge(a, b));
        }

        foreach (CellEdgeData edge in data.CellEdges)
        {
            Point siteA = pointLookup[(edge.SiteA.X, edge.SiteA.Y)];
            Point? siteB = edge.SiteB != null ? pointLookup[(edge.SiteB.X, edge.SiteB.Y)] : null;

            _cellVoronoiEdges.Add(
                new CellVoronoiEdge(
                    siteA,
                    siteB,
                    new Point(edge.Point1.X, edge.Point1.Y),
                    new Point(edge.Point2.X, edge.Point2.Y)));
        }

        _cellRawColors = data.RawColors
            .Select(c => new Color(c.R, c.G, c.B, c.A))
            .ToArray();

        _cellOverrideColors = data.OverrideColors
            .Select(c => new Color(c.R, c.G, c.B, c.A))
            .ToArray();

        BuildEdgesByCell();
        BuildSpatialGrid();

        _isGenerated = true;
        _textureValid = false;

        UpdateTexture(
            _boundaryMode,
            VoronoiRenderingFlags.Cells,
            _previousThickness,
            _previousPointSize,
            true);
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
    private bool ShouldCullBoundarySite(Point site)
        => _boundaryMode == VoronoiBoundaryMode.Culled &&
           _voronoiCells.TryGetValue(site, out VoronoiCell? cell) &&
           (cell.IsBoundary || cell.Vertices.Count == 0);

    private bool ClipLineToBounds(Point p1, Point p2, out Point clipped1, out Point clipped2)
    {
        float x1 = p1.X;
        float y1 = p1.Y;
        float x2 = p2.X;
        float y2 = p2.Y;

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
        AddVoronoiEdge(triangle, triangle.Neighbor0, triangle.Vertices[0], triangle.Vertices[1], boundaryMode);

        // Edge 1: vertices 1 -> 2
        AddVoronoiEdge(triangle, triangle.Neighbor1, triangle.Vertices[1], triangle.Vertices[2], boundaryMode);

        // Edge 2: vertices 2 -> 0
        AddVoronoiEdge(triangle, triangle.Neighbor2, triangle.Vertices[2], triangle.Vertices[0], boundaryMode);
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
        Vector2 origin = new(triangle.Circumcenter.X, triangle.Circumcenter.Y);

        Vector2 va = new(a.X, a.Y);
        Vector2 vb = new(b.X, b.Y);

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

        Vector2 normal = new(-edge.Y, edge.X);
        normal.Normalize();
        Vector2 midpoint = (va + vb) * 0.5f;
        Point thirdPoint;
        if (triangle.Vertices[0] != a && triangle.Vertices[0] != b)
        {
            thirdPoint = triangle.Vertices[0];
        }
        else if (triangle.Vertices[1] != a && triangle.Vertices[1] != b)
        {
            thirdPoint = triangle.Vertices[1];
        }
        else
        {
            thirdPoint = triangle.Vertices[2];
        }

        Vector2 third = new(thirdPoint.X, thirdPoint.Y);

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

        if (!ClipRayAxis(origin.X, normal.X, 0f, _width, ref tMin, ref tMax))
        {
            start = default;
            end = default;
            return false;
        }

        if (!ClipRayAxis(origin.Y, normal.Y, 0f, _height, ref tMin, ref tMax))
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

    #endregion
}