using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private Vector3[] _cellGeometry = [];
    private int[] _cellGeometryOffsets = [];
    private int[] _cellGeometryCounts = [];
    private uint _firstArtificialId;

    private unsafe void BuildPointBatch(float size)
    {
        int pointCount = _points.Length;

        if (pointCount == 0)
        {
            _pointBatchPrimitiveCount = 0;
            return;
        }

        int requiredVertices = pointCount * 6;

        if (_pointBatchVertices.Length < requiredVertices)
            _pointBatchVertices = GC.AllocateUninitializedArray<VertexPositionColor>(requiredVertices);

        var vertices = _pointBatchVertices;
        float half = size * 0.5f;
        int vertexIndex = 0;
        fixed (VertexPositionColor* output = vertices)
        {
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                Point point = _points[pointIndex];

                if (ShouldCullBoundarySite(point))
                    continue;

                float x = point.X;
                float y = point.Y;

                var dst = output + vertexIndex;

                dst[0].Position = new Vector3(x - half, y - half, 0f);
                dst[0].Color = _pointColor;

                dst[1].Position = new Vector3(x + half, y - half, 0f);
                dst[1].Color = _pointColor;

                dst[2].Position = new Vector3(x + half, y + half, 0f);
                dst[2].Color = _pointColor;

                dst[3].Position = dst[0].Position;
                dst[3].Color = _pointColor;

                dst[4].Position = dst[2].Position;
                dst[4].Color = _pointColor;

                dst[5].Position = new Vector3(x - half, y + half, 0f);
                dst[5].Color = _pointColor;

                vertexIndex += 6;
            }
        }

        _pointBatchPrimitiveCount = vertexIndex / 3;
    }

    private void BuildTriangleBatch(IEnumerable<Triangle> triangles)
    {
        var vertices = new List<VertexPositionColor>();
        foreach (Triangle triangle in triangles)
        {
            // The first four sites are artificial/super-triangle boundary sites.
            // They are required for Delaunay construction but are not part of
            // the actual diagram.
            if (_boundaryMode == VoronoiBoundaryMode.CulledSquare && IsArtificialBoundaryTriangle(triangle))
                continue;

            Point a = triangle.Vertices[0];
            Point b = triangle.Vertices[1];
            Point c = triangle.Vertices[2];

            vertices.Add(new VertexPositionColor(new Vector3(a.X, a.Y, 0f), Color.White));
            vertices.Add(new VertexPositionColor(new Vector3(b.X, b.Y, 0f), Color.White));
            vertices.Add(new VertexPositionColor(new Vector3(c.X, c.Y, 0f), Color.White));
        }

        _triangleBatchVertices = vertices.ToArray();
        _triangleBatchPrimitiveCount = vertices.Count / 3;
    }

    private unsafe void BuildEdgeBatch(float thickness)
    {
        int edgeCount = _voronoiEdges.Count;
        if (edgeCount == 0)
        {
            _edgeBatchPrimitiveCount = 0;
            return;
        }

        int requiredVertices = edgeCount * 6;
        if (_edgeBatchVertices.Length < requiredVertices)
            _edgeBatchVertices = GC.AllocateUninitializedArray<VertexPositionColor>(requiredVertices);

        var vertices = _edgeBatchVertices;
        float halfThickness = thickness * 0.5f;
        int vertexIndex = 0;

        fixed (VertexPositionColor* output = vertices)
        {
            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                Edge edge = _voronoiEdges[edgeIndex];

                Point p1 = edge.Point1;
                Point p2 = edge.Point2;

                float startX = p1.X;
                float startY = p1.Y;
                float endX = p2.X;
                float endY = p2.Y;

                float dx = endX - startX;
                float dy = endY - startY;

                float lengthSquared = dx * dx + dy * dy;

                if (lengthSquared <= 0.000001f)
                    continue;

                float inverseLength = 1f / MathF.Sqrt(lengthSquared);

                float px = -dy * inverseLength * halfThickness;
                float py = dx * inverseLength * halfThickness;

                float ax = startX + px;
                float ay = startY + py;

                float bx = startX - px;
                float by = startY - py;

                float cx = endX - px;
                float cy = endY - py;

                float dx2 = endX + px;
                float dy2 = endY + py;

                var dst = output + vertexIndex;

                dst[0].Position = new Vector3(ax, ay, 0f);
                dst[0].Color = _edgeColor;

                dst[1].Position = new Vector3(bx, by, 0f);
                dst[1].Color = _edgeColor;

                dst[2].Position = new Vector3(cx, cy, 0f);
                dst[2].Color = _edgeColor;

                dst[3].Position = dst[0].Position;
                dst[3].Color = _edgeColor;

                dst[4].Position = dst[2].Position;
                dst[4].Color = _edgeColor;

                dst[5].Position = new Vector3(dx2, dy2, 0f);
                dst[5].Color = _edgeColor;

                vertexIndex += 6;
            }
        }

        _edgeBatchPrimitiveCount = vertexIndex / 3;
    }

    private unsafe void BuildCellVertexBatch()
    {
        int totalVertices = _cellGeometry.Length;
        if (totalVertices == 0)
        {
            _cellBatchVertices = [];
            _cellBatchPrimitiveCount = 0;
            return;
        }

        if (_cellBatchVertices.Length < totalVertices)
            _cellBatchVertices = GC.AllocateUninitializedArray<VertexPositionColor>(totalVertices);

        var output = _cellBatchVertices;
        var geometry = _cellGeometry;

        fixed (VertexPositionColor* outputPtr = output)
        fixed (Vector3* geometryPtr = geometry)
            for (int i = 0; i < totalVertices; i++)
                outputPtr[i].Position = geometryPtr[i]; // Destination = Source

        _cellBatchPrimitiveCount = totalVertices / 3;
    }

    private void BuildVoronoiEdges(List<Triangle> triangulation) // TODO: RenderDebugEdgesThroughGaps +--> Settings
    {
        _voronoiEdges.Clear();
        _cellVoronoiEdges.Clear();
        foreach (Triangle triangle in triangulation)
        {
            AddVoronoiEdges(triangle, _boundaryMode);
        }
    }

    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private unsafe void BuildCellColorBatch(
        HashSet<uint> overrideIds,
        bool flattenOthers = false,
        (Color cell, Color blank)? @override = null)
    {
        int cellCount = _renderingCells.Length;
        if (cellCount == 0 || _cellBatchVertices.Length == 0)
        {
            _cellBatchPrimitiveCount = 0;
            return;
        }

        VertexPositionColor[] output = _cellBatchVertices;
        int[] offsets = _cellGeometryOffsets;
        int[] counts = _cellGeometryCounts;
        Color[] colors = _cellRawColors;
        VoronoiCell[] cells = _renderingCells;
        bool hasOverride = @override.HasValue;
        Color overrideCell = default;
        Color overrideBlank = default;

        if (hasOverride && @override != null)
        {
            (overrideCell, overrideBlank) = @override.Value;
        }

        fixed (VertexPositionColor* outputPtr = output)
        {
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                int count = counts[cellIndex];
                if (count == 0)
                    continue;

                Color color;
                if (!hasOverride) color = colors[cellIndex];
                else
                {
                    uint id = cells[cellIndex].ID;
                    if (overrideIds.Contains(id)) color = overrideCell;
                    else if (!flattenOthers) color = colors[cellIndex];
                    else color = overrideBlank;
                }

                int start = offsets[cellIndex];
                //int end = start + count;

                VertexPositionColor* dst = outputPtr + start;
                for (int i = 0; i < count; i++)
                    dst[i].Color = color;
            }
        }

        _cellBatchPrimitiveCount = _cellGeometry.Length / 3;
    }

    private void BuildRenderArrays()
    {
        int realCount = (int)_firstArtificialId;

        int visibleCount = 0;

        // First pass: determine how many renderable cells exist.
        for (int siteId = 0; siteId < realCount; siteId++)
        {
            VoronoiCell? cell = _cellsBySiteId[siteId];
            if (cell != null && !HasRenderableCellGeometry(cell))
            {
                cell.HasRenderableGeometry = false;
                continue;
            }

            visibleCount++;
        }

        if (_renderingCells.Length != visibleCount)
            _renderingCells = new VoronoiCell[visibleCount];

        if (_cellRawColors.Length != visibleCount)
            _cellRawColors = new Color[visibleCount];

        if (_cellOverrideColors.Length != visibleCount)
            _cellOverrideColors = new Color[visibleCount];

        Array.Clear(_cellOverrideColors);

        // Second pass: compact the renderable cells.
        uint nextId = 0;

        for (int siteId = 0; siteId < realCount; siteId++)
        {
            VoronoiCell? cell = _cellsBySiteId[siteId];
            if (cell != null && !cell.HasRenderableGeometry)
            {
                cell.ID = VoronoiCell.InvalidId;
                continue;
            }

            uint cellId = nextId++;

            // Compact public/render ID.
            Debug.Assert(cell != null, nameof(cell) + " != null");
            cell.ID = cellId;
            _renderingCells[cellId] = cell;
            _cellRawColors[cellId] = GetCellColor(cell);
        }
    }


    private void BuildDemarcateCellEdges()
    {
        _edgesByCell = new List<CellVoronoiEdge>?[_renderingCells.Length];

        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            VoronoiCell? cellA = _cellsBySiteId[(int)edge.SiteA.ID];
            if (cellA != null && cellA.ID == VoronoiCell.InvalidId)
                continue;

            Debug.Assert(cellA != null, nameof(cellA) + " != null");
            int idA = (int)cellA.ID;
            (_edgesByCell[idA] ??= []).Add(edge);

            if (!edge.SiteB.HasValue)
                continue;

            VoronoiCell? cellB = _cellsBySiteId[(int)edge.SiteB.Value.ID];
            if (cellB != null && cellB.ID == VoronoiCell.InvalidId)
                continue;

            Debug.Assert(cellB != null, nameof(cellB) + " != null");
            int idB = (int)cellB.ID;
            (_edgesByCell[idB] ??= []).Add(edge);
        }
    }
}