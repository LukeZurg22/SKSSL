using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Clipper2Lib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi.PointDistributors;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    /// Map geometry is non-negative, so this is equivalent to Math.Round(value)
    /// for the normal coordinate range while avoiding the comparatively expensive
    /// Math.Round call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long FastRoundToLong(double value) => (long)(value + 0.5);


    private static GapGeometry BuildGapGeometry(
        IReadOnlyList<GapPolygon> polygons,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
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

    private unsafe void BuildTriangleBatch(IEnumerable<Triangle> triangulation)
    {
        // Fast path for countable collections.
        if (triangulation is ICollection<Triangle> collection)
        {
            int count = collection.Count;
            int requiredVertices = count * 3;

            if (_triangleBatchVertices.Length < requiredVertices)
            {
                _triangleBatchVertices =
                    GC.AllocateUninitializedArray<VertexPositionColor>(requiredVertices);
            }

            VertexPositionColor[] output = _triangleBatchVertices;

            int vertexIndex = 0;
            uint randomState = unchecked(
                (uint)Environment.TickCount ^
                (uint)RuntimeHelpers.GetHashCode(this));

            fixed (VertexPositionColor* vertices = output)
            {
                foreach (Triangle triangle in collection)
                {
                    uint random = NextRandom(ref randomState);
                    int rgb = (int)(random & 0x00FFFFFF);

                    Color color = new(
                        (byte)(rgb >> 16),
                        (byte)(rgb >> 8),
                        (byte)rgb,
                        (byte)220);

                    Point v0 = triangle.Vertices[0];
                    Point v1 = triangle.Vertices[1];
                    Point v2 = triangle.Vertices[2];

                    VertexPositionColor* dst = vertices + vertexIndex;

                    dst[0].Position = new Vector3(v0.X, v0.Y, 0f);
                    dst[0].Color = color;

                    dst[1].Position = new Vector3(v1.X, v1.Y, 0f);
                    dst[1].Color = color;

                    dst[2].Position = new Vector3(v2.X, v2.Y, 0f);
                    dst[2].Color = color;

                    vertexIndex += 3;
                }
            }

            _triangleBatchPrimitiveCount = count;
            return;
        }

        // Non-countable enumerable: grow the actual vertex buffer directly.
        var verticesB = _triangleBatchVertices;

        int vertexIndexNonCounted = 0;
        int triangleCount = 0;

        uint state = unchecked(
            (uint)Environment.TickCount ^
            (uint)RuntimeHelpers.GetHashCode(this));

        foreach (Triangle triangle in triangulation)
        {
            if (vertexIndexNonCounted + 3 > verticesB.Length)
            {
                int oldLength = verticesB.Length;
                int newLength = oldLength == 0 ? 12 : oldLength << 1;

                if (newLength < vertexIndexNonCounted + 3)
                    newLength = vertexIndexNonCounted + 3;

                Array.Resize(ref verticesB, newLength);
            }

            uint random = NextRandom(ref state);
            int rgb = (int)(random & 0x00FFFFFF);

            Color color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, (byte)220);

            Point v0 = triangle.Vertices[0];
            Point v1 = triangle.Vertices[1];
            Point v2 = triangle.Vertices[2];

            verticesB[vertexIndexNonCounted + 0].Position = new Vector3(v0.X, v0.Y, 0f);
            verticesB[vertexIndexNonCounted + 0].Color = color;

            verticesB[vertexIndexNonCounted + 1].Position = new Vector3(v1.X, v1.Y, 0f);
            verticesB[vertexIndexNonCounted + 1].Color = color;

            verticesB[vertexIndexNonCounted + 2].Position = new Vector3(v2.X, v2.Y, 0f);
            verticesB[vertexIndexNonCounted + 2].Color = color;

            vertexIndexNonCounted += 3;
            triangleCount++;
        }

        _triangleBatchVertices = verticesB;
        _triangleBatchPrimitiveCount = triangleCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint NextRandom(ref uint state)
    {
        // xor-shift-32
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
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
        {
            _edgeBatchVertices = GC.AllocateUninitializedArray<VertexPositionColor>(requiredVertices);
        }

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

    private Vector3[] _cellGeometry = [];
    private int[] _cellGeometryOffsets = [];
    private int[] _cellGeometryCounts = [];

    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private unsafe void BuildCellBatch(
        HashSet<uint> overrideIds,
        bool ignoreFlatColor = false,
        (Color cell, Color blank)? @override = null)
    {
        int cellCount = _voronoiCellArray.Length;
        if (cellCount == 0)
        {
            _cellBatchPrimitiveCount = 0;
            return;
        }

        int totalVertices = _cellGeometry.Length;

        if (totalVertices == 0)
        {
            _cellBatchPrimitiveCount = 0;
            return;
        }

        if (_cellBatchVertices.Length < totalVertices)
            _cellBatchVertices = GC.AllocateUninitializedArray<VertexPositionColor>(totalVertices);

        VertexPositionColor[] output = _cellBatchVertices;
        Vector3[] geometry = _cellGeometry;
        int[] offsets = _cellGeometryOffsets;
        int[] counts = _cellGeometryCounts;
        Color[] rawColors = _cellRawColors;
        VoronoiCell[] cells = _voronoiCellArray;

        bool hasOverride = @override.HasValue;
        Color overrideCell = default;
        Color overrideBlank = default;

        if (hasOverride)
        {
            (overrideCell, overrideBlank) = @override.GetValueOrDefault();
        }

        fixed (VertexPositionColor* outputPtr = output)
        fixed (Vector3* geometryPtr = geometry)
        {
            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                int count = counts[cellIndex];

                if (count == 0)
                    continue;

                Color color;

                if (!hasOverride) color = rawColors[cellIndex];
                else
                {
                    uint id = cells[cellIndex].ID;

                    if (overrideIds.Contains(id)) color = overrideCell;
                    else if (ignoreFlatColor) color = rawColors[cellIndex];
                    else color = overrideBlank;
                }

                int start = offsets[cellIndex];
                //int end = start + count;

                VertexPositionColor* dst = outputPtr + start;
                Vector3* src = geometryPtr + start;

                for (int i = 0; i < count; i++)
                {
                    dst[i].Position = src[i];
                    dst[i].Color = color;
                }
            }
        }

        _cellBatchPrimitiveCount = totalVertices / 3;
    }
}