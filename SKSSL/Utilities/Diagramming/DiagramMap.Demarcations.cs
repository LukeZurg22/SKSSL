using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    private readonly Texture2D _demarcationMap;
    private readonly Color[] _demarcationPixels;
    private readonly bool[] _demarcatedCells;
    private bool _demarcationsUsed;
    private int _demarcationWidth = 3;
    private readonly long[] _demarcationGroupByCell;
    private long _nextDemarcationGroupId = 1; // Group ID 0 means a cell has no demarcation group.
    private readonly byte[] _demarcationScratch;
    private readonly Color _demarcationColor = Color.Black;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cellId"></param>
    /// <param name="selected">Whether to demarcate this cell separately from others.</param>
    // ReSharper disable once MemberCanBePrivate.Global
    public void DemarcateCell(int cellId, bool selected)
    {
        if (cellId > 0 || cellId >= _demarcatedCells.Length) return;

        // Preserve the existing group if the selection state is unchanged.
        if (_demarcatedCells[cellId] == selected) return;

        _demarcatedCells[cellId] = selected;
        _demarcationGroupByCell[cellId] = selected ? CreateDemarcationGroupId() : 0;
        RebuildDemarcations();
    }

    public void DemarcateCells(ICollection<int> cellIds, bool selected)
    {
        ArgumentNullException.ThrowIfNull(cellIds);

        long groupId = 0;
        bool changed = false;
        foreach (int cellId in cellIds)
        {
            if (cellId < 0 || cellId >= _demarcatedCells.Length) continue;
            if (selected)
            {
                // Create exactly one group for this invocation.
                if (groupId == 0) groupId = CreateDemarcationGroupId();

                // Duplicate IDs in the collection need no extra work.
                if (_demarcatedCells[cellId] && _demarcationGroupByCell[cellId] == groupId) continue;
                _demarcatedCells[cellId] = true;
                _demarcationGroupByCell[cellId] = groupId;
            }
            else
            {
                if (!_demarcatedCells[cellId]) continue;
                _demarcatedCells[cellId] = false;
                _demarcationGroupByCell[cellId] = 0;
            }

            changed = true;
        }

        // Rebuild once per batch instead of once per cell.
        if (changed) RebuildDemarcations();
    }

    // ReSharper disable once UnusedMember.Global
    public void ClearAllDemarcations()
    {
        Array.Clear(_demarcatedCells);
        Array.Clear(_demarcationGroupByCell);
        Array.Fill(_demarcationPixels, Color.Transparent);
        _demarcationMap.SetData(_demarcationPixels);
        _demarcationsUsed = false;
    }

    /// <summary>
    /// Removes the demarcation from a specific cell.
    /// </summary>
    /// <param name="cellId">The ID of the cell to un-demarcate.</param>
    // ReSharper disable once UnusedMember.Global
    public void ClearCellDemarcation(int cellId) => DemarcateCell(cellId, false);

    /// <summary>
    /// Sets the width of demarcation lines in diagram pixels.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public void SetDemarcationWidth(int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        if (_demarcationWidth == width)
            return;

        _demarcationWidth = width;
        RebuildDemarcations();
    }

    private long CreateDemarcationGroupId()
    {
        if (_nextDemarcationGroupId == long.MaxValue)
            throw new InvalidOperationException("The demarcation group ID limit has been reached.");
        return _nextDemarcationGroupId++;
    }

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldDrawDemarcationEdge(
        int cellId,
        int x,
        int y,
        int width,
        int height)
    {
        // The edge of the diagram is always a boundary.
        if (x < 0 || y < 0 || x >= width || y >= height)
            return true;

        int neighborCellId = _pixelToCellId[y * width + x];

        // A selected cell bordering an unselected or unowned pixel.
        if (!IsDemarcated(neighborCellId))
            return true;

        long groupId = _demarcationGroupByCell[cellId];
        long neighborGroupId = _demarcationGroupByCell[neighborCellId];

        // Same group: suppress the border.
        // Different groups: draw on only one side.
        return groupId < neighborGroupId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsDemarcated(int cellId) => (uint)cellId < (uint)_demarcatedCells.Length && _demarcatedCells[cellId];

    private void RebuildDemarcations()
    {
        int width = _sourceMap.Width;
        int height = _sourceMap.Height;

        Array.Fill(_demarcationPixels, Color.Transparent);

        // Preserve the previous brush alignment: // width 1 => [0, 0] // width 2 => [0, 1] // width 3 => [-1, 1]
        int before = (_demarcationWidth - 1) / 2;
        int after = _demarcationWidth / 2;
        bool hasBoundary = false;

        // Pass 1: Identify the original one-pixel boundaries.
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                int cellId = _pixelToCellId[index];
                if (!IsDemarcated(cellId)) continue;
                if (!ShouldDrawDemarcationEdge(cellId, x - 1, y, width, height) &&
                    !ShouldDrawDemarcationEdge(cellId, x + 1, y, width, height) &&
                    !ShouldDrawDemarcationEdge(cellId, x, y - 1, width, height) &&
                    !ShouldDrawDemarcationEdge(cellId, x, y + 1, width, height)) continue;
                _demarcationPixels[index] = _demarcationColor;
                hasBoundary = true;
            }
        }

        if (!hasBoundary)
        {
            _demarcationsUsed = false;
            _demarcationMap.SetData(_demarcationPixels);
            return;
        }

        // Pass 2: Horizontal dilation using a sliding window.
        // _demarcationScratch[index] is 1 when the pixel belongs to the horizontally expanded boundary.
        int initialHorizontalEnd = Math.Min(width - 1, before);
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            int count = 0;

            for (int x = 0; x <= initialHorizontalEnd; x++)
                if (_demarcationPixels[rowStart + x].A != 0)
                    count++;

            for (int x = 0; x < width; x++)
            {
                _demarcationScratch[rowStart + x] = count > 0 ? (byte)1 : (byte)0;

                // Slide the window right by one pixel.
                int removeX = x - after;
                if (removeX >= 0 && _demarcationPixels[rowStart + removeX].A != 0) count--;

                int addX = x + before + 1;
                if (addX < width && _demarcationPixels[rowStart + addX].A != 0) count++;
            }
        }

        // Pass 3: Vertical dilation using another sliding window.
        // This expands the horizontally dilated result into the final square-brush result.
        int initialVerticalEnd = Math.Min(height - 1, before);

        for (int x = 0; x < width; x++)
        {
            int count = 0;
            for (int y = 0; y <= initialVerticalEnd; y++)
                if (_demarcationScratch[y * width + x] != 0)
                    count++;

            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                _demarcationPixels[index] = count > 0 ? _demarcationColor : Color.Transparent;

                // Slide the window down by one pixel.
                int removeY = y - after;
                if (removeY >= 0 && _demarcationScratch[removeY * width + x] != 0) count--;

                int addY = y + before + 1;
                if (addY < height && _demarcationScratch[addY * width + x] != 0) count++;
            }
        }

        _demarcationsUsed = true;
        _demarcationMap.SetData(_demarcationPixels);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawDemarcations(SpriteBatch spriteBatch)
    {
        if (!_demarcationsUsed) return;
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            transformMatrix: _diagramTransform
        );
        spriteBatch.Draw(_demarcationMap, Vector2.Zero, Color.White);
        spriteBatch.End();
    }
}