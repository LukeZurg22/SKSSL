using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    // Indexing
    private uint _firstArtificialId;
    private VoronoiCell?[] _cellsBySiteId = null!;
    private VoronoiCell[] _renderingCells;

    // Data Storage
    private readonly List<CellVoronoiEdge> _cellVoronoiEdges = [];
    private List<CellVoronoiEdge>?[] _edgesByCell = [];
    private readonly List<Edge> _voronoiEdges = []; // For Rendering the "proper" edges of each cell.

    private int[] _cellGeometryOffsets = [];
    private int[] _cellGeometryCounts = [];
    private Vector3[] _cellGeometry = [];

    // RNG
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

    // Rendering Basics
    private readonly BasicEffect _effect;

    // Rendering Options
    private VoronoiBoundaryMode _boundaryMode = VoronoiBoundaryMode.CulledSquare;
    private readonly ColorMode _cellDrawMode;
    private readonly Color _pointColor;
    private readonly Color _edgeColor;

    // Cache Flags
    private bool _isGenerated = false;

    // Authoritative map data.
    private uint[] _pixelCellIds = [];

    // Authoritative colors. Never overwritten by an override.
    private Color[] _cellRawColors = [];

    // Values to display when an override is active.
    private Color[] _cellOverrideColors = [];

    // 0 = use raw color, 1 = use override.
    private byte[] _cellOverrideMask = [];

    // Resolved palette uploaded to the GPU.
    private Color[] _cellEffectiveColors = [];

    private Texture2D? _cellPaletteTexture;

    private Effect? _mapEffect;

    private const uint InvalidCellId = uint.MaxValue;

    // Voronoi Diagram Version
    private const uint Version = 1;

    public Voronoi(
        GraphicsDevice graphicsDevice,
        ColorMode cellDrawMode = ColorMode.Semi_Deterministic_Unique,
        Color? unifiedColor = null,
        Color? pointColor = null)
    {
        _cellDrawMode = cellDrawMode;
        _graphicsDevice = graphicsDevice;
        _cellRawColors = new Color[DefaultPointCount + 1];
        _cellOverrideColors = new Color[DefaultPointCount + 1];
        _edgeColor = unifiedColor ?? Color.Gray;
        _pointColor = pointColor ?? Color.Red;

        // Setup image data for writing. It begins as a 1x1 white pixel, but will be expanded later.
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
}