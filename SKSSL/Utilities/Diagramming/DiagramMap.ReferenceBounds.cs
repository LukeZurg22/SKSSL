using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private readonly int[] _renderCellIdByPixel;
    private Dictionary<ulong, int> _renderIdByCellRegion;
    private int[] _renderParentCellId;

    private static ulong MakeCellRegionKey(int cellId, int regionId) => ((ulong)(uint)regionId << 32) | (uint)cellId;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void ApplyReferenceBoundary(ReferenceBoundarySettings boundary)
{
    int width = _pixelMap.Width;
    int pixelCount = _cellIdByPixel.Length;
    int cellCount = _cellRawColors.Length;

    bool[] blackMask = boundary.CreateResizedBlackMask(
        width, _pixelMap.Height);

    int[] regions = BuildReferenceRegions(
        blackMask, width, _pixelMap.Height);

    int[] originalOwners = (int[])_cellIdByPixel.Clone();

    // Count each existing cell's pixels in each white region.
    var cellRegionCounts = new Dictionary<ulong, int>();
    int regionCount = 0;

    for (int pixel = 0; pixel < pixelCount; pixel++)
    {
        int region = regions[pixel];
        regionCount = Math.Max(regionCount, region + 1);

        int cellId = originalOwners[pixel];

        if (cellId < 0 || blackMask[pixel])
            continue;

        ulong key = MakeCellRegionKey(cellId, region);

        cellRegionCounts.TryGetValue(key, out int count);
        cellRegionCounts[key] = count + 1;
    }

    // Find each cell's largest region.
    var primaryRegion = new int[cellCount];
    var primaryCount = new int[cellCount];
    Array.Fill(primaryRegion, -1);

    foreach (KeyValuePair<ulong, int> entry in cellRegionCounts)
    {
        int cellId = (int)(uint)entry.Key;
        int region = (int)(entry.Key >> 32);

        if (entry.Value <= primaryCount[cellId])
            continue;

        primaryCount[cellId] = entry.Value;
        primaryRegion[cellId] = region;
    }

    // Determine which regions have eligible cells.
    var hasPrimaryCell = new bool[regionCount];

    for (int cellId = 0; cellId < cellCount; cellId++)
    {
        int region = primaryRegion[cellId];

        if (region >= 0)
            hasPrimaryCell[region] = true;
    }

    // If a region has no primary cell, use its largest existing fragment.
    // This creates no new cell IDs and fills no previously unowned pixels.
    var fallbackCell = new int[regionCount];
    var fallbackCount = new int[regionCount];
    Array.Fill(fallbackCell, -1);

    foreach (KeyValuePair<ulong, int> entry in cellRegionCounts)
    {
        int cellId = (int)(uint)entry.Key;
        int region = (int)(entry.Key >> 32);

        if (hasPrimaryCell[region] ||
            entry.Value <= fallbackCount[region])
        {
            continue;
        }

        fallbackCount[region] = entry.Value;
        fallbackCell[region] = cellId;
    }

    // Allowed cell/region combinations. Values remain original cell IDs.
    var regionCellIds = new Dictionary<ulong, int>();

    for (int cellId = 0; cellId < cellCount; cellId++)
    {
        int region = primaryRegion[cellId];

        if (region >= 0)
            regionCellIds[MakeCellRegionKey(cellId, region)] = cellId;
    }

    for (int region = 0; region < regionCount; region++)
    {
        int cellId = fallbackCell[region];

        if (cellId >= 0)
            regionCellIds[MakeCellRegionKey(cellId, region)] = cellId;
    }

    // Calculate nearest eligible cell ownership.
    // This assignment buffer can cover empty pixels, but those pixels
    // are never committed as owned cells.
    var assigned = new int[pixelCount];
    Array.Fill(assigned, -1);

    var queue = new int[pixelCount];
    int head = 0;
    int tail = 0;

    // Seed only white pixels that already belong to an eligible cell.
    for (int pixel = 0; pixel < pixelCount; pixel++)
    {
        if (blackMask[pixel])
            continue;

        int cellId = originalOwners[pixel];

        if (cellId < 0)
            continue;

        int region = regions[pixel];

        if (primaryRegion[cellId] != region &&
            fallbackCell[region] != cellId)
        {
            continue;
        }

        assigned[pixel] = cellId;
        queue[tail++] = pixel;
    }

    // Propagate ownership only within each white region.
    while (head < tail)
    {
        int pixel = queue[head++];
        int x = pixel % width;
        int region = regions[pixel];

        TryWhite(pixel - 1, x > 0);
        TryWhite(pixel + 1, x + 1 < width);
        TryWhite(pixel - width, pixel >= width);
        TryWhite(pixel + width, pixel + width < pixelCount);

        void TryWhite(int neighbor, bool inBounds)
        {
            if (!inBounds ||
                blackMask[neighbor] ||
                regions[neighbor] != region ||
                assigned[neighbor] >= 0)
            {
                return;
            }

            assigned[neighbor] = assigned[pixel];
            queue[tail++] = neighbor;
        }
    }

    // Assign black boundary pixels from their nearest side.
    // Only black pixels are traversed in this phase.
    head = 0;
    tail = 0;

    for (int pixel = 0; pixel < pixelCount; pixel++)
    {
        if (!blackMask[pixel] && assigned[pixel] >= 0)
            queue[tail++] = pixel;
    }

    while (head < tail)
    {
        int pixel = queue[head++];
        int x = pixel % width;
        int region = regions[pixel];

        TryBlack(pixel - 1, x > 0);
        TryBlack(pixel + 1, x + 1 < width);
        TryBlack(pixel - width, pixel >= width);
        TryBlack(pixel + width, pixel + width < pixelCount);

        void TryBlack(int neighbor, bool inBounds)
        {
            if (!inBounds ||
                !blackMask[neighbor] ||
                regions[neighbor] != region ||
                assigned[neighbor] >= 0)
            {
                return;
            }

            assigned[neighbor] = assigned[pixel];
            queue[tail++] = neighbor;
        }
    }

    // Commit changes only to pixels that were already owned.
    for (int pixel = 0; pixel < pixelCount; pixel++)
    {
        if (originalOwners[pixel] < 0)
        {
            _cellIdByPixel[pixel] = -1;
            continue;
        }

        // Preserve existing ownership if no replacement was reachable.
        _cellIdByPixel[pixel] = assigned[pixel] >= 0
            ? assigned[pixel]
            : originalOwners[pixel];
    }

    // Keep rendering IDs identical to logical cell IDs.
    Array.Copy(_cellIdByPixel, _renderCellIdByPixel, pixelCount);

    _renderParentCellId = new int[cellCount];

    for (int i = 0; i < cellCount; i++)
        _renderParentCellId[i] = i;

    _renderIdByCellRegion = regionCellIds;
    _boundaryRegionByPixel = regions;

    RebuildPixelIndexAfterBoundary();
    UpdateDisplayPixelsFromRenderIds();
}
    
    private void UpdateDisplayPixelsFromRenderIds()
    {
        for (int i = 0; i < _renderCellIdByPixel.Length; i++)
        {
            int renderId = _renderCellIdByPixel[i];

            _displayPixels[i] = renderId >= 0
                ? ((uint)renderId).EncodeAsColor()
                : EmptyCellIdColor;
        }
    }
    
    private void RebuildPixelIndexAfterBoundary()
    {
        foreach (var pixels in _pixelIndicesByCell)
            pixels.Clear();

        Array.Fill(_pixelIndexInCell, -1);
        for (int pixel = 0; pixel < _cellIdByPixel.Length; pixel++)
        {
            int cellId = _cellIdByPixel[pixel];
            if (cellId < 0)
                continue;

            var pixels = _pixelIndicesByCell[cellId];

            _pixelIndexInCell[pixel] = pixels.Count;
            pixels.Add(pixel);
        }
    }

    private static int[] BuildReferenceRegions(bool[] blackMask, int width, int height)
    {
        int pixelCount = checked(width * height);

        var regionByPixel = new int[pixelCount];
        Array.Fill(regionByPixel, -1);

        var queue = new int[pixelCount];
        int regionCount = 0;

        // Flood-fill white pixels. Black pixels cannot connect two regions.
        for (int start = 0; start < pixelCount; start++)
        {
            if (blackMask[start] || regionByPixel[start] >= 0)
                continue;

            int head = 0;
            int tail = 0;

            regionByPixel[start] = regionCount;
            queue[tail++] = start;

            while (head < tail)
            {
                int pixel = queue[head++];
                int x = pixel % width;

                if (x > 0)
                {
                    int n = pixel - 1;

                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (x + 1 < width)
                {
                    int n = pixel + 1;

                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (pixel >= width)
                {
                    int n = pixel - width;

                    if (regionByPixel[n] < 0 && !blackMask[n])
                    {
                        regionByPixel[n] = regionCount;
                        queue[tail++] = n;
                    }
                }

                if (pixel + width < pixelCount)
                {
                    int n = pixel + width;
                    if (regionByPixel[n] >= 0 || blackMask[n])
                        continue;

                    regionByPixel[n] = regionCount;
                    queue[tail++] = n;
                }
            }

            regionCount++;
        }

        // A completely black image has no white regions from which to
        // construct a meaningful partition.
        if (regionCount == 0)
        {
            Array.Fill(regionByPixel, 0);
            return regionByPixel;
        }

        // Assign boundary pixels to the nearest available region.
        // All white pixels are already labeled and are used as BFS seeds.
        int expansionHead = 0;
        int expansionTail = 0;

        for (int i = 0; i < pixelCount; i++)
        {
            if (!blackMask[i])
                queue[expansionTail++] = i;
        }

        while (expansionHead < expansionTail)
        {
            int pixel = queue[expansionHead++];
            int x = pixel % width;
            int regionId = regionByPixel[pixel];

            if (x > 0)
            {
                int n = pixel - 1;

                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (x + 1 < width)
            {
                int n = pixel + 1;

                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (pixel >= width)
            {
                int n = pixel - width;

                if (regionByPixel[n] < 0)
                {
                    regionByPixel[n] = regionId;
                    queue[expansionTail++] = n;
                }
            }

            if (pixel + width < pixelCount)
            {
                int n = pixel + width;
                if (regionByPixel[n] >= 0)
                    continue;

                regionByPixel[n] = regionId;
                queue[expansionTail++] = n;
            }
        }

        return regionByPixel;
    }
}