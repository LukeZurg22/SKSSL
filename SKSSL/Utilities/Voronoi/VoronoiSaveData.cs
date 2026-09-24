using System.Collections.Generic;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace SKSSL.Utilities.Voronoi;

internal sealed class VoronoiSaveData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public Voronoi.VoronoiBoundaryMode BoundaryMode { get; set; }
    public List<PointData> Points { get; set; } = [];
    public List<CellData> Cells { get; set; } = [];
    public List<EdgeData> Edges { get; set; } = [];
    public List<CellEdgeData> CellEdges { get; set; } = [];

    public ColorData[] RawColors { get; set; } = [];
    public ColorData[] OverrideColors { get; set; } = [];
}

internal sealed class PointData
{
    public float X { get; set; }
    public float Y { get; set; }
}

internal sealed class CellData
{
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsBoundary { get; set; }
    public List<PointData> Vertices { get; set; } = [];
}

internal sealed class EdgeData
{
    public PointData Point1 { get; set; } = new();
    public PointData Point2 { get; set; } = new();
}

internal sealed class CellEdgeData
{
    public PointData SiteA { get; set; } = new();
    public PointData? SiteB { get; set; }
    public PointData Point1 { get; set; } = new();
    public PointData Point2 { get; set; } = new();
}

internal sealed class ColorData
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }
}