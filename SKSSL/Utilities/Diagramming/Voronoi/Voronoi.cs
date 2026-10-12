using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using SKSSL.Utilities.Voronoi.PointDistributors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// Culling removes every Voronoi cell that owns at least one border pixel;
/// those cells become transparent. HardEdgeClosed leaves every pixel owned.
/// </summary>
public enum VoronoiBoundaryMode : byte
{
    /// <summary>Remove the entire cell if it owns any pixel on the raster border.</summary>
    Culled = 0,

    /// <summary>Every pixel in the rectangle remains assigned to a cell.</summary>
    HardEdgeClosed = 1
}

public enum VoronoiSourceMode : byte
{
    /// <summary>Generate sites according to the grayscale intensity of a height/density map.</summary>
    Heightmap,

    /// <summary>Use the configured IPointDistributor to create or reposition sites.</summary>
    CustomDistributor
}

/// <summary>
/// Raster-first MonoGame Voronoi generator. Heightmap sampling is the default source;
/// call UseDistributor to substitute an IPointDistributor. CellIds contain zero-based
/// site IDs and -1 for culled pixels. No polygon mesh is built.
/// </summary>
[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
public sealed class Voronoi : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Color? _unifiedColor;
    private readonly Color _pointColor;
    private readonly Color _edgeColor;
    private int _densityPixelsWidth;
    private int _densityPixelsHeight;
    private IPointDistributor? _distributor;
    private VoronoiBoundaryMode _lastBoundaryMode = VoronoiBoundaryMode.HardEdgeClosed;

    public VoronoiSourceMode SourceMode { get; private set; } = VoronoiSourceMode.Heightmap;
    public int Width { get; private set; }
    public int Height { get; private set; }
    private Vector2[] _sites = [];
    private int[] _cellIds = [];
    public Vector2[] Sites => _sites;
    public int[] CellIds => _cellIds;
    public int SiteCount => _sites.Length;
    public Texture2D? CellIdTexture { get; private set; }
    public Texture2D? OverlayTexture { get; private set; }

    private Color[] _encodedColors = []; // Colors per pixel
    public Color[] CellColors { get; set; } // Colors per cell
    private Color[]? _densityPixels; // Displacement
    private Color[] _overlayColors = []; // Overlay
    private bool _previewIsCurrent;
    private bool[]? _referenceBoundaryMask;

    private float[] _luminance = [];
    private float[] _weights = [];
    private double[] _cumulativeWeights = [];
    private double _totalWeight;
    private double _averageWeight;

    private double[] _sumX = [];
    private double[] _sumY = [];
    private int[] _pixelCounts = [];
    private readonly HashSet<int> _usedColors = [];
    private readonly CellColorMode _cellColorMode;
    private int _pointRadius;

    public static readonly Color DefaultEdgeColor = Color.Black;
    public static readonly Color DefaultPointColor = Color.Red;

    #region CONSTRUCTORS

    /// <summary>
    /// Construct a generator. Heightmap is the default source mode; configure a map with
    /// the constructor or UseHeightmap before GenerateDiagram. Existing color arguments are
    /// retained for compatibility with the older Voronoi constructor.
    /// </summary>
    public Voronoi(
        GraphicsDevice graphicsDevice,
        Color? unifiedColor = null,
        Color? pointColor = null,
        Texture2D? heightmap = null,
        IPointDistributor? distributor = null,
        CellColorMode cellColorMode = CellColorMode.Mixed_HashId_PureRandom)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _graphicsDevice = graphicsDevice;
        _unifiedColor = unifiedColor;
        _pointColor = pointColor ?? DefaultPointColor;
        _edgeColor = DefaultEdgeColor;
        _cellColorMode = cellColorMode;
        if (heightmap is not null)
            UseHeightmap(heightmap);
        if (distributor is not null)
            UseDistributor(distributor);
    }

    /// <summary>Convenience constructor selecting heightmap sampling immediately.</summary>
    // ReSharper disable once UnusedMember.Global
    public Voronoi(GraphicsDevice graphicsDevice, Texture2D heightmap)
        : this(graphicsDevice, unifiedColor: null, pointColor: null, heightmap: heightmap)
    {
    }

    /// <summary>Convenience constructor selecting a custom point distributor immediately.</summary>
    // ReSharper disable once UnusedMember.Global
    public Voronoi(GraphicsDevice graphicsDevice, IPointDistributor distributor)
        : this(graphicsDevice, unifiedColor: null, pointColor: null, distributor: distributor)
    {
    }

    #endregion

    #region HEIGHTMAP TOGGLES

    /// <summary>Set the source to a MonoGame height/density texture.</summary>
    public void UseHeightmap(Texture2D heightmap)
    {
        ArgumentNullException.ThrowIfNull(heightmap);
        _densityPixels = [];
        EnsureLength(ref _densityPixels, checked(heightmap.Width * heightmap.Height));
        heightmap.GetData(_densityPixels);
        _densityPixelsWidth = heightmap.Width;
        _densityPixelsHeight = heightmap.Height;
        SourceMode = VoronoiSourceMode.Heightmap;
    }

    /// <summary>
    /// Set a CPU-side height/density map. Keeping the pixels on the CPU avoids Texture2D.GetData
    /// read-backs. The input array is retained and must not be mutated during generation.
    /// </summary>
    public void UseHeightmap(Color[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height))
            throw new ArgumentException("Density pixel buffer dimensions are invalid.", nameof(pixels));
        _densityPixels = pixels;
        _densityPixelsWidth = width;
        _densityPixelsHeight = height;
        SourceMode = VoronoiSourceMode.Heightmap;
    }

    /// <summary>Switch to a custom distributor / sampler.</summary>
    public void UseDistributor(IPointDistributor distributor)
    {
        ArgumentNullException.ThrowIfNull(distributor);
        _distributor = distributor;
        SourceMode = VoronoiSourceMode.CustomDistributor;
    }

    /// <summary>Switch back to the previously configured heightmap. Throws if none is configured.</summary>
    // ReSharper disable once UnusedMember.Global
    public void UseHeightmap()
    {
        if (_densityPixels is null)
        {
            const string e =
                "No heightmap is configured. Call UseHeightmap(texture) or UseHeightmap(pixels, width, height) first.";
            throw new InvalidOperationException(e);
        }

        SourceMode = VoronoiSourceMode.Heightmap;
    }

    #endregion

    #region GENERATION

    /// <summary>
    /// Generate from the currently selected source. Output dimensions default to the current viewport.
    /// This method creates or reuses CellIdTexture and PreviewTexture; graphics-resource work must run
    /// on the graphics thread.
    /// </summary>
    public VoronoiDiagramData GenerateDiagram(
        int? width = null,
        int? height = null,
        VoronoiOptions? options = null,
        ReferenceBoundarySettings? referenceBoundary = null)
    {
        options ??= new VoronoiOptions();
        int outputWidth = width ?? _graphicsDevice.Viewport.Width;
        int outputHeight = height ?? _graphicsDevice.Viewport.Height;
        ValidateOptions(outputWidth, outputHeight, options);

        Color[]? densityPixels = null;
        int densityWidth = 0;
        int densityHeight = 0;
        if (SourceMode == VoronoiSourceMode.Heightmap)
        {
            if (_densityPixels is not null)
            {
                densityPixels = _densityPixels;
                densityWidth = _densityPixelsWidth;
                densityHeight = _densityPixelsHeight;
            }
            else
            {
                const string m = "Heightmap mode is selected but no heightmap is configured. Call UseHeightmap(...), " +
                                 "or call UseDistributor(...).";
                throw new InvalidOperationException(m);
            }
        }

        return GenerateCore(
            densityPixels, densityWidth, densityHeight,
            outputWidth, outputHeight, options, referenceBoundary);
    }

    /// <summary>Convenience overload to update the heightmap from CPU pixels and generate immediately.</summary>
    // ReSharper disable once UnusedMember.Global
    public VoronoiDiagramData GenerateDiagram(
        Color[] densityPixels,
        int densityWidth,
        int densityHeight,
        int? outputWidth = null,
        int? outputHeight = null,
        VoronoiOptions? options = null,
        ReferenceBoundarySettings? referenceBoundary = null)
    {
        UseHeightmap(densityPixels, densityWidth, densityHeight);
        return GenerateDiagram(outputWidth, outputHeight, options, referenceBoundary);
    }

    private VoronoiDiagramData GenerateCore(
        Color[]? densityPixels,
        int densityWidth,
        int densityHeight,
        int outputWidth,
        int outputHeight,
        VoronoiOptions options,
        ReferenceBoundarySettings? referenceBoundary)
    {
        if (SourceMode == VoronoiSourceMode.Heightmap)
        {
            // Ensure Density Map was actually provided, earlier.
            if (densityPixels is null)
                throw new InvalidOperationException("A density map is required in heightmap mode.");

            // Validate Density Pixels
            bool densityPixelLengthProperlySized = densityPixels.Length != checked(densityWidth * densityHeight);
            if (densityWidth <= 0 || densityHeight <= 0 || densityPixelLengthProperlySized)
                throw new ArgumentException("Density pixel buffer dimensions are invalid.", nameof(densityPixels));
            PrepareDensityField(densityPixels, options);
        }

        Width = outputWidth;
        Height = outputHeight;
        _lastBoundaryMode = options.BoundaryMode;
        _pointRadius = options.PointSize;
        EnsureLength(ref _sites, options.SiteCount);
        EnsureLength(ref _cellIds, checked(outputWidth * outputHeight));
        EnsureLength(ref _encodedColors, _cellIds.Length);
        EnsureLength(ref _sumX, options.SiteCount);
        EnsureLength(ref _sumY, options.SiteCount);
        EnsureLength(ref _pixelCounts, options.SiteCount);

        var random = new Random(options.Seed);
        switch (SourceMode)
        {
            case VoronoiSourceMode.Heightmap:
                SampleSites(options.SiteCount, outputWidth, outputHeight, options, random);
                break;
            case VoronoiSourceMode.CustomDistributor:
            default:
                SampleWithDistributor(options.SiteCount, outputWidth, outputHeight, options, random);
                break;
        }

        var rasterizer = new ExactRasterizer(outputWidth, outputHeight, Sites);
        for (int i = 0; i < options.RelaxationIterations; i++)
        {
            rasterizer.Rebuild(Sites);
            rasterizer.Rasterize(CellIds, options.MaxDegreeOfParallelism);
            RelaxSites(outputWidth, outputHeight, options.RelaxationStrength);
        }

        rasterizer.Rebuild(Sites);
        rasterizer.Rasterize(CellIds, options.MaxDegreeOfParallelism);

        // Toggle-able randomized cell IDs. Rather than state "Sort Cell IDs", I prefer to have the Cell IDs sorted by
        //  default.
        if (!options.RandomizeCellIds)
            SortCellsTopLeftToBottomRight();

        // Apply the reference mask after rasterization/ID sorting and optional border culling.
        // Cell IDs remain zero-based; -1 remains unowned. The mask also stays available
        // for overlay rendering so reference boundaries remain visible.
        _referenceBoundaryMask = referenceBoundary is null ? null : ApplyReferenceBoundary(referenceBoundary);

        // Generate one display color per site using your existing color hasher.
        CellColors = CreateCellColors(Sites.Length);

        // Fill each pixel with its owning cell's display color.
        WriteCellIdColors();
        _previewIsCurrent = false;

        // Create Output Texture References
        DiagramOverlayOptions overlayFlags = options.OverlayFlags;
        bool includeOverlay = overlayFlags != DiagramOverlayOptions.SuppressAll;
        EnsureOutputTextures(includeOverlay);
        UploadCellIdTexture(CellIdTexture!);
        if (includeOverlay)
        {
            BuildOverlayColors(Width, Height,
                overlayFlags,
                CellIds,
                Sites,
                ref _overlayColors,
                _edgeColor,
                _pointColor, _pointRadius,
                _referenceBoundaryMask
            );
            _previewIsCurrent = true;
            UploadOverlayTexture(OverlayTexture!);
        }
        else // Fallback for BuildOverlay.
        {
            OverlayTexture?.Dispose();
            OverlayTexture = null;
        }

        return new VoronoiDiagramData(
            Width, Height, Sites, CellColors, CellIds, CellIdTexture!,
            includeOverlay ? _overlayColors : null,
            SourceMode, _lastBoundaryMode, options);
    }

    #endregion


    /// <summary>
    /// Constrains existing cell ownership to connected white regions of a reference mask.
    /// Existing cell IDs and sites are retained; pixels that were unowned remain unowned.
    /// Returns the resized black mask so those reference boundaries can also be drawn in the overlay.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool[] ApplyReferenceBoundary(ReferenceBoundarySettings boundary)
    {
        int width = Width;
        int height = Height;
        int pixelCount = _cellIds.Length;
        int cellCount = _sites.Length;

        bool[] blackMask = boundary.CreateResizedBlackMask(width, height);
        int[] regions = BuildReferenceRegions(blackMask, width, height);
        int[] originalOwners = (int[])_cellIds.Clone();

        // Count each existing cell's pixels in each white region.
        var cellRegionCounts = new Dictionary<ulong, int>();
        int regionCount = 0;

        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            int region = regions[pixel];
            regionCount = Math.Max(regionCount, region + 1);

            int cellId = originalOwners[pixel];
            if (cellId < 0 || blackMask[pixel])
                continue;

            ulong key = MakeCellRegionKey(cellId, region);
            cellRegionCounts.TryGetValue(key, out int count);
            cellRegionCounts[key] = count + 1;
        }

        // Find each cell's largest white region.
        var primaryRegion = new int[cellCount];
        var primaryCount = new int[cellCount];
        Array.Fill(primaryRegion, -1);

        foreach (KeyValuePair<ulong, int> entry in cellRegionCounts)
        {
            int cellId = (int)(uint)entry.Key;
            int region = (int)(entry.Key >> 32);
            if (entry.Value <= primaryCount[cellId])
                continue;

            primaryCount[cellId] = entry.Value;
            primaryRegion[cellId] = region;
        }

        // Identify regions already served by at least one cell's primary region.
        var hasPrimaryCell = new bool[regionCount];
        for (int cellId = 0; cellId < cellCount; cellId++)
        {
            int region = primaryRegion[cellId];
            if (region >= 0)
                hasPrimaryCell[region] = true;
        }

        // Regions without a primary cell adopt their largest existing cell fragment.
        var fallbackCell = new int[regionCount];
        var fallbackCount = new int[regionCount];
        Array.Fill(fallbackCell, -1);

        foreach (KeyValuePair<ulong, int> entry in cellRegionCounts)
        {
            int cellId = (int)(uint)entry.Key;
            int region = (int)(entry.Key >> 32);
            if (hasPrimaryCell[region] || entry.Value <= fallbackCount[region])
                continue;

            fallbackCount[region] = entry.Value;
            fallbackCell[region] = cellId;
        }

        // Seed each region from pixels already owned by an eligible cell.
        var assigned = new int[pixelCount];
        Array.Fill(assigned, -1);
        var queue = new int[pixelCount];
        int head = 0;
        int tail = 0;

        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            if (blackMask[pixel])
                continue;

            int cellId = originalOwners[pixel];
            if (cellId < 0)
                continue;

            int region = regions[pixel];
            if (primaryRegion[cellId] != region && fallbackCell[region] != cellId)
                continue;

            assigned[pixel] = cellId;
            queue[tail++] = pixel;
        }

        // Propagate ownership only inside each white region.
        while (head < tail)
        {
            int pixel = queue[head++];
            int x = pixel % width;
            int region = regions[pixel];

            TryWhite(pixel - 1, x > 0);
            TryWhite(pixel + 1, x + 1 < width);
            TryWhite(pixel - width, pixel >= width);
            TryWhite(pixel + width, pixel + width < pixelCount);
            continue;

            void TryWhite(int neighbor, bool inBounds)
            {
                if (!inBounds ||
                    blackMask[neighbor] ||
                    regions[neighbor] != region ||
                    assigned[neighbor] >= 0)
                    return;

                assigned[neighbor] = assigned[pixel];
                queue[tail++] = neighbor;
            }
        }

        // Propagate assignments onto black mask pixels from their nearest side.
        // This assigns ownership for continuity, but the overlay still renders the mask black.
        head = 0;
        tail = 0;
        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            if (!blackMask[pixel] && assigned[pixel] >= 0)
                queue[tail++] = pixel;
        }

        while (head < tail)
        {
            int pixel = queue[head++];
            int x = pixel % width;
            int region = regions[pixel];

            TryBlack(pixel - 1, x > 0);
            TryBlack(pixel + 1, x + 1 < width);
            TryBlack(pixel - width, pixel >= width);
            TryBlack(pixel + width, pixel + width < pixelCount);
            continue;

            void TryBlack(int neighbor, bool inBounds)
            {
                if (!inBounds ||
                    !blackMask[neighbor] ||
                    regions[neighbor] != region ||
                    assigned[neighbor] >= 0)
                    return;

                assigned[neighbor] = assigned[pixel];
                queue[tail++] = neighbor;
            }
        }

        // Never claim pixels that were originally unowned (including culled cells).
        for (int pixel = 0; pixel < pixelCount; pixel++)
        {
            if (originalOwners[pixel] < 0)
            {
                _cellIds[pixel] = -1;
                continue;
            }

            _cellIds[pixel] = assigned[pixel] >= 0
                ? assigned[pixel]
                : originalOwners[pixel];
        }

        return blackMask;
    }

    private static ulong MakeCellRegionKey(int cellId, int regionId) =>
        ((ulong)(uint)regionId << 32) | (uint)cellId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] BuildReferenceRegions(bool[] blackMask, int width, int height)
    {
        int pixelCount = checked(width * height);
        var regionByPixel = new int[pixelCount];
        Array.Fill(regionByPixel, -1);

        var queue = new int[pixelCount];
        int regionCount = 0;

        // Flood-fill white pixels; black pixels cannot connect separate regions.
        for (int start = 0; start < pixelCount; start++)
        {
            if (blackMask[start] || regionByPixel[start] >= 0)
                continue;

            int head = 0;
            int tail = 0;
            regionByPixel[start] = regionCount;
            queue[tail++] = start;

            while (head < tail)
            {
                int pixel = queue[head++];
                int x = pixel % width;

                if (x > 0)
                {
                    int n = pixel - 1;
                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (x + 1 < width)
                {
                    int n = pixel + 1;
                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (pixel >= width)
                {
                    int n = pixel - width;
                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (pixel + width < pixelCount)
                {
                    int n = pixel + width;
                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }
            }

            regionCount++;
        }

        // An all-black reference mask has no white regions to seed from.
        if (regionCount == 0)
        {
            Array.Fill(regionByPixel, 0);
            return regionByPixel;
        }

        // Assign each black pixel to the nearest white region by multi-source BFS.
        int expansionHead = 0;
        int expansionTail = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            if (!blackMask[i])
                queue[expansionTail++] = i;
        }

        while (expansionHead < expansionTail)
        {
            int pixel = queue[expansionHead++];
            int x = pixel % width;
            int regionId = regionByPixel[pixel];

            if (x > 0)
            {
                int n = pixel - 1;
                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (x + 1 < width)
            {
                int n = pixel + 1;
                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (pixel >= width)
            {
                int n = pixel - width;
                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (pixel + width < pixelCount)
            {
                int n = pixel + width;
                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }
        }

        return regionByPixel;
    }

    #region TEXTURES

    /// <summary>
    /// Upload encoded IDs into an existing same-sized SurfaceFormat.Color texture.
    /// Create the texture once, then call this only after regenerating the map.
    /// Texture operations should run on the graphics thread.
    /// </summary>
    private void UploadCellIdTexture(Texture2D target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Width != Width || target.Height != Height)
            throw new ArgumentException("Target texture dimensions must match the generated map.", nameof(target));
        target.SetData(_encodedColors);
    }

    /// <summary>Upload a readable debug preview with filled cells and black boundaries.</summary>
    public void UploadOverlayTexture(Texture2D target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Width != Width || target.Height != Height)
            throw new ArgumentException("Target texture dimensions must match the generated map.", nameof(target));
        target.SetData(_overlayColors);
    }

    public Texture2D CreateCellIdTexture(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var texture = new Texture2D(graphicsDevice, Width, Height, false, SurfaceFormat.Color);
        UploadCellIdTexture(texture);
        return texture;
    }

    public Texture2D CreateOverlayTexture(GraphicsDevice graphicsDevice, DiagramOverlayOptions overlayOptions)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        var texture = new Texture2D(graphicsDevice, Width, Height, false, SurfaceFormat.Color);
        BuildOverlayColors(
            Width, Height, overlayOptions, CellIds, Sites,
            ref _overlayColors, _edgeColor, _pointColor, _pointRadius, _referenceBoundaryMask);
        _previewIsCurrent = true;
        UploadOverlayTexture(texture);
        return texture;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureOutputTextures(bool includeOverlay)
    {
        if (CellIdTexture is null || CellIdTexture.IsDisposed ||
            CellIdTexture.Width != Width || CellIdTexture.Height != Height)
        {
            CellIdTexture?.Dispose();
            CellIdTexture = new Texture2D(_graphicsDevice, Width, Height, false, SurfaceFormat.Color);
        }

        bool textureInvalid = OverlayTexture is null || OverlayTexture.IsDisposed;
        bool dimensionsFaulty = OverlayTexture?.Width != Width || OverlayTexture.Height != Height;
        switch (includeOverlay)
        {
            case true when textureInvalid || dimensionsFaulty:
                OverlayTexture?.Dispose();
                OverlayTexture = new Texture2D(_graphicsDevice, Width, Height, false, SurfaceFormat.Color);
                break;
            case false when OverlayTexture is not null:
                // Keep the allocated texture for reuse, but don't expose it in the result for this generation.
                break;
        }
    }

    #endregion

    private static void ValidateOptions(int outputWidth, int outputHeight, VoronoiOptions options)
    {
        if (outputWidth <= 0 || outputHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputWidth), "Output dimensions must be positive.");
        long pixelCount = (long)outputWidth * outputHeight;
        if (options.SiteCount <= 0 || options.SiteCount > pixelCount)
            throw new ArgumentOutOfRangeException(nameof(options.SiteCount),
                "SiteCount must be positive and no larger than the raster pixel count.");
        if (options.SiteCount > 0x00FF_FFFF)
            throw new ArgumentOutOfRangeException(nameof(options.SiteCount),
                "The RGB ID texture supports at most 16,777,215 sites.");
        if (options.DensityFloor <= 0f || options.DensityFloor > 1f ||
            float.IsNaN(options.DensityFloor) || float.IsInfinity(options.DensityFloor))
            throw new ArgumentOutOfRangeException(nameof(options.DensityFloor), "DensityFloor must be in (0,1].");
        if (options.DensityGamma <= 0f || float.IsNaN(options.DensityGamma) || float.IsInfinity(options.DensityGamma))
            throw new ArgumentOutOfRangeException(nameof(options.DensityGamma), "DensityGamma must be positive.");
        if (options.MinimumDistanceScale <= 0f || float.IsNaN(options.MinimumDistanceScale) ||
            float.IsInfinity(options.MinimumDistanceScale))
            throw new ArgumentOutOfRangeException(nameof(options.MinimumDistanceScale),
                "MinimumDistanceScale must be positive.");
        if (options.MaxSamplingAttemptsPerSite <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxSamplingAttemptsPerSite));
        if (options.RelaxationIterations < 0)
            throw new ArgumentOutOfRangeException(nameof(options.RelaxationIterations));
        if (options.RelaxationStrength < 0f || options.RelaxationStrength > 1f ||
            float.IsNaN(options.RelaxationStrength) || float.IsInfinity(options.RelaxationStrength))
            throw new ArgumentOutOfRangeException(nameof(options.RelaxationStrength),
                "RelaxationStrength must be in [0,1].");
        if (options.MaxDegreeOfParallelism is 0 or < -1)
            throw new ArgumentOutOfRangeException(nameof(options.MaxDegreeOfParallelism));
        if (double.IsNaN(options.Randomness) || double.IsInfinity(options.Randomness))
            throw new ArgumentOutOfRangeException(nameof(options.Randomness));
    }

    #region SAMPLING

    private static void ValidateDensityPixels(Color[] pixels, int densityWidth, int densityHeight)
    {
        if (densityWidth <= 0 || densityHeight <= 0 ||
            pixels.Length != checked(densityWidth * densityHeight))
            throw new ArgumentException("Density pixel buffer dimensions are invalid.", nameof(pixels));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PrepareDensityField(Color[] pixels, VoronoiOptions options)
    {
        int count = pixels.Length;
        EnsureLength(ref _luminance, count);
        EnsureLength(ref _weights, count);
        EnsureLength(ref _cumulativeWeights, count);

        double sum = 0;
        for (int i = 0; i < count; i++)
        {
            Color c = pixels[i];
            float luminance = (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) / 255f;
            float shaped = MathF.Pow(Math.Clamp(luminance, 0f, 1f), options.DensityGamma);
            float weight = options.DensityFloor + (1f - options.DensityFloor) * shaped;
            _luminance[i] = luminance;
            _weights[i] = weight;
            sum += weight;
            _cumulativeWeights[i] = sum;
        }

        _totalWeight = sum;
        _averageWeight = sum / count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SampleSites(int siteCount, int width, int height, VoronoiOptions options, Random random)
    {
        Array.Fill(Sites, Vector2.Zero);

        // Desired mean spacing for a uniform distribution. The local minimum
        // separation grows as density falls, approximately proportional to 1/sqrt(weight).
        float referenceSpacing = MathF.Sqrt((float)((double)width * height / siteCount));
        float maximumRadius = referenceSpacing * options.MinimumDistanceScale *
                              MathF.Sqrt((float)(_averageWeight / options.DensityFloor));
        maximumRadius = MathF.Max(maximumRadius, 1f);

        // Bin width >= any possible exclusion distance means a conflicting site can
        // only be in this bucket or one of its 8 neighbours.
        float binSize = maximumRadius;
        int gridWidth = Math.Max(1, (int)Math.Ceiling(width / (double)binSize));
        int gridHeight = Math.Max(1, (int)Math.Ceiling(height / (double)binSize));
        int[] heads = new int[checked(gridWidth * gridHeight)];
        Array.Fill(heads, -1);
        int[] next = new int[siteCount];
        float[] radii = new float[siteCount];

        long maxAttempts = (long)siteCount * options.MaxSamplingAttemptsPerSite;
        long attempts = 0;
        int accepted = 0;
        float minX = Math.Max(1e-4f, width * 1e-7f);
        float minY = Math.Max(1e-4f, height * 1e-7f);

        while (accepted < siteCount && attempts < maxAttempts)
        {
            attempts++;
            int densityIndex = SampleWeightedPixel(random);
            int tx = densityIndex % _densityPixelsWidth;
            int ty = densityIndex / _densityPixelsWidth;

            float x = (tx + (float)random.NextDouble()) / _densityPixelsWidth * width;
            float y = (ty + (float)random.NextDouble()) / _densityPixelsHeight * height;
            x = Math.Clamp(x, minX, width - minX);
            y = Math.Clamp(y, minY, height - minY);

            float radius = referenceSpacing * options.MinimumDistanceScale *
                           MathF.Sqrt((float)(_averageWeight / _weights[densityIndex]));
            // Float rounding or extreme options must not exceed the bucket search radius.
            radius = MathF.Min(radius, maximumRadius);

            int bx = Math.Clamp((int)(x / binSize), 0, gridWidth - 1);
            int by = Math.Clamp((int)(y / binSize), 0, gridHeight - 1);
            bool blocked = false;

            for (int gy = Math.Max(0, by - 1); gy <= Math.Min(gridHeight - 1, by + 1) && !blocked; gy++)
            for (int gx = Math.Max(0, bx - 1); gx <= Math.Min(gridWidth - 1, bx + 1) && !blocked; gx++)
            {
                int bucket = gy * gridWidth + gx;
                for (int other = heads[bucket]; other != -1; other = next[other])
                {
                    float dx = x - Sites[other].X;
                    float dy = y - Sites[other].Y;
                    float required = MathF.Max(radius, radii[other]);
                    if (!(dx * dx + dy * dy < required * required))
                        continue;

                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            Sites[accepted] = new Vector2(x, y);
            radii[accepted] = radius;
            int ownBucket = by * gridWidth + bx;
            next[accepted] = heads[ownBucket];
            heads[ownBucket] = accepted;
            accepted++;
        }

        if (accepted != siteCount)
        {
            throw new InvalidOperationException(
                $"Placed {accepted:N0} of {siteCount:N0} sites after {attempts:N0} candidate attempts. " +
                "Reduce MinimumDistanceScale (for example to 0.45), lower the target site count, or increase MaxSamplingAttemptsPerSite.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SampleWithDistributor(int siteCount, int width, int height, VoronoiOptions options, Random random)
    {
        if (_distributor is null)
            throw new InvalidOperationException(
                "Custom-distributor mode is selected but no IPointDistributor is configured. Call UseDistributor(...).");

        // Preserve the existing IPointDistributor convention: start with an initial site list,
        // then let the distributor reposition/replace it. It must return exactly SiteCount points.
        var points = new List<Vector2>(siteCount);
        float maxX = Math.Max(0.001f, width - 0.001f);
        float maxY = Math.Max(0.001f, height - 0.001f);
        for (int i = 0; i < siteCount; i++)
            points.Add(new Vector2((float)random.NextDouble() * maxX, (float)random.NextDouble() * maxY));

        _distributor.Generate(ref points, checked((uint)siteCount), width, height, options.Randomness);
        if (points is null || points.Count != siteCount)
            throw new InvalidOperationException(
                $"The configured IPointDistributor must return exactly {siteCount:N0} points; it returned {points?.Count ?? 0:N0}.");

        for (int i = 0; i < siteCount; i++)
        {
            Vector2 p = points[i];
            if (float.IsNaN(p.X) || float.IsInfinity(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.Y))
                throw new InvalidOperationException(
                    $"The configured IPointDistributor returned a non-finite point at index {i}.");
            _sites[i] = new Vector2(Math.Clamp(p.X, 0.001f, maxX), Math.Clamp(p.Y, 0.001f, maxY));
        }
    }

    private int SampleWeightedPixel(Random random)
    {
        double target = random.NextDouble() * _totalWeight;
        int lo = 0;
        int hi = _cumulativeWeights.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (_cumulativeWeights[mid] < target) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    #endregion

    private void RelaxSites(int width, int height, float strength)
    {
        if (strength <= 0f)
            return;

        Array.Clear(_sumX, 0, _sumX.Length);
        Array.Clear(_sumY, 0, _sumY.Length);
        Array.Clear(_pixelCounts, 0, _pixelCounts.Length);

        // CellIds are from the full closed diagram here (culling occurs only afterward).
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            double py = y + 0.5;
            for (int x = 0; x < width; x++)
            {
                var id = CellIds[row + x];
                _sumX[id] += x + 0.5;
                _sumY[id] += py;
                _pixelCounts[id]++;
            }
        }

        for (int id = 0; id < Sites.Length; id++)
        {
            int count = _pixelCounts[id];
            if (count == 0)
                continue;

            Vector2 site = Sites[id];
            float cx = (float)(_sumX[id] / count);
            float cy = (float)(_sumY[id] / count);
            float nx = site.X + (cx - site.X) * strength;
            float ny = site.Y + (cy - site.Y) * strength;
            Sites[id] = new Vector2(
                Math.Clamp(nx, 0.001f, Math.Max(0.001f, width - 0.001f)),
                Math.Clamp(ny, 0.001f, Math.Max(0.001f, height - 0.001f)));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CullBorderCells()
    {
        var cull = new bool[Sites.Length];
        int w = Width;
        int h = Height;

        for (int x = 0; x < w; x++)
        {
            Mark(CellIds[x]);
            Mark(CellIds[(h - 1) * w + x]);
        }

        for (int y = 0; y < h; y++)
        {
            Mark(CellIds[y * w]);
            Mark(CellIds[y * w + (w - 1)]);
        }

        for (int i = 0; i < CellIds.Length; i++)
        {
            if (cull[CellIds[i]])
                CellIds[i] = -1;
        }

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Mark(int id) => cull[id] = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SortCellsTopLeftToBottomRight()
    {
        int count = _sites.Length;
        if (count == 0) return;

        var firstPixel = new int[count];
        Array.Fill(firstPixel, int.MaxValue);

        // O(pixel count): find each cell's first row-major pixel.
        for (int pixel = 0; pixel < _cellIds.Length; pixel++)
        {
            int id = _cellIds[pixel];
            if ((uint)id < (uint)count && pixel < firstPixel[id]) firstPixel[id] = pixel;
        }

        // O(cell count log cell count): sort original IDs by first pixel.
        var order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;

        Array.Sort(order, (a, b) =>
        {
            int compare = firstPixel[a].CompareTo(firstPixel[b]);
            if (compare != 0) return compare;
            compare = _sites[a].Y.CompareTo(_sites[b].Y);
            if (compare != 0) return compare;
            compare = _sites[a].X.CompareTo(_sites[b].X);
            return compare != 0 ? compare : a.CompareTo(b);
        });

        var oldToNew = new int[count];
        var sortedSites = new Vector2[count];

        // Map IDs to 1..count; reorder sites into matching ID order.
        for (int newIndex = 0; newIndex < count; newIndex++)
        {
            int oldId = order[newIndex];
            oldToNew[oldId] = newIndex;
            sortedSites[newIndex] = _sites[oldId];
        }

        // Replace this with copying into the appropriate backing storage
        // if _sites is a fixed-size array.
        _sites = sortedSites;

        // O(pixel count): remap every rasterized cell ID.
        for (int pixel = 0; pixel < _cellIds.Length; pixel++)
        {
            int oldId = _cellIds[pixel];
            if ((uint)oldId < (uint)count) _cellIds[pixel] = oldToNew[oldId];
        }
    }

    #region COLORS

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteCellIdColors()
    {
        for (int i = 0; i < CellIds.Length; i++)
        {
            int cellId = CellIds[i];
            _encodedColors[i] = cellId < 0 ? Color.Transparent : CellColors[cellId];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color[] CreateCellColors(int cellCount)
    {
        lock (_usedColors) _usedColors.Clear();
        var colors = new Color[cellCount];
        for (int cellId = 0; cellId < cellCount; cellId++)
            colors[cellId] = ColorForId(0, 0, (uint)cellId);
        return colors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildOverlayColors(int w, int h,
        DiagramOverlayOptions showSiteMarkers,
        int[] cellIds,
        Vector2[] sites,
        ref Color[] overlayColors,
        Color edgeColor,
        Color pointColor,
        int pointRadius,
        bool[]? referenceBoundaryMask)
    {
        EnsureLength(ref overlayColors, cellIds.Length);
        
        if ((showSiteMarkers & DiagramOverlayOptions.Edges) != 0)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                int id = cellIds[idx];
                bool referenceEdge = referenceBoundaryMask is not null && referenceBoundaryMask[idx];

                // Keep reference-mask boundaries visible even when a pixel is unowned
                // (for example, when the Voronoi border-culling option removed its cell).
                if (id < 0)
                {
                    overlayColors[idx] = referenceEdge ? edgeColor : Color.Transparent;
                    continue;
                }

                // Build edges from neighboring cell IDs, while also preserving the reference mask.
                bool edge = referenceEdge || x == 0 || y == 0 || x == w - 1 || y == h - 1;
                if (!edge && cellIds[idx - 1] != id) edge = true;
                if (!edge && cellIds[idx + 1] != id) edge = true;
                if (!edge && cellIds[idx - w] != id) edge = true;
                if (!edge && cellIds[idx + w] != id) edge = true;

                overlayColors[idx] = edge ? edgeColor : Color.Transparent;
            }
        }

        // Build Sites
        if ((showSiteMarkers & DiagramOverlayOptions.Sites) != 0)
        {
            int radius = Math.Max(0, pointRadius);

            foreach (Vector2 site in sites)
            {
                int sx = Math.Clamp((int)site.X, 0, w - 1);
                int sy = Math.Clamp((int)site.Y, 0, h - 1);

                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        // Keep the marker circular.
                        if (dx * dx + dy * dy > radius * radius)
                            continue;

                        int x = sx + dx;
                        int y = sy + dy;

                        if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                            continue;

                        overlayColors[y * w + x] = pointColor;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color ColorForId(float x, float y, uint id)
    {
        if (_unifiedColor.HasValue)
            return _unifiedColor.Value;

        Color color;
        uint hash;
        byte r, g, b;
        Random shared = Random.Shared;
        switch (_cellColorMode)
        {
            case CellColorMode.Deterministic_Hash_position:
                hash = (uint)x + (uint)y;
                hash ^= hash >> 16;
                r = (byte)hash;
                hash ^= hash >> 15;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                return color;
            case CellColorMode.Deterministic_Hash_id:
                return GetDeterministicColorFromId(id);
            case CellColorMode.Deterministic_HashRandom: // Throw together a lazy hash based on cell Site vertex.
                hash = (uint)x ^ (uint)y;
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
            case CellColorMode.Random: // Truly random 0 -> 255
                color = new Color(shared.Next(0, 256), shared.Next(0, 256), shared.Next(0, 256), 255);
                return color;
            case CellColorMode.Mixed_HashId_PureRandom:
                // Grab a deterministic semi-unique color and go an integer check.
                // Naturally if it isn't unique, then it is reaching the birthday-paradox point
                color = GetDeterministicColorFromId(id);
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
                    if (!_usedColors.Add(reversed)) goto case CellColorMode.Pure_Random;
                }

                return color;
            case CellColorMode.Pure_Random: // Pure random RGB – three integer ops, no floats
                int rgb;
                lock (shared)
                lock (_usedColors)
                {
                    do rgb = shared.Next(0x1000000); // 0 … 16 777 215
                    while (!_usedColors.Add(rgb));
                }

                color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 255);
                break;
            case CellColorMode.Encoded:
                color = id.EncodeAsColor();
                break;
            default:
                color = _unifiedColor ?? Color.Gray;
                break;
        }

        return color;
    }

    private static Color GetDeterministicColorFromId(uint id)
    {
        var uih = id + 0x9E3779B9u;
        uih ^= uih >> 16;
        uih *= 0x7FEB352Du;
        uih ^= uih >> 15;
        uih *= 0x846CA68Bu;
        uih ^= uih >> 16;
        return new Color(
            (byte)(64 + (uih & 0x7F)),
            (byte)(64 + ((uih >> 8) & 0x7F)),
            (byte)(64 + ((uih >> 16) & 0x7F)),
            (byte)255
        );
    }

    #endregion

    public void Dispose()
    {
        CellIdTexture?.Dispose();
        OverlayTexture?.Dispose();
        CellIdTexture = null;
        OverlayTexture = null;
    }

    private static void EnsureLength<T>(ref T[] array, int length)
    {
        if (array.Length != length)
            array = new T[length];
    }

    /// <summary>Exact nearest-site rasterizer accelerated by a uniform spatial grid.</summary>
    private sealed class ExactRasterizer
    {
        private readonly int _width;
        private readonly int _height;
        private readonly float _binSize;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly int[] _heads;
        private readonly int[] _next;
        private Vector2[] _sites;

        public ExactRasterizer(int width, int height, Vector2[] sites)
        {
            _width = width;
            _height = height;
            _sites = sites;

            // Around one site per bin on average. Sparse areas simply search more bins.
            _binSize = MathF.Max(1f, MathF.Sqrt((float)((double)width * height / sites.Length)));
            _gridWidth = Math.Max(1, (int)Math.Ceiling(width / (double)_binSize));
            _gridHeight = Math.Max(1, (int)Math.Ceiling(height / (double)_binSize));
            _heads = new int[checked(_gridWidth * _gridHeight)];
            _next = new int[sites.Length];
            // Rebuild is called before every raster pass.
        }

        public void Rebuild(Vector2[] sites)
        {
            _sites = sites;
            Array.Fill(_heads, -1);
            for (int i = 0; i < sites.Length; i++)
            {
                Vector2 p = sites[i];
                int bx = Math.Clamp((int)(p.X / _binSize), 0, _gridWidth - 1);
                int by = Math.Clamp((int)(p.Y / _binSize), 0, _gridHeight - 1);
                int bucket = by * _gridWidth + bx;
                _next[i] = _heads[bucket];
                _heads[bucket] = i;
            }
        }

        public void Rasterize(int[] destination, int maxDegreeOfParallelism)
        {
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            Parallel.For(0, _height, parallel, y =>
            {
                int row = y * _width;
                float py = y + 0.5f;
                for (int x = 0; x < _width; x++)
                    destination[row + x] = FindNearest(x + 0.5f, py);
            });
        }

        private int FindNearest(float px, float py)
        {
            int bx = Math.Clamp((int)(px / _binSize), 0, _gridWidth - 1);
            int by = Math.Clamp((int)(py / _binSize), 0, _gridHeight - 1);
            int bestId = -1;
            float bestDistanceSquared = float.PositiveInfinity;
            int maxRings = Math.Max(_gridWidth, _gridHeight);

            for (int ring = 0; ring <= maxRings; ring++)
            {
                if (ring == 0)
                {
                    ScanBucket(bx, by, px, py, ref bestId, ref bestDistanceSquared);
                }
                else
                {
                    int left = bx - ring;
                    int right = bx + ring;
                    int top = by - ring;
                    int bottom = by + ring;
                    int fromX = Math.Max(0, left);
                    int toX = Math.Min(_gridWidth - 1, right);
                    //int fromY = Math.Max(0, top);
                    //int toY = Math.Min(_gridHeight - 1, bottom);

                    // Top and bottom rows of the new perimeter.
                    if (top >= 0)
                        for (int gx = fromX; gx <= toX; gx++)
                            ScanBucket(gx, top, px, py, ref bestId, ref bestDistanceSquared);
                    if (bottom < _gridHeight && bottom != top)
                        for (int gx = fromX; gx <= toX; gx++)
                            ScanBucket(gx, bottom, px, py, ref bestId, ref bestDistanceSquared);

                    // Left/right columns excluding corners already scanned above.
                    int fromSideY = Math.Max(0, top + 1);
                    int toSideY = Math.Min(_gridHeight - 1, bottom - 1);
                    if (left >= 0)
                        for (int gy = fromSideY; gy <= toSideY; gy++)
                            ScanBucket(left, gy, px, py, ref bestId, ref bestDistanceSquared);
                    if (right < _gridWidth && right != left)
                        for (int gy = fromSideY; gy <= toSideY; gy++)
                            ScanBucket(right, gy, px, py, ref bestId, ref bestDistanceSquared);
                }

                // Lower bound on the distance to any site in an unsearched bin.
                // Stop only when the current winner is strictly closer than this bound.
                float lowerBound = DistanceToUnsearchedBins(px, py, bx, by, ring);
                if (bestDistanceSquared < lowerBound * lowerBound)
                    break;
            }

            if (bestId >= 0) return bestId;
            throw new InvalidOperationException("Rasterizer could not find a site; the site array is empty.");
        }

        private float DistanceToUnsearchedBins(float px, float py, int bx, int by, int ring)
        {
            int minX = Math.Max(0, bx - ring);
            int maxX = Math.Min(_gridWidth - 1, bx + ring);
            int minY = Math.Max(0, by - ring);
            int maxY = Math.Min(_gridHeight - 1, by + ring);
            float result = float.PositiveInfinity;
            if (minX > 0) result = MathF.Min(result, px - minX * _binSize);
            if (maxX < _gridWidth - 1)
            {
                float rightEdge = MathF.Min(_width, (maxX + 1) * _binSize);
                result = MathF.Min(result, rightEdge - px);
            }

            if (minY > 0) result = MathF.Min(result, py - minY * _binSize);
            if (maxY >= _gridHeight - 1) return result;
            float bottomEdge = MathF.Min(_height, (maxY + 1) * _binSize);
            result = MathF.Min(result, bottomEdge - py);
            return result;
        }

        private void ScanBucket(int bx, int by, float px, float py, ref int bestId, ref float bestDistanceSquared)
        {
            int bucket = by * _gridWidth + bx;
            for (int id = _heads[bucket]; id != -1; id = _next[id])
            {
                Vector2 site = _sites[id];
                float dx = px - site.X;
                float dy = py - site.Y;
                float d2 = dx * dx + dy * dy;
                if (!(d2 < bestDistanceSquared) &&
                    (!(Math.Abs(d2 - bestDistanceSquared) < 0.001) || (id >= bestId)))
                    continue;
                bestDistanceSquared = d2;
                bestId = id;
            }
        }
    }
}

public enum CellColorMode
{
    Deterministic_Hash_position,
    Deterministic_HashRandom,
    Random,

    /// <summary>
    /// Deterministically generate a color from an integer.
    /// </summary>
    /// <remarks>
    /// The same ID always produces the same color. Colors are not
    /// mathematically guaranteed to be unique because the 32-bit hash
    /// is reduced to 24 bits of RGB color.
    /// </remarks>
    /// <returns>A deterministic <see cref="Color"/> derived from the ID.</returns>
    Mixed_HashId_PureRandom,
    Pure_Random,

    /// Stable integer hash; avoids a Random call per cell and is reproducible.
    Deterministic_Hash_id,
    Encoded
}