using System;
using System.Diagnostics.CodeAnalysis;

namespace SKSSL.Utilities.Voronoi;

public sealed class VoronoiOptions
{
    public int SiteCount { get; init; } = 100_000;
    public int Seed { get; init; } = 12345;

    /// <summary>Weight floor for pure black pixels. Must be &gt; 0.</summary>
    public float DensityFloor { get; init; } = 0.02f;

    /// <summary>Raises normalized luminance to this exponent before weighting.</summary>
    public float DensityGamma { get; init; } = 2.0f;

    /// <summary>
    /// Separation distance as a fraction of reference spacing. Lower values
    /// allow more sites/clustering; higher values improve spacing but may make
    /// SiteCount impossible to place. Typical range: 0.45–0.65.
    /// </summary>
    public float MinimumDistanceScale { get; init; } = 0.42f;

    /// <summary>Candidate attempts allowed per requested site before throwing.</summary>
    public int MaxSamplingAttemptsPerSite { get; init; } = 400;

    /// <summary>Raster Lloyd iterations to soften extreme slivers.</summary>
    public int RelaxationIterations { get; init; } = 1;

    /// <summary>Movement toward each cell's raster centroid, in [0,1].</summary>
    public float RelaxationStrength { get; init; } = 0.25f;

    public VoronoiBoundaryMode BoundaryMode { get; init; } = VoronoiBoundaryMode.HardEdgeClosed;

    /// <summary>Randomness forwarded to a custom IPointDistributor.</summary>
    public double Randomness { get; init; } = 0.25f;

    /// <summary>Optionally and deterministically draw site-center markers into the preview.</summary>
    public DiagramOverlayOptions OverlayFlags { get; init; } = DiagramOverlayOptions.All;

    /// <summary>-1 uses the default Parallel.For worker count; otherwise use a positive limit.</summary>
    public int MaxDegreeOfParallelism { get; init; } = -1;

    /// Thickness of Edges, if they are rendered.
    public float Thickness { get; init; } = 1f;

    /// Size of Points, if they are rendered.
    public float PointSize { get; init; } = 2f;

    public float DiagramOpacity { get; init; } = 1f;
    public float OverlayOpacity { get; init; } = 1f;

    public bool RandomizeCellIds { get; init; } = false;
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