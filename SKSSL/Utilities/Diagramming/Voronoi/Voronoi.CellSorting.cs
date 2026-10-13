using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using static System.Array;
using static SKSSL.Mathematics.Indexers;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities.Voronoi;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
public static class Voronoi_CellSorting
{
    public static void SortCellsBySpatialConnectivity(
        int rasterWidth,
        int rasterHeight,
        ref int[] cellIds,
        ref Vector2[] sites)
    {
        var count = sites.Length;
        if (count == 0) return;
        if (rasterWidth <= 0 || rasterHeight <= 0 || (long)rasterWidth * rasterHeight != cellIds.Length)
            throw new ArgumentException("Raster dimensions must match the cell-ID buffer.");

        // Build adjacency from shared raster edges, not site coordinates.
        int[][] neighbors = BuildRasterCellAdjacency(
            cellIds, rasterWidth, rasterHeight, count);

        // Find the first row-major pixel owned by every cell.
        var firstPixel = new int[count];
        Fill(firstPixel, int.MaxValue);

        for (int pixel = 0; pixel < cellIds.Length; pixel++)
        {
            int id = cellIds[pixel];
            if (id < count && pixel < firstPixel[id])
                firstPixel[id] = pixel;
        }

        // Start at the top-left occupied cell.
        int start = -1;
        for (int id = 0; id < count; id++)
        {
            if (firstPixel[id] == int.MaxValue) continue;
            if (start < 0 || firstPixel[id] < firstPixel[start]) start = id;
        }

        // If the raster has no valid cell IDs, use the top-left site.
        if (start < 0)
        {
            start = 0;
            for (int id = 1; id < count; id++)
                if (CompareSitePosition(id, start, ref sites) < 0)
                    start = id;
        }

        var order = new int[count];
        var visited = new bool[count];

        // Cells exposed along the boundary of the visited region.
        var frontier = new HashSet<int>();
        order[0] = start;
        visited[start] = true;
        AddFrontierNeighbors(start, neighbors, visited, frontier);
        for (int position = 1; position < count; position++)
        {
            int current = order[position - 1];

            // First preference: an actual unnumbered neighbor of the
            // immediately preceding cell. This keeps consecutive IDs
            // directly adjacent whenever possible.
            int next = FindClosestUnvisited(neighbors[current], current, visited, ref sites);

            // If the current cell is a dead end, choose the nearest
            // cell bordering ANY part of the visited region.
            if (next < 0)
            {
                next = FindClosestUnvisited(frontier, current, visited, ref sites);
            }

            // A disconnected component or a cell with no raster pixels
            // may not be present in the frontier. Start it at the
            // closest remaining site rather than putting all such cells
            // at the very end in arbitrary order.
            if (next < 0) next = FindClosestUnvisitedSite(current, visited, ref sites);
            if (next < 0) throw new InvalidOperationException("Failed to find an unvisited cell.");

            order[position] = next;
            visited[next] = true;
            frontier.Remove(next);

            AddFrontierNeighbors(next, neighbors, visited, frontier);
        }

        // order[newId] = oldId.
        // Construct the inverse permutation to remap raster ownership.
        var oldToNew = new int[count];
        var sortedSites = new Vector2[count];

        for (int newId = 0; newId < count; newId++)
        {
            int oldId = order[newId];

            oldToNew[oldId] = newId;
            sortedSites[newId] = sites[oldId];
        }

        sites = sortedSites;

        for (int pixel = 0; pixel < cellIds.Length; pixel++)
        {
            int oldId = cellIds[pixel];

            if ((uint)oldId < (uint)count)
                cellIds[pixel] = oldToNew[oldId];
        }

        // Apply oldToNew to any other arrays indexed by cell ID.
    }

