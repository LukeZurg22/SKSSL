using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    public void SetCellOverride(uint id, Color color)
    {
        ValidateCellId(id);

        int cellId = (int)id;

        _cellOverrideColors[cellId] = color;
        _hasCellOverride[cellId] = true;

        UpdateEffectiveColor(cellId);
    }

    public void ClearCellOverride(uint id)
    {
        ValidateCellId(id);

        int cellId = (int)id;

        if (!_hasCellOverride[cellId])
            return;

        _hasCellOverride[cellId] = false;

        UpdateEffectiveColor(cellId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCellId(uint id)
        => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, (uint)_cellRawColors.Length);

    private Color GetEffectiveColor(int cellId)
        => _hasCellOverride[cellId] ? _cellOverrideColors[cellId] : _cellRawColors[cellId];

    private void UpdateEffectiveColor(int cellId)
    {
        Color effectiveColor = GetEffectiveColor(cellId);
        var pixels = _pixelIndicesByCell[cellId];
        foreach (int pixelIndex in pixels)
        {
            if (_displayPixels[pixelIndex].Equals(effectiveColor))
                continue;

            _displayPixels[pixelIndex] = effectiveColor;
            _textureDirty = true;
        }
    }
}