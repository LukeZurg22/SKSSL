using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private static List<int>[] CreateCellPixelLists(int cellCount)
    {
        var result = new List<int>[cellCount];
        for (int i = 0; i < cellCount; i++)
            result[i] = [];

        return result;
    }

    private static ulong MakeCellRegionKey(int cellId, int regionId) => ((ulong)(uint)regionId << 32) | (uint)cellId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyReferenceBoundary(ReferenceBoundarySettings boundary)
    {
        int width = _pixelMap.Width;
        int height = _pixelMap.Height;
        int pixelCount = _sourcePixels.Length;

        // Size fallback.
        int[] regions = boundary.Width != width || boundary.Height != height
            ? BuildReferenceRegions(boundary.CreateResizedBlackMask(width, height), width, height)
            : BuildReferenceRegions(boundary.GetMask(), width, height);

        int originalCellCount = _cellRawColors.Length;

        // Each original cell reserves its original ID for its first region.
        var primaryRegionByCell = new int[originalCellCount];
        Array.Fill(primaryRegionByCell, -1);

        var cellRegionLookup = new Dictionary<ulong, int>();
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            int originalCellId = _cellIdByPixel[pixelIndex];
            if (originalCellId < 0)
                continue;

            int regionId = regions[pixelIndex];
            if (primaryRegionByCell[originalCellId] >= 0)
                continue;

            primaryRegionByCell[originalCellId] = regionId;
            cellRegionLookup.Add(MakeCellRegionKey(originalCellId, regionId), originalCellId);
        }

        var newCellColors = new List<Color>(_cellRawColors);
        var regionByCellId = new List<int>(originalCellCount);

        for (int i = 0; i < originalCellCount; i++)
            regionByCellId.Add(primaryRegionByCell[i]);

        var newCellIdByPixel = new int[pixelCount];
        Array.Fill(newCellIdByPixel, -1);

        // Map every occupied pixel to the appropriate cell fragment.
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            int originalCellId = _cellIdByPixel[pixelIndex];
            if (originalCellId < 0)
                continue;

            int regionId = regions[pixelIndex];
            ulong key = MakeCellRegionKey(originalCellId, regionId);

            if (!cellRegionLookup.TryGetValue(key, out int newCellId))
            {
                if (newCellColors.Count > MaxEncodableCellId)
                {
                    throw new IndexOutOfRangeException(
                        "Splitting cells exceeded the RGB cell-ID capacity.");
                }

                newCellId = newCellColors.Count;

                cellRegionLookup.Add(key, newCellId);

                // Preserve the original cell's appearance.
                newCellColors.Add(_cellRawColors[originalCellId]);
                regionByCellId.Add(regionId);
            }

            newCellIdByPixel[pixelIndex] = newCellId;
        }

        // Replace the original ownership with the constrained ownership.
        _cellRawColors = newCellColors.ToArray();
        _boundaryRegionByPixel = regions;
        _boundaryRegionByCellId = regionByCellId.ToArray();

        Array.Copy(newCellIdByPixel, _cellIdByPixel, pixelCount);

        // Rebuild the reverse index and encoded texture pixels.
        Array.Fill(_pixelIndexInCell, -1);

        _pixelIndicesByCell = CreateCellPixelLists(_cellRawColors.Length);

        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            int cellId = _cellIdByPixel[pixelIndex];

            if (cellId < 0)
            {
                _displayPixels[pixelIndex] = EmptyCellIdColor;
                continue;
            }

            var cellPixels = _pixelIndicesByCell[cellId];

            _pixelIndexInCell[pixelIndex] = cellPixels.Count;
            cellPixels.Add(pixelIndex);

            _displayPixels[pixelIndex] = ((uint)cellId).EncodeAsColor();
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