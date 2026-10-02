using System;
using System.Collections.Generic;
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
                if (!_voronoiCells.TryGetValue(site, out VoronoiCell? cell))
                {
                    cell = new VoronoiCell(site, []);
                    _voronoiCells.Add(site, cell);
                    _cellsById.Add(site.ID, cell);
                }

                cell.Vertices.Add(triangle.Circumcenter);
            }

            if (triangle.Neighbor0 == null)
            {
                _voronoiCells[triangle.Vertices[0]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[1]].IsBoundary = true;
            }

            if (triangle.Neighbor1 == null)
            {
                _voronoiCells[triangle.Vertices[1]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[2]].IsBoundary = true;
            }

            // ReSharper disable once InvertIf
            if (triangle.Neighbor2 == null)
            {
                _voronoiCells[triangle.Vertices[2]].IsBoundary = true;
                _voronoiCells[triangle.Vertices[0]].IsBoundary = true;
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
            var neighbors = new Dictionary<Point, HashSet<Point>>(_voronoiCells.Count);
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

            Parallel.ForEach(_voronoiCells.Values, cell =>
            {
                if (IsArtificialBoundarySite(cell.Site))
                {
                    cell.Vertices.Clear();
                    cell.IsCulled = true;
                    return;
                }

                // Start with the entire screen.
                var polygon = new List<Point>(4)
                    { new(0, 0), new(_width, 0), new(_width, _height), new(0, _height) };

                Point site = cell.Site;

                // Intersect the screen with the half-plane
                // containing points closer to `site` than to each neighbor.
                foreach (Point neighbor in neighbors[site])
                {
                    if (polygon.Count < 3)
                        break;

                    double dx = neighbor.X - site.X;
                    double dy = neighbor.Y - site.Y;

                    double c = (double)neighbor.X * neighbor.X +
                               (double)neighbor.Y * neighbor.Y -
                               (double)site.X * site.X -
                               (double)site.Y * site.Y;

                    double a = 2.0 * dx;
                    double b = 2.0 * dy;

                    var clipped = new List<Point>(polygon.Count + 2);
                    Point previous = polygon[^1];
                    double previousValue = a * previous.X + b * previous.Y - c;
                    bool previousInside = previousValue <= 0.0;
                    foreach (Point current in polygon)
                    {
                        double currentValue = a * current.X + b * current.Y - c;
                        bool currentInside = currentValue <= 0.0;
                        if (currentInside)
                        {
                            if (!previousInside)
                            {
                                double denominator = previousValue - currentValue;
                                if (Math.Abs(denominator) > 1e-12)
                                {
                                    double t = previousValue / denominator;
                                    clipped.Add(new Point(
                                        (int)Math.Round(previous.X + (current.X - previous.X) * t),
                                        (int)Math.Round(previous.Y + (current.Y - previous.Y) * t))
                                    );
                                }
                            }

                            clipped.Add(current);
                        }
                        else if (previousInside)
                        {
                            double denominator =
                                previousValue - currentValue;

                            if (Math.Abs(denominator) > 1e-12)
                            {
                                double t = previousValue / denominator;
                                clipped.Add(new Point(
                                    (int)Math.Round(previous.X + (current.X - previous.X) * t),
                                    (int)Math.Round(previous.Y + (current.Y - previous.Y) * t))
                                );
                            }
                        }

                        previous = current;
                        previousValue = currentValue;
                        previousInside = currentInside;
                    }

                    polygon = clipped
                        .Distinct()
                        .ToList();
                }

                cell.Vertices = polygon;
                cell.IsBoundary = false;
                cell.IsCulled = polygon.Count < 3;
            });

            return;
        }

        double centerX = _width * 0.5;
        double centerY = _height * 0.5;
        double radiusX = _width * 0.48;
        double radiusY = _height * 0.48;

        // Existing behavior for Culled / HardEdgeOpen.
        Parallel.ForEach(_voronoiCells.Values, cell =>
        {
            cell.Vertices = cell.Vertices
                .Distinct()
                .ToList();

            SortVerticesAround(cell.Vertices, cell.Site);

            bool touchesOutside = false;
            int padding = 0;

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

            cell.IsCulled = IsArtificialBoundarySite(cell.Site) ||
                            cell.Vertices.Count < 3 ||
                            (boundaryMode is VoronoiBoundaryMode.CulledSquare or VoronoiBoundaryMode.CulledCircular &&
                             cell.IsBoundary);
        });
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
            if (!HasRenderableCellGeometry(cell))
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
}