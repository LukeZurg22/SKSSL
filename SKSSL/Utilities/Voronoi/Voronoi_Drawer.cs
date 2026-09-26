using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
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

        SetScreenProjection();

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
}