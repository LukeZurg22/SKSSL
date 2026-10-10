using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCellOverride(uint id, Color color)
    {
        ValidateCellId(id);
        if (_hasCellOverride[id] && _cellOverrideColors[id] == color)
            return;

        _cellOverrideColors[id] = color;
        _hasCellOverride[id] = true;
        UpdateEffectiveColor(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        Color color = GetEffectiveColor(cellId);
        if (_palettePixels[cellId] == color)
            return;

        _palettePixels[cellId] = color;
        _paletteDirty = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color GetEffectiveColor(uint cellId)
        => _hasCellOverride[cellId] ? _cellOverrideColors[cellId] : _cellRawColors[cellId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearCellOverrides(ICollection<uint>? selectedIds = null)
    {
        switch (selectedIds)
        {
            case null:
                // The uint cast for an int indexer is a bit dangerous. But...
                // ...realistically-speaking, nobody is ever going to make 21 million cells on one diagram. Hopefully.
                Parallel.For(0, _hasCellOverride.Length, i => { ClearCellOverride((uint)i); });
                return;
            default:
            {
                foreach (uint id in selectedIds)
                    ClearCellOverride(id);
                return;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once UnusedMember.Global
    public void SetCellOverrides(ICollection<uint> selectedIds, Color color)
    {
        foreach (uint id in selectedIds) SetCellOverride(id, color);
    }
}