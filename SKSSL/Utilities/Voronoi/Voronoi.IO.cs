/*using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;

// ReSharper disable UnusedMember.Global

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    public void Save(string filePath) // TODO: Add test case for Voronoi Save()
    {
        var data = new VoronoiSaveData
        {
            Width = _width,
            Height = _height,
            BoundaryMode = _boundaryMode,

            Points = _points
                .Select(p => new PointData { X = p.X, Y = p.Y })
                .ToList(),

            Cells = _cellDictionary.Values
                .Select(c => new CellData
                {
                    X = c.Site.X,
                    Y = c.Site.Y,
                    IsBoundary = c.IsBoundary,
                    Vertices = c.Vertices
                        .Select(v => new PointData { X = v.X, Y = v.Y })
                        .ToList()
                })
                .ToList(),

            Edges = _voronoiEdges
                .Select(e => new EdgeData
                {
                    Point1 = new PointData { X = e.Point1.X, Y = e.Point1.Y },
                    Point2 = new PointData { X = e.Point2.X, Y = e.Point2.Y }
                })
                .ToList(),

            CellEdges = _cellVoronoiEdges
                .Select(e => new CellEdgeData
                {
                    SiteA = new PointData { X = e.SiteA.X, Y = e.SiteA.Y },
                    SiteB = e.SiteB.HasValue
                        ? new PointData { X = e.SiteB.Value.X, Y = e.SiteB.Value.Y }
                        : null,
                    Point1 = new PointData { X = e.Point1.X, Y = e.Point1.Y },
                    Point2 = new PointData { X = e.Point2.X, Y = e.Point2.Y }
                })
                .ToList(),

            RawColors = _cellRawColors
                .Select(c => new ColorData { R = c.R, G = c.G, B = c.B, A = c.A })
                .ToArray(),

            OverrideColors = _cellOverrideColors
                .Select(c => new ColorData { R = c.R, G = c.G, B = c.B, A = c.A })
                .ToArray()
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, _serializerOptions));
    }

    public void Load(string filePath) // TODO: Add test case for Voronoi Load()
    {
        var data = JsonSerializer.Deserialize<VoronoiSaveData>(File.ReadAllText(filePath));
        if (data == null)
            throw new InvalidDataException("Invalid Voronoi save file.");

        _width = data.Width;
        _height = data.Height;
        _boundaryMode = data.BoundaryMode;
        _points = data.Points.Select(p => new Point(p.X, p.Y)).ToArray();

        var pointLookup = _points.ToDictionary(p => (p.X, p.Y));

        _cellDictionary.Clear();
        _voronoiEdges.Clear();
        _cellVoronoiEdges.Clear();
        Array.Clear(_edgesByCell);
        _cellsById.Clear();

        foreach (VoronoiCell cell in _cellDictionary.Values)
            _cellsById[cell.ID] = cell;

        BuildCellArray();
        BuildCellGeometry();

        foreach (CellData savedCell in data.Cells)
        {
            Point site = pointLookup[(savedCell.X, savedCell.Y)];
            var vertices = savedCell.Vertices.Select(v => new Point(v.X, v.Y)).ToList();
            _cellDictionary[site] = new VoronoiCell(site, vertices) { IsBoundary = savedCell.IsBoundary };
        }

        foreach (EdgeData edge in data.Edges)
        {
            var a = new Point(edge.Point1.X, edge.Point1.Y);
            var b = new Point(edge.Point2.X, edge.Point2.Y);
            _voronoiEdges.Add(new Edge(a, b));
        }

        foreach (CellEdgeData edge in data.CellEdges)
        {
            Point siteA = pointLookup[(edge.SiteA.X, edge.SiteA.Y)];
            Point? siteB = edge.SiteB != null ? pointLookup[(edge.SiteB.X, edge.SiteB.Y)] : null;

            var point1 = new Point(edge.Point1.X, edge.Point1.Y);
            var point2 = new Point(edge.Point2.X, edge.Point2.Y);
            var cellVoronoiEdge = new CellVoronoiEdge(siteA, siteB, point1, point2);
            _cellVoronoiEdges.Add(cellVoronoiEdge);
        }

        _cellRawColors = data.RawColors
            .Select(c => new Color(c.R, c.G, c.B, c.A))
            .ToArray();

        _cellOverrideColors = data.OverrideColors
            .Select(c => new Color(c.R, c.G, c.B, c.A))
            .ToArray();

        BuildSelectCellEdges();
        _spatialGrid = SpatialGrid.FactoryMakeBuildSpatialGrid(_renderingCells, _width, _height);

        _isGenerated = true;
        _textureValid = false;

        UpdateTexture(
            _boundaryMode,
            VoronoiRenderingFlags.Cells,
            _previousThickness,
            _previousPointSize,
            true);
    }
}*/