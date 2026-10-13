using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities;

public partial class DiagramMap
{
    #region Fields

    /// Texture containing the per-pixel colors of the cell highlight layer.
    private readonly Texture2D _highlightMap;

    /// Indicates whether each cell currently has an active highlight.
    /// Indexed by cell ID.
    private readonly bool[] _hasCellHighlight;

    /// CPU-side pixel buffer for the highlight texture.
    /// Each pixel stores its cell's highlight color or transparency
    /// when no highlight is present.
    private readonly Color[] _highlightPixels;

    /// Highlight color assigned to each cell.
    /// Indexed by cell ID.
    private readonly Color[] _cellHighlightColors;

    /// Opacity multiplier applied when drawing the highlight layer.
    /// Values are expected to be in the range from 0 to 1.
    private float _highlightOpacity = 1f;

    /// Indicates whether highlighted cells should flash.
    private bool _highlightFlashing;

    /// Toggles whether highlighting is applied with a Lerp.
    private bool _smoothHighlightFlashing;

    /// Indicates whether the highlight layer is currently visible.
    /// Toggled by the flashing animation.
    private int _highlightedCells = 0;

    /// Duration of each visible or hidden phase of the flashing animation.
    private TimeSpan _highlightInterval = TimeSpan.FromMilliseconds(500);

    /// Elapsed time accumulated toward the next flashing state change.
    private TimeSpan _highlightElapsed;

    /// Control for highlight visibility when flashing.
    private float _highlightVisibility = 1f;

    #endregion

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
    public void SetCellHighlight(int cellId, Color color)
    {
        if (cellId < 0 || cellId >= _cellHighlightColors.Length) return;
        if (!_hasCellHighlight[cellId]) Interlocked.Increment(ref _highlightedCells);
        _cellHighlightColors[cellId] = color;
        _hasCellHighlight[cellId] = true;
        foreach (int pixelIndex in _pixelIndicesByCell[cellId]) _highlightPixels[pixelIndex] = color;
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
        if (_hasCellHighlight[cellId]) Interlocked.Decrement(ref _highlightedCells);
        _hasCellHighlight[id] = false;
        foreach (int pixelIndex in _pixelIndicesByCell[id]) _highlightPixels[pixelIndex] = Color.Transparent;
        _highlightMap.SetData(_highlightPixels);
    }

    /// <summary>
    /// Highlight every cell a provided color.
    /// </summary>
    /// <param name="color"></param>
    public void HighlightAllCells(Color color)
        => Parallel.ForEach(SelectAllCells(), cell => SetCellHighlight(cell, color));

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
        _highlightedCells = 0;
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
    /// <param name="smooth">Smooth highlighting with a LERP?</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified interval is zero or negative.
    /// </exception>
    /// <remarks>
    /// Each call resets the animation timer and makes the highlight layer
    /// visible immediately. When flashing is enabled, visibility alternates
    /// after each configured interval.
    /// </remarks>
    public void SetHighlightFlashing(bool enabled, TimeSpan? interval = null, bool smooth = false)
    {
        if (interval.HasValue)
        {
            if (interval.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
            _highlightInterval = interval.Value;
        }

        _highlightFlashing = enabled;
        _smoothHighlightFlashing = smooth;
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
        if (!_highlightFlashing || _highlightedCells == 0) return;
        _highlightElapsed += gameTime.ElapsedGameTime;

        double intervalMs = _highlightInterval.TotalMilliseconds;
        double phase = _highlightElapsed.TotalMilliseconds / intervalMs % 2.0;

        // Smoothly fade out, then smoothly fade in.
        _highlightVisibility = _smoothHighlightFlashing
            ? phase < 1.0 ? 1f - (float)phase : (float)(phase - 1.0)
            : phase < 1.0
                ? 1f
                : 0f;

        // Retain only elapsed time within the complete cycle.
        _highlightElapsed = TimeSpan.FromMilliseconds(
            _highlightElapsed.TotalMilliseconds % (intervalMs * 2.0)
        );
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
        if (_highlightVisibility <= 0f || _highlightedCells == 0) return;
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            transformMatrix: _diagramTransform
        );

        float opacity = MathHelper.Clamp(_highlightOpacity * _highlightVisibility, 0f, 1f);
        spriteBatch.Draw(_highlightMap, Vector2.Zero, Color.White * opacity);
        spriteBatch.End();
    }
}