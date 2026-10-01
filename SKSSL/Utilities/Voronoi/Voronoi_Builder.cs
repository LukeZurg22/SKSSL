using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Clipper2Lib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi.PointDistributors;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private Vector3[] _cellGeometry = [];
    private int[] _cellGeometryOffsets = [];
    private int[] _cellGeometryCounts = [];

    private static GapGeometry BuildGapGeometry(
        IReadOnlyList<GapPolygon> polygons,
        int sourceWidth, int sourceHeight,
        int targetWidth, int targetHeight)
    {
        int polygonCount = polygons.Count;
        if (polygonCount == 0)
            return new GapGeometry([]);

        double scaleX = (double)targetWidth / sourceWidth;
        double scaleY = (double)targetHeight / sourceHeight;

        var paths = new Paths64(polygonCount);
        for (int polygonIndex = 0; polygonIndex < polygonCount; polygonIndex++)
        {
            GapPolygon polygon = polygons[polygonIndex];
            var vertices = polygon.Vertices;

            int vertexCount = vertices.Count;
            if (vertexCount < 3)
                continue;

            var path = new Path64(vertexCount);

            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector2 vertex = vertices[vertexIndex];
                path.Add(new Point64(FastRoundToLong(vertex.X * scaleX), FastRoundToLong(vertex.Y * scaleY)));
            }

            paths.Add(path);
        }

        if (paths.Count == 0)
            return new GapGeometry([]);

        Paths64 unioned = Clipper.Union(paths, FillRule.EvenOdd);
        return new GapGeometry(unioned);
    }

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

    private void BuildTriangleBatch(IEnumerable<Triangle> triangles, VoronoiBoundaryMode boundaryMode)
    {
        var vertices = new List<VertexPositionColor>();

        foreach (Triangle triangle in triangles)
        {
            // The first four sites are artificial/super-triangle boundary sites.
            // They are required for Delaunay construction but are not part of
            // the actual diagram.
            if (boundaryMode == VoronoiBoundaryMode.CulledSquare && IsArtificialBoundaryTriangle(triangle))
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

    private void BuildVoronoiEdges(
        List<Triangle> triangulation,
        VoronoiBoundaryMode boundaryMode,
        GapGeometry? gaps,
        bool renderEdgesThroughGaps)
    {
        _voronoiEdges.Clear();
        _cellVoronoiEdges.Clear();

        foreach (Triangle triangle in triangulation)
        {
            AddVoronoiEdges(triangle, boundaryMode);
        }

        if (renderEdgesThroughGaps && gaps is not null && gaps.Paths.Count != 0)
            ClipVoronoiEdgesAgainstGaps(gaps);
    }

    private void ClipVoronoiEdgesAgainstGaps(GapGeometry gaps)
    {
        if (_voronoiEdges.Count == 0)
            return;

        int edgeCount = _voronoiEdges.Count;

        var subjects = new Paths64(edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            Edge edge = _voronoiEdges[i];

            subjects.Add(
            [
                new Point64(edge.Point1.X, edge.Point1.Y),
                new Point64(edge.Point2.X, edge.Point2.Y)
            ]);
        }

        var clipped = new Paths64();
        var openClipped = new Paths64();

        var clipper = new Clipper64();

        clipper.AddOpenSubject(subjects);
        clipper.AddClip(gaps.Paths);
        clipper.Execute(ClipType.Difference, FillRule.EvenOdd, clipped, openClipped);

        _voronoiEdges.Clear();

        foreach (Path64 path in openClipped)
        {
            if (path.Count < 2)
                continue;

            for (int i = 1; i < path.Count; i++)
            {
                Point64 a = path[i - 1];
                Point64 b = path[i];

                if (a == b)
                    continue;

                _voronoiEdges.Add(new Edge(new Point((int)a.X, (int)a.Y), new Point((int)b.X, (int)b.Y)));
            }
        }
    }

    // TODO: Make them "Proper" from 0 depending on mode.
    private static void AssignSpatialIds(List<Point> points)
    {
        // Preserve the four artificial boundary points.
        var realPoints = points
            .Skip(4)
            .OrderBy(p => MortonCode(
                (uint)p.X,
                (uint)p.Y))
            .ToList();

        points.RemoveRange(4, points.Count - 4);

        for (uint i = 0; i < realPoints.Count; i++)
        {
            Point old = realPoints[(int)i];
            points.Add(new Point(old.X, old.Y, i + 4));
        }
    }

    private static ulong MortonCode(uint x, uint y)
    {
        return Part1By1(x) | (Part1By1(y) << 1);

        static ulong Part1By1(uint value)
        {
            ulong x = value;

            x = (x | x << 16) & 0x0000FFFF0000FFFFUL;
            x = (x | x << 8) & 0x00FF00FF00FF00FFUL;
            x = (x | x << 4) & 0x0F0F0F0F0F0F0F0FUL;
            x = (x | x << 2) & 0x3333333333333333UL;
            x = (x | x << 1) & 0x5555555555555555UL;

            return x;
        }
    }

    private unsafe void SetCellColor(uint cellId, Color color)
    {
        for (int cellIndex = 0; cellIndex < _voronoiCellArray.Length; cellIndex++)
        {
            if (_voronoiCellArray[cellIndex].ID != cellId)
                continue;

            int count = _cellGeometryCounts[cellIndex];
            if (count == 0)
                return;

            int start = _cellGeometryOffsets[cellIndex];

            fixed (VertexPositionColor* outputPtr = _cellBatchVertices)
            {
                var dst = outputPtr + start;
                for (int i = 0; i < count; i++)
                    dst[i].Color = color;
            }

            return;
        }
    }

    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private unsafe void BuildCellColorBatch(
        HashSet<uint> overrideIds,
        bool flattenOthers = false,
        (Color cell, Color blank)? @override = null)
    {
        int cellCount = _voronoiCellArray.Length;
        if (cellCount == 0 || _cellBatchVertices.Length == 0)
        {
            _cellBatchPrimitiveCount = 0;
            return;
        }

        VertexPositionColor[] output = _cellBatchVertices;
        int[] offsets = _cellGeometryOffsets;
        int[] counts = _cellGeometryCounts;
        Color[] colors = _cellRawColors;
        VoronoiCell[] cells = _voronoiCellArray;
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

    private void BuildCellArray()
    {
        int count = _cellIndices.Count;
        if (_voronoiCellArray.Length != count)
            _voronoiCellArray = new VoronoiCell[count];

        foreach (VoronoiCell cell in _voronoiCells.Values)
            _voronoiCellArray[_cellIndices[cell.ID]] = cell;
    }

    private void BuildCellGeometry()
    {
        int cellCount = _voronoiCellArray.Length;
        if (cellCount == 0)
        {
            _cellGeometry = [];
            _cellGeometryOffsets = [];
            _cellGeometryCounts = [];
            return;
        }

        var geometries = new Vector3[cellCount][];
        var counts = new int[cellCount];

        Parallel.For(0, cellCount, i =>
        {
            VoronoiCell cell = _voronoiCellArray[i];
            Vector3[] geometry;

            // The first four Delaunay sites are the artificial bounding rectangle.
            // They are topology scaffolding, not renderable Voronoi cells.
            if (cell.IsCulled || !HasRenderableCellGeometry(cell) || IsArtificialBoundarySite(cell.Site))
            {
                geometries[i] = [];
                counts[i] = 0;
                return;
            }

            if (cell.Vertices.Count < 3 &&
                (cell.RenderPaths == null || cell.RenderPaths.Count == 0))
            {
                geometry = [];
            }
            else if (cell.RenderPaths == null)
            {
                geometry = TessellateOriginalCellPositions(cell.Vertices);
            }
            else if (cell.RenderPaths.Count == 0)
            {
                geometry = [];
            }
            else
            {
                geometry = TessellateClippedCellPositions(cell.RenderPaths);
            }

            geometries[i] = geometry;
            counts[i] = geometry.Length;
        });

        int total = 0;
        var offsets = new int[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            offsets[i] = total;
            total += counts[i];
        }

        var geometryBuffer = new Vector3[total];

        for (int i = 0; i < cellCount; i++)
        {
            var source = geometries[i];

            if (source.Length == 0)
                continue;

            source.AsSpan().CopyTo(geometryBuffer.AsSpan(offsets[i]));
        }

        _cellGeometry = geometryBuffer;
        _cellGeometryOffsets = offsets;
        _cellGeometryCounts = counts;
    }

    private void BuildSelectCellEdges()
    {
        long maxSiteId = -1;
        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            maxSiteId = Math.Max(maxSiteId, edge.SiteA.ID);
            if (edge.SiteB.HasValue)
                maxSiteId = Math.Max(maxSiteId, edge.SiteB.Value.ID);
        }

        if (_edgesByCell.Length != maxSiteId + 1)
            _edgesByCell = new List<CellVoronoiEdge>?[maxSiteId + 1];
        else Array.Clear(_edgesByCell);

        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            var edgesA = _edgesByCell[edge.SiteA.ID];
            if (edgesA == null)
            {
                edgesA = [];
                _edgesByCell[edge.SiteA.ID] = edgesA;
            }

            edgesA.Add(edge);
            if (!edge.SiteB.HasValue)
                continue;

            Point siteB = edge.SiteB.Value;
            var edgesB = _edgesByCell[siteB.ID];
            if (edgesB == null)
            {
                edgesB = [];
                _edgesByCell[siteB.ID] = edgesB;
            }

            edgesB.Add(edge);
        }
    }
}