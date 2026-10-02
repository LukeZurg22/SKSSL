using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private readonly Dictionary<uint, int> _siteIndices = [];
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
    private readonly BasicEffect _effect;
    private Texture2D _pixelMap;

    // Rendering Options
    private VoronoiBoundaryMode _boundaryMode = VoronoiBoundaryMode.CulledSquare;
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

    // WIP: All I need to do now is
    //  fix gaps,
    //  add a state-border carver,
    //  add some highlighting,
    //  ... and clean up.

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

        // Setup image data for writing. It begins as a 1x1 white pixel, but will be expanded later.
        _pixelMap = new Texture2D(_graphicsDevice, 1, 1);
        _pixelMap.SetData([Color.White]);
        _width = _graphicsDevice.Viewport.Width;
        _height = _graphicsDevice.Viewport.Height;
        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
        };

        SetDiagramProjection(Matrix.Identity, Matrix.Identity);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void SetDiagramProjection(Matrix world, Matrix view)
    {
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, _width, _height, 0f, 0f, 1f);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void SetScreenProjection(Matrix world, Matrix view)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f);
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
        uint points = DefaultPointCount,
        int? width = null,
        int? height = null,
        DiagramSettings? settings = null)
    {
        settings ??= new DiagramSettings();

        _isGenerated = false;
        _textureValid = false;
        _boundaryMode = settings.BoundaryMode;
        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;
        if (settings.Distributor is DistributorImage distributorImage)
            _imageDistributor = distributorImage;

        using DelaunayTriangulator delaunay = new();

        // Create a list of seeded points and then handle distribution using a provided distributor.
        int maxX = width.Value;
        int maxY = height.Value;
        var pointsList = delaunay.CreatePointsList(maxX, maxY, points);
        settings.Distributor.Generate(ref pointsList, points + 4, maxX, maxY, settings.Randomness);
        AssignSpatialIds(pointsList);
        _points = pointsList.ToArray();

        // Clear color storage. New sizes are +1 due to point amount being 1-based indexed.
        Array.Clear(_cellOverrideColors, 0, _cellRawColors.Length);
        Array.Clear(_cellRawColors, 0, _cellRawColors.Length);
        Array.Resize(ref _cellOverrideColors, _points.Length + 1);
        Array.Resize(ref _cellRawColors, _points.Length + 1);

        // Clear and repopulate cell index dictionary.
        _siteIndices.Clear();
        for (int i = 0; i < _points.Length; i++)
            _siteIndices[_points[i].ID] = i;

        // Make the triangles.
        var triangulation = delaunay.BowyerWatson(_points);

        // Clear old data.
        _cellsById.Clear();
        _voronoiCells.Clear();
        _voronoiEdges.Clear();
        _usedColors.Clear();
        _cellVoronoiEdges.Clear();
        _voronoiEdges.Capacity = Math.Max(_voronoiEdges.Capacity, (int)(points * 3));
        _voronoiCells.EnsureCapacity(_points.Length);

        #region DATA BUILDING

        // POPULATE CELLS
        //  -> LOYD RELAXATION
        for (int i = 0; i < 3; i++)
        {
            // Requires rebuilding the triangulation and Voronoi cells, which can be a little expensive for large graphs.
            delaunay.LloydSettlePoints(_voronoiCells.Values);
            _voronoiCells.Clear();
            _cellsById.Clear();
            triangulation = delaunay.BowyerWatson(_points);
            // --> POPULATE
            PopulateVoronoiCells(triangulation);
        }

#if DEBUG
        ValidateNeighbors(triangulation);
#endif

        ProcessCells(settings.BoundaryMode, triangulation);

        // CELL CLIPPING
        GapGeometry? gapGeometry = null;
        if (_imageDistributor is { AllowBlackGaps: true } dim) // ERR: CLIPPING IS BROKEN
        {
            //gapGeometry = BuildGapGeometry(dim.GapPolygons, dim.GapMask.Width, dim.GapMask.Height, _width, _height);
            //ClipCellsAgainstGaps(gapGeometry, _voronoiCells.Values);
        }

        BuildCellArray();


        BuildCellGeometry();
        BuildVoronoiEdges(triangulation, gapGeometry, false);

        #endregion

        #region BATCH BUILDING

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
            BuildTriangleBatch(triangulation);

        // Cells. (Star of the show.)
        if ((settings.Flags & VoronoiRenderingFlags.Cells) != 0)
        {
            BuildCellVertexBatch();
            BuildCellColorBatch([]);
        }

        #endregion

        _isGenerated = true;

        // Update the existing internal pixel map with visual changes.
        UpdateTexture(settings.BoundaryMode, settings.Flags, settings.Thickness, settings.PointSize);

        // A spatial grid, aka a bucket grid is needed to subdivide the voronoi map into workable chunks.
        // This is for performance.
        _spatialGrid = SpatialGrid.FactoryMakeBuildSpatialGrid(_voronoiCellArray, _width, _height);
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
    // ReSharper disable once MemberCanBePrivate.Global
    public bool TryGetCellAt(System.Drawing.Point position, [NotNullWhen(true)] out VoronoiCell? cell)
    {
        cell = null;
        return _spatialGrid != null && _spatialGrid.TryGetCellAt(position, out cell);
    }

    /// <summary>
    /// Variant of <see cref="TryGetCellAt(System.Drawing.Point,out SKSSL.Utilities.Voronoi.VoronoiCell?)"/>
    /// that instead outputs a found cell ID.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="cell"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public bool TryGetCellAt(System.Drawing.Point position, [NotNullWhen(true)] out uint? cell)
    {
        cell = null;
        if (!TryGetCellAt(position, out VoronoiCell? voronoiCell))
            return false;
        cell = voronoiCell.ID;
        return true;
    }

    #endregion
}