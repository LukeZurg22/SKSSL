using System;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// A set of options dictating the generation behaviour of a Voronoi Diagram, 
/// </summary>
/// <remarks>Ensure that this class only contains SIMPLE types, for serialization convenience.</remarks>
public sealed class VoronoiOptions
{
    public int SiteCount { get; set; } = 100_000;
    public int Seed { get; set; } = 12345;

    /// <summary>Weight floor for pure black pixels. Must be &gt; 0.</summary>
    public float DensityFloor { get; set; } = 0.02f;

    /// <summary>Raises normalized luminance to this exponent before weighting.</summary>
    public float DensityGamma { get; set; } = 2.0f;

    /// <summary>
    /// Separation distance as a fraction of reference spacing. Lower values
    /// allow more sites/clustering; higher values improve spacing but may make
    /// SiteCount impossible to place. Typical range: 0.45–0.65.
    /// </summary>
    public float MinimumDistanceScale { get; set; } = 0.42f;

    /// <summary>Candidate attempts allowed per requested site before throwing.</summary>
    public int MaxSamplingAttemptsPerSite { get; set; } = 400;

    /// <summary>Raster Lloyd iterations to soften extreme slivers.</summary>
    public int RelaxationIterations { get; set; } = 1;

    /// <summary>Movement toward each cell's raster centroid, in [0,1].</summary>
    public float RelaxationStrength { get; set; } = 0.25f;

    /// <summary>Control over the diagram's culling at the boundary edge.</summary>
    public VoronoiBoundaryMode BoundaryMode { get; set; } = VoronoiBoundaryMode.HardEdgeClosed;

    /// <summary>Randomness forwarded to a custom IPointDistributor.</summary>
    public double Randomness { get; set; } = 0.25f;

    /// <summary>Optionally and deterministically draw site-center markers into the preview.</summary>
    public DiagramOverlayOptions OverlayFlags { get; set; } = DiagramOverlayOptions.SuppressAll;

    /// <summary>-1 uses the default Parallel.For worker count; otherwise use a positive limit.</summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;

    /// Size of Points, if they are rendered.
    public int PointSize { get; set; } = 2;

    public float DiagramOpacity { get; set; } = 1f;
    public float OverlayOpacity { get; set; } = 1f;
    public CellIdSortingMode CellSortingMode { get; set; } = CellIdSortingMode.Random;
    public string DiagramName { get; set; } = "VoronoiDiagram";
}

[Flags]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("Design", "CA1069:Enums values should not be duplicated")]
public enum DiagramOverlayOptions : byte
{
    //@formatter:off
    /// Enabling this flag will completely shut down all overlay rendering by default, even if other flags are enabled.
    Sites         = 1 << 0,         // 1
    Edges         = 1 << 1,         // 2
    EdgesAndSites = Edges | Sites,  // 3
    All                  = 254,
    SuppressAll          = 255,            // 255
    //@formatter:on
}