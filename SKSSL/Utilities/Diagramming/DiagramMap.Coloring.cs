using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    // Override state, indexed by cell ID.
    private readonly Color[] _cellOverrideColors;
    private readonly bool[] _hasCellOverride;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawDiagram(SpriteBatch spriteBatch)
    {
        // Set all per-map shader parameters before drawing.
        _paletteEffect.Parameters["SpriteTexture"].SetValue(_displayMap);
        _paletteEffect.Parameters["PaletteTexture"].SetValue(_paletteTexture);
        _paletteEffect.Parameters["BaseTexture"].SetValue(_sourceMap);
        _paletteEffect.Parameters["PaletteWidth"].SetValue((float)_paletteWidth);
        _paletteEffect.Parameters["PaletteHeight"].SetValue((float)_paletteHeight);

        // The cell-ID texture is bound by SpriteBatch.
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            effect: _paletteEffect,
            transformMatrix: _diagramTransform);
        float opacity = MathHelper.Clamp(_diagramOpacity, 0f, 1f);
        spriteBatch.Draw(_displayMap, Vector2.Zero, Color.White * opacity);
        spriteBatch.End();
    }
}