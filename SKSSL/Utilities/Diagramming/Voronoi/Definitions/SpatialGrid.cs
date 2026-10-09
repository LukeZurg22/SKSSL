using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SKSSL.Utilities.Voronoi;

internal sealed class SpatialGrid
{
    private readonly List<VoronoiCell>[] _buckets;
    private readonly int _size;
    private readonly float _cellWidth;
    private readonly float _cellHeight;

    public SpatialGrid(List<VoronoiCell>[] buckets, int size, float cellWidth, float cellHeight)
    {
        _buckets = buckets;
        _size = size;
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
    }

    public bool TryGetCellAt(System.Drawing.Point position, [NotNullWhen(true)] out VoronoiCell? cell)
    {
        cell = null;
        if (_buckets.Length == 0)
            return false;

        int gridX = Math.Clamp((int)(position.X / _cellWidth), 0, _size - 1);
        int gridY = Math.Clamp((int)(position.Y / _cellHeight), 0, _size - 1);

        // Only cells in neighboring spatial buckets need to be tested.
        for (int y = gridY - 1; y <= gridY + 1; y++)
        {
            if (y < 0 || y >= _size)
                continue;

            for (int x = gridX - 1; x <= gridX + 1; x++)
            {
                if (x < 0 || x >= _size)
                    continue;

                var bucket = _buckets[y * _size + x];
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
    
    public static SpatialGrid FactoryMakeBuildSpatialGrid(VoronoiCell[] voronoiCellArray, int width, int height)
    {
        // ReSharper disable once PossibleLossOfFraction
        int gridSize = Math.Max(10, (int)Math.Sqrt(voronoiCellArray.Length / 4));
        float cellWidth = (float)width / gridSize;
        float cellHeight = (float)height / gridSize;
        var bucketIndices = new int[voronoiCellArray.Length];

        for (int i = 0; i < voronoiCellArray.Length; i++)
        {
            VoronoiCell cell = voronoiCellArray[i];
            int x = Math.Clamp((int)(cell.Site.X / cellWidth), 0, gridSize - 1);
            int y = Math.Clamp((int)(cell.Site.Y / cellHeight), 0, gridSize - 1);
            bucketIndices[i] = y * gridSize + x;
        }

        int bucketCount = gridSize * gridSize;
        var buckets = new List<VoronoiCell>[bucketCount];

        for (int i = 0; i < bucketCount; i++)
            buckets[i] = [];

        for (int i = 0; i < voronoiCellArray.Length; i++)
            buckets[bucketIndices[i]].Add(voronoiCellArray[i]);

        return new SpatialGrid(buckets, gridSize, cellWidth, cellHeight);
    }

}