using System;
using System.Collections.Generic;
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
        if ((uint)x >= (uint)_pixelMap.Width || (uint)y >= (uint)_pixelMap.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates are outside the texture.");

        if (cellId.HasValue)
            ValidateCellId(cellId.Value);

        int pixelIndex = y * _pixelMap.Width + x;
        int newCellId = cellId.HasValue ? (int)cellId.Value : -1;
        int oldCellId = _cellIdByPixel[pixelIndex];
        if (oldCellId == newCellId && (newCellId >= 0 || _displayPixels[pixelIndex].A == 128))
            return;

        RemovePixelFromCell(pixelIndex);

        if (newCellId >= 0)
            AddPixelToCell(pixelIndex, newCellId);

        // -1 encodes a deliberately empty/transparent pixel.
        _displayPixels[pixelIndex] = ((uint)newCellId).EncodeAsColor();
        MarkIdPixelDirty(x, y);
    }

    private void MarkIdPixelDirty(int x, int y)
    {
        var pixelRect = new Rectangle(x, y, 1, 1);
        _dirtyIdRectangle = _idTextureDirty ? Rectangle.Union(_dirtyIdRectangle, pixelRect) : pixelRect;
        _idTextureDirty = true;
    }

    private void RemovePixelFromCell(int pixelIndex)
    {
        int oldCellId = _cellIdByPixel[pixelIndex];

        if (oldCellId < 0)
            return;

        List<int> pixels = _pixelIndicesByCell[oldCellId];
        int removeIndex = _pixelIndexInCell[pixelIndex];
        int lastIndex = pixels.Count - 1;

        if (removeIndex != lastIndex)
        {
            int movedPixelIndex = pixels[lastIndex];

            pixels[removeIndex] = movedPixelIndex;
            _pixelIndexInCell[movedPixelIndex] = removeIndex;
        }

        pixels.RemoveAt(lastIndex);

        _cellIdByPixel[pixelIndex] = -1;
        _pixelIndexInCell[pixelIndex] = -1;
    }
}