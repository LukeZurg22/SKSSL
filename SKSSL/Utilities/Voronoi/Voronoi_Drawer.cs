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
    private void UpdateTexture(
        VoronoiBoundaryMode boundaryMode,
        VoronoiRenderingFlags flags,
        float thickness,
        float pointSize,
        bool forceUpdate = false)
    {
        if (!_isGenerated)
            throw new InvalidOperationException("Attempted to get Voronoi texture before generating a diagram.");

        if (!forceUpdate &&
            _textureValid &&
            flags == _previousFlags &&
            boundaryMode == _previousBoundaryMode &&
            Math.Abs(thickness - _previousThickness) < 0.01f &&
            Math.Abs(pointSize - _previousPointSize) < 0.01f)
            return;

        var output = new RenderTarget2D(
            _graphicsDevice,
            _width,
            _height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);

        var previousTargets = _graphicsDevice.GetRenderTargets();

        _graphicsDevice.SetRenderTarget(output);

        try
        {
            _graphicsDevice.Clear(Color.Transparent);

            if (flags.HasFlag(VoronoiRenderingFlags.Cells))
                DrawCellBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Triangles))
                DrawTriangleBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Edges))
                DrawEdgeBatch();

            if (flags.HasFlag(VoronoiRenderingFlags.Points))
                DrawPointBatch();

            //if (_imageDistributor is { AllowBlackGaps: true })
            //    DrawGapBatch();
        }
        finally
        {
            _graphicsDevice.SetRenderTargets(previousTargets);
        }

        Texture2D oldTexture = _pixelMap;
        _pixelMap = output;

        _previousBoundaryMode = boundaryMode;
        _previousThickness = thickness;
        _previousPointSize = pointSize;
        _previousFlags = flags;
        _textureValid = true;

        if (oldTexture is RenderTarget2D oldTarget)
            oldTarget.Dispose();
    }

    public void Draw(SpriteBatch? spriteBatch)
    {
        if (!_isGenerated || spriteBatch == null)
            return;

        spriteBatch.Begin();
        spriteBatch.Draw(_pixelMap, Vector2.Zero, Color.White);
        spriteBatch.End();

        DrawHighlightedCells();
    }

    #endregion

    #region Selective Drawing Parts

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

    private void DrawHighlightedCells()
    {
        if (_demarcateBatchPrimitiveCount == 0)
            return;

        SetScreenProjection(Matrix.Identity, Matrix.Identity);

        BlendState previousBlend = _graphicsDevice.BlendState;
        RasterizerState previousRasterizer = _graphicsDevice.RasterizerState;

        try
        {
            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _demarcateBatchVertices,
                    0,
                    _demarcateBatchPrimitiveCount);
            }
        }
        finally
        {
            _graphicsDevice.BlendState = previousBlend;
            _graphicsDevice.RasterizerState = previousRasterizer;
        }
    }

    #endregion
}