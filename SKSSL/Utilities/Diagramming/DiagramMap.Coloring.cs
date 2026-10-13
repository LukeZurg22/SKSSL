using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static System.Threading.Tasks.Parallel;

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    // Override state, indexed by cell ID.
    private readonly Color[] _cellOverrideColors;
    private readonly bool[] _hasCellOverride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCellOverride(int id, Color color)
    {
        ValidateCellId(id);
        if (_hasCellOverride[id] && _cellOverrideColors[id] == color) return;
        _cellOverrideColors[id] = color;
        _hasCellOverride[id] = true;
        UpdateEffectiveColor(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once MemberCanBePrivate.Global
    public void ClearCellOverride(int id)
    {
        ValidateCellId(id);
        if (!_hasCellOverride[id]) return;
        _hasCellOverride[id] = false;
        UpdateEffectiveColor(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateCellId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(id, _cellRawColors.Length);
    }

    private void UpdateEffectiveColor(int cellId)
    {
        Color color = GetEffectiveColor(cellId);
        if (_palettePixels[cellId] == color) return;
        _palettePixels[cellId] = color;
        _paletteDirty = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Color GetEffectiveColor(int cellId)
        => _hasCellOverride[cellId] ? _cellOverrideColors[cellId] : _cellRawColors[cellId];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once UnusedMember.Global
    public void ClearCellOverrides(HashSet<int>? selectedIds = null)
    {
        switch (selectedIds)
        {
            //@formatter:off
            case null: For(0, _hasCellOverride.Length, ClearCellOverride); return;
            default: ForEach(selectedIds, ClearCellOverride); return;
            //@formatter:on
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once UnusedMember.Global
    public void SetAllCellOverrides(ICollection<int> selectedIds, Color color)
        => ForEach(selectedIds, i => SetCellOverride(i, color));

    /// <summary>
    /// Set all cells to a unified override color in parallel.
    /// </summary>
    /// <param name="color"></param>
    // ReSharper disable once UnusedMember.Global
    public void SetAllCellsToOverride(Color color)
        => For(0, _hasCellOverride.Length, cell => SetCellOverride(cell, color));

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