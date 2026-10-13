using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    /// <summary>
    /// Texture containing the per-pixel colors of the cell highlight layer.
    /// </summary>
    private readonly Texture2D _highlightMap;

    /// <summary>
    /// Indicates whether each cell currently has an active highlight.
    /// Indexed by cell ID.
    /// </summary>
    private readonly bool[] _hasCellHighlight;

    /// <summary>
    /// CPU-side pixel buffer for the highlight texture.
    /// Each pixel stores its cell's highlight color or transparency
    /// when no highlight is present.
    /// </summary>
    private readonly Color[] _highlightPixels;

    /// <summary>
    /// Highlight color assigned to each cell.
    /// Indexed by cell ID.
    /// </summary>
    private readonly Color[] _cellHighlightColors;

    /// <summary>
    /// Opacity multiplier applied when drawing the highlight layer.
    /// Values are expected to be in the range from 0 to 1.
    /// </summary>
    private float _highlightOpacity = 1f;

    /// <summary>
    /// Indicates whether highlighted cells should flash.
    /// </summary>
    private bool _highlightFlashing;

    /// <summary>
    /// Indicates whether the highlight layer is currently visible.
    /// Toggled by the flashing animation.
    /// </summary>
    private bool _highlightVisible = true;

    /// <summary>
    /// Duration of each visible or hidden phase of the flashing animation.
    /// </summary>
    private TimeSpan _highlightInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Elapsed time accumulated toward the next flashing state change.
    /// </summary>
    private TimeSpan _highlightElapsed;

    /// <summary>
    /// Sets the highlight color for the specified cell.
    /// </summary>
    /// <param name="cellId">The ID of the cell to highlight.</param>
    /// <param name="color">The color to apply to the cell's highlight layer.</param>
    /// <remarks>
    /// If the cell ID is out of range, no changes are made.
    /// Setting a highlight replaces any previously assigned highlight color
    /// for that cell. The highlight is rendered independently of the cell's
    /// base color and color override.
    /// </remarks>
    public void SetCellHighlight(uint cellId, Color color)
    {
        if (cellId >= _cellHighlightColors.Length) return;
        int id = (int)cellId;
        _cellHighlightColors[id] = color;
        _hasCellHighlight[id] = true;
        foreach (int pixelIndex in _pixelIndicesByCell[id]) _highlightPixels[pixelIndex] = color;
        _highlightMap.SetData(_highlightPixels);
    }

    /// <summary>
    /// Removes the highlight from the specified cell.
    /// </summary>
    /// <param name="cellId">The ID of the cell whose highlight should be removed.</param>
    /// <remarks>
    /// If the cell ID is out of range, no changes are made.
    /// The cell's highlight pixels are made transparent, leaving the underlying
    /// diagram and other rendering layers visible.
    /// </remarks>
    public void ClearCellHighlight(uint cellId)
    {
        if (cellId >= _cellHighlightColors.Length) return;
        int id = (int)cellId;
        _hasCellHighlight[id] = false;
        foreach (int pixelIndex in _pixelIndicesByCell[id]) _highlightPixels[pixelIndex] = Color.Transparent;
        _highlightMap.SetData(_highlightPixels);
    }

    /// <summary>
    /// Removes all cell highlights from the diagram.
    /// </summary>
    /// <remarks>
    /// Clears the highlight state for every cell and makes the entire
    /// highlight texture transparent. This does not change cell colors,
    /// cell overrides, or the existing diagram overlay.
    /// </remarks>
    public void ClearAllCellHighlights()
    {
        Array.Clear(_hasCellHighlight);
        Array.Fill(_highlightPixels, Color.Transparent);
        _highlightMap.SetData(_highlightPixels);
    }

    /// <summary>
    /// Enables or disables flashing for the highlight layer.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> to enable flashing; otherwise,
    /// <see langword="false"/> to keep highlights continuously visible.
    /// </param>
    /// <param name="interval">
    /// Optional duration of each visible or hidden phase.
    /// Must be greater than <see cref="TimeSpan.Zero"/> when specified.
    /// If omitted, the previously configured interval is retained.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified interval is zero or negative.
    /// </exception>
    /// <remarks>
    /// Each call resets the animation timer and makes the highlight layer
    /// visible immediately. When flashing is enabled, visibility alternates
    /// after each configured interval.
    /// </remarks>
    public void SetHighlightFlashing(bool enabled, TimeSpan? interval = null)
    {
        if (interval.HasValue)
        {
            if (interval.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
            _highlightInterval = interval.Value;
        }

        _highlightFlashing = enabled;
        _highlightVisible = true;
        _highlightElapsed = TimeSpan.Zero;
    }

    /// <summary>
    /// Sets the opacity multiplier applied to the highlight layer.
    /// </summary>
    /// <param name="opacity">
    /// The opacity multiplier, where 0 is fully transparent and 1 is fully opaque.
    /// </param>
    /// <remarks>
    /// Values outside the range from 0 to 1 are not clamped by this method.
    /// The multiplier is applied during drawing and does not modify the
    /// stored highlight colors.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHighlightOpacity(float opacity) => _highlightOpacity = opacity;

    /// <summary>
    /// Advances the highlight flashing animation using the elapsed game time.
    /// </summary>
    /// <param name="gameTime">
    /// The game timing information for the current update.
    /// </param>
    /// <remarks>
    /// Call once per game update. If flashing is disabled, this method
    /// does nothing. Otherwise, it accumulates elapsed time and toggles
    /// highlight visibility for each interval that has elapsed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateHighlights(GameTime gameTime)
    {
        if (!_highlightFlashing) return;
        _highlightElapsed += gameTime.ElapsedGameTime;
        while (_highlightElapsed >= _highlightInterval)
        {
            _highlightElapsed -= _highlightInterval;
            _highlightVisible = !_highlightVisible;
        }
    }

    /// <summary>
    /// Draws the highlight layer using the current transform and opacity.
    /// </summary>
    /// <param name="spriteBatch">The sprite batch used to render the highlight texture.</param>
    /// <remarks>
    /// Does nothing while the highlight layer is hidden. Otherwise, draws the
    /// highlight texture with alpha blending and point sampling, using the
    /// diagram's transform matrix.
    /// The caller is responsible for ensuring the sprite batch is not
    /// already active when this method is called.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawHighlights(SpriteBatch spriteBatch)
    {
        if (!_highlightVisible) return;
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            transformMatrix: _diagramTransform
        );
        spriteBatch.Draw(_highlightMap, Vector2.Zero, Color.White * _highlightOpacity);
        spriteBatch.End();
    }
}