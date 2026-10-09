using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    #region MonoGame Primary Functions

    /// <summary>
    /// Gets the voronoi object's rasterized texture, or creates one.
    /// </summary>
    /// <returns>Texture2D reference of the Voronoi image result.</returns>
    /// <remarks>
    /// Make sure that the Diagram data is generated first through <see cref="GenerateDiagram"/>.
    /// When this function is called, it also assigns the internal pixel data to the new image.
    /// Avoid repetitive calls, as it's expensive.
    /// </remarks>
    private (Texture2D PixelMap, Texture2D? OverlayMap) CreateTextureMaps(
        VoronoiBoundaryMode boundaryMode,
        VoronoiRenderingFlags flags,
        float thickness,
        float pointSize)
    {
        if (!_isGenerated)
            throw new InvalidOperationException("Attempted to get Voronoi texture before generating a diagram.");

        var previousTargets = _graphicsDevice.GetRenderTargets();

        RenderTarget2D? cellOutput = null;
        RenderTarget2D? overlayOutput = null;

        try
        {
            // Render Voronoi cells to their own texture.
            cellOutput = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            _graphicsDevice.SetRenderTarget(cellOutput);
            _graphicsDevice.Clear(Color.Transparent);

            if (flags.HasFlag(VoronoiRenderingFlags.Cells))
                DrawCellBatch();

            // If there is no overlay, then simply do not output an overlay.
            if (flags != VoronoiRenderingFlags.Cells)
            {
                // Render triangles, edges, and points to a separate texture.
                overlayOutput = new RenderTarget2D(
                    _graphicsDevice,
                    _width,
                    _height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None);

                _graphicsDevice.SetRenderTarget(overlayOutput);
                _graphicsDevice.Clear(Color.Transparent);

                if (flags.HasFlag(VoronoiRenderingFlags.Triangles))
                    DrawTriangleBatch();

                if (flags.HasFlag(VoronoiRenderingFlags.Edges))
                    DrawEdgeBatch();

                if (flags.HasFlag(VoronoiRenderingFlags.Points))
                    DrawPointBatch();
            }
        }
        catch
        {
            cellOutput?.Dispose();
            overlayOutput?.Dispose();
            throw;
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
        }

        // Replace the old textures only after rendering succeeds.
        return (cellOutput, overlayOutput);
    }

    #endregion

    #region Drawing Overlay Parts

    private void DrawTriangleBatch()
    {
        if (_triangleBatchPrimitiveCount == 0)
            return;

        BlendState previousBlendState = _graphicsDevice.BlendState;
        RasterizerState previousRasterizerState = _graphicsDevice.RasterizerState;

        try
        {
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _triangleBatchVertices,
                    0,
                    _triangleBatchPrimitiveCount);
            }
        }
        finally
        {
            _graphicsDevice.BlendState = previousBlendState;
            _graphicsDevice.RasterizerState = previousRasterizerState;
        }
    }

    private void DrawPointBatch()
    {
        if (_pointBatchPrimitiveCount == 0)
            return;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _pointBatchVertices,
                0,
                _pointBatchPrimitiveCount);
        }
    }

    private void DrawEdgeBatch()
    {
        if (_edgeBatchPrimitiveCount == 0)
            return;

        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _edgeBatchVertices,
                0,
                _edgeBatchPrimitiveCount);
        }
    }

    private void DrawCellBatch()
    {
        if (_cellBatchPrimitiveCount == 0)
            return;

        RasterizerState previousRasterizer = _graphicsDevice.RasterizerState;

        try
        {
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _cellBatchVertices,
                    0,
                    _cellBatchPrimitiveCount);
            }
        }
        finally
        {
            _graphicsDevice.RasterizerState = previousRasterizer;
        }
    }

    #endregion
    
    private Color GenerateCellColor(float x, float y, uint id)
    {
        Color color;
        uint hash;
        byte r, g, b;
        switch (_cellDrawMode)
        {
            case ColorMode.Deterministic_Lines:
                hash = (uint)x + (uint)y;
                hash ^= hash >> 16;
                r = (byte)hash;
                hash ^= hash >> 15;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                return color;
            case ColorMode.Deterministic_Random: // Throw together a lazy hash based on cell Site vertex.
                hash = (uint)x ^ (uint)y;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                r = (byte)hash;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                return color;
            case ColorMode.Random: // Truly random 0 -> 255
                color = new Color(
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    255);
                return color;
            case ColorMode.Semi_Deterministic_Unique:
                // Grab a deterministic semi-unique color and go an integer check.
                // Naturally if it isn't unique, then it is reaching the birthday-paradox point
                color = ColorUtilities.GetSemiUniqueColor(id);
                int reversed = (color.R << 16) | (color.G << 8) | color.B;
                lock (_usedColors)
                {
                    // If the semi-unique color turns out to no longer be unique, then
                    // defaulting to the thread-dangerous unique color generator is the
                    // next best option. I am aware this causes an inner-dependency, and
                    // may also cause a little overhead. The deterministic method is faster
                    // than relying on the Random class to do its calls, and that for maps
                    // approximately smaller than 2000x2000, this would be incredibly efficient.
                    // As far as I see it, it's a small, but nevertheless preferred -optimization.
                    if (!_usedColors.Add(reversed)) goto case ColorMode.Unique;
                }

                return color;
            case ColorMode.Unique: // Pure random RGB – three integer ops, no floats
                int rgb;
                lock (_random)
                lock (_usedColors)
                {
                    do rgb = _random.Next(0x1000000); // 0 … 16 777 215
                    while (!_usedColors.Add(rgb));
                }

                color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);
                break;
            case ColorMode.Unified:
            default:
                color = _edgeColor;
                break;
        }

        return color;
    }
}