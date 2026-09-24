using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable once UnusedType.Global
public class RandomSystem : IPointDistributor
{
    public void Generate(ref List<Point> points, int amount, double maxX, double maxY, double randomness)
    {
        var random = new Random((int)randomness);
        for (int i = points.Count; i < amount; i++)
            points.Add(new Point(random.NextDouble() * maxX, random.NextDouble() * maxY));
    }
}