    public static void SortCellsTopLeftToBottomRight_Linear_v1(int count, ref int[] cellIds, ref Vector2[] sites)
    {
        if (count == 0) return;

        var firstPixel = new int[count];
        Fill(firstPixel, int.MaxValue);

        // O(pixel count): find each cell's first row-major pixel.
        for (int pixel = 0; pixel < cellIds.Length; pixel++)
        {
            int id = cellIds[pixel];
            if ((uint)id < (uint)count && pixel < firstPixel[id]) firstPixel[id] = pixel;
        }

        // O(cell count log cell count): sort original IDs by first pixel.
        var order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;

        var vector2s = sites;
        Sort(order, (a, b) =>
        {
            int compare = firstPixel[a].CompareTo(firstPixel[b]);
            if (compare != 0) return compare;
            compare = vector2s[a].Y.CompareTo(vector2s[b].Y);
            if (compare != 0) return compare;
            compare = vector2s[a].X.CompareTo(vector2s[b].X);
            return compare != 0 ? compare : a.CompareTo(b);
        });

        var oldToNew = new int[count];
        var sortedSites = new Vector2[count];

        // Map IDs to 1..count; reorder sites into matching ID order.
        for (int newIndex = 0; newIndex < count; newIndex++)
        {
            int oldId = order[newIndex];
            oldToNew[oldId] = newIndex;
            sortedSites[newIndex] = sites[oldId];
        }

        // Replace this with copying into the appropriate backing storage
        // if _sites is a fixed-size array.
        sites = sortedSites;

        // O(pixel count): remap every rasterized cell ID.
        for (int pixel = 0; pixel < cellIds.Length; pixel++)
        {
            int oldId = cellIds[pixel];
            if ((uint)oldId < count) cellIds[pixel] = oldToNew[oldId];
        }
    }

    public static void SortCellsTopLeftToBottomRight_Circular_v1(ref int[] cellIds, ref Vector2[] sites)
    {
        int count = sites.Length;
        if (count == 0)
            return;

        // Find the bounds of all valid site positions.
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            Vector2 site = sites[i];

            if (!float.IsFinite(site.X) || !float.IsFinite(site.Y))
                continue;

            minX = Math.Min(minX, site.X);
            minY = Math.Min(minY, site.Y);
            maxX = Math.Max(maxX, site.X);
            maxY = Math.Max(maxY, site.Y);
        }

        if (!double.IsFinite(minX))
            return;

        const uint MaxCoordinate = 65535;
        double rangeX = maxX - minX;
        double rangeY = maxY - minY;

        var sortKeys = new ulong[count];
        var order = new int[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
            Vector2 site = sites[i];

            if (!float.IsFinite(site.X) || !float.IsFinite(site.Y))
            {
                // Invalid sites sort last.
                sortKeys[i] = ulong.MaxValue;
                continue;
            }

            uint x = rangeX > 0
                ? (uint)Math.Clamp(
                    Math.Round((site.X - minX) / rangeX * MaxCoordinate),
                    0d, MaxCoordinate)
                : MaxCoordinate / 2;

            uint y = rangeY > 0
                ? (uint)Math.Clamp(
                    Math.Round((site.Y - minY) / rangeY * MaxCoordinate),
                    0d, MaxCoordinate)
                : MaxCoordinate / 2;

            sortKeys[i] = HilbertIndex16(x, y);
        }

        // Order by spatial locality rather than the first occupied pixel.
        var vector2s = sites;
        Sort(order, (a, b) =>
        {
            int compare = sortKeys[a].CompareTo(sortKeys[b]);
            if (compare != 0)
                return compare;

            compare = vector2s[a].Y.CompareTo(vector2s[b].Y);
            if (compare != 0)
                return compare;

            compare = vector2s[a].X.CompareTo(vector2s[b].X);
            return compare != 0 ? compare : a.CompareTo(b);
        });

        var oldToNew = new int[count];
        var sortedSites = new Vector2[count];

        for (int newId = 0; newId < count; newId++)
        {
            int oldId = order[newId];

            oldToNew[oldId] = newId;
            sortedSites[newId] = sites[oldId];
        }

        sites = sortedSites;

        // Remap pixel ownership using exactly the same permutation.
        for (int pixel = 0; pixel < cellIds.Length; pixel++)
        {
            int oldId = cellIds[pixel];

            if ((uint)oldId < (uint)count)
                cellIds[pixel] = oldToNew[oldId];
        }

