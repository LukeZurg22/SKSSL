using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using static System.Array;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BuildPixelIndexFromIds()
    {
        foreach (var pixels in _pixelIndicesByCell)
            pixels.Clear();

        Fill(_pixelIndexInCell, -1);
        for (int pixelIndex = 0; pixelIndex < _pixelToCellId.Length; pixelIndex++)
        {
            // -1 = culled/unassigned. Other invalid IDs are ignored as well.
            int cellId = _pixelToCellId[pixelIndex];
            if ((uint)cellId >= (uint)_pixelIndicesByCell.Length)
                continue;

            var pixels = _pixelIndicesByCell[cellId];
            _pixelIndexInCell[pixelIndex] = pixels.Count;
            pixels.Add(pixelIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddPixelToCell(int pixelIndex, int cellId)
    {
        var pixels = _pixelIndicesByCell[cellId];
        _pixelIndexInCell[pixelIndex] = pixels.Count;
        pixels.Add(pixelIndex);
        _pixelToCellId[pixelIndex] = cellId;
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

        if (!_idTextureDirty) return;
        Rectangle rect = _dirtyIdRectangle;
        long changedArea = (long)rect.Width * rect.Height;
        long totalArea = (long)_sourceMap.Width * _sourceMap.Height;

        // If most of the texture is covered, a full upload avoids
        // copying a large staging rectangle unnecessarily.
        if (changedArea * 10 >= totalArea * 6) _displayMap.SetData(_displayPixels);
        else
        {
            int sourceWidth = _sourceMap.Width;
            int copyWidth = rect.Width;
            for (int row = 0; row < rect.Height; row++)
            {
                int sourceIndex = (rect.Y + row) * sourceWidth + rect.X;
                int destinationIndex = row * copyWidth;
                Copy(_displayPixels,
                    sourceIndex,
                    _uploadScratch,
                    destinationIndex,
                    copyWidth);
            }

            int elementCount = rect.Width * rect.Height;
            _displayMap.SetData(0, rect, _uploadScratch, 0, elementCount);
        }

        _idTextureDirty = false;
        _dirtyIdRectangle = Rectangle.Empty;
    }

    //@formatter:off
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once UnusedMember.Local
    private bool TryGetCellIdFromColor(Color color, out int id) => _paletteColorToId.TryGetValue(color.PackedValue, out id) || Fail(out id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Fail(out int id) { id = -1; return false; }
    //@formatter:on
}