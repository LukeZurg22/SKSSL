using System;
using System.Collections.Generic;
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
        int sourceWidth, int sourceHeight,
        int targetWidth, int targetHeight)
    {
        if (polygons.Count == 0)
            return new GapGeometry([]);

        double scaleX = (double)targetWidth / sourceWidth;
        double scaleY = (double)targetHeight / sourceHeight;

        var paths = new Paths64(polygons.Count);

        foreach (GapPolygon polygon in polygons)
        {
            if (polygon.Vertices.Count < 3)
                continue;

            var path = new Path64(polygon.Vertices.Count);

            foreach (Vector2 vertex in polygon.Vertices)
            {
                long x = (long)Math.Round(vertex.X * scaleX);
                long y = (long)Math.Round(vertex.Y * scaleY);

                path.Add(new Point64(x, y));
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
        var vertices = new List<VertexPositionColor>(_points.Length * 6);
        float half = size * 0.5f;

        foreach (Point point in _points)
        {
            if (ShouldCullBoundarySite(point))
                continue;


            float x = point.X;
            float y = point.Y;

            Vector3 a = new(x - half, y - half, 0f);
            Vector3 b = new(x + half, y - half, 0f);
            Vector3 c = new(x + half, y + half, 0f);
            Vector3 d = new(x - half, y + half, 0f);

            vertices.Add(new VertexPositionColor(a, _pointColor));
            vertices.Add(new VertexPositionColor(b, _pointColor));
            vertices.Add(new VertexPositionColor(c, _pointColor));

            vertices.Add(new VertexPositionColor(a, _pointColor));
            vertices.Add(new VertexPositionColor(c, _pointColor));
            vertices.Add(new VertexPositionColor(d, _pointColor));
        }

        _pointBatchVertices = vertices.ToArray();
        _pointBatchPrimitiveCount = _pointBatchVertices.Length / 3;
    }

    // ReSharper disable once RedundantAssignment
    private void BuildTriangleBatch(IEnumerable<Triangle> triangulation)
    {
        var triangles = triangulation as ICollection<Triangle> ?? triangulation.ToList();
        _triangleBatchVertices = new VertexPositionColor[triangles.Count * 3];
        int vertexIndex = 0;
        Random random = Random.Shared;
        foreach (Triangle triangle in triangles)
        {
            int rgb = random.Next(0x1000000); // 0 … 16 777 215
            var color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(new Vector3(triangle.Vertices[0].X, triangle.Vertices[0].Y, 0f), color);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(new Vector3(triangle.Vertices[1].X, triangle.Vertices[1].Y, 0f), color);

            _triangleBatchVertices[vertexIndex++] =
                new VertexPositionColor(new Vector3(triangle.Vertices[2].X, triangle.Vertices[2].Y, 0f), color);
        }

        _triangleBatchPrimitiveCount = triangles.Count;
    }


    private void BuildEdgeBatch(float thickness)
    {
        var vertices = new List<VertexPositionColor>(_voronoiEdges.Count * 6);
        float halfThickness = thickness * 0.5f;
        foreach (Edge edge in _voronoiEdges)
        {
            Point p1 = edge.Point1;
            Point p2 = edge.Point2;

            Vector2 start = new(p1.X, p1.Y);
            Vector2 end = new(p2.X, p2.Y);

            Vector2 direction = end - start;
            float lengthSquared = direction.LengthSquared();

            if (lengthSquared <= 0.000001f)
                continue;

            direction *= 1f / MathF.Sqrt(lengthSquared);

            Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * halfThickness;
            Vector3 a = new(start + perpendicular, 0f);
            Vector3 b = new(start - perpendicular, 0f);
            Vector3 c = new(end - perpendicular, 0f);
            Vector3 d = new(end + perpendicular, 0f);

            // Two triangles.
            vertices.Add(new VertexPositionColor(a, _edgeColor));
            vertices.Add(new VertexPositionColor(b, _edgeColor));
            vertices.Add(new VertexPositionColor(c, _edgeColor));

            vertices.Add(new VertexPositionColor(a, _edgeColor));
            vertices.Add(new VertexPositionColor(c, _edgeColor));
            vertices.Add(new VertexPositionColor(d, _edgeColor));
        }

        _edgeBatchVertices = vertices.ToArray();
        _edgeBatchPrimitiveCount = _edgeBatchVertices.Length / 3;
    }

    private void BuildCellBatch(List<uint> overrideIds, bool ignoreFlatColor = false, (Color cell, Color blank)? @override = null)
    {
        var overrideSet = @override != null ? overrideIds.ToHashSet() : null;
        var cells = _voronoiCells.Values.ToArray();
        var cellVertices = new VertexPositionColor[cells.Length][];

        Parallel.For(0, cells.Length, i =>
        {
            VoronoiCell cell = cells[i];

            if (cell.Vertices.Count < 3 &&
                (cell.RenderPaths == null ||
                 cell.RenderPaths.Count == 0))
            {
                cellVertices[i] = [];
                return;
            }

            int cellIndex = _cellIndices[cell.ID];

            Color color;

            if (@override != null)
            {
                if (overrideSet!.Contains(cell.ID))
                    color = @override.Value.cell;
                else if (!ignoreFlatColor)
                    color = @override.Value.blank;
                else
                    color = _cellRawColors[cellIndex];
            }
            else
            {
                color = _cellRawColors[cellIndex];
            }

            if (cell.RenderPaths == null)
            {
                cellVertices[i] =
                    TessellateOriginalCell(
                        cell.Vertices,
                        color);

                return;
            }

            if (cell.RenderPaths.Count == 0)
            {
                cellVertices[i] = [];
                return;
            }

            cellVertices[i] = TessellateClippedCell(cell.RenderPaths, color);
        });

        int totalVertices = cellVertices.Sum(v => v.Length);

        _cellBatchVertices = new VertexPositionColor[totalVertices];

        int offset = 0;
        foreach (var vertices in cellVertices)
        {
            vertices.CopyTo(_cellBatchVertices, offset);
            offset += vertices.Length;
        }

        _cellBatchPrimitiveCount = _cellBatchVertices.Length / 3;
    }
}