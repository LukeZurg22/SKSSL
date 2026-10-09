using System;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    /// <summary>
    /// Assigns a pixel to a cell, updating both cells' pixel lists
    /// and the display buffer. A null ID removes cell ownership.
    /// </summary>
    public void SetPixelCell(int x, int y, uint? cellId, Color unassignedColor = default)
    {
        if ((uint)x >= (uint)_pixelMap.Width || (uint)y >= (uint)_pixelMap.Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates are outside the texture.");

        if (cellId.HasValue)
            ValidateCellId(cellId.Value);

        int pixelIndex = y * _pixelMap.Width + x;
        int newCellId = cellId.HasValue ? (int)cellId.Value : -1;
        int oldCellId = _cellIdByPixel[pixelIndex];

        if (oldCellId == newCellId)
            return;

        // Remove from the previous cell's membership list.
        RemovePixelFromCell(pixelIndex);

        Color newColor;
        if (newCellId >= 0)
        {
            AddPixelToCell(pixelIndex, newCellId);
            newColor = GetEffectiveColor(newCellId);
        }
        else newColor = unassignedColor; // Null means the pixel is no longer part of any cell.

        if (_displayPixels[pixelIndex].Equals(newColor))
            return;

        _displayPixels[pixelIndex] = newColor;
        _textureDirty = true;
    }

    private void RemovePixelFromCell(int pixelIndex)
    {
        int oldCellId = _cellIdByPixel[pixelIndex];
        if (oldCellId < 0)
            return;

        var pixels = _pixelIndicesByCell[oldCellId];
        int removeIndex = _pixelIndexInCell[pixelIndex];
        int lastIndex = pixels.Count - 1;

        // Move the final element into the removed element's position.
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