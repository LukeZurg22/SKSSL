using System;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi.PointDistributors;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    /// <summary>
    /// Generates a set of voronoi cells and internally inserts them into the data of this <see cref="Voronoi"/>
    /// class object.
    /// </summary>
    /// <returns>Generated 2D image of pixel data generated from diagram.</returns>
    /// <remarks>
    /// Also calls <see cref="CreateTextureMaps"/> to populate the pixel data.
    /// </remarks>
    /// <param name="settings"></param>
    /// <param name="points"></param>
    /// <param name="width">Width of diagram in pixels.</param>
    /// <param name="height">Height of diagram in pixels.</param>
    public (Texture2D PixelMap, Texture2D? OverlayMap) GenerateDiagram(
        uint points = DefaultPointCount,
        int? width = null,
        int? height = null,
        DiagramSettings? settings = null)
    {
        settings ??= new DiagramSettings();

        _isGenerated = false;
        _boundaryMode = settings.BoundaryMode;
        _width = width ??= _graphicsDevice.Viewport.Width;
        _height = height ??= _graphicsDevice.Viewport.Height;

        using DelaunayTriangulator delaunay = new();

        // Create a list of seeded points and then handle distribution using a provided distributor.
        int maxX = width.Value;
        int maxY = height.Value;
        var pointsList = delaunay.CreatePointsList(maxX, maxY, points);
        settings.Distributor.Generate(ref pointsList, points + 4, maxX, maxY, settings.Randomness);
        AssignSpatialIds(pointsList);
        _points = pointsList.ToArray();

        // Clear color storage. New sizes are +1 due to point amount being 1-based indexed.
        Array.Clear(_cellOverrideColors, 0, _cellRawColors.Length);
        Array.Clear(_cellRawColors, 0, _cellRawColors.Length);
        Array.Resize(ref _cellOverrideColors, _points.Length + 1);
        Array.Resize(ref _cellRawColors, _points.Length + 1);
        _cellsBySiteId = new VoronoiCell[_points.Length];
        Array.Clear(_cellsBySiteId);

        // Clear old data.
        _voronoiEdges.Clear();
        _usedColors.Clear();
        _cellVoronoiEdges.Clear();
        _voronoiEdges.Capacity = Math.Max(_voronoiEdges.Capacity, (int)(points * 3));
        _renderingCells = [];

        #region DATA BUILDING

        // Make the triangles.
        var triangulation = delaunay.BowyerWatson(_points);

        // Attempt to cast the distributor as an image distributor for weighted Lloyd settling.
        var distributor = settings.Distributor as DistributorImage;
        var isImgDist = distributor != null;

        // Populate the Graph w. Cells
        //  -> LOYD RELAXATION
        // Requires rebuilding the triangulation and Voronoi cells, which can be a little expensive for large graphs.
        //var pixels = ((DistributorImage)settings.Distributor).GetPixels();
        const int passes = 3;
        for (int i = 0; i < passes; i++)
        {
            Array.Clear(_cellsBySiteId);
            triangulation = delaunay.BowyerWatson(_points);
            PopulateVoronoiCells(triangulation);

            switch (isImgDist)
            {
                case true:
                    delaunay.WeightedLloydSettlePoints(_cellsBySiteId, distributor!.GetPixels(), _width, _height);
                    break;
                default:
                    delaunay.LloydSettlePoints(_cellsBySiteId);
                    break;
            }
        }

        // Checking for Conflicts
        ValidateNeighbors(triangulation); // Automatically removed in Release.

        // Process Cell Culling
        ProcessCells(settings.BoundaryMode, triangulation);

        foreach (Triangle triangle in triangulation)
            AddVoronoiEdges(triangle, _boundaryMode);

        // Build Selected Colors
        BuildRenderArrays();

        // Build Tessellations
        BuildCellGeometry();

        #endregion

        #region VERTEX BATCH BUILDING

        // Save some time. No need to check everything individually if it is certain that Cells isn't the
        // only flag enabled.
        if (settings.Flags != VoronoiRenderingFlags.Cells)
        {
            // Points.
            if ((settings.Flags & VoronoiRenderingFlags.Points) != 0)
                BuildPointBatch(settings.PointSize);

            // Edges.
            if ((settings.Flags & VoronoiRenderingFlags.Edges) != 0)
                BuildEdgeBatch(settings.Thickness);

            // Triangles. (Best not to use these, though. They're ugly.
            // ReSharper disable once PossibleMultipleEnumeration ; False positive.
            if ((settings.Flags & VoronoiRenderingFlags.Triangles) != 0)
                BuildTriangleBatch(triangulation);

        }

        // Cells. (Star of the show.)
        if ((settings.Flags & VoronoiRenderingFlags.Cells) != 0)
        {
            BuildCellVertexBatch();
            BuildCellColorBatch([]);
        }

        #endregion

        _isGenerated = true;

        (Texture2D PixelMap, Texture2D? OverlayMap) output = CreateTextureMaps(
            settings.BoundaryMode,
            settings.Flags,
            settings.Thickness,
            settings.PointSize
        );
        ReleaseTransientGeometry();

        // Update the existing internal pixel map with visual changes.
        return output;
    }

    ~Voronoi() => ReleaseTransientGeometry();

    private void ReleaseTransientGeometry()
    {
        _points = [];

        _cellsBySiteId = [];

        _renderingCells = [];

        _cellGeometryOffsets = [];
        _cellGeometryCounts = [];
        _cellGeometry = [];

        _cellBatchVertices = [];
        _edgeBatchVertices = [];
        _pointBatchVertices = [];
        _triangleBatchVertices = [];

        _cellVoronoiEdges.Clear();
        _cellVoronoiEdges.TrimExcess();

        _voronoiEdges.Clear();
        _voronoiEdges.TrimExcess();

        _edgesByCell = [];
    }
}