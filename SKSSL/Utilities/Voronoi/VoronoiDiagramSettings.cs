using SKSSL.Utilities.Voronoi.PointDistributors;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DiagramSettings
    {
        /// Toggle for easing points to a settled arrangement.
        public bool SettlePoints { get; set; } = false;

        ///     Evenness of distribution on a scale of 0.00 -> 1.00; only works with the
        ///     Provided custom point distributor that decides the positioning of the point X and Y positions.
        public double Randomness { get; set; } = 0.8;

        public IPointDistributor Distributor { get; set; } = new DistributorRandomJitter();
        public VoronoiBoundaryMode BoundaryMode { get; set; } = VoronoiBoundaryMode.Culled;

        /// Convenient Enum toggle of various parts of a Voronoi diagram.
        public VoronoiRenderingFlags Flags { get; set; } = VoronoiRenderingFlags.Cells;

        /// Thickness of Edges, if they are rendered.
        public float Thickness { get; set; } = 1f;

        /// Size of Points, if they are rendered.
        public float PointSize { get; set; } = 2f;
    }
}