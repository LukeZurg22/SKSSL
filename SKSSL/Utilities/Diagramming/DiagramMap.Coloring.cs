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
        if (_hasCellOverride[id] && _cellOverrideColors[id] == color)
            return;

        _cellOverrideColors[id] = color;
        _hasCellOverride[id] = true;

        UpdateEffectiveColor(id);
    }

    public void ClearCellOverride(uint id)
    {
        ValidateCellId(id);
        if (!_hasCellOverride[id])
            return;

        _hasCellOverride[id] = false;
        UpdateEffectiveColor(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCellId(uint id)
        => ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, (uint)_cellRawColors.Length);

    private void UpdateEffectiveColor(uint cellId)
    {
        Color color = _hasCellOverride[cellId] ? _cellOverrideColors[cellId] : _cellRawColors[cellId];
        if (_palettePixels[cellId] == color)
            return;

        _palettePixels[cellId] = color;
        _paletteDirty = true;
    }

    private Color GetEffectiveColor(int cellId)
        => _hasCellOverride[cellId] ? _cellOverrideColors[cellId] : _cellRawColors[cellId];

    public void ClearCellOverrides(ICollection<uint> selectedIds)
    {
        foreach (uint id in selectedIds)
        {
            if (!_hasCellOverride[id])
                continue;
            _hasCellOverride[id] = false;
            UpdateEffectiveColor(id);
        }
    }
}