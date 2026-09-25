using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

internal sealed class SpatialGrid
{
    public readonly List<VoronoiCell>[] Buckets;
    public readonly int Size;
    public readonly float CellWidth;
    public readonly float CellHeight;

    public SpatialGrid(List<VoronoiCell>[] buckets, int size, float cellWidth, float cellHeight)
    {
        Buckets = buckets;
        Size = size;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }
}