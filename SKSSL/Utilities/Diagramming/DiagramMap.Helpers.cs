using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using SKSSL.Utilities.Voronoi;
using static System.Array;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BuildPixelIndex()
    {
        for (int pixelIndex = 0; pixelIndex < _sourcePixels.Length; pixelIndex++)
        {
            // Default alpha = 0, so the shader shows the
            // original source pixel for unmatched colors.
            if (!TryGetCellIdFromColor(_sourcePixels[pixelIndex], out uint id))
                continue;


            if (id >= (uint)_pixelIndicesByCell.Length)
                continue;

            AddPixelToCell(pixelIndex, (int)id);
            _displayPixels[pixelIndex] = id.EncodeAsColor();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddPixelToCell(int pixelIndex, int cellId)
    {
        var pixels = _pixelIndicesByCell[cellId];
        _pixelIndexInCell[pixelIndex] = pixels.Count;
        pixels.Add(pixelIndex);
        _cellIdByPixel[pixelIndex] = cellId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FlushTextureUpdates()
    {
        // Color changes: one upload for the entire small palette.
        if (_paletteDirty)
        {
            _paletteTexture.SetData(_palettePixels);
            _paletteDirty = false;
        }

        if (!_idTextureDirty)
            return;

        Rectangle rect = _dirtyIdRectangle;

        long changedArea = (long)rect.Width * rect.Height;
        long totalArea = (long)_pixelMap.Width * _pixelMap.Height;

        // If most of the texture is covered, a full upload avoids
        // copying a large staging rectangle unnecessarily.
        if (changedArea * 10 >= totalArea * 6) _cellIdTexture.SetData(_displayPixels);
        else
        {
            int sourceWidth = _pixelMap.Width;
            int copyWidth = rect.Width;
            for (int row = 0; row < rect.Height; row++)
            {
                int sourceIndex = (rect.Y + row) * sourceWidth + rect.X;
                int destinationIndex = row * copyWidth;
                Copy(
                    _displayPixels,
                    sourceIndex,
                    _uploadScratch,
                    destinationIndex,
                    copyWidth);
            }

            int elementCount = rect.Width * rect.Height;
            _cellIdTexture.SetData(0, rect, _uploadScratch, 0, elementCount);
        }

        _idTextureDirty = false;
        _dirtyIdRectangle = Rectangle.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetCellIdFromColor(Color color, out uint id)
    {
        id = InvalidatedUint;
        if (_rawColorToIdLookup.Length == 0)
            return false;

        uint key = color.PackedValue;
        int index = (int)(key.HashPackedColor() & (uint)_rawColorToIdMask);

        while (true)
        {
            PackedColorEntry entry = _rawColorToIdLookup[index];
            if (entry.CellIdPlusOne == 0)
                return false;

            if (entry.PackedColor == key)
            {
                id = entry.CellIdPlusOne - 1;
                return true;
            }

            index = (index + 1) & _rawColorToIdMask;
        }
    }
}