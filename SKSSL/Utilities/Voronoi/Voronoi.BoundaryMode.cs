namespace SKSSL.Utilities.Voronoi;

// ReSharper disable UnusedMember.Global
public partial class Voronoi
{
    public enum VoronoiBoundaryMode : byte
    {
        CulledSquare,
        CulledCircular,
        HardEdgeOpen,
        HardEdgeClosed
    }
}