        // Remap any other arrays indexed by cell ID using oldToNew too.
    }

    public static void SortCellsHilbertByHeightAndAdjacency(
        int rasterWidth,
        int rasterHeight,
        Color[] heightPixels,
        ref int[] cellIds,
        ref Vector2[] sites,
        int localWindow = 8)
    {
        int count = sites.Length;
        if (count == 0)
            return;

        ArgumentOutOfRangeException.ThrowIfLessThan(localWindow, 1);

        if (rasterWidth <= 0 || rasterHeight <= 0 || (long)rasterWidth * rasterHeight != cellIds.Length)
            throw new ArgumentException("Raster dimensions must match the cell-ID array.");

        if (heightPixels == null || heightPixels.Length != cellIds.Length)
            throw new ArgumentException("Heightmap pixels must match the cell-ID array.", nameof(heightPixels));

        // 1. Calculate each cell's average height.
        var heightSums = new double[count];
        var pixelCounts = new int[count];

        for (int p = 0; p < cellIds.Length; p++)
        {
            int id = cellIds[p];
            if ((uint)id >= (uint)count) continue;
            heightSums[id] += heightPixels[p].R;
            pixelCounts[id]++;
        }

        var averageHeights = new float[count];
        for (int id = 0; id < count; id++)
            averageHeights[id] = pixelCounts[id] > 0 ? (float)(heightSums[id] / pixelCounts[id]) : 127.5f;

        // 2. Build raster adjacency.
        int[][] neighbors = BuildRasterCellAdjacency(cellIds, rasterWidth, rasterHeight, count);

        // 3. Find site-coordinate bounds for Hilbert normalization.
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            Vector2 site = sites[i];
            if (!float.IsFinite(site.X) || !float.IsFinite(site.Y)) continue;
            minX = Math.Min(minX, site.X);
            minY = Math.Min(minY, site.Y);
            maxX = Math.Max(maxX, site.X);
            maxY = Math.Max(maxY, site.Y);
        }

        if (!double.IsFinite(minX)) throw new InvalidOperationException("No finite site coordinates were found.");

        double rangeX = maxX - minX;
        double rangeY = maxY - minY;

        // 4. Generate the original Hilbert ordering.
        var order = new int[count];
        var keys = new ulong[count];

        for (int i = 0; i < count; i++)
        {
            order[i] = i;
            Vector2 site = sites[i];
            if (!float.IsFinite(site.X) || !float.IsFinite(site.Y))
            {
                keys[i] = ulong.MaxValue;
                continue;
            }

            uint x = rangeX > 0 ? (uint)Math.Clamp(Math.Round((site.X - minX) / rangeX * 65535.0), 0.0, 65535.0) : 0;
            uint y = rangeY > 0 ? (uint)Math.Clamp(Math.Round((site.Y - minY) / rangeY * 65535.0), 0.0, 65535.0) : 0;
            keys[i] = HilbertIndex16(x, y);
        }

        Sort(order, (a, b) =>
        {
            int comparison = keys[a].CompareTo(keys[b]);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });

        // 5. Locally refine Hilbert order.
        // Prefer adjacent cells with similar average height.
        for (int i = 1; i < count; i++)
        {
            int previousId = order[i - 1];

            int end = (int)Math.Min(count, (long)i + localWindow);
            int bestPosition = -1;
            float bestHeightDifference = float.MaxValue;

            for (int j = i; j < end; j++)
            {
                int candidateId = order[j];

                if (BinarySearch(neighbors[previousId], candidateId) < 0)
                    continue;

                float heightDifference = Math.Abs(averageHeights[previousId] - averageHeights[candidateId]);
                if (!(heightDifference < bestHeightDifference)) continue;
                bestHeightDifference = heightDifference;
                bestPosition = j;
            }

            // Swap rather than removing and reinserting to preserve a bounded, local adjustment.
            if (bestPosition >= 0) (order[i], order[bestPosition]) = (order[bestPosition], order[i]);
        }

        // 6. Build old-ID -> new-ID mapping and reorder sites.
        var oldToNew = new int[count];
        var sortedSites = new Vector2[count];
        for (int newId = 0; newId < count; newId++)
        {
            int oldId = order[newId];
            oldToNew[oldId] = newId;
            sortedSites[newId] = sites[oldId];
        }

        // 7. Remap raster cell IDs.
        for (int p = 0; p < cellIds.Length; p++)
        {
            int oldId = cellIds[p];

            if ((uint)oldId < (uint)count)
                cellIds[p] = oldToNew[oldId];
        }

        // 8. Commit the new site ordering.
        sites = sortedSites;
    }

    #region Helpers

    private static int[][] BuildRasterCellAdjacency(
        int[] cellIds,
        int width,
        int height,
        int cellCount)
    {
        var sets = new HashSet<int>?[cellCount];

        // Scan right and down: each shared edge is discovered once.
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = row + x;
                int id = cellIds[index];
                if (x + 1 < width) Connect(id, cellIds[index + 1]);
                if (y + 1 < height) Connect(id, cellIds[index + width]);
            }
        }

        var result = new int[cellCount][];

        for (int id = 0; id < cellCount; id++)
        {
            if (sets[id] is { } set)
            {
                result[id] = new int[set.Count];
                set.CopyTo(result[id]);
            }
            else result[id] = [];
        }

        return result;

        void Connect(int a, int b)
        {
            if ((uint)a >= (uint)cellCount || (uint)b >= (uint)cellCount || a == b) return;
            (sets[a] ??= []).Add(b);
            (sets[b] ??= []).Add(a);
        }
    }

    private static void AddFrontierNeighbors(int cellId, int[][] neighbors, bool[] visited, HashSet<int> frontier)
    {
        foreach (int neighbor in neighbors[cellId])
        {
            if (!visited[neighbor])
                frontier.Add(neighbor);
        }
    }

    private static int FindClosestUnvisited(IEnumerable<int> candidates, int fromCell, bool[] visited,
        ref Vector2[] sites)
    {
        int best = -1;
        double bestDistance = double.PositiveInfinity;
        Vector2 origin = sites[fromCell];
        foreach (int candidate in candidates)
        {
            if (visited[candidate])
                continue;

            double distance = SquaredDistance(origin, sites[candidate]);
            if (best < 0 ||
                distance < bestDistance ||
                (distance == bestDistance &&
                 CompareSitePosition(candidate, best, ref sites) < 0))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int FindClosestUnvisitedSite(int fromCell, bool[] visited, ref Vector2[] sites)
    {
        int best = -1;
        double bestDistance = double.PositiveInfinity;
        Vector2 origin = sites[fromCell];

        for (int candidate = 0; candidate < sites.Length; candidate++)
        {
            if (visited[candidate])
                continue;

            double distance = SquaredDistance(
                origin, sites[candidate]);

            if (best >= 0 && !(distance < bestDistance) &&
                (distance != bestDistance || CompareSitePosition(candidate, best, ref sites) >= 0))
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private static double SquaredDistance(Vector2 a, Vector2 b)
    {
        if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y))
            return double.PositiveInfinity;

        double dx = (double)a.X - b.X;
        double dy = (double)a.Y - b.Y;

        return dx * dx + dy * dy;
    }

    private static int CompareSitePosition(int a, int b, ref Vector2[] sites)
    {
        Vector2 pa = sites[a];
        Vector2 pb = sites[b];

        double ay = float.IsFinite(pa.Y) ? pa.Y : double.PositiveInfinity;
        double by = float.IsFinite(pb.Y) ? pb.Y : double.PositiveInfinity;

        int compare = ay.CompareTo(by);
        if (compare != 0)
            return compare;

        double ax = float.IsFinite(pa.X) ? pa.X : double.PositiveInfinity;
        double bx = float.IsFinite(pb.X) ? pb.X : double.PositiveInfinity;

        compare = ax.CompareTo(bx);

        return compare != 0 ? compare : a.CompareTo(b);
    }

    #endregion
}