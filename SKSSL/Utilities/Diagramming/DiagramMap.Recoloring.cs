using System;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    /// <summary>
    /// Assigns a pixel to a cell, updating both cells' pixel lists
    /// and the display buffer. A null ID removes cell ownership.
    /// </summary>
    public void SetPixelCell(int x, int y, uint? cellId)
    {
        if ((uint)x >= (uint)_sourceMap.Width || (uint)y >= (uint)_sourceMap.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates are outside the texture.");

        if (cellId.HasValue)
            ValidateCellId(cellId.Value);

        int pixelIndex = y * _sourceMap.Width + x;
        int newCellId = cellId.HasValue ? (int)cellId.Value : -1;

        if (cellId.HasValue && _boundaryRegionByPixel != null)
        {
            int region = _boundaryRegionByPixel[pixelIndex];
            ulong key = MakeCellRegionKey((int)cellId.Value, region);
            if (!_renderIdByCellRegion.TryGetValue(key, out _))
                throw new ArgumentException("The cell cannot be assigned to this boundary region.", nameof(cellId));
        }

        int oldCellId = _pixelToCellId[pixelIndex];
        if (oldCellId == newCellId && (newCellId >= 0 || _displayPixels[pixelIndex].A == 128))
            return;

        RemovePixelFromCell(pixelIndex);

        if (newCellId >= 0)
            AddPixelToCell(pixelIndex, newCellId);

        // -1 encodes a deliberately empty/transparent pixel.
        _displayPixels[pixelIndex] = newCellId >= 0 ? ((uint)newCellId).EncodeAsColor() : EmptyCellIdColor;
        MarkIdPixelDirty(x, y);
        _renderCellIdByPixel[pixelIndex] = newCellId;
    }

    private void MarkIdPixelDirty(int x, int y)
    {
        var pixelRect = new Rectangle(x, y, 1, 1);
        _dirtyIdRectangle = _idTextureDirty ? Rectangle.Union(_dirtyIdRectangle, pixelRect) : pixelRect;
        _idTextureDirty = true;
    }

    private void RemovePixelFromCell(int pixelIndex)
    {
        int oldCellId = _pixelToCellId[pixelIndex];
        if (oldCellId < 0)
            return;

        var pixels = _pixelIndicesByCell[oldCellId];
        int removeIndex = _pixelIndexInCell[pixelIndex];
        int lastIndex = pixels.Count - 1;

        if (removeIndex != lastIndex)
        {
            int movedPixelIndex = pixels[lastIndex];

            pixels[removeIndex] = movedPixelIndex;
            _pixelIndexInCell[movedPixelIndex] = removeIndex;
        }

        pixels.RemoveAt(lastIndex);

        _pixelToCellId[pixelIndex] = -1;
        _pixelIndexInCell[pixelIndex] = -1;
    }
}