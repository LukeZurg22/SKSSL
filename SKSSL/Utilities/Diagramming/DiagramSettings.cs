using SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SKSSL.Utilities.Voronoi;

// ReSharper disable once ClassNeverInstantiated.Global
public class DiagramSettings
{
    /// Evenness of distribution on a scale of 0.00 -> 1.00; only works with the
    /// Provided custom point distributor that decides the positioning of the point X and Y positions.
    public double Randomness { get; init; } = 0.8;

    public IPointDistributor Distributor { get; init; } = new DistributorRandomJitter();
    public Voronoi.VoronoiBoundaryMode BoundaryMode { get; init; } = Voronoi.VoronoiBoundaryMode.CulledSquare;

    /// Convenient Enum toggle of various parts of a Voronoi diagram.
    public Voronoi.VoronoiRenderingFlags Flags { get; init; } = Voronoi.VoronoiRenderingFlags.Cells;

    /// Control over how cell colors are assigned.
    public Voronoi.ColorMode CellColorMode { get; init; } = Voronoi.ColorMode.Semi_Random;

    /// Thickness of Edges, if they are rendered.
    public float Thickness { get; init; } = 1f;

    /// Size of Points, if they are rendered.
    public float PointSize { get; init; } = 2f;

    /// Reference boundary (I.e. State / Regional) map.
    public ReferenceBoundarySettings? ReferenceBoundary { get; init; } = null;
}