using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable once UnusedType.Global
public sealed class HeightmapPointDistributor : IPointDistributor
{
    private readonly double[,] _heightmap;
    private readonly double _exponent;

    public HeightmapPointDistributor(double[,] heightmap, double exponent = 1.0)
    {
        _heightmap = heightmap;
        _exponent = exponent;
    }

    public void Generate(ref List<Point> points, int amount, float maxX, float maxY, double randomness)
    {
        int width = _heightmap.GetLength(0);
        int height = _heightmap.GetLength(1);

        Random random = Random.Shared;
        while (points.Count < amount)
        {
            float x = (float)(random.NextDouble() * maxX);
            float y = (float)(random.NextDouble() * maxY);

            int px = Math.Clamp((int)(x / maxX * width), 0, width - 1);
            int py = Math.Clamp((int)(y / maxY * height), 0, height - 1);
            double terrainHeight = _heightmap[px, py];
            terrainHeight = Math.Clamp(terrainHeight, 0.0, 1.0);
            double density = Math.Pow(terrainHeight, _exponent);
            if (random.NextDouble() > density)
                continue;

            points.Add(new Point(x, y));
        }
    }
}