using Clipper2Lib;

namespace SKSSL.Utilities.Voronoi;

internal sealed class GapGeometry
{
    public Paths64 Paths { get; }

    public GapGeometry(Paths64 paths)
    {
        Paths = paths;
    }
}