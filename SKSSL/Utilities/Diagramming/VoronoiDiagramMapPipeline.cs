using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi;
using SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Utilities;

/// <summary>
/// Convenience entry points connecting the raster-first Voronoi generator to DiagramMap.
/// DiagramMap must use the VoronoiDiagramData constructor patch documented in this bundle;
/// that constructor copies CellIds to its own CPU/GPU buffers and builds its own textures.
/// </summary>
public static class VoronoiDiagramMapPipeline
{
    public static DiagramMap FromHeightmap(
        GraphicsDevice graphics,
        Texture2D heightmap,
        int width,
        int height,
        VoronoiOptions? options = null,
        ReferenceBoundarySettings? referenceBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(heightmap);

        using var generator = new Voronoi.Voronoi(graphics, heightmap);
        VoronoiDiagramData data = generator.GenerateDiagram(width, height, options);

        // DiagramMap snapshots data.CellIds and owns its own textures, so it remains valid
        // after this local generator is disposed.
        return new DiagramMap(graphics, data, referenceBoundary);
    }

    public static DiagramMap FromHeightmapPixels(
        GraphicsDevice graphics,
        Color[] heightmapPixels,
        int heightmapWidth,
        int heightmapHeight,
        int outputWidth,
        int outputHeight,
        VoronoiOptions? options = null,
        ReferenceBoundarySettings? referenceBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(heightmapPixels);

        using var generator = new Voronoi.Voronoi(graphics);
        VoronoiDiagramData data = generator.GenerateDiagram(
            heightmapPixels,
            heightmapWidth,
            heightmapHeight,
            outputWidth,
            outputHeight,
            options);
        return new DiagramMap(graphics, data, referenceBoundary);
    }

    public static DiagramMap FromDistributor(
        GraphicsDevice graphics,
        IPointDistributor distributor,
        int width,
        int height,
        VoronoiOptions? options = null,
        ReferenceBoundarySettings? referenceBoundary = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(distributor);

        using var generator = new Voronoi.Voronoi(graphics, distributor);
        VoronoiDiagramData data = generator.GenerateDiagram(width, height, options);
        return new DiagramMap(graphics, data, referenceBoundary);
    }
}