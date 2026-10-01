using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

// ReSharper disable once UnusedType.Global
public class DistributorRandomSystem : IPointDistributor
{
    public void Generate(ref List<Point> points, uint amount, float maxX, float maxY, double randomness)
    {
        Random random = Random.Shared;
        for (int i = points.Count; i < amount; i++)
            points.Add(new Point((float)(random.NextDouble() * maxX), (float)(random.NextDouble() * maxY)));
    }
}