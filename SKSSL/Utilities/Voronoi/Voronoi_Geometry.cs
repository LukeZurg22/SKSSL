using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopulateVoronoiCells(List<Triangle> triangulation)
    {
        foreach (Triangle triangle in triangulation)
        {
            foreach (Point site in triangle.Vertices)
            {
                int siteId = (int)site.ID;

                VoronoiCell? cell = _cellsBySiteId[siteId];
                if (cell == null)
                {
                    cell = new VoronoiCell(site, []);
                    _cellsBySiteId[siteId] = cell;
                }

                cell.Vertices.Add(triangle.Circumcenter);
            }

            if (triangle.Neighbor0 == null)
            {
                _cellsBySiteId[(int)triangle.Vertices[0].ID]!.IsBoundary = true;
                _cellsBySiteId[(int)triangle.Vertices[1].ID]!.IsBoundary = true;
            }

            if (triangle.Neighbor1 == null)
            {
                _cellsBySiteId[(int)triangle.Vertices[1].ID]!.IsBoundary = true;
                _cellsBySiteId[(int)triangle.Vertices[2].ID]!.IsBoundary = true;
            }

            if (triangle.Neighbor2 == null)
            {
                _cellsBySiteId[(int)triangle.Vertices[2].ID]!.IsBoundary = true;
                _cellsBySiteId[(int)triangle.Vertices[0].ID]!.IsBoundary = true;
            }
        }
    }

    /// Prepares the original cells before any special culling or other operations.
    private void ProcessCells(VoronoiBoundaryMode boundaryMode, List<Triangle> triangulation)
    {
        _usedColors.Clear();

        if (boundaryMode == VoronoiBoundaryMode.HardEdgeClosed)
        {
            // Build the Delaunay-neighbor graph for the REAL sites only.
            var neighbors = new Dictionary<Point, HashSet<Point>>(_cellsBySiteId.Length);

            foreach (Point point in _points)
            {
                if (!IsArtificialBoundarySite(point))
                    neighbors[point] = [];
            }

            foreach (Triangle triangle in triangulation)
            {
                Point a = triangle.Vertices[0];
                Point b = triangle.Vertices[1];
                Point c = triangle.Vertices[2];

                // Every pair of real points in a Delaunay triangle is a
                // Delaunay neighbor, including pairs from triangles that
                // also contain an artificial boundary point.
                if (!IsArtificialBoundarySite(a) && !IsArtificialBoundarySite(b))
                {
                    neighbors[a].Add(b);
                    neighbors[b].Add(a);
                }

                if (!IsArtificialBoundarySite(a) && !IsArtificialBoundarySite(c))
                {
                    neighbors[a].Add(c);
                    neighbors[c].Add(a);
                }

                if (!IsArtificialBoundarySite(b) && !IsArtificialBoundarySite(c))
                {
                    neighbors[b].Add(c);
                    neighbors[c].Add(b);
                }
            }

            Parallel.ForEach(_cellsBySiteId, cell =>
            {
                Debug.Assert(cell != null, nameof(cell) + " != null");
                if (IsArtificialBoundarySite(cell.Site))
                {
                    cell.Vertices.Clear();
                    cell.RenderPaths = null;
                    cell.IsCulled = true;
                    return;
                }

                // Keep double precision until the entire cell has been clipped.
                var polygon = new List<DoublePoint>(4)
                {
                    new(0.0, 0.0),
                    new(_width, 0.0),
                    new(_width, _height),
                    new(0.0, _height)
                };

                Point site = cell.Site;

                // Deterministic ordering is useful for reproducibility.
                foreach (Point neighbor in neighbors[site].OrderBy(p => p.ID))
                {
                    if (polygon.Count < 3)
                        break;

                    double dx = neighbor.X - site.X;
                    double dy = neighbor.Y - site.Y;

                    double c =
                        (double)neighbor.X * neighbor.X +
                        (double)neighbor.Y * neighbor.Y -
                        (double)site.X * site.X -
                        (double)site.Y * site.Y;

                    double a = 2.0 * dx;
                    double b = 2.0 * dy;

                    polygon = ClipHalfPlane(polygon, a, b, c);
                }

                // Quantize ONCE.
                cell.Vertices = QuantizePolygon(polygon, _width, _height);

                // HardEdgeClosed owns the entire finite cell now.
                cell.RenderPaths = null;
                cell.IsBoundary = false;
                cell.IsCulled = cell.Vertices.Count < 3;
            });

            return;
        }

        double centerX = _width * 0.5;
        double centerY = _height * 0.5;
        double radiusX = _width * 0.48;
        double radiusY = _height * 0.48;

        // Existing behavior for Culled / HardEdgeOpen.
        Parallel.ForEach(_cellsBySiteId, cell =>
        {
            Debug.Assert(cell != null, nameof(cell) + " != null");
            cell.Vertices = cell.Vertices
                .Distinct()
                .ToList();

            SortVerticesAround(cell.Vertices, cell.Site);

            bool touchesOutside = false;
            const int padding = 0; // TODO: Make padding do something.

            switch (boundaryMode)
            {
                case VoronoiBoundaryMode.CulledCircular:
                {
                    // Boundary cells are unbounded, so they cannot be guaranteed
                    // to fit entirely inside the finite circular region.
                    if (cell.IsBoundary)
                    {
                        cell.Vertices.Clear();
                        cell.IsCulled = true;
                        return;
                    }

                    touchesOutside = cell.Vertices.Any(v =>
                    {
                        double dx = (v.X - centerX) / radiusX;
                        double dy = (v.Y - centerY) / radiusY;

                        return dx * dx + dy * dy > 1.0;
                    });

                    break;
                }

                case VoronoiBoundaryMode.CulledSquare:
                {
                    touchesOutside = cell.Vertices.Any(v =>
                        v.X < -padding ||
                        v.X > _width + padding ||
                        v.Y < -padding ||
                        v.Y > _height + padding);

                    break;
                }

                case VoronoiBoundaryMode.HardEdgeClosed:
                case VoronoiBoundaryMode.HardEdgeOpen:
                default: break;
            }

            if (touchesOutside)
            {
                switch (boundaryMode)
                {
                    case VoronoiBoundaryMode.CulledCircular:
                    case VoronoiBoundaryMode.CulledSquare:
                        cell.Vertices.Clear();
                        break;
                    case VoronoiBoundaryMode.HardEdgeOpen:
                    default:
                        // TODO: This code is sketchy. May need adjustment.
                        cell.Vertices = ClipPolygonToBounds(cell.Vertices, _width, _height);
                        break;
                    case VoronoiBoundaryMode.HardEdgeClosed:
                        throw new Exception("Should not have reached HardEdgeClosed when case has been covered.");
                }
            }

            bool cullingMode = boundaryMode is VoronoiBoundaryMode.CulledSquare or VoronoiBoundaryMode.CulledCircular;
            bool cellIsBoundary = cullingMode && (cell.IsBoundary || cell.Vertices.Count < 3);
            cell.IsCulled = IsArtificialBoundarySite(cell.Site) || cellIsBoundary;
        });
    }

    private void BuildCellGeometry()
    {
        int cellCount = _renderingCells.Length;
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
            VoronoiCell cell = _renderingCells[i];
            Vector3[] geometry;

            // The first four Delaunay sites are the artificial bounding rectangle.
            // They are topology scaffolding, not renderable Voronoi cells.
            if (!cell.HasRenderableGeometry)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<DoublePoint> RemoveDuplicatePoints(List<DoublePoint> polygon)
    {
        if (polygon.Count < 2)
            return polygon;

        const double epsilon = 1e-9;

        var result = new List<DoublePoint>(polygon.Count);
        foreach (DoublePoint point in polygon)
        {
            if (result.Count == 0)
            {
                result.Add(point);
                continue;
            }

            DoublePoint previous = result[^1];

            double dx = point.X - previous.X;
            double dy = point.Y - previous.Y;
            if (dx * dx + dy * dy > epsilon * epsilon)
                result.Add(point);
        }

        // ReSharper disable once InvertIf
        if (result.Count > 1)
        {
            DoublePoint first = result[0];
            DoublePoint last = result[^1];

            double dx = first.X - last.X;
            double dy = first.Y - last.Y;
            if (dx * dx + dy * dy <= epsilon * epsilon)
                result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<Point> QuantizePolygon(List<DoublePoint> polygon, int width, int height)
    {
        if (polygon.Count < 3)
            return [];

        var result = new List<Point>(polygon.Count);

        foreach (DoublePoint p in polygon)
        {
            var point = new Point(
                (int)Math.Round(Math.Clamp(p.X, 0.0, width)),
                (int)Math.Round(Math.Clamp(p.Y, 0.0, height)));

            if (result.Count == 0 || result[^1] != point)
                result.Add(point);
        }

        if (result.Count > 1 && result[0] == result[^1])
            result.RemoveAt(result.Count - 1);

        return result;
    }
}