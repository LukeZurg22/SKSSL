using System;
using System.Collections.Generic;
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
        uint points = DefaultPointCount,
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
        _voronoiEdges.Capacity = Math.Max(_voronoiEdges.Capacity, (int)(points * 3));
        _voronoiCells.EnsureCapacity(_points.Length);

        // POPULATE

        #region Populate Cells

        // Lloyd relaxation requires rebuilding the triangulation and Voronoi cells, which can be a little expensive
        //  for large graphs.
        for (int i = 0; i < 3; i++)
        {
            delaunay.LloydSettlePoints(_voronoiCells.Values);

            _voronoiCells.Clear();
            _cellsById.Clear();

            triangulation = delaunay.BowyerWatson(_points);

            PopulateVoronoiCells();
        }


#if DEBUG
        ValidateNeighbors(triangulation);
#endif

        #endregion

        // PROCESS
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
        _spatialGrid = SpatialGrid.FactoryMakeBuildSpatialGrid(_voronoiCellArray, _width, _height);
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

        if (boundaryMode == VoronoiBoundaryMode.HardEdgeClosed)
        {
            // Build the Delaunay-neighbor graph for the REAL sites only.
            var neighbors = new Dictionary<Point, HashSet<Point>>(_voronoiCells.Count);
            foreach (Point point in _points)
            {
                if (!IsArtificialBoundarySite(point))
                    neighbors[point] = [];
            }

            foreach (Triangle triangle in triangulation)
            {
                Point a = triangle.Vertices[0];
                Point b = triangle.Vertices[1];
                Point c = triangle.Vertices[2];

                // Every pair of real points in a Delaunay triangle is a
                // Delaunay neighbor, including pairs from triangles that
                // also contain an artificial boundary point.
                if (!IsArtificialBoundarySite(a) && !IsArtificialBoundarySite(b))
                {
                    neighbors[a].Add(b);
                    neighbors[b].Add(a);
                }

                if (!IsArtificialBoundarySite(a) && !IsArtificialBoundarySite(c))
                {
                    neighbors[a].Add(c);
                    neighbors[c].Add(a);
                }

                if (!IsArtificialBoundarySite(b) && !IsArtificialBoundarySite(c))
                {
                    neighbors[b].Add(c);
                    neighbors[c].Add(b);
                }
            }

            Parallel.ForEach(_voronoiCells.Values, cell =>
            {
                if (IsArtificialBoundarySite(cell.Site))
                {
                    cell.Vertices.Clear();
                    cell.IsCulled = true;
                    return;
                }

                // Start with the entire screen.
                var polygon = new List<Point>(4)
                    { new(0, 0), new(_width, 0), new(_width, _height), new(0, _height) };

                Point site = cell.Site;

                // Intersect the screen with the half-plane
                // containing points closer to `site` than to each neighbor.
                foreach (Point neighbor in neighbors[site])
                {
                    if (polygon.Count < 3)
                        break;

                    double dx = neighbor.X - site.X;
                    double dy = neighbor.Y - site.Y;

                    double c = (double)neighbor.X * neighbor.X +
                               (double)neighbor.Y * neighbor.Y -
                               (double)site.X * site.X -
                               (double)site.Y * site.Y;

                    double a = 2.0 * dx;
                    double b = 2.0 * dy;

                    var clipped = new List<Point>(polygon.Count + 2);
                    Point previous = polygon[^1];
                    double previousValue = a * previous.X + b * previous.Y - c;
                    bool previousInside = previousValue <= 0.0;
                    foreach (Point current in polygon)
                    {
                        double currentValue = a * current.X + b * current.Y - c;
                        bool currentInside = currentValue <= 0.0;
                        if (currentInside)
                        {
                            if (!previousInside)
                            {
                                double denominator = previousValue - currentValue;
                                if (Math.Abs(denominator) > 1e-12)
                                {
                                    double t = previousValue / denominator;
                                    clipped.Add(new Point(
                                        (int)Math.Round(previous.X + (current.X - previous.X) * t),
                                        (int)Math.Round(previous.Y + (current.Y - previous.Y) * t))
                                    );
                                }
                            }

                            clipped.Add(current);
                        }
                        else if (previousInside)
                        {
                            double denominator =
                                previousValue - currentValue;

                            if (Math.Abs(denominator) > 1e-12)
                            {
                                double t = previousValue / denominator;
                                clipped.Add(new Point(
                                    (int)Math.Round(previous.X + (current.X - previous.X) * t),
                                    (int)Math.Round(previous.Y + (current.Y - previous.Y) * t))
                                );
                            }
                        }

                        previous = current;
                        previousValue = currentValue;
                        previousInside = currentInside;
                    }

                    polygon = clipped
                        .Distinct()
                        .ToList();
                }

                cell.Vertices = polygon;
                cell.IsBoundary = false;
                cell.IsCulled = polygon.Count < 3;

                if (!cell.IsCulled)
                {
                    _cellRawColors[_cellIndices[cell.ID]] =
                        GetCellColor(cell);
                }
            });

            return;
        }

        double centerX = _width * 0.5;
        double centerY = _height * 0.5;
        double radiusX = _width * 0.48;
        double radiusY = _height * 0.48;

        // Existing behavior for Culled / HardEdgeOpen.
        Parallel.ForEach(_voronoiCells.Values, cell =>
        {
            cell.Vertices = cell.Vertices
                .Distinct()
                .ToList();

            SortVerticesAround(cell.Vertices, cell.Site);

            bool touchesOutside;
            int padding = 0;

            // CULL CIRCULAR
            if (boundaryMode == VoronoiBoundaryMode.CulledCircular)
            {
                touchesOutside = cell.Vertices.Any(v =>
                {
                    double dx = (v.X - centerX) / radiusX;
                    double dy = (v.Y - centerY) / radiusY;
                    return dx * dx + dy * dy > 1.0;
                });
            }
            // CULL SQUARE
            else
            {
                touchesOutside = cell.Vertices.Any(v =>
                    v.X < -padding ||
                    v.X > _width + padding ||
                    v.Y < -padding ||
                    v.Y > _height + padding);
            }

            if (touchesOutside)
            {
                switch (boundaryMode)
                {
                    case VoronoiBoundaryMode.CulledCircular:
                    case VoronoiBoundaryMode.CulledSquare:
                        cell.Vertices.Clear();
                        break;
                    case VoronoiBoundaryMode.HardEdgeOpen:
                    default:
                        // TODO: This code is sketchy. May need adjustment.
                        cell.Vertices = ClipPolygonToBounds(cell.Vertices, _width, _height);
                        break;
                    case VoronoiBoundaryMode.HardEdgeClosed:
                        throw new Exception("Should not have reached HardEdgeClosed when case has been covered.");
                }
            }

            _cellRawColors[_cellIndices[cell.ID]] =
                GetCellColor(cell);

            cell.IsCulled =
                IsArtificialBoundarySite(cell.Site) ||
                (boundaryMode == VoronoiBoundaryMode.CulledSquare &&
                 (cell.IsBoundary ||
                  cell.Vertices.Count == 0));
        });
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