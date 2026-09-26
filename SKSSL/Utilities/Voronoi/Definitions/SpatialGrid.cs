using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SKSSL.Utilities.Voronoi;

internal sealed class SpatialGrid
{
    public readonly List<VoronoiCell>[] Buckets;
    private readonly int Size;
    private readonly float CellWidth;
    private readonly float CellHeight;

    public SpatialGrid(List<VoronoiCell>[] buckets, int size, float cellWidth, float cellHeight)
    {
        Buckets = buckets;
        Size = size;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
    }

    public bool TryGetCell(System.Drawing.Point position, [NotNullWhen(true)] out VoronoiCell? cell)
    {
        cell = null;
        if (Buckets.Length == 0)
            return false;

        int gridX = Math.Clamp((int)(position.X / CellWidth), 0, Size - 1);
        int gridY = Math.Clamp((int)(position.Y / CellHeight), 0, Size - 1);

        // Only cells in neighboring spatial buckets need to be tested.
        for (int y = gridY - 1; y <= gridY + 1; y++)
        {
            if (y < 0 || y >= Size)
                continue;

            for (int x = gridX - 1; x <= gridX + 1; x++)
            {
                if (x < 0 || x >= Size)
                    continue;

                var bucket = Buckets[y * Size + x];
                foreach (VoronoiCell candidate in bucket)
                {
                    if (!Voronoi.IsPointInsideCell(candidate, position.X, position.Y))
                        continue;

                    cell = candidate;
                    return true;
                }
            }
        }

        return false;
    }
}