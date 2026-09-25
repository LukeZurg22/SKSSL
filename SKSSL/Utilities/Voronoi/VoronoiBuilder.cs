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
    private static GapGeometry BuildGapGeometry(
        IReadOnlyList<GapPolygon> polygons,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight)
    {
        if (polygons.Count == 0)
            return new GapGeometry([]);

        var scaleX = (double)targetWidth / sourceWidth;
        var scaleY = (double)targetHeight / sourceHeight;

        var paths = new Paths64(polygons.Count);

        foreach (GapPolygon polygon in polygons)
        {
            var vertexCount = polygon.Vertices.Count;

            if (vertexCount < 3)
                continue;

            var path = new Path64(vertexCount);

            foreach (Vector2 vertex in polygon.Vertices)
            {
                path.Add(new Point64(
                    (long)Math.Round(vertex.X * scaleX),
                    (long)Math.Round(vertex.Y * scaleY)));
            }

            paths.Add(path);
        }

        if (paths.Count == 0)
            return new GapGeometry([]);

        Paths64 unioned = Clipper.Union(paths, FillRule.EvenOdd);

        return new GapGeometry(unioned);
    }

    private void BuildPointBatch(float size)
    {
        // Worst case: every point survives culling and produces 6 vertices.
        var vertices = new VertexPositionColor[_points.Length * 6];

        var half = size * 0.5f;
        var vertexIndex = 0;

        Color color = _pointColor;

        foreach (Point point in _points)
        {
            if (ShouldCullBoundarySite(point))
                continue;

            var x = point.X;
            var y = point.Y;

            Vector3 a = new(x - half, y - half, 0f);
            Vector3 b = new(x + half, y - half, 0f);
            Vector3 c = new(x + half, y + half, 0f);
            Vector3 d = new(x - half, y + half, 0f);

            vertices[vertexIndex++] = new VertexPositionColor(a, color);
            vertices[vertexIndex++] = new VertexPositionColor(b, color);
            vertices[vertexIndex++] = new VertexPositionColor(c, color);

            vertices[vertexIndex++] = new VertexPositionColor(a, color);
            vertices[vertexIndex++] = new VertexPositionColor(c, color);
            vertices[vertexIndex++] = new VertexPositionColor(d, color);
        }

        if (vertexIndex != vertices.Length)
            Array.Resize(ref vertices, vertexIndex);

        _pointBatchVertices = vertices;
        _pointBatchPrimitiveCount = vertexIndex / 3;
    }

    private void BuildTriangleBatch(IEnumerable<Triangle> triangulation)
    {
        // Avoid LINQ ToList/Count where possible.
        int count;

        switch (triangulation)
        {
            case ICollection<Triangle> collection:
                count = collection.Count;
                break;
            case IReadOnlyCollection<Triangle> readOnlyCollection:
                count = readOnlyCollection.Count;
                break;
            default:
            {
                // We need materialization for a non-countable enumerable.
                var list = new List<Triangle>();

                foreach (Triangle triangle in triangulation)
                    list.Add(triangle);

                triangulation = list;
                count = list.Count;
                break;
            }
        }

        var vertices = new VertexPositionColor[count * 3];

        var vertexIndex = 0;
        Random random = Random.Shared;

        foreach (Triangle triangle in triangulation)
        {
            var rgb = random.Next(0x1000000);

            Color color = new(
                (rgb >> 16) & 0xFF,
                (rgb >> 8) & 0xFF,
                rgb & 0xFF,
                220);

            Point v0 = triangle.Vertices[0];
            Point v1 = triangle.Vertices[1];
            Point v2 = triangle.Vertices[2];

            vertices[vertexIndex++] = new VertexPositionColor(
                new Vector3(v0.X, v0.Y, 0f),
                color);

            vertices[vertexIndex++] = new VertexPositionColor(
                new Vector3(v1.X, v1.Y, 0f),
                color);

            vertices[vertexIndex++] = new VertexPositionColor(
                new Vector3(v2.X, v2.Y, 0f),
                color);
        }

        _triangleBatchVertices = vertices;
        _triangleBatchPrimitiveCount = count;
    }

    private void BuildEdgeBatch(float thickness)
    {
        var vertices = new VertexPositionColor[_voronoiEdges.Count * 6];

        var halfThickness = thickness * 0.5f;

        var vertexIndex = 0;

        foreach (Edge edge in _voronoiEdges)
        {
            Point p1 = edge.Point1;
            Point p2 = edge.Point2;

            var startX = p1.X;
            var startY = p1.Y;
            var endX = p2.X;
            var endY = p2.Y;

            var dx = endX - startX;
            var dy = endY - startY;

            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared <= 0.000001f)
                continue;

            var inverseLength = 1f / MathF.Sqrt(lengthSquared);

            // Normalized perpendicular.
            var px = -dy * inverseLength * halfThickness;
            var py = dx * inverseLength * halfThickness;

            Vector3 a = new(startX + px, startY + py, 0f);
            Vector3 b = new(startX - px, startY - py, 0f);
            Vector3 c = new(endX - px, endY - py, 0f);
            Vector3 d = new(endX + px, endY + py, 0f);

            vertices[vertexIndex++] = new VertexPositionColor(a, _edgeColor);
            vertices[vertexIndex++] = new VertexPositionColor(b, _edgeColor);
            vertices[vertexIndex++] = new VertexPositionColor(c, _edgeColor);

            vertices[vertexIndex++] = new VertexPositionColor(a, _edgeColor);
            vertices[vertexIndex++] = new VertexPositionColor(c, _edgeColor);
            vertices[vertexIndex++] = new VertexPositionColor(d, _edgeColor);
        }

        if (vertexIndex != vertices.Length)
            Array.Resize(ref vertices, vertexIndex);

        _edgeBatchVertices = vertices;
        _edgeBatchPrimitiveCount = vertexIndex / 3;
    }

    private Vector3[] _cellGeometry = [];
    private int[] _cellGeometryOffsets = [];
    private int[] _cellGeometryCounts = [];

    [SuppressMessage("ReSharper", "SuggestVarOrType_Elsewhere")]
    private void BuildCellBatch(
        HashSet<uint> overrideIds,
        bool ignoreFlatColor = false,
        (Color cell, Color blank)? @override = null)
    {
        int cellCount = _voronoiCellArray.Length;

        if (cellCount == 0)
        {
            _cellBatchVertices = [];
            _cellBatchPrimitiveCount = 0;
            return;
        }

        int totalVertices = _cellGeometry.Length;

        if (_cellBatchVertices.Length < totalVertices)
            _cellBatchVertices = new VertexPositionColor[totalVertices];

        bool hasOverride = @override.HasValue;

        Color overrideCell = default;
        Color overrideBlank = default;
        if (hasOverride && @override != null)
            (overrideCell, overrideBlank) = @override.Value;

        VertexPositionColor[] output = _cellBatchVertices;
        Vector3[] geometry = _cellGeometry;
        int[] offsets = _cellGeometryOffsets;
        int[] counts = _cellGeometryCounts;
        Color[] colors = _cellRawColors;
        VoronoiCell[] cells = _voronoiCellArray;

        for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            int count = counts[cellIndex];
            if (count == 0)
                continue;

            Color color;

            if (!hasOverride)
            {
                color = colors[cellIndex];
            }
            else
            {
                uint id = cells[cellIndex].ID;

                if (overrideIds.Contains(id))
                    color = overrideCell;
                else if (!ignoreFlatColor)
                    color = overrideBlank;
                else
                    color = colors[cellIndex];
            }

            int sourceIndex = offsets[cellIndex];
            int end = sourceIndex + count;

            for (int destinationIndex = sourceIndex; destinationIndex < end; destinationIndex++)
            {
                output[destinationIndex] = new VertexPositionColor(geometry[destinationIndex], color);
            }
        }

        _cellBatchPrimitiveCount = totalVertices / 3;
    }
}